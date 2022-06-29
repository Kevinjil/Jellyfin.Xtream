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
      Dashboard.hideLoadingMsg();
    });

    view.querySelector('#XtreamCredentialsForm').addEventListener('submit', (e) => {
      Dashboard.showLoadingMsg();

      ApiClient.getPluginConfiguration(pluginId).then((config) => {
        config.BaseUrl = view.querySelector('#BaseUrl').value;
        config.Username = view.querySelector('#Username').value;
        config.Password = view.querySelector('#Password').value;
        ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
          Dashboard.processPluginConfigurationUpdateResult(result);
        });
      });

      e.preventDefault();
      return false;
    });
  }));
}