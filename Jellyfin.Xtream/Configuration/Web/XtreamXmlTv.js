export default function (view) {
    const pluginId = '5d774c35-8567-46d3-a950-9bb8227a0c5d';

    view.addEventListener('viewshow', () => {
        console.log("Loading XMLTV configuration...");
        ApiClient.getPluginConfiguration(pluginId)
            .then((config) => {
                console.log("Loaded plugin configuration:", config);
                view.querySelector('#UseXmlTv').checked = config.UseXmlTv;
                view.querySelector('#XmlTvUrl').value = config.XmlTvUrl || '';
                view.querySelector('#XmlTvHistoricalDays').value = config.XmlTvHistoricalDays || 0;
                view.querySelector('#XmlTvCacheMinutes').value = config.XmlTvCacheMinutes || 10;
                view.querySelector('#XmlTvSupportsTimeshift').checked = config.XmlTvSupportsTimeshift;
                view.querySelector('#XmlTvDiskCache').checked = config.XmlTvDiskCache;
                view.querySelector('#XmlTvCachePath').value = config.XmlTvCachePath || '';
            })
            .catch(err => {
                console.error("Error loading configuration:", err);
                Dashboard.alert({
                    message: "Failed to load plugin configuration. Please check the server logs."
                });
            });
    });

    view.querySelector('#XtreamXmlTvForm').addEventListener('submit', function (e) {
        e.preventDefault();
        console.log("Saving XMLTV configuration...");
        
        const useXmlTv = view.querySelector('#UseXmlTv').checked;
        const xmlTvUrl = view.querySelector('#XmlTvUrl').value.trim();
        const historicalDays = Math.max(0, parseInt(view.querySelector('#XmlTvHistoricalDays').value || "0"));
        const cacheMinutes = Math.max(1, parseInt(view.querySelector('#XmlTvCacheMinutes').value || "10"));
        const diskCache = view.querySelector('#XmlTvDiskCache').checked;
        const cachePath = view.querySelector('#XmlTvCachePath').value.trim();
        
        if (useXmlTv && xmlTvUrl && !xmlTvUrl.startsWith('/') && !xmlTvUrl.match(/^https?:\/\/.+/i)) {
            Dashboard.alert({
                message: "XMLTV URL must be either an absolute URL starting with http:// or https://, or a relative path starting with /"
            });
            return false;
        }

        ApiClient.getPluginConfiguration(pluginId)
            .then(function (config) {
                config.UseXmlTv = useXmlTv;
                config.XmlTvUrl = xmlTvUrl;
                config.XmlTvHistoricalDays = historicalDays;
                config.XmlTvCacheMinutes = cacheMinutes;
                config.XmlTvSupportsTimeshift = view.querySelector('#XmlTvSupportsTimeshift').checked;
                config.XmlTvDiskCache = diskCache;
                config.XmlTvCachePath = cachePath;

                console.log("Saving configuration:", config);
                return ApiClient.updatePluginConfiguration(pluginId, config);
            })
            .then((result) => {
                console.log("Configuration saved successfully");
                return ApiClient.fetch({
                    type: 'POST',
                    url: ApiClient.getUrl('Xtream/LogConfigChange'),
                    data: JSON.stringify({ page: 'XMLTV' }),
                    contentType: 'application/json'
                });
            })
            .then(() => {
                console.log("Configuration change logged");
                Dashboard.processPluginConfigurationUpdateResult();
            })
            .catch(err => {
                console.error("Error saving configuration:", err);
                Dashboard.alert({
                    message: "Failed to save configuration. Please check the browser console and server logs."
                });
            });

        return false;
    });
}
