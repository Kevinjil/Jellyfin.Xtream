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

#pragma warning disable CA1815
#pragma warning disable CA1819
namespace Jellyfin.Xtream.Service;

/// <summary>
/// A struct which holds information of parsed stream names.
/// </summary>
/// <param name="title">The parsed title.</param>
/// <param name="tags">The parsed tags.</param>
public readonly struct ParsedName(string title, string[] tags)
{
    /// <summary>
    /// Gets the parsed title.
    /// </summary>
    public string Title { get; init; } = title;

    /// <summary>
    /// Gets the parsed tags.
    /// </summary>
    public string[] Tags { get; init; } = tags;
}
