using System;
using System.IO;

namespace EazyRentRevamp
{
    internal sealed class ErrorDbWriter
    {
        private const string TableName = "ErrorDBlog";
        private const string PreferredFileNameAccdb = "ErrorDB.accdb";
        private const string PreferredFileNameMdb = "error_db.mdb";
        private readonly DataAccess _dataAccess;
        private readonly string _errorDbPath;

        public ErrorDbWriter(DataAccess dataAccess, string mainDbPath, string? errorDbPath = null)
        {
            _dataAccess = dataAccess;
            _errorDbPath = ResolveErrorDbPath(mainDbPath, errorDbPath);
        }

        public void Save(Row sourceRow, string errorMessage)
        {
            EnsureDbAndTable();

            _dataAccess.ExecuteOn(
                _errorDbPath,
                includePassword: false,
                $"INSERT INTO [ErrorDBLog] ([Room_no],[Descr1],[Rent_no],[Date_OF_DB],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenter],[CrcostCenter],[Ser],[ERORR],[failed_requests_timestamp]) " +
                "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                sourceRow.GetStringOrEmpty("Room_no"),
                sourceRow.GetStringOrEmpty("Descr1"),
                sourceRow.GetStringOrEmpty("Rent_no"),
                sourceRow.Values.TryGetValue("Date", out var dt) ? dt ?? DBNull.Value : DBNull.Value,
                sourceRow.Values.TryGetValue("Amount", out var amt) ? amt ?? DBNull.Value : DBNull.Value,
                sourceRow.GetStringOrEmpty("Type"),
                sourceRow.GetStringOrEmpty("DebitAccount1"),
                sourceRow.GetStringOrEmpty("DebitAccount2"),
                sourceRow.GetStringOrEmpty("CreditAccount1"),
                sourceRow.GetStringOrEmpty("CreditAccount2"),
                sourceRow.GetStringOrEmpty("DrcostCenterCode"),
                sourceRow.GetStringOrEmpty("Crcostcentercode"),
                sourceRow.GetStringOrEmpty("Ser"),
                errorMessage ?? string.Empty,
                DateTime.Now
            );
        }

        private void EnsureDbAndTable()
        {
            if (!File.Exists(_errorDbPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_errorDbPath)!);
                CreateAccessDb(_errorDbPath);
            }

            try
            {
                _dataAccess.ExecuteOn(
                    _errorDbPath,
                    includePassword: false,
                    $"CREATE TABLE [ErrorDBLog] (" +
                    "[Room_no] LONG," +
                    "[Descr1] TEXT(255)," +
                    "[Rent_no] LONG," +
                    "[Date_OF_DB] DATETIME," +
                    "[Amount] DOUBLE," +
                    "[Type] TEXT(50)," +
                    "[DebitAccount1] TEXT(50)," +
                    "[DebitAccount2] TEXT(50)," +
                    "[CreditAccount1] TEXT(50)," +
                    "[CreditAccount2] TEXT(50)," +
                    "[DrcostCenter] TEXT(50)," +
                    "[CrcostCenter] TEXT(50)," +
                    "[Ser] TEXT(50)," +
                    "[ERORR] MEMO," +
                    "[failed_requests_timestamp] DATETIME" +
                    ")"
                );
            }
            catch
            {
                // Could be "already exists" or a real failure. Verify the table is actually readable.
                _dataAccess.CountOn(_errorDbPath, includePassword: false, $"SELECT COUNT(*) FROM [ErrorDBLog]");
            }

            // Ensure column exists for upgraded schemas.
            try
            {
                _dataAccess.ExecuteOn(
                    _errorDbPath,
                    includePassword: false,
                    $"ALTER TABLE [ErrorDBLog] ADD COLUMN [failed_requests_timestamp] DATETIME"
                );
            }
            catch
            {
                // Column already exists; ignore.
            }
        }

        private static string ResolveErrorDbPath(string mainDbPath, string? configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            string? mainDbDir = !string.IsNullOrWhiteSpace(mainDbPath) ? Path.GetDirectoryName(mainDbPath) : null;
            var baseDir = AppContext.BaseDirectory;

            var candidates = new[]
            {
                Path.Combine(baseDir, PreferredFileNameAccdb),
                Path.Combine(baseDir, PreferredFileNameMdb),
                mainDbDir != null ? Path.Combine(mainDbDir, PreferredFileNameAccdb) : null,
                mainDbDir != null ? Path.Combine(mainDbDir, PreferredFileNameMdb) : null,
            };

            foreach (var c in candidates)
            {
                if (string.IsNullOrWhiteSpace(c)) continue;
                if (File.Exists(c)) return c;
            }

            // Default location: next to the main DB (if known) otherwise next to the executable.
            var targetDir = mainDbDir ?? baseDir;
            return Path.Combine(targetDir, PreferredFileNameAccdb);
        }
        public void EnsureReady()
        {
            EnsureDbAndTable(); // already has all the logic
        }

        private static void CreateAccessDb(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            if (string.Equals(ext, ".accdb", StringComparison.OrdinalIgnoreCase))
            {
                if (TryCreateAccdb(filePath)) return;
                // fall back to mdb if ACE isn't available
                var mdbFallback = Path.ChangeExtension(filePath, ".mdb");
                if (TryCreateMdb(mdbFallback)) return;
                throw new InvalidOperationException("Unable to create Access error database (.accdb/.mdb). ACE/Jet OLE DB providers not available.");
            }

            // .mdb
            if (TryCreateMdb(filePath)) return;
            if (TryCreateAccdb(Path.ChangeExtension(filePath, ".accdb"))) return;

            throw new InvalidOperationException("Unable to create Access error database (.mdb/.accdb). Jet/ACE OLE DB providers not available.");
        }

        private static bool TryCreateMdb(string filePath)
        {
            try
            {
                var t = Type.GetTypeFromProgID("ADOX.Catalog");
                if (t == null) return false;

                dynamic cat = Activator.CreateInstance(t)!;
                var cs = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={filePath};Jet OLEDB:Engine Type=5;";
                cat.Create(cs);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryCreateAccdb(string filePath)
        {
            try
            {
                var t = Type.GetTypeFromProgID("ADOX.Catalog");
                if (t == null) return false;

                dynamic cat = Activator.CreateInstance(t)!;
                var cs = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={filePath};";
                cat.Create(cs);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
