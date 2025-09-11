package services;

import db.AccessConnection;
import db.TimeStamp;  // connects to TimeStamp.mdb
import services.JournalBuilder;

import java.io.FileWriter;
import java.io.IOException;
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



    // ----------- TOKEN CACHE -----------------------------------------------------
    private static String cachedToken;

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

    // ----------- Auth ------------------------------------------------------------
    /** Logs in EXACTLY like your cURL (headers + body) and returns a token string. */
    public static String loginExact() {
        if (cachedToken != null) return cachedToken;

        String body = "{\"userId\":\"1\",\"password\":\"1\"}";
        HttpRequest req = HttpRequest.newBuilder()
                .uri(URI.create(LOGIN_URL))
                .timeout(REQ_TIMEOUT)
                .header("accept", HDR_ACCEPT)           // text/plain
                .header("year", HDR_YEAR)               // 2025
                .header("activity", HDR_ACTIVITY)       // 1
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

    /**
     * Reads pending rows from home.mdb; if HTTP import succeeds,
     * INSERT a row into TimeStamp.mdb -> [TimeStamp]([Ser],[Table],[TimeStamp]).
     */
    public static String sendRange(String startDate, String endDate) {
        int ok = 0, fail = 0, done = 0;

        // Clean up TimeStamp rows that are already Done in home
        String cleanupSummary = cleanupDoneStamps(startDate, endDate);
        logToFile("CLEANUP_RUN", cleanupSummary);

        // 1) Fetch rows (NOT already stamped) from home.mdb
        List<Map<String,Object>> rows;
        try (Connection home = AccessConnection.getConnection()) {
            // make __SentKeys reflect current TimeStamp contents in THIS same connection
            logError("ANTI_RESEND", "TimeStamp path=" + db.TimeStamp.getPath(), null);

            final String q =buildAntiResendSql();
            // uses __SentKeys
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
        final String token = loginExact(); // Bearer token

        // 2) For each row: guard against resend, then HTTP, then stamp
        for (Map<String,Object> row : rows) {
            try {
                Map<String,Object> journal = JournalBuilder.buildJournalForApi(row);
                if (journal == null) {
                    fail++; done++; System.out.println("PROGRESS " + done + "/" + total);
                    continue;
                }

                // Decide source table + compute Ser
                String table = "statement";
                String src = String.valueOf(row.get("src"));
                if ("Gl_Journal".equalsIgnoreCase(src)) table = "Gl_Journal";
                final String serStr = String.valueOf(row.get("Ser")).trim();

                // HARD STOP: if it's already in TimeStamp, skip sending
                if (isAlreadyStamped(table, serStr)) {
                    System.out.println("SKIP Ser=" + serStr + " already in TimeStamp");
                    done++; System.out.println("PROGRESS " + done + "/" + total);
                    continue;
                }

                // --- HTTP send ---
                String payload = "[" + toJson(journal) + "]";
                HttpRequest req = HttpRequest.newBuilder()
                        .uri(URI.create(IMPORT_URL))
                        .timeout(REQ_TIMEOUT)
                        .header("accept", "application/json")
                        .header("Content-Type", "application/json")
                        .header("Authorization", "Bearer " + token)
                        .POST(HttpRequest.BodyPublishers.ofString(payload, StandardCharsets.UTF_8))
                        .build();

                logRequest("IMPORT", IMPORT_URL, payload);
                HttpResponse<String> resp = HTTP.send(req, HttpResponse.BodyHandlers.ofString());
                String body = (resp.body() == null ? "" : resp.body()).trim();
                logResponse("IMPORT", resp.statusCode(), body);

                boolean success = resp.statusCode()/100 == 2 && looksSuccessful(body);
                if (!success) {
                    System.err.println("FAIL " + serStr + " " + resp.statusCode() + " " + body);
                    fail++; done++; System.out.println("PROGRESS " + done + "/" + total);
                    continue;
                }

                // --- Upsert into TimeStamp ---
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
                    ok++;
                    System.out.println("DONE Ser=" + serStr + " (logged in TimeStamp.mdb)");
                } else {
                    fail++;
                    System.err.println("FAIL " + serStr + " response ok, but stamping failed (TimeStamp.mdb)");
                }

            } catch (Exception ex) {
                logError("IMPORT", "Exception while sending Ser=" + row.get("Ser"), ex);
                System.err.println("ERR " + row.get("Ser") + " " + ex.getMessage());
                fail++;
            }

            done++;
            System.out.println("PROGRESS " + done + "/" + total);
        }

        String summary = "{\"ok\":" + ok + ",\"fail\":" + fail + "}";
        logToFile("SUMMARY", summary);
        return summary;
    }


    // ----------------------- Helpers --------------------------------------------

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
                     "SELECT 1 FROM [TimeStamp] WHERE [Table]=? AND TRIM([Ser])=?")) {
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

    /** Ensure [TimeStamp] exists with TEXT columns (compatible with your Long Text/Short Text). */
    private static void ensureTimeStampTable(Connection conn) throws SQLException {
        try (Statement s = conn.createStatement()) {
            s.execute("SELECT TOP 1 [Ser],[Table],[TimeStamp] FROM [TimeStamp]");
        } catch (SQLException e) {
            try (Statement s2 = conn.createStatement()) {
                // TEXT sizes chosen to be broadly compatible; Access will map appropriately.
                s2.execute(
                        "CREATE TABLE [TimeStamp] (" +
                                "  [Ser] TEXT(255), " +
                                "  [Table] TEXT(64), " +
                                "  [TimeStamp] TEXT(32)" +
                                ")"
                );
            }
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
    /** Overwrite if exists (Table,Ser), else insert a new row. */
    private static boolean upsertTimeStamp(Connection conn, String ser, String tableName) throws SQLException {
        final String ts = LocalDateTime.now().format(TS_FMT);

        // 1) Try UPDATE first
        int updated;
        try (PreparedStatement ps = conn.prepareStatement(
                "UPDATE [TimeStamp] SET [TimeStamp]=? WHERE [Table]=? AND [Ser]=?")) {
            ps.setString(1, ts);
            ps.setString(2, tableName);
            ps.setString(3, ser);
            updated = ps.executeUpdate();
        }
        if (updated > 0) return true;

        // 2) INSERT if not found
        try (PreparedStatement ps = conn.prepareStatement(
                "INSERT INTO [TimeStamp] ([Ser],[Table],[TimeStamp]) VALUES (?,?,?)")) {
            ps.setString(1, ser);
            ps.setString(2, tableName);
            ps.setString(3, ts);
            return ps.executeUpdate() > 0;
        } catch (SQLException ex) {
            // If there's a unique index and we hit a duplicate, UPDATE once more (lost a race)
            final String msg = String.valueOf(ex.getMessage()).toLowerCase(Locale.ROOT);
            if ("23000".equals(ex.getSQLState()) || msg.contains("duplicate")) {
                try (PreparedStatement ps2 = conn.prepareStatement(
                        "UPDATE [TimeStamp] SET [TimeStamp]=? WHERE [Table]=? AND [Ser]=?")) {
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


    private static boolean looksSuccessful(String respBody) {
        if (respBody == null) return false;
        String lower = respBody.toLowerCase(Locale.ROOT);

        int codeIdx = lower.indexOf("\"code\"");
        if (codeIdx >= 0) {
            int colon = lower.indexOf(':', codeIdx);
            if (colon > 0) {
                StringBuilder num = new StringBuilder();
                for (int j = colon + 1; j < lower.length(); j++) {
                    char ch = lower.charAt(j);
                    if (Character.isWhitespace(ch)) continue;
                    if (Character.isDigit(ch)) num.append(ch);
                    else if (num.length() > 0 || ch != '\"') break;
                }
                return num.toString().equals("0");
            }
        }
        if (lower.contains("\"errorno\"") && lower.contains("\"0\"")) return true;
        return lower.contains("success");
    }
    /**
     * Remove rows from TimeStamp.mdb for which home.mdb has Done=TRUE.
     * If startDate/endDate are non-blank, only rows with [Date] BETWEEN startDate AND endDate are considered.
     * Returns a JSON summary: {"scanned":N,"deleted":M}
     */
    public static String cleanupDoneStamps(String startDate, String endDate) {
        int scanned = 0, deleted = 0;

        // --- 1) Build query (optionally date-filtered) over home.mdb ---
        boolean hasRange = startDate != null && !startDate.isBlank()
                && endDate   != null && !endDate.isBlank();

        String sqlHome =
                "SELECT [Ser], 'statement' AS TableName FROM [statement] " +
                        "WHERE [Done]=TRUE " +                               // ← space here
                        (hasRange ? "AND [Date] BETWEEN ? AND ? " : "") +
                        "UNION ALL " +
                        "SELECT [Ser], 'Gl_Journal' AS TableName FROM [Gl_Journal] " +
                        "WHERE [Done]=TRUE " +                               // ← and here
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

        // --- 2) Delete matching rows from TimeStamp.mdb in a batch ---
        try (Connection ts = TimeStamp.getConnection()) {
            ts.setAutoCommit(true); // simple
            ensureTimeStampTable(ts); // in case table is missing

            try (PreparedStatement del = ts.prepareStatement(
                    "DELETE FROM [TimeStamp] WHERE [Table]=? AND [Ser]=?")) {
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
                for (int c : counts) if (c > 0) deleted += c; // 0 if not found, 1 if deleted
            }
        } catch (Exception e) {
            logError("CLEANUP", "Failed deleting from TimeStamp.mdb", e);
            return "{\"scanned\":" + scanned + ",\"deleted\":" + deleted + ",\"error\":\"timestamp_delete_failed\"}";
        }

        String summary = "{\"scanned\":" + scanned + ",\"deleted\":" + deleted + "}";
        logToFile("CLEANUP", summary);
        return summary;
    }

    /** 3) Build anti-resend SQL using local __SentKeys (no cross-db IN) */
    private static String buildAntiResendSql() {
        return
                "SELECT s.[Ser], s.[Date], s.[Amount], s.[Descr1], s.[Room_no], s.[Rent_no], " +
                        "       s.[DebitAccount1], s.[CreditAccount1] AS CreditAccount11, s.[DebitAccount2], s.[CreditAccount2], " +
                        "       s.[DrcostCenterCode] AS DrcostCenterCode, s.[CrcostCenterCode] AS CrcostCenterCode, " +
                        "       s.[CreditAmount1] AS Credit_Amount1, s.[CreditAmount2] AS Credit_Amount2, " +
                        "       s.[DebitAmount1]  AS Debit_Amount1,  s.[DebitAmount2]  AS Debit_Amount2, " +
                        "       'statement' AS src " +
                        "FROM [statement] s " +
                        "WHERE s.[Date] BETWEEN ? AND ? AND s.[Time_Stamp] IS NULL " +
                        "UNION ALL " +
                        "SELECT g.[Ser], g.[Date], g.[Amount], g.[Descr1], g.[Room_no], g.[Rent_no], " +
                        "       g.[DebitAccount1], g.[CreditAccount1] AS CreditAccount11, g.[DebitAccount2], g.[CreditAccount2], " +
                        "       g.[DrcostCenterCode] AS DrcostCenterCode, g.[CrcostCenterCode] AS CrcostCenterCode, " +
                        "       g.[CreditAmount1] AS Credit_Amount1, g.[CreditAmount2] AS Credit_Amount2, " +
                        "       g.[DebitAmount1]  AS Debit_Amount1,  g.[DebitAmount2]  AS Debit_Amount2, " +
                        "       'Gl_Journal' AS src " +
                        "FROM [Gl_Journal] g " +
                        "WHERE g.[Date] BETWEEN ? AND ? AND g.[Time_Stamp] IS NULL";
    }



    /** Anti-resend SELECT: only rows with Time_Stamp IS NULL and NOT in __SentKeys (both tables). */
    private static String buildAntiResendSqlLocal() {
        return
                "SELECT s.[Ser], s.[Date], s.[Amount], s.[Descr1], s.[Room_no], s.[Rent_no], " +
                        "       s.[DebitAccount1], s.[CreditAccount1] AS CreditAccount11, s.[DebitAccount2], s.[CreditAccount2], " +
                        "       s.[DrcostCenterCode] AS DrcostCenterCode, s.[CrcostCenterCode] AS CrcostCenterCode, " +
                        "       s.[CreditAmount1] AS Credit_Amount1, s.[CreditAmount2] AS Credit_Amount2, " +
                        "       s.[DebitAmount1] AS Debit_Amount1, s.[DebitAmount2] AS Debit_Amount2, " +
                        "       'statement' AS src " +
                        "FROM [statement] AS s " +
                        "WHERE [Date] BETWEEN ? AND ? " +                 // keep your range; or drop if you don’t need dates
                        "  AND s.[Time_Stamp] IS NULL " +
                        "  AND NOT EXISTS ( " +
                        "      SELECT 1 FROM [__SentKeys] k " +
                        "      WHERE k.[Table]='statement' " +
                        "        AND LTRIM(RTRIM(k.[Ser])) = LTRIM(RTRIM(CStr(s.[Ser]))) " +
                        "  ) " +
                        "UNION ALL " +
                        "SELECT g.[Ser], g.[Date], g.[Amount], g.[Descr1], g.[Room_no], g.[Rent_no], " +
                        "       g.[DebitAccount1], g.[CreditAccount1] AS CreditAccount11, g.[DebitAccount2], g.[CreditAccount2], " +
                        "       g.[DrcostCenterCode] AS DrcostCenterCode, g.[CrcostCenterCode] AS CrcostCenterCode, " +
                        "       g.[CreditAmount1] AS Credit_Amount1, g.[CreditAmount2] AS Credit_Amount2, " +
                        "       g.[DebitAmount1] AS Debit_Amount1, g.[DebitAmount2] AS Debit_Amount2, " +
                        "       'Gl_Journal' AS src " +
                        "FROM [Gl_Journal] AS g " +
                        "WHERE [Date] BETWEEN ? AND ? " +                 // keep your range; or drop if you don’t need dates
                        "  AND g.[Time_Stamp] IS NULL " +
                        "  AND NOT EXISTS ( " +
                        "      SELECT 1 FROM [__SentKeys] k " +
                        "      WHERE k.[Table]='Gl_Journal' " +
                        "        AND LTRIM(RTRIM(k.[Ser])) = LTRIM(RTRIM(CStr(g.[Ser]))) " +
                        "  )";
    }


}
