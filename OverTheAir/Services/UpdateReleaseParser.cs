using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace OverTheAir.Services;

public struct UpdateInfo
{
    public Version Version;
    public string DownloadUrl;
    public string Extension;
}

public static class UpdateReleaseParser
{
    public const string BundleExtension = ".exe";
    public const string MsiExtension = ".msi";

    public static bool TryParseNewerRelease(string json, Version currentVersion, out UpdateInfo update)
    {
        update = default;

        if (string.IsNullOrWhiteSpace(json) || currentVersion == null)
        {
            return false;
        }

        GitHubRelease? release;
        try
        {
            release = Deserialize(json);
        }
        catch (Exception)
        {
            return false;
        }

        if (release == null || string.IsNullOrWhiteSpace(release.TagName))
        {
            return false;
        }

        if (!TryParseTag(release.TagName, out Version? releaseVersion) || releaseVersion == null)
        {
            return false;
        }

        if (releaseVersion.CompareTo(currentVersion) <= 0)
        {
            return false;
        }

        string[] urls = (release.Assets ?? Array.Empty<GitHubReleaseAsset>())
            .Where(a => a != null && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))
            .Select(a => a.BrowserDownloadUrl!)
            .ToArray();

        string? url = FirstEndingWith(urls, BundleExtension);
        string extension = BundleExtension;

        if (url == null)
        {
            url = FirstEndingWith(urls, MsiExtension);
            extension = MsiExtension;
        }

        if (url == null)
        {
            return false;
        }

        update = new UpdateInfo
        {
            Version = releaseVersion,
            DownloadUrl = url,
            Extension = extension,
        };
        return true;
    }

    private static string? FirstEndingWith(string[] urls, string extension)
    {
        return urls.FirstOrDefault(
            url => url.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly Regex TagPattern = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);

    public static bool TryParseTag(string tag, out Version? version)
    {
        version = null;

        if (tag == null)
        {
            return false;
        }

        string trimmed = tag.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(1);
        }

        if (!TagPattern.IsMatch(trimmed))
        {
            return false;
        }

        return Version.TryParse(trimmed, out version);
    }

    private static GitHubRelease? Deserialize(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return (GitHubRelease?)new DataContractJsonSerializer(typeof(GitHubRelease)).ReadObject(stream);
    }

    [DataContract]
    internal sealed class GitHubRelease
    {
        [DataMember(Name = "tag_name")]
        public string? TagName { get; set; }

        [DataMember(Name = "assets")]
        public GitHubReleaseAsset[]? Assets { get; set; }
    }

    [DataContract]
    internal sealed class GitHubReleaseAsset
    {
        [DataMember(Name = "browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
