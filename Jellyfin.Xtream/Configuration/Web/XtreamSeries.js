export default function (view) {
  view.addEventListener("viewshow", () => import(
    ApiClient.getUrl("web/ConfigurationPage", {
      name: "Xtream.js",
    })
  ).then((Xtream) => Xtream.default
  ).then((Xtream) => {
    const pluginId = Xtream.pluginConfig.UniqueId;
    Xtream.setTabs(4);

    const getConfig = ApiClient.getPluginConfiguration(pluginId);
    const visible = view.querySelector("#Visible");
    const flattenSeriesView = view.querySelector("#FlattenSeriesView");
    const enableCaching = view.querySelector("#EnableCaching");
    const cacheOptionsContainer = view.querySelector("#CacheOptionsContainer");
    const cacheRefreshMinutes = view.querySelector("#SeriesCacheRefreshMinutes");
    const refreshCacheBtn = view.querySelector("#RefreshCacheBtn");
    const clearCacheBtn = view.querySelector("#ClearCacheBtn");
    const cacheStatusContainer = view.querySelector("#CacheStatusContainer");
    const cacheProgressFill = view.querySelector("#CacheProgressFill");
    const cacheStatusText = view.querySelector("#CacheStatusText");
    const cacheParallelism = view.querySelector("#CacheRefreshParallelism");
    const cacheParallelismValue = view.querySelector("#CacheParallelismValue");
    const cacheMinDelay = view.querySelector("#CacheRefreshMinDelayMs");
    const cacheMinDelayValue = view.querySelector("#CacheMinDelayValue");

    // Artwork Injector settings
    const useTvdbForSeriesMetadata = view.querySelector("#UseTvdbForSeriesMetadata");
    const tvdbOptionsContainer = view.querySelector("#TvdbOptionsContainer");
    const tvdbTitleOverrides = view.querySelector("#TvdbTitleOverrides");

    // Toggle cache options visibility
    function updateCacheOptionsVisibility() {
      cacheOptionsContainer.style.display = enableCaching.checked ? 'block' : 'none';
    }

    // Toggle TVDb options visibility
    function updateTvdbOptionsVisibility() {
      tvdbOptionsContainer.style.display = useTvdbForSeriesMetadata.checked ? 'block' : 'none';
    }

    // Update parallelism display value
    function updateParallelismDisplay() {
      cacheParallelismValue.textContent = cacheParallelism.value;
    }

    // Update min delay display value
    function updateMinDelayDisplay() {
      cacheMinDelayValue.textContent = cacheMinDelay.value;
    }

    enableCaching.addEventListener('change', updateCacheOptionsVisibility);
    cacheParallelism.addEventListener('input', updateParallelismDisplay);
    cacheMinDelay.addEventListener('input', updateMinDelayDisplay);
    useTvdbForSeriesMetadata.addEventListener('change', updateTvdbOptionsVisibility);

    getConfig.then((config) => {
      visible.checked = config.IsSeriesVisible;
      flattenSeriesView.checked = config.FlattenSeriesView || false;
      enableCaching.checked = config.EnableSeriesCaching !== false;
      cacheRefreshMinutes.value = config.SeriesCacheExpirationMinutes || 600;
      cacheParallelism.value = config.CacheRefreshParallelism || 3;
      cacheMinDelay.value = config.CacheRefreshMinDelayMs !== undefined ? config.CacheRefreshMinDelayMs : 100;

      // Artwork Injector settings
      useTvdbForSeriesMetadata.checked = config.UseTvdbForSeriesMetadata !== false;
      tvdbTitleOverrides.value = config.TvdbTitleOverrides || '';

      updateCacheOptionsVisibility();
      updateParallelismDisplay();
      updateMinDelayDisplay();
      updateTvdbOptionsVisibility();
    });

    // Refresh Now button handler
    refreshCacheBtn.addEventListener('click', () => {
      // Check if already refreshing
      if (refreshCacheBtn.disabled) {
        Dashboard.alert('Cache refresh is already in progress. Please wait for it to complete.');
        return;
      }

      refreshCacheBtn.disabled = true;
      refreshCacheBtn.querySelector('span').textContent = 'Starting...';

      ApiClient.fetch({
        url: ApiClient.getUrl('Xtream/SeriesCacheRefresh'),
        type: 'POST',
        dataType: 'json'
      })
        .then(result => {
          // Handle both Response object and parsed JSON
          if (result && typeof result.json === 'function') {
            return result.json();
          }
          return result;
        })
        .then(result => {
          if (result.Success) {
            cacheStatusText.textContent = 'Refresh started...';
            cacheStatusText.style.color = '#00a4dc';
            cacheStatusContainer.style.display = 'block';
          } else {
            Dashboard.alert(result.Message || 'Failed to start refresh');
          }
        })
        .catch(err => {
          console.error('Failed to trigger cache refresh:', err);
          Dashboard.alert('Failed to trigger cache refresh: ' + err.message);
        })
        .finally(() => {
          refreshCacheBtn.disabled = false;
          refreshCacheBtn.querySelector('span').textContent = 'Refresh Now';
        });
    });

    // Clear Cache button handler
    clearCacheBtn.addEventListener('click', () => {
      // Check if a refresh is currently running
      Xtream.fetchJson('Xtream/SeriesCacheStatus')
        .then((status) => {
          let confirmMessage = 'Are you sure you want to clear the cache? Next refresh will fetch all data from scratch.';
          if (status.IsRefreshing) {
            confirmMessage = 'A cache refresh is currently in progress. Clearing the cache will stop the refresh. Are you sure you want to continue?';
          }

          if (!confirm(confirmMessage)) {
            return;
          }

          clearCache();
        })
        .catch((err) => {
          console.error('Failed to check cache status:', err);
          // If status check fails, still allow clearing with default message
          if (confirm('Are you sure you want to clear the cache? Next refresh will fetch all data from scratch.')) {
            clearCache();
          }
        });
    });

    function clearCache() {
      console.log('clearCache() called');
      clearCacheBtn.disabled = true;
      clearCacheBtn.querySelector('span').textContent = 'Clearing...';

      fetch(ApiClient.getUrl('Xtream/SeriesCacheClear'), {
        method: 'POST',
        headers: ApiClient.defaultRequestHeaders()
      })
        .then(response => {
          console.log('Clear cache response status:', response.status);
          if (!response.ok) {
            throw new Error('Server returned ' + response.status);
          }
          return response.json();
        })
        .then(result => {
          console.log('Clear cache result:', result);
          if (result.Success) {
            Dashboard.alert(result.Message || 'Cache cleared successfully');
            cacheStatusText.textContent = 'Cache cleared';
            cacheStatusText.style.color = '#a0a0a0';
            cacheProgressFill.style.width = '0%';
          } else {
            Dashboard.alert(result.Message || 'Failed to clear cache');
          }
        })
        .catch(err => {
          console.error('Failed to clear cache:', err);
          Dashboard.alert('Failed to clear cache: ' + err.message);
        })
        .finally(() => {
          console.log('clearCache() finally block - re-enabling button');
          clearCacheBtn.disabled = false;
          clearCacheBtn.querySelector('span').textContent = 'Clear Cache';
        });
    }

    // Poll cache status every 2 seconds
    let statusPollInterval;
    function updateCacheStatus() {
      Xtream.fetchJson('Xtream/SeriesCacheStatus')
        .then((status) => {
          if (status.IsRefreshing || status.Progress > 0 || status.IsCachePopulated) {
            cacheStatusContainer.style.display = 'block';
            const progressPercent = Math.round(status.Progress * 100);
            cacheProgressFill.style.width = progressPercent + '%';
            cacheStatusText.textContent = status.Status || 'Idle';

            if (status.IsRefreshing) {
              cacheStatusText.style.color = '#00a4dc';
              refreshCacheBtn.disabled = true;
              // Clear Cache button stays enabled - it will cancel the refresh
            } else {
              refreshCacheBtn.disabled = false;
              if (status.Progress >= 1.0) {
                cacheStatusText.style.color = '#4caf50';
              } else {
                cacheStatusText.style.color = '#a0a0a0';
              }
            }
          } else {
            cacheStatusContainer.style.display = 'none';
            refreshCacheBtn.disabled = false;
          }
        })
        .catch(() => {
          // Silently fail if API is not available
        });
    }

    // Start polling when view is shown
    updateCacheStatus();
    statusPollInterval = setInterval(updateCacheStatus, 2000);

    // Clean up interval when view is hidden
    view.addEventListener("viewhide", () => {
      if (statusPollInterval) {
        clearInterval(statusPollInterval);
      }
    });
    const table = view.querySelector('#SeriesContent');
    Xtream.populateCategoriesTable(
      table,
      () => getConfig.then((config) => config.Series),
      () => Xtream.fetchJson('Xtream/SeriesCategories'),
      (categoryId) => Xtream.fetchJson(`Xtream/SeriesCategories/${categoryId}`),
    ).then((data) => {
      view.querySelector('#XtreamSeriesForm').addEventListener('submit', (e) => {
        e.preventDefault();

        // Guard: only save if categories actually loaded into the table
        if (table.querySelectorAll('tr[data-category-id]').length === 0) {
          Dashboard.alert('Cannot save: series categories failed to load. Please check your credentials and refresh the page.');
          return false;
        }

        Dashboard.showLoadingMsg();

        // Validate configuration before saving
        let warnings = [];
        if (visible.checked && flattenSeriesView.checked) {
          // In flatten mode, check if any categories have series selected
          let hasAnySelection = false;
          for (let categoryId in data) {
            if (data[categoryId] !== undefined) {
              hasAnySelection = true;
              break;
            }
          }
          if (!hasAnySelection) {
            warnings.push('Series visibility is enabled but no categories have series selected. Users will see an empty list.');
          }
        }

        if (warnings.length > 0) {
          let proceed = confirm('Configuration warnings:\n\n' + warnings.join('\n\n') + '\n\nDo you want to save anyway?');
          if (!proceed) {
            Dashboard.hideLoadingMsg();
            e.preventDefault();
            return false;
          }
        }

        ApiClient.getPluginConfiguration(pluginId).then((config) => {
          config.IsSeriesVisible = visible.checked;
          config.FlattenSeriesView = flattenSeriesView.checked;
          config.EnableSeriesCaching = enableCaching.checked;

          // Validate refresh frequency (min: 10, max: 1380 to prevent exceeding 24h cache expiration)
          let refreshMinutes = parseInt(cacheRefreshMinutes.value, 10) || 600;
          if (refreshMinutes < 10) refreshMinutes = 10;
          if (refreshMinutes > 1380) refreshMinutes = 1380;
          config.SeriesCacheExpirationMinutes = refreshMinutes;

          // Validate parallelism (1-10)
          let parallelism = parseInt(cacheParallelism.value, 10) || 3;
          if (parallelism < 1) parallelism = 1;
          if (parallelism > 10) parallelism = 10;
          config.CacheRefreshParallelism = parallelism;

          // Validate min delay (0-1000)
          let minDelay = parseInt(cacheMinDelay.value, 10) || 100;
          if (minDelay < 0) minDelay = 0;
          if (minDelay > 1000) minDelay = 1000;
          config.CacheRefreshMinDelayMs = minDelay;

          // Artwork Injector settings
          config.UseTvdbForSeriesMetadata = useTvdbForSeriesMetadata.checked;
          config.TvdbTitleOverrides = tvdbTitleOverrides.value;

          config.Series = data;
          console.log('Saving series configuration:', JSON.stringify(data, null, 2));
          ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
            Dashboard.processPluginConfigurationUpdateResult(result);
          });
        });
      });
    }).catch((error) => {
      console.error('Failed to load series categories:', error);
      Dashboard.hideLoadingMsg();
      // Clear any previous content/errors before showing new error
      table.innerHTML = '';
      const errorRow = document.createElement('tr');
      const errorCell = document.createElement('td');
      errorCell.colSpan = 3;
      errorCell.style.color = '#ff6b6b';
      errorCell.style.padding = '16px';
      errorCell.innerHTML = 'Failed to load categories. Please check:<br>' +
        '1. Xtream credentials are configured (Credentials tab)<br>' +
        '2. Xtream server is accessible<br>' +
        '3. Browser console for detailed errors';
      errorRow.appendChild(errorCell);
      table.appendChild(errorRow);
    });
  }));
}
