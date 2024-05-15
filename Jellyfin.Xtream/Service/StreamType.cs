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

namespace Jellyfin.Xtream.Service;

/// <summary>
/// An enum describing the Xtream stream types.
/// </summary>
public enum StreamType : int
{
    /// <summary>
    /// Live IPTV.
    /// </summary>
    Live = 0,

    /// <summary>
    /// Catch up IPTV.
    /// </summary>
    CatchUp = 1,

    /// <summary>
    /// On-demand series grouped in seasons and episodes.
    /// </summary>
    Series = 2,

    /// <summary>
    /// Video on-demand.
    /// </summary>
    Vod = 3,
}
