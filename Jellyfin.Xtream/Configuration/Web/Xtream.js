export default {
  getTabs: () => {
    const url = (name) =>
      ApiClient.getUrl("web/ConfigurationPage", {
        name,
      });
    const tabs = [
      {
        href: url('XtreamCredentials.html'),
        name: 'Xtream Credentials'
      },
      {
        href: url('XtreamLive.html'),
        name: 'Live TV'
      }
    ];
    return tabs;
  },
  PluginConfig: {
    UniqueId: '5d774c35-8567-46d3-a950-9bb8227a0c5d'
  }
}