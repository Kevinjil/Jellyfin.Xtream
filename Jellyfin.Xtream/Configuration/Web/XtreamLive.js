export default function (view) {
  const createStreamRow = (live, data, state, update) => {
    const tr = document.createElement('tr');
    tr.dataset['streamId'] = data.StreamId;

    let td = document.createElement('td');
    if (data.TvArchive) {
      let span = document.createElement('span');
      span.ariaHidden = true;
      span.title = `Catch-up supported for ${data.TvArchiveDuration} days.`;
      span.className = 'material-icons fiber_manual_record';
      td.appendChild(span);
    }
    tr.appendChild(td);

    td = document.createElement('td');
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
    ApiClient.fetch({
      url: ApiClient.getUrl(`Xtream/LiveStreams/${data.CategoryId}`),
      type: 'GET',
    }).then((response) => response.json())
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
    checkbox.onchange = () => checkbox.checked ? live[data.CategoryId] = [] : delete live[data.CategoryId];
    td.appendChild(checkbox);
    tr.appendChild(td);

    td = document.createElement('td');
    const expand = document.createElement('button');
    expand.type = 'button';
    expand.className = 'emby-button raised monospace';
    expand.innerText = '+';
    expand.onclick = (e) => {
      e.preventDefault();
      const originalClick = expand.onclick;

      Dashboard.showLoadingMsg();
      expand.innerText = '-';
      const state = checkbox.indeterminate ? undefined : checkbox.checked;
      const table = createCategoryTable(live, data, state, (e) => {
        const eventTr = e.target.parentElement.parentElement;
        const streamId = parseInt(eventTr.dataset['streamId']);

        let all = true;
        let none = true;
        table.querySelectorAll('input[type="checkbox"]').forEach((c) => {
          all &= c.checked;
          none &= !c.checked;
        });

        checkbox.checked = all;
        checkbox.indeterminate = !(all || none);

        if (checkbox.indeterminate) {
          live[data.CategoryId] ??= [];
          if (e.target.checked) {
            live[data.CategoryId].push(streamId);
          } else {
            live[data.CategoryId] = live[data.CategoryId].filter(id => id != streamId);
          }
        } else if (checkbox.checked) {
          live[data.CategoryId] = [];
        } else {
          delete live[data.CategoryId];
        }
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

  view.addEventListener("viewshow", () => import(
    ApiClient.getUrl("web/ConfigurationPage", {
      name: "Xtream.js",
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.PluginConfig.UniqueId;
    LibraryMenu.setTabs('Live TV', 1, Xtream.getTabs);

    Dashboard.showLoadingMsg();
    const fetchConfig = ApiClient.getPluginConfiguration(pluginId);
    const fetchCategories = ApiClient.fetch({
      url: ApiClient.getUrl('Xtream/LiveCategories'),
      type: 'GET',
    }).then((response) => response.json());

    let liveData;
    Promise.all([fetchConfig, fetchCategories])
      .then(([config, categories]) => {
        liveData = config.LiveTv;
        const live = view.querySelector('#LiveContent');
        for (let i = 0; i < categories.length; ++i) {
          const elem = createRow(liveData, categories[i]);
          live.appendChild(elem);
        }
        Dashboard.hideLoadingMsg();
      });

    view.querySelector('#XtreamLiveForm').addEventListener('submit', (e) => {
      Dashboard.showLoadingMsg();

      ApiClient.getPluginConfiguration(pluginId).then((config) => {
        config.LiveTv = liveData;
        ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
          Dashboard.processPluginConfigurationUpdateResult(result);
        });
      });

      e.preventDefault();
      return false;
    });
  }));
}