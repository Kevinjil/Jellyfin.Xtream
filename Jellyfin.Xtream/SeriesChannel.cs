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

namespace Jellyfin.Xtream
{
    /// <summary>
    /// The Xtream Codes API channel.
    /// </summary>
    public class SeriesChannel : IChannel
    {
        private readonly ILogger<SeriesChannel> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesChannel"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
        public SeriesChannel(ILogger<SeriesChannel> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc />
        public string? Name => "Xtream Series";

        /// <inheritdoc />
        public string? Description => "Series streamed from the Xtream-compatible server.";

        /// <inheritdoc />
        public string DataVersion => Plugin.Instance.Creds.ToString();

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

            return new ChannelItemResult()
            {
                TotalRecordCount = 0,
            };
        }

        private ChannelItemInfo CreateChannelItemInfo(Series series)
        {
            ParsedName parsedName = StreamService.ParseName(series.Name);
            return new ChannelItemInfo()
            {
                CommunityRating = (float)series.Rating5Based,
                DateModified = series.LastModified,
                // FolderType = ChannelFolderType.Series,
                Genres = GetGenres(series.Genre),
                Id = StreamService.ToGuid(StreamService.SeriesPrefix, series.CategoryId, series.SeriesId, 0).ToString(),
                ImageUrl = series.Cover,
                Name = parsedName.Title,
                People = GetPeople(series.Cast),
                Tags = new List<string>(parsedName.Tags),
                Type = ChannelItemType.Folder,
            };
        }

        private List<string> GetGenres(string genreString)
        {
            return new List<string>(genreString.Split(',').Select(genre => genre.Trim()));
        }

        private List<PersonInfo> GetPeople(string cast)
        {
            return cast.Split(',').Select(name => new PersonInfo()
            {
                Name = name.Trim()
            }).ToList();
        }

        private ChannelItemInfo CreateChannelItemInfo(int seriesId, SeriesStreamInfo series, int seasonId)
        {
            Jellyfin.Xtream.Client.Models.SeriesInfo serie = series.Info;
            string name = $"Season {seasonId}";
            string cover = series.Info.Cover;
            string? overview = null;
            DateTime? created = null;
            List<string> tags = new List<string>();

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

            return new ChannelItemInfo()
            {
                DateCreated = created,
                // FolderType = ChannelFolderType.Season,
                Genres = GetGenres(serie.Genre),
                Id = StreamService.ToGuid(StreamService.SeasonPrefix, serie.CategoryId, seriesId, seasonId).ToString(),
                ImageUrl = cover,
                Name = name,
                Overview = overview,
                People = GetPeople(serie.Cast),
                Tags = tags,
                Type = ChannelItemType.Folder,
            };
        }

        private ChannelItemInfo CreateChannelItemInfo(SeriesStreamInfo series, Season? season, Episode episode)
        {
            Jellyfin.Xtream.Client.Models.SeriesInfo serie = series.Info;
            ParsedName parsedName = StreamService.ParseName(episode.Title);
            List<MediaSourceInfo> sources = new List<MediaSourceInfo>()
            {
                Plugin.Instance.StreamService.GetMediaSourceInfo(StreamType.Series, episode.EpisodeId, episode.ContainerExtension)
            };

            string cover = episode.Info.MovieImage;
            if (string.IsNullOrEmpty(cover) && season != null)
            {
                cover = season.Cover;
            }

            if (string.IsNullOrEmpty(cover))
            {
                cover = serie.Cover;
            }

            return new ChannelItemInfo()
            {
                ContentType = ChannelMediaContentType.Episode,
                DateCreated = DateTimeOffset.FromUnixTimeSeconds(episode.Added).DateTime,
                Genres = GetGenres(serie.Genre),
                Id = StreamService.ToGuid(StreamService.EpisodePrefix, 0, 0, episode.EpisodeId).ToString(),
                ImageUrl = cover,
                IsLiveStream = false,
                MediaSources = sources,
                MediaType = ChannelMediaType.Video,
                Name = parsedName.Title,
                Overview = episode.Info.Plot,
                People = GetPeople(serie.Cast),
                Tags = new List<string>(parsedName.Tags),
                Type = ChannelItemType.Media,
            };
        }

        private async Task<ChannelItemResult> GetCategories(CancellationToken cancellationToken)
        {
            List<ChannelItemInfo> items = new List<ChannelItemInfo>(
                (await Plugin.Instance.StreamService.GetSeriesCategories(cancellationToken).ConfigureAwait(false))
                    .Select((Category category) => StreamService.CreateChannelItemInfo(StreamService.SeriesCategoryPrefix, category)));
            return new ChannelItemResult()
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private async Task<ChannelItemResult> GetSeries(int categoryId, CancellationToken cancellationToken)
        {
            List<ChannelItemInfo> items = new List<ChannelItemInfo>(
                (await Plugin.Instance.StreamService.GetSeries(categoryId, cancellationToken).ConfigureAwait(false))
                    .Select((Series series) => CreateChannelItemInfo(series)));
            return new ChannelItemResult()
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private async Task<ChannelItemResult> GetSeasons(int seriesId, CancellationToken cancellationToken)
        {
            List<ChannelItemInfo> items = new List<ChannelItemInfo>(
                (await Plugin.Instance.StreamService.GetSeasons(seriesId, cancellationToken).ConfigureAwait(false))
                    .Select((Tuple<SeriesStreamInfo, int> tuple) => CreateChannelItemInfo(seriesId, tuple.Item1, tuple.Item2)));
            return new ChannelItemResult()
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private async Task<ChannelItemResult> GetEpisodes(int seriesId, int seasonId, CancellationToken cancellationToken)
        {
            List<ChannelItemInfo> items = new List<ChannelItemInfo>(
                (await Plugin.Instance.StreamService.GetEpisodes(seriesId, seasonId, cancellationToken).ConfigureAwait(false))
                    .Select((Tuple<SeriesStreamInfo, Season?, Episode> tuple) => CreateChannelItemInfo(tuple.Item1, tuple.Item2, tuple.Item3)));
            return new ChannelItemResult()
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
}
