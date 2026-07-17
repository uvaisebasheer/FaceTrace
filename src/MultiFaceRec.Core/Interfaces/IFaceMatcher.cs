namespace MultiFaceRec.Core.Interfaces;

public record MatchCandidate(int PersonId, string PersonName, float Score, bool IsMatch);

/// <summary>
/// Abstraction over "given an embedding, find the closest known person".
/// The default implementation does an in-memory cosine-similarity scan,
/// which is fine up to a few thousand embeddings; swap for a real vector
/// index (FAISS/pgvector) if the gallery grows beyond that.
/// </summary>
public interface IFaceMatcher
{
    /// <summary>
    /// Rebuilds the in-memory index from the current contents of the
    /// embeddings repository. Call this once at startup and again any
    /// time enrollment changes — NOT on every frame.
    /// </summary>
    Task RefreshIndexAsync(CancellationToken ct = default);

    /// <summary>
    /// Finds the closest known person for the given embedding, or null only
    /// if the gallery is empty. IsMatch tells you whether the score actually
    /// cleared the configured threshold — the candidate (and its raw score)
    /// is still returned even when it didn't, so callers can display "closest
    /// guess was X at 41%" instead of a bare "Unknown" with no numbers to
    /// diagnose a bad threshold or a bad embedding from.
    /// </summary>
    MatchCandidate? FindBestMatch(float[] embedding);
}
