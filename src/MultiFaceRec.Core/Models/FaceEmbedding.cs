namespace MultiFaceRec.Core.Models;

/// <summary>
/// A single 128-d SFace embedding vector tied to a <see cref="Person"/>.
/// A person can have several embeddings (different angles/lighting)
/// captured over multiple enrollment sessions — this is what lets
/// recognition tolerate more real-world variation than the old
/// single-eigenface-per-label approach.
/// </summary>
public class FaceEmbedding
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public float[] Vector { get; set; } = Array.Empty<float>();
    public DateTimeOffset CreatedAt { get; set; }
    public string? SourceVideoName { get; set; }
}
