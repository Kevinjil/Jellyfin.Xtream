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
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Service;
using MediaBrowser.Controller.Channels;
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
public class CatchupChannel(ILogger<CatchupChannel> logger) : IChannel, IDisableMediaSourceDisplay
{
    private readonly ILogger<CatchupChannel> _logger = logger;

    /// <inheritdoc />
    public string? Name => "Xtream Catch-up";

    /// <inheritdoc />
    public string? Description => "Rewatch IPTV streamed from the Xtream-compatible server.";

    /// <inheritdoc />
    public string DataVersion => Plugin.Instance.DataVersion + DateTime.Today.ToShortDateString();

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
                ChannelMediaContentType.TvExtra,
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
    public IEnumerable<ImageType> GetSupportedChannelImages() => new List<ImageType>
    {
        // ImageType.Primary
    };

    /// <inheritdoc />
    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(query.FolderId))
            {
                return await GetChannels(cancellationToken).ConfigureAwait(false);
            }

            Guid guid = Guid.Parse(query.FolderId);
            StreamService.FromGuid(guid, out int prefix, out int categoryId, out int channelId, out int date);

            if (date == 0)
            {
                return await GetDays(categoryId, channelId, cancellationToken).ConfigureAwait(false);
            }

            return await GetStreams(categoryId, channelId, date, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get channel items");
            throw;
        }
    }

    private async Task<ChannelItemResult> GetChannels(CancellationToken cancellationToken)
    {
        Plugin plugin = Plugin.Instance;
        List<ChannelItemInfo> items = [];
        foreach (StreamInfo channel in await plugin.StreamService.GetLiveStreamsWithOverrides(cancellationToken).ConfigureAwait(false))
        {
            if (!channel.TvArchive)
            {
                // Channel has no catch-up support.
                continue;
            }

            ParsedName parsedName = StreamService.ParseName(channel.Name);
            items.Add(new ChannelItemInfo()
            {
                Id = StreamService.ToGuid(StreamService.CatchupPrefix, channel.CategoryId ?? 0, channel.StreamId, 0).ToString(),
                ImageUrl = channel.StreamIcon,
                Name = parsedName.Title,
                Tags = new List<string>(parsedName.Tags),
                Type = ChannelItemType.Folder,
            });
        }

        ChannelItemResult result = new ChannelItemResult()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
        return result;
    }

    private async Task<ChannelItemResult> GetDays(int categoryId, int channelId, CancellationToken cancellationToken)
    {
        Plugin plugin = Plugin.Instance;
        using XtreamClient client = new XtreamClient();

        List<StreamInfo> streams = await client.GetLiveStreamsByCategoryAsync(plugin.Creds, categoryId, cancellationToken).ConfigureAwait(false);
        StreamInfo channel = streams.FirstOrDefault(s => s.StreamId == channelId)
            ?? throw new ArgumentException($"Channel with id {channelId} not found in category {categoryId}");
        ParsedName parsedName = StreamService.ParseName(channel.Name);

        List<ChannelItemInfo> items = [];
        for (int i = 0; i <= channel.TvArchiveDuration; i++)
        {
            DateTime channelDay = DateTime.Today.AddDays(-i);
            int day = (int)(channelDay - DateTime.UnixEpoch).TotalDays;
            items.Add(new()
            {
                Id = StreamService.ToGuid(StreamService.CatchupPrefix, channel.CategoryId ?? 0, channel.StreamId, day).ToString(),
                ImageUrl = channel.StreamIcon,
                Name = channelDay.ToLocalTime().ToString("ddd dd'-'MM'-'yyyy", CultureInfo.InvariantCulture),
                Tags = new List<string>(parsedName.Tags),
                Type = ChannelItemType.Folder,
            });
        }

        ChannelItemResult result = new()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
        return result;
    }

    private async Task<ChannelItemResult> GetStreams(int categoryId, int channelId, int day, CancellationToken cancellationToken)
    {
        DateTime start = DateTime.UnixEpoch.AddDays(day);
        DateTime end = start.AddDays(1);
        Plugin plugin = Plugin.Instance;
        using XtreamClient client = new XtreamClient();

        List<StreamInfo> streams = await client.GetLiveStreamsByCategoryAsync(plugin.Creds, categoryId, cancellationToken).ConfigureAwait(false);
        StreamInfo channel = streams.FirstOrDefault(s => s.StreamId == channelId)
            ?? throw new ArgumentException($"Channel with id {channelId} not found in category {categoryId}");
        EpgListings epgs = await client.GetEpgInfoAsync(plugin.Creds, channelId, cancellationToken).ConfigureAwait(false);
        List<ChannelItemInfo> items = [];

        // Create fallback single-stream catch-up if no EPG is available.
        if (epgs.Listings.Count == 0)
        {
            int durationMinutes = 24 * 60;
            return new()
            {
                Items = new List<ChannelItemInfo>()
                    {
                        new()
                        {
                            ContentType = ChannelMediaContentType.TvExtra,
                            Id = StreamService.ToGuid(StreamService.CatchupStreamPrefix, channelId, 0, day).ToString(),
                            IsLiveStream = false,
                            MediaSources = [
                                plugin.StreamService.GetMediaSourceInfo(StreamType.CatchUp, channelId, start: start, durationMinutes: durationMinutes)
                            ],
                            MediaType = ChannelMediaType.Video,
                            Name = $"No EPG available",
                            RunTimeTicks = durationMinutes * TimeSpan.TicksPerMinute,
                            Type = ChannelItemType.Media,
                        }
                    },
                TotalRecordCount = 1
            };
        }

        foreach (EpgInfo epg in epgs.Listings.Where(epg => epg.Start <= end && epg.End >= start))
        {
            ParsedName parsedName = StreamService.ParseName(epg.Title);
            int durationMinutes = (int)Math.Ceiling((epg.End - epg.Start).TotalMinutes);
            string dateTitle = epg.Start.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
            List<MediaSourceInfo> sources = [
                plugin.StreamService.GetMediaSourceInfo(StreamType.CatchUp, channelId, start: epg.StartLocalTime, durationMinutes: durationMinutes)
            ];

            items.Add(new()
            {
                ContentType = ChannelMediaContentType.TvExtra,
                DateCreated = epg.Start,
                Id = StreamService.ToGuid(StreamService.CatchupStreamPrefix, channel.StreamId, epg.Id, day).ToString(),
                IsLiveStream = false,
                MediaSources = sources,
                MediaType = ChannelMediaType.Video,
                Name = $"{dateTitle} - {parsedName.Title}",
                Overview = epg.Description,
                PremiereDate = epg.Start,
                RunTimeTicks = durationMinutes * TimeSpan.TicksPerMinute,
                Tags = new List<string>(parsedName.Tags),
                Type = ChannelItemType.Media,
            });
        }

        ChannelItemResult result = new()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
        return result;
    }

    /// <inheritdoc />
    public bool IsEnabledFor(string userId)
    {
        return Plugin.Instance.Configuration.IsCatchupVisible;
    }
}
