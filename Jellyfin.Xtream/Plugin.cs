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
using System.Reflection;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Configuration;
using Jellyfin.Xtream.Service;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private static Plugin? _instance;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="taskManager">Instance of the <see cref="ITaskManager"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ITaskManager taskManager)
        : base(applicationPaths, xmlSerializer)
    {
        _instance = this;
        StreamService = new();
        TaskService = new(taskManager);
    }

    /// <inheritdoc />
    public override string Name => "Jellyfin Xtream";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("5d774c35-8567-46d3-a950-9bb8227a0c5d");

    /// <summary>
    /// Gets the Xtream connection info with credentials.
    /// </summary>
    public ConnectionInfo Creds => new(Configuration.BaseUrl, Configuration.Username, Configuration.Password);

    /// <summary>
    /// Gets the data version used to trigger a cache invalidation on plugin update or config change.
    /// </summary>
    public string DataVersion => Assembly.GetCallingAssembly().GetName().Version?.ToString() + Configuration.GetHashCode();

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin Instance => _instance ?? throw new InvalidOperationException("Plugin instance not available");

    /// <summary>
    /// Gets the stream service instance.
    /// </summary>
    public StreamService StreamService { get; init; }

    /// <summary>
    /// Gets the task service instance.
    /// </summary>
    public TaskService TaskService { get; init; }

    private static PluginPageInfo CreateStatic(string name) => new()
    {
        Name = name,
        EmbeddedResourcePath = string.Format(
            CultureInfo.InvariantCulture,
            "{0}.Configuration.Web.{1}",
            typeof(Plugin).Namespace,
            name),
    };

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            CreateStatic("XtreamCredentials.html"),
            CreateStatic("XtreamCredentials.js"),
            CreateStatic("Xtream.css"),
            CreateStatic("Xtream.js"),
            CreateStatic("XtreamLive.html"),
            CreateStatic("XtreamLive.js"),
            CreateStatic("XtreamLiveOverrides.html"),
            CreateStatic("XtreamLiveOverrides.js"),
            CreateStatic("XtreamSeries.html"),
            CreateStatic("XtreamSeries.js"),
            CreateStatic("XtreamVod.html"),
            CreateStatic("XtreamVod.js"),
        };
    }

    /// <inheritdoc />
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        base.UpdateConfiguration(configuration);

        // Force a refresh of TV guide on configuration update.
        // - This will update the TV channels.
        // - This will remove channels on credentials change.
        TaskService.CancelIfRunningAndQueue(
            "Jellyfin.LiveTv",
            "Jellyfin.LiveTv.Guide.RefreshGuideScheduledTask");

        // Force a refresh of Channels on configuration update.
        // - This will update the channel entries.
        // - This will remove channel entries on credentials change.
        TaskService.CancelIfRunningAndQueue(
            "Jellyfin.LiveTv",
            "Jellyfin.LiveTv.Channels.RefreshChannelsScheduledTask");
    }
}
