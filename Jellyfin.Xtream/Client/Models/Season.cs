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
using Newtonsoft.Json;

#pragma warning disable CS1591
namespace Jellyfin.Xtream.Client.Models;

public class Season
{
    [JsonProperty("air_date")]
    public DateTime AirDate { get; set; }

    [JsonProperty("episode_count")]
    public int EpisodeCount { get; set; }

    [JsonProperty("id")]
    public int SeasonId { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("overview")]
    public string Overview { get; set; } = string.Empty;

    [JsonProperty("season_number")]
    public int Cast { get; set; }

    [JsonProperty("cover")]
    public string Cover { get; set; } = string.Empty;

    [JsonProperty("cover_big")]
    public string CoverBig { get; set; } = string.Empty;
}
