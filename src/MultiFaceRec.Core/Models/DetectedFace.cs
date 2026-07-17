namespace MultiFaceRec.Core.Models;

/// <summary>
/// One face found in one video frame, before or after it has been matched
/// against the gallery. Carries enough information for both display
/// (bounding box) and further processing (the embedding + raw crop).
/// </summary>
public class DetectedFace
{
    /// <summary>Bounding box in the coordinate space of the frame it was detected in.</summary>
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Detector confidence score, 0..1.</summary>
    public float DetectionConfidence { get; set; }

    /// <summary>The 5-point landmarks YuNet returns (right eye, left eye, nose tip, right mouth, left mouth), x,y pairs.</summary>
    public float[] Landmarks { get; set; } = Array.Empty<float>();

    /// <summary>128-d embedding computed by SFace for this face, filled in by the embedder.</summary>
    public float[]? Embedding { get; set; }

    /// <summary>Best-match person name once recognition has run, or null if unrecognized / not yet processed.</summary>
    public string? RecognizedName { get; set; }

    /// <summary>Cosine similarity to the best match, for display / debugging.</summary>
    public float? MatchScore { get; set; }

    /// <summary>
    /// The closest known person even when their score didn't clear the
    /// match threshold, and the raw score against them — always populated
    /// once the gallery has at least one enrolled face, so the UI can show
    /// "closest guess: X (41%)" instead of a bare "Unknown" with nothing to
    /// diagnose a too-low threshold or a bad embedding from.
    /// </summary>
    public string? BestGuessName { get; set; }
    public float? BestGuessScore { get; set; }
}

/// <summary>
/// All faces found in a single video frame, plus the frame index/timestamp
/// they came from — this is what flows through the video pipeline's channel.
/// </summary>
public class FrameDetectionResult
{
    public required int FrameIndex { get; init; }
    public required TimeSpan Timestamp { get; init; }
    public required List<DetectedFace> Faces { get; init; }

    /// <summary>Raw BGR frame bytes (row-major, 3 bytes/pixel) so the UI can render it without depending on OpenCvSharp types.</summary>
    public required byte[] FrameBgrBytes { get; init; }
    public required int FrameWidth { get; init; }
    public required int FrameHeight { get; init; }
}
