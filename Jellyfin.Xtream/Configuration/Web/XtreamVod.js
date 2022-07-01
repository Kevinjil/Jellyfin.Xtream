export default function (view) {
  view.addEventListener("viewshow", () => import(
    ApiClient.getUrl("web/ConfigurationPage", {
      name: "Xtream.js",
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    Xtream.setTabs(2);

    const getConfig = ApiClient.getPluginConfiguration(pluginId);
    const visible = view.querySelector("#Visible");
    getConfig.then((config) => visible.checked = config.IsVodVisible);
    const table = view.querySelector('#VodContent');
    Xtream.populateCategoriesTable(
      table,
      () => getConfig.then((config) => config.Vod),
      () => Xtream.fetchJson('Xtream/VodCategories'),
      (categoryId) => Xtream.fetchJson(`Xtream/VodCategories/${categoryId}`),
    ).then((data) => {
      view.querySelector('#XtreamVodForm').addEventListener('submit', (e) => {
        Dashboard.showLoadingMsg();

        ApiClient.getPluginConfiguration(pluginId).then((config) => {
          config.IsVodVisible = visible.checked;
          config.Vod = data;
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