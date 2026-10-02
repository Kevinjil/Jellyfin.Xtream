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
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Newtonsoft.Json;
using Xunit;

namespace Jellyfin.Xtream.Tests;

/// <summary>
/// Tests for <see cref="SafeDateTimeConverter"/>.
/// </summary>
public class SafeDateTimeConverterTests
{
    [Theory]
    [InlineData("\"2021-05-04\"")]
    [InlineData("\"2021-05-04 00:00:00\"")]
    [InlineData("\"2021-05-04T00:00:00\"")]
    public void ReadJson_ParsesUsableDate(string token)
    {
        VodInfo? info = JsonConvert.DeserializeObject<VodInfo>($"{{\"releasedate\":{token}}}");

        Assert.NotNull(info);
        Assert.Equal(new DateTime(2021, 5, 4, 0, 0, 0, DateTimeKind.Unspecified), info.ReleaseDate);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("\"N/A\"")]
    [InlineData("\"0000-00-00\"")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("0")]
    public void ReadJson_YieldsNullForUnusableToken(string token)
    {
        VodInfo? info = JsonConvert.DeserializeObject<VodInfo>($"{{\"releasedate\":{token}}}");

        Assert.NotNull(info);
        Assert.Null(info.ReleaseDate);
    }

    [Fact]
    public void ReadJson_YieldsNullWhenAbsent()
    {
        VodInfo? info = JsonConvert.DeserializeObject<VodInfo>("{}");

        Assert.NotNull(info);
        Assert.Null(info.ReleaseDate);
    }

    [Theory]
    [InlineData("\"2021-05-04\"", true)]
    [InlineData("{}", false)]
    [InlineData("\"\"", false)]
    public void ReadJson_BehavesIdenticallyForEpisodeAndVod(string token, bool expectDate)
    {
        string json = $"{{\"releasedate\":{token}}}";
        EpisodeInfo? episode = JsonConvert.DeserializeObject<EpisodeInfo>(json);
        VodInfo? vod = JsonConvert.DeserializeObject<VodInfo>(json);

        Assert.NotNull(episode);
        Assert.NotNull(vod);
        Assert.Equal(expectDate, episode.ReleaseDate is not null);
        Assert.Equal(episode.ReleaseDate, vod.ReleaseDate);
    }

    [Fact]
    public void CanConvert_AcceptsOnlyNullableDateTime()
    {
        SafeDateTimeConverter converter = new SafeDateTimeConverter();

        Assert.True(converter.CanConvert(typeof(DateTime?)));
        Assert.False(converter.CanConvert(typeof(DateTime)));
        Assert.False(converter.CanConvert(typeof(string)));
    }
}
