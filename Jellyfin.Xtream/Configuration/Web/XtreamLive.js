export default function (view) {
  view.addEventListener("viewshow", () => import(
    ApiClient.getUrl("web/ConfigurationPage", {
      name: "Xtream.js",
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.PluginConfig.UniqueId;
    LibraryMenu.setTabs('Live TV', 1, Xtream.getTabs);

    const table = view.querySelector('#LiveContent');
    Xtream.populateCategoriesTable(
      table,
      () => ApiClient.getPluginConfiguration(pluginId).then((config) => config.LiveTv),
      () => ApiClient.fetch({
        dataType: 'json',
        type: 'GET',
        url: ApiClient.getUrl('Xtream/LiveCategories'),
      }),
      (categoryId) => ApiClient.fetch({
        dataType: 'json',
        type: 'GET',
        url: ApiClient.getUrl(`Xtream/LiveCategories/${categoryId}`),
      }),
    ).then((data) => {
      view.querySelector('#XtreamLiveForm').addEventListener('submit', (e) => {
        Dashboard.showLoadingMsg();

        ApiClient.getPluginConfiguration(pluginId).then((config) => {
          config.LiveTv = data;
          ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
            Dashboard.processPluginConfigurationUpdateResult(result);
          });
        });

        e.preventDefault();
        return false;
      });
    });
  }));
}