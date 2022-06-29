export default function (view) {
  view.addEventListener("viewshow", () => import(
    ApiClient.getUrl("web/ConfigurationPage", {
      name: "Xtream.js",
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    Xtream.setTabs(3);

    const table = view.querySelector('#SeriesContent');
    Xtream.populateCategoriesTable(
      table,
      () => ApiClient.getPluginConfiguration(pluginId).then((config) => config.Series),
      () => Xtream.fetchJson('Xtream/SeriesCategories'),
      (categoryId) => Xtream.fetchJson(`Xtream/SeriesCategories/${categoryId}`),
    ).then((data) => {
      view.querySelector('#XtreamSeriesForm').addEventListener('submit', (e) => {
        Dashboard.showLoadingMsg();

        ApiClient.getPluginConfiguration(pluginId).then((config) => {
          config.Series = data;
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