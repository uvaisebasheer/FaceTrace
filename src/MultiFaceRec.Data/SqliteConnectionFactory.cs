using System.Reflection;
using Microsoft.Data.Sqlite;

namespace MultiFaceRec.Data;

/// <summary>
/// Owns the connection string and applies the schema script on first use.
/// Deliberately simple (a single embedded .sql file) — fine for a
/// single-user desktop app; swap for a real migrator (e.g. DbUp) if this
/// ever needs to support rolling schema changes across many installs.
/// </summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;
    private bool _schemaApplied;
    private readonly object _lock = new();

    public SqliteConnectionFactory(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        EnsureSchema(connection);
        return connection;
    }

    private void EnsureSchema(SqliteConnection connection)
    {
        if (_schemaApplied) return;
        lock (_lock)
        {
            if (_schemaApplied) return;

            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "MultiFaceRec.Data.Migrations.001_InitialSchema.sql";
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded schema resource '{resourceName}' not found.");
            using var reader = new StreamReader(stream);
            string schemaSql = reader.ReadToEnd();

            using var command = connection.CreateCommand();
            command.CommandText = schemaSql;
            command.ExecuteNonQuery();

            _schemaApplied = true;
        }
    }
}
