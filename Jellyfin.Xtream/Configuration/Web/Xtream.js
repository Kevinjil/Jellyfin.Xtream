const url = (name) =>
  ApiClient.getUrl("configurationpage", {
    name,
  });
const tab = (name) => '/configurationpage?name=' + name + '.html';

$(document).ready(() => {
  const style = document.createElement('link');
  style.rel = 'stylesheet';
  style.href = url('Xtream.css')
  document.head.appendChild(style);
});

const htmlExpand = document.createElement('span');
htmlExpand.ariaHidden = true;
htmlExpand.classList.add('material-icons', 'expand_more');

const createItemRow = (item, state, update) => {
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

const populateItemsTable = (wrapper, table, items) => {
  for (let i = 0; i < items.length; ++i) {
    const item = items[i];
    const state = wrapper.live !== undefined && (wrapper.live.length === 0 || wrapper.live.includes(item.Id));
    const row = createItemRow(item, state, (e) => {
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

const createCategoryRow = (wrapper, category, loadItems) => {
  const tr = document.createElement('tr');
  tr.dataset['categoryId'] = category.Id;

  let td = document.createElement('td');
  const checkbox = document.createElement('input');
  checkbox.type = 'checkbox';
  setCheckboxState(checkbox, wrapper.live);
  const onchange = () => {
    if (checkbox.checked) {
      wrapper.live = [];
    } else {
      wrapper.live = undefined;
    }
  };
  checkbox.onchange = onchange;
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
    loadItems(category.Id).then((items) => {
      populateItemsTable(_wrapper, table, items);
      Dashboard.hideLoadingMsg();
    });
    checkbox.onchange = () => {
      onchange();
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

const populateCategoriesTable = (table, loadConfig, loadCategories, loadItems) => {
  Dashboard.showLoadingMsg();
  const fetchConfig = loadConfig();
  const fetchCategories = loadCategories();

  return Promise.all([fetchConfig, fetchCategories])
    .then(([config, categories]) => {
      const data = config;
      for (let i = 0; i < categories.length; ++i) {
        const category = categories[i];
        const wrapper = {
          get live() { return data[category.Id]; },
          set live(value) {
            data[category.Id] = value;
          },
        }
        const elem = createCategoryRow(wrapper, category, loadItems);
        table.appendChild(elem);
      }
      Dashboard.hideLoadingMsg();
      return data;
    });
}

const fetchJson = (url) => ApiClient.fetch({
  dataType: 'json',
  type: 'GET',
  url: ApiClient.getUrl(url),
});

const filter = (obj, predicate) => Object.keys(obj)
  .filter(key => predicate(obj[key]))
  .reduce((res, key) => (res[key] = obj[key], res), {});

const tabs = [
  {
    href: tab('XtreamCredentials'),
    name: 'Credentials'
  },
  {
    href: tab('XtreamLive'),
    name: 'Live TV'
  },
  {
    href: tab('XtreamLiveOverrides'),
    name: 'TV overrides'
  },
  {
    href: tab('XtreamVod'),
    name: 'Video On-Demand',
  },
  {
    href: tab('XtreamSeries'),
    name: 'Series',
  },
];

const setTabs = (index) => {
  const name = tabs[index].name;
  LibraryMenu.setTabs(name, index, () => tabs);
}

const pluginConfig = {
  UniqueId: '5d774c35-8567-46d3-a950-9bb8227a0c5d'
};

export default {
  fetchJson,
  filter,
  pluginConfig,
  populateCategoriesTable,
  setTabs,
}
