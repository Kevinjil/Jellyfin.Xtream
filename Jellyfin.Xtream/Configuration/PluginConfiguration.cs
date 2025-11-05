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
namespace Jellyfin.Xtream.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the base url including protocol and trailing slash.
    /// </summary>
    public string BaseUrl { get; set; } = "https://example.com";

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    public string Password { get; set; } = string.Empty;

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
    /// Gets or sets a value indicating whether the Video On-demand channel is visible.
    /// </summary>
    public bool IsTmdbVodOverride { get; set; } = true;

    /// <summary>
    /// Gets or sets the format for the catch-up URL.
    /// </summary>
    public string CatchupUrlFormat { get; set; } = "{0}/streaming/timeshift.php?username={1}&password={2}&stream={3}&start={4}&duration={5}";

    /// <summary>
    /// Gets or sets the channels displayed in Live TV.
    /// </summary>
    public SerializableDictionary<int, HashSet<int>> LiveTv { get; set; } = [];

    /// <summary>
    /// Gets or sets the streams displayed in VOD.
    /// </summary>
    public SerializableDictionary<int, HashSet<int>> Vod { get; set; } = [];

    /// <summary>
    /// Gets or sets the streams displayed in Series.
    /// </summary>
    public SerializableDictionary<int, HashSet<int>> Series { get; set; } = [];

    /// <summary>
    /// Gets or sets the channel override configuration for Live TV.
    /// </summary>
    public SerializableDictionary<int, ChannelOverrides> LiveTvOverrides { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to use a single XMLTV endpoint instead of per-channel EPG API calls.
    /// </summary>
    public bool UseXmlTv { get; set; } = false;

    /// <summary>
    /// Gets or sets the XMLTV URL. If empty the client will use the default path '/xmltv.php?username=...&amp;password=...'.
    /// </summary>
    public string XmlTvUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cache duration (in minutes) for the downloaded XMLTV file stored in memory.
    /// </summary>
    public int XmlTvCacheMinutes { get; set; } = 10;

    /// <summary>
    /// Gets or sets the number of days of historical EPG data to fetch. If 0 or negative, will use the maximum archive duration of all channels.
    /// </summary>
    public int XmlTvHistoricalDays { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether to append time range parameters to XMLTV URL (timeshift=1&amp;from=...&amp;to=...).
    /// </summary>
    public bool XmlTvSupportsTimeshift { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to cache the XMLTV file to disk.
    /// </summary>
    public bool XmlTvDiskCache { get; set; } = false;

    /// <summary>
    /// Gets or sets the path where the XMLTV cache file will be stored. If empty, uses the plugin's data directory.
    /// </summary>
    public string XmlTvCachePath { get; set; } = string.Empty;
}
#pragma warning restore CA2227
