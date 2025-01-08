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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Providers;

/// <summary>
/// The Xtream Codes VOD metadata provider.
/// </summary>
/// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
public class XtreamVodProvider(ILogger<VodChannel> logger) : ICustomMetadataProvider<Movie>, IPreRefreshProvider
{
    /// <summary>
    /// The name of the provider.
    /// </summary>
    public const string ProviderName = "XtreamVodProvider";

    /// <inheritdoc/>
    public string Name => ProviderName;

    /// <inheritdoc/>
    public async Task<ItemUpdateType> FetchAsync(Movie item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        string? idStr = item.GetProviderId(ProviderName);
        if (idStr is not null)
        {
            logger.LogDebug("Getting metadata for movie {Id}", idStr);
            int id = int.Parse(idStr, CultureInfo.InvariantCulture);
            using XtreamClient client = new();
            VodStreamInfo vod = await client.GetVodInfoAsync(Plugin.Instance.Creds, id, cancellationToken).ConfigureAwait(false);
            VodInfo? i = vod.Info;

            if (i is null)
            {
                return ItemUpdateType.None;
            }

            item.Overview ??= i.Plot;
            item.PremiereDate ??= i.ReleaseDate;
            item.RunTimeTicks ??= i.DurationSecs is not null ? TimeSpan.TicksPerSecond * i.DurationSecs : null;
            item.TotalBitrate ??= i.Bitrate;

            if (i.Genre is string genres)
            {
                item.Genres ??= genres.Split(',').Select(genre => genre.Trim()).ToArray();
            }

            if (!item.HasProviderId(MetadataProvider.Tmdb))
            {
                if (i.TmdbId is int tmdbId)
                {
                    options.ReplaceAllMetadata = true;
                    item.SetProviderId(MetadataProvider.Tmdb, tmdbId.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        return ItemUpdateType.MetadataImport;
    }
}
