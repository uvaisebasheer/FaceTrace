using MultiFaceRec.Core.Models;

namespace MultiFaceRec.Core.Interfaces;

/// <summary>
/// Abstraction over "find faces in an image". The Vision project provides
/// the real (YuNet/ONNX) implementation; anything upstream only depends on
/// this interface, so the detector can be swapped (e.g. for a cloud Face API)
/// without touching application services.
/// </summary>
public interface IFaceDetector : IDisposable
{
    /// <param name="bgrBytes">Row-major BGR24 pixel bytes.</param>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    List<DetectedFace> Detect(byte[] bgrBytes, int width, int height);
}
