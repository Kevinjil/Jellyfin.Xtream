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
using MediaBrowser.Model.Channels;
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
            return id.StartsWith(StreamService.CategoryPrefix, StringComparison.InvariantCulture);
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
                    string categoryId = entry.Key.ToString(CultureInfo.InvariantCulture);
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

        private ChannelItemInfo CreateChannelItemInfo(Category category)
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

        private ChannelItemInfo CreateChannelItemInfo(StreamInfo stream, ChannelMediaContentType type)
        {
            string id = stream.StreamId.ToString(CultureInfo.InvariantCulture);
            long added = long.Parse(stream.Added, CultureInfo.InvariantCulture);
            ParsedName parsedName = ParseName(stream.Name);
            List<MediaSourceInfo> sources = new List<MediaSourceInfo>()
            {
                GetMediaSourceInfo(StreamType.Vod, id, stream.ContainerExtension)
            };

            return new ChannelItemInfo()
            {
                ContentType = type,
                DateCreated = DateTimeOffset.FromUnixTimeSeconds(added).DateTime,
                FolderType = ChannelFolderType.Container,
                Id = id,
                ImageUrl = stream.StreamIcon,
                IsLiveStream = false,
                MediaSources = sources,
                MediaType = ChannelMediaType.Video,
                Name = parsedName.Title,
                Tags = new List<string>(parsedName.Tags),
                Type = ChannelItemType.Media,
            };
        }

        /// <summary>
        /// Gets an iterator for the configured VOD categories.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
        public async Task<IEnumerable<ChannelItemInfo>> GetVodCategories(CancellationToken cancellationToken)
        {
            using (XtreamClient client = new XtreamClient())
            {
                List<Category> categories = await client.GetVodCategoryAsync(plugin.Creds, cancellationToken).ConfigureAwait(false);
                return categories
                    .Where((Category category) => plugin.Configuration.Vod.ContainsKey(category.CategoryId))
                    .Select((Category category) => CreateChannelItemInfo(category));
            }
        }

        /// <summary>
        /// Gets an iterator for the configured VOD categories.
        /// </summary>
        /// <param name="categoryId">The Xtream id of category.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
        public async Task<IEnumerable<ChannelItemInfo>> GetVodStreams(int categoryId, CancellationToken cancellationToken)
        {
            if (!plugin.Configuration.Vod.ContainsKey(categoryId))
            {
                return new List<ChannelItemInfo>();
            }

            using (XtreamClient client = new XtreamClient())
            {
                List<StreamInfo> streams = await client.GetVodStreamsByCategoryAsync(plugin.Creds, categoryId, cancellationToken).ConfigureAwait(false);
                return streams
                    .Where((StreamInfo stream) => plugin.Configuration.Vod.ContainsKey(stream.CategoryId))
                    .Select((StreamInfo stream) => CreateChannelItemInfo(stream, ChannelMediaContentType.Movie));
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
            string id,
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
                Id = id,
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
