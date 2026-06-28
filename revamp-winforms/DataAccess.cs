using System;
using System.Collections.Generic;
using System.Data.Odbc;

namespace EazyRentRevamp
{
    public class DataAccess
    {
        private string _dbPath = "";
        private static readonly string _dbPassword = Obfuscation.GetDbPassword();
        private const string AccessOdbcDriverName = "Microsoft Access Driver (*.mdb, *.accdb)";

        public void SetConnectionPath(string path)
        {
            _dbPath = path;
        }

        private string BuildConnectionString(bool includePassword = true)
        {
            if (string.IsNullOrEmpty(_dbPath))
                throw new InvalidOperationException("Database path is not set.");

            var conn =
                $"Driver={{{AccessOdbcDriverName}}};Dbq={_dbPath};";

            if (includePassword && !string.IsNullOrWhiteSpace(_dbPassword))
            {
                conn += $"PWD={_dbPassword};";
            }

            return conn;
        }


        private static string BuildConnectionString(string dbPath, bool includePassword)
        {
            if (string.IsNullOrEmpty(dbPath)) throw new InvalidOperationException("Database path is not set.");
            return includePassword
                ? $"Driver={{{AccessOdbcDriverName}}};Dbq={dbPath};PWD={_dbPassword};"
                : $"Driver={{{AccessOdbcDriverName}}};Dbq={dbPath};";
        }

        public List<Row> Query(string sql, params object[] parameters)
        {
            var list = new List<Row>();
            using var conn = new OdbcConnection(BuildConnectionString());
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var p in parameters)
            {
                var param = cmd.CreateParameter();
                param.Value = p ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                var row = new Row();
                for (int i = 0; i < rdr.FieldCount; i++)
                {
                    var name = rdr.GetName(i);
                    var val = rdr.IsDBNull(i) ? null : rdr.GetValue(i);
                    row.Values[name] = val;
                }
                list.Add(row);
            }
            return list;
        }

        public List<Row> QueryOn(string dbPath, bool includePassword, string sql, params object[] parameters)
        {
            var list = new List<Row>();
            using var conn = new OdbcConnection(BuildConnectionString(dbPath, includePassword));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var p in parameters)
            {
                var param = cmd.CreateParameter();
                param.Value = p ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                var row = new Row();
                for (int i = 0; i < rdr.FieldCount; i++)
                {
                    var name = rdr.GetName(i);
                    var val = rdr.IsDBNull(i) ? null : rdr.GetValue(i);
                    row.Values[name] = val;
                }
                list.Add(row);
            }
            return list;
        }

        public int Count(string sql, params object[] parameters)
        {
            using var conn = new OdbcConnection(BuildConnectionString());
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var p in parameters)
            {
                var param = cmd.CreateParameter();
                param.Value = p ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value) return 0;
            try { return Convert.ToInt32(result); }
            catch { return 0; }
        }

        public int Execute(string sql, params object[] parameters)
        {
            using var conn = new OdbcConnection(BuildConnectionString());
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var p in parameters)
            {
                var param = cmd.CreateParameter();
                param.Value = p ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
            return cmd.ExecuteNonQuery();
        }

        public int ExecuteOn(string dbPath, bool includePassword, string sql, params object[] parameters)
        {
            using var conn = new OdbcConnection(BuildConnectionString(dbPath, includePassword));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var p in parameters)
            {
                var param = cmd.CreateParameter();
                param.Value = p ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
            return cmd.ExecuteNonQuery();
        }

        public int CountOn(string dbPath, bool includePassword, string sql, params object[] parameters)
        {
            using var conn = new OdbcConnection(BuildConnectionString(dbPath, includePassword));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var p in parameters)
            {
                var param = cmd.CreateParameter();
                param.Value = p ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value) return 0;
            try { return Convert.ToInt32(result); }
            catch { return 0; }
        }




    }

    public class Row
    {
        public Dictionary<string, object?> Values { get; } = new Dictionary<string, object?>();

        public string GetStringOrEmpty(string key)
        {
            if (!Values.ContainsKey(key) || Values[key] == null) return string.Empty;
            return Convert.ToString(Values[key]) ?? string.Empty;
        }
    }
}
