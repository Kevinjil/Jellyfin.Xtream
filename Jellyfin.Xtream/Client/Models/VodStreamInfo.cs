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

using Newtonsoft.Json;

#pragma warning disable CS1591
namespace Jellyfin.Xtream.Client.Models;

public class VodStreamInfo
{
    [JsonProperty("info")]
    [JsonConverter(typeof(OnlyObjectConverter<VodInfo>))]
    public VodInfo? Info { get; set; }

    [JsonProperty("movie_data")]
    [JsonConverter(typeof(OnlyObjectConverter<StreamInfo>))]
    public StreamInfo? MovieData { get; set; }
}
