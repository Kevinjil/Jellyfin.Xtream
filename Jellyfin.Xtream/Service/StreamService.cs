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
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Configuration;
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
