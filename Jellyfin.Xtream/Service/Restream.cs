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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// A live stream implementation that can be restreamed.
/// </summary>
public class Restream : ILiveStream, IDirectStreamProvider, IDisposable
{
    /// <summary>
    /// The global constant for the restream tuner host.
    /// </summary>
    public const string TunerHost = "Xtream-Restream";

    private static readonly HttpStatusCode[] _redirects = [
        HttpStatusCode.Moved,
        HttpStatusCode.MovedPermanently,
        HttpStatusCode.PermanentRedirect,
        HttpStatusCode.Redirect,
    ];

    private readonly WrappedBufferStream _buffer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _tokenSource;
    private readonly string _url;

    private Task? _copyTask;
    private Stream? _inputStream;

    /// <summary>
    /// Initializes a new instance of the <see cref="Restream"/> class.
    /// </summary>
    /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
    /// <param name="mediaSource">The media which must be restreamed.</param>
    public Restream(IServerApplicationHost appHost, IHttpClientFactory httpClientFactory, ILogger logger, MediaSourceInfo mediaSource)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        MediaSource = mediaSource;

        _buffer = new WrappedBufferStream(16 * 1024 * 1024); // 16MiB
        _tokenSource = new CancellationTokenSource();

        OriginalStreamId = MediaSource.Id;
        UniqueId = Guid.NewGuid().ToString();

        _url = MediaSource.Path;
        string path = $"/LiveTv/LiveStreamFiles/{UniqueId}/stream.ts";
        MediaSource.Path = appHost.GetSmartApiUrl(IPAddress.Any) + path;
        MediaSource.EncoderPath = appHost.GetApiUrlForLocalAccess() + path;
        MediaSource.Protocol = MediaProtocol.Http;
    }

    /// <inheritdoc />
    public int ConsumerCount { get; set; }

    /// <inheritdoc />
    public string OriginalStreamId { get; set; }

    /// <inheritdoc />
    public string TunerHostId => TunerHost;

    /// <inheritdoc />
    public bool EnableStreamSharing => true;

    /// <inheritdoc />
    public MediaSourceInfo MediaSource { get; set; }

    /// <inheritdoc />
    public string UniqueId { get; init; }

    /// <inheritdoc />
    public async Task Open(CancellationToken openCancellationToken)
    {
        if (_inputStream != null)
        {
            _logger.LogWarning("Restream for channel {ChannelId} is already open.", MediaSource.Id);
            return;
        }

        string channelId = MediaSource.Id;
        _logger.LogInformation("Starting restream for channel {ChannelId}.", channelId);

        // Response stream is disposed manually.
        HttpResponseMessage response = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetAsync(_url, HttpCompletionOption.ResponseHeadersRead, openCancellationToken)
            .ConfigureAwait(true);
        _logger.LogDebug("Stream for channel {ChannelId} using url {Url}", channelId, _url);

        // Handle a manual redirect in the case of a HTTPS to HTTP downgrade.
        if (_redirects.Contains(response.StatusCode))
        {
            _logger.LogDebug("Stream for channel {ChannelId} redirected to url {Url}", channelId, response.Headers.Location);
            response = await _httpClientFactory.CreateClient(NamedClient.Default)
                .GetAsync(response.Headers.Location, HttpCompletionOption.ResponseHeadersRead, openCancellationToken)
                .ConfigureAwait(true);
        }

        _inputStream = await response.Content.ReadAsStreamAsync(CancellationToken.None).ConfigureAwait(false);
        _copyTask = _inputStream.CopyToAsync(_buffer, _tokenSource.Token)
            .ContinueWith(
                (Task t) =>
                {
                    _logger.LogInformation("Restream for channel {ChannelId} finished with state {Status}", MediaSource.Id, t.Status);
                    _inputStream.Close();
                    _inputStream = null;
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
    }

    /// <inheritdoc />
    public async Task Close()
    {
        if (_copyTask == null)
        {
            throw new ArgumentNullException("copyTask");
        }

        await _tokenSource.CancelAsync().ConfigureAwait(false);
        await _copyTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Stream GetStream()
    {
        if (_inputStream == null)
        {
            _logger.LogWarning("Restream for channel {ChannelId} was not opened.", MediaSource.Id);
            _ = Open(CancellationToken.None);
        }

        _logger.LogInformation("Opening restream {Count} for channel {ChannelId}.", ConsumerCount, MediaSource.Id);
        return new WrappedBufferReadStream(_buffer);
    }

    /// <summary>
    /// Disposes the fields.
    /// </summary>
    /// <param name="disposing">Whether or not to dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inputStream?.Dispose();
            _buffer.Dispose();
            _tokenSource.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        Dispose(true);
        // This object will be cleaned up by the Dispose method.
        // Therefore, you should call GC.SuppressFinalize to
        // take this object off the finalization queue
        // and prevent finalization code for this object
        // from executing a second time.
        GC.SuppressFinalize(this);
    }
}
