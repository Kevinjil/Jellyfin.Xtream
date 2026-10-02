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

using System.Linq;
using System.Net.Http;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Xtream.Tests;

/// <summary>
/// Tests that a bad member of a nested object does not take its siblings down with it.
/// </summary>
public class NestedObjectParsingTests
{
    private static XtreamClient CreateClient() =>
        new XtreamClient(new HttpClient(), NullLogger<XtreamClient>.Instance);

    private static EpisodeInfo? EpisodeInfoOf(string info)
    {
        using XtreamClient client = CreateClient();
        SeriesStreamInfo? series = client.ParseObjectResponse<SeriesStreamInfo>(
            $"{{\"info\":{{}},\"episodes\":{{\"1\":[{{\"id\":1,\"season\":1,\"info\":{info}}}]}}}}",
            "Series",
            1);

        Assert.NotNull(series);
        return series.Episodes[1].Single().Info;
    }

    [Theory]
    [InlineData("\"bitrate\":\"N/A\"")]
    [InlineData("\"duration_secs\":\"N/A\"")]
    [InlineData("\"rating\":\"N/A\"")]
    [InlineData("\"releasedate\":\"N/A\"")]
    [InlineData("\"bitrate\":[]")]
    public void ParseObjectResponse_UnparseableEpisodeInfoMember_KeepsSiblings(string member)
    {
        EpisodeInfo? info = EpisodeInfoOf($"{{\"plot\":\"A plot\",\"movie_image\":\"http://x/y.jpg\",{member}}}");

        Assert.NotNull(info);
        Assert.Equal("A plot", info.Plot);
        Assert.Equal("http://x/y.jpg", info.MovieImage);
    }

    [Fact]
    public void ParseObjectResponse_WellFormedEpisodeInfo_IsUnaffected()
    {
        EpisodeInfo? info = EpisodeInfoOf("{\"plot\":\"A plot\",\"duration_secs\":60,\"bitrate\":128}");

        Assert.NotNull(info);
        Assert.Equal("A plot", info.Plot);
        Assert.Equal(60, info.DurationSecs);
        Assert.Equal(128, info.Bitrate);
    }

    [Fact]
    public void ParseObjectResponse_EpisodeInfoAsArray_YieldsNoInfo()
    {
        Assert.Null(EpisodeInfoOf("[]"));
    }

    [Fact]
    public void ParseObjectResponse_UnparseableVodInfoMember_KeepsSiblings()
    {
        using XtreamClient client = CreateClient();

        VodStreamInfo? vod = client.ParseObjectResponse<VodStreamInfo>(
            "{\"info\":{\"plot\":\"A plot\",\"tmdb_id\":\"N/A\",\"duration_secs\":90}}",
            "VOD stream",
            1);

        Assert.NotNull(vod);
        Assert.NotNull(vod.Info);
        Assert.Equal("A plot", vod.Info.Plot);
        Assert.Equal(90, vod.Info.DurationSecs);
        Assert.Null(vod.Info.TmdbId);
    }

    [Fact]
    public void ParseObjectResponse_UnparseableNestedVideoMember_KeepsSiblings()
    {
        using XtreamClient client = CreateClient();

        VodStreamInfo? vod = client.ParseObjectResponse<VodStreamInfo>(
            "{\"info\":{\"plot\":\"A plot\",\"video\":{\"width\":\"N/A\",\"height\":1080}}}",
            "VOD stream",
            1);

        Assert.NotNull(vod);
        Assert.NotNull(vod.Info);
        Assert.Equal("A plot", vod.Info.Plot);
        Assert.NotNull(vod.Info.Video);
        Assert.Equal(1080, vod.Info.Video.Height);
    }
}
