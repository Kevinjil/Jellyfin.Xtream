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
using System.IO;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Stream which writes to a self-overwriting internal buffer.
/// </summary>
/// <param name="bufferSize">Size in bytes of the internal buffer.</param>
public class WrappedBufferStream(int bufferSize) : Stream
{
    /// <summary>
    /// Gets the maximal size in bytes of read/write chunks.
    /// </summary>
    public int BufferSize { get => Buffer.Length; }

#pragma warning disable CA1819
    /// <summary>
    /// Gets the internal buffer.
    /// </summary>
    public byte[] Buffer { get; } = new byte[bufferSize];
#pragma warning restore CA1819

    /// <summary>
    /// Gets the number of bytes that have been written to this stream.
    /// </summary>
    public long TotalBytesWritten { get; private set; }

    /// <inheritdoc />
    public override long Position
    {
        get => TotalBytesWritten % BufferSize; set { }
    }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        long written = 0;

        // Copy inside a loop to simplify wrapping logic.
        while (written < count)
        {
            // The amount of bytes that we can directly write from the current position without wrapping.
            long writable = Math.Min(count - written, BufferSize - Position);

            // Copy the data.
            Array.Copy(buffer, offset + written, Buffer, Position, writable);
            written += writable;
            TotalBytesWritten += writable;
        }
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Flush()
    {
        // Do nothing
    }
}
