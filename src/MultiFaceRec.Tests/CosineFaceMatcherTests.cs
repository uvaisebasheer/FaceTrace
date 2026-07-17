using Moq;
using MultiFaceRec.Core.Interfaces;
using MultiFaceRec.Core.Models;
using MultiFaceRec.Vision;
using Xunit;

namespace MultiFaceRec.Tests;

public class CosineFaceMatcherTests
{
    [Fact]
    public async Task FindBestMatch_ReturnsNull_WhenIndexEmpty()
    {
        var repo = new Mock<IFaceEmbeddingRepository>();
        repo.Setup(r => r.GetAllWithPersonNameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(FaceEmbedding, string)>());

        var matcher = new CosineFaceMatcher(repo.Object);
        await matcher.RefreshIndexAsync();

        var result = matcher.FindBestMatch(new float[] { 1, 0, 0 });

        Assert.Null(result);
    }

    [Fact]
    public async Task FindBestMatch_ReturnsClosestPerson_AboveThreshold()
    {
        var alice = new FaceEmbedding { Id = 1, PersonId = 1, Vector = new float[] { 1, 0, 0 } };
        var bob = new FaceEmbedding { Id = 2, PersonId = 2, Vector = new float[] { 0, 1, 0 } };

        var repo = new Mock<IFaceEmbeddingRepository>();
        repo.Setup(r => r.GetAllWithPersonNameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(FaceEmbedding, string)> { (alice, "Alice"), (bob, "Bob") });

        var matcher = new CosineFaceMatcher(repo.Object, similarityThreshold: 0.9f);
        await matcher.RefreshIndexAsync();

        // Nearly identical to Alice's vector
        var result = matcher.FindBestMatch(new float[] { 0.99f, 0.01f, 0f });

        Assert.NotNull(result);
        Assert.Equal("Alice", result!.PersonName);
    }

    [Fact]
    public async Task FindBestMatch_ReturnsCandidateWithIsMatchFalse_WhenBelowThreshold()
    {
        var alice = new FaceEmbedding { Id = 1, PersonId = 1, Vector = new float[] { 1, 0, 0 } };

        var repo = new Mock<IFaceEmbeddingRepository>();
        repo.Setup(r => r.GetAllWithPersonNameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(FaceEmbedding, string)> { (alice, "Alice") });

        // Very strict threshold — an orthogonal vector should not clear it,
        // but the candidate + raw score should still come back for diagnostics.
        var matcher = new CosineFaceMatcher(repo.Object, similarityThreshold: 0.9f);
        await matcher.RefreshIndexAsync();

        var result = matcher.FindBestMatch(new float[] { 0, 1, 0 });

        Assert.NotNull(result);
        Assert.False(result!.IsMatch);
        Assert.Equal("Alice", result.PersonName); // still tells you who it was closest to
    }

    [Fact]
    public async Task RefreshIndexAsync_PicksUpNewlyEnrolledFaces()
    {
        var repo = new Mock<IFaceEmbeddingRepository>();
        repo.SetupSequence(r => r.GetAllWithPersonNameAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(FaceEmbedding, string)>())
            .ReturnsAsync(new List<(FaceEmbedding, string)>
            {
                (new FaceEmbedding { Id = 1, PersonId = 1, Vector = new float[] { 1, 0 } }, "Alice")
            });

        var matcher = new CosineFaceMatcher(repo.Object, similarityThreshold: 0.9f);

        await matcher.RefreshIndexAsync();
        Assert.Null(matcher.FindBestMatch(new float[] { 1, 0 }));

        // Simulates EnrollmentService calling RefreshIndexAsync after a new face is added.
        await matcher.RefreshIndexAsync();
        Assert.NotNull(matcher.FindBestMatch(new float[] { 1, 0 }));
    }
}
