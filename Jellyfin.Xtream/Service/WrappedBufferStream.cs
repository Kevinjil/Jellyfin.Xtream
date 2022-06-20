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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service
{
    /// <summary>
    /// Stream which writes to a self-overwriting internal buffer.
    /// </summary>
    public class WrappedBufferStream : Stream
    {
        private readonly byte[] buffer;

        private long position;
        private long totalBytesWritten;

        /// <summary>
        /// Initializes a new instance of the <see cref="WrappedBufferStream"/> class.
        /// </summary>
        /// <param name="bufferSize">Size in bytes of the internal buffer.</param>
        public WrappedBufferStream(int bufferSize)
        {
            this.buffer = new byte[bufferSize];
            this.totalBytesWritten = 0;
        }

        /// <summary>
        /// Gets the maximal size in bytes of read/write chunks.
        /// </summary>
        public int BufferSize { get => buffer.Length; }

#pragma warning disable CA1819
        /// <summary>
        /// Gets the internal buffer.
        /// </summary>
        public byte[] Buffer { get => buffer; }
#pragma warning restore CA1819

        /// <summary>
        /// Gets the number of bytes that have been written to this stream.
        /// </summary>
        public long TotalBytesWritten { get => totalBytesWritten; }

        /// <inheritdoc />
        public override long Position { get => position; set => position = value; }

        /// <inheritdoc />
        public override bool CanRead => false;

        /// <inheritdoc />
        public override bool CanWrite => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

#pragma warning disable CA1065
        /// <inheritdoc />
        public override long Length { get => throw new NotImplementedException(); }
#pragma warning restore CA1065

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            // The bytes that still need to be copied.
            long remaining = count;
            long remainingOffset = offset;

            // Copy inside a loop to simplify wrapping logic.
            while (remaining > 0)
            {
                // The amount of bytes that we can directly write from the current position without wrapping.
                long writable = Math.Min(remaining, BufferSize - Position);

                // Copy the data.
                Array.Copy(buffer, remainingOffset, this.buffer, Position, writable);
                remaining -= writable;
                remainingOffset += writable;
                Position += writable;
                totalBytesWritten += writable;

                // We might have to wrap the position.
                Position %= BufferSize;
            }
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override void Flush()
        {
            // Do nothing
        }
    }
}
