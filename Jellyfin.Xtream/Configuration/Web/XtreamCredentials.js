export default function (view) {
  view.addEventListener("viewshow", () => import(
    window.ApiClient.getUrl("web/ConfigurationPage", {
      name: "Xtream.js",
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    Xtream.setTabs(0);

    Dashboard.showLoadingMsg();
    ApiClient.getPluginConfiguration(pluginId).then(function (config) {
      view.querySelector('#BaseUrl').value = config.BaseUrl;
      view.querySelector('#Username').value = config.Username;
      view.querySelector('#Password').value = config.Password;
      view.querySelector('#UserAgent').value = config.UserAgent;
      Dashboard.hideLoadingMsg();
    });

    const reloadStatus = () => {
      const status = view.querySelector("#ProviderStatus");
      const expiry = view.querySelector("#ProviderExpiry");
      const cons = view.querySelector("#ProviderConnections");
      const maxCons = view.querySelector("#ProviderMaxConnections");
      const time = view.querySelector("#ProviderTime");
      const timezone = view.querySelector("#ProviderTimezone");
      const mpegTs = view.querySelector("#ProviderMpegTs");

      Xtream.fetchJson('Xtream/TestProvider').then(response => {
        status.innerText = response.Status;
        expiry.innerText = response.ExpiryDate;
        cons.innerText = response.ActiveConnections;
        maxCons.innerText = response.MaxConnections;
        time.innerText = response.ServerTime;
        timezone.innerText = response.ServerTimezone;
        mpegTs.innerText = response.SupportsMpegTs;
      }).catch((_) => {
        status.innerText = "Failed. Check server logs.";
        expiry.innerText = "";
        cons.innerText = "";
        maxCons.innerText = "";
        time.innerText = "";
        timezone.innerText = "";
        mpegTs.innerText = "";
      });
    };
    reloadStatus();

    view.querySelector('#UserAgentFromBrowser').addEventListener('click', (e) => {
      e.preventDefault();
      view.querySelector('#UserAgent').value = navigator.userAgent;
    });

    view.querySelector('#XtreamCredentialsForm').addEventListener('submit', (e) => {
      Dashboard.showLoadingMsg();

      ApiClient.getPluginConfiguration(pluginId).then((config) => {
        config.BaseUrl = view.querySelector('#BaseUrl').value;
        config.Username = view.querySelector('#Username').value;
        config.Password = view.querySelector('#Password').value;
        config.UserAgent = view.querySelector('#UserAgent').value;
        ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
          reloadStatus();
          Dashboard.processPluginConfigurationUpdateResult(result);
        });
      });

      e.preventDefault();
      return false;
    });
  }));
}