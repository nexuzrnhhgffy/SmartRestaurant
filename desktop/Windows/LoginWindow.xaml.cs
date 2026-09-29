using System.Windows;
using System.Windows.Input;
using SmartRestaurant.Desktop.Core;

namespace SmartRestaurant.Desktop.Windows;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        ServerBox.Text = App.Settings.ServerUrl;
        UserBox.Text = App.Settings.LastUserName ?? "";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void PassBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Login_Click(sender, e); }

    private void Demo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button b && b.Tag is string tag)
        {
            var parts = tag.Split('|');
            UserBox.Text = parts[0];
            PassBox.Password = parts[1];
            Login_Click(sender, e);
        }
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        LoginBtn.IsEnabled = false;
        LoginBtn.Content = "Signing in…";
        try
        {
            App.Api.SetServer(ServerBox.Text.Trim());
            var auth = await App.Api.LoginAsync(UserBox.Text.Trim(), PassBox.Password);
            Session.AccessToken = auth.AccessToken;
            Session.RefreshToken = auth.RefreshToken;
            Session.User = auth.User;
            App.Settings.LastUserName = UserBox.Text.Trim();
            App.Settings.Save();

            await App.Realtime.ConnectAsync(auth.AccessToken, auth.User!.BranchId, auth.User.Role);

            var main = new MainWindow();
            main.Show();
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = "⚠ " + ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            LoginBtn.IsEnabled = true;
            LoginBtn.Content = "Sign in  →";
        }
    }
}
