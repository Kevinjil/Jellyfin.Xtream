export default function (view) {
  const getConfigurationPageUrl = (name) => {
    return 'configurationpage?name=' + encodeURIComponent(name);
  }

  function getTabs() {
    var tabs = [
      {
        href: getConfigurationPageUrl('XtreamCredentials'),
        name: 'Xtream Credentials'
      }
    ];
    return tabs;
  }

  var XtreamConfig = {
    pluginUniqueId: '5d774c35-8567-46d3-a950-9bb8227a0c5d'
  };

  document.querySelector('#XtreamConfigPage').addEventListener('pageshow', function () {
    Dashboard.showLoadingMsg();
    ApiClient.getPluginConfiguration(XtreamConfig.pluginUniqueId).then(function (config) {
      document.querySelector('#BaseUrl').value = config.BaseUrl;
      document.querySelector('#Username').value = config.Username;
      document.querySelector('#Password').value = config.Password;
      Dashboard.hideLoadingMsg();
    });
  });

  document.querySelector('#XtreamConfigForm').addEventListener('submit', function (e) {
    Dashboard.showLoadingMsg();

    ApiClient.getPluginConfiguration(XtreamConfig.pluginUniqueId).then(function (config) {
      config.BaseUrl = document.querySelector('#BaseUrl').value;
      config.Username = document.querySelector('#Username').value;
      config.Password = document.querySelector('#Password').value;
      ApiClient.updatePluginConfiguration(XtreamConfig.pluginUniqueId, config).then(function (result) {
        Dashboard.processPluginConfigurationUpdateResult(result);
      });
    });

    e.preventDefault();
    return false;
  });

  view.addEventListener("viewshow", function (e) {
    LibraryMenu.setTabs('credentials', 2, getTabs);
  });
}