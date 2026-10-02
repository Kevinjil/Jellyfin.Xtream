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
using Jellyfin.Xtream.Service;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit;

namespace Jellyfin.Xtream.Tests;

/// <summary>
/// Tests for the grouping which backs both the season listing and the episode listing.
/// </summary>
public class SeasonGroupingTests
{
    private static SeriesStreamInfo Parse(string json) =>
        JsonConvert.DeserializeObject<SeriesStreamInfo>(json)!;

    private static Dictionary<int, List<Episode>> Group(string json) =>
        StreamService.GroupEpisodesBySeason(JsonConvert.DeserializeObject<SeriesStreamInfo>(json)!);

    [Fact]
    public void Group_AllEpisodesUnderOneKey_ExposesEverySeason()
    {
        Dictionary<int, List<Episode>> seasons =
            Group("{\"info\":{},\"episodes\":{\"0\":[{\"id\":11,\"season\":1},{\"id\":22,\"season\":2}]}}");

        Assert.Equal(new[] { 1, 2 }, seasons.Keys.OrderBy(k => k));
        Assert.Equal(11, seasons[1].Single().EpisodeId);
        Assert.Equal(22, seasons[2].Single().EpisodeId);
    }

    [Fact]
    public void Group_KeysDisagreeWithEpisodes_PrefersTheEpisodes()
    {
        Dictionary<int, List<Episode>> seasons =
            Group("{\"info\":{},\"episodes\":{\"1\":[{\"id\":11,\"season\":2}],\"2\":[{\"id\":22,\"season\":3}]}}");

        Assert.Equal(new[] { 2, 3 }, seasons.Keys.OrderBy(k => k));
    }

    [Fact]
    public void Group_EmptyBucket_DoesNotDuplicateEpisodes()
    {
        Dictionary<int, List<Episode>> seasons =
            Group("{\"info\":{},\"episodes\":{\"1\":[],\"2\":[{\"id\":55,\"season\":1}]}}");

        List<int> ids = seasons.Values.SelectMany(e => e).Select(e => e.EpisodeId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(new[] { 1 }, seasons.Keys);
    }

    [Fact]
    public void Group_EpisodesWithoutSeason_FallsBackToKeys()
    {
        Dictionary<int, List<Episode>> seasons =
            Group("{\"info\":{},\"episodes\":{\"1\":[{\"id\":11}],\"2\":[{\"id\":22}]}}");

        Assert.Equal(new[] { 1, 2 }, seasons.Keys.OrderBy(k => k));
        Assert.Equal(11, seasons[1].Single().EpisodeId);
    }

    [Fact]
    public void Group_SpecialsInSeasonZero_AreKeptAlongsideRealSeasons()
    {
        Dictionary<int, List<Episode>> seasons =
            Group("{\"info\":{},\"episodes\":{\"0\":[{\"id\":9,\"season\":0}],\"1\":[{\"id\":11,\"season\":1}]}}");

        Assert.Equal(new[] { 0, 1 }, seasons.Keys.OrderBy(k => k));
        Assert.Equal(9, seasons[0].Single().EpisodeId);
    }

    [Fact]
    public void Group_WellFormedResponse_IsUnaffected()
    {
        Dictionary<int, List<Episode>> seasons =
            Group("{\"info\":{},\"episodes\":{\"1\":[{\"id\":11,\"season\":1}],\"2\":[{\"id\":22,\"season\":2}]}}");

        Assert.Equal(new[] { 1, 2 }, seasons.Keys.OrderBy(k => k));
        Assert.Equal(11, seasons[1].Single().EpisodeId);
        Assert.Equal(22, seasons[2].Single().EpisodeId);
    }

    [Fact]
    public void Group_SeasonMissingOnSomeEpisodes_KeepsThemInTheirBucket()
    {
        Dictionary<int, List<Episode>> seasons =
            Group("{\"info\":{},\"episodes\":{\"1\":[{\"id\":11,\"season\":1},{\"id\":12}],\"2\":[{\"id\":22,\"season\":2}]}}");

        Assert.Equal(new[] { 1, 2 }, seasons.Keys.OrderBy(k => k));
        Assert.Equal(new[] { 11, 12 }, seasons[1].Select(e => e.EpisodeId).OrderBy(id => id));
    }

    [Fact]
    public void Group_UnparseableSeasonOnSomeEpisodes_KeepsThemInTheirBucket()
    {
        // A season sent as null or a non-number is swallowed by the error handler and left at 0.
        SeriesStreamInfo series = JsonConvert.DeserializeObject<SeriesStreamInfo>(
            "{\"info\":{},\"episodes\":{\"3\":[{\"id\":31,\"season\":3},{\"id\":32,\"season\":null},{\"id\":33,\"season\":\"n/a\"}]}}",
            new JsonSerializerSettings { Error = XtreamClient.NullableEventHandler(NullLogger<XtreamClient>.Instance) })!;

        Dictionary<int, List<Episode>> seasons = StreamService.GroupEpisodesBySeason(series);

        Assert.Equal(new[] { 3 }, seasons.Keys);
        Assert.Equal(new[] { 31, 32, 33 }, seasons[3].Select(e => e.EpisodeId).OrderBy(id => id));
    }

    [Theory]
    [InlineData("\"id\":\"n/a\"")]
    [InlineData("\"id\":0")]
    [InlineData("\"id\":-1")]
    public void Group_EpisodeWithoutUsableId_IsDropped(string id)
    {
        // An unparseable id is swallowed by the error handler and left at 0, so it must be dropped.
        SeriesStreamInfo series = JsonConvert.DeserializeObject<SeriesStreamInfo>(
            $"{{\"info\":{{}},\"episodes\":{{\"1\":[{{{id},\"season\":1}},{{\"id\":12,\"season\":1}}]}}}}",
            new JsonSerializerSettings { Error = XtreamClient.NullableEventHandler(NullLogger<XtreamClient>.Instance) })!;

        Dictionary<int, List<Episode>> seasons = StreamService.GroupEpisodesBySeason(series);

        Assert.Equal(new[] { 12 }, seasons[1].Select(e => e.EpisodeId));
    }

    [Fact]
    public void Group_SeasonWithOnlyUnusableEpisodes_IsNotListed()
    {
        Assert.Empty(Group("{\"info\":{},\"episodes\":{\"1\":[{\"id\":0,\"season\":1}]}}"));
    }

    [Fact]
    public void Resolve_SeasonProducedByTheGrouping_ReturnsItsEpisodes()
    {
        SeriesStreamInfo series = Parse("{\"info\":{},\"episodes\":{\"1\":[{\"id\":11,\"season\":1}]}}");

        Assert.Equal(new[] { 11 }, StreamService.ResolveEpisodes(series, 1).Select(e => e.EpisodeId));
    }

    [Fact]
    public void Resolve_SeasonFolderPredatingTheGrouping_FallsBackToItsStoredKey()
    {
        // The grouping moves this episode out of the catch-all key, leaving a folder for 0 empty.
        SeriesStreamInfo series = Parse("{\"info\":{},\"episodes\":{\"0\":[{\"id\":11,\"season\":2}]}}");

        Assert.DoesNotContain(0, StreamService.GroupEpisodesBySeason(series).Keys);
        Assert.Equal(new[] { 11 }, StreamService.ResolveEpisodes(series, 0).Select(e => e.EpisodeId));
    }

    [Fact]
    public void Resolve_SeasonWhichIsNeitherGroupedNorStored_IsEmpty()
    {
        SeriesStreamInfo series = Parse("{\"info\":{},\"episodes\":{\"1\":[{\"id\":11,\"season\":1}]}}");

        Assert.Empty(StreamService.ResolveEpisodes(series, 7));
    }

    [Fact]
    public void Resolve_StoredKeyHoldingOnlyUnusableEpisodes_IsEmpty()
    {
        SeriesStreamInfo series = Parse("{\"info\":{},\"episodes\":{\"1\":[{\"id\":0,\"season\":2}]}}");

        Assert.Empty(StreamService.ResolveEpisodes(series, 1));
    }

    [Fact]
    public void Group_NoEpisodes_YieldsNoSeasons()
    {
        Assert.Empty(Group("{\"info\":{},\"episodes\":{}}"));
    }

    [Theory]
    [InlineData("{\"info\":{},\"episodes\":{\"0\":[{\"id\":11,\"season\":1},{\"id\":22,\"season\":2}]}}", 2)]
    [InlineData("{\"info\":{},\"episodes\":{\"1\":[{\"id\":11,\"season\":2}],\"2\":[{\"id\":22,\"season\":3}]}}", 2)]
    [InlineData("{\"info\":{},\"episodes\":{\"1\":[],\"2\":[{\"id\":55,\"season\":1}]}}", 1)]
    [InlineData("{\"info\":{},\"episodes\":{\"1\":[{\"id\":11}],\"2\":[{\"id\":22}]}}", 2)]
    [InlineData("{\"info\":{},\"episodes\":{\"1\":[{\"id\":11,\"season\":1},{\"id\":12}],\"2\":[{\"id\":22,\"season\":2}]}}", 3)]
    public void Group_PartitionsEpisodesExactlyOnce(string json, int expected)
    {
        Dictionary<int, List<Episode>> seasons = Group(json);
        List<int> ids = seasons.Values.SelectMany(e => e).Select(e => e.EpisodeId).ToList();

        Assert.Equal(expected, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
