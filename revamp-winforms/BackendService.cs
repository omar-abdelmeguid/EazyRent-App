using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace EazyRentRevamp
{
    public class BackendService
    {
        private readonly DataAccess _dataAccess = new DataAccess();
        private readonly MemoryStore _memoryStore = new MemoryStore();
        private MemoryModel _memory;
        private ErrorDbWriter? _errorDbWriter;

        private string _loginUrl = "http://localhost:8081/api/Auth/login";
        private string _importUrl = "http://localhost:8081/api/GL/ImportJournal";
        private string? _cachedToken;

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        // Headers (match existing Java service defaults)
        private const string _hdrAccept = "text/plain";
        private const string _hdrYear = "2025";
        private const string _hdrActivity = "1";
        private const string _loginUserId = "1";
        private const string _loginPassword = "1";

        // SQL templates: choose based on UI mode selection
        // sqlUnsent: records not yet sent (timestamp is NULL)
        private string _sqlUnsent =
            "SELECT [Room_no],[Descr1],[Rent_no],[Date],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenterCode],[Crcostcentercode],[Ser] " +
            "FROM (" +
            " SELECT [Room_no],[Descr1],[Rent_no],[Date],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenterCode],[Crcostcentercode],[Ser],[TR_TimeStamp] FROM [statement] " +
            " UNION ALL " +
            " SELECT [Room_no],[Descr1],[Rent_no],[Date],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenterCode],[Crcostcentercode],[Ser],[TR_TimeStamp] FROM [Gl_Journal] " +
            ") AS U " +
            "WHERE [Date] >= ? AND [Date] <= ? AND [TR_TimeStamp] IS NULL";
        // sqlSent: records already sent (timestamp is NOT NULL)
        private string _sqlSent =
            "SELECT [Room_no],[Descr1],[Rent_no],[Date],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenterCode],[Crcostcentercode],[Ser] " +
            "FROM (" +
            " SELECT [Room_no],[Descr1],[Rent_no],[Date],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenterCode],[Crcostcentercode],[Ser],[TR_TimeStamp] FROM [statement] " +
            " UNION ALL " +
            " SELECT [Room_no],[Descr1],[Rent_no],[Date],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenterCode],[Crcostcentercode],[Ser],[TR_TimeStamp] FROM [Gl_Journal] " +
            ") AS U " +
            "WHERE [Date] >= ? AND [Date] <= ? AND [TR_TimeStamp] IS NOT NULL";
        // Keep this query minimal so it works across DB variants; BuildJournalForApi applies fallbacks when optional fields are absent.
        private string _sqlSendRange_statment = "SELECT [Ser],[Room_no],[Descr1],[Rent_no],[Date],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenterCode],[Crcostcentercode],[Dr_dtl_ac1],[Dr_dtl_ac2],[Cr_dtl_ac1],[Cr_dtl_ac2] FROM [statement] WHERE [Date] >= ? AND [Date] <= ? AND [TR_TimeStamp] IS NULL";

        private string _sqlSendRange_GL = "SELECT [Ser],[Room_no],[Descr1],[Rent_no],[Date],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenterCode],[Crcostcentercode],[Dr_dtl_ac1],[Dr_dtl_ac2],[Cr_dtl_ac1],[Cr_dtl_ac2] FROM [Gl_Journal] WHERE [Date] >= ? AND [Date] <= ? AND [TR_TimeStamp] IS NULL";

        private string _countUnsentStatement = "SELECT COUNT(*) FROM [statement] WHERE [Date] >= ? AND [Date] <= ? AND [TR_TimeStamp] IS NULL";
        private string _countUnsentGl = "SELECT COUNT(*) FROM [Gl_Journal] WHERE [Date] >= ? AND [Date] <= ? AND [TR_TimeStamp] IS NULL";
        private string _countSentStatement = "SELECT COUNT(*) FROM [statement] WHERE [Date] >= ? AND [Date] <= ? AND [TR_TimeStamp] IS NOT NULL";
        private string _countSentGl = "SELECT COUNT(*) FROM [Gl_Journal] WHERE [Date] >= ? AND [Date] <= ? AND [TR_TimeStamp] IS NOT NULL";

        // Access doesn't support TRUNC(); use a half-open range to ignore time: ts >= fromDate AND ts < (toDate + 1 day)
        private string _errorsql = "SELECT [Room_no],[Descr1],[Rent_no],[Date_OF_DB],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenter],[CrcostCenter],[Ser],[ERORR],[failed_requests_timestamp] FROM [ErrorDBlog] WHERE [failed_requests_timestamp] >= ? AND [failed_requests_timestamp] < DateAdd('d', 1, ?) ORDER BY [failed_requests_timestamp] DESC";
        private string _countErrorsSql = "SELECT COUNT(*) FROM [ErrorDBlog] WHERE [failed_requests_timestamp] >= ? AND [failed_requests_timestamp] < DateAdd('d', 1, ?)";
        // Search helpers: ignore time by using half-open range [fromDate, toDate + 1 day)
        private string _errorSearchSerial =
            "SELECT [Room_no],[Descr1],[Rent_no],[Date_OF_DB],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenter],[CrcostCenter],[Ser],[ERORR],[failed_requests_timestamp] " +
            "FROM [ErrorDBlog] WHERE [Ser] = ? AND [failed_requests_timestamp] >= ? AND [failed_requests_timestamp] < DateAdd('d', 1, ?) " +
            "ORDER BY [failed_requests_timestamp] DESC";

        private string _errorSearchRoomNumber =
            "SELECT [Room_no],[Descr1],[Rent_no],[Date_OF_DB],[Amount],[Type],[DebitAccount1],[DebitAccount2],[CreditAccount1],[CreditAccount2],[DrcostCenter],[CrcostCenter],[Ser],[ERORR],[failed_requests_timestamp] " +
            "FROM [ErrorDBlog] WHERE [Room_no] = ? AND [failed_requests_timestamp] >= ? AND [failed_requests_timestamp] < DateAdd('d', 1, ?) " +
            "ORDER BY [failed_requests_timestamp] DESC";

        private string _countErrorSearch =
            "SELECT COUNT(*) FROM [ErrorDBlog] WHERE [Ser] = ? AND [failed_requests_timestamp] >= ? AND [failed_requests_timestamp] < DateAdd('d', 1, ?)";

        private string _countErrorRoomNumber =
            "SELECT COUNT(*) FROM [ErrorDBlog] WHERE [Room_no] = ? AND [failed_requests_timestamp] >= ? AND [failed_requests_timestamp] < DateAdd('d', 1, ?)";

        private string _markSentStatementBySer = "UPDATE [statement] SET [TR_TimeStamp] = ? WHERE [Ser] = ?";
        private string _markSentGlBySer = "UPDATE [Gl_Journal] SET [TR_TimeStamp] = ? WHERE [Ser] = ?";      
        public BackendService()
        {
            _memory = _memoryStore.Load();

            if (!string.IsNullOrWhiteSpace(_memory.ImportUrl))
            {
                _importUrl = _memory.ImportUrl;
            }
            else if (!string.IsNullOrWhiteSpace(_memory.Endpoint))
            {
                // Back-compat: older memory.json stored import URL in Endpoint
                _importUrl = _memory.Endpoint;
            }

            if (!string.IsNullOrWhiteSpace(_memory.LoginUrl))
            {
                _loginUrl = _memory.LoginUrl;
            }

            if (!string.IsNullOrWhiteSpace(_memory.DatabasePath))
            {
                _dataAccess.SetConnectionPath(_memory.DatabasePath);
                _errorDbWriter = new ErrorDbWriter(_dataAccess, _memory.DatabasePath, _memory.ErrorDbPath);
            }
        }

        public string LoginUrl => _loginUrl;
        public string ImportUrl => _importUrl;
        public string DatabasePath => _memory.DatabasePath ?? string.Empty;
        public string ErrorDbPath => _memory.ErrorDbPath ?? string.Empty;
        public IReadOnlyList<string> RecentDatabases => _memory.RecentDatabases.AsReadOnly();

        public List<Record> FetchRecords(DateTime from, DateTime to, string mode)
        {
            // select SQL based on mode
            string q = (mode == "sent") ? _sqlSent : _sqlUnsent;
            var rows = _dataAccess.Query(q, from, to);
            var list = new List<Record>();
            foreach (var r in rows)
            {
                list.Add(new Record
                {
                    RoomNo = r.GetStringOrEmpty("Room_no"),
                    Descr1 = r.GetStringOrEmpty("Descr1"),
                    RentNo = r.GetStringOrEmpty("Rent_no"),
                    Date = r.GetStringOrEmpty("Date"),
                    Amount = r.GetStringOrEmpty("Amount"),
                    Type = r.GetStringOrEmpty("Type"),
                    DebitAccount1 = r.GetStringOrEmpty("DebitAccount1"),
                    DebitAccount2 = r.GetStringOrEmpty("DebitAccount2"),
                    CreditAccount1 = r.GetStringOrEmpty("CreditAccount1"),
                    CreditAccount2 = r.GetStringOrEmpty("CreditAccount2"),
                    DrCostCenterCode = r.GetStringOrEmpty("DrcostCenterCode"),
                    CrCostCenterCode = r.GetStringOrEmpty("Crcostcentercode"),
                    Ser = r.GetStringOrEmpty("Ser")
                });
            }
            return list;
        }

        public List<ErrorRecord> FetchErrorRecords(DateTime from, DateTime to)
        {
            if (string.IsNullOrWhiteSpace(_memory.ErrorDbPath)) return new List<ErrorRecord>();

            var rows = _dataAccess.QueryOn(_memory.ErrorDbPath, includePassword: false, _errorsql, from, to);
            var list = new List<ErrorRecord>();
            foreach (var r in rows)
            {
                list.Add(new ErrorRecord
                {
                    RoomNo = r.GetStringOrEmpty("Room_no"),
                    Descr1 = r.GetStringOrEmpty("Descr1"),
                    RentNo = r.GetStringOrEmpty("Rent_no"),
                    DateOfDb = r.GetStringOrEmpty("Date_OF_DB"),
                    Amount = r.GetStringOrEmpty("Amount"),
                    Type = r.GetStringOrEmpty("Type"),
                    DebitAccount1 = r.GetStringOrEmpty("DebitAccount1"),
                    DebitAccount2 = r.GetStringOrEmpty("DebitAccount2"),
                    CreditAccount1 = r.GetStringOrEmpty("CreditAccount1"),
                    CreditAccount2 = r.GetStringOrEmpty("CreditAccount2"),
                    DrCostCenter = r.GetStringOrEmpty("DrcostCenter"),
                    CrCostCenter = r.GetStringOrEmpty("CrcostCenter"),
                    Ser = r.GetStringOrEmpty("Ser"),
                    Error = r.GetStringOrEmpty("ERORR"),
                    FailedRequestsTimestamp = r.GetStringOrEmpty("failed_requests_timestamp")
                });
            }
            return list;
        }

        public int CountErrorRecords(DateTime from, DateTime to)
        {
            if (string.IsNullOrWhiteSpace(_memory.ErrorDbPath)) return 0;
            return _dataAccess.CountOn(_memory.ErrorDbPath, includePassword: false, _countErrorsSql, from, to);
        }

        public List<ErrorRecord> SearchErrorRecords(DateTime from, DateTime to, string searchText)
        {
            if (string.IsNullOrWhiteSpace(_memory.ErrorDbPath)) return new List<ErrorRecord>();
            if (string.IsNullOrWhiteSpace(searchText)) return new List<ErrorRecord>();

            var term = searchText.Trim();

            // Union in code: run both queries and de-duplicate.
            var list = new List<ErrorRecord>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddRows(List<Row> rows)
            {
                foreach (var r in rows)
                {
                    var rec = new ErrorRecord
                    {
                        RoomNo = r.GetStringOrEmpty("Room_no"),
                        Descr1 = r.GetStringOrEmpty("Descr1"),
                        RentNo = r.GetStringOrEmpty("Rent_no"),
                        DateOfDb = r.GetStringOrEmpty("Date_OF_DB"),
                        Amount = r.GetStringOrEmpty("Amount"),
                        Type = r.GetStringOrEmpty("Type"),
                        DebitAccount1 = r.GetStringOrEmpty("DebitAccount1"),
                        DebitAccount2 = r.GetStringOrEmpty("DebitAccount2"),
                        CreditAccount1 = r.GetStringOrEmpty("CreditAccount1"),
                        CreditAccount2 = r.GetStringOrEmpty("CreditAccount2"),
                        DrCostCenter = r.GetStringOrEmpty("DrcostCenter"),
                        CrCostCenter = r.GetStringOrEmpty("CrcostCenter"),
                        Ser = r.GetStringOrEmpty("Ser"),
                        Error = r.GetStringOrEmpty("ERORR"),
                        FailedRequestsTimestamp = r.GetStringOrEmpty("failed_requests_timestamp")
                    };

                    var key = $"{rec.Ser}|{rec.RoomNo}|{rec.RentNo}|{rec.FailedRequestsTimestamp}|{rec.Error}";
                    if (seen.Add(key)) list.Add(rec);
                }
            }

            AddRows(_dataAccess.QueryOn(_memory.ErrorDbPath, includePassword: false, _errorSearchSerial, term, from, to));
            AddRows(_dataAccess.QueryOn(_memory.ErrorDbPath, includePassword: false, _errorSearchRoomNumber, term, from, to));

            return list;
        }

        public int CountErrorRecordsSearch(DateTime from, DateTime to, string searchText)
        {
            if (string.IsNullOrWhiteSpace(_memory.ErrorDbPath)) return 0;
            if (string.IsNullOrWhiteSpace(searchText)) return 0;
            var term = searchText.Trim();
            return _dataAccess.CountOn(_memory.ErrorDbPath, includePassword: false, _countErrorSearch, term, from, to)
                 + _dataAccess.CountOn(_memory.ErrorDbPath, includePassword: false, _countErrorRoomNumber, term, from, to);
        }
public (int successStatement, int failStatement, int successGl, int failGl) SendRangeWithStats(DateTime from, DateTime to)
{
    int successStatement = 0, failStatement = 0, successGl = 0, failGl = 0;
    SendRange_statement(from, to, ref successStatement, ref failStatement);
    SendRange_gl(from, to, ref successGl, ref failGl);
    return (successStatement, failStatement, successGl, failGl);
}

    public string SendRange(DateTime from, DateTime to)
    {
        try { _errorDbWriter.EnsureReady(); }
        catch (Exception ex) { return "Error: Could not initialize error database: " + ex.Message; }

        if (_errorDbWriter == null)
            return "Error: No error database configured. Please set the error DB path before sending.";

        int successStatement = 0, failStatement = 0, successGl = 0, failGl = 0;

        string statement = SendRange_statement(from, to, ref successStatement, ref failStatement);
        string gl = SendRange_gl(from, to, ref successGl, ref failGl);

        bool statementOk = statement.ToUpper().Contains("OK") || statement == "No records to send";
        bool glOk = gl.ToUpper().Contains("OK") || gl == "No records to send";

        // build summary
        int totalSuccess = successStatement + successGl;
        int totalFail = failStatement + failGl;

        string summary =
            $"Results:\n" +
            $"Statement → Success: {successStatement}, Failed: {failStatement}\n" +
            $"GL        → Success: {successGl}, Failed: {failGl}\n" +
            $"─────────────────────────\n" +
            $"Total     → Success: {totalSuccess}, Failed: {totalFail}";

        if (statementOk && glOk)
            return "OK\n\n" + summary;

        var errors = new List<string>();
        if (!statementOk) errors.Add("Statement error: " + statement);
        if (!glOk) errors.Add("GL error: " + gl);

        return string.Join(" | ", errors) + "\n\n" + summary;
    }

    public string SendRange_statement(DateTime from, DateTime to, ref int successCount, ref int failCount)
    {
        try
        {
            var rows = _dataAccess.Query(_sqlSendRange_statment, from, to);
            if (rows == null || rows.Count == 0) return "No records to send";

            var token = LoginExact();
            foreach (var row in rows)
            {
                var journal = BuildJournalForApi(row);
                if (journal == null) continue;
                var ser = row.GetStringOrEmpty("Ser");

                var payload = JsonSerializer.Serialize(new[] { journal });
                using var req = new HttpRequestMessage(HttpMethod.Post, _importUrl);
                req.Headers.Accept.Clear();
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                var resp = _httpClient.SendAsync(req).GetAwaiter().GetResult();
                var respBody = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;

                var parsed = TryParseImportResponse(respBody);

                if (IsImportSuccess(parsed))
                {
                    successCount++;
                    if (!string.IsNullOrWhiteSpace(ser))
                        _dataAccess.Execute(_markSentStatementBySer, DateTime.Now, ser);
                }
                else
                {
                    failCount++;
                    SaveImportError(row, parsed, respBody);
                }
            }
            return "OK";
        }
        catch (Exception ex)
        {
            return "Error sending: " + ex.Message + " | SQL(statement): " + _sqlSendRange_statment;
        }
    }

    public string SendRange_gl(DateTime from, DateTime to, ref int successCount, ref int failCount)
    {
        try
        {
            var rows = _dataAccess.Query(_sqlSendRange_GL, from, to);
            if (rows == null || rows.Count == 0) return "No records to send";

            var token = LoginExact();
            foreach (var row in rows)
            {
                var journal = BuildJournalForApi(row);
                if (journal == null) continue;
                var ser = row.GetStringOrEmpty("Ser");

                var payload = JsonSerializer.Serialize(new[] { journal });
                using var req = new HttpRequestMessage(HttpMethod.Post, _importUrl);
                req.Headers.Accept.Clear();
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                var resp = _httpClient.SendAsync(req).GetAwaiter().GetResult();
                var respBody = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;

                var parsed = TryParseImportResponse(respBody);

                if (IsImportSuccess(parsed))
                {
                    successCount++;
                    if (!string.IsNullOrWhiteSpace(ser))
                        _dataAccess.Execute(_markSentGlBySer, DateTime.Now, ser);
                }
                else
                {
                    failCount++;
                    SaveImportError(row, parsed, respBody);
                }
            }
            return "OK";
        }
        catch (Exception ex)
        {
            return "Error sending: " + ex.Message + " | SQL(gl): " + _sqlSendRange_GL;
        }
    }
        private static ImportJournalResponse? TryParseImportResponse(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody)) return null;
            try { return JsonSerializer.Deserialize<ImportJournalResponse>(responseBody); }
            catch { return null; }
        }

        private static bool IsImportSuccess(ImportJournalResponse? parsed)
        {
          
            if (parsed == null) return false;
            return parsed.Code == 0;

        }

        private void SaveImportError(Row sourceRow, ImportJournalResponse? parsed, string responseBody)
        {
            if (_errorDbWriter == null) {
            MessageBox.Show("Critical Error: No error database configured. The request was sent!!");
            return ;
            }
            try
            {
                var message = ExtractImportError(parsed, responseBody);
                _errorDbWriter.Save(sourceRow, message);
            }
            catch
            {
                MessageBox.Show("Error saving error to Database: " + responseBody);
            }
        }

        private static string ExtractImportError(ImportJournalResponse? parsed, string responseBody)
        {
            if (parsed == null) return responseBody ?? string.Empty;

            var msg = parsed.Message ?? string.Empty;
            if (parsed.Content != null && parsed.Content.Count > 0)
            {
                var details = parsed.Content[0].Message ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(details))
                {
                    msg = string.IsNullOrWhiteSpace(msg) ? details : $"{msg} | {details}";
                }
            }

            return msg;
        }

        private sealed class ImportJournalResponse
        {
            [JsonPropertyName("code")]
            public int Code { get; set; }

            [JsonPropertyName("message")]
            public string? Message { get; set; }

            [JsonPropertyName("content")]
            public List<ImportJournalContentItem>? Content { get; set; }
        }

        private sealed class ImportJournalContentItem
        {
            [JsonPropertyName("referenceId")]
            public string? ReferenceId { get; set; }

            [JsonPropertyName("message")]
            public string? Message { get; set; }

            [JsonPropertyName("errorNo")]
            public string? ErrorNo { get; set; }

            [JsonPropertyName("docNo")]
            public string? DocNo { get; set; }

            [JsonPropertyName("docSer")]
            public string? DocSer { get; set; }
        }


        public void SetEndpoint(string url)
        {
            // Back-compat entry point; treat as import URL.
            SetImportUrl(url);
        }

        public void SetLoginUrl(string url)
        {
            _loginUrl = url;
            _memory.LoginUrl = url;
            _memoryStore.Save(_memory);
        }

        public void SetImportUrl(string url)
        {
            _importUrl = url;
            _memory.ImportUrl = url;
            _memoryStore.Save(_memory);
        }

        public void SetErrorDbPath(string path)
        {
            _memory.ErrorDbPath = path ?? string.Empty;
            _memoryStore.Save(_memory);
            _errorDbWriter = new ErrorDbWriter(_dataAccess, _memory.DatabasePath, _memory.ErrorDbPath);
        }

        private string LoginExact()
        {
            if (!string.IsNullOrWhiteSpace(_cachedToken)) return _cachedToken!;

            var body = JsonSerializer.Serialize(new { userId = _loginUserId, password = _loginPassword });
            using var req = new HttpRequestMessage(HttpMethod.Post, _loginUrl);
            req.Headers.TryAddWithoutValidation("accept", _hdrAccept);
            req.Headers.TryAddWithoutValidation("year", _hdrYear);
            req.Headers.TryAddWithoutValidation("activity", _hdrActivity);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var resp = _httpClient.SendAsync(req).GetAwaiter().GetResult();
            var respBody = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;

            var token = ExtractTokenLoose(respBody);
            if (string.IsNullOrWhiteSpace(token))
            {
                token = respBody.Trim().Trim('"');
            }

            _cachedToken = token;
            return token;
        }

        private static string? ExtractTokenLoose(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            var b = body.Trim();
            if (b.StartsWith("\"", StringComparison.Ordinal) && b.EndsWith("\"", StringComparison.Ordinal) && b.Length > 2)
            {
                return b.Substring(1, b.Length - 2);
            }

            const string key = "\"token\"";
            var i = b.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i >= 0)
            {
                var q1 = b.IndexOf('"', i + key.Length);
                if (q1 >= 0)
                {
                    var q2 = b.IndexOf('"', q1 + 1);
                    if (q2 > q1) return b.Substring(q1 + 1, q2 - q1 - 1);
                }
            }

            // Best-effort: return a JWT-like token if present.
            var parts = b.Split(new[] { ' ', '"', '{', '}', ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Length > 20 && part.Contains('.', StringComparison.Ordinal)) return part;
            }

            return null;
        }

        private static Dictionary<string, object>? BuildJournalForApi(Row r)
        {
            if (r == null) return null;

            string GetS(string key) => r.GetStringOrEmpty(key);
            string GetS2(string key1, string key2)
            {
                var a = GetS(key1);
                return !string.IsNullOrWhiteSpace(a) ? a : GetS(key2);
            }

            static double ToDouble(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return 0d;
                return double.TryParse(s, out var v) ? v : 0d;
            }

            static string ToYmd(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                var trimmed = s.Trim();
                var sp = trimmed.IndexOf(' ');
                return sp > 0 ? trimmed.Substring(0, sp) : trimmed;
            }

            static bool IsValid(string s) => !string.IsNullOrWhiteSpace(s) && s.Trim().Length > 1;

            var debitAcct1 = GetS("DebitAccount1");
            var creditAcct1 = GetS("CreditAccount1");
            if (!IsValid(debitAcct1) || !IsValid(creditAcct1)) return null;

            var date = ToYmd(GetS("Date"));
            var amt = Math.Abs(ToDouble(GetS("Amount")));

            var j = new Dictionary<string, object>
            {
                ["docSerExternal"] = GetS2("Ser", "ser"),
                ["branchNo"] = 1,
                ["docDate"] = date,
                ["docNo"] = null!,
                ["jvType"] = 1,
                ["amountLocal"] = amt,
                ["referenceNo"] = "0",
                ["beneficiaryName"] = "",
                ["receiver"] = "",
                ["manualDocNo"] = "",
                ["description"] = "",
                ["addTerminalName"] = "1"
            };

            var details = new List<Dictionary<string, object>>();

            var drCostCenter = GetS2("DrcostCenterCode", "DrcostCenter");
            var crCostCenter = GetS2("CrcostCenterCode", "Crcostcentercode");

            var lineDescr = "room#= " + GetS("Room_no") + " rent#= " + GetS("Rent_no") + " " + GetS("Descr1");

            void AddDr(string acctKey, string amtKey, string dtlKey)
            {
                var acct = GetS(acctKey);
                if (string.IsNullOrWhiteSpace(acct)) return;
                var lineAmt = Math.Abs(ToDouble(GetS(amtKey)));
                if (lineAmt <= 0d) lineAmt = amt;
                details.Add(new Dictionary<string, object>
                {
                    ["docDueDate"] = date,
                    ["accountCode"] = acct,
                    ["accountCodeDtl"] = GetS(dtlKey),
                    ["accountCodeDtlSub"] = "",
                    ["currencyCode"] = "SAR",
                    ["exchangeRate"] = 0,
                    ["drOrCr"] = 1,
                    ["amountLocal"] = lineAmt,
                    ["amountForeign"] = 0,
                    ["costCenterCode"] = drCostCenter,
                    ["chequeNo"] = "0",
                    ["referenceNo"] = "0",
                    ["billNo"] = "",
                    ["billSer"] = "",
                    ["installmentNo"] = 0,
                    ["description"] = lineDescr
                });
            }

            void AddCr(string acctValue, string amtKey, string dtlKey)
            {
                if (string.IsNullOrWhiteSpace(acctValue)) return;
                var lineAmt = Math.Abs(ToDouble(GetS(amtKey)));
                if (lineAmt <= 0d) lineAmt = amt;
                details.Add(new Dictionary<string, object>
                {
                    ["docDueDate"] = date,
                    ["accountCode"] = acctValue,
                    ["accountCodeDtl"] = GetS(dtlKey),
                    ["accountCodeDtlSub"] = "",
                    ["currencyCode"] = "SAR",
                    ["exchangeRate"] = 0,
                    ["drOrCr"] = -1,
                    ["amountLocal"] = lineAmt,
                    ["amountForeign"] = 0,
                    ["costCenterCode"] = crCostCenter,
                    ["chequeNo"] = "0",
                    ["referenceNo"] = "0",
                    ["billNo"] = "",
                    ["billSer"] = "",
                    ["installmentNo"] = 0,
                    ["description"] = lineDescr
                });
            }

            AddDr("DebitAccount1", "DebitAmount1", "Dr_dtl_ac1");
            AddDr("DebitAccount2", "DebitAmount2", "Dr_dtl_ac2");
            AddCr(creditAcct1, "CreditAmount1", "Cr_dtl_ac1");
            AddCr(GetS("CreditAccount2"), "CreditAmount2", "Cr_dtl_ac2");

            j["details"] = details;
            return j;
        }

        public int CountRecords(DateTime from, DateTime to, string mode)
        {
            if (mode == "sent")
            {
                return _dataAccess.Count(_countSentStatement, from, to) + _dataAccess.Count(_countSentGl, from, to);
            }

            return _dataAccess.Count(_countUnsentStatement, from, to) + _dataAccess.Count(_countUnsentGl, from, to);
        }

        public void SetDbPath(string path)
        {
            _dataAccess.SetConnectionPath(path);
            _memory.DatabasePath = path ?? string.Empty;
            _memory.LastUsedDatabase = path;
            _memory.PushRecentDatabase(path);
            _memoryStore.Save(_memory);
            _errorDbWriter = new ErrorDbWriter(_dataAccess, _memory.DatabasePath, _memory.ErrorDbPath);
        }
    }
}
