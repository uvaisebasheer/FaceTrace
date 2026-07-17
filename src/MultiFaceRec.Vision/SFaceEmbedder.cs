using MultiFaceRec.Core.Interfaces;
using MultiFaceRec.Core.Models;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using System.Threading;

namespace MultiFaceRec.Vision;
public sealed class SFaceEmbedder : IFaceEmbedder
{
    // Standard 112x112 ArcFace/MobileFaceNet reference landmark template.
  
    private static readonly Point2f[] ReferenceLandmarks112 =
    {
        new(38.2946f, 51.6963f),
        new(73.5318f, 51.5014f),
        new(56.0252f, 71.7366f),
        new(41.5493f, 92.3655f),
        new(70.7299f, 92.2041f),
    };

    private const int AlignedSize = 112;

    private readonly Net _net;
    private readonly object _lock = new();
    private readonly string? _debugCropFolder;
    private readonly string? _debugLogFile;
    private int _debugCropCounter;

    /// <param name="modelPath">Path to face_recognition_sface.onnx (see models/README.md).</param>
    /// <param name="debugCropFolder">
    /// If set, every aligned 112x112 crop is saved as a .png here before
    /// being fed to the network. Turn this on temporarily if recognition
    /// looks wrong — a correctly aligned crop should look like a
    /// straight-on, tightly cropped face with eyes roughly level and near
    /// the top third of the image. If the saved crops look skewed, off-
    /// center, or clearly wrong, the landmark alignment (not the threshold)
    /// is the thing to fix.
    /// </param>
    /// <param name="debugLogFile">
    /// If set, appends one line per embedded face to this text file: the
    /// raw shape of the network's output blob (before flattening), the L2
    /// norm of the extracted vector, and its first 8 values. This is the
    /// fastest way to tell "the output shape is wrong" (norm/shape look
    /// degenerate, e.g. shape reads as 1x1) apart from "the shape is fine
    /// but two different people's vectors are numerically too close" (an
    /// alignment/preprocessing quality issue, not an extraction bug).
    /// </param>
    public SFaceEmbedder(string modelPath, string? debugCropFolder = null, string? debugLogFile = null)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                $"SFace model not found at '{modelPath}'. See models/README.md for the download link.", modelPath);

        _net = CvDnn.ReadNetFromOnnx(modelPath);
        _debugCropFolder = debugCropFolder;
        if (_debugCropFolder is not null)
            Directory.CreateDirectory(_debugCropFolder);

        _debugLogFile = debugLogFile;
        if (_debugLogFile is not null)
        {
            var dir = Path.GetDirectoryName(_debugLogFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            LogNetworkArchitectureOnce();
        }
    }

    /// <summary>
    /// Logs the network's full layer list and its actual unconnected output
    /// layer name(s) once, up front. This checks a specific hypothesis: that
    /// `_net.Forward()` with no arguments might not be grabbing the layer we
    /// think it is. If `GetUnconnectedOutLayersNames()` returns more than one
    /// name, or a name that doesn't look like a final embedding/feature
    /// layer, that's the next thing to fix -- call the named overload of
    /// Forward() explicitly instead of trusting the parameterless default.
    /// </summary>
    private void LogNetworkArchitectureOnce()
    {
        try
        {
            string[] layerNames = _net.GetLayerNames();
            string[] outputNames = _net.GetUnconnectedOutLayersNames();
            string line = $"[network architecture] totalLayers={layerNames.Length}  " +
                          $"outputLayers=[{string.Join(", ", outputNames)}]  " +
                          $"lastFewLayers=[{string.Join(", ", layerNames.TakeLast(Math.Min(5, layerNames.Length)))}]";
            File.AppendAllText(_debugLogFile!, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            File.AppendAllText(_debugLogFile!, $"[network architecture] failed to introspect: {ex.Message}{Environment.NewLine}");
        }
    }

    public float[] Embed(byte[] bgrBytes, int width, int height, DetectedFace face)
    {
        lock (_lock)
        {
            using var frame = YuNetFaceDetector.BytesToMat(bgrBytes, width, height);

            int debugN = _debugCropFolder is not null ? Interlocked.Increment(ref _debugCropCounter) : 0;
            if (_debugCropFolder is not null)
                SaveLandmarkVisualization(frame, face, debugN);

            using var aligned = AlignFace(frame, face);

            if (_debugCropFolder is not null)
                aligned.SaveImage(Path.Combine(_debugCropFolder, $"crop_{debugN:0000}.png"));

            // swapRB=false alone did NOT separate same-person from
            // different-person scores in real testing (still 0.94-0.98
            // across guaranteed-different people). Next hypothesis: this
            // SFace ONNX export may already include its own internal
            // normalization (Sub/Div nodes) as part of the graph -- a common
            // pattern for production-exported models, meant to make the
            // model self-contained regardless of caller preprocessing. If
            // so, applying scaleFactor=1/127.5 + mean-subtraction ON TOP of
            // that double-normalizes the input, which wouldn't make the
            // network fail outright, but would systematically shrink/distort
            // pixel values into a range the network's early layers were
            // never trained to see meaningful signal in -- consistent with
            // "some variation gets through, but it's mostly noise" (matches
            // what we measured). This version feeds raw 0..255 pixels
            // untouched and lets the graph do any normalization itself.
            using var blob = CvDnn.BlobFromImage(aligned, scaleFactor: 1.0, size: new Size(AlignedSize, AlignedSize),
                mean: new Scalar(0, 0, 0), swapRB: false, crop: false);

            _net.SetInput(blob);
            using var output = _net.Forward();

            // net.Forward() can return a multi-dimensional blob (e.g. shape
            // [1, 128, 1, 1]) rather than a plain 2D matrix. Reading
            // output.Cols directly on that kind of blob can silently return
            // 1 instead of 128 -- i.e. only the FIRST value gets extracted
            // as "the embedding". Comparing two 1-element vectors with
            // cosine similarity always yields exactly +/-1 (a "perfect
            // match") no matter whose face it was, which is exactly the
            // "everyone matches the same enrolled name" symptom. Flattening
            // via Reshape(1, 1) first guarantees we read every element of
            // the blob, however it was originally shaped.
            using var flat = output.Reshape(cn: 1, rows: 1);
            int length = (int)flat.Total();
            var vector = new float[length];
            for (int i = 0; i < length; i++)
                vector[i] = flat.At<float>(0, i);

            if (_debugLogFile is not null)
                LogEmbeddingDiagnostics(debugN, output, vector);

            return vector;
        }
    }

    /// <summary>
    /// Appends one diagnostic line per embedded face: the network output's
    /// raw dimensions before flattening, the flattened vector's length and
    /// L2 norm, and its first 8 values. This is what actually tells you
    /// whether two different people's embeddings are numerically distinct
    /// (norms/values clearly differ) or suspiciously close/identical
    /// (a real computation bug, not a threshold or alignment issue).
    /// </summary>
    private void LogEmbeddingDiagnostics(int debugN, Mat rawOutput, float[] vector)
    {
        double normSquared = 0;
        foreach (float v in vector) normSquared += v * v;
        double norm = Math.Sqrt(normSquared);

        string dims = string.Join("x", Enumerable.Range(0, rawOutput.Dims).Select(rawOutput.Size));
        string first8 = string.Join(", ", vector.Take(8).Select(v => v.ToString("F4")));

        string line = $"{DateTimeOffset.Now:HH:mm:ss.fff}  n={debugN:0000}  rawOutputDims={dims}  " +
                      $"flattenedLength={vector.Length}  L2Norm={norm:F4}  first8=[{first8}]";

        File.AppendAllText(_debugLogFile!, line + Environment.NewLine);
    }

    /// <summary>
    /// Saves a copy of the face's bounding-box region with each of the 5
    /// raw landmark points drawn as a numbered, colored dot directly on
    /// top of the real (unaligned) image. This is the most direct way to
    /// check whether YuNet's landmark order matches what this code assumes
    /// -- open a saved landmarks_*.png: dot 0 and 1 should sit on the two
    /// eyes, dot 2 on the nose tip, dots 3 and 4 on the two mouth corners.
    /// If they're on the wrong features, or in a different order than
    /// that, the fix is to reorder the indices used when building
    /// `detected` in <see cref="AlignFace"/> -- NOT to touch the reference
    /// template, which is fixed by what the model was trained on.
    /// </summary>
    private void SaveLandmarkVisualization(Mat frame, DetectedFace face, int debugN)
    {
        const int padding = 20;
        int x0 = Math.Max(0, face.X - padding);
        int y0 = Math.Max(0, face.Y - padding);
        int x1 = Math.Min(frame.Width, face.X + face.Width + padding);
        int y1 = Math.Min(frame.Height, face.Y + face.Height + padding);
        if (x1 <= x0 || y1 <= y0) return;

        var cropRect = new Rect(x0, y0, x1 - x0, y1 - y0);
        using var crop = new Mat(frame, cropRect).Clone();

        (string Label, Scalar Color)[] pointStyles =
        {
            ("0", Scalar.Red),      // assumed: right eye
            ("1", Scalar.Lime),     // assumed: left eye
            ("2", Scalar.Yellow),   // assumed: nose tip
            ("3", Scalar.Cyan),     // assumed: right mouth corner
            ("4", Scalar.Magenta),  // assumed: left mouth corner
        };

        for (int i = 0; i < 5 && i < pointStyles.Length; i++)
        {
            float px = face.Landmarks[i * 2] - x0;
            float py = face.Landmarks[i * 2 + 1] - y0;
            var center = new Point((int)px, (int)py);
            Cv2.Circle(crop, center, 4, pointStyles[i].Color, thickness: -1);
            Cv2.PutText(crop, pointStyles[i].Label, new Point((int)px + 6, (int)py - 6),
                HersheyFonts.HersheySimplex, 0.5, pointStyles[i].Color, 1);
        }

        crop.SaveImage(Path.Combine(_debugCropFolder!, $"landmarks_{debugN:0000}.png"));
    }

    /// <summary>
    /// Estimates the similarity transform from the detected 5-point
    /// landmarks to the canonical 112x112 template and warps the face into
    /// alignment -- equivalent to what cv::FaceRecognizerSF::alignCrop does
    /// internally, just done explicitly here.
    /// </summary>
    private static Mat AlignFace(Mat frame, DetectedFace face)
    {
        if (face.Landmarks.Length < 10)
            throw new InvalidOperationException(
                "Face alignment requires 5 landmarks (10 floats) from the detector; got fewer. " +
                "Make sure IFaceDetector is returning YuNet's landmark output.");

        var detected = new[]
        {
            new Point2f(face.Landmarks[0], face.Landmarks[1]),
            new Point2f(face.Landmarks[2], face.Landmarks[3]),
            new Point2f(face.Landmarks[4], face.Landmarks[5]),
            new Point2f(face.Landmarks[6], face.Landmarks[7]),
            new Point2f(face.Landmarks[8], face.Landmarks[9]),
        };

        using var detectedInput = InputArray.Create(detected);
        using var referenceInput = InputArray.Create(ReferenceLandmarks112);
        using var transform = Cv2.EstimateAffinePartial2D(detectedInput, referenceInput);
        if (transform.Empty())
            throw new InvalidOperationException("Could not estimate an alignment transform for this face's landmarks.");

        var aligned = new Mat();
        Cv2.WarpAffine(frame, aligned, transform, new Size(AlignedSize, AlignedSize));
        return aligned;
    }

    public void Dispose() => _net.Dispose();
}
