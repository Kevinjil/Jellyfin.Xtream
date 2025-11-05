// Copyright (C) 2022  Kevin Jilissen

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

namespace Jellyfin.Xtream.Api;

/// <summary>
/// The Jellyfin Xtream configuration API.
/// </summary>
[ApiController]
[Route("[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public class XtreamController : ControllerBase
{
    private readonly IXtreamClient _xtreamClient;
    private readonly ILogger<XtreamController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtreamController"/> class.
    /// </summary>
    /// <param name="xtreamClient">The Xtream client.</param>
    /// <param name="logger">The logger.</param>
    public XtreamController(IXtreamClient xtreamClient, ILogger<XtreamController> logger)
    {
        _xtreamClient = xtreamClient;
        _logger = logger;
    }

    /// <summary>
    /// Log a configuration change.
    /// </summary>
    /// <param name="request">The log request.</param>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    /// <response code="204">Configuration change logged successfully.</response>
    [HttpPost("LogConfigChange")]
    [ProducesResponseType(204)]
    [Authorize(Policy = "RequiresElevation")]
    public ActionResult LogConfigChange([FromBody] LogConfigChangeRequest request)
    {
        _logger.LogInformation("Xtream plugin configuration changed: {Page} settings updated", request.Page);
        return NoContent();
    }

    private static CategoryResponse CreateCategoryResponse(Category category) =>
        new()
        {
            Id = category.CategoryId,
            Name = category.CategoryName,
        };
}
