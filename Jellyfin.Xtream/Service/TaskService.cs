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
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service
{
    /// <summary>
    /// A service for dealing with stream information.
    /// </summary>
    public class TaskService
    {
        private readonly ILogger logger;
        private readonly Plugin plugin;
        private readonly ITaskManager taskManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskService"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
        /// <param name="plugin">Instance of the <see cref="Plugin"/> class.</param>
        /// <param name="taskManager">Instance of the <see cref="ITaskManager"/> interface.</param>
        public TaskService(ILogger logger, Plugin plugin, ITaskManager taskManager)
        {
            this.logger = logger;
            this.plugin = plugin;
            this.taskManager = taskManager;
        }

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
            Type? refreshType = FindType(assembly, fullName);
            if (refreshType == null)
            {
                throw new ArgumentException("Refresh task not found");
            }

            // As the type is not publicly visible, use reflection.
            typeof(ITaskManager)
                .GetMethod(nameof(ITaskManager.CancelIfRunningAndQueue), 1, Array.Empty<Type>())?
                .MakeGenericMethod(refreshType)?
                .Invoke(taskManager, Array.Empty<object>());
        }
    }
}
