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
using System.Threading;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// Stream which writes to a self-overwriting internal buffer.
/// </summary>
public class WrappedBufferReadStream : Stream
{
    private readonly WrappedBufferStream _sourceBuffer;

    private readonly long _initialReadHead;

    /// <summary>
    /// Initializes a new instance of the <see cref="WrappedBufferReadStream"/> class.
    /// </summary>
    /// <param name="sourceBuffer">The source buffer to read from.</param>
    public WrappedBufferReadStream(WrappedBufferStream sourceBuffer)
    {
        _sourceBuffer = sourceBuffer;
        _initialReadHead = sourceBuffer.TotalBytesWritten;
        ReadHead = _initialReadHead;
    }

    /// <summary>
    /// Gets the virtual position in the source buffer.
    /// </summary>
    public long ReadHead { get; private set; }

    /// <summary>
    /// Gets the number of bytes that have been written to this stream.
    /// </summary>
    public long TotalBytesRead { get => ReadHead - _initialReadHead; }

    /// <inheritdoc />
    public override long Position
    {
        get => ReadHead % _sourceBuffer.BufferSize; set { }
    }

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
        long gap = _sourceBuffer.TotalBytesWritten - ReadHead;

        // We cannot return with 0 bytes read, as that indicates the end of the stream has been reached
        while (gap == 0)
        {
            Thread.Sleep(1);
            gap = _sourceBuffer.TotalBytesWritten - ReadHead;
        }

        if (gap > _sourceBuffer.BufferSize)
        {
            // TODO: design good handling method.
            // Options:
            // - throw exception
            // - skip to buffer.Position+1 to only read 'up-to-date' bytes.
            throw new IOException("Reader cannot keep up");
        }

        // The number of bytes that can be copied.
        long canCopy = Math.Min(count, gap);
        long read = 0;

        // Copy inside a loop to simplify wrapping logic.
        while (read < canCopy)
        {
            // The amount of bytes that we can directly write from the current position without wrapping.
            long readable = Math.Min(canCopy - read, _sourceBuffer.BufferSize - Position);

            // Copy the data.
            Array.Copy(_sourceBuffer.Buffer, Position, buffer, offset + read, readable);
            read += readable;
            ReadHead += readable;
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
