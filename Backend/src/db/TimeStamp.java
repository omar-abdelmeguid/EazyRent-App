package db;

import java.io.IOException;
import java.nio.file.*;
import java.sql.Connection;
import java.sql.DriverManager;

public class TimeStamp {
    // Default to your current .accdb (works for .mdb too once setPath is called)
    private static String dbPath = "C:/Users/omara/OneDrive/Desktop/EazyRent/TimeStamp.accdb";
    private static final String MEMORY_FILE = "timestamp_location.txt";

    static {
        try {
            Path file = Paths.get(MEMORY_FILE);
            if (Files.exists(file)) {
                String saved = Files.readString(file).trim();
                if (isValidTimeStampPath(saved)) {
                    dbPath = saved;
                    System.out.println("Loaded TimeStamp DB path: " + dbPath);
                } else {
                    System.out.println("Ignoring saved path (not a valid Access DB): " + saved);
                }
            }
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    /** Accept any existing file that ends with .mdb or .accdb */
    private static boolean isValidTimeStampPath(String p) {
        if (p == null || p.isBlank()) return false;
        try {
            Path path = Paths.get(p);
            if (!Files.exists(path) || !Files.isRegularFile(path)) return false;
            String s = p.replace('\\','/').toLowerCase();
            return s.endsWith(".mdb") || s.endsWith(".accdb");
        } catch (Exception ignore) {
            return false;
        }
    }

    /** Persist a new path if it's a valid Access DB (.mdb/.accdb) */
    public static void setPath(String newPath) {
        if (!isValidTimeStampPath(newPath)) {
            throw new IllegalArgumentException("Not a valid Access DB file (.mdb/.accdb): " + newPath);
        }
        dbPath = Paths.get(newPath).toAbsolutePath().toString();
        try {
            Files.writeString(Paths.get(MEMORY_FILE), dbPath,
                    StandardOpenOption.CREATE, StandardOpenOption.TRUNCATE_EXISTING);
            System.out.println("Saved TimeStamp DB path: " + dbPath);
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    public static String getPath() {
        return dbPath;
    }

    /** Opens a connection to the TimeStamp DB (MDB or ACCDB) via UCanAccess */
    public static Connection getConnection() throws Exception {
        Path p = Paths.get(dbPath);
        if (!Files.exists(p)) {
            throw new IllegalStateException("TimeStamp DB not found at: " + dbPath);
        }
        // Spaces are fine in UCanAccess paths; no extra encoding needed.
        String url = "jdbc:ucanaccess://" + dbPath + ";memory=false";
        return DriverManager.getConnection(url);
    }
}
