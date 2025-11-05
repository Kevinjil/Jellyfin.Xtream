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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Service;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream;

/// <summary>
/// Class LiveTvService.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="LiveTvService"/> class.
/// </remarks>
/// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
/// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
/// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
/// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
/// <param name="xtreamClient">Instance of the <see cref="IXtreamClient"/> interface.</param>
public class LiveTvService(IServerApplicationHost appHost, IHttpClientFactory httpClientFactory, ILogger<LiveTvService> logger, IMemoryCache memoryCache, IXtreamClient xtreamClient) : ILiveTvService, ISupportsDirectStreamProvider
{
    /// <inheritdoc />
    public string Name => "Xtream Live";

    /// <inheritdoc />
    public string HomePageUrl => string.Empty;

    /// <inheritdoc />
    public async Task<IEnumerable<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken)
    {
        Plugin plugin = Plugin.Instance;
        List<ChannelInfo> items = new List<ChannelInfo>();
        foreach (StreamInfo channel in await plugin.StreamService.GetLiveStreamsWithOverrides(cancellationToken).ConfigureAwait(false))
        {
            ParsedName parsed = StreamService.ParseName(channel.Name);
            items.Add(new ChannelInfo()
            {
                Id = StreamService.ToGuid(StreamService.LiveTvPrefix, channel.StreamId, 0, 0).ToString(),
                Number = channel.Num.ToString(CultureInfo.InvariantCulture),
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
        return new List<MediaSourceInfo> { source };
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
        Guid guid = Guid.Parse(channelId);
        StreamService.FromGuid(guid, out int prefix, out int streamId, out int _, out int _);
        if (prefix != StreamService.LiveTvPrefix)
        {
            throw new ArgumentException("Unsupported channel");
        }

        string key = $"xtream-epg-{channelId}";
        ICollection<ProgramInfo>? items = null;
        if (memoryCache.TryGetValue(key, out ICollection<ProgramInfo>? o))
        {
            items = o;
        }
        else
        {
            items = new List<ProgramInfo>();
            Plugin plugin = Plugin.Instance;
            logger.LogInformation(
                "GetProgramsAsync for channel {ChannelId}, streamId {StreamId}. UseXmlTv: {UseXmlTv}, XmlTvUrl: '{XmlTvUrl}'",
                channelId,
                streamId,
                plugin.Configuration.UseXmlTv,
                plugin.Configuration.XmlTvUrl ?? "(default)");

            if (plugin.Configuration.UseXmlTv)
            {
                logger.LogInformation("Using XMLTV for EPG data (streamId: {StreamId})", streamId);

                // Cache the live streams list to avoid repeated API calls
                string streamsCacheKey = $"xtream-liveStreams-{plugin.DataVersion}";
                if (!memoryCache.TryGetValue(streamsCacheKey, out IEnumerable<StreamInfo>? allStreams))
                {
                    allStreams = await plugin.StreamService.GetLiveStreams(cancellationToken).ConfigureAwait(false);
                    memoryCache.Set(streamsCacheKey, allStreams, TimeSpan.FromMinutes(plugin.Configuration.XmlTvCacheMinutes));
                }

                // Try to find the stream to get the EPG channel id
                StreamInfo? streamInfo = allStreams?.FirstOrDefault(s => s.StreamId == streamId);
                if (streamInfo != null && allStreams != null)
                {
                    // Build channel mapping
                    var channelMapping = XmlTvValidation.BuildChannelMapping(allStreams);

                    string xmlCacheKey = $"xtream-xmltv-{plugin.DataVersion}";
                    if (!memoryCache.TryGetValue(xmlCacheKey, out Dictionary<string, List<(DateTime Start, DateTime End, string Title, string Description)>>? mapping))
                    {
                        mapping = new Dictionary<string, List<(DateTime Start, DateTime End, string Title, string Description)>>();
                        try
                        {
                            string xml;
                            string diskCachePath = XmlTvValidation.GetCachePath(plugin.Configuration.XmlTvCachePath);

                            // Try disk cache first if enabled
                            if (plugin.Configuration.XmlTvDiskCache && File.Exists(diskCachePath))
                            {
                                xml = await File.ReadAllTextAsync(diskCachePath, cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                xml = await xtreamClient.GetXmlTvAsync(plugin.Creds, plugin.Configuration.XmlTvUrl, cancellationToken).ConfigureAwait(false);

                                // Write to disk cache if enabled
                                if (plugin.Configuration.XmlTvDiskCache)
                                {
                                    await File.WriteAllTextAsync(diskCachePath, xml, cancellationToken).ConfigureAwait(false);
                                }
                            }

                            // Validate XMLTV content
                            int requiredDays = plugin.Configuration.XmlTvHistoricalDays;
                            if (requiredDays <= 0)
                            {
                                requiredDays = allStreams.Where(s => s.TvArchive).Select(s => s.TvArchiveDuration).DefaultIfEmpty(7).Max();
                            }

                            if (!XmlTvValidation.ValidateXmlTv(xml, requiredDays, logger))
                            {
                                throw new InvalidDataException("XMLTV content validation failed - check logs for details");
                            }

                            var doc = XDocument.Parse(xml);
                            foreach (var prog in doc.Descendants("programme"))
                            {
                                string? ch = prog.Attribute("channel")?.Value ?? string.Empty;
                                string? startRaw = prog.Attribute("start")?.Value ?? string.Empty;
                                string? stopRaw = prog.Attribute("stop")?.Value ?? string.Empty;

                                DateTime start;
                                DateTime stop;

                                // Local helper to parse XMLTV date/time formats used by providers (handles timezone like +0000 -> +00:00)
                                static DateTime ParseXmlTvDate(string raw)
                                {
                                    if (string.IsNullOrWhiteSpace(raw))
                                    {
                                        return DateTime.MinValue;
                                    }

                                    string s = raw.Replace(" ", string.Empty, StringComparison.Ordinal);
                                    // If timezone is in form +0000 convert to +00:00 for parsing with 'zzz'
                                    if (s.Length > 14)
                                    {
                                        string zone = s[^5..];
                                        if ((zone[0] == '+' || zone[0] == '-') && int.TryParse(zone[1..], out _))
                                        {
                                            string zoneWithColon = zone.Insert(3, ":");
                                            s = s[..^5] + zoneWithColon;
                                        }
                                    }

                                    if (DateTime.TryParseExact(s, "yyyyMMddHHmmsszzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                                    {
                                        return dt.ToUniversalTime();
                                    }

                                    // Fallback
                                    return DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                                }

                                try
                                {
                                    start = ParseXmlTvDate(startRaw);
                                }
                                catch
                                {
                                    start = DateTime.MinValue;
                                }

                                try
                                {
                                    stop = ParseXmlTvDate(stopRaw);
                                }
                                catch
                                {
                                    stop = DateTime.MinValue;
                                }

                                string title = prog.Elements("title").FirstOrDefault()?.Value ?? string.Empty;
                                string desc = prog.Elements("desc").FirstOrDefault()?.Value ?? string.Empty;

                                if (!mapping.TryGetValue(ch, out var list))
                                {
                                    list = new List<(DateTime Start, DateTime End, string Title, string Description)>();
                                    mapping[ch] = list;
                                }

                                list.Add((Start: start, End: stop, Title: title, Description: desc));
                            }

                            memoryCache.Set(xmlCacheKey, mapping, DateTimeOffset.Now.AddMinutes(plugin.Configuration.XmlTvCacheMinutes));
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to download or parse XMLTV feed");
                            mapping = new Dictionary<string, List<(DateTime, DateTime, string, string)>>();
                        }
                    }

                    // Find all possible EPG keys for this stream
                    foreach (var kvp in channelMapping)
                    {
                        if (kvp.Value != null && mapping != null &&
                            kvp.Value.Contains(streamId) &&
                            mapping.TryGetValue(kvp.Key, out var progsForChannel) &&
                            progsForChannel != null)
                        {
                            int localId = 1;
                            foreach (var p in progsForChannel)
                            {
                                items.Add(new()
                                {
                                    Id = StreamService.ToGuid(StreamService.EpgPrefix, streamId, localId++, 0).ToString(),
                                    ChannelId = channelId,
                                    StartDate = p.Start,
                                    EndDate = p.End,
                                    Name = p.Title,
                                    Overview = p.Description,
                                });
                            }
                        }
                    }
                }
            }
            else if (!plugin.Configuration.UseXmlTv)
            {
                logger.LogInformation("Using per-channel EPG API call for streamId: {StreamId}", streamId);
                EpgListings epgs = await xtreamClient.GetEpgInfoAsync(plugin.Creds, streamId, cancellationToken).ConfigureAwait(false);
                foreach (EpgInfo epg in epgs.Listings)
                {
                    items.Add(new()
                    {
                        Id = StreamService.ToGuid(StreamService.EpgPrefix, streamId, epg.Id, 0).ToString(),
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
    public Task ResetTuner(string id, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<ILiveStream> GetChannelStreamWithDirectStreamProvider(string channelId, string streamId, List<ILiveStream> currentLiveStreams, CancellationToken cancellationToken)
    {
        Guid guid = Guid.Parse(channelId);
        StreamService.FromGuid(guid, out int prefix, out int channel, out int _, out int _);
        if (prefix != StreamService.LiveTvPrefix)
        {
            throw new ArgumentException("Unsupported channel");
        }

        Plugin plugin = Plugin.Instance;
        MediaSourceInfo mediaSourceInfo = plugin.StreamService.GetMediaSourceInfo(StreamType.Live, channel, restream: true);
        ILiveStream? stream = currentLiveStreams.Find(stream => stream.TunerHostId == Restream.TunerHost && stream.MediaSource.Id == mediaSourceInfo.Id);

        if (stream == null)
        {
            stream = new Restream(appHost, httpClientFactory, logger, mediaSourceInfo);
            await stream.Open(cancellationToken).ConfigureAwait(false);
        }

        stream.ConsumerCount++;
        return stream;
    }
}
