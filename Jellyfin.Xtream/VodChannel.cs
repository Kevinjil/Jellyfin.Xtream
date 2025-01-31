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
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Providers;
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
public class VodChannel(ILogger<VodChannel> logger) : IChannel, IDisableMediaSourceDisplay
{
    /// <inheritdoc />
    public string? Name => "Xtream Video On-Demand";

    /// <inheritdoc />
    public string? Description => "Video On-Demand streamed from the Xtream-compatible server.";

    /// <inheritdoc />
    public string DataVersion => Plugin.Instance.DataVersion;

    /// <inheritdoc />
    public string HomePageUrl => string.Empty;

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    /// <inheritdoc />
    public InternalChannelFeatures GetChannelFeatures()
    {
        return new()
        {
            ContentTypes = [
                ChannelMediaContentType.Movie,
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
                return await GetCategories(cancellationToken).ConfigureAwait(false);
            }

            Guid guid = Guid.Parse(query.FolderId);
            StreamService.FromGuid(guid, out int prefix, out int categoryId, out int _, out int _);
            if (prefix == StreamService.VodCategoryPrefix)
            {
                return await GetStreams(categoryId, cancellationToken).ConfigureAwait(false);
            }

            return new ChannelItemResult()
            {
                TotalRecordCount = 0,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get channel items");
            throw;
        }
    }

    private Task<ChannelItemInfo> CreateChannelItemInfo(StreamInfo stream)
    {
        long added = long.Parse(stream.Added, CultureInfo.InvariantCulture);
        ParsedName parsedName = StreamService.ParseName(stream.Name);

        List<MediaSourceInfo> sources =
        [
            Plugin.Instance.StreamService.GetMediaSourceInfo(
                StreamType.Vod,
                stream.StreamId,
                stream.ContainerExtension)
        ];

        ChannelItemInfo result = new ChannelItemInfo()
        {
            ContentType = ChannelMediaContentType.Movie,
            DateCreated = DateTimeOffset.FromUnixTimeSeconds(added).DateTime,
            Id = $"{StreamService.StreamPrefix}{stream.StreamId}",
            ImageUrl = stream.StreamIcon,
            IsLiveStream = false,
            MediaSources = sources,
            MediaType = ChannelMediaType.Video,
            Name = parsedName.Title,
            Tags = new List<string>(parsedName.Tags),
            Type = ChannelItemType.Media,
            ProviderIds = { { XtreamVodProvider.ProviderName, stream.StreamId.ToString(CultureInfo.InvariantCulture) } },
        };

        return Task.FromResult(result);
    }

    private async Task<ChannelItemResult> GetCategories(CancellationToken cancellationToken)
    {
        IEnumerable<Category> categories = await Plugin.Instance.StreamService.GetVodCategories(cancellationToken).ConfigureAwait(false);
        List<ChannelItemInfo> items = new List<ChannelItemInfo>(
            categories.Select((Category category) => StreamService.CreateChannelItemInfo(StreamService.VodCategoryPrefix, category)));
        return new()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
    }

    private async Task<ChannelItemResult> GetStreams(int categoryId, CancellationToken cancellationToken)
    {
        IEnumerable<StreamInfo> streams = await Plugin.Instance.StreamService.GetVodStreams(categoryId, cancellationToken).ConfigureAwait(false);
        List<ChannelItemInfo> items = [.. await Task.WhenAll(streams.Select(CreateChannelItemInfo)).ConfigureAwait(false)];
        ChannelItemResult result = new ChannelItemResult()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
        return result;
    }

    /// <inheritdoc />
    public bool IsEnabledFor(string userId)
    {
        return Plugin.Instance.Configuration.IsVodVisible;
    }
}
