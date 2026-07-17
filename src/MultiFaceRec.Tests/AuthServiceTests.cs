using Moq;
using MultiFaceRec.App.Services;
using MultiFaceRec.Core.Interfaces;
using MultiFaceRec.Core.Models;
using Xunit;

namespace MultiFaceRec.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task RegisterAsync_Fails_WhenPasswordsDontMatch()
    {
        var repo = new Mock<IUserRepository>();
        var auth = new AuthService(repo.Object);

        var (succeeded, error) = await auth.RegisterAsync("alice", "password123", "different");

        Assert.False(succeeded);
        Assert.Equal("Confirmation password does not match.", error);
        repo.Verify(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_Fails_WhenPasswordTooShort()
    {
        var repo = new Mock<IUserRepository>();
        var auth = new AuthService(repo.Object);

        var (succeeded, error) = await auth.RegisterAsync("alice", "short", "short");

        Assert.False(succeeded);
        Assert.Contains("at least", error);
    }

    [Fact]
    public async Task RegisterAsync_StoresHashedPassword_NotRawPassword()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAccount?)null);

        string? capturedHash = null;
        repo.Setup(r => r.CreateAsync("alice", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, hash, _) => capturedHash = hash)
            .ReturnsAsync(new UserAccount { Id = 1, Username = "alice" });

        var auth = new AuthService(repo.Object);
        var (succeeded, _) = await auth.RegisterAsync("alice", "correct horse battery", "correct horse battery");

        Assert.True(succeeded);
        Assert.NotNull(capturedHash);
        Assert.NotEqual("correct horse battery", capturedHash); // never store the raw password
        Assert.True(BCrypt.Net.BCrypt.Verify("correct horse battery", capturedHash));
    }

    [Fact]
    public async Task LoginAsync_Fails_WhenPasswordWrong()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("correctPassword");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccount { Id = 1, Username = "alice", PasswordHash = hash });

        var auth = new AuthService(repo.Object);
        var result = await auth.LoginAsync("alice", "wrongPassword");

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid username or password.", result.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_Succeeds_WhenPasswordCorrect()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("correctPassword");
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByUsernameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccount { Id = 1, Username = "alice", PasswordHash = hash });

        var auth = new AuthService(repo.Object);
        var result = await auth.LoginAsync("alice", "correctPassword");

        Assert.True(result.Succeeded);
        Assert.Equal("alice", result.User!.Username);
        repo.Verify(r => r.UpdateLastLoginAsync(1, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
