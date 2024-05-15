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
using System.Text;
using Newtonsoft.Json;

namespace Jellyfin.Xtream.Client;

/// <summary>
/// Class Base64Converter converts strings from and to base64.
/// </summary>
public class Base64Converter : JsonConverter
{
    /// <inheritdoc />
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(string);
    }

    /// <inheritdoc />
    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.Value == null)
        {
            throw new ArgumentException("Value cannot be null.");
        }

        byte[] bytes = Convert.FromBase64String((string)reader.Value);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            throw new ArgumentException("Value cannot be null.");
        }

        byte[] bytes = Encoding.UTF8.GetBytes((string)value);
        writer.WriteValue(Convert.ToBase64String(bytes));
    }
}
