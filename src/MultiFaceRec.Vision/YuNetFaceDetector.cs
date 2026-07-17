using MultiFaceRec.Core.Interfaces;
using MultiFaceRec.Core.Models;
using OpenCvSharp;

namespace MultiFaceRec.Vision;


public sealed class YuNetFaceDetector : IFaceDetector
{
    private readonly string _modelPath;
    private readonly float _scoreThreshold;
    private readonly float _nmsThreshold;
    private readonly int _topK;
    private readonly object _lock = new();

    private FaceDetectorYN? _detector;
    private int _detectorWidth;
    private int _detectorHeight;

    /// <param name="modelPath">Path to yunet.onnx (see models/README.md).</param>
    /// <param name="scoreThreshold">Minimum detector confidence to keep a face, 0..1.</param>
    /// <param name="nmsThreshold">Non-max-suppression IoU threshold for de-duplicating overlapping boxes.</param>
    /// <param name="topK">Max number of candidate boxes considered before NMS.</param>
    public YuNetFaceDetector(string modelPath, float scoreThreshold = 0.8f, float nmsThreshold = 0.3f, int topK = 5000)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                $"YuNet model not found at '{modelPath}'. See models/README.md for the download link.", modelPath);

        _modelPath = modelPath;
        _scoreThreshold = scoreThreshold;
        _nmsThreshold = nmsThreshold;
        _topK = topK;
    }

    public List<DetectedFace> Detect(byte[] bgrBytes, int width, int height)
    {
        lock (_lock)
        {
            using var mat = BytesToMat(bgrBytes, width, height);
            EnsureDetectorForSize(width, height);

            using var faces = new Mat();
            _detector!.Detect(mat, faces);

            var results = new List<DetectedFace>();
            for (int i = 0; i < faces.Rows; i++)
            {
                // Each row: x, y, w, h, then 5 landmark (x,y) pairs, then score -- 15 floats total.
                var row = faces.Row(i);
                float x = row.At<float>(0, 0);
                float y = row.At<float>(0, 1);
                float w = row.At<float>(0, 2);
                float h = row.At<float>(0, 3);
                float score = row.At<float>(0, 14);

                var landmarks = new float[10];
                for (int lm = 0; lm < 10; lm++)
                    landmarks[lm] = row.At<float>(0, 4 + lm);

                results.Add(new DetectedFace
                {
                    X = (int)Math.Max(0, x),
                    Y = (int)Math.Max(0, y),
                    Width = (int)w,
                    Height = (int)h,
                    DetectionConfidence = score,
                    Landmarks = landmarks
                });
            }

            return results;
        }
    }

    private void EnsureDetectorForSize(int width, int height)
    {
        if (_detector is not null && _detectorWidth == width && _detectorHeight == height)
            return;

        _detector?.Dispose();
        _detector = FaceDetectorYN.Create(_modelPath, string.Empty, new Size(width, height),
            _scoreThreshold, _nmsThreshold, _topK);
        _detectorWidth = width;
        _detectorHeight = height;
    }

    internal static Mat BytesToMat(byte[] bgrBytes, int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3);
        System.Runtime.InteropServices.Marshal.Copy(bgrBytes, 0, mat.Data, bgrBytes.Length);
        return mat;
    }

    public void Dispose() => _detector?.Dispose();
}
