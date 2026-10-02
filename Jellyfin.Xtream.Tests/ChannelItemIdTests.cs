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
using Jellyfin.Xtream.Service;
using Xunit;

namespace Jellyfin.Xtream.Tests;

/// <summary>
/// Tests for the packing of channel item ids, which must round-trip exactly.
/// </summary>
public class ChannelItemIdTests
{
    public static TheoryData<int, int, int> Values()
    {
        TheoryData<int, int, int> data = new TheoryData<int, int, int>();
        int[] values = [0, 1, 2, 7, -1, 1000, int.MaxValue, int.MinValue];
        foreach (int category in values)
        {
            foreach (int series in values)
            {
                foreach (int season in values)
                {
                    data.Add(category, series, season);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void ToGuid_RoundTripsThroughFromGuid(int categoryId, int seriesId, int seasonId)
    {
        Guid guid = StreamService.ToGuid(StreamService.SeasonPrefix, categoryId, seriesId, seasonId);
        StreamService.FromGuid(guid, out int prefix, out int category, out int series, out int season);

        Assert.Equal(StreamService.SeasonPrefix, prefix);
        Assert.Equal(categoryId, category);
        Assert.Equal(seriesId, series);
        Assert.Equal(seasonId, season);
    }

    [Fact]
    public void ToGuid_DistinguishesItemKinds()
    {
        Guid season = StreamService.ToGuid(StreamService.SeasonPrefix, 1, 2, 3);
        Guid series = StreamService.ToGuid(StreamService.SeriesPrefix, 1, 2, 3);

        Assert.NotEqual(season, series);
    }
}
