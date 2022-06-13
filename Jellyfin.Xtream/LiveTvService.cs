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
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream
{
    /// <summary>
    /// Class LiveTvService.
    /// </summary>
    public class LiveTvService : ILiveTvService
    {
        private readonly ILogger<LiveTvService> logger;
        private readonly IMemoryCache memoryCache;
        private int liveStreams;

        /// <summary>
        /// Initializes a new instance of the <see cref="LiveTvService"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
        /// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
        public LiveTvService(ILogger<LiveTvService> logger, IMemoryCache memoryCache)
        {
            this.logger = logger;
            this.memoryCache = memoryCache;
        }

        /// <inheritdoc />
        public string Name => "Xtream Live";

        /// <inheritdoc />
        public string HomePageUrl => string.Empty;

        /// <summary>
        /// Gets an async iterator for the configured channels.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
        private async IAsyncEnumerable<StreamInfo> GetLiveStreams([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Plugin? plugin = Plugin.Instance;
            if (plugin == null)
            {
                throw new ArgumentException("Plugin not initialized!");
            }

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

        /// <inheritdoc />
        public async Task<IEnumerable<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken)
        {
            Plugin? plugin = Plugin.Instance;
            if (plugin == null)
            {
                throw new ArgumentException("Plugin not initialized!");
            }

            PluginConfiguration config = plugin.Configuration;
            List<ChannelInfo> items = new List<ChannelInfo>();

            await foreach (StreamInfo channel in GetLiveStreams(cancellationToken))
            {
                items.Add(new ChannelInfo()
                {
                    Id = channel.StreamId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ImageUrl = channel.StreamIcon,
                    Name = channel.Name,
                });
            }

            return items;
        }

        /// <inheritdoc />
        public Task CancelTimerAsync(string timerId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task CreateTimerAsync(TimerInfo info, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<IEnumerable<TimerInfo>> GetTimersAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<IEnumerable<SeriesTimerInfo>> GetSeriesTimersAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task CreateSeriesTimerAsync(SeriesTimerInfo info, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task UpdateSeriesTimerAsync(SeriesTimerInfo info, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task UpdateTimerAsync(TimerInfo info, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task CancelSeriesTimerAsync(string timerId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task<List<MediaSourceInfo>> GetChannelStreamMediaSources(string channelId, CancellationToken cancellationToken)
        {
            MediaSourceInfo source = await GetChannelStream(channelId, string.Empty, cancellationToken).ConfigureAwait(false);
            return new List<MediaSourceInfo>() { source };
        }

        /// <inheritdoc />
        public Task<MediaSourceInfo> GetChannelStream(string channelId, string streamId, CancellationToken cancellationToken)
        {
            Plugin? plugin = Plugin.Instance;
            if (plugin == null)
            {
                throw new ArgumentException("Plugin not initialized!");
            }

            PluginConfiguration config = plugin.Configuration;
            logger.LogInformation("Start livestream {ChannelId}", channelId);
            liveStreams++;

            string uri = $"{config.BaseUrl}/{config.Username}/{config.Password}/{channelId}";
            var mediaSourceInfo = new MediaSourceInfo
            {
                Id = liveStreams.ToString(CultureInfo.InvariantCulture),
                Path = uri,
                Protocol = MediaProtocol.Http,
                RequiresOpening = true,
                MediaStreams = new List<MediaStream>
                {
                    new MediaStream
                    {
                        Type = MediaStreamType.Video,
                        IsInterlaced = true,
                        // Set the index to -1 because we don't know the exact index of the video stream within the container
                        Index = -1,
                    },
                    new MediaStream
                    {
                        Type = MediaStreamType.Audio,
                        // Set the index to -1 because we don't know the exact index of the audio stream within the container
                        Index = -1
                    }
                },
                Container = "mpegts",
                SupportsProbing = true
            };

            return Task.FromResult(mediaSourceInfo);
        }

        /// <inheritdoc />
        public Task CloseLiveStream(string id, CancellationToken cancellationToken)
        {
            logger.LogInformation("Closing livestream {ChannelId}", id);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<SeriesTimerInfo> GetNewTimerDefaultsAsync(CancellationToken cancellationToken, ProgramInfo? program = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task<IEnumerable<ProgramInfo>> GetProgramsAsync(string channelId, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task RecordLiveStream(string id, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Task ResetTuner(string id, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
