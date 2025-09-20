package db;

import java.io.IOException;
import java.nio.file.*;
import java.sql.Connection;
import java.sql.DriverManager;

public class AccessConnection {
    private static String dbPath = "C:/Users/omara/OneDrive/Desktop/branches/home.mdb"; // default
    private static final String DB_PASSWORD = "28081959";
    private static final String MEMORY_FILE = "database_location.txt";

    static {
        // load path from file at startup
        try {
            Path file = Paths.get(MEMORY_FILE);
            if (Files.exists(file)) {
                String saved = Files.readString(file).trim();
                if (!saved.isEmpty()) {
                    dbPath = saved;
                    System.out.println("Loaded DB path from memory file: " + dbPath);
                }
            }
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    public static void setPath(String newPath) {
        dbPath = newPath;
        // save path to file immediately
        try {
            Files.writeString(Paths.get(MEMORY_FILE), dbPath, StandardOpenOption.CREATE, StandardOpenOption.TRUNCATE_EXISTING);
            System.out.println("Saved DB path to memory file: " + dbPath);
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    public static String getPath() {
        return dbPath;
    }

    public static Connection getConnection() throws Exception {
        String url = "jdbc:ucanaccess://" + dbPath + ";memory=false;jackcessOpener=db.CryptCodecOpener;pwd=" + DB_PASSWORD + 10;
        return DriverManager.getConnection(url);
    }
}
