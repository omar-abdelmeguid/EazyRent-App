using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EazyRentRevamp
{
    public sealed class MemoryStore
    {
        private readonly object _gate = new object();
        private readonly string _path;
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public MemoryStore(string fileName = "memory.json")
        {
            // Prefer storing next to the executable so settings persist for users.
            // (Ensure memory.json is copied to output during build.)
            _path = Path.Combine(AppContext.BaseDirectory, fileName);
        }

        public MemoryModel Load()
        {
            lock (_gate)
            {
                if (!File.Exists(_path)) return new MemoryModel();

                try
                {
                    var json = File.ReadAllText(_path);
                    var model = JsonSerializer.Deserialize<MemoryModel>(json, _jsonOptions) ?? new MemoryModel();
                    model.Normalize();
                    return model;
                }
                catch
                {
                    // If the file is corrupted, fall back to defaults instead of crashing the UI.
                    return new MemoryModel();
                }
            }
        }

        public void Save(MemoryModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            lock (_gate)
            {
                model.Normalize();
                var json = JsonSerializer.Serialize(model, _jsonOptions);
                File.WriteAllText(_path, json);
            }
        }
    }

    public sealed class MemoryModel
    {
        public string DatabasePath { get; set; } = string.Empty;
        public string ErrorDbPath { get; set; } = string.Empty;
        // Import endpoint (GL ImportJournal)
        public string ImportUrl { get; set; } = "http://localhost:8081/api/GL/ImportJournal";
        // Login endpoint
        public string LoginUrl { get; set; } = "http://localhost:8081/api/Auth/login";
        // Legacy field kept for backward compatibility with older memory.json files.
        public string Endpoint { get; set; } = string.Empty;
        public string? LastUsedDatabase { get; set; }
        public List<string> RecentDatabases { get; set; } = new List<string>();

        public void Normalize()
        {
            DatabasePath ??= string.Empty;
            ErrorDbPath ??= string.Empty;
            ImportUrl ??= "http://localhost:8081/api/GL/ImportJournal";
            LoginUrl ??= "http://localhost:8081/api/Auth/login";
            Endpoint ??= string.Empty;
            RecentDatabases ??= new List<string>();

            // Migrate older Endpoint value to ImportUrl if ImportUrl is empty.
            if (string.IsNullOrWhiteSpace(ImportUrl) && !string.IsNullOrWhiteSpace(Endpoint))
            {
                ImportUrl = Endpoint;
            }

            // Keep the list size capped at 3.
            if (RecentDatabases.Count > 3)
            {
                RecentDatabases.RemoveRange(3, RecentDatabases.Count - 3);
            }
        }

        public void PushRecentDatabase(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            // Remove existing instance (case-insensitive on Windows).
            RecentDatabases.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            RecentDatabases.Insert(0, path);

            if (RecentDatabases.Count > 3)
            {
                RecentDatabases.RemoveRange(3, RecentDatabases.Count - 3);
            }
        }
    }
}
