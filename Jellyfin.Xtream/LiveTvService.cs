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
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Configuration;
using Jellyfin.Xtream.Service;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream
{
    /// <summary>
    /// Class LiveTvService.
    /// </summary>
    public class LiveTvService : ILiveTvService, ISupportsDirectStreamProvider
    {
        private readonly IServerApplicationHost appHost;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger<LiveTvService> logger;
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="LiveTvService"/> class.
        /// </summary>
        /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
        /// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
        public LiveTvService(IServerApplicationHost appHost, IHttpClientFactory httpClientFactory, ILogger<LiveTvService> logger, IMemoryCache memoryCache)
        {
            this.appHost = appHost;
            this.httpClientFactory = httpClientFactory;
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
            Plugin plugin = Plugin.Instance;
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
            Plugin plugin = Plugin.Instance;
            List<ChannelInfo> items = new List<ChannelInfo>();
            await foreach (StreamInfo channel in GetLiveStreams(cancellationToken))
            {
                ParsedName parsed = plugin.StreamService.ParseName(channel.Name);
                items.Add(new ChannelInfo()
                {
                    Id = channel.StreamId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ImageUrl = channel.StreamIcon,
                    Name = parsed.Title,
                    Tags = parsed.Tags,
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
            return Task.FromResult<IEnumerable<TimerInfo>>(new List<TimerInfo>());
        }

        /// <inheritdoc />
        public Task<IEnumerable<SeriesTimerInfo>> GetSeriesTimersAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<SeriesTimerInfo>>(new List<SeriesTimerInfo>());
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
        public Task UpdateTimerAsync(TimerInfo updatedTimer, CancellationToken cancellationToken)
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
            throw new NotImplementedException();
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
            return Task.FromResult(new SeriesTimerInfo
            {
                PostPaddingSeconds = 120,
                PrePaddingSeconds = 120,
                RecordAnyChannel = false,
                RecordAnyTime = true,
                RecordNewOnly = false
            });
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ProgramInfo>> GetProgramsAsync(string channelId, DateTime startDateUtc, DateTime endDateUtc, CancellationToken cancellationToken)
        {
            string key = $"xtream-epg-{channelId}";
            ICollection<ProgramInfo>? items = null;
            if (memoryCache.TryGetValue(key, out ICollection<ProgramInfo> o))
            {
                items = o;
            }
            else
            {
                items = new List<ProgramInfo>();
                Plugin plugin = Plugin.Instance;
                using (XtreamClient client = new XtreamClient())
                {
                    int streamId = int.Parse(channelId, CultureInfo.InvariantCulture);
                    EpgListings epgs = await client.GetEpgInfoAsync(plugin.Creds, streamId, cancellationToken).ConfigureAwait(false);
                    foreach (EpgInfo epg in epgs.Listings)
                    {
                        items.Add(new ProgramInfo()
                        {
                            Id = $"epg-{epg.Id}",
                            ChannelId = channelId,
                            StartDate = epg.Start,
                            EndDate = epg.End,
                            Name = epg.Title,
                            Overview = epg.Description,
                        });
                    }
                }

                memoryCache.Set(key, items, DateTimeOffset.Now.AddMinutes(10));
            }

            return from epg in items
                   where epg.EndDate >= startDateUtc && epg.StartDate < endDateUtc
                   select epg;
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

        /// <inheritdoc />
        public Task<ILiveStream> GetChannelStreamWithDirectStreamProvider(string channelId, string streamId, List<ILiveStream> currentLiveStreams, CancellationToken cancellationToken)
        {
            ILiveStream? stream = currentLiveStreams.Find(stream => stream.TunerHostId == Restream.TunerHost && stream.MediaSource.Id == channelId);
            if (stream != null)
            {
                return Task.FromResult(stream);
            }

            Plugin plugin = Plugin.Instance;
            MediaSourceInfo mediaSourceInfo = plugin.StreamService.GetMediaSourceInfo(StreamType.Live, channelId, null, true);
            stream = new Restream(appHost, httpClientFactory, logger, mediaSourceInfo);
            return Task.FromResult(stream);
        }
    }
}
