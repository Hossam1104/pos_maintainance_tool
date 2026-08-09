using System.Net.Http.Headers;
using System.Net.Http.Json;
using PosAdminTool.Domain.Exceptions;
using PosAdminTool.Domain.Interfaces;
using PosAdminTool.Domain.Models;

namespace PosAdminTool.Infrastructure.Http;

/// <summary>
/// Triggers only the server-configured RMS endpoint. Automatic redirects are intentionally not
/// trusted; every redirect is inspected by the same endpoint policy before another request is sent.
/// </summary>
public sealed class BackupApiClient : IBackupApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IHostAddressResolver _addressResolver;

    public BackupApiClient(HttpClient httpClient, IHostAddressResolver? addressResolver = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _addressResolver = addressResolver ?? new SystemHostAddressResolver();
    }

    public async Task TriggerBackupAsync(
        string apiUrl,
        IReadOnlyList<string> branchCodes,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> normalizedBranches;
        try
        {
            normalizedBranches = DownloaderInputPolicy.NormalizeBranchCodes(branchCodes);
        }
        catch (ArgumentException)
        {
            throw new BackupApiPolicyException(DownloaderFailureCodes.InvalidBranch);
        }

        BackupApiEndpointPolicy policy;
        Uri current;
        try
        {
            policy = BackupApiEndpointPolicy.FromConfiguredEndpoint(apiUrl);
            current = policy.ValidateInitial(apiUrl);
            await policy.ValidateResolvedAddressesAsync(current, _addressResolver, cancellationToken).ConfigureAwait(false);
        }
        catch (BackupApiPolicyException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            throw new BackupApiPolicyException(DownloaderFailureCodes.EndpointRejected);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(policy.RequestTimeout);

        for (var redirect = 0; ; redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, current)
            {
                Content = JsonContent.Create(normalizedBranches)
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (TryFind(exception, out BackupApiPolicyException? policyException))
                {
                    throw new BackupApiPolicyException(policyException!.Code);
                }

                if (TryFind(exception, out BackupApiRequestException? requestException))
                {
                    throw new BackupApiRequestException(requestException!.Code);
                }

                throw new BackupApiRequestException(DownloaderFailureCodes.TriggerFailed);
            }

            using (response)
            {
                // This also detects a client configured with an unsafe automatic redirect handler
                // after the fact; production registration disables automatic redirects entirely.
                if (response.RequestMessage?.RequestUri is { } actualUri
                    && !string.Equals(actualUri.AbsoluteUri, current.AbsoluteUri, StringComparison.Ordinal))
                {
                    throw new BackupApiPolicyException("downloader.redirect_rejected");
                }

                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    if (redirect >= policy.MaxRedirects || response.Headers.Location is not { } location)
                    {
                        throw new BackupApiPolicyException("downloader.redirect_rejected");
                    }

                    current = policy.ValidateRedirect(current, location);
                    await policy.ValidateResolvedAddressesAsync(current, _addressResolver, timeout.Token).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode
                    || response.Content.Headers.ContentLength is > 64 * 1024)
                {
                    throw new BackupApiRequestException(DownloaderFailureCodes.TriggerFailed);
                }

                return;
            }
        }
    }

    private static bool TryFind<T>(Exception exception, out T? match)
        where T : Exception
    {
        if (exception is T typed)
        {
            match = typed;
            return true;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                if (TryFind(inner, out match))
                {
                    return true;
                }
            }
        }
        else if (exception.InnerException is not null && TryFind(exception.InnerException, out match))
        {
            return true;
        }

        match = null;
        return false;
    }
}

public sealed class BackupApiRequestException(string code) : DownloaderTriggerException(code)
{
}
