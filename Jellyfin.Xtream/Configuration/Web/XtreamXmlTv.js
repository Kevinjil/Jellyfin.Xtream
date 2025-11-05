export default function (view) {
    let Xtream;
    let pluginId;

    view.addEventListener('viewshow', () => {
        import(window.ApiClient.getUrl("web/ConfigurationPage", {
            name: "Xtream.js",
        }))
        .then((module) => {
            Xtream = module.default;
            pluginId = Xtream.pluginConfig.UniqueId;
            return ApiClient.getPluginConfiguration(pluginId);
        })
        .then((config) => {
            view.querySelector('#UseXmlTv').checked = config.UseXmlTv;
            view.querySelector('#XmlTvUrl').value = config.XmlTvUrl;
            view.querySelector('#XmlTvHistoricalDays').value = config.XmlTvHistoricalDays;
            view.querySelector('#XmlTvCacheMinutes').value = config.XmlTvCacheMinutes;
            view.querySelector('#XmlTvSupportsTimeshift').checked = config.XmlTvSupportsTimeshift;
            view.querySelector('#XmlTvDiskCache').checked = config.XmlTvDiskCache;
            view.querySelector('#XmlTvCachePath').value = config.XmlTvCachePath;
        });
    });

    view.querySelector('#XtreamXmlTvForm').addEventListener('submit', function (e) {
        e.preventDefault();
        
        const useXmlTv = view.querySelector('#UseXmlTv').checked;
        const xmlTvUrl = view.querySelector('#XmlTvUrl').value.trim();
        const historicalDays = Math.max(0, parseInt(view.querySelector('#XmlTvHistoricalDays').value || "0"));
        const cacheMinutes = Math.max(1, parseInt(view.querySelector('#XmlTvCacheMinutes').value || "10"));
        const diskCache = view.querySelector('#XmlTvDiskCache').checked;
        const cachePath = view.querySelector('#XmlTvCachePath').value.trim();
        
        // Validate URL if provided and XMLTV is enabled
        if (useXmlTv && xmlTvUrl && !xmlTvUrl.startsWith('/') && !xmlTvUrl.match(/^https?:\/\/.+/i)) {
            Dashboard.alert({
                message: "XMLTV URL must be either an absolute URL starting with http:// or https://, or a relative path starting with /"
            });
            return false;
        }

        ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            config.UseXmlTv = useXmlTv;
            config.XmlTvUrl = xmlTvUrl;
            config.XmlTvHistoricalDays = historicalDays;
            config.XmlTvCacheMinutes = cacheMinutes;
            config.XmlTvSupportsTimeshift = view.querySelector('#XmlTvSupportsTimeshift').checked;
            config.XmlTvDiskCache = diskCache;
            config.XmlTvCachePath = cachePath;

            ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
                Xtream.logConfigurationChange('XMLTV');
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });

        return false;
    });
}
