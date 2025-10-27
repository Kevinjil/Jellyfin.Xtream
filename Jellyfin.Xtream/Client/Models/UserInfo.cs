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
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

#pragma warning disable CS1591
namespace Jellyfin.Xtream.Client.Models;

public class UserInfo
{
    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("password")]
    public string Password { get; set; } = string.Empty;

    [JsonProperty("auth")]
    public int Auth { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonConverter(typeof(UnixDateTimeConverter))]
    [JsonProperty("exp_date")]
    public DateTime ExpDate { get; set; }

    [JsonConverter(typeof(StringBoolConverter))]
    [JsonProperty("is_trial")]
    public bool? IsTrial { get; set; }

    [JsonProperty("active_cons")]
    public int ActiveCons { get; set; }

    [JsonConverter(typeof(UnixDateTimeConverter))]
    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("max_connections")]
    public int MaxConnections { get; set; }

    #pragma warning disable CA2227
    [JsonProperty("allowed_output_formats")]
    public ICollection<string> AllowedOutputFormats { get; set; } = new List<string>();
    #pragma warning restore CA2227
}
