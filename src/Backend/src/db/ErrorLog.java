package db;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.sql.*;
import java.util.HashMap;
import java.util.Map;

/**
 * ErrorLog
 * --------
 * A tiny helper for persisting per-row errors into a Microsoft Access database
 * using UCanAccess. Each error is keyed by the row "serial" (ser).
 *
 * Table schema (auto-created on first use):
 *   CREATE TABLE [errorLog] (
 *     [ser] TEXT(50) NOT NULL,
 *     [errorMessage] MEMO,
 *     [created_at] DATETIME DEFAULT NOW()
 *   );
 *   CREATE INDEX idx_error_ser ON [errorLog]([ser]);
 *
 * Typical usage:
 *   ErrorLog.upsertError("12345", "Missing account code");
 *   Map<String,String> map = ErrorLog.getAllErrors(); // ser -> message
 *   ErrorLog.deleteError("12345"); // when fixed
 *   ErrorLog.clearAllErrors();     // wipe all
 *
 * Notes:
 * - Requires UCanAccess on classpath.
 * - Uses immediatelyReleaseResources to reduce Access file locks.
 */
public final class ErrorLog {

    private static final String TABLE_NAME = "errorLog";
    private static final String MEMORY_FILE = "errorlog_location.txt";

    /** Thread-visible DB path (.mdb or .accdb). */
    private static volatile String dbPath = "errorLog.mdb";

    static {
        // Load persisted location if available
        try {
            Path file = Paths.get(MEMORY_FILE);
            if (Files.exists(file)) {
                String saved = Files.readString(file, StandardCharsets.UTF_8).trim();
                if (isValidDbPath(saved)) {
                    dbPath = Paths.get(saved).toAbsolutePath().toString();
                    System.out.println("[ErrorLog] Loaded DB path: " + dbPath);
                } else {
                    System.out.println("[ErrorLog] Ignoring invalid saved path: " + saved);
                }
            }
        } catch (IOException e) {
            System.err.println("[ErrorLog] Failed to load saved path: " + e.getMessage());
        }
    }

    private ErrorLog() {}

    // ---------- Public API ----------

    /** Persist a new DB location (.mdb/.accdb) and use it from now on. */
    public static synchronized void setPath(String newPath) {
        if (!isValidDbPath(newPath)) {
            throw new IllegalArgumentException("Not a valid Access DB file (.mdb/.accdb): " + newPath);
        }
        dbPath = Paths.get(newPath).toAbsolutePath().toString();
        try {
            Files.writeString(Paths.get(MEMORY_FILE), dbPath, StandardCharsets.UTF_8,
                    StandardOpenOption.CREATE, StandardOpenOption.TRUNCATE_EXISTING);
            System.out.println("[ErrorLog] Saved DB path: " + dbPath);
        } catch (IOException e) {
            System.err.println("[ErrorLog] Failed to save path: " + e.getMessage());
        }
    }

    /** Current DB path. */
    public static String getPath() {
        return dbPath;
    }

    /**
     * Insert a new error row. If the same ser already exists, use {@link #upsertError(String, String)}.
     * @return true if one row inserted, false otherwise
     */
    public static boolean insertError(String ser, String errorMsg) {
        String sql = "INSERT INTO [" + TABLE_NAME + "] ([ser],[errorMessage]) VALUES (?, ?)";
        try (Connection conn = getConnection();
             PreparedStatement ps = conn.prepareStatement(sql)) {
            ps.setString(1, ser);
            ps.setString(2, errorMsg);
            ensureTableExists(conn);
            return ps.executeUpdate() == 1;
        } catch (SQLException e) {
            System.err.println("[ErrorLog] insertError: " + e.getMessage());
            return false;
        }
    }

    /**
     * Upsert by ser: update message if exists; otherwise insert.
     * @return true if changed (inserted or updated), false if no-op
     */
    public static boolean upsertError(String ser, String errorMsg) {
        String upd = "UPDATE [" + TABLE_NAME + "] SET [errorMessage]=? WHERE [ser]=?";
        String ins = "INSERT INTO [" + TABLE_NAME + "] ([ser],[errorMessage]) VALUES (?, ?)";
        try (Connection conn = getConnection()) {
            ensureTableExists(conn);
            // Try update first
            try (PreparedStatement psU = conn.prepareStatement(upd)) {
                psU.setString(1, errorMsg);
                psU.setString(2, ser);
                int u = psU.executeUpdate();
                if (u > 0) return true; // updated an existing row
            }
            // Insert if not found
            try (PreparedStatement psI = conn.prepareStatement(ins)) {
                psI.setString(1, ser);
                psI.setString(2, errorMsg);
                return psI.executeUpdate() == 1;
            }
        } catch (SQLException e) {
            System.err.println("[ErrorLog] upsertError: " + e.getMessage());
            return false;
        }
    }


    /**
     * Delete a single error by ser (useful when a row is fixed/sent successfully).
     * @return number of rows deleted (0 or 1)
     */
    public static int deleteError(String ser) {
        String sql = "DELETE FROM [" + TABLE_NAME + "] WHERE [ser]=?";
        try (Connection conn = getConnection();
             PreparedStatement ps = conn.prepareStatement(sql)) {
            ensureTableExists(conn);
            ps.setString(1, ser);
            return ps.executeUpdate();
        } catch (SQLException e) {
            System.err.println("[ErrorLog] deleteError: " + e.getMessage());
            return 0;
        }
    }

    /** Return all errors as a map: ser -> message. */
    public static Map<String, String> getAllErrors() {
        Map<String, String> errors = new HashMap<>();
        String sql = "SELECT [ser],[errorMessage] FROM [" + TABLE_NAME + "]";
        try (Connection conn = getConnection();
             Statement st = conn.createStatement()) {
            ensureTableExists(conn);
            try (ResultSet rs = st.executeQuery(sql)) {
                while (rs.next()) {
                    errors.put(rs.getString(1), rs.getString(2));
                }
            }
        } catch (SQLException e) {
            System.err.println("[ErrorLog] getAllErrors: " + e.getMessage());
        }
        return errors;
    }

    /**
     * Remove all errors.
     * @return number of rows deleted
     */
    public static int clearAllErrors() {
        String sql = "DELETE FROM [" + TABLE_NAME + "]";
        try (Connection conn = getConnection();
             Statement st = conn.createStatement()) {
            ensureTableExists(conn);
            return st.executeUpdate(sql);
        } catch (SQLException e) {
            System.err.println("[ErrorLog] clearAllErrors: " + e.getMessage());
            return 0;
        }
    }

    // ---------- Internals ----------

    private static boolean isValidDbPath(String candidate) {
        if (candidate == null || candidate.isBlank()) return false;
        try {
            Path p = Paths.get(candidate);
            if (!Files.exists(p) || !Files.isRegularFile(p)) return false;
            String lower = p.toString().replace('\\', '/').toLowerCase();
            return lower.endsWith(".mdb") || lower.endsWith(".accdb");
        } catch (Exception ignore) {
            return false;
        }
    }

    /** Get a UCanAccess connection and reduce file locks. Also ensures table exists. */
    public static Connection getConnection() throws SQLException {
        String url = "jdbc:ucanaccess://" + dbPath + ";immediatelyReleaseResources=true";
        Connection conn = DriverManager.getConnection(url);
        // Table creation is cheap; keep here for resilience on first call.
        ensureTableExists(conn);
        return conn;
    }

    /** Create table + index if missing. */
    private static void ensureTableExists(Connection conn) throws SQLException {
        try (ResultSet rs = conn.getMetaData().getTables(null, null, TABLE_NAME, null)) {
            if (rs.next()) return; // already exists
        }
        try (Statement st = conn.createStatement()) {
            st.executeUpdate(
                    "CREATE TABLE [" + TABLE_NAME + "] (" +
                            "  [ser] TEXT(50) NOT NULL," +
                            "  [errorMessage] MEMO" +
                            ")"
            );
        }
        try (Statement st = conn.createStatement()) {
            st.executeUpdate("CREATE INDEX idx_error_ser ON [" + TABLE_NAME + "]([ser])");
        } catch (SQLException ignore) {
            // index may already exist
        }
    }

}
