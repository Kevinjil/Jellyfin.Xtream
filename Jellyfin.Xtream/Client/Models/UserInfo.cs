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

#pragma warning disable CS1591
namespace Jellyfin.Xtream.Client.Models;

public class UserInfo
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public int Auth { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime ExpDate { get; set; }

    public bool IsTrial { get; set; }

    public int ActiveCons { get; set; }

    public DateTime CreatedAt { get; set; }

    public int MaxConnections { get; set; }

    #pragma warning disable CA2227
    public ICollection<string> AllowedOutputFormats { get; set; } = new List<string>();
    #pragma warning restore CA2227
}
