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

public class StreamInfo
{
    [JsonProperty("num")]
    public int Num { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("stream_type")]
    public string StreamType { get; set; } = string.Empty;

    [JsonProperty("stream_id")]
    public int StreamId { get; set; }

    [JsonProperty("stream_icon")]
    public string StreamIcon { get; set; } = string.Empty;

    [JsonProperty("epg_channel_id")]
    public string EpgChannelId { get; set; } = string.Empty;

    [JsonProperty("added")]
    public string Added { get; set; } = string.Empty;

    [JsonProperty("category_id")]
    public int? CategoryId { get; set; }

    [JsonProperty("container_extension")]
    public string ContainerExtension { get; set; } = string.Empty;

    [JsonProperty("custom_sid")]
    public string CustomSid { get; set; } = string.Empty;

    [JsonProperty("tv_archive")]
    public bool TvArchive { get; set; }

    [JsonProperty("direct_source")]
    public string DirectSource { get; set; } = string.Empty;

    [JsonProperty("tv_archive_duration")]
    public int TvArchiveDuration { get; set; }
}
