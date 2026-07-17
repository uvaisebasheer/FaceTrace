using Microsoft.Extensions.DependencyInjection;
using MultiFaceRec.App.Services;
using System.IO;
namespace MultiFaceRec.UI.Forms;


/// <summary>
///  AUTHOR : UVAISE K B
/// </summary>
public sealed class LoginForm : Form
{
    private readonly AuthService _authService;
    private readonly IServiceProvider _serviceProvider;

    private readonly TextBox _usernameBox = new() { Left = 160, Top = 60, Width = 200 };
    private readonly TextBox _passwordBox = new() { Left = 160, Top = 100, Width = 200, PasswordChar = '*' };
    private readonly Button _loginButton = new() { Text = "Login", Left = 160, Top = 140, Width = 90 };
    private readonly Button _registerButton = new() { Text = "Create account", Left = 260, Top = 140, Width = 100 };
    private readonly Label _statusLabel = new() { Left = 160, Top = 180, Width = 300, ForeColor = Color.Firebrick };

    public LoginForm(AuthService authService, IServiceProvider serviceProvider)
    {
        _authService = authService;
        _serviceProvider = serviceProvider;


        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Text = "FaceTrace — Login";
        Width = 480;
        Height = 280;

        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        BackgroundImage = Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "background.jpg"));
        BackgroundImageLayout = ImageLayout.Stretch;


        _statusLabel.BackColor = Color.Transparent;
       


        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += (_, _) => { if (Application.OpenForms.Count <= 1) Application.Exit(); };

        Controls.Add(new Label { Text = "Username", Left = 60, Top = 63, Width = 90,BackColor = Color.Transparent });
        Controls.Add(new Label { Text = "Password", Left = 60, Top = 103, Width = 90 , BackColor =Color.Transparent });
        Controls.Add(_usernameBox);
        Controls.Add(_passwordBox);
        Controls.Add(_loginButton);
        Controls.Add(_registerButton);
        Controls.Add(_statusLabel);

        _loginButton.Click += OnLoginClicked;
        _registerButton.Click += OnRegisterClicked;

        Load += async (_, _) =>
        {
            // First-run convenience: if there are no accounts yet, send the
            // user straight to Register instead of a dead-end login screen.
            if (!await _authService.HasAnyAccountAsync())
                OnRegisterClicked(this, EventArgs.Empty);
        };
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        _statusLabel.Text = "";
        var result = await _authService.LoginAsync(_usernameBox.Text, _passwordBox.Text);
        if (!result.Succeeded)
        {
            _statusLabel.Text = result.ErrorMessage;
            return;
        }

        var mainForm = _serviceProvider.GetRequiredService<MainForm>();
        mainForm.Show();
        Hide();
    }

    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoginForm));
        SuspendLayout();
        // 
        // LoginForm
        // 
        BackColor = SystemColors.ActiveBorder;
        ClientSize = new Size(284, 261);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        Icon = (Icon)resources.GetObject("$this.Icon");
        Name = "LoginForm";
        Load += LoginForm_Load;
        ResumeLayout(false);
    }

    private void OnRegisterClicked(object? sender, EventArgs e)
    {
        var registerForm = _serviceProvider.GetRequiredService<RegisterForm>();
        registerForm.ShowDialog(this);
    }

    private void LoginForm_Load(object sender, EventArgs e)
    {

    }
}
