using MultiFaceRec.Core.Interfaces;

namespace MultiFaceRec.Vision;

/// <summary>
/// In-memory nearest-neighbor matcher using cosine similarity.

public sealed class CosineFaceMatcher : IFaceMatcher
{
    private readonly IFaceEmbeddingRepository _embeddingRepository;
    private readonly float _similarityThreshold;

    private volatile List<(int PersonId, string PersonName, float[] Vector)> _index = new();

    /// <param name="similarityThreshold">
    /// Minimum cosine similarity (0..1) to count as a match.
    /// </param>
    public CosineFaceMatcher(IFaceEmbeddingRepository embeddingRepository, float similarityThreshold = 0.363f)
    {
        _embeddingRepository = embeddingRepository;
        _similarityThreshold = similarityThreshold;
    }

    public async Task RefreshIndexAsync(CancellationToken ct = default)
    {
        var rows = await _embeddingRepository.GetAllWithPersonNameAsync(ct);
        _index = rows
            .Select(r => (r.Embedding.PersonId, r.PersonName, r.Embedding.Vector))
            .ToList();
    }

    public MatchCandidate? FindBestMatch(float[] embedding)
    {
        var snapshot = _index; // volatile read — safe against a concurrent RefreshIndexAsync
        if (snapshot.Count == 0) return null;

        int bestPersonId = -1;
        string bestName = string.Empty;
        float bestScore = -1f;

        foreach (var (personId, personName, vector) in snapshot)
        {
            float score = CosineSimilarity(embedding, vector);
            if (score > bestScore)
            {
                bestScore = score;
                bestPersonId = personId;
                bestName = personName;
            }
        }

        return new MatchCandidate(bestPersonId, bestName, bestScore, IsMatch: bestScore >= _similarityThreshold);
    }

   
    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return -1f;

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return -1f;
        return (float)(dot / (Math.Sqrt(magA) * Math.Sqrt(magB)));
    }
}
