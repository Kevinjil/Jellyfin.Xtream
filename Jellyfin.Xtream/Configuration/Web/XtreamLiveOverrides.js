export default function (view) {
  const createChannelRow = (channel, overrides) => {
    const tr = document.createElement('tr');
    tr.dataset['channelId'] = channel.Id;

    let td = document.createElement('td');
    const number = document.createElement('input');
    number.type = 'number';
    number.setAttribute('is', 'emby-input');
    number.placeholder = channel.Number;
    number.value = overrides.Number ?? '';
    number.onchange = () => number.value ?
      overrides.Number = parseInt(number.value) :
      delete overrides.Number;
    td.appendChild(number);
    tr.appendChild(td);

    td = document.createElement('td');
    const name = document.createElement('input');
    name.type = 'text';
    name.setAttribute('is', 'emby-input');
    name.placeholder = channel.Name;
    name.value = overrides.Name ?? '';
    name.onchange = () => name.value ?
      overrides.Name = name.value :
      delete overrides.Name;
    td.appendChild(name);
    tr.appendChild(td);

    td = document.createElement('td');
    const image = document.createElement('input');
    image.type = 'text';
    image.setAttribute('is', 'emby-input');
    image.placeholder = channel.LogoUrl;
    image.value = overrides.LogoUrl ?? '';
    image.onchange = () => image.value ?
      overrides.LogoUrl = image.value :
      delete overrides.LogoUrl;
    td.appendChild(image);
    tr.appendChild(td);

    td = document.createElement('td');
    const epg = document.createElement('input');
    epg.type = 'text';
    epg.setAttribute('is', 'emby-input');
    epg.setAttribute('list', 'XmlTvChannelOptions');
    epg.placeholder = 'XMLTV channel id';
    epg.value = overrides.EpgId ?? '';
    epg.onchange = () => epg.value.trim() ?
      overrides.EpgId = epg.value.trim() :
      delete overrides.EpgId;
    td.appendChild(epg);
    tr.appendChild(td);

    return tr;
  };

  const populateEpgOptions = (channels) => {
    const list = view.querySelector('#XmlTvChannelOptions');
    list.innerHTML = '';
    for (const channel of channels) {
      const option = document.createElement('option');
      option.value = channel.Id;
      option.label = channel.Name;
      list.appendChild(option);
    }
  };

  view.addEventListener('viewshow', () => import(
    ApiClient.getUrl('web/ConfigurationPage', { name: 'Xtream.js' })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    Xtream.setTabs(2);
    const table = view.querySelector('#LiveChannels');
    table.innerHTML = '';
    const status = view.querySelector('#CustomEpgStatus');
    status.textContent = '';

    Dashboard.showLoadingMsg();
    Promise.all([
      ApiClient.getPluginConfiguration(pluginId),
      Xtream.fetchJson('Xtream/LiveTv'),
    ]).then(([config, channels]) => {
      const data = config.LiveTvOverrides ?? {};
      view.querySelector('#EpgMode').value = config.EpgMode ?? 'Xtream';
      view.querySelector('#CustomEpgPath').value = config.CustomEpgPath ?? '';

      for (const channel of channels) {
        data[channel.Id] ??= {};
        table.appendChild(createChannelRow(channel, data[channel.Id]));
      }

      const epgPromise = config.CustomEpgPath ?
        Xtream.fetchJson('Xtream/CustomEpg/Channels') :
        Promise.resolve([]);

      return epgPromise.then((epgChannels) => {
        populateEpgOptions(epgChannels);
        status.textContent = config.CustomEpgPath ?
          `Loaded ${epgChannels.length} XMLTV channels.` :
          'Set an XMLTV path and save to load channel-id suggestions.';
        return { config, data };
      }).catch((error) => {
        console.error('Unable to load XMLTV channels', error);
        status.textContent = 'The XMLTV channel list could not be loaded. Check the saved path and Jellyfin server permissions.';
        return { config, data };
      });
    }).then(({ data }) => {
      Dashboard.hideLoadingMsg();
      view.querySelector('#XtreamLiveOverridesForm').onsubmit = (e) => {
        e.preventDefault();
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then((config) => {
          config.EpgMode = view.querySelector('#EpgMode').value;
          config.CustomEpgPath = view.querySelector('#CustomEpgPath').value.trim();
          config.LiveTvOverrides = Xtream.filter(
            data,
            overrides => Object.keys(overrides).length > 0
          );
          return ApiClient.updatePluginConfiguration(pluginId, config);
        }).then((result) => {
          Dashboard.processPluginConfigurationUpdateResult(result);
        });
        return false;
      };
    }).catch((error) => {
      console.error(error);
      Dashboard.hideLoadingMsg();
      status.textContent = 'Unable to load the TV Overrides page.';
    });
  }));
}
