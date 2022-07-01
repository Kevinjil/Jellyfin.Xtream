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
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Configuration;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service
{
    /// <summary>
    /// A service for dealing with stream information.
    /// </summary>
    public class StreamService
    {
        /// <summary>
        /// The id prefix for category channel items.
        /// </summary>
        public const string CategoryPrefix = "category-";

        /// <summary>
        /// The id prefix for stream channel items.
        /// </summary>
        public const string StreamPrefix = "stream-";

        /// <summary>
        /// The id prefix for series channel items.
        /// </summary>
        public const string SeriesPrefix = "series-";

        /// <summary>
        /// The id prefix for season channel items.
        /// </summary>
        public const string SeasonPrefix = "seasons-";

        /// <summary>
        /// The id prefix for season channel items.
        /// </summary>
        public const string EpisodePrefix = "episode-";

        private static readonly Regex TagRegex = new Regex(@"\[([^\]]+)\]|\|([^\|]+)\|");

        private readonly ILogger logger;
        private readonly Plugin plugin;

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamService"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
        /// <param name="plugin">Instance of the <see cref="Plugin"/> class.</param>
        public StreamService(ILogger logger, Plugin plugin)
        {
            this.logger = logger;
            this.plugin = plugin;
        }

        /// <summary>
        /// Parses tags in the name of a stream entry.
        /// The name commonly contains tags of the forms:
        /// <list>
        /// <item>[TAG]</item>
        /// <item>|TAG|</item>
        /// </list>
        /// These tags are parsed and returned as separate strings.
        /// The returned title is cleaned from tags and trimmed.
        /// </summary>
        /// <param name="name">The name which should be parsed.</param>
        /// <returns>A <see cref="ParsedName"/> struct containing the cleaned title and parsed tags.</returns>
        public ParsedName ParseName(string name)
        {
            List<string> tags = new List<string>();
            string title = TagRegex.Replace(
                name,
                (match) =>
                {
                    for (int i = 1; i < match.Groups.Count; ++i)
                    {
                        Group g = match.Groups[i];
                        if (g.Success)
                        {
                            tags.Add(g.Value);
                        }
                    }

                    return string.Empty;
                });

            return new ParsedName
            {
                Title = title.Trim(),
                Tags = tags.ToArray(),
            };
        }

        /// <summary>
        /// Checks if the id string is an id with the given prefix.
        /// </summary>
        /// <param name="id">The id string.</param>
        /// <param name="prefix">The prefix string.</param>
        /// <returns>Whether or not the id string has the given prefix.</returns>
        public bool IsId(string id, string prefix)
        {
            return id.StartsWith(prefix, StringComparison.InvariantCulture);
        }

        /// <summary>
        /// Parses the given id by removing the prefix.
        /// </summary>
        /// <param name="id">The id string.</param>
        /// <param name="prefix">The prefix string.</param>
        /// <returns>The parsed it as integer.</returns>
        public int ParseId(string id, string prefix)
        {
            return int.Parse(id.Substring(prefix.Length), CultureInfo.InvariantCulture);
        }

        private bool IsConfigured(SerializableDictionary<int, HashSet<int>> config, int category, int id)
        {
            return config.ContainsKey(category) && (config[category].Count == 0 || config[category].Contains(id));
        }

        /// <summary>
        /// Gets an async iterator for the configured channels.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
        public async IAsyncEnumerable<StreamInfo> GetLiveStreams([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            PluginConfiguration config = plugin.Configuration;
            using (XtreamClient client = new XtreamClient())
            {
                foreach (var entry in config.LiveTv)
                {
                    int categoryId = entry.Key;
                    HashSet<int> streams = entry.Value;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    IEnumerable<StreamInfo> channels = await client.GetLiveStreamsByCategoryAsync(plugin.Creds, categoryId, cancellationToken).ConfigureAwait(false);
                    foreach (StreamInfo channel in channels)
                    {
                        // If the set is empty, include all channels for the category.
                        if (streams.Count == 0 || streams.Contains(channel.StreamId))
                        {
                            yield return channel;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets an channel item info for the category.
        /// </summary>
        /// <param name="category">The Xtream category.</param>
        /// <returns>A channel item representing the category.</returns>
        public ChannelItemInfo CreateChannelItemInfo(Category category)
        {
            ParsedName parsedName = ParseName(category.CategoryName);
            return new ChannelItemInfo()
            {
                Id = $"{CategoryPrefix}{category.CategoryId}",
                Name = category.CategoryName,
                Tags = new List<string>(parsedName.Tags),
                Type = ChannelItemType.Folder,
            };
        }

        /// <summary>
        /// Gets an iterator for the configured VOD categories.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
        public async Task<IEnumerable<Category>> GetVodCategories(CancellationToken cancellationToken)
        {
            using (XtreamClient client = new XtreamClient())
            {
                List<Category> categories = await client.GetVodCategoryAsync(plugin.Creds, cancellationToken).ConfigureAwait(false);
                return categories.Where((Category category) => plugin.Configuration.Vod.ContainsKey(category.CategoryId));
            }
        }

        /// <summary>
        /// Gets an iterator for the configured VOD streams.
        /// </summary>
        /// <param name="categoryId">The Xtream id of the category.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
        public async Task<IEnumerable<StreamInfo>> GetVodStreams(int categoryId, CancellationToken cancellationToken)
        {
            if (!plugin.Configuration.Vod.ContainsKey(categoryId))
            {
                return new List<StreamInfo>();
            }

            using (XtreamClient client = new XtreamClient())
            {
                List<StreamInfo> streams = await client.GetVodStreamsByCategoryAsync(plugin.Creds, categoryId, cancellationToken).ConfigureAwait(false);
                return streams.Where((StreamInfo stream) => IsConfigured(plugin.Configuration.Vod, categoryId, stream.StreamId));
            }
        }

        /// <summary>
        /// Gets an iterator for the configured Series categories.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
        public async Task<IEnumerable<Category>> GetSeriesCategories(CancellationToken cancellationToken)
        {
            using (XtreamClient client = new XtreamClient())
            {
                List<Category> categories = await client.GetSeriesCategoryAsync(plugin.Creds, cancellationToken).ConfigureAwait(false);
                return categories
                    .Where((Category category) => plugin.Configuration.Series.ContainsKey(category.CategoryId));
            }
        }

        /// <summary>
        /// Gets an iterator for the configured Series.
        /// </summary>
        /// <param name="categoryId">The Xtream id of the category.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
        public async Task<IEnumerable<Series>> GetSeries(int categoryId, CancellationToken cancellationToken)
        {
            if (!plugin.Configuration.Series.ContainsKey(categoryId))
            {
                return new List<Series>();
            }

            using (XtreamClient client = new XtreamClient())
            {
                List<Series> series = await client.GetSeriesByCategoryAsync(plugin.Creds, categoryId, cancellationToken).ConfigureAwait(false);
                return series.Where((Series series) => IsConfigured(plugin.Configuration.Series, series.CategoryId, series.SeriesId));
            }
        }

        /// <summary>
        /// Gets an iterator for the configured seasons in the Series.
        /// </summary>
        /// <param name="seriesId">The Xtream id of the Series.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
        public async Task<IEnumerable<Tuple<SeriesStreamInfo, int>>> GetSeasons(int seriesId, CancellationToken cancellationToken)
        {
            using (XtreamClient client = new XtreamClient())
            {
                SeriesStreamInfo series = await client.GetSeriesStreamsBySeriesAsync(plugin.Creds, seriesId, cancellationToken).ConfigureAwait(false);
                int categoryId = series.Info.CategoryId;
                if (!IsConfigured(plugin.Configuration.Series, categoryId, seriesId))
                {
                    return new List<Tuple<SeriesStreamInfo, int>>();
                }

                SeriesInfo serie = series.Info;
                return series.Episodes.Keys.Select((int seasonId) => new Tuple<SeriesStreamInfo, int>(series, seasonId));
            }
        }

        /// <summary>
        /// Gets an iterator for the configured seasons in the Series.
        /// </summary>
        /// <param name="seriesId">The Xtream id of the Series.</param>
        /// <param name="seasonId">The Xtream id of the Season.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
        public async Task<IEnumerable<Tuple<SeriesStreamInfo, Season?, Episode>>> GetEpisodes(int seriesId, int seasonId, CancellationToken cancellationToken)
        {
            using (XtreamClient client = new XtreamClient())
            {
                SeriesStreamInfo series = await client.GetSeriesStreamsBySeriesAsync(plugin.Creds, seriesId, cancellationToken).ConfigureAwait(false);
                Season? season = series.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);
                return series.Episodes[seasonId].Select((Episode episode) => new Tuple<SeriesStreamInfo, Season?, Episode>(series, season, episode));
            }
        }

        /// <summary>
        /// Gets the media source information for the given Xtream stream.
        /// </summary>
        /// <param name="type">The stream media type.</param>
        /// <param name="id">The unique identifier of the stream.</param>
        /// <param name="extension">The container extension of the stream.</param>
        /// <param name="restream">Boolean indicating whether or not restreaming is used.</param>
        /// <param name="start">The datetime representing the start time of catcup TV.</param>
        /// <param name="durationMinutes">The duration in minutes of the catcup TV stream.</param>
        /// <returns>The media source info as <see cref="MediaSourceInfo"/> class.</returns>
        public MediaSourceInfo GetMediaSourceInfo(
            StreamType type,
            int id,
            string? extension = null,
            bool restream = false,
            DateTime? start = null,
            int durationMinutes = 0)
        {
            string prefix = string.Empty;
            switch (type)
            {
                case StreamType.Series:
                    prefix = "/series";
                    break;
                case StreamType.Vod:
                    prefix = "/movie";
                    break;
            }

            PluginConfiguration config = plugin.Configuration;
            string uri = $"{config.BaseUrl}{prefix}/{config.Username}/{config.Password}/{id}";
            if (!string.IsNullOrEmpty(extension))
            {
                uri += $".{extension}";
            }

            if (type == StreamType.CatchUp)
            {
                string? startString = start?.ToString("yyyy'-'MM'-'dd':'HH'-'mm", CultureInfo.InvariantCulture);
                uri = $"{config.BaseUrl}/streaming/timeshift.php?username={config.Username}&password={config.Password}&stream={id}&start={startString}&duration={durationMinutes}";
            }

            bool isLive = type == StreamType.Live;
            return new MediaSourceInfo()
            {
                EncoderProtocol = MediaProtocol.Http,
                Id = id.ToString(CultureInfo.InvariantCulture),
                IsInfiniteStream = isLive,
                IsRemote = true,
                Name = "default",
                Path = uri,
                Protocol = MediaProtocol.Http,
                RequiresClosing = restream,
                RequiresOpening = restream,
                SupportsDirectPlay = false,
                SupportsDirectStream = true,
                SupportsProbing = true,
            };
        }
    }
}
