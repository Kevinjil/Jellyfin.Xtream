export default function (view) {
  const htmlExpand = document.createElement('span');
  htmlExpand.ariaHidden = true;
  htmlExpand.classList.add('material-icons', 'expand_more');

  const createStreamRow = (item, state, update) => {
    const tr = document.createElement('tr');
    tr.dataset['itemId'] = item.Id;

    let td = document.createElement('td');
    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    checkbox.checked = state;
    checkbox.onchange = update;
    td.appendChild(checkbox);
    tr.appendChild(td);

    td = document.createElement('td');
    const label = document.createElement('label');
    label.innerText = item.Name;
    td.appendChild(label);
    tr.appendChild(td);

    td = document.createElement('td');
    if (item.HasCatchup) {
      td.title = `Catch-up supported for ${item.CatchupDuration} days.`;

      let span = document.createElement('span');
      span.innerText = item.CatchupDuration;
      td.appendChild(span);

      span = document.createElement('span');
      span.ariaHidden = true;
      span.classList.add('material-icons', 'timer');
      td.appendChild(span);
    }
    tr.appendChild(td);

    return tr;
  }

  const populateCategoryTable = (wrapper, table, items) => {
    for (let i = 0; i < items.length; ++i) {
      const item = items[i];
      const state = wrapper.live !== undefined && (wrapper.live.length === 0 || wrapper.live.includes(item.Id));
      const row = createStreamRow(item, state, (e) => {
        let live = wrapper.live;
        if (e.target.checked) {
          live ??= [];
          live.push(item.Id);
          if (items.every(s => live.includes(s.Id))) {
            live = [];
          }
        } else {
          if (live.length === 0) {
            live = items.map(s => s.Id);
          }
          live = live.filter(id => id != item.Id);
          if (live.length === 0) {
            live = undefined;
          }
        }
        wrapper.live = live;
      });
      table.appendChild(row);
    }
  }

  const setCheckboxState = (checkbox, live) => {
    checkbox.indeterminate = live !== undefined && live.length > 0;
    checkbox.checked = live !== undefined && live.length === 0;
  }

  const createRow = (wrapper, category) => {
    const tr = document.createElement('tr');
    tr.dataset['categoryId'] = category.Id;

    let td = document.createElement('td');
    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    setCheckboxState(checkbox, wrapper.live);
    td.appendChild(checkbox);
    tr.appendChild(td);

    const _wrapper = {
      get live() { return wrapper.live; },
      set live(value) {
        wrapper.live = value;
        setCheckboxState(checkbox, wrapper.live);
      },
    }

    td = document.createElement('td');
    td.innerHTML = category.Name;
    tr.appendChild(td);

    td = document.createElement('td');
    const expand = document.createElement('button');
    expand.type = 'button';
    expand.classList.add('paper-icon-button-light');
    expand.appendChild(htmlExpand.cloneNode(true));
    expand.onclick = (e) => {
      e.preventDefault();
      const originalClick = expand.onclick;

      Dashboard.showLoadingMsg();
      expand.firstElementChild.classList.replace('expand_more', 'expand_less');
      const table = document.createElement('table');
      ApiClient.fetch({
        url: ApiClient.getUrl(`Xtream/LiveCategories/${category.Id}`),
        type: 'GET',
      }).then((response) => response.json())
        .then((items) => {
          populateCategoryTable(_wrapper, table, items);
          Dashboard.hideLoadingMsg();
        });
      checkbox.onchange = () => {
        if (checkbox.checked) {
          wrapper.live = [];
        } else {
          wrapper.live = undefined;
        }
        table.querySelectorAll('input[type="checkbox"]').forEach((c) => c.checked = checkbox.checked);
      };
      td.appendChild(table);

      expand.onclick = () => {
        expand.onclick = originalClick;

        Dashboard.showLoadingMsg();
        expand.firstElementChild.classList.replace('expand_less', 'expand_more');
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
          const category = categories[i];
          const wrapper = {
            get live() { return liveData[category.Id]; },
            set live(value) {
              liveData[category.Id] = value;
            },
          }
          const elem = createRow(wrapper, category);
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