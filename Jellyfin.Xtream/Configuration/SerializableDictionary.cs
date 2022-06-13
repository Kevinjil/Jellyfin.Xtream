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

namespace Jellyfin.Xtream.Configuration
{
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
        private const string DefaultItemTag = "Item";

        private const string DefaultKeyTag = "Key";

        private const string DefaultValueTag = "Value";

        private static readonly XmlSerializer KeySerializer = new XmlSerializer(typeof(TKey));

        private static readonly XmlSerializer ValueSerializer = new XmlSerializer(typeof(TValue));

        /// <summary>Initializes a new instance of the
        /// <see cref="SerializableDictionary&lt;TKey, TValue&gt;"/> class.
        /// </summary>
        public SerializableDictionary()
        {
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="SerializableDictionary&lt;TKey, TValue&gt;"/> class.
        /// </summary>
        /// <param name="info">A
        /// <see cref="System.Runtime.Serialization.SerializationInfo"/> object
        /// containing the information required to serialize the
        /// <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>.
        /// </param>
        /// <param name="context">A
        /// <see cref="System.Runtime.Serialization.StreamingContext"/> structure
        /// containing the source and destination of the serialized stream
        /// associated with the
        /// <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>.
        /// </param>
        private SerializableDictionary(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        private string ItemTagName => DefaultItemTag;

        private string KeyTagName => DefaultKeyTag;

        private string ValueTagName => DefaultValueTag;

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
                WriteItem(writer, keyValuePair);
            }
        }

        /// <summary>
        /// Deserializes the dictionary item.
        /// </summary>
        /// <param name="reader">The XML representation of the object.</param>
        private void ReadItem(XmlReader reader)
        {
            reader.ReadStartElement(ItemTagName);
            try
            {
                Add(ReadKey(reader), ReadValue(reader));
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
        private TKey ReadKey(XmlReader reader)
        {
            reader.ReadStartElement(KeyTagName);
            try
            {
                TKey? deserialized = (TKey?)KeySerializer.Deserialize(reader);
                if (deserialized == null)
                {
                    throw new SerializationException("Key cannot be null");
                }

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
        private TValue ReadValue(XmlReader reader)
        {
            reader.ReadStartElement(ValueTagName);
            try
            {
                TValue? deserialized = (TValue?)ValueSerializer.Deserialize(reader);
                if (deserialized == null)
                {
                    throw new SerializationException("Value cannot be null");
                }

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
        private void WriteItem(XmlWriter writer, KeyValuePair<TKey, TValue> keyValuePair)
        {
            writer.WriteStartElement(ItemTagName);
            try
            {
                WriteKey(writer, keyValuePair.Key);
                WriteValue(writer, keyValuePair.Value);
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
        private void WriteKey(XmlWriter writer, TKey key)
        {
            writer.WriteStartElement(KeyTagName);
            try
            {
                KeySerializer.Serialize(writer, key);
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
        private void WriteValue(XmlWriter writer, TValue value)
        {
            writer.WriteStartElement(ValueTagName);
            try
            {
                ValueSerializer.Serialize(writer, value);
            }
            finally
            {
                writer.WriteEndElement();
            }
        }
    }
}
