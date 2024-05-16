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
public class WrappedBufferStream : Stream
{
    private readonly byte[] sourceBuffer;

    private long totalBytesWritten;

    /// <summary>
    /// Initializes a new instance of the <see cref="WrappedBufferStream"/> class.
    /// </summary>
    /// <param name="bufferSize">Size in bytes of the internal buffer.</param>
    public WrappedBufferStream(int bufferSize)
    {
        this.sourceBuffer = new byte[bufferSize];
        this.totalBytesWritten = 0;
    }

    /// <summary>
    /// Gets the maximal size in bytes of read/write chunks.
    /// </summary>
    public int BufferSize { get => sourceBuffer.Length; }

#pragma warning disable CA1819
    /// <summary>
    /// Gets the internal buffer.
    /// </summary>
    public byte[] Buffer { get => sourceBuffer; }
#pragma warning restore CA1819

    /// <summary>
    /// Gets the number of bytes that have been written to this stream.
    /// </summary>
    public long TotalBytesWritten { get => totalBytesWritten; }

    /// <inheritdoc />
    public override long Position
    {
        get => totalBytesWritten % BufferSize; set { }
    }

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
        long written = 0;

        // Copy inside a loop to simplify wrapping logic.
        while (written < count)
        {
            // The amount of bytes that we can directly write from the current position without wrapping.
            long writable = Math.Min(count - written, BufferSize - Position);

            // Copy the data.
            Array.Copy(buffer, offset + written, sourceBuffer, Position, writable);
            written += writable;
            totalBytesWritten += writable;
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
