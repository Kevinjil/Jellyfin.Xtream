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
using System.Threading.Tasks;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Configuration;
using Jellyfin.Xtream.Service;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private static Plugin? _instance;
    private readonly ILogger<Plugin> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    /// <param name="taskManager">Instance of the <see cref="ITaskManager"/> interface.</param>
    /// <param name="xtreamClient">Instance of the <see cref="IXtreamClient"/> interface.</param>
    /// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
    /// <param name="failureTrackingService">Instance of the <see cref="FailureTrackingService"/> class.</param>
    /// <param name="channelManager">Instance of the <see cref="IChannelManager"/> interface.</param>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface.</param>
    /// <param name="serverConfigManager">Instance of the <see cref="IServerConfigurationManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{Plugin}"/> interface.</param>
    /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ITaskManager taskManager,
        IXtreamClient xtreamClient,
        IMemoryCache memoryCache,
        FailureTrackingService failureTrackingService,
        IChannelManager channelManager,
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        IServerConfigurationManager serverConfigManager,
        ILogger<Plugin> logger,
        ILoggerFactory loggerFactory)
        : base(applicationPaths, xmlSerializer)
    {
        _instance = this;
        XtreamClient = xtreamClient;
        ChannelManager = channelManager;
        LibraryManager = libraryManager;
        if (XtreamClient is XtreamClient client)
        {
            client.UpdateUserAgent();
        }

        StreamService = new(xtreamClient, loggerFactory.CreateLogger<Service.StreamService>());
        TaskService = new(taskManager);
        _logger = logger;
        SeriesCacheService = new Service.SeriesCacheService(
            StreamService,
            memoryCache,
            failureTrackingService,
            loggerFactory.CreateLogger<Service.SeriesCacheService>(),
            providerManager,
            serverConfigManager);

        // Start cache refresh in background (don't await - let it run async)
        // Only refresh if caching is enabled and credentials are configured
        if (Configuration.EnableSeriesCaching &&
            !string.IsNullOrEmpty(Configuration.BaseUrl) &&
            Configuration.BaseUrl != "https://example.com" &&
            !string.IsNullOrEmpty(Configuration.Username))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await SeriesCacheService.RefreshCacheAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize series cache");
                }
            });
        }
        else if (!Configuration.EnableSeriesCaching)
        {
            _logger.LogInformation("Skipping initial cache refresh - caching is disabled");
        }
        else
        {
            _logger.LogInformation("Skipping initial cache refresh - credentials not configured");
        }
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
    /// Gets the cache-specific data version that only changes when cache-relevant settings change.
    /// This excludes settings like refresh frequency that don't affect cached data.
    /// </summary>
    public string CacheDataVersion => Assembly.GetCallingAssembly().GetName().Version?.ToString() + Configuration.GetCacheRelevantHash();

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin Instance => _instance ?? throw new InvalidOperationException("Plugin instance not available");

    /// <summary>
    /// Gets the stream service instance.
    /// </summary>
    public StreamService StreamService { get; init; }

    private IXtreamClient XtreamClient { get; init; }

    /// <summary>
    /// Gets the task service instance.
    /// </summary>
    public TaskService TaskService { get; init; }

    /// <summary>
    /// Gets the channel manager instance for eager database population.
    /// </summary>
    public IChannelManager ChannelManager { get; init; }

    /// <summary>
    /// Gets the library manager instance for delta-based database population.
    /// </summary>
    public ILibraryManager LibraryManager { get; init; }

    /// <summary>
    /// Gets the series cache service instance.
    /// </summary>
    public Service.SeriesCacheService SeriesCacheService { get; init; }

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

        if (XtreamClient is XtreamClient client)
        {
            client.UpdateUserAgent();
        }

        // Update scheduled task interval to match plugin configuration
        if (configuration is PluginConfiguration pluginConfig)
        {
            int refreshMinutes = pluginConfig.SeriesCacheExpirationMinutes;
            if (refreshMinutes >= 10)
            {
                TaskService.UpdateCacheRefreshInterval(refreshMinutes);
            }
        }

        // Refresh series cache in background when configuration changes
        // Only refresh if caching is enabled and credentials are configured
        if (Configuration.EnableSeriesCaching &&
            !string.IsNullOrEmpty(Configuration.BaseUrl) &&
            Configuration.BaseUrl != "https://example.com" &&
            !string.IsNullOrEmpty(Configuration.Username))
        {
            // Cancel any running refresh so the new one can start with updated settings
            SeriesCacheService.CancelRefresh();

            _ = Task.Run(async () =>
            {
                try
                {
                    // Small delay to allow cancellation to propagate
                    await Task.Delay(500).ConfigureAwait(false);
                    await SeriesCacheService.RefreshCacheAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh series cache after configuration update");
                }
            });
        }
        else if (!Configuration.EnableSeriesCaching)
        {
            // Clear cache when caching is disabled
            SeriesCacheService.InvalidateCache();
        }

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
