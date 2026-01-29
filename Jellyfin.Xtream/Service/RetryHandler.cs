using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Handles HTTP request retry logic with exponential backoff for transient errors.
/// </summary>
public class RetryHandler
{
    private readonly FailureTrackingService _failureTracker;
    private readonly ILogger<RetryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryHandler"/> class.
    /// </summary>
    /// <param name="failureTracker">The failure tracking service.</param>
    /// <param name="logger">The logger instance.</param>
    public RetryHandler(FailureTrackingService failureTracker, ILogger<RetryHandler> logger)
    {
        _failureTracker = failureTracker;
        _logger = logger;
    }

    /// <summary>
    /// Executes an HTTP operation with retry logic and exponential backoff.
    /// </summary>
    /// <param name="uri">The URI being requested.</param>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result, or null if all retries failed.</returns>
    public async Task<string?> ExecuteWithRetryAsync(
        Uri uri,
        Func<Uri, CancellationToken, Task<string>> operation,
        CancellationToken cancellationToken)
    {
        string url = uri.ToString();

        // Check if this URL is known to fail persistently
        if (_failureTracker.IsKnownFailure(url))
        {
            _logger.LogDebug("Skipping retry for known persistent failure: {Url}", url);
            return null;
        }

        int maxAttempts = Plugin.Instance?.Configuration.HttpRetryMaxAttempts ?? 3;
        int initialDelayMs = Plugin.Instance?.Configuration.HttpRetryInitialDelayMs ?? 1000;

        // Validate configuration
        if (maxAttempts < 0)
        {
            maxAttempts = 0;
        }

        if (maxAttempts > 10)
        {
            maxAttempts = 10;
        }

        if (initialDelayMs < 100)
        {
            initialDelayMs = 100;
        }

        if (initialDelayMs > 10000)
        {
            initialDelayMs = 10000;
        }

        HttpRequestException? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Execute the operation
                string result = await operation(uri, cancellationToken).ConfigureAwait(false);
                return result;
            }
            catch (HttpRequestException ex) when (IsRetryableError(ex))
            {
                lastException = ex;

                if (attempt < maxAttempts)
                {
                    // Calculate exponential backoff delay: initialDelay * 2^(attempt-1)
                    int delayMs = initialDelayMs * (int)Math.Pow(2, attempt - 1);

                    _logger.LogWarning(
                        "HTTP {StatusCode} error on attempt {Attempt}/{MaxAttempts} for {Url}. Retrying in {DelayMs}ms. Error: {Message}",
                        ex.StatusCode,
                        attempt,
                        maxAttempts,
                        url,
                        delayMs,
                        ex.Message);

                    // Wait before retry
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // All retries exhausted
                    _logger.LogWarning(
                        "HTTP {StatusCode} error persisted after {MaxAttempts} attempts for {Url}. Recording as persistent failure. Error: {Message}",
                        ex.StatusCode,
                        maxAttempts,
                        url,
                        ex.Message);

                    // Record this URL as a persistent failure
                    _failureTracker.RecordFailure(url, $"HTTP {ex.StatusCode}: {ex.Message}");
                }
            }
            catch (HttpRequestException ex) when (!IsRetryableError(ex))
            {
                // Non-retryable error (4xx client errors) - fail immediately
                _logger.LogDebug(
                    "Non-retryable HTTP {StatusCode} error for {Url}. Not retrying. Error: {Message}",
                    ex.StatusCode,
                    url,
                    ex.Message);
                throw;
            }
            catch (OperationCanceledException)
            {
                // Cancellation requested - don't retry
                _logger.LogDebug("Operation cancelled for {Url}", url);
                throw;
            }
        }

        // All retries exhausted, return null for graceful degradation
        bool throwOnFailure = Plugin.Instance?.Configuration.HttpRetryThrowOnPersistentFailure ?? false;
        if (throwOnFailure && lastException != null)
        {
            throw lastException;
        }

        return null;
    }

    /// <summary>
    /// Determines if an HTTP error is retryable.
    /// </summary>
    /// <param name="ex">The HTTP request exception.</param>
    /// <returns>True if the error is retryable (5xx server errors), false otherwise.</returns>
    private static bool IsRetryableError(HttpRequestException ex)
    {
        if (ex.StatusCode == null)
        {
            // Network errors without status code are retryable
            return true;
        }

        // Retry on 5xx server errors
        return ex.StatusCode >= HttpStatusCode.InternalServerError &&
               ex.StatusCode < (HttpStatusCode)600;
    }
}
