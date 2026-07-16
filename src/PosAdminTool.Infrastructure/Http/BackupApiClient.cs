using System.Net.Http.Json;
using PosAdminTool.Domain.Interfaces;

namespace PosAdminTool.Infrastructure.Http;

public sealed class BackupApiClient(HttpClient httpClient) : IBackupApiClient
{
    public async Task TriggerBackupAsync(string apiUrl, IReadOnlyList<string> branchCodes, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
        {
            Content = JsonContent.Create(branchCodes)
        };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
