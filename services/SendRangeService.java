package services;

import db.AccessConnection;

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
    private static String LOGIN_URL  = "http://localhost:6060/api/Auth/login";
    private static String IMPORT_URL = "http://localhost:6060/api/GL/ImportJournal";

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

        String body = "{\"userId\":\"1\",\"password\":\"0\"}";
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

        try (Connection conn = AccessConnection.getConnection()) {
            conn.setAutoCommit(false);

            final String sql =
                    "SELECT [Ser],[Date],[Amount],[Descr1],[Room_no],[Rent_no]," +
                            "       [DebitAccount1],[CreditAccount1] AS CreditAccount11,[DebitAccount2],[CreditAccount2]," +
                            "       'statement' AS src " +
                            "FROM [statement] " +
                            "WHERE [Date] BETWEEN ? AND ? AND [Time_Stamp] IS NULL " +
                            "UNION ALL " +
                            "SELECT [Ser],[Date],[Amount],[Descr1],[Room_no],[Rent_no]," +
                            "       [DebitAccount1],[CreditAccount1] AS CreditAccount11,[DebitAccount2],[CreditAccount2]," +
                            "       'Gl_Journal' AS src " +
                            "FROM [Gl_Journal] " +
                            "WHERE [Date] BETWEEN ? AND ? AND [Time_Stamp] IS NULL";

            List<Map<String,Object>> rows;
            try (PreparedStatement ps = conn.prepareStatement(sql)) {
                ps.setString(1, startDate);
                ps.setString(2, endDate);
                ps.setString(3, startDate);
                ps.setString(4, endDate);
                try (ResultSet rs = ps.executeQuery()) {
                    rows = resultSetToList(rs);
                }
            }

            final int total = rows.size();
            final String token = loginExact(); // Bearer payload for ImportJournal

            for (Map<String,Object> row : rows) {
                Map<String,Object> journal = buildJournalForApi(row);
                if (journal == null) {
                    fail++; done++;
                    System.out.println("PROGRESS " + done + "/" + total);
                    continue;
                }

                // GL/ImportJournal expects an array; we send one at a time.
                String payload = "[" + toJson(journal) + "]";

                HttpRequest req = HttpRequest.newBuilder()
                        .uri(URI.create(IMPORT_URL))
                        .timeout(REQ_TIMEOUT)
                        .header("accept", "text/plain")
                        .header("Content-Type", "application/json")
                        .header("Authorization", "Bearer " + token)
                        .POST(HttpRequest.BodyPublishers.ofString(payload, StandardCharsets.UTF_8))
                        .build();

                // Log outgoing import JSON
                logRequest("IMPORT", IMPORT_URL, payload);

                try {
                    HttpResponse<String> resp = HTTP.send(req, HttpResponse.BodyHandlers.ofString());
                    String respBody = (resp.body() == null ? "" : resp.body()).trim();

                    // Log response
                    logResponse("IMPORT", resp.statusCode(), respBody);

                    boolean success = resp.statusCode() / 100 == 2 && looksSuccessful(respBody);
                    if (success) {
                        System.out.println("DONE Ser=" + row.get("Ser"));

                        String table = "statement";
                        String src = String.valueOf(row.get("src"));
                        if ("Gl_Journal".equalsIgnoreCase(src)) table = "Gl_Journal";

                        if (updateTimestampRobust(conn, table, row.get("Ser"))) {
                            conn.commit();
                            ok++;
                        } else {
                            conn.rollback();
                            System.err.println("FAIL " + row.get("Ser") + " response ok, but Time_Stamp update failed");
                            logError("IMPORT", "Time_Stamp update failed for Ser=" + row.get("Ser"), null);
                            fail++;
                        }
                    } else {
                        System.err.println("FAIL " + row.get("Ser") + " " + resp.statusCode() + " " + respBody);
                        conn.rollback();
                        fail++;
                    }
                } catch (Exception ex) {
                    System.err.println("ERR " + row.get("Ser") + " " + ex.getMessage());
                    logError("IMPORT", "Exception while sending Ser=" + row.get("Ser"), ex);
                    conn.rollback();
                    fail++;
                }

                done++;
                System.out.println("PROGRESS " + done + "/" + total);
            }
        } catch (Exception e) {
            logError("SEND_RANGE", "Fatal error in sendRange", e);
            e.printStackTrace();
        }

        String summary = "{\"ok\":" + ok + ",\"fail\":" + fail + "}";
        logToFile("SUMMARY", summary);
        return summary;
    }

    // ---------------- Journal builder -------------------------------------------

    private static Map<String,Object> buildJournalForApi(Map<String,Object> r) {
        if (r == null) return null;

        String date = toYmd(r.get("Date"));                // "YYYY-MM-DD"
        double amt  = Math.abs(toDouble(r.get("Amount"))); // always positive

        Map<String,Object> j = new LinkedHashMap<>();
        j.put("docSerExternal", r.get("Ser"));
        j.put("branchNo", 1);
        j.put("docDate", date);
        j.put("docNo", null);
        j.put("jvType", 1);
        j.put("amountLocal", amt);
        j.put("referenceNo", "0");
        j.put("beneficiaryName", "");
        j.put("receiver", "");
        j.put("manualDocNo", "");
        j.put("description", "");
        j.put("addTerminalName", "1");

        List<Map<String,Object>> details = new ArrayList<>();

        // Debit
        Map<String,Object> dr = new LinkedHashMap<>();
        dr.put("docDueDate", date);
        dr.put("accountCode", r.get("DebitAccount1"));
        dr.put("accountCodeDtl", "");
        dr.put("accountCodeDtlSub", "");
        dr.put("currencyCode", "SAR");
        dr.put("exchangeRate", 0);
        dr.put("drOrCr", 1);
        dr.put("amountLocal", amt);
        dr.put("amountForeign", 0);
        dr.put("chequeNo", "0");
        dr.put("referenceNo", "0");
        dr.put("billNo", "");
        dr.put("billSer", "");
        dr.put("installmentNo", 0);
        dr.put("description", "room number= " + s(r.get("Room_no")) +
                " rent number= " + s(r.get("Rent_no")) + " " + s(r.get("Descr1")));
        details.add(dr);

        // Credit (prefer alias CreditAccount11; fallback CreditAccount1)
        Object creditAcct = r.get("CreditAccount11");
        if (creditAcct == null) creditAcct = r.get("CreditAccount1");

        Map<String,Object> cr = new LinkedHashMap<>();
        cr.put("docDueDate", date);
        cr.put("accountCode", creditAcct);
        cr.put("accountCodeDtl", "");
        cr.put("accountCodeDtlSub", "");
        cr.put("currencyCode", "SAR");
        cr.put("exchangeRate", 0);
        cr.put("drOrCr", -1);
        cr.put("amountLocal", amt);
        cr.put("amountForeign", 0);
        cr.put("chequeNo", "0");
        cr.put("referenceNo", "0");
        cr.put("billNo", "");
        cr.put("billSer", "");
        cr.put("installmentNo", 0);
        cr.put("description", "room number= " + s(r.get("Room_no")) +
                " rent number= " + s(r.get("Rent_no")) + " " + s(r.get("Descr1")));
        details.add(cr);

        j.put("details", details);
        return j;
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

    private static String toYmd(Object dateObj) {
        if (dateObj == null) return "";
        String s = String.valueOf(dateObj).trim();
        int sp = s.indexOf(' ');
        if (sp > 0) s = s.substring(0, sp);
        return s;
    }

    private static String s(Object v) { return v == null ? "" : String.valueOf(v); }

    private static double toDouble(Object v) {
        if (v == null) return 0d;
        try { return Double.parseDouble(v.toString()); } catch (Exception e) { return 0d; }
    }

    private static boolean updateTimestampRobust(Connection conn, String table, Object ser) throws SQLException {
        String[] attempts = new String[] {
                "UPDATE [" + table + "] SET [Time_Stamp]=Now() WHERE [Ser]=?",
                "UPDATE [" + table + "] SET [Time_Stamp]=Format(Now(),'yyyy-mm-dd hh:nn:ss') WHERE [Ser]=?",
                "UPDATE [" + table + "] SET [Time_Stamp]='1' WHERE [Ser]=?"
        };
        for (String sql : attempts) {
            try (PreparedStatement up = conn.prepareStatement(sql)) {
                up.setObject(1, ser);
                int n = up.executeUpdate();
                if (n > 0) return true;
            } catch (SQLException ex) {
                String msg = (ex.getMessage() == null ? "" : ex.getMessage().toLowerCase(Locale.ROOT));
                boolean trunc = msg.contains("right truncation") || msg.contains("data exception");
                if (!trunc) throw ex;
            }
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
        if (lower.contains("success")) return true;
        if (lower.contains("\"code\"")) {
            int i = lower.indexOf("\"code\"");
            int c = lower.indexOf(':', i);
            if (c > 0) {
                for (int j = c + 1; j < Math.min(lower.length(), c + 6); j++) {
                    char ch = lower.charAt(j);
                    if (ch == '0') return true;
                    if (Character.isDigit(ch)) break;
                }
            }
        }
        if (lower.contains("\"errorno\"") && lower.contains("\"0\"")) return true;
        return false;
    }
}
