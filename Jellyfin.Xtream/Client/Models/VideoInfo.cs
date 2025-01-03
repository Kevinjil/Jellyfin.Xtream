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

public class VideoInfo
{
    [JsonProperty("index")]
    public int Index { get; set; }

    [JsonProperty("codec_name")]
    public string CodecName { get; set; } = string.Empty;

    [JsonProperty("profile")]
    public string Profile { get; set; } = string.Empty;

    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("display_aspect_ratio")]
    public string AspectRatio { get; set; } = string.Empty;

    [JsonProperty("pix_fmt")]
    public string PixelFormat { get; set; } = string.Empty;

    [JsonProperty("level")]
    public int Level { get; set; }

    [JsonProperty("color_range")]
    public string ColorRange { get; set; } = string.Empty;

    [JsonProperty("color_space")]
    public string ColorSpace { get; set; } = string.Empty;

    [JsonProperty("color_transfer")]
    public string ColorTransfer { get; set; } = string.Empty;

    [JsonProperty("color_primaries")]
    public string ColorPrimaries { get; set; } = string.Empty;

    [JsonProperty("is_avc")]
    public bool IsAVC { get; set; }

    [JsonProperty("bits_per_raw_sample")]
    public int BitsPerRawSample { get; set; }
}
