export default function (view) {
  const pluginId = window.Xtream.PluginConfig.UniqueId;

  document.querySelector('#XtreamCredentialsPage').addEventListener('pageshow', () => {
    Dashboard.showLoadingMsg();
    ApiClient.getPluginConfiguration(pluginId).then(function (config) {
      document.querySelector('#XtreamCredentialsForm #BaseUrl').value = config.BaseUrl;
      document.querySelector('#XtreamCredentialsForm #Username').value = config.Username;
      document.querySelector('#XtreamCredentialsForm #Password').value = config.Password;
      Dashboard.hideLoadingMsg();
    });
  });

  document.querySelector('#XtreamCredentialsForm').addEventListener('submit', (e) => {
    Dashboard.showLoadingMsg();

    ApiClient.getPluginConfiguration(pluginId).then((config) => {
      config.BaseUrl = document.querySelector('#XtreamCredentialsForm #BaseUrl').value;
      config.Username = document.querySelector('#XtreamCredentialsForm #Username').value;
      config.Password = document.querySelector('#XtreamCredentialsForm #Password').value;
      ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
        Dashboard.processPluginConfigurationUpdateResult(result);
      });
    });

    e.preventDefault();
    return false;
  });

  view.addEventListener("viewshow", () => {
    LibraryMenu.setTabs('XtreamCredentials', 2, window.Xtream.getTabs);
  });
}