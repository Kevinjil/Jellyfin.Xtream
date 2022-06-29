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

namespace Jellyfin.Xtream
{
    /// <summary>
    /// The Xtream Codes API channel.
    /// </summary>
    public class VodChannel : IChannel
    {
        private readonly ILogger<VodChannel> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="VodChannel"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
        public VodChannel(ILogger<VodChannel> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc />
        public string? Name => "Xtream Video On-Demand";

        /// <inheritdoc />
        public string? Description => "Video On-Demand streamed from the Xtream-compatible server.";

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
                    ChannelMediaContentType.Movie,
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
            Plugin plugin = Plugin.Instance;
            if (string.IsNullOrEmpty(query.FolderId))
            {
                return await GetCategories(cancellationToken).ConfigureAwait(false);
            }

            if (plugin.StreamService.IsId(query.FolderId, StreamService.CategoryPrefix))
            {
                int categoryId = plugin.StreamService.ParseId(query.FolderId, StreamService.CategoryPrefix);
                return await GetStreams(categoryId, cancellationToken).ConfigureAwait(false);
            }

            return new ChannelItemResult()
            {
                TotalRecordCount = 0,
            };
        }

        private async Task<ChannelItemResult> GetCategories(CancellationToken cancellationToken)
        {
            List<ChannelItemInfo> items = new List<ChannelItemInfo>(
                await Plugin.Instance.StreamService.GetVodCategories(cancellationToken).ConfigureAwait(false));
            return new ChannelItemResult()
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }

        private async Task<ChannelItemResult> GetStreams(int categoryId, CancellationToken cancellationToken)
        {
            List<ChannelItemInfo> items = new List<ChannelItemInfo>(
                await Plugin.Instance.StreamService.GetVodStreams(categoryId, cancellationToken).ConfigureAwait(false));
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
            return true;
        }
    }
}
