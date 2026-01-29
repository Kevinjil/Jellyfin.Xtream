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
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Service;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream;

/// <summary>
/// The Xtream Codes API channel.
/// </summary>
/// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
public class SeriesChannel(ILogger<SeriesChannel> logger) : IChannel, IDisableMediaSourceDisplay
{
    /// <inheritdoc />
    public string? Name => "Xtream Series";

    /// <inheritdoc />
    public string? Description => "Series streamed from the Xtream-compatible server.";

    /// <inheritdoc />
    public string DataVersion => Plugin.Instance.DataVersion;

    /// <inheritdoc />
    public string HomePageUrl => string.Empty;

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    /// <inheritdoc />
    public InternalChannelFeatures GetChannelFeatures()
    {
        return new InternalChannelFeatures
        {
            ContentTypes = [
                ChannelMediaContentType.Episode,
            ],

            MediaTypes = [
                ChannelMediaType.Video
            ],
        };
    }

    /// <inheritdoc />
    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        switch (type)
        {
            default:
                throw new ArgumentException("Unsupported image type: " + type);
        }
    }

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedChannelImages()
    {
        return new List<ImageType>
        {
            // ImageType.Primary
        };
    }

    /// <inheritdoc />
    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        logger.LogInformation("GetChannelItems called - FolderId: {FolderId}", query.FolderId ?? "(root)");
        try
        {
            if (string.IsNullOrEmpty(query.FolderId))
            {
                // Check if flat series view is enabled
                if (Plugin.Instance.Configuration.FlattenSeriesView)
                {
                    return await GetAllSeriesFlattened(cancellationToken).ConfigureAwait(false);
                }

                return await GetCategories(cancellationToken).ConfigureAwait(false);
            }

            Guid guid = Guid.Parse(query.FolderId);
            StreamService.FromGuid(guid, out int prefix, out int categoryId, out int seriesId, out int seasonId);
            if (prefix == StreamService.SeriesCategoryPrefix)
            {
                return await GetSeries(categoryId, cancellationToken).ConfigureAwait(false);
            }

            if (prefix == StreamService.SeriesPrefix)
            {
                return await GetSeasons(seriesId, cancellationToken).ConfigureAwait(false);
            }

            if (prefix == StreamService.SeasonPrefix)
            {
                return await GetEpisodes(seriesId, seasonId, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get channel items");
            throw;
        }

        return new ChannelItemResult()
        {
            TotalRecordCount = 0,
        };
    }

    private ChannelItemInfo CreateChannelItemInfo(Series series)
    {
        ParsedName parsedName = StreamService.ParseName(series.Name);

        // Use cached TVDb image if available, otherwise fall back to Xtream cover
        string? imageUrl = Plugin.Instance.SeriesCacheService.GetCachedTmdbImageUrl(series.SeriesId);
        imageUrl ??= series.Cover;

        return new ChannelItemInfo()
        {
            CommunityRating = (float)series.Rating5Based,
            DateModified = series.LastModified,
            FolderType = ChannelFolderType.Series,
            Genres = GetGenres(series.Genre),
            Id = StreamService.ToGuid(StreamService.SeriesPrefix, series.CategoryId, series.SeriesId, 0).ToString(),
            ImageUrl = imageUrl,
            Name = parsedName.Title,
            SeriesName = parsedName.Title,
            People = GetPeople(series.Cast),
            Tags = new List<string>(parsedName.Tags),
            Type = ChannelItemType.Folder,
        };
    }

    private static List<string> GetGenres(string genreString)
    {
        if (string.IsNullOrEmpty(genreString))
        {
            return [];
        }

        return new(genreString.Split(',').Select(genre => genre.Trim()));
    }

    private static List<PersonInfo> GetPeople(string cast)
    {
        if (string.IsNullOrEmpty(cast))
        {
            return [];
        }

        return cast.Split(',').Select(name => new PersonInfo()
        {
            Name = name.Trim()
        }).ToList();
    }

    private ChannelItemInfo CreateChannelItemInfo(int seriesId, SeriesStreamInfo series, int seasonId)
    {
        Client.Models.SeriesInfo serie = series.Info;
        string name = $"Season {seasonId}";
        string? overview = null;
        DateTime? created = null;
        List<string> tags = [];

        string? cover = null;
        Season? season = series.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);
        if (season != null)
        {
            ParsedName parsedName = StreamService.ParseName(season.Name);
            name = parsedName.Title;
            tags.AddRange(parsedName.Tags);
            created = season.AirDate;
            overview = season.Overview;
            if (!string.IsNullOrEmpty(season.Cover))
            {
                cover = season.Cover;
            }
        }

        cover ??= Plugin.Instance.SeriesCacheService.GetCachedTmdbImageUrl(seriesId);
        cover ??= series.Info.Cover;

        return new()
        {
            DateCreated = created,
            FolderType = ChannelFolderType.Season,
            Genres = GetGenres(serie.Genre),
            Id = StreamService.ToGuid(StreamService.SeasonPrefix, serie.CategoryId, seriesId, seasonId).ToString(),
            ImageUrl = cover,
            IndexNumber = seasonId,
            Name = name,
            Overview = overview,
            People = GetPeople(serie.Cast),
            Tags = tags,
            Type = ChannelItemType.Folder,
        };
    }

    private ChannelItemInfo CreateChannelItemInfo(int seriesId, SeriesStreamInfo series, Season? season, Episode episode)
    {
        Client.Models.SeriesInfo serie = series.Info;
        ParsedName parsedName = StreamService.ParseName(episode.Title);
        List<MediaSourceInfo> sources =
        [
            Plugin.Instance.StreamService.GetMediaSourceInfo(
                StreamType.Series,
                episode.EpisodeId,
                episode.ContainerExtension,
                videoInfo: episode.Info?.Video,
                audioInfo: episode.Info?.Audio)
        ];

        string? cover = Plugin.Instance.SeriesCacheService.GetCachedEpisodeImageUrl(
            seriesId, episode.Season, episode.EpisodeNum);
        cover ??= episode.Info?.MovieImage;
        cover ??= season?.Cover;
        cover ??= Plugin.Instance.SeriesCacheService.GetCachedTmdbImageUrl(seriesId);
        cover ??= serie.Cover;

        return new()
        {
            ContentType = ChannelMediaContentType.Episode,
            DateCreated = episode.Added,
            Genres = GetGenres(serie.Genre),
            Id = StreamService.ToGuid(StreamService.EpisodePrefix, 0, 0, episode.EpisodeId).ToString(),
            ImageUrl = cover,
            IndexNumber = episode.EpisodeNum,
            IsLiveStream = false,
            MediaSources = sources,
            MediaType = ChannelMediaType.Video,
            Name = string.IsNullOrWhiteSpace(parsedName.Title)
                ? $"Episode {episode.EpisodeNum}"
                : parsedName.Title,
            Overview = episode.Info?.Plot,
            ParentIndexNumber = episode.Season,
            People = GetPeople(serie.Cast),
            RunTimeTicks = episode.Info?.DurationSecs * TimeSpan.TicksPerSecond,
            SeriesName = serie.Name,
            Tags = new(parsedName.Tags),
            Type = ChannelItemType.Media,
        };
    }

    private async Task<ChannelItemResult> GetCategories(CancellationToken cancellationToken)
    {
        // Try cache first
        IEnumerable<Category>? cachedCategories = Plugin.Instance.SeriesCacheService.GetCachedCategories();
        IEnumerable<Category> categories = cachedCategories ?? await Plugin.Instance.StreamService.GetSeriesCategories(cancellationToken).ConfigureAwait(false);

        List<ChannelItemInfo> items = new(
            categories.Select((Category category) => StreamService.CreateChannelItemInfo(StreamService.SeriesCategoryPrefix, category)));
        return new()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
    }

    private async Task<ChannelItemResult> GetAllSeriesFlattened(CancellationToken cancellationToken)
    {
        logger.LogInformation("GetAllSeriesFlattened called");
        // Try cache first for categories
        IEnumerable<Category>? cachedCategories = Plugin.Instance.SeriesCacheService.GetCachedCategories();
        IEnumerable<Category> categories = cachedCategories ?? await Plugin.Instance.StreamService.GetSeriesCategories(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("GetAllSeriesFlattened found {Count} categories", categories.Count());
        List<ChannelItemInfo> items = new();

        // Get all series from all selected categories
        foreach (Category category in categories)
        {
            try
            {
                // Try to get from cache first
                IEnumerable<Series>? cachedSeries = Plugin.Instance.SeriesCacheService.GetCachedSeriesList(category.CategoryId);
                IEnumerable<Series> series;

                if (cachedSeries != null)
                {
                    series = cachedSeries;
                    logger.LogInformation("GetAllSeriesFlattened got {Count} series from cache for category {CategoryId}", series.Count(), category.CategoryId);
                }
                else
                {
                    // Fallback to API if cache miss
                    series = await Plugin.Instance.StreamService.GetSeries(category.CategoryId, cancellationToken).ConfigureAwait(false);
                    logger.LogInformation("GetAllSeriesFlattened got {Count} series from API for category {CategoryId}", series.Count(), category.CategoryId);
                }

                items.AddRange(series.Select(CreateChannelItemInfo));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get series for category {CategoryId}", category.CategoryId);
            }
        }

        // Sort alphabetically for consistent display
        items = items.OrderBy(item => item.Name).ToList();

        logger.LogInformation("GetAllSeriesFlattened returning {Count} series total", items.Count);
        return new()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
    }

    private async Task<ChannelItemResult> GetSeries(int categoryId, CancellationToken cancellationToken)
    {
        IEnumerable<Series> series = await Plugin.Instance.StreamService.GetSeries(categoryId, cancellationToken).ConfigureAwait(false);
        List<ChannelItemInfo> items = new(series.Select(CreateChannelItemInfo));
        return new()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
    }

    private async Task<ChannelItemResult> GetSeasons(int seriesId, CancellationToken cancellationToken)
    {
        logger.LogInformation("GetSeasons called - seriesId: {SeriesId}", seriesId);
        // Try cache first
        SeriesStreamInfo? cachedSeriesInfo = Plugin.Instance.SeriesCacheService.GetCachedSeriesInfo(seriesId);

        IEnumerable<Tuple<SeriesStreamInfo, int>> seasons;
        if (cachedSeriesInfo != null)
        {
            // Use cached data - get season IDs from Episodes dictionary keys
            seasons = cachedSeriesInfo.Episodes?.Keys.Select(seasonId => new Tuple<SeriesStreamInfo, int>(cachedSeriesInfo, seasonId))
                ?? Enumerable.Empty<Tuple<SeriesStreamInfo, int>>();
            logger.LogInformation("GetSeasons cache HIT for series {SeriesId} - returning {Count} seasons from cache", seriesId, seasons.Count());
        }
        else
        {
            // Fallback to API call
            logger.LogWarning("GetSeasons cache MISS for series {SeriesId} - falling back to API call", seriesId);
            seasons = await Plugin.Instance.StreamService.GetSeasons(seriesId, cancellationToken).ConfigureAwait(false);
        }

        List<ChannelItemInfo> items = new(
            seasons.Select((Tuple<SeriesStreamInfo, int> tuple) => CreateChannelItemInfo(seriesId, tuple.Item1, tuple.Item2)));
        return new()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
    }

    private async Task<ChannelItemResult> GetEpisodes(int seriesId, int seasonId, CancellationToken cancellationToken)
    {
        logger.LogInformation("GetEpisodes called - seriesId: {SeriesId}, seasonId: {SeasonId}", seriesId, seasonId);
        // Try cache first
        IEnumerable<Episode>? cachedEpisodes = Plugin.Instance.SeriesCacheService.GetCachedEpisodes(seriesId, seasonId);
        Season? cachedSeason = Plugin.Instance.SeriesCacheService.GetCachedSeason(seriesId, seasonId);
        SeriesStreamInfo? cachedSeriesInfo = Plugin.Instance.SeriesCacheService.GetCachedSeriesInfo(seriesId);

        List<ChannelItemInfo> items;
        if (cachedEpisodes != null && cachedSeriesInfo != null)
        {
            // Use cached data
            items = new List<ChannelItemInfo>(
                cachedEpisodes.Select(episode => CreateChannelItemInfo(seriesId, cachedSeriesInfo, cachedSeason, episode)));
            logger.LogInformation("GetEpisodes cache HIT for series {SeriesId} season {SeasonId} - returning {Count} episodes from cache", seriesId, seasonId, items.Count);
        }
        else
        {
            // Fallback to API call
            logger.LogWarning("GetEpisodes cache MISS for series {SeriesId} season {SeasonId} (cachedEpisodes: {HasEpisodes}, cachedSeriesInfo: {HasInfo}) - falling back to API call", seriesId, seasonId, cachedEpisodes != null, cachedSeriesInfo != null);
            IEnumerable<Tuple<SeriesStreamInfo, Season?, Episode>> episodes = await Plugin.Instance.StreamService.GetEpisodes(seriesId, seasonId, cancellationToken).ConfigureAwait(false);
            items = new List<ChannelItemInfo>(
                episodes.Select((Tuple<SeriesStreamInfo, Season?, Episode> tuple) => CreateChannelItemInfo(seriesId, tuple.Item1, tuple.Item2, tuple.Item3)));
        }

        logger.LogInformation("GetEpisodes returning {Count} episodes for seriesId: {SeriesId}, seasonId: {SeasonId}", items.Count, seriesId, seasonId);
        return new()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
    }

    /// <inheritdoc />
    public bool IsEnabledFor(string userId)
    {
        return Plugin.Instance.Configuration.IsSeriesVisible;
    }
}
