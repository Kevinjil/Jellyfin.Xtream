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

using System.Collections.Generic;
using System.Linq;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit;

namespace Jellyfin.Xtream.Tests;

/// <summary>
/// Tests that a get_series_info response deviating from the expected schema keeps what is usable.
/// </summary>
public class SeriesDeserializationTests
{
    private static SeriesStreamInfo? Deserialize(string json) =>
        JsonConvert.DeserializeObject<SeriesStreamInfo>(
            json,
            new JsonSerializerSettings { Error = XtreamClient.NullableEventHandler(NullLogger<XtreamClient>.Instance) });

    private static int EpisodeCount(SeriesStreamInfo series) => series.Episodes.Values.Sum(e => e.Count);

    [Fact]
    public void Deserialize_EpisodesAsArray_KeepsRestOfResponse()
    {
        SeriesStreamInfo? series = Deserialize("{\"info\":{\"category_id\":7,\"name\":\"Show\"},\"episodes\":[]}");

        Assert.NotNull(series);
        Assert.Equal(7, series.Info.CategoryId);
        Assert.Equal("Show", series.Info.Name);
        Assert.Empty(series.Episodes);
    }

    [Fact]
    public void Deserialize_NonNumericEpisodeKey_KeepsOtherBuckets()
    {
        SeriesStreamInfo? series = Deserialize(
            "{\"info\":{\"category_id\":7},\"episodes\":{\"specials\":[{\"id\":1}],\"2\":[{\"id\":5,\"season\":2}]}}");

        Assert.NotNull(series);
        Assert.Equal(7, series.Info.CategoryId);
        Assert.Equal(new[] { 2 }, series.Episodes.Keys);
        Assert.Equal(5, series.Episodes[2].Single().EpisodeId);
    }

    [Fact]
    public void Deserialize_InfoAsArray_LeavesInfoUsable()
    {
        SeriesStreamInfo? series = Deserialize("{\"info\":[],\"episodes\":{\"1\":[{\"id\":1,\"season\":1}]}}");

        Assert.NotNull(series);
        Assert.NotNull(series.Info);
        Assert.Equal(1, EpisodeCount(series));
    }

    [Fact]
    public void Deserialize_FieldOfWrongType_KeepsSiblingEpisodes()
    {
        SeriesStreamInfo? series = Deserialize(
            "{\"info\":{},\"episodes\":{\"1\":[{\"id\":\"abc\",\"season\":1},{\"id\":9,\"season\":1}]}}");

        Assert.NotNull(series);
        Assert.Contains(series.Episodes[1], e => e.EpisodeId == 9);
    }

    [Fact]
    public void Deserialize_WellFormedResponse_IsUnaffected()
    {
        SeriesStreamInfo? series = Deserialize(
            "{\"info\":{\"category_id\":7},\"seasons\":[{\"season_number\":1}],\"episodes\":{\"1\":[{\"id\":11,\"season\":1,\"episode_num\":3}]}}");

        Assert.NotNull(series);
        Assert.Equal(7, series.Info.CategoryId);
        Assert.Single(series.Seasons);
        Episode episode = series.Episodes[1].Single();
        Assert.Equal(11, episode.EpisodeId);
        Assert.Equal(3, episode.EpisodeNum);
    }

    [Fact]
    public void Deserialize_RootLevelFailure_IsNotSwallowed()
    {
        // The client tells "no series data" apart from "one bad member" by this throwing.
        Assert.ThrowsAny<JsonException>(() => Deserialize("[]"));
    }

    [Fact]
    public void Deserialize_SeriesListWithDateStringLastModified_KeepsEverySeries()
    {
        // As sent by the provider of #287, where Xtream normally sends a Unix timestamp.
        List<Series>? series = JsonConvert.DeserializeObject<List<Series>>(
            "[{\"series_id\":998,\"name\":\"A\",\"last_modified\":\"2026-02-02 15:08:55\",\"category_id\":\"276\"},"
            + "{\"series_id\":999,\"name\":\"B\",\"last_modified\":\"2026-01-06 17:23:48\",\"category_id\":\"276\"}]",
            new JsonSerializerSettings { Error = XtreamClient.NullableEventHandler(NullLogger<XtreamClient>.Instance) });

        Assert.NotNull(series);
        Assert.Equal(new[] { 998, 999 }, series.Select(s => s.SeriesId));
        Assert.Equal(new[] { "A", "B" }, series.Select(s => s.Name));
    }
}
