export default function (view) {
  view.addEventListener("viewshow", () => import(
    ApiClient.getUrl("web/ConfigurationPage", {
      name: "Xtream.js",
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    Xtream.setTabs(3);

    const getConfig = ApiClient.getPluginConfiguration(pluginId);
    const visible = view.querySelector("#Visible");
    getConfig.then((config) => visible.checked = config.IsVodVisible);
    const tmdbOverride = view.querySelector("#TmdbOverride");
    getConfig.then((config) => TmdbOverride.checked = config.IsTmdbVodOverride);
    const table = view.querySelector('#VodContent');
    const createMainFolder = view.querySelector('#CreateMainFolder');
    const mainFolderName = view.querySelector('#MainFolderName');

    getConfig.then((config) => {
      createMainFolder.checked = config.CreateMainFolder || false;
      mainFolderName.value = config.MainFolderName || "Filme";
    });

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
          config.IsTmdbVodOverride = tmdbOverride.checked;
          config.Vod = data;
          config.CreateMainFolder = createMainFolder.checked;
          config.MainFolderName = mainFolderName.value || "Filme";
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