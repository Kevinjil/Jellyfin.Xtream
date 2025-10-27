﻿// Copyright (C) 2022  Kevin Jilissen

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

#pragma warning disable CS1591
using Newtonsoft.Json;

namespace Jellyfin.Xtream.Client.Models;

public class PlayerApi
{
    [JsonProperty("user_info")]
    public UserInfo UserInfo { get; set; } = new UserInfo();

    [JsonProperty("server_info")]
    public ServerInfo ServerInfo { get; set; } = new ServerInfo();
}
