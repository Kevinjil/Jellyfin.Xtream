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
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client.Models;
using Newtonsoft.Json;

#pragma warning disable CS1591
namespace Jellyfin.Xtream.Client
{
    /// <summary>
    /// The Xtream API client implementation.
    /// </summary>
    public class XtreamClient : IDisposable
    {
        private readonly HttpClient _client;

        /// <summary>
        /// Initializes a new instance of the <see cref="XtreamClient"/> class.
        /// </summary>
        public XtreamClient() : this(new HttpClient())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XtreamClient"/> class.
        /// </summary>
        /// <param name="client">The HTTP client used.</param>
        public XtreamClient(HttpClient client)
        {
            _client = client;
        }

        private async Task<T> QueryApi<T>(ConnectionInfo connectionInfo, string urlPath, CancellationToken cancellationToken)
        {
          Uri uri = new Uri(connectionInfo.BaseUrl + urlPath);
          string jsonContent = await _client.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
          return JsonConvert.DeserializeObject<T>(jsonContent)!;
        }

        public Task<PlayerApi> GetUserAndServerInfoAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken) =>
            QueryApi<PlayerApi>(
              connectionInfo,
              $"/player_api.php?username={connectionInfo.UserName}&password={connectionInfo.Password}",
              cancellationToken);

        public Task<List<Series>> GetSeriesByCategoryAsync(ConnectionInfo connectionInfo, string categoryId, CancellationToken cancellationToken) =>
             QueryApi<List<Series>>(
               connectionInfo,
               $"/player_api.php?username={connectionInfo.UserName}&password={connectionInfo.Password}&action=get_series&category_id={categoryId}",
               cancellationToken);

        public Task<SeriesStreamInfo> GetSeriesStreamsBySeriesAsync(ConnectionInfo connectionInfo, string seriesId, CancellationToken cancellationToken) =>
             QueryApi<SeriesStreamInfo>(
               connectionInfo,
               $"/player_api.php?username={connectionInfo.UserName}&password={connectionInfo.Password}&action=get_series_info&series_id={seriesId}",
               cancellationToken);

        public Task<List<StreamInfo>> GetVodStreamsByCategoryAsync(ConnectionInfo connectionInfo, string categoryId, CancellationToken cancellationToken) =>
             QueryApi<List<StreamInfo>>(
               connectionInfo,
               $"/player_api.php?username={connectionInfo.UserName}&password={connectionInfo.Password}&action=get_vod_streams&category_id={categoryId}",
               cancellationToken);

        public Task<List<StreamInfo>> GetLiveStreamsByCategoryAsync(ConnectionInfo connectionInfo, string categoryId, CancellationToken cancellationToken) =>
             QueryApi<List<StreamInfo>>(
               connectionInfo,
               $"/player_api.php?username={connectionInfo.UserName}&password={connectionInfo.Password}&action=get_live_streams&category_id={categoryId}",
               cancellationToken);

        public Task<List<Category>> GetSeriesCategoryAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken) =>
             QueryApi<List<Category>>(
               connectionInfo,
               $"/player_api.php?username={connectionInfo.UserName}&password={connectionInfo.Password}&action=get_series_categories",
               cancellationToken);

        public Task<List<Category>> GetVodCategoryAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken) =>
             QueryApi<List<Category>>(
               connectionInfo,
               $"/player_api.php?username={connectionInfo.UserName}&password={connectionInfo.Password}&action=get_vod_categories",
               cancellationToken);

        public Task<List<Category>> GetLiveCategoryAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken) =>
             QueryApi<List<Category>>(
               connectionInfo,
               $"/player_api.php?username={connectionInfo.UserName}&password={connectionInfo.Password}&action=get_live_categories",
               cancellationToken);

        /// <summary>
        /// Dispose the HTTP client.
        /// </summary>
        /// <param name="b">Unused.</param>
        protected virtual void Dispose(bool b)
        {
            _client?.Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
          Dispose(true);
          GC.SuppressFinalize(this);
        }
    }
}
