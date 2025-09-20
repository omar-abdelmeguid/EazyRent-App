package services;

import db.AccessConnection;

import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;

public class CountUnsentRange {

    public static int execute(String startDate, String endDate) {
    String sql =
    "SELECT COUNT(*) AS n " +
    "FROM ( " +
    "    SELECT [Date], [Time_Stamp] " +
    "    FROM [statement] " +
    "    WHERE [Date] BETWEEN ? AND ? " +
    "      AND [Time_Stamp] IS NULL " +
    "    UNION ALL " +
    "    SELECT [Date], [Time_Stamp] " +
    "    FROM [Gl_Journal] " +
    "    WHERE [Date] BETWEEN ? AND ? " +
    "      AND [Time_Stamp] IS NULL " +
    ")";




        try (Connection conn = AccessConnection.getConnection();
             PreparedStatement ps = conn.prepareStatement(sql)) {

            ps.setString(1, startDate);
            ps.setString(2, endDate);
            ps.setString(3, startDate);
            ps.setString(4, endDate);

            ResultSet rs = ps.executeQuery();
            if (rs.next()) {
                return rs.getInt("n");
            }
        } catch (Exception e) {
            e.printStackTrace();
        }
        return -1; // error
    }
}
