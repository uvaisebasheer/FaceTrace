using Microsoft.Data.Sqlite;
using MultiFaceRec.Core.Interfaces;
using MultiFaceRec.Core.Models;

namespace MultiFaceRec.Data;

public sealed class SqliteUserRepository : IUserRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteUserRepository(SqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<UserAccount?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Username, PasswordHash, CreatedAt, LastLoginAt
            FROM Users WHERE Username = $username COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$username", username);

        using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return new UserAccount
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(3)),
            LastLoginAt = reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4))
        };
    }

    public async Task<UserAccount> CreateAsync(string username, string passwordHash, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var createdAt = DateTimeOffset.UtcNow;

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (Username, PasswordHash, CreatedAt)
            VALUES ($username, $hash, $createdAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$hash", passwordHash);
        command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O"));

        var id = (long)(await command.ExecuteScalarAsync(ct))!;

        return new UserAccount { Id = (int)id, Username = username, PasswordHash = passwordHash, CreatedAt = createdAt };
    }

    public async Task UpdateLastLoginAsync(int userId, DateTimeOffset when, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET LastLoginAt = $when WHERE Id = $id;";
        command.Parameters.AddWithValue("$when", when.ToString("O"));
        command.Parameters.AddWithValue("$id", userId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> AnyUsersExistAsync(CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM Users);";
        var result = (long)(await command.ExecuteScalarAsync(ct))!;
        return result == 1;
    }
}
