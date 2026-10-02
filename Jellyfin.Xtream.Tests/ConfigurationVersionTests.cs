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

using Jellyfin.Xtream.Configuration;
using Xunit;

namespace Jellyfin.Xtream.Tests;

/// <summary>
/// Tests for the configuration component of the channel cache key.
/// </summary>
public class ConfigurationVersionTests
{
    private static PluginConfiguration Base() => new PluginConfiguration
    {
        BaseUrl = "https://example.com",
        Username = "user",
        Password = "secret",
    };

    [Fact]
    public void ComputeConfigurationVersion_IsStableAcrossInstances()
    {
        // Two equivalent instances stand in for the same configuration across a restart.
        Assert.Equal(Plugin.ComputeConfigurationVersion(Base()), Plugin.ComputeConfigurationVersion(Base()));
    }

    [Fact]
    public void ComputeConfigurationVersion_IsStableAcrossCalls()
    {
        PluginConfiguration configuration = Base();

        Assert.Equal(
            Plugin.ComputeConfigurationVersion(configuration),
            Plugin.ComputeConfigurationVersion(configuration));
    }

    public static TheoryData<string, PluginConfiguration> ChangedConfigurations()
    {
        PluginConfiguration url = Base();
        url.BaseUrl = "https://other.example.com";

        PluginConfiguration username = Base();
        username.Username = "someone-else";

        PluginConfiguration password = Base();
        password.Password = "different";

        PluginConfiguration userAgent = Base();
        userAgent.UserAgent = "Custom/1.0";

        PluginConfiguration visibility = Base();
        visibility.IsSeriesVisible = true;

        PluginConfiguration series = Base();
        series.Series.Add(5, new System.Collections.Generic.HashSet<int> { 1, 2 });

        return new TheoryData<string, PluginConfiguration>
        {
            { "BaseUrl", url },
            { "Username", username },
            { "Password", password },
            { "UserAgent", userAgent },
            { "IsSeriesVisible", visibility },
            { "Series", series },
        };
    }

    [Theory]
    [MemberData(nameof(ChangedConfigurations))]
    public void ComputeConfigurationVersion_ChangesWithConfiguration(string changed, PluginConfiguration configuration)
    {
        Assert.True(
            Plugin.ComputeConfigurationVersion(Base()) != Plugin.ComputeConfigurationVersion(configuration),
            $"Changing `{changed}` left the configuration version untouched.");
    }
}
