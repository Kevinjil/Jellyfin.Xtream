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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Configuration;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// A service for dealing with stream information.
/// </summary>
public partial class StreamService
{
    /// <summary>
    /// The id prefix for VOD category channel items.
    /// </summary>
    public const int VodCategoryPrefix = 0x5d774c35;

    /// <summary>
    /// The id prefix for stream channel items.
    /// </summary>
    public const int StreamPrefix = 0x5d774c36;

    /// <summary>
    /// The id prefix for series category channel items.
    /// </summary>
    public const int SeriesCategoryPrefix = 0x5d774c37;

    /// <summary>
    /// The id prefix for series category channel items.
    /// </summary>
    public const int SeriesPrefix = 0x5d774c38;

    /// <summary>
    /// The id prefix for season channel items.
    /// </summary>
    public const int SeasonPrefix = 0x5d774c39;

    /// <summary>
    /// The id prefix for season channel items.
    /// </summary>
    public const int EpisodePrefix = 0x5d774c3a;

    /// <summary>
    /// The id prefix for catchup channel items.
    /// </summary>
    public const int CatchupPrefix = 0x5d774c3b;

    /// <summary>
    /// The id prefix for catchup stream items.
    /// </summary>
    public const int CatchupStreamPrefix = 0x5d774c3c;

    /// <summary>
    /// The id prefix for media source items.
    /// </summary>
    public const int MediaSourcePrefix = 0x5d774c3d;

    /// <summary>
    /// The id prefix for Live TV items.
    /// </summary>
    public const int LiveTvPrefix = 0x5d774c3e;

    /// <summary>
    /// The id prefix for TV EPG items.
    /// </summary>
    public const int EpgPrefix = 0x5d774c3f;

    private static readonly Regex _tagRegex = TagRegex();

    /// <summary>
    /// Parses tags in the name of a stream entry.
    /// The name commonly contains tags of the forms:
    /// <list>
    /// <item>[TAG]</item>
    /// <item>|TAG|</item>
    /// </list>
    /// These tags are parsed and returned as separate strings.
    /// The returned title is cleaned from tags and trimmed.
    /// </summary>
    /// <param name="name">The name which should be parsed.</param>
    /// <returns>A <see cref="ParsedName"/> struct containing the cleaned title and parsed tags.</returns>
    public static ParsedName ParseName(string name)
    {
        List<string> tags = [];
        string title = _tagRegex.Replace(
            name,
            (match) =>
            {
                for (int i = 1; i < match.Groups.Count; ++i)
                {
                    Group g = match.Groups[i];
                    if (g.Success)
                    {
                        tags.Add(g.Value);
                    }
                }

                return string.Empty;
            });

        // Tag prefixes separated by the a character in the unicode Block Elements range
        int stripLength = 0;
        for (int i = 0; i < title.Length; i++)
        {
            char c = title[i];
            if (c >= '\u2580' && c <= '\u259F')
            {
                tags.Add(title[stripLength..i].Trim());
                stripLength = i + 1;
            }
        }

        return new ParsedName
        {
            Title = title[stripLength..].Trim(),
            Tags = [.. tags],
        };
    }

    private bool IsConfigured(SerializableDictionary<int, HashSet<int>> config, int category, int id)
    {
        return config.TryGetValue(category, out var values) && (values.Count == 0 || values.Contains(id));
    }

    /// <summary>
    /// Gets an async iterator for the configured channels.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<StreamInfo>> GetLiveStreams(CancellationToken cancellationToken)
    {
        PluginConfiguration config = Plugin.Instance.Configuration;
        using XtreamClient client = new XtreamClient();

        IEnumerable<StreamInfo> streams = await client.GetLiveStreamsAsync(Plugin.Instance.Creds, cancellationToken).ConfigureAwait(false);
        return streams.Where((StreamInfo channel) => channel.CategoryId.HasValue && IsConfigured(config.LiveTv, channel.CategoryId.Value, channel.StreamId));
    }

    /// <summary>
    /// Gets an async iterator for the configured channels after applying the configured overrides.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<StreamInfo>> GetLiveStreamsWithOverrides(CancellationToken cancellationToken)
    {
        PluginConfiguration config = Plugin.Instance.Configuration;
        IEnumerable<StreamInfo> streams = await GetLiveStreams(cancellationToken).ConfigureAwait(false);
        return streams.Select((StreamInfo stream) =>
        {
            if (config.LiveTvOverrides.TryGetValue(stream.StreamId, out ChannelOverrides? overrides))
            {
                stream.Num = overrides.Number ?? stream.Num;
                stream.Name = overrides.Name ?? stream.Name;
                stream.StreamIcon = overrides.LogoUrl ?? stream.StreamIcon;
            }

            return stream;
        });
    }

    /// <summary>
    /// Gets an channel item info for the category.
    /// </summary>
    /// <param name="prefix">The channel category prefix.</param>
    /// <param name="category">The Xtream category.</param>
    /// <returns>A channel item representing the category.</returns>
    public static ChannelItemInfo CreateChannelItemInfo(int prefix, Category category)
    {
        ParsedName parsedName = ParseName(category.CategoryName);
        return new ChannelItemInfo()
        {
            Id = ToGuid(prefix, category.CategoryId, 0, 0).ToString(),
            Name = category.CategoryName,
            Tags = new List<string>(parsedName.Tags),
            Type = ChannelItemType.Folder,
        };
    }

    /// <summary>
    /// Gets an iterator for the configured VOD categories.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<Category>> GetVodCategories(CancellationToken cancellationToken)
    {
        using XtreamClient client = new XtreamClient();
        List<Category> categories = await client.GetVodCategoryAsync(Plugin.Instance.Creds, cancellationToken).ConfigureAwait(false);
        return categories.Where((Category category) => Plugin.Instance.Configuration.Vod.ContainsKey(category.CategoryId));
    }

    /// <summary>
    /// Gets an iterator for the configured VOD streams.
    /// </summary>
    /// <param name="categoryId">The Xtream id of the category.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<StreamInfo>> GetVodStreams(int categoryId, CancellationToken cancellationToken)
    {
        if (!Plugin.Instance.Configuration.Vod.ContainsKey(categoryId))
        {
            return new List<StreamInfo>();
        }

        using XtreamClient client = new XtreamClient();
        List<StreamInfo> streams = await client.GetVodStreamsByCategoryAsync(Plugin.Instance.Creds, categoryId, cancellationToken).ConfigureAwait(false);
        return streams.Where((StreamInfo stream) => IsConfigured(Plugin.Instance.Configuration.Vod, categoryId, stream.StreamId));
    }

    /// <summary>
    /// Gets an iterator for the configured Series categories.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<Category>> GetSeriesCategories(CancellationToken cancellationToken)
    {
        using XtreamClient client = new XtreamClient();
        List<Category> categories = await client.GetSeriesCategoryAsync(Plugin.Instance.Creds, cancellationToken).ConfigureAwait(false);
        return categories.Where((Category category) => Plugin.Instance.Configuration.Series.ContainsKey(category.CategoryId));
    }

    /// <summary>
    /// Gets an iterator for the configured Series.
    /// </summary>
    /// <param name="categoryId">The Xtream id of the category.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<Series>> GetSeries(int categoryId, CancellationToken cancellationToken)
    {
        if (!Plugin.Instance.Configuration.Series.ContainsKey(categoryId))
        {
            return new List<Series>();
        }

        using XtreamClient client = new XtreamClient();
        List<Series> series = await client.GetSeriesByCategoryAsync(Plugin.Instance.Creds, categoryId, cancellationToken).ConfigureAwait(false);
        return series.Where((Series series) => IsConfigured(Plugin.Instance.Configuration.Series, series.CategoryId, series.SeriesId));
    }

    /// <summary>
    /// Gets an iterator for the configured seasons in the Series.
    /// </summary>
    /// <param name="seriesId">The Xtream id of the Series.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<Tuple<SeriesStreamInfo, int>>> GetSeasons(int seriesId, CancellationToken cancellationToken)
    {
        using XtreamClient client = new XtreamClient();
        SeriesStreamInfo series = await client.GetSeriesStreamsBySeriesAsync(Plugin.Instance.Creds, seriesId, cancellationToken).ConfigureAwait(false);
        int categoryId = series.Info.CategoryId;
        if (!IsConfigured(Plugin.Instance.Configuration.Series, categoryId, seriesId))
        {
            return new List<Tuple<SeriesStreamInfo, int>>();
        }

        return series.Episodes.Keys.Select((int seasonId) => new Tuple<SeriesStreamInfo, int>(series, seasonId));
    }

    /// <summary>
    /// Gets an iterator for the configured seasons in the Series.
    /// </summary>
    /// <param name="seriesId">The Xtream id of the Series.</param>
    /// <param name="seasonId">The Xtream id of the Season.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<Tuple<SeriesStreamInfo, Season?, Episode>>> GetEpisodes(int seriesId, int seasonId, CancellationToken cancellationToken)
    {
        using XtreamClient client = new XtreamClient();
        SeriesStreamInfo series = await client.GetSeriesStreamsBySeriesAsync(Plugin.Instance.Creds, seriesId, cancellationToken).ConfigureAwait(false);
        Season? season = series.Seasons.FirstOrDefault(s => s.SeasonId == seasonId);
        return series.Episodes[seasonId].Select((Episode episode) => new Tuple<SeriesStreamInfo, Season?, Episode>(series, season, episode));
    }

    private static void StoreBytes(byte[] dst, int offset, int i)
    {
        byte[] intBytes = BitConverter.GetBytes(i);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(intBytes);
        }

        Buffer.BlockCopy(intBytes, 0, dst, offset, 4);
    }

    /// <summary>
    /// Gets a GUID representing the four 32-bit integers.
    /// </summary>
    /// <param name="i0">Bytes 0-3.</param>
    /// <param name="i1">Bytes 4-7.</param>
    /// <param name="i2">Bytes 8-11.</param>
    /// <param name="i3">Bytes 12-15.</param>
    /// <returns>Guid.</returns>
    public static Guid ToGuid(int i0, int i1, int i2, int i3)
    {
        byte[] guid = new byte[16];
        StoreBytes(guid, 0, i0);
        StoreBytes(guid, 4, i1);
        StoreBytes(guid, 8, i2);
        StoreBytes(guid, 12, i3);
        return new Guid(guid);
    }

    /// <summary>
    /// Gets the four 32-bit integers represented in the GUID.
    /// </summary>
    /// <param name="id">The input GUID.</param>
    /// <param name="i0">Bytes 0-3.</param>
    /// <param name="i1">Bytes 4-7.</param>
    /// <param name="i2">Bytes 8-11.</param>
    /// <param name="i3">Bytes 12-15.</param>
    public static void FromGuid(Guid id, out int i0, out int i1, out int i2, out int i3)
    {
        byte[] tmp = id.ToByteArray();
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(tmp);
            i0 = BitConverter.ToInt32(tmp, 12);
            i1 = BitConverter.ToInt32(tmp, 8);
            i2 = BitConverter.ToInt32(tmp, 4);
            i3 = BitConverter.ToInt32(tmp, 0);
        }
        else
        {
            i0 = BitConverter.ToInt32(tmp, 0);
            i1 = BitConverter.ToInt32(tmp, 4);
            i2 = BitConverter.ToInt32(tmp, 8);
            i3 = BitConverter.ToInt32(tmp, 12);
        }
    }

    /// <summary>
    /// Gets the media source information for the given Xtream stream.
    /// </summary>
    /// <param name="type">The stream media type.</param>
    /// <param name="id">The unique identifier of the stream.</param>
    /// <param name="extension">The container extension of the stream.</param>
    /// <param name="restream">Boolean indicating whether or not restreaming is used.</param>
    /// <param name="start">The datetime representing the start time of catcup TV.</param>
    /// <param name="durationMinutes">The duration in minutes of the catcup TV stream.</param>
    /// <param name="videoInfo">The Xtream video info if known.</param>
    /// <param name="audioInfo">The Xtream audio info if known.</param>
    /// <returns>The media source info as <see cref="MediaSourceInfo"/> class.</returns>
    public MediaSourceInfo GetMediaSourceInfo(
        StreamType type,
        int id,
        string? extension = null,
        bool restream = false,
        DateTime? start = null,
        int durationMinutes = 0,
        VideoInfo? videoInfo = null,
        AudioInfo? audioInfo = null)
    {
        string prefix = string.Empty;
        switch (type)
        {
            case StreamType.Series:
                prefix = "/series";
                break;
            case StreamType.Vod:
                prefix = "/movie";
                break;
        }

        PluginConfiguration config = Plugin.Instance.Configuration;
        string uri = $"{config.BaseUrl}{prefix}/{config.Username}/{config.Password}/{id}";
        if (!string.IsNullOrEmpty(extension))
        {
            uri += $".{extension}";
        }

        if (type == StreamType.CatchUp)
        {
            string? startString = start?.ToString("yyyy'-'MM'-'dd':'HH'-'mm", CultureInfo.InvariantCulture);
            uri = $"{config.BaseUrl}/streaming/timeshift.php?username={config.Username}&password={config.Password}&stream={id}&start={startString}&duration={durationMinutes}";
        }

        bool isLive = type == StreamType.Live;
        return new MediaSourceInfo()
        {
            Container = extension,
            EncoderProtocol = MediaProtocol.Http,
            Id = ToGuid(MediaSourcePrefix, (int)type, id, 0).ToString(),
            IsInfiniteStream = isLive,
            IsRemote = true,
            MediaStreams =
            [
                new()
                {
                    AspectRatio = videoInfo?.AspectRatio,
                    BitDepth = videoInfo?.BitsPerRawSample,
                    Codec = videoInfo?.CodecName,
                    ColorPrimaries = videoInfo?.ColorPrimaries,
                    ColorRange = videoInfo?.ColorRange,
                    ColorSpace = videoInfo?.ColorSpace,
                    ColorTransfer = videoInfo?.ColorTransfer,
                    Height = videoInfo?.Height,
                    Index = videoInfo?.Index ?? -1,
                    IsAVC = videoInfo?.IsAVC,
                    IsInterlaced = true,
                    Level = videoInfo?.Level,
                    PixelFormat = videoInfo?.PixelFormat,
                    Profile = videoInfo?.Profile,
                    Type = MediaStreamType.Video,
                    Width = videoInfo?.Width,
                },
                new()
                {
                    BitRate = audioInfo?.Bitrate,
                    ChannelLayout = audioInfo?.ChannelLayout,
                    Channels = audioInfo?.Channels,
                    Codec = audioInfo?.CodecName,
                    Index = audioInfo?.Index ?? -1,
                    Profile = audioInfo?.Profile,
                    SampleRate = audioInfo?.SampleRate,
                    Type = MediaStreamType.Audio,
                }
            ],
            Name = "default",
            Path = uri,
            Protocol = MediaProtocol.Http,
            RequiresClosing = restream,
            RequiresOpening = restream,
            SupportsDirectPlay = true,
            SupportsDirectStream = true,
            SupportsProbing = true,
        };
    }

    [GeneratedRegex(@"\[([^\]]+)\]|\|([^\|]+)\|")]
    private static partial Regex TagRegex();
}
