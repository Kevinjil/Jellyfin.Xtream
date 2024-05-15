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
using Newtonsoft.Json;

#pragma warning disable CS1591
#pragma warning disable CA2227
namespace Jellyfin.Xtream.Client.Models;

public class EpgListings
{
    [JsonProperty("epg_listings")]
    public ICollection<EpgInfo> Listings { get; set; } = new List<EpgInfo>();
}
#pragma warning restore CA2227
#pragma warning restore CS1591
