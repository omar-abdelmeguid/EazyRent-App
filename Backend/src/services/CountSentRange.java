package services;

import db.AccessConnection;

import java.sql.Connection;
import java.sql.PreparedStatement;
import java.sql.ResultSet;
import java.util.logging.Logger;

public class CountSentRange {
    private static final Logger logger = Logger.getLogger(CountSentRange.class.getName());

    public static int execute(String startDate, String endDate) {
    String sql = 
    "SELECT COUNT(*) AS n " +
    "FROM ( " +
    "    SELECT [Date], [Time_Stamp] " +
    "    FROM [statement] " +
    "    WHERE [Date] BETWEEN ? AND ? " +
    "      AND [Time_Stamp] IS NOT NULL " +
    "    UNION ALL " +
    "    SELECT [Date], [Time_Stamp] " +
    "    FROM [Gl_Journal] " +
    "    WHERE [Date] BETWEEN ? AND ? " +
    "      AND [Time_Stamp] IS NOT NULL " +
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
            logger.severe("SQL error: " + e.getMessage());

        }
        return -1; // error
    }
}
