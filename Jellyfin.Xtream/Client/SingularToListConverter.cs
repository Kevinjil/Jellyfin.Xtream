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

namespace Jellyfin.Xtream.Client;

/// <summary>
/// Class SingularToListConverter converts singular tokens to lists.
/// </summary>
/// <typeparam name="T">The object which should be converted.</typeparam>
public class SingularToListConverter<T> : JsonConverter
{
    /// <inheritdoc/>
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(T);
    }

    /// <inheritdoc/>
    public override ICollection<T>? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.StartObject:
                T? result = serializer.Deserialize<T>(reader);
                if (result is null)
                {
                    return null;
                }

                return [result];
            case JsonToken.StartArray:
                return serializer.Deserialize<List<T>>(reader);
            case JsonToken.String:
                if (typeof(T) == typeof(string))
                {
                    goto case JsonToken.StartObject;
                }

                goto default;
            case JsonToken.Null:
                return [];
            default:
                throw new JsonReaderException("The JsonReader points to an unexpected point.");
        }
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is T typedValue)
        {
            value = new List<T> { typedValue };
        }

        serializer.Serialize(writer, value);
    }
}
