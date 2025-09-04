package services;

import db.AccessConnection;

import java.sql.*;
import java.time.LocalDate;
import java.util.*;

public class FetchRowsRange {

    /**
     * Fetch rows between two dates, filtered by sent/unsent mode.
     *
     * @param startDate must be earlier date
     * @param endDate   must be later date
     * @param mode      "sent" or "unsent"
     * @return list of rows (each row is a map: columnName -> value)
     * @throws Exception if DB connection or SQL fails
     */
    public static List<Map<String, Object>> execute(LocalDate startDate, LocalDate endDate, String mode) throws Exception {

        if (!mode.equalsIgnoreCase("sent") && !mode.equalsIgnoreCase("unsent")) {
            throw new IllegalArgumentException("Mode must be 'sent' or 'unsent'");
        }

        // Build condition
        String tsCondition = mode.equalsIgnoreCase("sent")
                ? "[Time_Stamp] IS NOT NULL"
                : "[Time_Stamp] IS NULL";

        String sql =
                "SELECT * FROM (" +
                        "  SELECT [Date], [Amount], [Descr1], [Type], [Room_no], [Rent_no], " +
                        "         [DebitAccount1], [CreditAccount1] AS CreditAccount11, " +
                        "         [DebitAccount2], [CreditAccount2] " +
                        "  FROM [statement] " +
                        "  WHERE [Date] BETWEEN ? AND ? AND " + tsCondition +
                        "  UNION ALL " +
                        "  SELECT [Date], [Amount], [Descr1], [Type], [Room_no], [Rent_no], " +
                        "         [DebitAccount1], [CreditAccount1] AS CreditAccount11, " +
                        "         [DebitAccount2], [CreditAccount2] " +
                        "  FROM [Gl_Journal] " +
                        "  WHERE [Date] BETWEEN ? AND ? AND " + tsCondition +
                        ") AS combined " +
                        "ORDER BY [Date];";

        List<Map<String, Object>> results = new ArrayList<>();

        try (Connection conn = AccessConnection.getConnection();
             PreparedStatement ps = conn.prepareStatement(sql)) {

            // Use java.sql.Date converted from LocalDate
            java.sql.Date sqlStart = java.sql.Date.valueOf(startDate);
            java.sql.Date sqlEnd = java.sql.Date.valueOf(endDate);

            ps.setDate(1, sqlStart);
            ps.setDate(2, sqlEnd);
            ps.setDate(3, sqlStart);
            ps.setDate(4, sqlEnd);

            try (ResultSet rs = ps.executeQuery()) {
                ResultSetMetaData meta = rs.getMetaData();
                int colCount = meta.getColumnCount();

                while (rs.next()) {
                    Map<String, Object> row = new LinkedHashMap<>();
                    for (int i = 1; i <= colCount; i++) {
                        String colName = meta.getColumnLabel(i);

                        // Skip phantom/extra columns if driver injects them
                        if (colName.equalsIgnoreCase("Num")) continue;

                        row.put(colName, rs.getObject(i));
                    }
                    results.add(row);
                }
            }
        }

        return results;
    }
}
