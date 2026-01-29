using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Tracks HTTP request failures to avoid repeated retry attempts on persistently failing URLs.
/// </summary>
public class FailureTrackingService
{
    private const string CacheKeyPrefix = "http_failure_";
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<FailureTrackingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FailureTrackingService"/> class.
    /// </summary>
    /// <param name="memoryCache">The memory cache for storing failure records.</param>
    /// <param name="logger">The logger instance.</param>
    public FailureTrackingService(IMemoryCache memoryCache, ILogger<FailureTrackingService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a URL is recorded as a known failure.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <returns>True if the URL is in the failure cache, false otherwise.</returns>
    public bool IsKnownFailure(string url)
    {
        string cacheKey = GetCacheKey(url);
        bool isKnown = _memoryCache.TryGetValue(cacheKey, out _);

        if (isKnown)
        {
            _logger.LogDebug("URL is a known failure (cached): {Url}", url);
        }

        return isKnown;
    }

    /// <summary>
    /// Records a URL as a persistent failure with expiration.
    /// </summary>
    /// <param name="url">The URL that failed.</param>
    /// <param name="errorDetails">Details about the error.</param>
    public void RecordFailure(string url, string errorDetails)
    {
        string cacheKey = GetCacheKey(url);
        int expirationHours = Plugin.Instance?.Configuration.HttpFailureCacheExpirationHours ?? 24;

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(expirationHours)
        };

        var failureRecord = new FailureRecord
        {
            Url = url,
            ErrorDetails = errorDetails,
            FirstFailureTime = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(expirationHours)
        };

        _memoryCache.Set(cacheKey, failureRecord, cacheOptions);

        _logger.LogWarning(
            "Recorded persistent failure for URL: {Url}. Will skip retries for {Hours} hours. Error: {Error}",
            url,
            expirationHours,
            errorDetails);
    }

    /// <summary>
    /// Clears all failure records from the cache.
    /// </summary>
    public void ClearFailures()
    {
        _logger.LogInformation("Clearing all HTTP failure records (cache invalidation not supported, entries will expire naturally)");

        // Note: IMemoryCache doesn't provide a way to enumerate or clear specific entries.
        // Entries will expire based on their TTL. This method is here for future extensibility
        // if we switch to a cache implementation that supports selective clearing.
    }

    /// <summary>
    /// Gets failure statistics for logging and monitoring.
    /// </summary>
    /// <returns>A tuple containing the count of failures and a list of failed URLs.</returns>
    public (int Count, List<string> Items) GetFailureStats()
    {
        // Note: IMemoryCache doesn't provide enumeration capabilities.
        // We return empty stats here. For production use, consider using a cache
        // implementation that supports enumeration, or maintain a separate tracking structure.
        _logger.LogDebug("Failure stats requested (enumeration not supported by IMemoryCache)");

        return (0, new List<string>());
    }

    /// <summary>
    /// Generates a cache key for a given URL using MD5 hash.
    /// </summary>
    /// <param name="url">The URL to generate a key for.</param>
    /// <returns>A cache key string.</returns>
#pragma warning disable CA5351 // MD5 is used for non-cryptographic cache key generation only
    private static string GetCacheKey(string url)
    {
        byte[] hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(url));
        string hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return CacheKeyPrefix + hash;
    }
#pragma warning restore CA5351

    /// <summary>
    /// Represents a failure record stored in the cache.
    /// </summary>
    private sealed class FailureRecord
    {
        /// <summary>
        /// Gets or sets the URL that failed.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets error details.
        /// </summary>
        public string ErrorDetails { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the time of first failure.
        /// </summary>
        public DateTime FirstFailureTime { get; set; }

        /// <summary>
        /// Gets or sets the expiration time.
        /// </summary>
        public DateTime ExpiresAt { get; set; }
    }
}
