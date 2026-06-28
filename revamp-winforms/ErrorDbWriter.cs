using System;
using System.IO;

namespace EazyRentRevamp
{
    internal sealed class ErrorDbWriter
    {
        private const int MemoSafeLength = 30000;
        private const int Text50Length = 50;
        private const int Text255Length = 255;
        private const string ErrorTableName = "ErrorDBlog";
        private const string SuccessTableName = "SuccessDBLog";
        private const string RequestColumnName = "request_body";
        private const string ResponseColumnName = "response_body";
        private const string PreferredFileNameAccdb = "ErrorDB.accdb";
        private const string PreferredFileNameMdb = "error_db.mdb";
        private readonly DataAccess _dataAccess;
        private readonly string _errorDbPath;

        public ErrorDbWriter(DataAccess dataAccess, string mainDbPath, string? errorDbPath = null)
        {
            _dataAccess = dataAccess;
            _errorDbPath = ResolveErrorDbPath(mainDbPath, errorDbPath);
        }

        public void SaveError(Row sourceRow, string errorMessage, string requestBody, string responseBody)
        {
            EnsureDbAndTable();

            _dataAccess.ExecuteOn(
                _errorDbPath,
                includePassword: false,
                $"INSERT INTO [{ErrorTableName}] ([Room_no],[Descr1],[Rent_no],[Date_OF_DB],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenter],[CrcostCenter],[Ser],[ERORR],[failed_requests_timestamp],[{RequestColumnName}],[{ResponseColumnName}]) " +
                $"VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,{ToAccessTextLiteral(LimitMemo(errorMessage))},?,{ToAccessTextLiteral(LimitMemo(requestBody))},{ToAccessTextLiteral(LimitMemo(responseBody))})",
                GetRowNumberValue(sourceRow, "Room_no"),
                LimitText(sourceRow.GetStringOrEmpty("Descr1"), Text255Length),
                GetRowNumberValue(sourceRow, "Rent_no"),
                sourceRow.Values.TryGetValue("Date", out var dt) ? dt ?? DBNull.Value : DBNull.Value,
                GetRowNumericValue(sourceRow, "Amount"),
                LimitText(sourceRow.GetStringOrEmpty("Type"), Text50Length),
                LimitText(sourceRow.GetStringOrEmpty("DebitAccount1"), Text50Length),
                LimitText(sourceRow.GetStringOrEmpty("DebitAccount2"), Text50Length),
                LimitText(sourceRow.GetStringOrEmpty("CreditAccount1"), Text50Length),
                LimitText(sourceRow.GetStringOrEmpty("CreditAccount2"), Text50Length),
                LimitText(GetRowStringOrFallback(sourceRow, "DrcostCenterCode", "DrcostCenter"), Text50Length),
                LimitText(GetRowStringOrFallback(sourceRow, "Crcostcentercode", "CrcostCenter"), Text50Length),
                LimitText(sourceRow.GetStringOrEmpty("Ser"), Text50Length),
                DateTime.Now
            );
        }

        public void SaveSuccess(Row sourceRow, string successMessage, string requestBody, string responseBody)
        {
            EnsureDbAndTable();

            _dataAccess.ExecuteOn(
                _errorDbPath,
                includePassword: false,
                $"INSERT INTO [{SuccessTableName}] ([Room_no],[Descr1],[Rent_no],[Date_OF_DB],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenter],[CrcostCenter],[Ser],[SUCCESS_MESSAGE],[successful_requests_timestamp],[{RequestColumnName}],[{ResponseColumnName}]) " +
                $"VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,{ToAccessTextLiteral(LimitMemo(successMessage))},?,{ToAccessTextLiteral(LimitMemo(requestBody))},{ToAccessTextLiteral(LimitMemo(responseBody))})",
                GetRowNumberValue(sourceRow, "Room_no"),
                LimitText(sourceRow.GetStringOrEmpty("Descr1"), Text255Length),
                GetRowNumberValue(sourceRow, "Rent_no"),
                sourceRow.Values.TryGetValue("Date", out var dt) ? dt ?? DBNull.Value : DBNull.Value,
                GetRowNumericValue(sourceRow, "Amount"),
                LimitText(sourceRow.GetStringOrEmpty("Type"), Text50Length),
                LimitText(sourceRow.GetStringOrEmpty("DebitAccount1"), Text50Length),
                LimitText(sourceRow.GetStringOrEmpty("DebitAccount2"), Text50Length),
                LimitText(sourceRow.GetStringOrEmpty("CreditAccount1"), Text50Length),
                LimitText(sourceRow.GetStringOrEmpty("CreditAccount2"), Text50Length),
                LimitText(GetRowStringOrFallback(sourceRow, "DrcostCenterCode", "DrcostCenter"), Text50Length),
                LimitText(GetRowStringOrFallback(sourceRow, "Crcostcentercode", "CrcostCenter"), Text50Length),
                LimitText(sourceRow.GetStringOrEmpty("Ser"), Text50Length),
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
                    $"CREATE TABLE [{ErrorTableName}] (" +
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
                    "[failed_requests_timestamp] DATETIME," +
                    $"[{RequestColumnName}] MEMO," +
                    $"[{ResponseColumnName}] MEMO" +
                    ")"
                );
            }
            catch
            {
                // Could be "already exists" or a real failure. Verify the table is actually readable.
                _dataAccess.CountOn(_errorDbPath, includePassword: false, $"SELECT COUNT(*) FROM [{ErrorTableName}]");
            }

            try
            {
                _dataAccess.ExecuteOn(
                    _errorDbPath,
                    includePassword: false,
                    $"CREATE TABLE [{SuccessTableName}] (" +
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
                    "[SUCCESS_MESSAGE] MEMO," +
                    "[successful_requests_timestamp] DATETIME," +
                    $"[{RequestColumnName}] MEMO," +
                    $"[{ResponseColumnName}] MEMO" +
                    ")"
                );
            }
            catch
            {
                _dataAccess.CountOn(_errorDbPath, includePassword: false, $"SELECT COUNT(*) FROM [{SuccessTableName}]");
            }

            EnsureColumn(ErrorTableName, "failed_requests_timestamp", "DATETIME");
            EnsureColumn(ErrorTableName, RequestColumnName, "MEMO");
            EnsureColumn(ErrorTableName, ResponseColumnName, "MEMO");
            EnsureColumn(SuccessTableName, RequestColumnName, "MEMO");
            EnsureColumn(SuccessTableName, ResponseColumnName, "MEMO");
            EnsureColumn(SuccessTableName, "SUCCESS_MESSAGE", "MEMO");
            EnsureColumn(SuccessTableName, "successful_requests_timestamp", "DATETIME");
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

        private void EnsureColumn(string tableName, string columnName, string accessType)
        {
            try
            {
                _dataAccess.ExecuteOn(
                    _errorDbPath,
                    includePassword: false,
                    $"ALTER TABLE [{tableName}] ADD COLUMN [{columnName}] {accessType}"
                );
            }
            catch
            {
                // Column already exists; ignore.
            }
        }

        private static object GetRowNumericValue(Row sourceRow, string key)
        {
            if (!sourceRow.Values.TryGetValue(key, out var value) || value == null)
                return DBNull.Value;

            if (value is double || value is float)
                return Convert.ToDouble(value);

            if (value is decimal || value is int || value is long || value is short || value is byte)
                return Convert.ToDouble(value);

            return double.TryParse(Convert.ToString(value), out var parsed) ? parsed : DBNull.Value;
        }

        private static object GetRowNumberValue(Row sourceRow, string key)
        {
            if (!sourceRow.Values.TryGetValue(key, out var value) || value == null)
                return DBNull.Value;

            if (value is int || value is long || value is short || value is byte)
                return value;

            return int.TryParse(Convert.ToString(value), out var parsed) ? parsed : DBNull.Value;
        }

        private static string GetRowStringOrFallback(Row sourceRow, string primaryKey, string fallbackKey)
        {
            var primary = sourceRow.GetStringOrEmpty(primaryKey);
            return string.IsNullOrWhiteSpace(primary) ? sourceRow.GetStringOrEmpty(fallbackKey) : primary;
        }

        private static string LimitText(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        private static string LimitMemo(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= MemoSafeLength ? value : value.Substring(0, MemoSafeLength);
        }

        private static string ToAccessTextLiteral(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "''";
            return $"'{value.Replace("'", "''")}'";
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
