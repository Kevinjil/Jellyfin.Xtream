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

using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

#pragma warning disable CA2227
namespace Jellyfin.Xtream.Configuration
{
    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration()
        {
            // set default options here
            BaseUrl = "https://example.com";
            Username = string.Empty;
            Password = string.Empty;
            IsCatchupVisible = false;
            IsSeriesVisible = false;
            IsVodVisible = false;
            LiveTv = new SerializableDictionary<int, HashSet<int>>();
            Vod = new SerializableDictionary<int, HashSet<int>>();
            Series = new SerializableDictionary<int, HashSet<int>>();
        }

        /// <summary>
        /// Gets or sets the base url including protocol and trailing slash.
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Catch-up channel is visible.
        /// </summary>
        public bool IsCatchupVisible { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Series channel is visible.
        /// </summary>
        public bool IsSeriesVisible { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Video On-demand channel is visible.
        /// </summary>
        public bool IsVodVisible { get; set; }

        /// <summary>
        /// Gets or sets the channels displayed in Live TV.
        /// </summary>
        public SerializableDictionary<int, HashSet<int>> LiveTv { get; set; }

        /// <summary>
        /// Gets or sets the streams displayed in VOD.
        /// </summary>
        public SerializableDictionary<int, HashSet<int>> Vod { get; set; }

        /// <summary>
        /// Gets or sets the streams displayed in Series.
        /// </summary>
        public SerializableDictionary<int, HashSet<int>> Series { get; set; }
    }
}
#pragma warning restore CA2227
