using System.Runtime.CompilerServices;
using System.Text;
using Freeway.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Freeway.Infrastructure.Streaming;

/// <summary>
/// The transport half of reading a streamed reply: sending the request without
/// buffering the body, and splitting what comes back into events.
///
/// Timeouts are idle timeouts. A non-streaming call gets COMPLETION_TIMEOUT_SECONDS
/// for the whole reply; a stream gets it between events instead, because a long
/// answer that keeps arriving is working, and cutting it off at a fixed total would
/// fail exactly the requests streaming exists for.
/// </summary>
internal static class UpstreamStream
{
    /// <summary>
    /// Sends <paramref name="request"/> and returns the response once its headers are in.
    /// A refusal throws with the upstream's status, worded the way the non-streaming
    /// providers word theirs, so the logs read the same either way.
    /// </summary>
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient http,
        HttpRequestMessage request,
        string label,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var status = response.StatusCode;
        var body = "";
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            // The status is what matters; the body is only for the log.
        }
        finally
        {
            response.Dispose();
        }

        logger.LogError("{Label} API error: {StatusCode} - {Content}", label, status, body);
        throw new ChatCompletionStreamException($"{label} API error: {status}", (int)status);
    }

    /// <summary>
    /// The data payload of each server-sent event in the body. Comments (": ...", which
    /// some upstreams send as keep-alives) and event names are skipped, but every line
    /// still counts as activity for the idle timeout.
    /// </summary>
    public static async IAsyncEnumerable<string> ReadSseDataAsync(
        HttpResponseMessage response,
        CancellationTokenSource idle,
        TimeSpan idleTimeout,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var line in ReadLinesAsync(response, idle, idleTimeout, cancellationToken))
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            // Every upstream here sends one JSON object per data line, so each line is
            // an event in its own right. Waiting for the blank separator as well would
            // add nothing but a dependency on it being sent.
            var data = line.AsSpan(5);
            if (data.Length > 0 && data[0] == ' ')
            {
                data = data[1..];
            }

            if (data.Length > 0)
            {
                yield return data.ToString();
            }
        }
    }

    /// <summary>The body line by line, for newline-delimited JSON.</summary>
    public static async IAsyncEnumerable<string> ReadLinesAsync(
        HttpResponseMessage response,
        CancellationTokenSource idle,
        TimeSpan idleTimeout,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(body, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            idle.CancelAfter(idleTimeout);

            if (line.Length > 0)
            {
                yield return line;
            }
        }
    }

    /// <summary>A fresh result for a stream to fill in, seeded with what is known up front.</summary>
    public static ChatCompletionResult NewResult(string providerName, string model) => new()
    {
        Id = $"chatcmpl-{Guid.NewGuid():N}",
        Model = model,
        Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ProviderName = providerName
    };
}
