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
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Jellyfin.Xtream.Configuration;

/// <summary>
/// Dictionary implementation that can be serialized to and from XML.
/// </summary>
/// <typeparam name="TKey">The dictionary key type.</typeparam>
/// <typeparam name="TValue">The dictionary value type.</typeparam>
[Serializable]
[XmlRoot("Dictionary")]
public sealed class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IXmlSerializable
where TKey : notnull
{
    private const string ItemTag = "Item";

    private const string KeyTag = "Key";

    private const string ValueTag = "Value";

    private static readonly XmlSerializer _keySerializer = new(typeof(TKey));

    private static readonly XmlSerializer _valueSerializer = new(typeof(TValue));

    /// <summary>Initializes a new instance of the
    /// <see cref="SerializableDictionary&lt;TKey, TValue&gt;"/> class.
    /// </summary>
    public SerializableDictionary()
    {
    }

    /// <inheritdoc />
    public XmlSchema? GetSchema()
    {
        return null;
    }

    /// <inheritdoc />
    public void ReadXml(XmlReader reader)
    {
        var wasEmpty = reader.IsEmptyElement;

        reader.Read();
        if (wasEmpty)
        {
            return;
        }

        try
        {
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                ReadItem(reader);
                reader.MoveToContent();
            }
        }
        finally
        {
            reader.ReadEndElement();
        }
    }

    /// <inheritdoc />
    public void WriteXml(XmlWriter writer)
    {
        foreach (var keyValuePair in this)
        {
            SerializableDictionary<TKey, TValue>.WriteItem(writer, keyValuePair);
        }
    }

    /// <summary>
    /// Deserializes the dictionary item.
    /// </summary>
    /// <param name="reader">The XML representation of the object.</param>
    private void ReadItem(XmlReader reader)
    {
        reader.ReadStartElement(ItemTag);
        try
        {
            Add(SerializableDictionary<TKey, TValue>.ReadKey(reader), SerializableDictionary<TKey, TValue>.ReadValue(reader));
        }
        finally
        {
            reader.ReadEndElement();
        }
    }

    /// <summary>
    /// De-serializes the dictionary item's key.
    /// </summary>
    /// <param name="reader">The XML representation of the object.</param>
    /// <returns>The dictionary item's key.</returns>
    private static TKey ReadKey(XmlReader reader)
    {
        reader.ReadStartElement(KeyTag);
        try
        {
            TKey deserialized = (TKey?)_keySerializer.Deserialize(reader) ?? throw new SerializationException("Key cannot be null");
            return deserialized;
        }
        finally
        {
            reader.ReadEndElement();
        }
    }

    /// <summary>
    /// Deserializes the dictionary item's value.
    /// </summary>
    /// <param name="reader">The XML representation of the object.</param>
    /// <returns>The dictionary item's value.</returns>
    private static TValue ReadValue(XmlReader reader)
    {
        reader.ReadStartElement(ValueTag);
        try
        {
            TValue deserialized = (TValue?)_valueSerializer.Deserialize(reader) ?? throw new SerializationException("Value cannot be null");
            return deserialized;
        }
        finally
        {
            reader.ReadEndElement();
        }
    }

    /// <summary>
    /// Serializes the dictionary item.
    /// </summary>
    /// <param name="writer">The XML writer to serialize to.</param>
    /// <param name="keyValuePair">The key/value pair.</param>
    private static void WriteItem(XmlWriter writer, KeyValuePair<TKey, TValue> keyValuePair)
    {
        writer.WriteStartElement(ItemTag);
        try
        {
            SerializableDictionary<TKey, TValue>.WriteKey(writer, keyValuePair.Key);
            SerializableDictionary<TKey, TValue>.WriteValue(writer, keyValuePair.Value);
        }
        finally
        {
            writer.WriteEndElement();
        }
    }

    /// <summary>
    /// Serializes the dictionary item's key.
    /// </summary>
    /// <param name="writer">The XML writer to serialize to.</param>
    /// <param name="key">The dictionary item's key.</param>
    private static void WriteKey(XmlWriter writer, TKey key)
    {
        writer.WriteStartElement(KeyTag);
        try
        {
            _keySerializer.Serialize(writer, key);
        }
        finally
        {
            writer.WriteEndElement();
        }
    }

    /// <summary>
    /// Serializes the dictionary item's value.
    /// </summary>
    /// <param name="writer">The XML writer to serialize to.</param>
    /// <param name="value">The dictionary item's value.</param>
    private static void WriteValue(XmlWriter writer, TValue value)
    {
        writer.WriteStartElement(ValueTag);
        try
        {
            _valueSerializer.Serialize(writer, value);
        }
        finally
        {
            writer.WriteEndElement();
        }
    }
}
