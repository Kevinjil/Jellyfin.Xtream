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
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Configuration;
using Jellyfin.Xtream.Service;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream
{
    /// <summary>
    /// The Xtream Codes API channel.
    /// </summary>
    public class SerieChannel : IChannel
    {
        private readonly ILogger<SerieChannel> logger;
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="SerieChannel"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
        /// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
        public SerieChannel(ILogger<SerieChannel> logger, IMemoryCache memoryCache)
        {
            this.logger = logger;
            this.memoryCache = memoryCache;
        }

        /// <inheritdoc />
        public string? Name => "Xtream Series";

        /// <inheritdoc />
        public string? Description => "Series streamed from the Xtream-compatible server.";

        /// <inheritdoc />
        public string DataVersion => string.Empty;

        /// <inheritdoc />
        public string HomePageUrl => string.Empty;

        /// <inheritdoc />
        public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

        /// <inheritdoc />
        public InternalChannelFeatures GetChannelFeatures()
        {
            return new InternalChannelFeatures
            {
                ContentTypes = new List<ChannelMediaContentType>
                {
                    ChannelMediaContentType.Episode,
                },

                MediaTypes = new List<ChannelMediaType>
                {
                    ChannelMediaType.Video
                },
                MaxPageSize = int.MaxValue
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
            if (string.IsNullOrEmpty(query.FolderId))
            {
                return await GetCategories(cancellationToken).ConfigureAwait(false);
            }

            if (query.FolderId.StartsWith("cat-", StringComparison.InvariantCulture))
            {
                return await GetSeries(query.FolderId.Substring(4), cancellationToken).ConfigureAwait(false);
            }

            if (query.FolderId.StartsWith("ser-", StringComparison.InvariantCulture))
            {
                return await GetSeasons(query.FolderId.Substring(4), cancellationToken).ConfigureAwait(false);
            }

            string[] parts = query.FolderId.Split('-');
            string seriesId = parts[0];
            int season = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);

            return await GetEpisodes(seriesId, season, cancellationToken).ConfigureAwait(false);
        }

        private async Task<ChannelItemResult> GetCategories(CancellationToken cancellationToken)
        {
            string key = "xtream-series-categories";
            if (memoryCache.TryGetValue(key, out ChannelItemResult o))
            {
                return o;
            }

            Plugin? plugin = Plugin.Instance;
            if (plugin == null)
            {
                throw new ArgumentException("Plugin not initialized!");
            }

            using (XtreamClient client = new XtreamClient())
            {
                List<Category> categories = await client.GetSeriesCategoryAsync(plugin.Creds, cancellationToken).ConfigureAwait(false);
                List<ChannelItemInfo> items = new List<ChannelItemInfo>();

                foreach (Category category in categories)
                {
                    ParsedName parsedName = plugin.StreamService.ParseName(category.CategoryName);
                    items.Add(new ChannelItemInfo()
                    {
                        Id = $"cat-{category.CategoryId}",
                        Name = category.CategoryName,
                        Tags = new List<string>(parsedName.Tags),
                        Type = ChannelItemType.Folder,
                    });
                }

                ChannelItemResult result = new ChannelItemResult()
                {
                    Items = items,
                    TotalRecordCount = items.Count
                };
                memoryCache.Set(key, result, DateTimeOffset.Now.AddMinutes(7));
                return result;
            }
        }

        private async Task<ChannelItemResult> GetSeries(string categoryId, CancellationToken cancellationToken)
        {
            string key = $"xtream-series-{categoryId}";
            if (memoryCache.TryGetValue(key, out ChannelItemResult o))
            {
                return o;
            }

            Plugin? plugin = Plugin.Instance;
            if (plugin == null)
            {
                throw new ArgumentException("Plugin not initialized!");
            }

            using (XtreamClient client = new XtreamClient())
            {
                List<Series> series = await client.GetSeriesByCategoryAsync(plugin.Creds, categoryId, cancellationToken).ConfigureAwait(false);
                List<ChannelItemInfo> items = new List<ChannelItemInfo>();

                foreach (Series serie in series)
                {
                    ParsedName parsedName = plugin.StreamService.ParseName(serie.Name);
                    items.Add(new ChannelItemInfo()
                    {
                        Id = $"ser-{serie.SeriesId}",
                        Name = parsedName.Title,
                        Tags = new List<string>(parsedName.Tags),
                        Type = ChannelItemType.Folder,
                        ImageUrl = serie.Cover,
                        DateModified = serie.LastModified,
                        CommunityRating = (float)serie.Rating5Based,
                        Genres = new List<string>() { serie.Genre },
                        People = serie.Cast.Split(",").Select(name => new PersonInfo()
                        {
                            Name = name.Trim()
                        }).ToList(),
                        // FolderType = ChannelFolderType.Series,
                    });
                }

                ChannelItemResult result = new ChannelItemResult()
                {
                    Items = items,
                    TotalRecordCount = items.Count
                };
                memoryCache.Set(key, result, DateTimeOffset.Now.AddMinutes(7));
                return result;
            }
        }

        private async Task<ChannelItemResult> GetSeasons(string seriesId, CancellationToken cancellationToken)
        {
            string key = $"xtream-seasons-{seriesId}";
            if (memoryCache.TryGetValue(key, out ChannelItemResult o))
            {
                return o;
            }

            Plugin? plugin = Plugin.Instance;
            if (plugin == null)
            {
                throw new ArgumentException("Plugin not initialized!");
            }

            using (XtreamClient client = new XtreamClient())
            {
                SeriesStreamInfo series = await client.GetSeriesStreamsBySeriesAsync(plugin.Creds, seriesId, cancellationToken).ConfigureAwait(false);
                List<ChannelItemInfo> items = new List<ChannelItemInfo>();

                foreach (int seasonId in series.Episodes.Keys)
                {
                    Season? season = series.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);
                    if (season != null)
                    {
                        ParsedName parsedName = plugin.StreamService.ParseName(season.Name);
                        items.Add(new ChannelItemInfo()
                        {
                            Id = $"{seriesId}-{seasonId}",
                            Name = parsedName.Title,
                            Tags = new List<string>(parsedName.Tags),
                            Type = ChannelItemType.Folder,
                            ImageUrl = season.Cover,
                            DateCreated = season.AirDate,
                            // FolderType = ChannelFolderType.Season,
                        });
                    }
                    else
                    {
                        items.Add(new ChannelItemInfo()
                        {
                            Id = $"{seriesId}-{seasonId}",
                            Name = $"Season {seasonId}",
                            Type = ChannelItemType.Folder,
                            ImageUrl = series.Info.Cover,
                            // FolderType = ChannelFolderType.Season,
                        });
                    }
                }

                ChannelItemResult result = new ChannelItemResult()
                {
                    Items = items,
                    TotalRecordCount = items.Count
                };
                memoryCache.Set(key, result, DateTimeOffset.Now.AddMinutes(7));
                return result;
            }
        }

        private async Task<ChannelItemResult> GetEpisodes(string seriesId, int season, CancellationToken cancellationToken)
        {
            string key = $"xtream-episodes-{seriesId}";
            if (memoryCache.TryGetValue(key, out ChannelItemResult o))
            {
                return o;
            }

            Plugin? plugin = Plugin.Instance;
            if (plugin == null)
            {
                throw new ArgumentException("Plugin not initialized!");
            }

            using (XtreamClient client = new XtreamClient())
            {
                SeriesStreamInfo channel = await client.GetSeriesStreamsBySeriesAsync(plugin.Creds, seriesId, cancellationToken).ConfigureAwait(false);
                List<ChannelItemInfo> items = new List<ChannelItemInfo>();

                foreach (Episode episode in channel.Episodes[season])
                {
                    string id = episode.EpisodeId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    ParsedName parsedName = plugin.StreamService.ParseName(episode.Title);
                    List<MediaSourceInfo> sources = new List<MediaSourceInfo>()
                    {
                        plugin.StreamService.GetMediaSourceInfo(StreamType.Series, id, episode.ContainerExtension)
                    };

                    items.Add(new ChannelItemInfo()
                    {
                        ContentType = ChannelMediaContentType.Episode,
                        DateCreated = DateTimeOffset.FromUnixTimeSeconds(episode.Added).DateTime,
                        Id = id,
                        ImageUrl = episode.Info.MovieImage,
                        IsLiveStream = false,
                        MediaSources = sources,
                        MediaType = ChannelMediaType.Video,
                        Name = parsedName.Title,
                        Tags = new List<string>(parsedName.Tags),
                        Type = ChannelItemType.Media,
                    });
                }

                ChannelItemResult result = new ChannelItemResult()
                {
                    Items = items,
                    TotalRecordCount = items.Count
                };
                memoryCache.Set(key, result, DateTimeOffset.Now.AddMinutes(1));
                return result;
            }
        }

        /// <inheritdoc />
        public bool IsEnabledFor(string userId)
        {
            return true;
        }
    }
}
