using MultiFaceRec.App.Services;

namespace MultiFaceRec.UI.Forms;

/// <summary>
///  AUTHOR : UVAISE K B
/// </summary>
public sealed class RegisterForm : Form
{
    private readonly AuthService _authService;

    private readonly TextBox _usernameBox = new() { Left = 160, Top = 40, Width = 200 };
    private readonly TextBox _passwordBox = new() { Left = 160, Top = 80, Width = 200, PasswordChar = '*' };
    private readonly TextBox _confirmBox = new() { Left = 160, Top = 120, Width = 200, PasswordChar = '*' };
    private readonly Button _createButton = new() { Text = "Create account", Left = 160, Top = 160, Width = 130 };
    private readonly Label _statusLabel = new() { Left = 160, Top = 200, Width = 300, ForeColor = Color.Firebrick,BackColor= Color.Transparent};

    public RegisterForm(AuthService authService)
    {
        _authService = authService;

        Text = "FaceTrace — Create account";
        Width = 480;
        Height = 300;

        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        BackgroundImage = Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "background.jpg"));
        BackgroundImageLayout = ImageLayout.Stretch;


        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(new Label { Text = "Username", Left = 60, Top = 43, Width = 90 , BackColor=Color.Transparent});
        Controls.Add(new Label { Text = "Password", Left = 60, Top = 83, Width = 90, BackColor = Color.Transparent });
        Controls.Add(new Label { Text = "Confirm", Left = 60, Top = 123, Width = 90, BackColor = Color.Transparent });
        Controls.Add(_usernameBox);
        Controls.Add(_passwordBox);
        Controls.Add(_confirmBox);
        Controls.Add(_createButton);
        Controls.Add(_statusLabel);

        
        _createButton.Click += OnCreateClicked;
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        var (succeeded, error) = await _authService.RegisterAsync(_usernameBox.Text, _passwordBox.Text, _confirmBox.Text);
        if (!succeeded)
        {
            _statusLabel.Text = error;
            return;
        }

        MessageBox.Show(this, "Account created — you can log in now.", "Success",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }
}
