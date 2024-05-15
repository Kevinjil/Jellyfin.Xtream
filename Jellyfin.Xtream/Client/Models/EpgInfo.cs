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
using Newtonsoft.Json.Converters;

#pragma warning disable CS1591
namespace Jellyfin.Xtream.Client.Models;

public class EpgInfo
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("epg_id")]
    public int EpgId { get; set; }

    [JsonConverter(typeof(Base64Converter))]
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("lang")]
    public string Language { get; set; } = string.Empty;

    [JsonConverter(typeof(UnixDateTimeConverter))]
    [JsonProperty("start_timestamp")]
    public DateTime Start { get; set; }

    [JsonProperty("start")]
    public DateTime StartLocalTime { get; set; }

    [JsonConverter(typeof(UnixDateTimeConverter))]
    [JsonProperty("stop_timestamp")]
    public DateTime End { get; set; }

    [JsonConverter(typeof(Base64Converter))]
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("channel_id")]
    public string ChannelId { get; set; } = string.Empty;

    [JsonProperty("now_playing")]
    public bool NowPlaying { get; set; }

    [JsonProperty("has_archive")]
    public bool HasArchive { get; set; }
}
