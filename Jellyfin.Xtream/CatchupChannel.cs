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
public class CatchupChannel : IChannel
{
    private readonly ILogger<CatchupChannel> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatchupChannel"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
    public CatchupChannel(ILogger<CatchupChannel> logger)
    {
        this.logger = logger;
    }

    /// <inheritdoc />
    public string? Name => "Xtream Catch-up";

    /// <inheritdoc />
    public string? Description => "Rewatch IPTV streamed from the Xtream-compatible server.";

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
            ContentTypes = new List<ChannelMediaContentType>
            {
                ChannelMediaContentType.TvExtra,
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
        try
        {
            if (string.IsNullOrEmpty(query.FolderId))
            {
                return await GetChannels(cancellationToken).ConfigureAwait(false);
            }

            Guid guid = Guid.Parse(query.FolderId);
            StreamService.FromGuid(guid, out int prefix, out int categoryId, out int channelId, out int _);
            return await GetStreams(categoryId, channelId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get channel items");
            throw;
        }
    }

    private async Task<ChannelItemResult> GetChannels(CancellationToken cancellationToken)
    {
        Plugin plugin = Plugin.Instance;
        List<ChannelItemInfo> items = new List<ChannelItemInfo>();
        await foreach (StreamInfo channel in plugin.StreamService.GetLiveStreamsWithOverrides(cancellationToken))
        {
            if (!channel.TvArchive)
            {
                // Channel has no catch-up support.
                continue;
            }

            ParsedName parsedName = StreamService.ParseName(channel.Name);
            items.Add(new ChannelItemInfo()
            {
                Id = StreamService.ToGuid(StreamService.CatchupPrefix, channel.CategoryId, channel.StreamId, 0).ToString(),
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

    private async Task<ChannelItemResult> GetStreams(int categoryId, int channelId, CancellationToken cancellationToken)
    {
        Plugin plugin = Plugin.Instance;
        using (XtreamClient client = new XtreamClient())
        {
            StreamInfo? channel = (
                await client.GetLiveStreamsByCategoryAsync(plugin.Creds, categoryId, cancellationToken).ConfigureAwait(false)
            ).FirstOrDefault(s => s.StreamId == channelId);
            if (channel == null)
            {
                throw new ArgumentException($"Channel with id {channelId} not found in category {categoryId}");
            }

            EpgListings epgs = await client.GetEpgInfoAsync(plugin.Creds, channelId, cancellationToken).ConfigureAwait(false);
            List<ChannelItemInfo> items = new List<ChannelItemInfo>();

            // Create fallback single-stream catch-up if no EPG is available.
            if (epgs.Listings.Count == 0)
            {
                DateTime now = DateTime.UtcNow;
                DateTime start = now.AddDays(-channel.TvArchiveDuration);
                int duration = channel.TvArchiveDuration * 24 * 60;
                return new ChannelItemResult()
                {
                    Items = new List<ChannelItemInfo>()
                    {
                        new ChannelItemInfo()
                        {
                            ContentType = ChannelMediaContentType.TvExtra,
                            FolderType = ChannelFolderType.Container,
                            Id = StreamService.ToGuid(StreamService.FallbackPrefix, channelId, 0, 0).ToString(),
                            IsLiveStream = false,
                            MediaSources = new List<MediaSourceInfo>()
                            {
                                plugin.StreamService.GetMediaSourceInfo(StreamType.CatchUp, channelId, start: start, durationMinutes: duration)
                            },
                            MediaType = ChannelMediaType.Video,
                            Name = $"No EPG available",
                            Type = ChannelItemType.Media,
                        }
                    },
                    TotalRecordCount = items.Count
                };
            }

            // Include all EPGs that start during the maximum cache interval of Jellyfin for channels.
            DateTime startBefore = DateTime.UtcNow.AddHours(3);
            DateTime startAfter = DateTime.UtcNow.AddDays(-channel.TvArchiveDuration);
            foreach (EpgInfo epg in epgs.Listings.Where(epg => epg.Start < startBefore && epg.Start >= startAfter))
            {
                string id = epg.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                ParsedName parsedName = StreamService.ParseName(epg.Title);
                int durationMinutes = (int)Math.Ceiling((epg.End - epg.Start).TotalMinutes);
                string dateTitle = epg.Start.ToLocalTime().ToString("ddd HH:mm", CultureInfo.InvariantCulture);
                List<MediaSourceInfo> sources = new List<MediaSourceInfo>()
                {
                    plugin.StreamService.GetMediaSourceInfo(StreamType.CatchUp, channelId, start: epg.StartLocalTime, durationMinutes: durationMinutes)
                };

                items.Add(new ChannelItemInfo()
                {
                    ContentType = ChannelMediaContentType.TvExtra,
                    DateCreated = epg.Start,
                    FolderType = ChannelFolderType.Container,
                    Id = id,
                    IsLiveStream = false,
                    MediaSources = sources,
                    MediaType = ChannelMediaType.Video,
                    Name = $"{dateTitle} - {parsedName.Title}",
                    Overview = epg.Description,
                    PremiereDate = epg.Start,
                    Tags = new List<string>(parsedName.Tags),
                    Type = ChannelItemType.Media,
                });
            }

            ChannelItemResult result = new ChannelItemResult()
            {
                Items = items,
                TotalRecordCount = items.Count
            };
            return result;
        }
    }

    /// <inheritdoc />
    public bool IsEnabledFor(string userId)
    {
        return Plugin.Instance.Configuration.IsCatchupVisible;
    }
}
