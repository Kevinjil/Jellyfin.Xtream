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
/// Tests for the guard shared by get_series_info and get_vod_info, the single-object endpoints.
/// </summary>
public class ObjectResponseTests
{
    private static XtreamClient CreateClient() =>
        new XtreamClient(new HttpClient(), NullLogger<XtreamClient>.Instance);

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"info\":{}}]")]
    [InlineData("null")]
    [InlineData("false")]
    [InlineData("\"\"")]
    [InlineData("0")]
    public void ParseObjectResponse_NonObject_YieldsNullForSeries(string json)
    {
        using XtreamClient client = CreateClient();

        Assert.Null(client.ParseObjectResponse<SeriesStreamInfo>(json, "Series", 1));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("0")]
    public void ParseObjectResponse_NonObject_YieldsNullForVod(string json)
    {
        using XtreamClient client = CreateClient();

        Assert.Null(client.ParseObjectResponse<VodStreamInfo>(json, "VOD stream", 1));
    }

    [Theory]
    [InlineData("{\"info\":")]
    [InlineData("<html>429 Too Many Requests</html>")]
    [InlineData("")]
    public void ParseObjectResponse_MalformedJson_YieldsNull(string json)
    {
        using XtreamClient client = CreateClient();

        Assert.Null(client.ParseObjectResponse<SeriesStreamInfo>(json, "Series", 1));
        Assert.Null(client.ParseObjectResponse<VodStreamInfo>(json, "VOD stream", 1));
    }

    [Fact]
    public void ParseObjectResponse_WellFormedSeries_IsUnaffected()
    {
        using XtreamClient client = CreateClient();

        SeriesStreamInfo? series = client.ParseObjectResponse<SeriesStreamInfo>(
            "{\"info\":{\"category_id\":7},\"seasons\":[{\"season_number\":1}],\"episodes\":{\"1\":[{\"id\":11,\"season\":1}]}}",
            "Series",
            1);

        Assert.NotNull(series);
        Assert.Equal(7, series.Info.CategoryId);
        Assert.Single(series.Seasons);
        Assert.Equal(11, series.Episodes[1].Single().EpisodeId);
    }

    [Fact]
    public void ParseObjectResponse_WellFormedVod_IsUnaffected()
    {
        using XtreamClient client = CreateClient();

        VodStreamInfo? vod = client.ParseObjectResponse<VodStreamInfo>(
            "{\"info\":{\"plot\":\"A plot\",\"releasedate\":\"2021-05-04\"}}",
            "VOD stream",
            1);

        Assert.NotNull(vod);
        Assert.NotNull(vod.Info);
        Assert.Equal("A plot", vod.Info.Plot);
    }

    [Fact]
    public void ParseObjectResponse_UnparseableMember_KeepsTheRest()
    {
        using XtreamClient client = CreateClient();

        SeriesStreamInfo? series = client.ParseObjectResponse<SeriesStreamInfo>(
            "{\"info\":{\"category_id\":7},\"episodes\":[]}",
            "Series",
            1);

        Assert.NotNull(series);
        Assert.Equal(7, series.Info.CategoryId);
        Assert.Empty(series.Episodes);
    }
}
