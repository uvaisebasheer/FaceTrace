using MultiFaceRec.Core.Models;

namespace MultiFaceRec.Core.Interfaces;

public interface IPersonRepository
{
    Task<Person> AddAsync(string name, string? notes, CancellationToken ct = default);
    Task<Person?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Person?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<List<Person>> GetAllAsync(CancellationToken ct = default);
}

public interface IFaceEmbeddingRepository
{
    Task<FaceEmbedding> AddAsync(int personId, float[] vector, string? sourceVideoName, CancellationToken ct = default);
    Task<List<(FaceEmbedding Embedding, string PersonName)>> GetAllWithPersonNameAsync(CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<UserAccount?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<UserAccount> CreateAsync(string username, string passwordHash, CancellationToken ct = default);
    Task UpdateLastLoginAsync(int userId, DateTimeOffset when, CancellationToken ct = default);
    Task<bool> AnyUsersExistAsync(CancellationToken ct = default);
}
