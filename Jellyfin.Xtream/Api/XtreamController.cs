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
using System.Globalization;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary>
        /// Get all Live TV categories.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
        /// <returns>A collection containing the categories.</returns>
        [Authorize(Policy = "RequiresElevation")]
        [HttpGet("LiveCategories")]
        public async Task<ActionResult<ICollection<Category>>> GetLiveCategories(CancellationToken cancellationToken)
        {
            Plugin plugin = Plugin.Instance;
            using (XtreamClient client = new XtreamClient())
            {
                List<Category> categories = await client.GetLiveCategoryAsync(plugin.Creds, cancellationToken).ConfigureAwait(false);
                return Ok(categories);
            }
        }

        /// <summary>
        /// Get all Live TV streams for the given category.
        /// </summary>
        /// <param name="categoryId">The category for which to fetch the streams.</param>
        /// <param name="cancellationToken">The cancellation token for cancelling requests.</param>
        /// <returns>A collection containing the streams.</returns>
        [Authorize(Policy = "RequiresElevation")]
        [HttpGet("LiveStreams/{categoryId}")]
        public async Task<ActionResult<ICollection<StreamInfo>>> GetLiveStreams(int categoryId, CancellationToken cancellationToken)
        {
            Plugin plugin = Plugin.Instance;
            using (XtreamClient client = new XtreamClient())
            {
                List<StreamInfo> streams = await client.GetLiveStreamsByCategoryAsync(
                  plugin.Creds,
                  categoryId.ToString(CultureInfo.InvariantCulture),
                  cancellationToken).ConfigureAwait(false);
                return Ok(streams);
            }
        }
    }
}
