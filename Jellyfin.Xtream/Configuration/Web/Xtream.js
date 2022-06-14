if (!window.Xtream) {
  const getConfigurationPageUrl = (name) => {
    return 'configurationpage?name=' + encodeURIComponent(name);
  };

  window.Xtream = {
    getTabs: () => {
      const tabs = [
        {
          href: getConfigurationPageUrl('XtreamCredentials'),
          name: 'Xtream Credentials'
        },
        {
          href: getConfigurationPageUrl('XtreamLive'),
          name: 'Live TV'
        }
      ];
      return tabs;
    },
    PluginConfig: {
      UniqueId: '5d774c35-8567-46d3-a950-9bb8227a0c5d'
    }
  }
}