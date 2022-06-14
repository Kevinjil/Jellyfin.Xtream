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
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream
{
/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly ILogger<Plugin> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        _logger = logger;
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Jellyfin Xtream";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("5d774c35-8567-46d3-a950-9bb8227a0c5d");

    /// <summary>
    /// Gets the Xtream connection info with credentials.
    /// </summary>
    public ConnectionInfo Creds
    {
        get => new ConnectionInfo(this.Configuration.BaseUrl, this.Configuration.Username, this.Configuration.Password);
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "XtreamCredentials",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.Web.XtreamCredentials.html", GetType().Namespace)
            },
            new PluginPageInfo
            {
                Name = "XtreamCredentials.js",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.Web.XtreamCredentials.js", GetType().Namespace)
            },
            new PluginPageInfo
            {
                Name = "XtreamLive",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.Web.XtreamLive.html", GetType().Namespace)
            },
            new PluginPageInfo
            {
                Name = "XtreamLive.js",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.Web.XtreamLive.js", GetType().Namespace)
            },
            new PluginPageInfo
            {
                Name = "Xtream.js",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.Web.Xtream.js", GetType().Namespace)
            }
        };
    }
}
}
