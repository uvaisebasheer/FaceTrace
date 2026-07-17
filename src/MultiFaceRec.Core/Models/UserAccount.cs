namespace MultiFaceRec.Core.Models;

/// <summary>
/// A local application account. Replaces the single plain-text
/// username/password pair that used to live in the Windows Registry.
/// </summary>
public class UserAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;

    /// <summary>BCrypt hash — never the raw password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
