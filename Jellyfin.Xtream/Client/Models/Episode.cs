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

public class Episode
{
    [JsonProperty("id")]
    public int EpisodeId { get; set; }

    [JsonProperty("episode_num")]
    public int EpisodeNum { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("container_extension")]
    public string ContainerExtension { get; set; } = string.Empty;

    [JsonConverter(typeof(OnlyObjectConverter<EpisodeInfo>))]
    [JsonProperty("info")]
    public EpisodeInfo? Info { get; set; } = new EpisodeInfo();

    [JsonProperty("custom_sid")]
    public string CustomSid { get; set; } = string.Empty;

    [JsonProperty("added")]
    public long Added { get; set; }

    [JsonProperty("season")]
    public int Season { get; set; }

    [JsonProperty("direct_source")]
    public string DirectSource { get; set; } = string.Empty;
}
