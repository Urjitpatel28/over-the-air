using System.Reflection;
using System.Windows;
using OverTheAir.Services;

namespace OverTheAir;

public partial class MainWindow : Window
{
    private readonly UpdateChecker _updateChecker = new();

    public MainWindow()
    {
        InitializeComponent();
        Version current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
        VersionText.Text = "Version " + current;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        UpdateInfo? update = await _updateChecker.CheckAsync();
        if (update == null)
        {
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            this,
            "A new version is available (" + update.Value.Version + ").\n\n" +
            "The app will close and reopen once it's installed.",
            "Update available",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        string installerPath;
        try
        {
            StatusText.Text = "Downloading OverTheAir " + update.Value.Version;
            DownloadProgress.Visibility = Visibility.Visible;
            DownloadProgress.IsIndeterminate = true;

            var progress = new Progress<double>(fraction =>
            {
                if (fraction < 0)
                {
                    DownloadProgress.IsIndeterminate = true;
                    return;
                }

                DownloadProgress.IsIndeterminate = false;
                DownloadProgress.Value = fraction;
            });

            installerPath = await _updateChecker.DownloadAsync(update.Value, progress);
        }
        catch (Exception)
        {
            DownloadProgress.Visibility = Visibility.Collapsed;
            StatusText.Text = string.Empty;
            MessageBox.Show(
                this,
                "Could not download the update. Try again later.",
                "OverTheAir",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        StatusText.Text = "Installing OverTheAir " + update.Value.Version;
        _updateChecker.LaunchInstaller(installerPath);
        Close();
    }
}
