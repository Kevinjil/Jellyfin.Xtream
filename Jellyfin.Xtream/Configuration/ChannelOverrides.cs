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

namespace Jellyfin.Xtream.Configuration;

/// <summary>
/// Override configuration for a Live TV channel.
/// </summary>
public class ChannelOverrides
{
    /// <summary>
    /// Gets or sets the TV channel number.
    /// </summary>
    public int? Number { get; set; }

    /// <summary>
    /// Gets or sets the TV channel name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the url of the channel logo.
    /// </summary>
    public string? LogoUrl { get; set; }
}
