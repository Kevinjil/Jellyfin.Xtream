// Copyright (C) 2022  Kevin Jilissen

export default function (view) {
    view.addEventListener('viewshow', function () {
        ApiClient.getPluginConfiguration('Xtream').then(function (config) {
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
        ApiClient.getPluginConfiguration('Xtream').then(function (config) {
            config.UseXmlTv = view.querySelector('#UseXmlTv').checked;
            config.XmlTvUrl = view.querySelector('#XmlTvUrl').value;
            config.XmlTvHistoricalDays = parseInt(view.querySelector('#XmlTvHistoricalDays').value || "0");
            config.XmlTvCacheMinutes = Math.max(1, parseInt(view.querySelector('#XmlTvCacheMinutes').value || "10"));
            config.XmlTvSupportsTimeshift = view.querySelector('#XmlTvSupportsTimeshift').checked;
            config.XmlTvDiskCache = view.querySelector('#XmlTvDiskCache').checked;
            config.XmlTvCachePath = view.querySelector('#XmlTvCachePath').value;

            ApiClient.updatePluginConfiguration('Xtream', config).then(Dashboard.processPluginConfigurationUpdateResult);
        });

        return false;
    });
}