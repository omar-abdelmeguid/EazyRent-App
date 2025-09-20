    package services;

    import db.AccessConnection;
    import db.TimeStamp;   // <-- used to read keys from TimeStamp DB
    import java.sql.*;
    import java.time.LocalDate;
    import java.util.*;

    public class FetchRowsRange {

        /**
         * Pulls rows between dates with optional mode:
         *  - "sent"   -> Time_Stamp IS NOT NULL
         *  - "unsent" -> Time_Stamp IS NULL AND NOT in TimeStamp DB
         * Returns List of Maps with aliased keys used by the GUI.
         */
        public static List<Map<String, Object>> execute(LocalDate from, LocalDate to, String mode) throws Exception {
            List<Map<String, Object>> out = new ArrayList<>();

            final boolean isUnsent = "unsent".equalsIgnoreCase(mode);
            final boolean isSent   = "sent".equalsIgnoreCase(mode);

            // half-open date ranges
            final String datePredS = "(s.[Date] >= ? AND s.[Date] < ?)";
            final String datePredG = "(g.[Date] >= ? AND g.[Date] < ?)";

            // load keys from TimeStamp DB once
            final StampedKeys stamped = loadStampedKeysFromTimestamp();

            // We fetch everything in range and decide sent/unsent in Java using _tsNull + stamped sets.
            final String sql = """
SELECT
    'statement' AS _src,
    Replace(Trim(CStr(s.[Ser])), ',', '') AS _ser,
    IIF(s.[TR_TimeStamp] IS NULL OR Len(Trim(s.[TR_TimeStamp]))=0, 1, 0) AS _tsNull,
    s.[Date] AS Date, s.[Amount] AS Amount, s.[Descr1] AS Descr1, s.[Type] AS Type,
    s.[Room_no] AS Room_no, s.[Rent_no] AS Rent_no,
    s.[DebitAccount1] AS DebitAccount1, s.[CreditAccount1] AS CreditAccount1,
    s.[DebitAccount2] AS DebitAccount2, s.[CreditAccount2] AS CreditAccount2,
    s.[DrcostCenterCode] AS DrcostCenterCode, s.[CrcostCenterCode] AS CrcostCenterCode,
    s.[CreditAmount1] AS Credit_Amount1, s.[CreditAmount2] AS Credit_Amount2,
    s.[DebitAmount1]  AS Debit_Amount1,  s.[DebitAmount2]  AS Debit_Amount2
FROM [statement] s
WHERE %s

UNION ALL

SELECT
    'Gl_Journal' AS _src,
    Replace(Trim(CStr(g.[Ser])), ',', '') AS _ser,
    IIF(g.[TR_TimeStamp] IS NULL OR Len(Trim(g.[TR_TimeStamp]))=0, 1, 0) AS _tsNull,
    g.[Date] AS Date, g.[Amount] AS Amount, g.[Descr1] AS Descr1, g.[Type] AS Type,
    g.[Room_no] AS Room_no, g.[Rent_no] AS Rent_no,
    g.[DebitAccount1] AS DebitAccount1, g.[CreditAccount1] AS CreditAccount1,
    g.[DebitAccount2] AS DebitAccount2, g.[CreditAccount2] AS CreditAccount2,
    g.[DrcostCenterCode] AS DrcostCenterCode, g.[CrcostCenterCode] AS CrcostCenterCode,
    g.[CreditAmount1] AS Credit_Amount1, g.[CreditAmount2] AS Credit_Amount2,
    g.[DebitAmount1]  AS Debit_Amount1,  g.[DebitAmount2]  AS Debit_Amount2
FROM [Gl_Journal] g
WHERE %s

ORDER BY Date ASC
""".formatted(datePredS, datePredG);


            LocalDate toExclusive = to.plusDays(1);

            try (Connection conn = AccessConnection.getConnection();
                 PreparedStatement ps = conn.prepareStatement(sql)) {

                ps.setString(1, from.toString());
                ps.setString(2, toExclusive.toString());
                ps.setString(3, from.toString());
                ps.setString(4, toExclusive.toString());

                try (ResultSet rs = ps.executeQuery()) {
                    ResultSetMetaData md = rs.getMetaData();
                    int cols = md.getColumnCount();

                    while (rs.next()) {
                        String src     = Optional.ofNullable(rs.getString("_src")).orElse("").trim();
                        String ser     = Optional.ofNullable(rs.getString("_ser")).orElse("").trim();
                        boolean tsNull = rs.getInt("_tsNull") == 1;
                        boolean inStamp = src.equalsIgnoreCase("statement")
                                ? stamped.stmt.contains(ser)
                                : stamped.gl.contains(ser);

                        boolean keep;
                        if (isUnsent) {
                            // only truly unsent
                            keep = tsNull && !inStamp;
                        } else if (isSent) {
                            // consider sent if it has a timestamp OR is recorded in TimeStamp DB
                            keep = !tsNull || inStamp;
                        } else {
                            keep = true; // no mode filter
                        }
                        if (!keep) continue;

                        Map<String,Object> row = new LinkedHashMap<>();
                        row.put("Ser", ser);           // make Ser available to the GUI
                        row.put("src", src);
                        for (int i = 1; i <= cols; i++) {
                            String key = md.getColumnLabel(i);
                            if ("_src".equals(key) || "_ser".equals(key) || "_tsNull".equals(key)) continue;
                            row.put(key, normalize(key, rs.getObject(i)));
                        }
                        out.add(row);
                    }
                }
            }

            log("FETCH_SUMMARY", "returned=" + out.size() + " mode=" + mode);
            return out;
        }






        // ---------- helpers ---------------------------------------------------------

        private static Object normalize(String key, Object val) {
            if (val == null) return "";
            if ("Date".equalsIgnoreCase(key)) {
                String s = val.toString();
                int sp = s.indexOf(' ');
                return (sp > 0) ? s.substring(0, sp) : s; // yyyy-MM-dd
            }
            return val;
        }
        // --- at top of FetchRowsRange ---
        private static final boolean LOG_VERBOSE = false; // set true to log every keep
        private static synchronized void log(String tag, String msg) {
            try (java.io.FileWriter fw = new java.io.FileWriter("log.txt", true)) {
                fw.write("[" + java.time.LocalDateTime.now() + "] " + tag + " " + msg + System.lineSeparator());
            } catch (Exception ignore) {}
        }
        private static final class StampedKeys {
            final Set<String> stmt = new HashSet<>();      // statement
            final Set<String> gl   = new HashSet<>();      // Gl_Journal
        }

        private static StampedKeys loadStampedKeysFromTimestamp() {
            StampedKeys out = new StampedKeys();
            try (Connection ts = db.TimeStamp.getConnection();
                 PreparedStatement ps = ts.prepareStatement(
                         "SELECT [Table], Replace(Trim([Ser]), ',', '') FROM [TR_TimeStamp]"
                 );
                 ResultSet rs = ps.executeQuery()) {

                while (rs.next()) {
                    String tbl = rs.getString(1);
                    String ser = rs.getString(2);
                    if (tbl == null || ser == null) continue;
                    ser = ser.trim();
                    if (ser.isEmpty()) continue;

                    if ("statement".equalsIgnoreCase(tbl)) out.stmt.add(ser);
                    else if ("Gl_Journal".equalsIgnoreCase(tbl)) out.gl.add(ser);
                }
                log("STAMP_LOAD", "path=" + db.TimeStamp.getPath()
                        + " statement=" + out.stmt.size()
                        + " gl=" + out.gl.size());
                log("STAMP_LOAD_SAMPLES",
                        "statement-> " + out.stmt.stream().limit(10).toList()
                                + " | gl-> " + out.gl.stream().limit(10).toList());
            } catch (Exception e) {
                log("STAMP_LOAD_ERR", String.valueOf(e));
            }
            return out;
        }




    }
