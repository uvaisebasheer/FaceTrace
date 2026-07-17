using MultiFaceRec.Core.Interfaces;
using MultiFaceRec.Core.Models;
using System.Threading;

namespace MultiFaceRec.App.Services;

/// <summary>
/// Replaces the per-frame body of FrameGrabber() in the old MainForm.cs.
/// Detection + embedding run per frame (unavoidable — that's the actual
/// work), but matching against the gallery is now a cheap lookup against
/// an index that's built once by EnrollmentService, not rebuilt here.
/// </summary>
public sealed class RecognitionService
{
    private readonly IFaceDetector _detector;
    private readonly IFaceEmbedder _embedder;
    private readonly IFaceMatcher _matcher;
    private readonly string? _debugLogFile;
    private int _frameCounter;

    /// <param name="debugLogFile">
    /// If set, every frame with 2+ detected faces gets a line logging the
    /// cosine similarity between every pair of faces in that SAME frame.
    /// Different bounding boxes in the same frame are guaranteed to be
    /// different real people (barring a detection bug), so this is the
    /// cleanest possible ground truth for "are the embeddings actually
    /// discriminating between people" — no pose/time/lighting difference to
    /// muddy the comparison the way comparing across separate frames does.
    /// </param>
    public RecognitionService(IFaceDetector detector, IFaceEmbedder embedder, IFaceMatcher matcher, string? debugLogFile = null)
    {
        _detector = detector;
        _embedder = embedder;
        _matcher = matcher;
        _debugLogFile = debugLogFile;
        if (_debugLogFile is not null)
        {
            var dir = Path.GetDirectoryName(_debugLogFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
    }

    /// <summary>
    /// Call once at application start (after the database has at least been
    /// created) so the matcher has something to compare against before the
    /// first frame arrives.
    /// </summary>
    public Task WarmUpAsync(CancellationToken ct = default) => _matcher.RefreshIndexAsync(ct);

    /// <summary>
    /// Detects and (where possible) recognizes every face in one frame.
    /// Pure function of its inputs — no shared mutable state — so it's safe
    /// to call from a background pipeline thread.
    /// </summary>
    public List<DetectedFace> ProcessFrame(byte[] bgrBytes, int width, int height)
    {
        var faces = _detector.Detect(bgrBytes, width, height);

        foreach (var face in faces)
        {
            face.Embedding = _embedder.Embed(bgrBytes, width, height, face);
            var candidate = _matcher.FindBestMatch(face.Embedding);
            if (candidate is not null)
            {
                face.BestGuessName = candidate.PersonName;
                face.BestGuessScore = candidate.Score;

                if (candidate.IsMatch)
                {
                    face.RecognizedName = candidate.PersonName;
                    face.MatchScore = candidate.Score;
                }
            }
        }

        if (_debugLogFile is not null && faces.Count >= 2)
            LogSameFramePairwiseSimilarities(faces);

        return faces;
    }

    private void LogSameFramePairwiseSimilarities(List<DetectedFace> faces)
    {
        int frameN = Interlocked.Increment(ref _frameCounter);
        var lines = new List<string>();

        for (int i = 0; i < faces.Count; i++)
        {
            for (int j = i + 1; j < faces.Count; j++)
            {
                if (faces[i].Embedding is null || faces[j].Embedding is null) continue;
                float score = MultiFaceRec.Vision.CosineFaceMatcher.CosineSimilarity(faces[i].Embedding!, faces[j].Embedding!);
                lines.Add($"frame={frameN:0000}  face{i + 1}-vs-face{j + 1}  cosineSim={score:F4}  " +
                          $"(face{i + 1}={faces[i].RecognizedName ?? "Unknown"}, face{j + 1}={faces[j].RecognizedName ?? "Unknown"})");
            }
        }

        if (lines.Count > 0)
            File.AppendAllLines(_debugLogFile!, lines);
    }
}
