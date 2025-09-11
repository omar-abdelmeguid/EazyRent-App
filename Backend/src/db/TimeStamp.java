package db;

import java.io.IOException;
import java.nio.file.*;
import java.sql.Connection;
import java.sql.DriverManager;

public class TimeStamp {
    private static String dbPath = "C:/Users/omara/OneDrive/Desktop/EazyRent/TimeStamp.accdb";
    private static final String MEMORY_FILE = "timestamp_location.txt";

    static {
        try {
            Path file = Paths.get(MEMORY_FILE);
            if (Files.exists(file)) {
                String saved = Files.readString(file).trim();
                if (isValidTimeStampPath(saved)) {
                    dbPath = saved;
                    System.out.println("Loaded TimeStamp.mdb path: " + dbPath);
                } else {
                    System.out.println("Ignoring saved path (not a valid TimeStamp.mdb): " + saved);
                }
            }
        } catch (IOException e) {
            e.printStackTrace();
        }
    }


    private static boolean isValidTimeStampPath(String p) {
        if (p == null || p.isBlank()) return false;
        try {
            Path path = Paths.get(p);
            if (!Files.exists(path)) return false;
            String s = p.replace('\\','/').toLowerCase();
            // accept either .mdb or .accdb
            return s.endsWith("/timestamp.mdb") || s.endsWith("/timestamp.accdb");
        } catch (Exception ignore) {
            return false;
        }
    }


    public static void setPath(String newPath) {
        dbPath = newPath;
        try {
            Files.writeString(Paths.get(MEMORY_FILE), dbPath,
                    StandardOpenOption.CREATE, StandardOpenOption.TRUNCATE_EXISTING);
            System.out.println("Saved TimeStamp.mdb path: " + dbPath);
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    public static String getPath() {
        return dbPath;
    }

    /** Opens a connection to TimeStamp.mdb */
    public static Connection getConnection() throws Exception {
        // Optional safety: fail fast if file missing
        if (!Files.exists(Paths.get(dbPath))) {
            throw new IllegalStateException("TimeStamp.mdb not found at: " + dbPath);
        }
        String url = "jdbc:ucanaccess://" + dbPath + ";memory=false";
        return DriverManager.getConnection(url);
    }
}
