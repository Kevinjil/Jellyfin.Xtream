#pragma warning disable
// Custom XMLTV configuration API for Jellyfin.Xtream.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using Jellyfin.Xtream.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Xtream.Api;

/// <summary>Provides the XMLTV channel list used by the TV Overrides mapping UI.</summary>
[ApiController]
[Route("Xtream/CustomEpg")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class CustomEpgController : ControllerBase
{
    /// <summary>Gets channels from the XMLTV file saved in plugin configuration.</summary>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("Channels")]
    public ActionResult<IReadOnlyList<XmlTvChannelInfo>> GetChannels()
    {
        string path = Plugin.Instance.Configuration.CustomEpgPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return Ok(Array.Empty<XmlTvChannelInfo>());
        }

        try
        {
            return Ok(XmlTvGuideService.GetChannels(path));
        }
        catch (FileNotFoundException exception)
        {
            return NotFound(new { Message = exception.Message });
        }
        catch (Exception exception)
        {
            return StatusCode(500, new { Message = exception.Message });
        }
    }
}
#pragma warning restore
