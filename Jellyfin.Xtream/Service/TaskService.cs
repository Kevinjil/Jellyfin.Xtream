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
using System.Linq;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// A service for dealing with stream information.
/// </summary>
/// <param name="taskManager">Instance of the <see cref="ITaskManager"/> interface.</param>
public class TaskService(ITaskManager taskManager)
{
    private static Type? FindType(string assembly, string fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a =>
                !a.IsDynamic &&
                (a.FullName?.StartsWith($"{assembly},", StringComparison.InvariantCulture) ?? false))
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t?.FullName == fullName);
    }

    /// <summary>
    /// Executes a task from the given assembly and name.
    /// </summary>
    /// <param name="assembly">The name of the assembly to search in for the type.</param>
    /// <param name="fullName">The full name of the task type.</param>
    /// <exception cref="ArgumentException">If the task type is not found.</exception>
    public void CancelIfRunningAndQueue(string assembly, string fullName)
    {
        Type refreshType = FindType(assembly, fullName) ?? throw new ArgumentException("Refresh task not found");

        // As the type is not publicly visible, use reflection.
        typeof(ITaskManager)
            .GetMethod(nameof(ITaskManager.CancelIfRunningAndQueue), 1, [])?
            .MakeGenericMethod(refreshType)?
            .Invoke(taskManager, []);
    }
}
