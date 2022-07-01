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

using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Api.Models;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Api
{
    /// <summary>
    /// The Jellyfin Xtream configuration API.
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    [Produces(MediaTypeNames.Application.Json)]
    public class XtreamController : ControllerBase
    {
        private readonly ILogger<XtreamController> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="XtreamController"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
        public XtreamController(ILogger<XtreamController> logger)
        {
            this.logger = logger;
        }

        private static CategoryResponse CreateCategoryResponse(Category category) =>
            new CategoryResponse()
            {
                Id = category.CategoryId,
                Name = category.CategoryName,
            };

        private static ItemResponse CreateItemResponse(StreamInfo stream) =>
            new ItemResponse()
            {
                Id = stream.StreamId,
                Name = stream.Name,
                HasCatchup = stream.TvArchive,
                CatchupDuration = stream.TvArchiveDuration,
            };

        private static ItemResponse CreateItemResponse(Series series) =>
            new ItemResponse()
            {
                Id = series.SeriesId,
                Name = series.Name,
                HasCatchup = false,
                CatchupDuration = 0,
            };

        /// <summary>
        /// Get all Live TV categories.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
        /// <returns>An enumerable containing the categories.</returns>
        [Authorize(Policy = "RequiresElevation")]
        [HttpGet("LiveCategories")]
        public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetLiveCategories(CancellationToken cancellationToken)
        {
            Plugin plugin = Plugin.Instance;
            using (XtreamClient client = new XtreamClient())
            {
                List<Category> categories = await client.GetLiveCategoryAsync(plugin.Creds, cancellationToken).ConfigureAwait(false);
                return Ok(categories.Select((Category c) => CreateCategoryResponse(c)));
            }
        }

        /// <summary>
        /// Get all Live TV streams for the given category.
        /// </summary>
        /// <param name="categoryId">The category for which to fetch the streams.</param>
        /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
        /// <returns>An enumerable containing the streams.</returns>
        [Authorize(Policy = "RequiresElevation")]
        [HttpGet("LiveCategories/{categoryId}")]
        public async Task<ActionResult<IEnumerable<StreamInfo>>> GetLiveStreams(int categoryId, CancellationToken cancellationToken)
        {
            Plugin plugin = Plugin.Instance;
            using (XtreamClient client = new XtreamClient())
            {
                List<StreamInfo> streams = await client.GetLiveStreamsByCategoryAsync(
                  plugin.Creds,
                  categoryId,
                  cancellationToken).ConfigureAwait(false);
                return Ok(streams.Select((StreamInfo s) => CreateItemResponse(s)));
            }
        }

        /// <summary>
        /// Get all VOD categories.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
        /// <returns>An enumerable containing the categories.</returns>
        [Authorize(Policy = "RequiresElevation")]
        [HttpGet("VodCategories")]
        public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetVodCategories(CancellationToken cancellationToken)
        {
            Plugin plugin = Plugin.Instance;
            using (XtreamClient client = new XtreamClient())
            {
                List<Category> categories = await client.GetVodCategoryAsync(plugin.Creds, cancellationToken).ConfigureAwait(false);
                return Ok(categories.Select((Category c) => CreateCategoryResponse(c)));
            }
        }

        /// <summary>
        /// Get all VOD streams for the given category.
        /// </summary>
        /// <param name="categoryId">The category for which to fetch the streams.</param>
        /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
        /// <returns>An enumerable containing the streams.</returns>
        [Authorize(Policy = "RequiresElevation")]
        [HttpGet("VodCategories/{categoryId}")]
        public async Task<ActionResult<IEnumerable<StreamInfo>>> GetVodStreams(int categoryId, CancellationToken cancellationToken)
        {
            Plugin plugin = Plugin.Instance;
            using (XtreamClient client = new XtreamClient())
            {
                List<StreamInfo> streams = await client.GetVodStreamsByCategoryAsync(
                  plugin.Creds,
                  categoryId,
                  cancellationToken).ConfigureAwait(false);
                return Ok(streams.Select((StreamInfo s) => CreateItemResponse(s)));
            }
        }

        /// <summary>
        /// Get all Series categories.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
        /// <returns>An enumerable containing the categories.</returns>
        [Authorize(Policy = "RequiresElevation")]
        [HttpGet("SeriesCategories")]
        public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetSeriesCategories(CancellationToken cancellationToken)
        {
            Plugin plugin = Plugin.Instance;
            using (XtreamClient client = new XtreamClient())
            {
                List<Category> categories = await client.GetSeriesCategoryAsync(plugin.Creds, cancellationToken).ConfigureAwait(false);
                return Ok(categories.Select((Category c) => CreateCategoryResponse(c)));
            }
        }

        /// <summary>
        /// Get all Series streams for the given category.
        /// </summary>
        /// <param name="categoryId">The category for which to fetch the streams.</param>
        /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
        /// <returns>An enumerable containing the streams.</returns>
        [Authorize(Policy = "RequiresElevation")]
        [HttpGet("SeriesCategories/{categoryId}")]
        public async Task<ActionResult<IEnumerable<StreamInfo>>> GetSeriesStreams(int categoryId, CancellationToken cancellationToken)
        {
            Plugin plugin = Plugin.Instance;
            using (XtreamClient client = new XtreamClient())
            {
                List<Series> series = await client.GetSeriesByCategoryAsync(
                  plugin.Creds,
                  categoryId,
                  cancellationToken).ConfigureAwait(false);
                return Ok(series.Select((Series s) => CreateItemResponse(s)));
            }
        }
    }
}
