using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClawJump.Avalonia.Services;

public sealed record UpdateCheckResult(
    string CurrentVersion,
    string? LatestVersion,
    bool HasUpdate,
    string? ReleaseUrl,
    string? ReleaseName,
    DateTimeOffset? PublishedAt,
    string? ErrorMessage);

public static class UpdateCheckService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/ShengSir1/ClawJump/releases/latest";

    public static string CurrentVersion => GetCurrentVersion();

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = CurrentVersion;

        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ClawJump", currentVersion));
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            var json = await httpClient.GetStringAsync(LatestReleaseUrl, cancellationToken);

            if (JsonNode.Parse(json) is not JsonObject releaseObj)
            {
                return CreateError(currentVersion, "GitHub Releases 返回格式无效。");
            }

            var tagName = releaseObj["tag_name"]?.GetValue<string>();
            var releaseUrl = releaseObj["html_url"]?.GetValue<string>();
            var releaseName = releaseObj["name"]?.GetValue<string>();
            var publishedAtText = releaseObj["published_at"]?.GetValue<string>();
            var latestVersion = NormalizeVersionText(tagName);

            if (string.IsNullOrWhiteSpace(latestVersion))
            {
                return CreateError(currentVersion, "GitHub Releases 未返回有效版本号。");
            }

            var hasUpdate = IsNewerVersion(latestVersion, currentVersion);
            var publishedAt = DateTimeOffset.TryParse(publishedAtText, out var parsedPublishedAt)
                ? parsedPublishedAt
                : (DateTimeOffset?)null;

            return new UpdateCheckResult(
                currentVersion,
                latestVersion,
                hasUpdate,
                releaseUrl,
                releaseName,
                publishedAt,
                null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return CreateError(currentVersion, ex.Message);
        }
    }

    private static UpdateCheckResult CreateError(string currentVersion, string errorMessage)
    {
        return new UpdateCheckResult(
            currentVersion,
            null,
            false,
            null,
            null,
            null,
            errorMessage);
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return NormalizeVersionText(informationalVersion) ?? informationalVersion;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string? NormalizeVersionText(string? versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return null;
        }

        var normalized = versionText.Trim();

        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOfAny(['+', '-']);

        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        return normalized.Trim();
    }

    private static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        if (Version.TryParse(latestVersion, out var latest) &&
            Version.TryParse(currentVersion, out var current))
        {
            return latest > current;
        }

        return string.Compare(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
    }
}
