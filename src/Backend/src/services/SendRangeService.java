package services;

import db.AccessConnection;
import db.TimeStamp;      // connects to TimeStamp.mdb
import db.ErrorLog;      // persist per-row errors for GUI

import java.io.FileWriter;
import java.io.IOException;
import java.io.PrintWriter;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.sql.*;
import java.time.Duration;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.*;
import java.util.concurrent.ConcurrentHashMap;
import java.util.function.Function;

public class SendRangeService {

    // ----------- Endpoints -------------------------------------------------------
    private static String LOGIN_URL  = "http://localhost:8081/api/Auth/login";
    private static String IMPORT_URL = "http://localhost:8081/api/GL/ImportJournal";

    public static void setLoginUrl(String url)  { if (url != null && !url.isBlank()) LOGIN_URL = url; }
    public static String  getLoginUrl()         { return LOGIN_URL; }
    public static void setImportUrl(String url) { if (url != null && !url.isBlank()) IMPORT_URL = url; }
    public static String  getImportUrl()        { return IMPORT_URL; }

    // ----------- HTTP/TIMEOUT ----------------------------------------------------
    private static final Duration REQ_TIMEOUT = Duration.ofSeconds(8);
    private static final HttpClient HTTP = HttpClient.newBuilder().connectTimeout(REQ_TIMEOUT).build();

    // ----------- Headers (from your cURL) ---------------------------------------
    private static String HDR_ACCEPT   = "text/plain";
    private static String HDR_YEAR     = "2025";
    private static String HDR_ACTIVITY = "1";

    // ----------- TOKEN CACHE + AUTH LOCK ----------------------------------------
    private static volatile String cachedToken;
    private static final Object AUTH_LOCK = new Object();

    // ----------- File Logger -----------------------------------------------------
    private static synchronized void logToFile(String tag, String message) {
        try (FileWriter fw = new FileWriter("log.txt", true)) {
            fw.write("[" + LocalDateTime.now() + "] " + tag + " " + message + System.lineSeparator());
        } catch (IOException e) {
            e.printStackTrace();
        }
    }
    private static void logRequest(String where, String url, String json) {
        logToFile("REQUEST", where + " URL=" + url + " JSON=" + json);
    }
    private static void logResponse(String where, int status, String body) {
        logToFile("RESPONSE", where + " STATUS=" + status + " BODY=" + body);
    }
    private static void logError(String where, String message, Throwable t) {
        logToFile("ERROR", where + " " + message + (t != null ? " EX=" + t.getClass().getSimpleName() + ": " + t.getMessage() : ""));
    }

    // ----------- Auth (login + token extraction) --------------------------------
    /** Logs in EXACTLY like your cURL (headers + body) and returns a token string. */
    public static String loginExact() {
        if (cachedToken != null) return cachedToken;

        String body = "{\"userId\":\"1\",\"password\":\"1\"}";
        HttpRequest req = HttpRequest.newBuilder()
                .uri(URI.create(LOGIN_URL))
                .timeout(REQ_TIMEOUT)
                .header("accept", HDR_ACCEPT)     // text/plain
                .header("year", HDR_YEAR)         // 2025
                .header("activity", HDR_ACTIVITY) // 1
                .header("Content-Type", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(body, StandardCharsets.UTF_8))
                .build();

        logRequest("LOGIN", LOGIN_URL, body);

        try {
            HttpResponse<String> resp = HTTP.send(req, HttpResponse.BodyHandlers.ofString());
            String respBody = resp.body() == null ? "" : resp.body();
            logResponse("LOGIN", resp.statusCode(), respBody);

            if (resp.statusCode() / 100 != 2) {
                throw new RuntimeException("Login failed: " + resp.statusCode() + " " + respBody);
            }
            String token = extractTokenLoose(respBody);
            if (token == null || token.isBlank()) token = respBody.trim().replace("\"", "");
            cachedToken = token;
            return token;
        } catch (Exception e) {
            logError("LOGIN", "Login error", e);
            throw new RuntimeException("Login error: " + e.getMessage(), e);
        }
    }

    // ----------- In-memory last-errors (also persisted via ErrorLog) ------------
    private static final Map<String,String> LAST_ERRORS = new ConcurrentHashMap<>();
    public static Map<String,String> getLastErrorsSnapshot() {
        return new LinkedHashMap<>(LAST_ERRORS); // safe copy for UI
    }
    private static void putError(String ser, String msg) {
        if (ser == null || msg == null || msg.isBlank()) return;
        LAST_ERRORS.put(ser.trim(), msg);
    }
    private static void putErrorPersist(String ser, String msg) {
        putError(ser, msg);
        ErrorLog.upsertError(ser, msg);
    }
    private static void clearErrorPersist(String ser) {
        if (ser == null) return;
        LAST_ERRORS.remove(ser.trim());
        ErrorLog.deleteError(ser.trim());
    }

    // ----------- Main entry: send range -----------------------------------------
    /**
     * Reads pending rows from home.mdb; sends to API; on success stamps TimeStamp.mdb
     * AND updates home.mdb's TR_TimeStamp; on failure logs error per Ser (ErrorLog).
     */
    public static String sendRange(String startDate, String endDate) {
        int ok = 0, fail = 0, done = 0;
        String error = "";
        LAST_ERRORS.clear();  // reset per-run errors

        // Clean up TimeStamp rows that are already Done in home
        String cleanupSummary = cleanupDoneStamps(startDate, endDate);
        logToFile("CLEANUP_RUN", cleanupSummary);

        // 1) Fetch rows (NOT already stamped) from home.mdb
        List<Map<String,Object>> rows;
        try (Connection home = AccessConnection.getConnection()) {
            logError("ANTI_RESEND", "TimeStamp path=" + db.TimeStamp.getPath(), null);
            final String q = buildAntiResendSql(); // uses TR_TimeStamp IS NULL
            try (PreparedStatement ps = home.prepareStatement(q)) {
                ps.setString(1, startDate);
                ps.setString(2, endDate);
                ps.setString(3, startDate);
                ps.setString(4, endDate);
                try (ResultSet rs = ps.executeQuery()) {
                    rows = resultSetToList(rs);
                }
            }
        } catch (Exception e) {
            logError("SEND_RANGE", "Failed fetching rows", e);
            return "{\"ok\":0,\"fail\":0,\"error\":\"fetch_failed\"}";
        }

        final int total = rows.size();
        ensureLoggedIn(); // warm up token

        // 2) For each row: guard, HTTP with auth-retry, stamping, TR_TimeStamp update, error persistence
        for (Map<String,Object> row : rows) {
            final String serStr = String.valueOf(row.get("Ser")).trim();
            try {
                // ---- Guard for missing debit/credit accounts ----
                Object debitAcct  = row.get("DebitAccount1");
                Object creditAcct = row.get("CreditAccount11");      // alias from SQL
                if (creditAcct == null) creditAcct = row.get("CreditAccount1");

                if (isBlank(debitAcct) || isBlank(creditAcct)) {
                    String msg = "يرجى ملء حسابات المدين والدائن";
                    error = error.isEmpty() ? msg : (error + " | " + msg);
                    putErrorPersist(serStr, msg);
                    appendToLog("ERROR: " + msg);
                    fail++; done++; System.out.println("PROGRESS " + done + "/" + total);
                    continue;
                }

                Map<String,Object> journal = JournalBuilder.buildJournalForApi(row);
                if (journal == null) {
                    putErrorPersist(serStr, "تعذر إنشاء القيد");
                    fail++; done++; System.out.println("PROGRESS " + done + "/" + total);
                    continue;
                }

                // Decide source table
                String table = "statement";
                String src = String.valueOf(row.get("src"));
                if ("Gl_Journal".equalsIgnoreCase(src)) table = "Gl_Journal";

                // HARD STOP: if it's already in TimeStamp, skip sending
                if (isAlreadyStamped(table, serStr)) {
                    clearErrorPersist(serStr);
                    done++; System.out.println("PROGRESS " + done + "/" + total);
                    continue;
                }

                // --- HTTP send with auth-refresh-and-retry ---
                String payload = "[" + toJson(journal) + "]";
                HttpResponse<String> resp = withAuthRetry(token -> sendImport(token, payload));
                String body = (resp.body() == null ? "" : resp.body()).trim();

                boolean success = resp.statusCode()/100 == 2 && looksSuccessful(body);
                if (!success) {
                    // Only extract/display Arabic error if code != 0
                    boolean isCodeZero = false;
                    String lower = body.toLowerCase(Locale.ROOT);
                    int i = lower.indexOf("\"code\"");
                    if (i >= 0) {
                        int colon = lower.indexOf(':', i);
                        if (colon > 0) {
                            StringBuilder num = new StringBuilder();
                            for (int j = colon + 1; j < lower.length(); j++) {
                                char ch = lower.charAt(j);
                                if (Character.isWhitespace(ch)) continue;
                                if (Character.isDigit(ch)) { num.append(ch); continue; }
                                break;
                            }
                            if (num.length() > 0 && "0".contentEquals(num)) isCodeZero = true;
                        }
                    }
                    if (!isCodeZero) {
                        String arabic = extractArabicFrom(body);
                        if (!arabic.isEmpty()) { error = arabic; putErrorPersist(serStr, arabic); }
                        else putErrorPersist(serStr, "فشل الإرسال");
                    }
                    System.err.println("FAIL " + serStr + " " + resp.statusCode() + " " + body);
                    fail++; done++; System.out.println("PROGRESS " + done + "/" + total);
                    continue;
                }

                // --- Upsert into TimeStamp.mdb ---
                boolean stamped = false;
                for (int attempt = 0; attempt < 2 && !stamped; attempt++) {
                    try (Connection tsConn = TimeStamp.getConnection()) {
                        tsConn.setAutoCommit(true);
                        ensureTimeStampTable(tsConn);
                        if (upsertTimeStamp(tsConn, serStr, table)) {
                            stamped = true;
                        } else {
                            logError("TimeStamp", "UPSERT affected 0 rows for Ser=" + serStr + " Table=" + table, null);
                        }
                    } catch (SQLException ex) {
                        String msg = String.valueOf(ex.getMessage()).toLowerCase(Locale.ROOT);
                        boolean closed = msg.contains("connection") && msg.contains("closed");
                        if (closed && attempt == 0) continue; // retry once
                        logError("TimeStamp", "SQL error during stamping", ex);
                    } catch (Exception ex) {
                        logError("TimeStamp", "Failed to open TimeStamp.mdb connection", ex);
                        break;
                    }
                }

                if (stamped) {
                    // Update home.mdb TR_TimeStamp so anti-resend stops picking this row
                    updateHomeTRTimeStamp(table, serStr);
                    ok++;
                    clearErrorPersist(serStr);
                    System.out.println("DONE Ser=" + serStr + " (stamped + home TR_TimeStamp updated)");
                } else {
                    fail++;
                    putErrorPersist(serStr, "خطأ في التحديث");
                    System.err.println("FAIL " + serStr + " response ok, but stamping failed (TimeStamp.mdb)");
                }

            } catch (Exception ex) {
                String arabic = extractArabicFrom(String.valueOf(ex.getMessage()));
                if (!arabic.isEmpty()) putErrorPersist(serStr, arabic);
                else putErrorPersist(serStr, "استثناء أثناء الإرسال");

                logError("IMPORT", "Exception while sending Ser=" + row.get("Ser"), ex);
                System.err.println("ERR " + row.get("Ser") + " " + ex.getMessage());
                fail++;
            }

            done++;
            System.out.println("PROGRESS " + done + "/" + total);
        }

        String summary = "{OK : " + ok + " ✓ Fail : " + fail + " X }";
        logToFile("SUMMARY", summary);

        System.out.println("BACKEND ERR MAP SIZE = " + LAST_ERRORS.size());
        LAST_ERRORS.forEach((k,v) -> System.out.println("ERR-BACKEND " + k + " -> " + v));

        return summary;
    }

    // ----------------------- Auth helpers ---------------------------------------
    /** Return a valid token; login if needed. */
    private static String ensureLoggedIn() {
        if (cachedToken != null) return cachedToken;
        synchronized (AUTH_LOCK) {
            if (cachedToken != null) return cachedToken;
            cachedToken = loginExact();
            return cachedToken;
        }
    }
    private static void invalidateToken() {
        synchronized (AUTH_LOCK) { cachedToken = null; }
    }
    /** Wrap a call that needs Authorization and retry once on 401/403. */
    private static <T> T withAuthRetry(Function<String, T> call) {
        String token = ensureLoggedIn();
        try {
            return call.apply(token);
        } catch (HttpUnauthorizedException | HttpForbiddenException first) {
            invalidateToken();
            String newToken = ensureLoggedIn();
            return call.apply(newToken);
        }
    }
    // Tiny exceptions so we can detect 401/403 cleanly.
    static class HttpUnauthorizedException extends RuntimeException { HttpUnauthorizedException(String m){super(m);} }
    static class HttpForbiddenException     extends RuntimeException { HttpForbiddenException(String m){super(m);} }

    /** Centralized HTTP call for Import; throws on 401/403, returns the response otherwise. */
    private static HttpResponse<String> sendImport(String token, String payload) {
        try {
            HttpRequest req = HttpRequest.newBuilder()
                    .uri(URI.create(IMPORT_URL))
                    .timeout(Duration.ofSeconds(25)) // safer for uploads than 8s
                    .header("accept", "application/json")
                    .header("Content-Type", "application/json")
                    .header("Authorization", "Bearer " + token)
                    .header("year", HDR_YEAR)
                    .header("activity", HDR_ACTIVITY)
                    .POST(HttpRequest.BodyPublishers.ofString(payload, StandardCharsets.UTF_8))
                    .build();

            logRequest("IMPORT", IMPORT_URL, payload);
            HttpResponse<String> resp = HTTP.send(req, HttpResponse.BodyHandlers.ofString());
            String body = (resp.body() == null ? "" : resp.body()).trim();
            logResponse("IMPORT", resp.statusCode(), body);

            int sc = resp.statusCode();
            if (sc == 401) throw new HttpUnauthorizedException("401 from import");
            if (sc == 403) throw new HttpForbiddenException("403 from import");
            return resp;
        } catch (IOException | InterruptedException e) {
            throw new RuntimeException(e);
        }
    }
    // success ONLY when the response contains: "code": 0  (or "code":"0")
    private static boolean looksSuccessful(String body) {
        if (body == null) return false;
        // Find: "code" : 0   or   "code" : "0"   (case-insensitive for "code")
        java.util.regex.Matcher m = java.util.regex.Pattern
                .compile("(?i)\\\"code\\\"\\s*:\\s*(\\\"?)(-?\\d+)\\1")
                .matcher(body);
        if (m.find()) {
            return "0".equals(m.group(2));
        }
        return false; // if there's no code field, it's not a success
    }


    // ----------------------- DB / utility helpers -------------------------------
    private static boolean isBlank(Object v) {
        return v == null || String.valueOf(v).trim().isEmpty();
    }
    private static void appendToLog(String message) {
        try (FileWriter fw = new FileWriter("log.txt", true);
             PrintWriter pw = new PrintWriter(fw)) {
            pw.println(java.time.LocalDateTime.now() + " - " + message);
        } catch (IOException e) {
            e.printStackTrace(); // fallback to console
        }
    }

    // Arabic extractor: returns all Arabic snippets joined by " | "
    private static String extractArabicFrom(String text) {
        if (text == null || text.isEmpty()) return "";
        final java.util.regex.Pattern ARABIC = java.util.regex.Pattern.compile("[\\u0600-\\u06FF\\u0750-\\u077F\\u08A0-\\u08FF]+(?:[^\\.\\!\\?\\n\\r\\u061F]*[\\.\\!\\?\\u061F])?");
        java.util.regex.Matcher m = ARABIC.matcher(text);
        StringBuilder out = new StringBuilder();
        while (m.find()) {
            if (out.length() > 0) out.append(" | ");
            out.append(m.group().trim());
        }
        return out.toString();
    }

    private static List<Map<String,Object>> resultSetToList(ResultSet rs) throws SQLException {
        List<Map<String,Object>> list = new ArrayList<>();
        ResultSetMetaData md = rs.getMetaData();
        int cols = md.getColumnCount();
        while (rs.next()) {
            Map<String,Object> row = new LinkedHashMap<>();
            for (int i = 1; i <= cols; i++) {
                row.put(md.getColumnLabel(i), rs.getObject(i));
            }
            list.add(row);
        }
        return list;
    }

    private static boolean isAlreadyStamped(String table, String ser) {
        if (ser == null) return false;
        String s = ser.trim();
        try (Connection ts = db.TimeStamp.getConnection();
             PreparedStatement ps = ts.prepareStatement(
                     "SELECT 1 FROM [TR_TimeStamp] WHERE [Table]=? AND TRIM([Ser])=?")) {
            ps.setString(1, table);
            ps.setString(2, s);
            try (ResultSet rs = ps.executeQuery()) {
                return rs.next();
            }
        } catch (Exception e) {
            logError("STAMP_CHECK", "lookup failed for " + table + "/" + s, e);
            return false; // don’t block if lookup fails
        }
    }

    private static final DateTimeFormatter TS_FMT =
            DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm:ss"); // 19 chars

    /** Ensure [TR_TimeStamp] exists with TEXT columns (compatible with your Long Text/Short Text). */
    private static void ensureTimeStampTable(Connection conn) throws SQLException {
        try (Statement s = conn.createStatement()) {
            s.execute("SELECT TOP 1 [Ser],[Table],[TimeStamp] FROM [TR_TimeStamp]");
        } catch (SQLException e) {
            try (Statement s2 = conn.createStatement()) {
                s2.execute(
                        "CREATE TABLE [TR_TimeStamp] (" +
                                "  [Ser] TEXT(255), " +
                                "  [Table] TEXT(64), " +
                                "  [TimeStamp] TEXT(32)" +
                                ")"
                );
            }
        }
    }

    /** Overwrite if exists (Table,Ser), else insert a new row. */
    private static boolean upsertTimeStamp(Connection conn, String ser, String tableName) throws SQLException {
        final String ts = LocalDateTime.now().format(TS_FMT);

        // 1) Try UPDATE first
        int updated;
        try (PreparedStatement ps = conn.prepareStatement(
                "UPDATE [TR_TimeStamp] SET [TimeStamp]=? WHERE [Table]=? AND [Ser]=?")) {
            ps.setString(1, ts);
            ps.setString(2, tableName);
            ps.setString(3, ser);
            updated = ps.executeUpdate();
        }
        if (updated > 0) return true;

        // 2) INSERT if not found
        try (PreparedStatement ps = conn.prepareStatement(
                "INSERT INTO [TR_TimeStamp] ([Ser],[Table],[TimeStamp]) VALUES (?,?,?)")) {
            ps.setString(1, ser);
            ps.setString(2, tableName);
            ps.setString(3, ts);
            return ps.executeUpdate() > 0;
        } catch (SQLException ex) {
            final String msg = String.valueOf(ex.getMessage()).toLowerCase(Locale.ROOT);
            if ("23000".equals(ex.getSQLState()) || msg.contains("duplicate")) {
                try (PreparedStatement ps2 = conn.prepareStatement(
                        "UPDATE [TR_TimeStamp] SET [TimeStamp]=? WHERE [Table]=? AND [Ser]=?")) {
                    ps2.setString(1, ts);
                    ps2.setString(2, tableName);
                    ps2.setString(3, ser);
                    return ps2.executeUpdate() > 0;
                }
            }
            logError("TimeStamp", "UPSERT insert failed", ex);
            return false;
        }
    }

    private static void updateHomeTRTimeStamp(String srcTable, String ser) {
        final String sql = "UPDATE [" + srcTable + "] SET [TR_TimeStamp] = ? WHERE TRIM([Ser]) = ?";
        try (Connection home = AccessConnection.getConnection();
             PreparedStatement ps = home.prepareStatement(sql)) {
            ps.setString(1, LocalDateTime.now().format(TS_FMT));
            ps.setString(2, ser.trim());
            ps.executeUpdate();
        } catch (SQLException ex) {
            logError("HOME_TR_TS", "Failed updating TR_TimeStamp for " + srcTable + "/" + ser, ex);
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
    }

    private static String toJson(Object v) {
        if (v == null) return "null";
        if (v instanceof Number || v instanceof Boolean) return v.toString();
        if (v instanceof Map<?,?> m) {
            StringBuilder sb = new StringBuilder("{");
            boolean first = true;
            for (var e : m.entrySet()) {
                if (!first) sb.append(',');
                sb.append('"').append(esc(e.getKey().toString())).append("\":").append(toJson(e.getValue()));
                first = false;
            }
            return sb.append('}').toString();
        }
        if (v instanceof Iterable<?> it) {
            StringBuilder sb = new StringBuilder("[");
            boolean first = true;
            for (Object o : it) {
                if (!first) sb.append(',');
                sb.append(toJson(o));
                first = false;
            }
            return sb.append(']').toString();
        }
        return '"' + esc(String.valueOf(v)) + '"';
    }
    private static String esc(String s) {
        return s.replace("\\","\\\\").replace("\"","\\\"")
                .replace("\b","\\b").replace("\f","\\f")
                .replace("\n","\\n").replace("\r","\\r").replace("\t","\\t");
    }

    /** Best-effort token extractor for loose APIs. */
    private static String extractTokenLoose(String body) {
        if (body == null) return null;
        String b = body.trim();
        if (b.startsWith("\"") && b.endsWith("\"") && b.length() > 2) return b.substring(1, b.length()-1);
        String key = "\"token\"";
        int i = b.indexOf(key);
        if (i >= 0) {
            int q1 = b.indexOf('"', i + key.length());
            if (q1 >= 0) {
                int q2 = b.indexOf('"', q1 + 1);
                if (q2 > q1) return b.substring(q1 + 1, q2);
            }
        }
        for (String part : b.split("[\\s\"{}:,]+")) {
            if (part.length() > 20 && part.contains(".")) return part;
        }
        return null;
    }

    // ----------------------- Cleanup & anti-resend SQL ---------------------------
    /**
     * Remove rows from TimeStamp.mdb for which home.mdb has Done=TRUE.
     * If startDate/endDate are non-blank, only rows with [Date] BETWEEN startDate AND endDate are considered.
     * Returns a JSON summary: {"scanned":N,"deleted":M}
     */
    public static String cleanupDoneStamps(String startDate, String endDate) {
        int scanned = 0, deleted = 0;

        boolean hasRange = startDate != null && !startDate.isBlank()
                && endDate   != null && !endDate.isBlank();

        String sqlHome =
                "SELECT [Ser], 'statement' AS TableName FROM [statement] " +
                        "WHERE [TR_Done]=TRUE " +
                        (hasRange ? "AND [Date] BETWEEN ? AND ? " : "") +
                        "UNION ALL " +
                        "SELECT [Ser], 'Gl_Journal' AS TableName FROM [Gl_Journal] " +
                        "WHERE [TR_Done]=TRUE " +
                        (hasRange ? "AND [Date] BETWEEN ? AND ? " : "");

        List<Map<String,Object>> keys = new ArrayList<>();
        try (Connection home = AccessConnection.getConnection();
             PreparedStatement ps = home.prepareStatement(sqlHome)) {

            if (hasRange) {
                ps.setString(1, startDate);
                ps.setString(2, endDate);
                ps.setString(3, startDate);
                ps.setString(4, endDate);
            }
            try (ResultSet rs = ps.executeQuery()) {
                ResultSetMetaData md = rs.getMetaData();
                int cols = md.getColumnCount();
                while (rs.next()) {
                    Map<String,Object> row = new LinkedHashMap<>();
                    for (int i = 1; i <= cols; i++) row.put(md.getColumnLabel(i), rs.getObject(i));
                    keys.add(row);
                }
            }
        } catch (Exception e) {
            logError("CLEANUP", "Failed reading Done rows from home.mdb", e);
            return "{\"scanned\":0,\"deleted\":0,\"error\":\"home_query_failed\"}";
        }

        scanned = keys.size();
        if (scanned == 0) {
            String summary = "{\"scanned\":0,\"deleted\":0}";
            logToFile("CLEANUP", summary);
            return summary;
        }

        try (Connection ts = TimeStamp.getConnection()) {
            ts.setAutoCommit(true);
            ensureTimeStampTable(ts);

            try (PreparedStatement del = ts.prepareStatement(
                    "DELETE FROM [TR_TimeStamp] WHERE [Table]=? AND [Ser]=?")) {
                for (Map<String,Object> k : keys) {
                    String tableName = String.valueOf(k.get("TableName"));
                    Object serObj = k.get("Ser");
                    if (serObj == null) continue;
                    String serStr = serObj.toString().trim();
                    del.setString(1, tableName);
                    del.setString(2, serStr);
                    del.addBatch();
                }
                int[] counts = del.executeBatch();
                for (int c : counts) if (c > 0) deleted += c;
            }
        } catch (Exception e) {
            logError("CLEANUP", "Failed deleting from TimeStamp.mdb", e);
            return "{\"scanned\":" + scanned + ",\"deleted\":" + deleted + ",\"error\":\"timestamp_delete_failed\"}";
        }

        String summary = "{\"scanned\":" + scanned + ",\"deleted\":" + deleted + "}";
        logToFile("CLEANUP", summary);
        return summary;
    }

    /** Anti-resend: uses TR_TimeStamp in home.mdb (null means not sent yet). */
    private static String buildAntiResendSql() {
        return
                "SELECT s.[Ser], s.[Date], s.[Amount], s.[Descr1], s.[Room_no], s.[Rent_no], " +
                        "       s.[DebitAccount1], s.[CreditAccount1] AS CreditAccount11, s.[DebitAccount2], s.[CreditAccount2], " +
                        "       s.[DrcostCenterCode] AS DrcostCenterCode, s.[CrcostCenterCode] AS CrcostCenterCode, " +
                        "       s.[CreditAmount1] AS Credit_Amount1, s.[CreditAmount2] AS Credit_Amount2, " +
                        "       s.[DebitAmount1]  AS Debit_Amount1,  s.[DebitAmount2]  AS Debit_Amount2, " +
                        "       s.[Cr_dtl_ac1] AS Cr_dtl_ac1, s.[Cr_dtl_ac2] AS Cr_dtl_ac2, " +
                        "       s.[Dr_dtl_ac1]  AS Dr_dtl_ac1,  s.[Dr_dtl_ac2]  AS Dr_dtl_ac2, " +
                        "       'statement' AS src " +
                        "FROM [statement] s " +
                        "WHERE s.[Date] BETWEEN ? AND ? AND s.[TR_TimeStamp] IS NULL " +
                        "UNION ALL " +
                        "SELECT g.[Ser], g.[Date], g.[Amount], g.[Descr1], g.[Room_no], g.[Rent_no], " +
                        "       g.[DebitAccount1], g.[CreditAccount1] AS CreditAccount11, g.[DebitAccount2], g.[CreditAccount2], " +
                        "       g.[DrcostCenterCode] AS DrcostCenterCode, g.[CrcostCenterCode] AS CrcostCenterCode, " +
                        "       g.[CreditAmount1] AS Credit_Amount1, g.[CreditAmount2] AS Credit_Amount2, " +
                        "       g.[DebitAmount1]  AS Debit_Amount1,  g.[DebitAmount2]  AS Debit_Amount2, " +
                        "       g.[Cr_dtl_ac1] AS Cr_dtl_ac1, g.[Cr_dtl_ac2] AS Cr_dtl_ac2, " +
                        "       g.[Dr_dtl_ac1]  AS Dr_dtl_ac1,  g.[Dr_dtl_ac2]  AS Dr_dtl_ac2, " +
                        "       'Gl_Journal' AS src " +
                        "FROM [Gl_Journal] g " +
                        "WHERE g.[Date] BETWEEN ? AND ? AND g.[TR_TimeStamp] IS NULL";
    }
}
