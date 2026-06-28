using System;
using System.IO;
using System.Diagnostics;
using System.Text;

namespace EazyRentRevamp
{
    public class RequestLogger
    {
        private readonly string _logDirectory = "logs";
        private readonly object _lockObject = new object();

        public RequestLogger()
        {
            EnsureLogDirectory();
        }

        private void EnsureLogDirectory()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                    Directory.CreateDirectory(_logDirectory);
            }
            catch { }
        }

        private string GetLogFilePath()
        {
            return Path.Combine(_logDirectory, $"app_{DateTime.Now:yyyy-MM-dd}.log");
        }

        public void LogRequest(string method, string endpoint, string? requestBody = null)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine("================================================================================");
            sb.AppendLine($"[{timestamp}] REQUEST");
            sb.AppendLine($"Method: {method}");
            sb.AppendLine($"Endpoint: {endpoint}");
            if (!string.IsNullOrWhiteSpace(requestBody))
                sb.AppendLine($"Body: {requestBody}");

            WriteToLog(sb.ToString());
        }

        public void LogResponse(int statusCode, string? responseBody = null, long elapsedMs = 0)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Status: {statusCode}");
            if (!string.IsNullOrWhiteSpace(responseBody))
                sb.AppendLine($"Response: {(responseBody.Length > 500 ? responseBody.Substring(0, 500) + "..." : responseBody)}");
            sb.AppendLine($"Response Time: {elapsedMs}ms");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            WriteToLog(sb.ToString());
        }

        public void LogError(string endpoint, string errorMessage)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine("================================================================================");
            sb.AppendLine($"[{timestamp}] ERROR");
            sb.AppendLine($"Endpoint: {endpoint}");
            sb.AppendLine($"Error: {errorMessage}");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            WriteToLog(sb.ToString());
        }

        public void LogImportSuccess(string ser, string responseStatus)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var sb = new StringBuilder();

            sb.AppendLine($"[{timestamp}] SUCCESS | Serial: {ser} | Status: {responseStatus}");

            WriteToLog(sb.ToString());
        }

        public void LogImportFailure(string ser, string errorMessage)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var sb = new StringBuilder();

            sb.AppendLine($"[{timestamp}] FAILURE | Serial: {ser} | Error: {errorMessage}");

            WriteToLog(sb.ToString());
        }

        private void WriteToLog(string message)
        {
            lock (_lockObject)
            {
                try
                {
                    var logFilePath = GetLogFilePath();
                    File.AppendAllText(logFilePath, message, Encoding.UTF8);
                }
                catch { }
            }
        }
    }
}
