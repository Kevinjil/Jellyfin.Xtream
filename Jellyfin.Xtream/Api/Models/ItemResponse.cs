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

namespace Jellyfin.Xtream.Api.Models;

/// <summary>
/// A response model for items inside an Xtream category.
/// </summary>
public class ItemResponse
{
    /// <summary>
    /// Gets or sets the Xtream API id of the item.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the item.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether or not catch-up is supported by the item.
    /// </summary>
    public bool HasCatchup { get; set; }

    /// <summary>
    /// Gets or sets the catch-up duration of the item in days.
    /// </summary>
    public int CatchupDuration { get; set; }
}
