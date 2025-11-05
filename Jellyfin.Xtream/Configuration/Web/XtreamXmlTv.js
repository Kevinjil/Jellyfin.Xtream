export default function (view) {
    let Xtream;
    let pluginId;

    view.addEventListener('viewshow', () => {
        const moduleUrl = ApiClient.getUrl("configurationpage", {
            name: "Xtream.js",
        });
        
        // Load the shared module
        return import(moduleUrl)
        .then((module) => {
            console.log("Xtream module loaded");
            Xtream = module.default;
            pluginId = Xtream.pluginConfig.UniqueId;
            console.log("Plugin ID:", pluginId);
            return ApiClient.getPluginConfiguration(pluginId);
        })
        .catch(err => {
            console.error("Error loading Xtream module:", err);
            Dashboard.alert({
                message: "Failed to load configuration module. Please check the browser console for details."
            });
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

            console.log("Updating configuration:", config);
            return ApiClient.updatePluginConfiguration(pluginId, config)
                .then((result) => {
                    console.log("Configuration updated successfully");
                    return Xtream.logConfigurationChange('XMLTV')
                        .then(() => {
                            console.log("Configuration change logged");
                            Dashboard.processPluginConfigurationUpdateResult(result);
                        })
                        .catch(err => {
                            console.error("Error logging configuration change:", err);
                            // Still show success since the config was saved
                            Dashboard.processPluginConfigurationUpdateResult(result);
                        });
                })
                .catch(err => {
                    console.error("Error updating configuration:", err);
                    Dashboard.alert({
                        message: "Failed to save configuration. Check the browser console for details."
                    });
                });
        });

        return false;
    });
}
