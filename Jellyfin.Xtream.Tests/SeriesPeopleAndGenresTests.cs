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
/// Tests that the cast and genres of a series are read without failing on a missing value.
/// </summary>
public class SeriesPeopleAndGenresTests
{
    [Fact]
    public void Deserialize_NullCastAndGenre_YieldsNull()
    {
        List<Series>? series = JsonConvert.DeserializeObject<List<Series>>(
            "[{\"series_id\":1,\"name\":\"Show\",\"cast\":null,\"genre\":null}]",
            new JsonSerializerSettings { Error = XtreamClient.NullableEventHandler(NullLogger<XtreamClient>.Instance) });

        Assert.NotNull(series);
        Assert.Null(series.Single().Cast);
        Assert.Null(series.Single().Genre);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GetPeople_NoCast_IsEmpty(string? cast)
    {
        Assert.Empty(SeriesChannel.GetPeople(cast));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GetGenres_NoGenre_IsEmpty(string? genre)
    {
        Assert.Empty(SeriesChannel.GetGenres(genre));
    }

    [Fact]
    public void GetPeople_Cast_IsSplitAndTrimmed()
    {
        Assert.Equal(new[] { "Ann", "Bob" }, SeriesChannel.GetPeople("Ann, Bob").Select(p => p.Name));
    }

    [Fact]
    public void GetGenres_Genres_AreSplitAndTrimmed()
    {
        Assert.Equal(new[] { "Drama", "Comedy" }, SeriesChannel.GetGenres("Drama, Comedy"));
    }
}
