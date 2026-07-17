using Microsoft.Data.Sqlite;
using MultiFaceRec.Core.Interfaces;
using MultiFaceRec.Core.Models;

namespace MultiFaceRec.Data;

public sealed class SqliteFaceEmbeddingRepository : IFaceEmbeddingRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteFaceEmbeddingRepository(SqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<FaceEmbedding> AddAsync(int personId, float[] vector, string? sourceVideoName, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        var createdAt = DateTimeOffset.UtcNow;
        byte[] blob = FloatsToBytes(vector);

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FaceEmbeddings (PersonId, Vector, CreatedAt, SourceVideoName)
            VALUES ($personId, $vector, $createdAt, $sourceVideoName);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$personId", personId);
        command.Parameters.AddWithValue("$vector", blob);
        command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O"));
        command.Parameters.AddWithValue("$sourceVideoName", (object?)sourceVideoName ?? DBNull.Value);

        var id = (long)(await command.ExecuteScalarAsync(ct))!;

        return new FaceEmbedding
        {
            Id = (int)id,
            PersonId = personId,
            Vector = vector,
            CreatedAt = createdAt,
            SourceVideoName = sourceVideoName
        };
    }

    public async Task<List<(FaceEmbedding Embedding, string PersonName)>> GetAllWithPersonNameAsync(CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT fe.Id, fe.PersonId, fe.Vector, fe.CreatedAt, fe.SourceVideoName, p.Name
            FROM FaceEmbeddings fe
            JOIN People p ON p.Id = fe.PersonId;
            """;

        var results = new List<(FaceEmbedding, string)>();
        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var blob = (byte[])reader["Vector"];
            var embedding = new FaceEmbedding
            {
                Id = reader.GetInt32(0),
                PersonId = reader.GetInt32(1),
                Vector = BytesToFloats(blob),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(3)),
                SourceVideoName = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
            results.Add((embedding, reader.GetString(5)));
        }

        return results;
    }

    private static byte[] FloatsToBytes(float[] floats)
    {
        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToFloats(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
