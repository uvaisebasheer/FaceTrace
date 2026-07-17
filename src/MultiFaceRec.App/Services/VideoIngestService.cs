using System.Threading.Channels;
using MultiFaceRec.Core.Models;
using OpenCvSharp;

namespace MultiFaceRec.App.Services;

/// <summary>
/// Replaces hooking Application.Idle to a FrameGrabber method, which pumped
/// video frames on the UI thread itself. Here, decoding + detection +
/// recognition run on a background Task and publish results through a
/// bounded Channel; the UI just reads whatever's ready, on its own timer,
/// so a slow frame never blocks input or window repaint.
/// </summary>
public sealed class VideoIngestService : IAsyncDisposable
{
    private readonly RecognitionService _recognitionService;
    private CancellationTokenSource? _cts;
    private Task? _pumpTask;
    private Channel<FrameDetectionResult>? _channel;

    public VideoIngestService(RecognitionService recognitionService) => _recognitionService = recognitionService;

    /// <summary>Frames per second to sample the source video at — skips frames rather than trying to process every single one in real time.</summary>
    public int TargetFps { get; set; } = 8;

    public ChannelReader<FrameDetectionResult> Start(string videoFilePath)
    {
        Stop(); // ensure any previous run is torn down first

        _cts = new CancellationTokenSource();
        _channel = Channel.CreateBounded<FrameDetectionResult>(new BoundedChannelOptions(capacity: 4)
        {
            FullMode = BoundedChannelFullMode.DropOldest // if the UI falls behind, prefer fresher frames over a growing backlog
        });

        _pumpTask = Task.Run(() => PumpAsync(videoFilePath, _channel.Writer, _cts.Token));
        return _channel.Reader;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task PumpAsync(string videoFilePath, ChannelWriter<FrameDetectionResult> writer, CancellationToken ct)
    {
        Exception? failure = null;
        try
        {
            using var capture = new VideoCapture(videoFilePath);
            if (!capture.IsOpened())
                throw new IOException($"Could not open video file: {videoFilePath}");

            double sourceFps = capture.Fps > 0 ? capture.Fps : 25.0;
            int frameSkip = Math.Max(1, (int)Math.Round(sourceFps / TargetFps));

            int frameIndex = 0;
            using var frame = new Mat();

            while (!ct.IsCancellationRequested && capture.Read(frame) && !frame.Empty())
            {
                if (frameIndex % frameSkip == 0)
                {
                    byte[] bgrBytes = MatToBytes(frame);
                    var faces = _recognitionService.ProcessFrame(bgrBytes, frame.Width, frame.Height);

                    var result = new FrameDetectionResult
                    {
                        FrameIndex = frameIndex,
                        Timestamp = TimeSpan.FromSeconds(frameIndex / sourceFps),
                        Faces = faces,
                        FrameBgrBytes = bgrBytes,
                        FrameWidth = frame.Width,
                        FrameHeight = frame.Height
                    };

                    // Best-effort publish — with DropOldest this never blocks
                    // the pipeline on a slow UI consumer.
                    await writer.WriteAsync(result, ct);
                }

                frameIndex++;
            }
        }
        catch (OperationCanceledException)
        {
            // normal on Stop()
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            writer.Complete(failure);
        }
    }

    private static byte[] MatToBytes(Mat bgrFrame)
    {
        int size = (int)(bgrFrame.Total() * bgrFrame.ElemSize());
        var bytes = new byte[size];
        System.Runtime.InteropServices.Marshal.Copy(bgrFrame.Data, bytes, 0, size);
        return bytes;
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        if (_pumpTask is not null)
        {
            try { await _pumpTask; } catch { /* already surfaced via channel completion */ }
        }
    }
}
