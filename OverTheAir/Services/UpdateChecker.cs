using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace OverTheAir.Services;

public sealed class UpdateChecker
{
    // Replace YOUR_GITHUB_OWNER with the GitHub user or org that hosts this repo.
    public const string LatestReleaseUrl =
        "https://api.github.com/repos/YOUR_GITHUB_OWNER/over-the-air/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        try
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }
        catch (NotSupportedException)
        {
        }

        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OverTheAir-UpdateChecker");
        return client;
    }

    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            using HttpResponseMessage response = await Http.GetAsync(LatestReleaseUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Version current = Assembly.GetExecutingAssembly().GetName().Version
                ?? new Version(0, 0, 0, 0);

            if (UpdateReleaseParser.TryParseNewerRelease(json, current, out UpdateInfo update))
            {
                return update;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private const int DownloadBufferSize = 80 * 1024;

    public async Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress)
    {
        string extension = string.IsNullOrEmpty(update.Extension)
            ? UpdateReleaseParser.BundleExtension
            : update.Extension;

        string fileName = "OverTheAir-" + update.Version + extension;
        string path = Path.Combine(Path.GetTempPath(), fileName);

        using HttpResponseMessage response = await Http
            .GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;
        progress?.Report(total.HasValue ? 0d : -1d);

        using Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var destination = new FileStream(
            path, FileMode.Create, FileAccess.Write, FileShare.None,
            DownloadBufferSize, useAsync: true);

        var buffer = new byte[DownloadBufferSize];
        long written = 0;

        while (true)
        {
            int read = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer, 0, read).ConfigureAwait(false);
            written += read;

            if (total.HasValue && total.Value > 0)
            {
                progress?.Report((double)written / total.Value);
            }
        }

        return path;
    }

    public void LaunchInstaller(string installerPath)
    {
        string exePath = Environment.ProcessPath
            ?? Assembly.GetExecutingAssembly().Location;
        string logPath = Path.Combine(Path.GetTempPath(), "OverTheAir-update.log");

        bool isBundle = installerPath.EndsWith(
            UpdateReleaseParser.BundleExtension, StringComparison.OrdinalIgnoreCase);

        string install;
        if (isBundle)
        {
            install =
                "start /wait \"\" \"" + installerPath + "\" -passive -norestart -log \"" +
                logPath + "\"";
        }
        else
        {
            install =
                "\"msiexec.exe\" /i \"" + installerPath + "\" /quiet /norestart /l*v \"" +
                logPath + "\"";
        }

        string command = install + " & start \"\" \"" + exePath + "\"";

        Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + command + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }
}
