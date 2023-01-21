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
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service
{
    /// <summary>
    /// A live stream implementation that can be restreamed.
    /// </summary>
    public class Restream : ILiveStream, IDisposable
    {
        /// <summary>
        /// The global constant for the restream tuner host.
        /// </summary>
        public const string TunerHost = "Xtream-Restream";

        private static readonly HttpStatusCode[] Redirects = new[]
        {
            HttpStatusCode.Moved,
            HttpStatusCode.MovedPermanently,
            HttpStatusCode.PermanentRedirect,
            HttpStatusCode.Redirect,
        };

        private readonly IServerApplicationHost appHost;
        private readonly WrappedBufferStream buffer;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger logger;
        private readonly CancellationTokenSource tokenSource;
        private readonly bool enableStreamSharing;
        private readonly string uniqueId;
        private readonly string uri;

        private Task? copyTask;
        private Stream? inputStream;

        private int consumerCount;
        private string originalStreamId;
        private MediaSourceInfo mediaSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="Restream"/> class.
        /// </summary>
        /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
        /// <param name="mediaSource">The media which must be restreamed.</param>
        public Restream(IServerApplicationHost appHost, IHttpClientFactory httpClientFactory, ILogger logger, MediaSourceInfo mediaSource)
        {
            this.appHost = appHost;
            this.buffer = new WrappedBufferStream(16777216); // 16MiB
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
            this.tokenSource = new CancellationTokenSource();

            this.mediaSource = mediaSource;
            this.originalStreamId = mediaSource.Id;
            this.enableStreamSharing = true;
            this.uniqueId = Guid.NewGuid().ToString();

            this.uri = mediaSource.Path;
            string path = "/LiveTv/LiveStreamFiles/" + UniqueId + "/stream.ts";
            MediaSource.Path = appHost.GetSmartApiUrl(IPAddress.Any) + path;
            MediaSource.EncoderPath = appHost.GetApiUrlForLocalAccess() + path;
            MediaSource.Protocol = MediaProtocol.Http;
        }

        /// <inheritdoc />
        public int ConsumerCount { get => consumerCount; set => consumerCount = value; }

        /// <inheritdoc />
        public string OriginalStreamId { get => originalStreamId; set => originalStreamId = value; }

        /// <inheritdoc />
        public string TunerHostId { get => TunerHost; }

        /// <inheritdoc />
        public bool EnableStreamSharing { get => enableStreamSharing; }

        /// <inheritdoc />
        public MediaSourceInfo MediaSource { get => mediaSource; set => mediaSource = value; }

        /// <inheritdoc />
        public string UniqueId { get => uniqueId; }

        /// <inheritdoc />
        public async Task Open(CancellationToken openCancellationToken)
        {
            if (inputStream != null)
            {
                // Channel is already opened.
                return;
            }

            string channelId = mediaSource.Id;
            this.logger.LogInformation("Starting restream for channel {ChannelId}.", channelId);

            // Response stream is disposed manually.
            HttpResponseMessage response = await this.httpClientFactory.CreateClient(NamedClient.Default)
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None)
                .ConfigureAwait(true);
            this.logger.LogDebug("Stream for channel {ChannelId} using url {Url}", channelId, uri);

            // Handle a manual redirect in the case of a HTTPS to HTTP downgrade.
            if (Redirects.Contains(response.StatusCode))
            {
                this.logger.LogDebug("Stream for channel {ChannelId} redirected to url {Url}", channelId, response.Headers.Location);
                response = await this.httpClientFactory.CreateClient(NamedClient.Default)
                    .GetAsync(response.Headers.Location, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None)
                    .ConfigureAwait(true);
            }

            inputStream = await response.Content.ReadAsStreamAsync(CancellationToken.None).ConfigureAwait(false);
            copyTask = inputStream.CopyToAsync(buffer, tokenSource.Token)
                .ContinueWith(
                    (Task t) =>
                    {
                        this.logger.LogInformation("Restream for channel {ChannelId} finished with state {Status}", mediaSource.Id, t.Status);
                        inputStream.Close();
                        inputStream = null;
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
        }

        /// <inheritdoc />
        public async Task Close()
        {
            if (copyTask == null)
            {
                throw new ArgumentNullException("copyTask");
            }

            tokenSource.Cancel();
            await copyTask.ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Stream GetStream()
        {
            if (inputStream == null)
            {
                this.logger.LogInformation("Restream for channel {ChannelId} was not opened.", mediaSource.Id);
                Task open = Open(CancellationToken.None);
            }

            consumerCount++;
            this.logger.LogInformation("Opening restream {Count} for channel {ChannelId}.", consumerCount, mediaSource.Id);
            return new WrappedBufferReadStream(this.buffer);
        }

        /// <summary>
        /// Disposes the fields.
        /// </summary>
        /// <param name="disposing">Whether or not to dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.inputStream?.Dispose();
                this.buffer.Dispose();
                this.tokenSource.Dispose();
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
}
