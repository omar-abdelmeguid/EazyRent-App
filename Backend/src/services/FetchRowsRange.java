package services;

import db.AccessConnection;

import java.sql.*;
import java.time.LocalDate;
import java.util.*;

public class FetchRowsRange {

    /**
     * Pulls rows between dates with optional mode:
     *  - "sent"   -> Time_Stamp IS NOT NULL
     *  - "unsent" -> Time_Stamp IS NULL
     * Returns a List of Maps with keys used by the GUI:
     *  Date, Amount, Descr1, Type, Room_no, Rent_no,
     *  DebitAccount1, CreditAccount1, DebitAccount2, CreditAccount2
     */
    public static List<Map<String, Object>> execute(LocalDate from, LocalDate to, String mode) throws SQLException {
        List<Map<String, Object>> out = new ArrayList<>();

        String statusFilter = "";
        if ("sent".equalsIgnoreCase(mode)) {
            statusFilter = "AND [Time_Stamp] IS NOT NULL";
        } else if ("unsent".equalsIgnoreCase(mode)) {
            statusFilter = "AND [Time_Stamp] IS NULL";
        }

        // Use aliases on the RIGHT so keys match the GUI
        final String sql = """
                SELECT 
                    [Date]                           AS Date,
                    [Amount]                         AS Amount,
                    [Descr1]                         AS Descr1,
                    [Type]                           AS Type,
                    [Room_no]                        AS Room_no,
                    [Rent_no]                        AS Rent_no,
                    [DebitAccount1]                  AS DebitAccount1,
                    [CreditAccount1]                 AS CreditAccount1,
                    [DebitAccount2]                  AS DebitAccount2,
                    [CreditAccount2]                 AS CreditAccount2,
                    [costCenterCode]                 AS costCenterCode
                FROM [statement]
                WHERE [Date] BETWEEN ? AND ? %s
                UNION ALL
                SELECT 
                    [Date]                           AS Date,
                    [Amount]                         AS Amount,
                    [Descr1]                         AS Descr1,
                    [Type]                           AS Type,
                    [Room_no]                        AS Room_no,
                    [Rent_no]                        AS Rent_no,
                    [DebitAccount1]                  AS DebitAccount1,
                    [CreditAccount1]                 AS CreditAccount1,
                    [DebitAccount2]                  AS DebitAccount2,
                    [CreditAccount2]                 AS CreditAccount2,
                    [costCenterCode]                 AS costCenterCode
                FROM [Gl_Journal]
                WHERE [Date] BETWEEN ? AND ? %s
                ORDER BY Date ASC
                """.formatted(statusFilter, statusFilter);

        try (Connection conn = AccessConnection.getConnection();
             PreparedStatement ps = conn.prepareStatement(sql)) {

            ps.setString(1, from.toString());
            ps.setString(2, to.toString());
            ps.setString(3, from.toString());
            ps.setString(4, to.toString());

            try (ResultSet rs = ps.executeQuery()) {
                ResultSetMetaData md = rs.getMetaData();
                int cols = md.getColumnCount();
                while (rs.next()) {
                    Map<String, Object> row = new LinkedHashMap<>();
                    for (int i = 1; i <= cols; i++) {
                        String key = md.getColumnLabel(i); // use alias
                        Object val = rs.getObject(i);
                        row.put(key, normalize(key, val));
                    }
                    out.add(row);
                }
            }
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
        return out;
    }

    private static Object normalize(String key, Object val) {
        if (val == null) return "";
        if ("Date".equalsIgnoreCase(key)) {
            // format date nicely
            String s = val.toString();
            int sp = s.indexOf(' ');
            return (sp > 0) ? s.substring(0, sp) : s; // yyyy-MM-dd
        }
        return val;
    }
}
