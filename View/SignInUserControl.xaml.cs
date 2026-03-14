using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ADLMRateGen.ViewModel;
using FontAwesome.Sharp;
using Microsoft.Win32;

namespace ADLMRateGen.View
{
    public partial class SignInUserControl : UserControl
    {
        private const string ForgotPasswordUrl = "https://adlmstudio.net/login";
        private const string SignUpUrl = "https://adlmstudio.net/signup";
        private const string FallbackBackdropUri = "pack://application:,,,/Resources/Backdrop.png";

        private bool _pwdVisible;
        private bool _syncingPassword;

        public SignInUserControl()
        {
            InitializeComponent();
            ApplyDesktopBackdrop();
            UpdatePasswordVisibility();
        }

        private void TogglePwd_Click(object sender, RoutedEventArgs e)
        {
            _pwdVisible = !_pwdVisible;
            UpdatePasswordVisibility();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_syncingPassword || sender is not PasswordBox pb)
            {
                return;
            }

            _syncingPassword = true;
            PasswordPreviewTextBox.Text = pb.Password;
            _syncingPassword = false;

            if (DataContext is SignInViewModel vm)
            {
                vm.Password = pb.Password;
            }
        }

        private void PasswordPreviewTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingPassword || sender is not TextBox tb)
            {
                return;
            }

            _syncingPassword = true;
            PasswordBox.Password = tb.Text;
            _syncingPassword = false;

            if (DataContext is SignInViewModel vm)
            {
                vm.Password = tb.Text;
            }
        }

        private void OpenForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            OpenExternalUrl(ForgotPasswordUrl);
        }

        private void OpenSignUp_Click(object sender, RoutedEventArgs e)
        {
            OpenExternalUrl(SignUpUrl);
        }

        private void UpdatePasswordVisibility()
        {
            var password = PasswordBox?.Password ?? PasswordPreviewTextBox?.Text ?? string.Empty;

            _syncingPassword = true;

            if (PasswordPreviewTextBox != null)
            {
                PasswordPreviewTextBox.Text = password;
                PasswordPreviewTextBox.Visibility = _pwdVisible ? Visibility.Visible : Visibility.Collapsed;
                PasswordPreviewTextBox.CaretIndex = PasswordPreviewTextBox.Text.Length;
            }

            if (PasswordBox != null)
            {
                PasswordBox.Password = password;
                PasswordBox.Visibility = _pwdVisible ? Visibility.Collapsed : Visibility.Visible;
            }

            if (PasswordToggleIcon != null)
            {
                PasswordToggleIcon.Icon = _pwdVisible ? IconChar.EyeSlash : IconChar.Eye;
            }

            _syncingPassword = false;
        }

        private void ApplyDesktopBackdrop()
        {
            WallpaperBrush.ImageSource =
                TryLoadImage(GetWallpaperPath()) ??
                TryLoadImage(FallbackBackdropUri);
        }

        private static string? GetWallpaperPath()
        {
            var registryPath = Registry.CurrentUser
                .OpenSubKey(@"Control Panel\Desktop")
                ?.GetValue("WallPaper") as string;

            if (!string.IsNullOrWhiteSpace(registryPath) && File.Exists(registryPath))
            {
                return registryPath;
            }

            var transcodedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes\TranscodedWallpaper");

            return File.Exists(transcodedPath) ? transcodedPath : null;
        }

        private static ImageSource? TryLoadImage(string? source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(source, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private static void OpenExternalUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open link.\n{ex.Message}", "ADLM Rate Gen");
            }
        }
    }
}
