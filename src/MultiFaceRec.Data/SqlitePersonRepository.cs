using Microsoft.Data.Sqlite;
using MultiFaceRec.Core.Interfaces;
using MultiFaceRec.Core.Models;

namespace MultiFaceRec.Data;

public sealed class SqlitePersonRepository : IPersonRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqlitePersonRepository(SqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<Person> AddAsync(string name, string? notes, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var createdAt = DateTimeOffset.UtcNow;

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO People (Name, Notes, CreatedAt)
            VALUES ($name, $notes, $createdAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O"));

        var id = (long)(await command.ExecuteScalarAsync(ct))!;

        return new Person { Id = (int)id, Name = name, Notes = notes, CreatedAt = createdAt };
    }

    public async Task<Person?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Notes, CreatedAt FROM People WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapPerson(reader) : null;
    }

    public async Task<Person?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Notes, CreatedAt FROM People WHERE Name = $name COLLATE NOCASE;";
        command.Parameters.AddWithValue("$name", name);

        using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapPerson(reader) : null;
    }

    public async Task<List<Person>> GetAllAsync(CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Notes, CreatedAt FROM People ORDER BY Name;";

        var results = new List<Person>();
        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(MapPerson(reader));

        return results;
    }

    private static Person MapPerson(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        Notes = reader.IsDBNull(2) ? null : reader.GetString(2),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(3))
    };
}
