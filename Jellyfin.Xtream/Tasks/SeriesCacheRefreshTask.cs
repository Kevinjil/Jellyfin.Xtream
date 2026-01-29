// Copyright (C) 2022  Kevin Jilissen

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Xtream.Tasks;

/// <summary>
/// Scheduled task for refreshing series cache with progress tracking.
/// </summary>
public class SeriesCacheRefreshTask : IScheduledTask
{
    /// <summary>
    /// Gets the name of the task.
    /// </summary>
    public string Name => "Refresh Xtream Series Cache";

    /// <summary>
    /// Gets the description of the task.
    /// </summary>
    public string Description => "Pre-fetches and caches all series data for faster navigation. Interval configurable in plugin settings or Scheduled Tasks.";

    /// <summary>
    /// Gets the category of the task.
    /// </summary>
    public string Category => "Xtream";

    /// <summary>
    /// Gets the key of the task.
    /// </summary>
    public string Key => "XtreamSeriesCacheRefresh";

    /// <summary>
    /// Gets a value indicating whether the task is hidden.
    /// </summary>
    public bool IsHidden => false;

    /// <summary>
    /// Gets a value indicating whether the task is enabled.
    /// </summary>
    public bool IsEnabled => true;

    /// <summary>
    /// Gets a value indicating whether the task is logged.
    /// </summary>
    public bool IsLogged => true;

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (Plugin.Instance?.SeriesCacheService == null)
        {
            return;
        }

        await Plugin.Instance.SeriesCacheService.RefreshCacheAsync(progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the task without progress reporting.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync(new Progress<double>(), cancellationToken);
    }

    /// <summary>
    /// Gets the default triggers for this task.
    /// </summary>
    /// <returns>Default triggers (runs every 60 minutes by default).</returns>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        // Default: 60 minutes. Can be changed via plugin settings or Scheduled Tasks UI.
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromMinutes(60).Ticks
            }
        };
    }
}
