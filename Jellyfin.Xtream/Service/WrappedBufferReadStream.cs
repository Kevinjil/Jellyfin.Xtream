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
    public class WrappedBufferReadStream : Stream
    {
        private readonly WrappedBufferStream buffer;

        private long position;
        private long readHead;
        private long totalBytesRead;

        /// <summary>
        /// Initializes a new instance of the <see cref="WrappedBufferReadStream"/> class.
        /// </summary>
        /// <param name="buffer">The source buffer to read from.</param>
        public WrappedBufferReadStream(WrappedBufferStream buffer)
        {
            this.buffer = buffer;
            this.readHead = buffer.TotalBytesWritten;
            this.totalBytesRead = 0;
            this.Position = buffer.Position;
        }

        /// <summary>
        /// Gets the virtual position in the source buffer.
        /// </summary>
        public long ReadHead { get => readHead; }

        /// <summary>
        /// Gets the number of bytes that have been written to this stream.
        /// </summary>
        public long TotalBytesRead { get => totalBytesRead; }

        /// <inheritdoc />
        public override long Position { get => position; set => position = value; }

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override bool CanSeek => false;

#pragma warning disable CA1065
        /// <inheritdoc />
        public override long Length { get => throw new NotImplementedException(); }
#pragma warning restore CA1065

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            long gap = this.buffer.TotalBytesWritten - readHead;
            if (gap > this.buffer.BufferSize)
            {
                // TODO: design good handling method.
                // Options:
                // - throw exception
                // - skip to buffer.Position+1 to only read 'up-to-date' bytes.
                throw new IOException("Reader cannot keep up");
            }

            // The bytes that still need to be copied.
            long remaining = Math.Min(count, this.buffer.TotalBytesWritten - readHead);
            long remainingOffset = offset;

            long read = 0;

            // Copy inside a loop to simplify wrapping logic.
            while (remaining > 0)
            {
                // The amount of bytes that we can directly write from the current position without wrapping.
                long readable = Math.Min(remaining, this.buffer.BufferSize - Position);

                // Copy the data.
                Array.Copy(this.buffer.Buffer, Position, buffer, remainingOffset, readable);
                remaining -= readable;
                remainingOffset += readable;

                read += readable;
                Position += readable;
                readHead += readable;
                totalBytesRead += readable;

                // We might have to loop the position.
                Position %= this.buffer.BufferSize;
            }

            return (int)read;
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
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
