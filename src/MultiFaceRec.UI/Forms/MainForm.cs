using System.Drawing.Imaging;
using System.Threading.Channels;
using MultiFaceRec.App.Services;
using MultiFaceRec.Core.Models;

namespace MultiFaceRec.UI.Forms;

/// <summary>
///  AUTHOR : UVAISE K B
/// </summary>
public sealed class MainForm : Form
{
    private readonly EnrollmentService _enrollmentService;
    private readonly VideoIngestService _videoIngestService;

    private readonly PictureBox _framePictureBox = new() { Left = 12, Top = 12, Width = 640, Height = 480, BorderStyle = BorderStyle.FixedSingle, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly ListBox _facesListBox = new() { Left = 664, Top = 36, Width = 220, Height = 276 };
    private readonly CheckBox _freezeCheckBox = new() { Text = "Freeze frame to label", Left = 664, Top = 316, Width = 220 };
    private readonly TextBox _nameBox = new() { Left = 664, Top = 348, Width = 220 };
    private readonly Button _enrollButton = new() { Text = "Enroll selected face", Left = 664, Top = 380, Width = 220 };
    private readonly Button _openButton = new() { Text = "Open video…", Left = 12, Top = 500, Width = 120 };
    private readonly Button _startStopButton = new() { Text = "Start", Left = 140, Top = 500, Width = 100, Enabled = false };
    private readonly Label _statusLabel = new() { Left = 12, Top = 534, Width = 872 };
    private readonly OpenFileDialog _openFileDialog = new() { Filter = "Video files|*.mp4;*.avi;*.mkv;*.mov|All files|*.*" };

    private string? _currentVideoPath;
    private List<DetectedFace> _currentFaces = new();
    private byte[] _currentFrameBytes = Array.Empty<byte>();
    private int _currentFrameWidth, _currentFrameHeight;
    private bool _isRunning;

    public MainForm(EnrollmentService enrollmentService, VideoIngestService videoIngestService)
    {
        _enrollmentService = enrollmentService;
        _videoIngestService = videoIngestService;

        Text = "FaceTrace — Face Recognition in Video";
        Width = 920;
        Height = 610;

        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        BackgroundImage = Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "mainformback.jpg"));
        BackgroundImageLayout = ImageLayout.Stretch;



        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += (_, _) => Application.Exit();

        Controls.Add(_framePictureBox);
        Controls.Add(new Label { Text = "Faces in current frame:", Left = 664, Top = 12, Width = 220 , BackColor=Color.Transparent});
        Controls.Add(_facesListBox);
        Controls.Add(_freezeCheckBox);
        Controls.Add(new Label { Text = "Name to enroll as:", Left = 664, Top = 328, Width = 220,BackColor=Color.Transparent });
        Controls.Add(_nameBox);
        Controls.Add(_enrollButton);
        Controls.Add(_openButton);
        Controls.Add(_startStopButton);
        Controls.Add(_statusLabel);

        _statusLabel.BackColor = Color.Transparent;

        _openButton.Click += OnOpenClicked;
        _startStopButton.Click += OnStartStopClicked;
        _enrollButton.Click += OnEnrollClicked;
        _facesListBox.SelectedIndexChanged += (_, _) =>
        {
            UpdateNameBoxFromSelection();
            RedrawWithHighlight();
        };
    }

    private void OnOpenClicked(object? sender, EventArgs e)
    {
        if (_openFileDialog.ShowDialog(this) != DialogResult.OK) return;

        _currentVideoPath = _openFileDialog.FileName;
        _statusLabel.Text = $"Loaded: {Path.GetFileName(_currentVideoPath)}";
        _startStopButton.Enabled = true;
    }

    private void OnStartStopClicked(object? sender, EventArgs e)
    {
        if (_isRunning)
        {
            _videoIngestService.Stop();
            _isRunning = false;
            _startStopButton.Text = "Start";
            _statusLabel.Text = "Stopped.";
            return;
        }

        if (_currentVideoPath is null) return;

        ChannelReader<FrameDetectionResult> reader = _videoIngestService.Start(_currentVideoPath);
        _isRunning = true;
        _startStopButton.Text = "Stop";
        _ = ConsumeFramesAsync(reader, _currentVideoPath);
    }

    private async Task ConsumeFramesAsync(ChannelReader<FrameDetectionResult> reader, string videoPath)
    {
        try
        {
            await foreach (var result in reader.ReadAllAsync())
            {
                if (IsDisposed) return;
                BeginInvoke(() => OnFrameArrived(result, videoPath));
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
                BeginInvoke(() => _statusLabel.Text = $"Video ended or failed: {ex.Message}");
        }
        finally
        {
            if (!IsDisposed)
                BeginInvoke(() =>
                {
                    _isRunning = false;
                    _startStopButton.Text = "Start";
                });
        }
    }

    
    private void OnFrameArrived(FrameDetectionResult result, string videoPath)
    {
        if (_freezeCheckBox.Checked)
            return;

        _currentFaces = result.Faces;
        _currentFrameBytes = result.FrameBgrBytes;
        _currentFrameWidth = result.FrameWidth;
        _currentFrameHeight = result.FrameHeight;

        RedrawWithHighlight();

        _facesListBox.Items.Clear();
        for (int i = 0; i < _currentFaces.Count; i++)
        {
            var f = _currentFaces[i];
            string entry;
            if (f.RecognizedName is not null)
            {
                entry = $"Face {i + 1}: {f.RecognizedName} ({f.MatchScore:P0})";
            }
            else if (f.BestGuessName is not null)
            {
                entry = $"Face {i + 1}: Unknown (closest: {f.BestGuessName} @ {f.BestGuessScore:P0})";
            }
            else
            {
                entry = $"Face {i + 1}: Unknown (no faces enrolled yet)";
            }
            _facesListBox.Items.Add(entry);
        }

        _statusLabel.Text = $"{Path.GetFileName(videoPath)} — frame {result.FrameIndex} @ {result.Timestamp:mm\\:ss} — {_currentFaces.Count} face(s)";
    }

    private void RedrawWithHighlight()
    {
        if (_currentFrameBytes.Length == 0) return;

        int selectedIndex = _facesListBox.SelectedIndex;

        using var bitmap = BgrBytesToBitmap(_currentFrameBytes, _currentFrameWidth, _currentFrameHeight);
        using (var g = Graphics.FromImage(bitmap))
        using (var normalPen = new Pen(Color.LimeGreen, 2))
        using (var highlightPen = new Pen(Color.Gold, 4))
        using (var font = new Font("Segoe UI", 12, FontStyle.Bold))
        using (var normalBrush = new SolidBrush(Color.LimeGreen))
        using (var highlightBrush = new SolidBrush(Color.Gold))
        {
            for (int i = 0; i < _currentFaces.Count; i++)
            {
                var face = _currentFaces[i];
                bool isSelected = i == selectedIndex;

                g.DrawRectangle(isSelected ? highlightPen : normalPen, face.X, face.Y, face.Width, face.Height);
                string label = $"{i + 1}: {face.RecognizedName ?? "Unknown"}" + (isSelected ? "  ◄ selected" : "");
                g.DrawString(label, font, isSelected ? highlightBrush : normalBrush, face.X, Math.Max(0, face.Y - 20));
            }
        }

        _framePictureBox.Image?.Dispose();
        _framePictureBox.Image = (Bitmap)bitmap.Clone();
    }

    private void UpdateNameBoxFromSelection()
    {
        int index = _facesListBox.SelectedIndex;
        if (index < 0 || index >= _currentFaces.Count) return;
        _nameBox.Text = _currentFaces[index].RecognizedName ?? string.Empty;
    }

    private async void OnEnrollClicked(object? sender, EventArgs e)
    {
        int index = _facesListBox.SelectedIndex;
        if (index < 0 || index >= _currentFaces.Count)
        {
            MessageBox.Show(this, "Select a face from the list first.", "No face selected",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show(this, "Enter a name for this face.", "Name required",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_freezeCheckBox.Checked)
        {
            MessageBox.Show(this, "Check \"Freeze frame to label\" first, so the face list doesn't change under you while you enroll.",
                "Freeze the frame first", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

       
        var face = _currentFaces[index];
        var person = await _enrollmentService.EnrollFaceAsync(
            _nameBox.Text.Trim(),
            _currentFrameBytes, _currentFrameWidth, _currentFrameHeight,
            face,
            sourceVideoName: _currentVideoPath is null ? null : Path.GetFileName(_currentVideoPath));

        MessageBox.Show(this, $"'{person.Name}' enrolled from this frame.", "Enrolled",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static Bitmap BgrBytesToBitmap(byte[] bgrBytes, int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        var rect = new Rectangle(0, 0, width, height);
        var bitmapData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            int strideBytes = width * 3;
            for (int row = 0; row < height; row++)
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    bgrBytes, row * strideBytes,
                    bitmapData.Scan0 + row * bitmapData.Stride,
                    strideBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
        return bitmap;
    }
}
