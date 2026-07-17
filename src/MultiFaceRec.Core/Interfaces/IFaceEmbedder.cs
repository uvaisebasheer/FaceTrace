using MultiFaceRec.Core.Models;

namespace MultiFaceRec.Core.Interfaces;

/// <summary>
/// Abstraction over "turn a detected face into a comparable embedding vector".
/// Replaces Eigenfaces (which needed the whole training set present to compute
/// anything) with a stateless per-face embedding, which is what makes caching
/// and incremental enrollment straightforward.
/// </summary>
public interface IFaceEmbedder : IDisposable
{
    /// <summary>
    /// Aligns and embeds the given face crop from the full frame.
    /// </summary>
    /// <param name="bgrBytes">Row-major BGR24 pixel bytes of the FULL frame (alignment needs surrounding context).</param>
    /// <param name="width">Full frame width.</param>
    /// <param name="height">Full frame height.</param>
    /// <param name="face">The detected face (bounding box + landmarks) to embed.</param>
    /// <returns>A 128-d embedding vector.</returns>
    float[] Embed(byte[] bgrBytes, int width, int height, DetectedFace face);
}
