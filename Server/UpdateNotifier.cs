using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace SenzeBodycam.Server;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 1)]
public sealed class UpdateNotifier(ISptLogger<UpdateNotifier> logger) : IOnLoad
{
    private const string CurrentVersionText = "1.0.0";
    private const string LatestReleaseUrl = "https://api.github.com/repos/notSENZE/Bodycam/releases/latest";
    private const string RepositoryUrl = "https://github.com/notSENZE/Bodycam";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));

        try
        {
            using var response = await HttpClient.GetAsync(LatestReleaseUrl, timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.Debug($"Bodycam update check returned HTTP {(int)response.StatusCode}.");
                return;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: timeout.Token);
            if (!document.RootElement.TryGetProperty("tag_name", out var tagElement))
            {
                logger.Debug("Bodycam update check did not receive a release tag.");
                return;
            }

            var tag = tagElement.GetString()?.Trim().TrimStart('v', 'V');
            if (!SemanticVersioning.Version.TryParse(tag, out var latestVersion))
            {
                logger.Debug($"Bodycam update check received an invalid release tag: {tag}");
                return;
            }

            var currentVersion = new SemanticVersioning.Version(CurrentVersionText);
            if (latestVersion > currentVersion)
            {
                logger.Warning(
                    $"A new Senze Bodycam version is available: {latestVersion} "
                    + $"(installed: {currentVersion}). Download it from {RepositoryUrl}/releases/latest");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.Debug("Bodycam update check timed out.");
        }
        catch (HttpRequestException exception)
        {
            logger.Debug($"Bodycam update check failed: {exception.Message}");
        }
        catch (JsonException exception)
        {
            logger.Debug($"Bodycam update response could not be read: {exception.Message}");
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SenzeBodycam", CurrentVersionText));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
