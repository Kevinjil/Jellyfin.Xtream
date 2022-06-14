export default function (view) {
  const pluginId = window.Xtream.PluginConfig.UniqueId;

  const createStreamRow = (live, data, state, update) => {
    const tr = document.createElement('tr');
    tr.dataset['streamId'] = data.StreamId;

    let td = document.createElement('td');
    td.innerHTML = data.Name;
    tr.appendChild(td);

    td = document.createElement('td');
    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    if (typeof state === 'boolean') {
      checkbox.checked = state
    } else {
      checkbox.checked = live.includes(data.StreamId);
    }
    checkbox.onchange = update;
    td.appendChild(checkbox);
    tr.appendChild(td);

    return tr;
  }

  const createCategoryTable = (live, data, state, update) => {
    const table = document.createElement('table');
    ApiClient.fetch({ url: ApiClient.getUrl(`Xtream/LiveStreams/${data.CategoryId}`), type: 'GET' })
      .then((response) => response.json())
      .then((streams) => {
        for (let i = 0; i < streams.length; ++i) {
          const stream = streams[i];
          const row = createStreamRow(live[data.CategoryId] ?? [], stream, state, update);
          table.appendChild(row);
        }
        Dashboard.hideLoadingMsg();
      });

    return table;
  }

  const createRow = (live, data) => {
    const tr = document.createElement('tr');
    tr.dataset['categoryId'] = data.CategoryId;

    let td = document.createElement('td');
    td.innerHTML = data.CategoryName;
    tr.appendChild(td);

    td = document.createElement('td');
    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    if (data.CategoryId in live) {
      if (live[data.CategoryId].length > 0) {
        checkbox.indeterminate = true;
      } else {
        checkbox.checked = true
      }
    }
    td.appendChild(checkbox);
    tr.appendChild(td);

    td = document.createElement('td');
    const expand = document.createElement('button');
    expand.type = 'button';
    expand.className = 'emby-button';
    expand.innerText = '+';
    expand.onclick = (e) => {
      e.preventDefault();
      const originalClick = expand.onclick;

      Dashboard.showLoadingMsg();
      expand.innerText = '-';
      const state = checkbox.indeterminate ? undefined : checkbox.checked;
      const table = createCategoryTable(live, data, state, () => {
        let all = true;
        let none = true;
        table.querySelectorAll('input[type="checkbox"]').forEach((c) => {
          all &= c.checked;
          none &= !c.checked;
        });
        checkbox.checked = all;
        checkbox.indeterminate = !(all || none);
      });
      checkbox.onclick = () => {
        table.querySelectorAll('input[type="checkbox"]').forEach((c) => c.checked = checkbox.checked);
      };
      td.appendChild(table);

      expand.onclick = () => {
        expand.onclick = originalClick;

        Dashboard.showLoadingMsg();
        expand.innerText = '+';
        td.removeChild(table);
        Dashboard.hideLoadingMsg();
      };
    };
    td.appendChild(expand);
    tr.appendChild(td);

    return tr;
  };

  const computeLiveTv = (config, live) => {
    const result = {};
    const categories = live.querySelectorAll('tr[data-category-id]');
    for (let i = 0; i < categories.length; ++i) {
      const category = categories[i];
      const categoryId = category.dataset['categoryId'];

      const checkbox = category.querySelector(':scope > td > input[type="checkbox"]');
      if (checkbox.indeterminate) {
        const table = category.querySelector('table');
        if (!table) {
          result[categoryId] = config.LiveTv[categoryId];
          continue;
        }

        const streams = table.querySelectorAll('tr[data-stream-id]');
        for (let j = 0; j < streams.length; ++j) {
          const stream = streams[j];
          const streamId = stream.dataset['streamId'];

          const checkbox2 = stream.querySelector('td > input[type="checkbox"]');
          if (checkbox2.checked) {
            result[categoryId] ??= [];
            result[categoryId].push(streamId);
          }
        }
      } else if (checkbox.checked) {
        result[categoryId] = [];
      }
    }
    return result;
  };

  document.querySelector('#XtreamLivePage').addEventListener('pageshow', () => {
    Dashboard.showLoadingMsg();
    const fetchConfig = ApiClient.getPluginConfiguration(pluginId);
    const fetchCategories = ApiClient.fetch({ url: ApiClient.getUrl('Xtream/LiveCategories'), type: 'GET' }).then((response) => response.json());
    Promise.all([fetchConfig, fetchCategories]).then(([config, categories]) => {
      const live = document.querySelector('#XtreamLiveForm #LiveContent');
      for (let i = 0; i < categories.length; ++i) {
        const elem = createRow(config.LiveTv, categories[i]);
        live.appendChild(elem);
      }
      Dashboard.hideLoadingMsg();
    });
  });

  document.querySelector('#XtreamLiveForm').addEventListener('submit', (e) => {
    Dashboard.showLoadingMsg();

    ApiClient.getPluginConfiguration(pluginId).then((config) => {
      const live = document.querySelector('#XtreamLiveForm #LiveContent');
      config.LiveTv = computeLiveTv(config, live);
      ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
        Dashboard.processPluginConfigurationUpdateResult(result);
      });
    });

    e.preventDefault();
    return false;
  });

  view.addEventListener("viewshow", () => {
    LibraryMenu.setTabs('XtreamLive', 2, window.Xtream.getTabs);
  });
}