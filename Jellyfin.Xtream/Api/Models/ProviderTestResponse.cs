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

namespace Jellyfin.Xtream.Api.Models;

/// <summary>
/// A response model for Xtream provider tests.
/// </summary>
public class ProviderTestResponse
{
    /// <summary>
    /// Gets or sets the status of provider.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account expiry date.
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Gets or sets the current simultaneous connections to the provider.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Gets or sets the maximum simultaneous connections supported by the provider.
    /// </summary>
    public int MaxConnections { get; set; }

    /// <summary>
    /// Gets or sets the server time.
    /// </summary>
    public DateTime ServerTime { get; set; }

    /// <summary>
    /// Gets or sets the server time zone.
    /// </summary>
    public string ServerTimezone { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether or not MPEG-TS is supported by the provider.
    /// </summary>
    public bool SupportsMpegTs { get; set; }
}
