// Copyright (C) 2022  Kevin Jilissen

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client.Models;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

// Type aliases to disambiguate from MediaBrowser.Controller.Entities.TV types
using JellyfinEpisode = MediaBrowser.Controller.Entities.TV.Episode;
using JellyfinEpisodeInfo = MediaBrowser.Controller.Providers.EpisodeInfo;
using JellyfinSeries = MediaBrowser.Controller.Entities.TV.Series;
using JellyfinSeriesInfo = MediaBrowser.Controller.Providers.SeriesInfo;
using XtreamEpisode = Jellyfin.Xtream.Client.Models.Episode;
using XtreamSeason = Jellyfin.Xtream.Client.Models.Season;
using XtreamSeries = Jellyfin.Xtream.Client.Models.Series;
using XtreamSeriesInfo = Jellyfin.Xtream.Client.Models.SeriesInfo;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Service for pre-fetching and caching all series data upfront.
/// </summary>
public class SeriesCacheService : IDisposable
{
    private readonly StreamService _streamService;
    private readonly IMemoryCache _memoryCache;
    private readonly FailureTrackingService _failureTrackingService;
    private readonly ILogger<SeriesCacheService>? _logger;
    private readonly IProviderManager? _providerManager;
    private readonly IServerConfigurationManager? _serverConfigManager;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private int _cacheVersion = 0;
    private bool _isRefreshing = false;
    private double _currentProgress = 0.0;
    private string _currentStatus = "Idle";
    private DateTime? _lastRefreshStart;
    private DateTime? _lastRefreshComplete;
    private CancellationTokenSource? _refreshCancellationTokenSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeriesCacheService"/> class.
    /// </summary>
    /// <param name="streamService">The stream service instance.</param>
    /// <param name="memoryCache">The memory cache instance.</param>
    /// <param name="failureTrackingService">The failure tracking service instance.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="providerManager">Optional provider manager for TMDB lookups.</param>
    /// <param name="serverConfigManager">Optional server configuration manager for metadata language.</param>
    public SeriesCacheService(
        StreamService streamService,
        IMemoryCache memoryCache,
        FailureTrackingService failureTrackingService,
        ILogger<SeriesCacheService>? logger = null,
        IProviderManager? providerManager = null,
        IServerConfigurationManager? serverConfigManager = null)
    {
        _streamService = streamService;
        _memoryCache = memoryCache;
        _failureTrackingService = failureTrackingService;
        _logger = logger;
        _providerManager = providerManager;
        _serverConfigManager = serverConfigManager;
    }

    /// <summary>
    /// Gets the current cache key prefix.
    /// Uses CacheDataVersion which only changes when cache-relevant settings change
    /// (not when refresh frequency changes).
    /// </summary>
    private string CachePrefix => $"series_cache_{Plugin.Instance.CacheDataVersion}_v{_cacheVersion}_";

    /// <summary>
    /// Pre-fetches and caches all series data (categories, series, seasons, episodes).
    /// </summary>
    /// <param name="progress">Optional progress reporter (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task RefreshCacheAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        // Prevent concurrent refreshes
        if (!await _refreshLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            _logger?.LogInformation("Cache refresh already in progress, skipping");
            return;
        }

        try
        {
            if (_isRefreshing)
            {
                _logger?.LogInformation("Cache refresh already in progress, skipping");
                return;
            }

            _isRefreshing = true;
            _currentProgress = 0.0;
            _currentStatus = "Starting...";
            _lastRefreshStart = DateTime.UtcNow;

            // Create a linked cancellation token source so we can cancel the refresh
            // Use atomic swap to avoid race with CancelRefresh()
            var oldCts = _refreshCancellationTokenSource;
            _refreshCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            oldCts?.Dispose();

            _logger?.LogInformation("Starting series data cache refresh");

            string cacheDataVersion = Plugin.Instance.CacheDataVersion;
            string cachePrefix = $"series_cache_{cacheDataVersion}_v{_cacheVersion}_";

            // Clear old cache entries
            ClearCache(cacheDataVersion);

            try
            {
                // Cache entries have a 24-hour safety expiration to prevent memory leaks
                // from orphaned entries (e.g., when cache version changes).
                // Normal refresh frequency is controlled by the scheduled task (default: every 60 minutes)
                MemoryCacheEntryOptions cacheOptions = new()
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                };

                // Fetch all categories
                _currentStatus = "Fetching categories...";
                progress?.Report(0.05);
                _logger?.LogInformation("Fetching series categories...");
                IEnumerable<Category> categories = await _streamService.GetSeriesCategories(_refreshCancellationTokenSource.Token).ConfigureAwait(false);
                List<Category> categoryList = categories.ToList();
                _memoryCache.Set($"{cachePrefix}categories", categoryList, cacheOptions);
                _logger?.LogInformation("Found {CategoryCount} categories", categoryList.Count);

                // Log configuration state for debugging
                var seriesConfig = Plugin.Instance.Configuration.Series;
                _logger?.LogInformation("Configuration has {ConfigCategoryCount} configured series categories", seriesConfig.Count);
                foreach (var kvp in seriesConfig)
                {
                    if (kvp.Value.Count == 0)
                    {
                        _logger?.LogInformation("  Category {CategoryId}: ALL series allowed (empty config)", kvp.Key);
                    }
                    else
                    {
                        _logger?.LogInformation("  Category {CategoryId}: {SeriesCount} specific series configured", kvp.Key, kvp.Value.Count);
                    }
                }

                int seriesCount = 0;
                int seasonCount = 0;
                int episodeCount = 0;
                int totalSeries = 0;

                // Single pass: fetch all series lists and cache them for reuse
                // This eliminates the double API call that was happening before
                Dictionary<int, List<XtreamSeries>> seriesListsByCategory = new();
                _currentStatus = "Fetching series lists...";
                foreach (Category category in categoryList)
                {
                    _refreshCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    IEnumerable<XtreamSeries> seriesList = await _streamService.GetSeries(category.CategoryId, _refreshCancellationTokenSource.Token).ConfigureAwait(false);
                    List<XtreamSeries> seriesItems = seriesList.ToList();
                    seriesListsByCategory[category.CategoryId] = seriesItems;
                    totalSeries += seriesItems.Count;
                }

                _logger?.LogInformation("Fetched {TotalSeries} series across {CategoryCount} categories", totalSeries, categoryList.Count);

                // Get parallelism configuration
                int parallelism = Math.Max(1, Math.Min(10, Plugin.Instance?.Configuration.CacheRefreshParallelism ?? 3));
                int minDelayMs = Math.Max(0, Math.Min(1000, Plugin.Instance?.Configuration.CacheRefreshMinDelayMs ?? 100));
                _logger?.LogInformation("Starting parallel series processing with parallelism={Parallelism}, minDelayMs={MinDelayMs}", parallelism, minDelayMs);

                // Throttle semaphore for rate limiting API requests
                using SemaphoreSlim throttleSemaphore = new(1, 1);
                DateTime lastRequestTime = DateTime.MinValue;

                // Helper to throttle requests
                async Task ThrottleRequestAsync()
                {
                    if (minDelayMs <= 0)
                    {
                        return;
                    }

                    await throttleSemaphore.WaitAsync(_refreshCancellationTokenSource.Token).ConfigureAwait(false);
                    try
                    {
                        double elapsedMs = (DateTime.UtcNow - lastRequestTime).TotalMilliseconds;
                        if (elapsedMs < minDelayMs)
                        {
                            await Task.Delay(minDelayMs - (int)elapsedMs, _refreshCancellationTokenSource.Token).ConfigureAwait(false);
                        }

                        lastRequestTime = DateTime.UtcNow;
                    }
                    finally
                    {
                        throttleSemaphore.Release();
                    }
                }

                // Flatten all series into a single list with category info for parallel processing
                List<(XtreamSeries Series, Category Category)> allSeries = new();
                foreach (Category category in categoryList)
                {
                    List<XtreamSeries> seriesListItems = seriesListsByCategory[category.CategoryId];
                    _memoryCache.Set($"{cachePrefix}serieslist_{category.CategoryId}", seriesListItems, cacheOptions);
                    foreach (XtreamSeries series in seriesListItems)
                    {
                        allSeries.Add((series, category));
                    }
                }

                // Thread-safe counters for progress tracking
                int processedSeries = 0;

                // Parallel processing options
                ParallelOptions parallelOptions = new()
                {
                    MaxDegreeOfParallelism = parallelism,
                    CancellationToken = _refreshCancellationTokenSource.Token
                };

                await Parallel.ForEachAsync(allSeries, parallelOptions, async (item, ct) =>
                {
                    XtreamSeries series = item.Series;

                    try
                    {
                        // Throttle to prevent rate limiting
                        await ThrottleRequestAsync().ConfigureAwait(false);

                        // Fetch seasons for this series (makes ONE API call to get SeriesStreamInfo)
                        IEnumerable<Tuple<SeriesStreamInfo, int>> seasons = await _streamService.GetSeasons(series.SeriesId, ct).ConfigureAwait(false);
                        List<Tuple<SeriesStreamInfo, int>> seasonList = seasons.ToList();

                        // Reuse the SeriesStreamInfo from GetSeasons for all episodes
                        // This eliminates redundant API calls (was calling GetSeriesStreamsBySeriesAsync once per season)
                        SeriesStreamInfo? seriesStreamInfo = seasonList.FirstOrDefault()?.Item1;

                        int localSeasonCount = 0;
                        int localEpisodeCount = 0;

                        foreach (var seasonTuple in seasonList)
                        {
                            int seasonId = seasonTuple.Item2;
                            localSeasonCount++;

                            // Get episodes from the already-fetched SeriesStreamInfo (no API call)
                            IEnumerable<Tuple<SeriesStreamInfo, XtreamSeason?, XtreamEpisode>> episodes = _streamService.GetEpisodesFromSeriesInfo(seriesStreamInfo!, series.SeriesId, seasonId);

                            List<XtreamEpisode> episodeList = episodes.Select(e => e.Item3).ToList();
                            localEpisodeCount += episodeList.Count;

                            // Cache episodes for this season
                            _memoryCache.Set($"{cachePrefix}episodes_{series.SeriesId}_{seasonId}", episodeList, cacheOptions);

                            // Cache season info
                            XtreamSeason? season = seriesStreamInfo?.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);
                            _memoryCache.Set($"{cachePrefix}season_{series.SeriesId}_{seasonId}", season, cacheOptions);
                        }

                        // Cache series stream info
                        if (seriesStreamInfo != null)
                        {
                            _memoryCache.Set($"{cachePrefix}seriesinfo_{series.SeriesId}", seriesStreamInfo, cacheOptions);
                        }

                        // Update counters atomically
                        int currentProcessed = Interlocked.Increment(ref processedSeries);
                        Interlocked.Add(ref seriesCount, 1);
                        Interlocked.Add(ref seasonCount, localSeasonCount);
                        Interlocked.Add(ref episodeCount, localEpisodeCount);

                        // Update progress (thread-safe since only read by UI)
                        if (totalSeries > 0)
                        {
                            double progressValue = 0.1 + (currentProcessed * 0.8 / totalSeries);
                            _currentProgress = progressValue;
                            _currentStatus = $"Processing series {currentProcessed}/{totalSeries} ({seriesCount} series, {seasonCount} seasons, {episodeCount} episodes)";
                            progress?.Report(progressValue);
                        }

                        // Log progress every 50 series
                        if (currentProcessed % 50 == 0)
                        {
                            _logger?.LogInformation(
                                "Progress: {Processed}/{Total} series ({Seasons} seasons, {Episodes} episodes)",
                                currentProcessed,
                                totalSeries,
                                seasonCount,
                                episodeCount);
                        }
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode >= HttpStatusCode.InternalServerError)
                    {
                        // HTTP 5xx errors - already retried by RetryHandler if enabled
                        _logger?.LogWarning(
                            "Persistent HTTP {StatusCode} error for series {SeriesId} ({SeriesName}) after {MaxRetries} retries: {Message}",
                            ex.StatusCode,
                            series.SeriesId,
                            series.Name,
                            Plugin.Instance?.Configuration.HttpRetryMaxAttempts ?? 3,
                            ex.Message);
                        Interlocked.Increment(ref processedSeries);
                    }
                    catch (OperationCanceledException)
                    {
                        throw; // Let cancellation propagate
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to cache data for series {SeriesId} ({SeriesName})", series.SeriesId, series.Name);
                        Interlocked.Increment(ref processedSeries);
                    }
                }).ConfigureAwait(false);

                _logger?.LogInformation(
                    "Parallel processing completed: {SeriesCount} series, {SeasonCount} seasons, {EpisodeCount} episodes",
                    seriesCount,
                    seasonCount,
                    episodeCount);

                // Fetch TVDb images for series if enabled
                bool useTvdb = Plugin.Instance?.Configuration.UseTvdbForSeriesMetadata ?? true;
                if (useTvdb && _providerManager != null)
                {
                    _logger?.LogInformation("Looking up TVDb metadata for {Count} series...", totalSeries);
                    _currentStatus = "Fetching TVDb images...";

                    // Parse title overrides once before the lookup loop
                    Dictionary<string, string> titleOverrides = ParseTitleOverrides(
                        Plugin.Instance?.Configuration.TvdbTitleOverrides ?? string.Empty);

                    if (titleOverrides.Count > 0)
                    {
                        _logger?.LogInformation("Loaded {Count} TVDb title overrides", titleOverrides.Count);
                    }

                    int tmdbFound = 0;
                    int tmdbNotFound = 0;

                    foreach (var kvp in seriesListsByCategory)
                    {
                        foreach (var series in kvp.Value)
                        {
                            _refreshCancellationTokenSource.Token.ThrowIfCancellationRequested();

                            string? tmdbUrl = await LookupAndCacheTmdbImageAsync(
                                series.SeriesId,
                                series.Name,
                                titleOverrides,
                                cacheOptions,
                                _refreshCancellationTokenSource.Token).ConfigureAwait(false);

                            if (tmdbUrl != null)
                            {
                                tmdbFound++;
                            }
                            else
                            {
                                tmdbNotFound++;
                            }
                        }
                    }

                    _logger?.LogInformation(
                        "TVDb lookup completed: {Found} found, {NotFound} not found",
                        tmdbFound,
                        tmdbNotFound);

                    // NOTE: Per-episode TVDb image lookup is disabled.
                    // The TVDb plugin's TvdbEpisodeProvider.GetSearchResults() does not populate ImageUrl.
                    // Episode images require TvdbEpisodeImageProvider.GetImages(BaseItem), which needs
                    // actual Jellyfin Episode entities in the database (not available for channel plugins).
                    // See docs/features/08-tvdb-artwork-injection/TODO.md for future options.
                }

                progress?.Report(1.0); // 100% complete
                _currentProgress = 1.0;
                _currentStatus = $"Completed: {seriesCount} series, {seasonCount} seasons, {episodeCount} episodes";
                _lastRefreshComplete = DateTime.UtcNow;
                _logger?.LogInformation("Cache refresh completed: {SeriesCount} series, {SeasonCount} seasons, {EpisodeCount} episodes across {CategoryCount} categories", seriesCount, seasonCount, episodeCount, categoryList.Count);

                // Log failure summary if failures occurred
                var (failureCount, failedItems) = _failureTrackingService.GetFailureStats();
                if (failureCount > 0)
                {
                    _logger?.LogWarning(
                        "Cache refresh completed with {FailureCount} persistent HTTP failures. " +
                        "These items will be skipped for the next {ExpirationHours} hours. " +
                        "First 10 failed URLs: {FailedItems}",
                        failureCount,
                        Plugin.Instance?.Configuration.HttpFailureCacheExpirationHours ?? 24,
                        string.Join(", ", failedItems.Take(10)));
                }

                // Eagerly populate Jellyfin's database by fetching all channel items
                // This ensures browsing is instant without any lazy loading
                try
                {
                    _logger?.LogInformation("Starting eager population of Jellyfin database from cache...");
                    await PopulateJellyfinDatabaseAsync(_refreshCancellationTokenSource.Token).ConfigureAwait(false);
                    _logger?.LogInformation("Jellyfin database population completed");
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("Database population cancelled");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to populate Jellyfin database - items may load lazily when browsing");
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Cache refresh cancelled");
                _currentStatus = "Cancelled";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during cache refresh");
                throw;
            }
        }
        finally
        {
            _isRefreshing = false;
            if (_currentProgress < 1.0)
            {
                _currentStatus = "Failed or cancelled";
            }

            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Gets cached categories.
    /// </summary>
    /// <returns>Cached categories, or null if not available.</returns>
    public IEnumerable<Category>? GetCachedCategories()
    {
        try
        {
            string cacheKey = $"{CachePrefix}categories";
            if (_memoryCache.TryGetValue(cacheKey, out List<Category>? categories) && categories != null)
            {
                return categories;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets cached series stream info.
    /// </summary>
    /// <param name="seriesId">The series ID.</param>
    /// <returns>Cached series stream info, or null if not available.</returns>
    public SeriesStreamInfo? GetCachedSeriesInfo(int seriesId)
    {
        try
        {
            string cacheKey = $"{CachePrefix}seriesinfo_{seriesId}";
            return _memoryCache.TryGetValue(cacheKey, out SeriesStreamInfo? info) ? info : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets cached season info.
    /// </summary>
    /// <param name="seriesId">The series ID.</param>
    /// <param name="seasonId">The season ID.</param>
    /// <returns>Cached season info, or null if not available.</returns>
    public XtreamSeason? GetCachedSeason(int seriesId, int seasonId)
    {
        try
        {
            string cacheKey = $"{CachePrefix}season_{seriesId}_{seasonId}";
            return _memoryCache.TryGetValue(cacheKey, out XtreamSeason? season) ? season : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets cached episodes for a season.
    /// </summary>
    /// <param name="seriesId">The series ID.</param>
    /// <param name="seasonId">The season ID.</param>
    /// <returns>Cached episodes, or null if not available.</returns>
    public IEnumerable<XtreamEpisode>? GetCachedEpisodes(int seriesId, int seasonId)
    {
        try
        {
            string cacheKey = $"{CachePrefix}episodes_{seriesId}_{seasonId}";
            if (_memoryCache.TryGetValue(cacheKey, out List<XtreamEpisode>? episodes) && episodes != null)
            {
                return episodes;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets cached series list for a category.
    /// </summary>
    /// <param name="categoryId">The category ID.</param>
    /// <returns>Cached series list, or null if not available.</returns>
    public IEnumerable<XtreamSeries>? GetCachedSeriesList(int categoryId)
    {
        try
        {
            string cacheKey = $"{CachePrefix}serieslist_{categoryId}";
            if (_memoryCache.TryGetValue(cacheKey, out List<XtreamSeries>? seriesList) && seriesList != null)
            {
                return seriesList;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the cached TMDB image URL for a series.
    /// </summary>
    /// <param name="seriesId">The series ID.</param>
    /// <returns>TMDB image URL, or null if not cached.</returns>
    public string? GetCachedTmdbImageUrl(int seriesId)
    {
        try
        {
            string cacheKey = $"{CachePrefix}tmdb_image_{seriesId}";
            if (_memoryCache.TryGetValue(cacheKey, out string? imageUrl) && imageUrl != null)
            {
                return imageUrl;
            }

            _logger?.LogWarning("No cached TVDb image found for series {SeriesId} (key: {CacheKey})", seriesId, cacheKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error retrieving cached TVDb image for series {SeriesId}", seriesId);
            return null;
        }
    }

    /// <summary>
    /// Gets the cached TVDb series ID for a series.
    /// </summary>
    /// <param name="seriesId">The Xtream series ID.</param>
    /// <returns>TVDb series ID, or null if not cached.</returns>
    public string? GetCachedTvdbSeriesId(int seriesId)
    {
        string cacheKey = $"{CachePrefix}tvdb_series_id_{seriesId}";
        return _memoryCache.TryGetValue(cacheKey, out string? tvdbId) ? tvdbId : null;
    }

    /// <summary>
    /// Gets the cached TVDb episode image URL.
    /// </summary>
    /// <param name="seriesId">The Xtream series ID.</param>
    /// <param name="seasonNum">The season number.</param>
    /// <param name="episodeNum">The episode number.</param>
    /// <returns>TVDb episode image URL, or null if not cached.</returns>
    public string? GetCachedEpisodeImageUrl(int seriesId, int seasonNum, int episodeNum)
    {
        string cacheKey = $"{CachePrefix}episode_images_{seriesId}";
        if (_memoryCache.TryGetValue(cacheKey, out Dictionary<string, string>? images) && images != null)
        {
            string key = $"S{seasonNum}E{episodeNum}";
            return images.TryGetValue(key, out string? url) ? url : null;
        }

        return null;
    }

    /// <summary>
    /// Looks up TMDB image URL for a series and caches it.
    /// Checks title overrides first for direct TVDb ID lookup, then falls back to name search.
    /// </summary>
    /// <param name="seriesId">The Xtream series ID.</param>
    /// <param name="seriesName">The series name to search for.</param>
    /// <param name="titleOverrides">Title-to-TVDb-ID override map.</param>
    /// <param name="cacheOptions">Cache options to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The TMDB image URL if found, null otherwise.</returns>
    private async Task<string?> LookupAndCacheTmdbImageAsync(
        int seriesId,
        string seriesName,
        Dictionary<string, string> titleOverrides,
        MemoryCacheEntryOptions cacheOptions,
        CancellationToken cancellationToken)
    {
        if (_providerManager == null)
        {
            return null;
        }

        try
        {
            // Parse the name to remove tags
            string cleanName = StreamService.ParseName(seriesName).Title;
            if (string.IsNullOrWhiteSpace(cleanName))
            {
                return null;
            }

            // Check title overrides first for direct TVDb ID lookup
            if (titleOverrides.TryGetValue(cleanName, out string? tvdbId))
            {
                _logger?.LogInformation("Using TVDb title override for series {SeriesId} ({Name}) → TVDb ID {TvdbId}", seriesId, cleanName, tvdbId);
                string? overrideResult = await LookupByTvdbIdAsync(cleanName, tvdbId, cancellationToken).ConfigureAwait(false);
                if (overrideResult != null)
                {
                    string cacheKey = $"{CachePrefix}tmdb_image_{seriesId}";
                    _memoryCache.Set(cacheKey, overrideResult, cacheOptions);

                    // Cache TVDb series ID for episode lookups
                    _memoryCache.Set($"{CachePrefix}tvdb_series_id_{seriesId}", tvdbId, cacheOptions);

                    _logger?.LogInformation("Cached TVDb image for series {SeriesId} ({Name}) via override (TVDb ID {TvdbId}): {Url}", seriesId, cleanName, tvdbId, overrideResult);
                    return overrideResult;
                }

                _logger?.LogWarning("TVDb title override for series {SeriesId} ({Name}) with TVDb ID {TvdbId} returned no image, falling back to name search", seriesId, cleanName, tvdbId);
            }

            // Fall back to name-based search with progressively cleaned search terms
            string[] searchTerms = GenerateSearchTerms(cleanName);

            foreach (string searchTerm in searchTerms)
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    continue;
                }

                // Search TVDb for the series
                string? lang = _serverConfigManager?.Configuration?.PreferredMetadataLanguage;
                RemoteSearchQuery<JellyfinSeriesInfo> query = new()
                {
                    SearchInfo = new()
                    {
                        Name = searchTerm,
                        MetadataLanguage = lang ?? string.Empty,
                    },
                    SearchProviderName = "TheTVDB",
                };

                IEnumerable<RemoteSearchResult> results = await _providerManager
                    .GetRemoteSearchResults<JellyfinSeries, JellyfinSeriesInfo>(query, cancellationToken)
                    .ConfigureAwait(false);

                // Find first result with a real image (not a placeholder)
                RemoteSearchResult? resultWithImage = results.FirstOrDefault(r =>
                    !string.IsNullOrEmpty(r.ImageUrl) &&
                    !r.ImageUrl.Contains("missing/series", StringComparison.OrdinalIgnoreCase) &&
                    !r.ImageUrl.Contains("missing/movie", StringComparison.OrdinalIgnoreCase));
                if (resultWithImage?.ImageUrl != null)
                {
                    // Cache the TVDb image URL
                    string cacheKey = $"{CachePrefix}tmdb_image_{seriesId}";
                    _memoryCache.Set(cacheKey, resultWithImage.ImageUrl, cacheOptions);

                    // Cache TVDb series ID for episode lookups
                    string? foundTvdbId = resultWithImage.GetProviderId(MetadataProvider.Tvdb);
                    if (!string.IsNullOrEmpty(foundTvdbId))
                    {
                        _memoryCache.Set($"{CachePrefix}tvdb_series_id_{seriesId}", foundTvdbId, cacheOptions);
                    }

                    _logger?.LogInformation("Cached TVDb image for series {SeriesId} ({Name}) using search term '{SearchTerm}': {Url}", seriesId, cleanName, searchTerm, resultWithImage.ImageUrl);
                    return resultWithImage.ImageUrl;
                }
            }

            _logger?.LogWarning("TVDb search found no image for series {SeriesId} ({Name}) after trying: {SearchTerms}", seriesId, cleanName, string.Join(", ", searchTerms));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to lookup TVDb image for series {SeriesId} ({Name})", seriesId, seriesName);
        }

        return null;
    }

    /// <summary>
    /// Looks up a series on TVDb by its TVDb ID and returns the image URL.
    /// </summary>
    /// <param name="cleanName">The cleaned series name (for logging).</param>
    /// <param name="tvdbId">The TVDb ID to look up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The image URL if found, null otherwise.</returns>
    private async Task<string?> LookupByTvdbIdAsync(
        string cleanName,
        string tvdbId,
        CancellationToken cancellationToken)
    {
        if (_providerManager == null)
        {
            return null;
        }

        string? lang = _serverConfigManager?.Configuration?.PreferredMetadataLanguage;
        RemoteSearchQuery<JellyfinSeriesInfo> query = new()
        {
            SearchInfo = new()
            {
                Name = cleanName,
                MetadataLanguage = lang ?? string.Empty,
                ProviderIds = new Dictionary<string, string>
                {
                    { MetadataProvider.Tvdb.ToString(), tvdbId }
                }
            },
            SearchProviderName = "TheTVDB",
        };

        IEnumerable<RemoteSearchResult> results = await _providerManager
            .GetRemoteSearchResults<JellyfinSeries, JellyfinSeriesInfo>(query, cancellationToken)
            .ConfigureAwait(false);

        RemoteSearchResult? resultWithImage = results.FirstOrDefault(r =>
            !string.IsNullOrEmpty(r.ImageUrl) &&
            !r.ImageUrl.Contains("missing/series", StringComparison.OrdinalIgnoreCase) &&
            !r.ImageUrl.Contains("missing/movie", StringComparison.OrdinalIgnoreCase));

        return resultWithImage?.ImageUrl;
    }

    /// <summary>
    /// Generates search terms from a series name for TVDb lookup.
    /// Returns the original name and a variant with language indicators stripped.
    /// </summary>
    /// <param name="name">The original series name.</param>
    /// <returns>Array of search terms to try.</returns>
    private static string[] GenerateSearchTerms(string name)
    {
        List<string> terms = new();

        // 1. Add original name first
        terms.Add(name);

        // 2. Remove language indicators like "(NL Gesproken)", "(DE)", "(French)", etc.
        string withoutLang = System.Text.RegularExpressions.Regex.Replace(
            name,
            @"\s*\([^)]*(?:Gesproken|Dubbed|Subbed|NL|DE|FR|Dutch|German|French|Nederlands|Deutsch)[^)]*\)\s*",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

        if (!string.Equals(withoutLang, name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(withoutLang))
        {
            terms.Add(withoutLang);
        }

        return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Parses the title override configuration string into a dictionary.
    /// Format: one mapping per line, "SeriesTitle=TVDbID".
    /// </summary>
    private static Dictionary<string, string> ParseTitleOverrides(string config)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(config))
        {
            return result;
        }

        foreach (string line in config.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int equalsIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex > 0 && equalsIndex < line.Length - 1)
            {
                string key = line[..equalsIndex].Trim();
                string value = line[(equalsIndex + 1)..].Trim();
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                {
                    result[key] = value;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Looks up TVDb episode images for all series with cached TVDb IDs and caches them.
    /// </summary>
    /// <param name="seriesListsByCategory">All series grouped by category.</param>
    /// <param name="cacheOptions">Cache options to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    private async Task LookupAndCacheEpisodeImagesAsync(
        Dictionary<int, List<XtreamSeries>> seriesListsByCategory,
        MemoryCacheEntryOptions cacheOptions,
        CancellationToken cancellationToken)
    {
        int episodeLookups = 0;
        int episodeImagesFound = 0;
        string? lang = _serverConfigManager?.Configuration?.PreferredMetadataLanguage;

        foreach (var kvp in seriesListsByCategory)
        {
            foreach (var series in kvp.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Only look up episodes for series that have a cached TVDb ID
                string? tvdbSeriesId = GetCachedTvdbSeriesId(series.SeriesId);
                if (string.IsNullOrEmpty(tvdbSeriesId))
                {
                    continue;
                }

                // Get cached series info to iterate episodes
                SeriesStreamInfo? seriesInfo = GetCachedSeriesInfo(series.SeriesId);
                if (seriesInfo == null)
                {
                    continue;
                }

                // Build dictionary of episode images for this series
                Dictionary<string, string> episodeImages = new();

                foreach (var seasonKvp in seriesInfo.Episodes ?? new Dictionary<int, ICollection<Episode>>())
                {
                    int seasonNum = seasonKvp.Key;
                    foreach (var episode in seasonKvp.Value)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        episodeLookups++;

                        try
                        {
                            RemoteSearchQuery<JellyfinEpisodeInfo> query = new()
                            {
                                SearchInfo = new()
                                {
                                    Name = episode.Title,
                                    IndexNumber = episode.EpisodeNum,
                                    ParentIndexNumber = seasonNum,
                                    SeriesProviderIds = new Dictionary<string, string>
                                    {
                                        { MetadataProvider.Tvdb.ToString(), tvdbSeriesId }
                                    },
                                    MetadataLanguage = lang ?? string.Empty,
                                },
                                SearchProviderName = "TheTVDB",
                            };

                            var results = await _providerManager!
                                .GetRemoteSearchResults<JellyfinEpisode, JellyfinEpisodeInfo>(
                                    query, cancellationToken)
                                .ConfigureAwait(false);

                            var match = results.FirstOrDefault(r =>
                                !string.IsNullOrEmpty(r.ImageUrl) &&
                                !r.ImageUrl.Contains("missing/", StringComparison.OrdinalIgnoreCase));

                            if (match?.ImageUrl != null)
                            {
                                string key = $"S{seasonNum}E{episode.EpisodeNum}";
                                episodeImages[key] = match.ImageUrl;
                                episodeImagesFound++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogDebug(
                                ex,
                                "Episode image lookup failed: S{Season}E{Episode} of series {SeriesId}",
                                seasonNum,
                                episode.EpisodeNum,
                                series.SeriesId);
                        }
                    }
                }

                // Cache all episode images for this series as a dictionary
                if (episodeImages.Count > 0)
                {
                    _memoryCache.Set($"{CachePrefix}episode_images_{series.SeriesId}", episodeImages, cacheOptions);
                }

                // Log progress periodically
                if (episodeLookups % 100 == 0)
                {
                    _logger?.LogInformation(
                        "Episode image lookup progress: {Lookups} lookups, {Found} images found",
                        episodeLookups,
                        episodeImagesFound);
                }
            }
        }

        _logger?.LogInformation(
            "Episode image lookup completed: {Lookups} lookups, {Found} images found",
            episodeLookups,
            episodeImagesFound);
    }

    /// <summary>
    /// Clears all cache entries for the given data version.
    /// </summary>
    private void ClearCache(string dataVersion)
    {
        // Note: IMemoryCache doesn't support enumerating keys, so we can't clear by prefix
        // Instead, we rely on cache expiration and version-based keys
        // When data version changes, old keys won't be accessed anymore
        _logger?.LogInformation("Cache cleared (old entries will expire naturally)");
    }

    /// <summary>
    /// Checks if cache is populated for the current data version.
    /// </summary>
    /// <returns>True if cache is populated, false otherwise.</returns>
    public bool IsCachePopulated()
    {
        try
        {
            string cacheKey = $"{CachePrefix}categories";
            return _memoryCache.TryGetValue(cacheKey, out _);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cancels the currently running cache refresh operation.
    /// </summary>
    public void CancelRefresh()
    {
        var cts = _refreshCancellationTokenSource;
        if (_isRefreshing && cts != null)
        {
            _logger?.LogInformation("Cancelling cache refresh...");
            _currentStatus = "Cancelling...";
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, nothing to cancel
            }
        }
    }

    /// <summary>
    /// Invalidates all cached data by incrementing the cache version.
    /// Old cache entries will remain in memory but won't be accessed.
    /// </summary>
    public void InvalidateCache()
    {
        _cacheVersion++;
        _currentProgress = 0.0;
        _currentStatus = "Cache invalidated";
        _lastRefreshComplete = null;
        _logger?.LogInformation("Cache invalidated (version incremented to {Version})", _cacheVersion);
    }

    /// <summary>
    /// Eagerly populates Jellyfin's database using delta-based approach.
    /// Compares existing series in DB with cache and only processes new/missing series.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task PopulateJellyfinDatabaseAsync(CancellationToken cancellationToken)
    {
        _currentStatus = "Populating Jellyfin database...";
        var startTime = DateTime.UtcNow;

        try
        {
            IChannelManager? channelManager = Plugin.Instance?.ChannelManager;
            if (channelManager == null)
            {
                _logger?.LogWarning("ChannelManager not available, skipping database population");
                return;
            }

            // Find our Series channel
            var channelQuery = new ChannelQuery();
            var channelsResult = await channelManager.GetChannelsInternalAsync(channelQuery).ConfigureAwait(false);

            var seriesChannel = channelsResult.Items.FirstOrDefault(c => c.Name == "Xtream Series");
            if (seriesChannel == null)
            {
                _logger?.LogWarning("Xtream Series channel not found, skipping database population");
                return;
            }

            Guid channelId = seriesChannel.Id;

            // Phase 1: Get expected series from cache
            var expectedSeriesList = new List<(int SeriesId, string Name, int CategoryId, Guid FolderGuid)>();
            var categories = GetCachedCategories();
            if (categories != null)
            {
                foreach (var category in categories)
                {
                    var seriesList = GetCachedSeriesList(category.CategoryId);
                    if (seriesList != null)
                    {
                        foreach (var series in seriesList)
                        {
                            var parsedName = StreamService.ParseName(series.Name);
                            // Generate the same GUID that SeriesChannel uses for folder IDs
                            Guid folderGuid = StreamService.ToGuid(StreamService.SeriesPrefix, series.CategoryId, series.SeriesId, 0);
                            expectedSeriesList.Add((series.SeriesId, parsedName.Title, series.CategoryId, folderGuid));
                        }
                    }
                }
            }

            _logger?.LogInformation("Cache contains {Count} series", expectedSeriesList.Count);

            if (expectedSeriesList.Count == 0)
            {
                _logger?.LogInformation("No series in cache, skipping database population");
                _currentStatus = "No series to populate";
                return;
            }

            // Phase 2: Get existing root items (series) from channel
            _logger?.LogInformation("Querying existing series from channel...");
            var rootQuery = new InternalItemsQuery
            {
                ChannelIds = new[] { channelId },
                Recursive = false
            };

            var existingRoot = await channelManager.GetChannelItemsInternal(
                rootQuery,
                new Progress<double>(),
                cancellationToken).ConfigureAwait(false);

            // Build map of ExternalId to existing item for delta detection
            // Channel items use ExternalId to store the folder GUID string
            var existingByExternalId = new Dictionary<string, BaseItem>();
            if (existingRoot?.Items != null)
            {
                foreach (var item in existingRoot.Items)
                {
                    if (!string.IsNullOrEmpty(item.ExternalId))
                    {
                        existingByExternalId[item.ExternalId] = item;
                    }
                }
            }

            int existingSeriesCount = existingByExternalId.Count;
            _logger?.LogInformation("Found {Count} existing series in database", existingSeriesCount);

            // Phase 3: Determine which series need processing
            var seriesToProcess = new List<(int SeriesId, string Name, int CategoryId, Guid FolderGuid, BaseItem? DbItem)>();
            int unchangedCount = 0;

            foreach (var series in expectedSeriesList)
            {
                string externalId = series.FolderGuid.ToString();
                if (existingByExternalId.TryGetValue(externalId, out var dbItem))
                {
                    // Series exists - check if we need to refresh its children
                    seriesToProcess.Add((series.SeriesId, series.Name, series.CategoryId, series.FolderGuid, dbItem));
                    unchangedCount++;
                }
                else
                {
                    // New series - needs to be added
                    seriesToProcess.Add((series.SeriesId, series.Name, series.CategoryId, series.FolderGuid, null));
                }
            }

            int newSeriesCount = seriesToProcess.Count(s => s.DbItem == null);
            bool isFullyPopulated = unchangedCount >= expectedSeriesList.Count * 0.95;

            _logger?.LogInformation(
                "Delta analysis: {Unchanged} unchanged, {New} new, isFullyPopulated={IsPopulated}",
                unchangedCount,
                newSeriesCount,
                isFullyPopulated);

            // Phase 4: Fast path - if database is already fully populated with all series, just verify a sample
            if (newSeriesCount == 0 && isFullyPopulated)
            {
                // Quick check: verify first few series have children populated
                int verifyCount = Math.Min(5, seriesToProcess.Count);
                bool allHaveChildren = true;

                for (int i = 0; i < verifyCount && allHaveChildren; i++)
                {
                    var series = seriesToProcess[i];
                    if (series.DbItem != null)
                    {
                        try
                        {
                            var childQuery = new InternalItemsQuery
                            {
                                ChannelIds = new[] { channelId },
                                ParentId = series.DbItem.Id,
                                Recursive = false,
                                Limit = 1
                            };

                            var childResult = await channelManager.GetChannelItemsInternal(
                                childQuery,
                                new Progress<double>(),
                                cancellationToken).ConfigureAwait(false);

                            allHaveChildren = (childResult?.TotalRecordCount ?? 0) > 0;
                        }
                        catch
                        {
                            allHaveChildren = false;
                        }
                    }
                }

                if (allHaveChildren)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    _logger?.LogInformation(
                        "Database already fully populated ({Existing}/{Expected} series) - skipping in {Elapsed:F1}s",
                        existingSeriesCount,
                        expectedSeriesList.Count,
                        elapsed.TotalSeconds);
                    _currentStatus = "Database up to date";
                    return;
                }
            }

            // Phase 5: Process series - populate seasons and episodes
            int totalSeries = seriesToProcess.Count;
            _logger?.LogInformation(
                "Starting parallel database population with parallelism=5, minDelayMs=50 for {Count} series",
                totalSeries);

            const int parallelism = 5;
            const int minDelayMs = 50;

            int seriesProcessed = 0;
            int seasonsProcessed = 0;
            int episodesProcessed = 0;
            int errorCount = 0;
            const int maxWarningErrors = 10;

            using var throttleSemaphore = new SemaphoreSlim(1, 1);
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = parallelism,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(seriesToProcess, parallelOptions, async (seriesInfo, ct) =>
            {
                await throttleSemaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await Task.Delay(minDelayMs, ct).ConfigureAwait(false);
                }
                finally
                {
                    throttleSemaphore.Release();
                }

                int localSeasons = 0;
                int localEpisodes = 0;

                try
                {
                    BaseItem? seriesDbItem = seriesInfo.DbItem;

                    // If series isn't in DB yet, we need to fetch root to add it
                    if (seriesDbItem == null)
                    {
                        // Re-query root items to get this series added
                        var refreshQuery = new InternalItemsQuery
                        {
                            ChannelIds = new[] { channelId },
                            Recursive = false
                        };

                        var refreshResult = await channelManager.GetChannelItemsInternal(
                            refreshQuery,
                            new Progress<double>(),
                            ct).ConfigureAwait(false);

                        // Find our series in the result
                        string targetExternalId = seriesInfo.FolderGuid.ToString();
                        seriesDbItem = refreshResult?.Items?.FirstOrDefault(i => i.ExternalId == targetExternalId);

                        if (seriesDbItem == null)
                        {
                            _logger?.LogDebug("Series {Name} not found after refresh, skipping", seriesInfo.Name);
                            return;
                        }
                    }

                    // Fetch series children (seasons) using ParentId
                    var seriesQuery = new InternalItemsQuery
                    {
                        ChannelIds = new[] { channelId },
                        ParentId = seriesDbItem.Id,
                        Recursive = false
                    };

                    var seasonsResult = await channelManager.GetChannelItemsInternal(
                        seriesQuery,
                        new Progress<double>(),
                        ct).ConfigureAwait(false);

                    if (seasonsResult?.Items != null)
                    {
                        foreach (var seasonItem in seasonsResult.Items)
                        {
                            ct.ThrowIfCancellationRequested();
                            localSeasons++;

                            // Fetch season children (episodes)
                            var seasonQuery = new InternalItemsQuery
                            {
                                ChannelIds = new[] { channelId },
                                ParentId = seasonItem.Id,
                                Recursive = false
                            };

                            try
                            {
                                var episodesResult = await channelManager.GetChannelItemsInternal(
                                    seasonQuery,
                                    new Progress<double>(),
                                    ct).ConfigureAwait(false);

                                localEpisodes += episodesResult?.TotalRecordCount ?? 0;
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                // Jellyfin internal error - skip this season's episodes
                                _logger?.LogDebug("ArgumentOutOfRangeException fetching episodes for season in {Name}, skipping", seriesInfo.Name);
                            }
                        }
                    }

                    Interlocked.Add(ref seasonsProcessed, localSeasons);
                    Interlocked.Add(ref episodesProcessed, localEpisodes);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Jellyfin internal error - this can happen with channel queries
                    // Log but continue with other series
                    int currentErrorCount = Interlocked.Increment(ref errorCount);
                    if (currentErrorCount <= maxWarningErrors)
                    {
                        _logger?.LogDebug("ArgumentOutOfRangeException processing series {Name}, skipping", seriesInfo.Name);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    int currentErrorCount = Interlocked.Increment(ref errorCount);
                    if (currentErrorCount <= maxWarningErrors)
                    {
                        _logger?.LogWarning(ex, "Error populating series {SeriesId} ({Name})", seriesInfo.SeriesId, seriesInfo.Name);
                    }
                }

                int currentProcessed = Interlocked.Increment(ref seriesProcessed);
                if (currentProcessed % 10 == 0 || currentProcessed == totalSeries)
                {
                    _logger?.LogInformation(
                        "Database population progress: {Series}/{Total} series, {Seasons} seasons, {Episodes} episodes",
                        currentProcessed,
                        totalSeries,
                        Volatile.Read(ref seasonsProcessed),
                        Volatile.Read(ref episodesProcessed));
                    _currentStatus = $"Populating: {currentProcessed}/{totalSeries} series...";
                }
            }).ConfigureAwait(false);

            var totalElapsed = DateTime.UtcNow - startTime;

            // Summary
            if (errorCount > 0)
            {
                _logger?.LogWarning(
                    "Jellyfin database population completed in {Elapsed:F1}s with {Errors} errors: {Series} series, {Seasons} seasons, {Episodes} episodes",
                    totalElapsed.TotalSeconds,
                    errorCount,
                    seriesProcessed,
                    seasonsProcessed,
                    episodesProcessed);
            }
            else
            {
                _logger?.LogInformation(
                    "Jellyfin database population completed in {Elapsed:F1}s: {Series} series, {Seasons} seasons, {Episodes} episodes",
                    totalElapsed.TotalSeconds,
                    seriesProcessed,
                    seasonsProcessed,
                    episodesProcessed);
            }

            _currentStatus = $"Database populated: {seriesProcessed} series, {seasonsProcessed} seasons, {episodesProcessed} episodes";
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Database population cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during Jellyfin database population");
        }
    }

    /// <summary>
    /// Gets the current cache refresh status.
    /// </summary>
    /// <returns>Cache status information.</returns>
    public (bool IsRefreshing, double Progress, string Status, DateTime? StartTime, DateTime? CompleteTime) GetStatus()
    {
        return (_isRefreshing, _currentProgress, _currentStatus, _lastRefreshStart, _lastRefreshComplete);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the SeriesCacheService and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshCancellationTokenSource?.Dispose();
            _refreshLock?.Dispose();
        }
    }
}
