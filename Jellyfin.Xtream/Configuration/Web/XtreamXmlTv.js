export default function (view) {
    let Xtream;
    let pluginId;

    view.addEventListener('viewshow', () => {
        const moduleUrl = ApiClient.getUrl("web/ConfigurationPage", {
            name: "Xtream.js"
        });
        
        // Use traditional script loading since dynamic import is failing
        const script = document.createElement('script');
        script.src = moduleUrl;
        script.type = 'module';
        
        script.onload = () => {
            // Access the module through the global namespace
            Xtream = window.Xtream;
            pluginId = Xtream.pluginConfig.UniqueId;
            return ApiClient.getPluginConfiguration(pluginId);
        };
        
        script.onerror = (err) => {
            console.error('Failed to load Xtream module:', err);
            Dashboard.alert({
                message: 'Failed to load plugin configuration module. Please try refreshing the page.'
            });
        };
        
        document.head.appendChild(script);

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
