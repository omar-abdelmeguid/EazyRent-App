package services;

import db.AccessConnection;
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
import java.util.*;

/**
 * Sends journals using:
 *  - Login (matches your cURL exactly by default):
 *      POST http://localhost:6060/api/Auth/login
 *      Headers: accept:text/plain, year:2025, activity:1, Content-Type: application/json
 *      Body: {"userId":"1","password":"0"}  -> returns a Bearer token (string)
 *
 *  - Import:
 *      POST http://localhost:6060/api/GL/ImportJournal
 *      Headers: accept:text/plain, Content-Type: application/json, Authorization: Bearer <token>
 *      Body: [ { journal }, ... ] (this code sends one journal per request)
 *
 * Every JSON request + response is appended to log.txt
 */
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

    public static void setLoginHeaders(String accept, String year, String activity) {
        if (accept   != null && !accept.isBlank())   HDR_ACCEPT   = accept;
        if (year     != null && !year.isBlank())     HDR_YEAR     = year;
        if (activity != null && !activity.isBlank()) HDR_ACTIVITY = activity;
    }

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

    /**
     * Logs in EXACTLY like your cURL (headers + body) and returns a token string.
     */
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

        // Log outgoing JSON
        logRequest("LOGIN", LOGIN_URL, body);

        try {
            HttpResponse<String> resp = HTTP.send(req, HttpResponse.BodyHandlers.ofString());
            String respBody = resp.body() == null ? "" : resp.body();
            logResponse("LOGIN", resp.statusCode(), respBody);

            if (resp.statusCode() / 100 != 2) {
                throw new RuntimeException("Login failed: " + resp.statusCode() + " " + respBody);
            }
            String token = extractTokenLoose(respBody);
            if (token == null || token.isBlank()) {
                token = respBody.trim().replace("\"", "");
            }
            cachedToken = token;
            return token;
        } catch (Exception e) {
            logError("LOGIN", "Login error", e);
            throw new RuntimeException("Login error: " + e.getMessage(), e);
        }
    }

    /**
     * Main entry: send pending rows in [statement] and [Gl_Journal] for the date range.
     */
    public static String sendRange(String startDate, String endDate) {
        int ok = 0, fail = 0, done = 0;

        // 1) Fetch rows in a short-lived connection (no HTTP inside this txn)
        final String sql =
                "SELECT [Ser],[Date],[Amount],[Descr1],[Room_no],[Rent_no],"
                        + "       [DebitAccount1],[CreditAccount1] AS CreditAccount11,[DebitAccount2],[CreditAccount2],"
                        + "       [CostCenterCode] AS CostCenterCode,"   // keep your alias consistent
                        + "       'statement' AS src "
                        + "FROM [statement] "
                        + "WHERE [Date] BETWEEN ? AND ? AND [Time_Stamp] IS NULL "
                        + "UNION ALL "
                        + "SELECT [Ser],[Date],[Amount],[Descr1],[Room_no],[Rent_no],"
                        + "       [DebitAccount1],[CreditAccount1] AS CreditAccount11,[DebitAccount2],[CreditAccount2],"
                        + "       [CostCenterCode] AS CostCenterCode,"   // and here
                        + "       'Gl_Journal' AS src "
                        + "FROM [Gl_Journal] "
                        + "WHERE [Date] BETWEEN ? AND ? AND [Time_Stamp] IS NULL";

        List<Map<String,Object>> rows;
        try (Connection c = AccessConnection.getConnection();
             PreparedStatement ps = c.prepareStatement(sql)) {
            ps.setString(1, startDate);
            ps.setString(2, endDate);
            ps.setString(3, startDate);
            ps.setString(4, endDate);
            try (ResultSet rs = ps.executeQuery()) {
                rows = resultSetToList(rs);
            }
        } catch (Exception e) {
            logError("SEND_RANGE", "Failed fetching rows", e);
            return "{\"ok\":0,\"fail\":0,\"error\":\"fetch_failed\"}";
        }

        final int total = rows.size();
        final String token = loginExact(); // Bearer for ImportJournal

        // 2) For each row: HTTP first, then a fresh DB connection to stamp
        for (Map<String,Object> row : rows) {
            try {
                Map<String,Object> journal = JournalBuilder.buildJournalForApi(row);
                if (journal == null) {
                    fail++; done++; System.out.println("PROGRESS " + done + "/" + total);
                    continue;
                }

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
                    System.err.println("FAIL " + row.get("Ser") + " " + resp.statusCode() + " " + body);
                    fail++; done++; System.out.println("PROGRESS " + done + "/" + total);
                    continue;
                }

                // 2.a) If HTTP success, stamp using a brand-new connection/transaction
                String table = "statement";
                String src = String.valueOf(row.get("src"));
                if ("Gl_Journal".equalsIgnoreCase(src)) table = "Gl_Journal";

                boolean stamped = false;
                for (int attempt = 0; attempt < 2 && !stamped; attempt++) {
                    try (Connection conn2 = AccessConnection.getConnection()) {
                        conn2.setAutoCommit(false);
                        if (updateTimestampRobust(conn2, table, row.get("Ser"))) {
                            conn2.commit();
                            stamped = true;
                        } else {
                            conn2.rollback();
                            logError("IMPORT", "Time_Stamp update returned 0 rows for Ser=" + row.get("Ser"), null);
                        }
                    } catch (SQLException ex) {
                        String msg = String.valueOf(ex.getMessage()).toLowerCase(Locale.ROOT);
                        boolean closed = msg.contains("connection") && msg.contains("closed");
                        if (closed && attempt == 0) {
                            // retry once with a fresh connection
                            continue;
                        }
                        logError("IMPORT", "Stamping error for Ser=" + row.get("Ser"), ex);
                    }
                }

                if (stamped) {
                    ok++;
                    System.out.println("DONE Ser=" + row.get("Ser"));
                } else {
                    fail++;
                    System.err.println("FAIL " + row.get("Ser") + " response ok, but stamping failed");
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

    private static boolean updateTimestampRobust(Connection conn, String table, Object ser) throws SQLException {
        // 1) Insert as formatted string "yyyy-MM-dd HH:mm:ss"
        String sql1 = "UPDATE [" + table + "] SET [Time_Stamp]=? WHERE [Ser]=?";
        try (PreparedStatement up = conn.prepareStatement(sql1)) {
            String ts = java.time.LocalDateTime.now()
                    .format(java.time.format.DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm:ss"));
            up.setString(1, ts);
            up.setObject(2, ser);
            int n = up.executeUpdate();
            if (n > 0) return true;
        } catch (SQLException ex) {
            String msg = String.valueOf(ex.getMessage()).toLowerCase(Locale.ROOT);
            boolean trunc = msg.contains("right truncation") || msg.contains("data exception");
            if (!trunc) throw ex;
        }

        // 2) Fallback: try plain timestamp (if column type is truly Date/Time)
        String sql2 = "UPDATE [" + table + "] SET [Time_Stamp]=? WHERE [Ser]=?";
        try (PreparedStatement up = conn.prepareStatement(sql2)) {
            up.setTimestamp(1, new java.sql.Timestamp(System.currentTimeMillis()));
            up.setObject(2, ser);
            int n = up.executeUpdate();
            if (n > 0) return true;
        } catch (SQLException ex) {
            String msg = String.valueOf(ex.getMessage()).toLowerCase(Locale.ROOT);
            boolean trunc = msg.contains("right truncation") || msg.contains("data exception");
            if (!trunc) throw ex;
        }

        // 3) Last resort: literal '1'
        String sql3 = "UPDATE [" + table + "] SET [Time_Stamp]='1' WHERE [Ser]=?";
        try (PreparedStatement up = conn.prepareStatement(sql3)) {
            up.setObject(1, ser);
            int n = up.executeUpdate();
            if (n > 0) return true;
        }

        return false;
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

    /**
     * Tries to pull a token from various response styles.
     */
    private static String extractTokenLoose(String body) {
        if (body == null) return null;
        String b = body.trim();
        // raw quoted string
        if (b.startsWith("\"") && b.endsWith("\"") && b.length() > 2) {
            return b.substring(1, b.length()-1);
        }
        // naive "token":"..."
        String key = "\"token\"";
        int i = b.indexOf(key);
        if (i >= 0) {
            int q1 = b.indexOf('"', i + key.length());
            if (q1 >= 0) {
                int q2 = b.indexOf('"', q1 + 1);
                if (q2 > q1) return b.substring(q1 + 1, q2);
            }
        }
        // look for JWT-like thing
        for (String part : b.split("[\\s\"{}:,]+")) {
            if (part.length() > 20 && part.contains(".")) return part;
        }
        return null;
    }

    private static boolean looksSuccessful(String respBody) {
        if (respBody == null) return false;

        String lower = respBody.toLowerCase(Locale.ROOT);

        // 1. Look explicitly for "code":0
        int codeIdx = lower.indexOf("\"code\"");
        if (codeIdx >= 0) {
            int colon = lower.indexOf(':', codeIdx);
            if (colon > 0) {
                // read the digits right after the colon
                StringBuilder num = new StringBuilder();
                for (int j = colon + 1; j < lower.length(); j++) {
                    char ch = lower.charAt(j);
                    if (Character.isWhitespace(ch)) continue;
                    if (Character.isDigit(ch)) {
                        num.append(ch);
                    } else if (num.length() > 0) {
                        break; // stop when number finished
                    } else if (ch != '\"') {
                        break; // non-digit/non-quote before number
                    }
                }
                if (num.toString().equals("0")) {
                    return true; // ✅ code == 0 means success
                } else {
                    return false; // ❌ code != 0
                }
            }
        }

        // 2. Fallback: check "errorNo":"0"
        if (lower.contains("\"errorno\"") && lower.contains("\"0\"")) {
            return true;
        }

        // 3. Fallback: check word "success"
        if (lower.contains("success")) {
            return true;
        }

        return false;
    }

}
