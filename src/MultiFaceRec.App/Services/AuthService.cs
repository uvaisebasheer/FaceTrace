using MultiFaceRec.Core.Interfaces;
using MultiFaceRec.Core.Models;

namespace MultiFaceRec.App.Services;

public record LoginResult(bool Succeeded, string? ErrorMessage, UserAccount? User);

/// <summary>
/// Replaces MainLogin.cs / Register.cs, which stored and compared plain-text
/// credentials in the Windows Registry. Passwords are hashed with BCrypt
/// (salted, slow-by-design) both at registration and at login, and the
/// value is never compared as a raw string.
/// </summary>
public sealed class AuthService
{
    private const int MinPasswordLength = 8;

    private readonly IUserRepository _userRepository;

    public AuthService(IUserRepository userRepository) => _userRepository = userRepository;

    public async Task<bool> HasAnyAccountAsync(CancellationToken ct = default) =>
        await _userRepository.AnyUsersExistAsync(ct);

    public async Task<(bool Succeeded, string? Error)> RegisterAsync(string username, string password, string confirmPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return (false, "Please enter a username.");
        if (password.Length < MinPasswordLength)
            return (false, $"Password must be at least {MinPasswordLength} characters.");
        if (password != confirmPassword)
            return (false, "Confirmation password does not match.");

        var existing = await _userRepository.GetByUsernameAsync(username, ct);
        if (existing is not null)
            return (false, "That username is already taken.");

        string hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        await _userRepository.CreateAsync(username, hash, ct);
        return (true, null);
    }

    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new LoginResult(false, "Please enter a username & password.", null);

        var user = await _userRepository.GetByUsernameAsync(username, ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new LoginResult(false, "Invalid username or password.", null);

        await _userRepository.UpdateLastLoginAsync(user.Id, DateTimeOffset.UtcNow, ct);
        return new LoginResult(true, null, user);
    }
}
