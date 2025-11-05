// Copyright (C) 2022  Kevin Jilissen

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Client;

/// <summary>
/// Helper class for XMLTV validation and disk caching.
/// </summary>
public static class XmlTvValidation
{
    /// <summary>
    /// Validates XMLTV content to ensure it has historical data for catchup.
    /// </summary>
    /// <param name="xml">The XMLTV content to validate.</param>
    /// <param name="requiredHistoricalDays">The minimum number of historical days required.</param>
    /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateXmlTv(string xml, int requiredHistoricalDays, ILogger logger)
    {
        try
        {
            var doc = XDocument.Parse(xml);

            // Get all programme start times
            var programmes = doc.Descendants("programme")
                .Select(p => p.Attribute("start")?.Value)
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s =>
                {
                    try
                    {
                        if (s is null)
                        {
                            return DateTime.MinValue;
                        }

                        s = s.Replace(" ", string.Empty, StringComparison.Ordinal);
                        if (s.Length > 14)
                        {
                            string zone = s[^5..];
                            if ((zone[0] == '+' || zone[0] == '-') && int.TryParse(zone[1..], out _))
                            {
                                string zoneWithColon = zone.Insert(3, ":");
                                s = s[..^5] + zoneWithColon;
                            }
                        }

                        if (DateTime.TryParseExact(s, "yyyyMMddHHmmsszzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                        {
                            return dt.ToUniversalTime();
                        }

                        return DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                    }
                    catch
                    {
                        return DateTime.MinValue;
                    }
                })
                .Where(dt => dt != DateTime.MinValue)
                .ToList();

            if (programmes.Count == 0)
            {
                logger.LogError("XMLTV validation failed: No valid programme entries found");
                return false;
            }

            var oldestDate = programmes.Min();
            var newestDate = programmes.Max();
            var daysCovered = (newestDate - oldestDate).TotalDays;
            var historicalDays = (DateTime.UtcNow - oldestDate).TotalDays;

            if (historicalDays < requiredHistoricalDays)
            {
                logger.LogError(
                    "XMLTV validation failed: Only {HistoricalDays:F1} days of historical data found, {RequiredDays} days required",
                    historicalDays,
                    requiredHistoricalDays);

                return false;
            }

            logger.LogInformation(
                "XMLTV validation passed: {HistoricalDays:F1} days of historical data, {TotalDays:F1} days total coverage",
                historicalDays,
                daysCovered);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "XMLTV validation failed: Invalid XML format");
            return false;
        }
    }

    /// <summary>
    /// Gets the path to use for disk caching of XMLTV data.
    /// </summary>
    /// <param name="configPath">The configured cache path (may be empty).</param>
    /// <returns>The absolute path to use for caching.</returns>
    public static string GetCachePath(string configPath)
    {
        string path;
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            path = configPath;
            // Create all directories in the configured path
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        else
        {
            // Use plugin data directory
            string dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "jellyfin",
                "plugins",
                "xtream");

            Directory.CreateDirectory(dataPath);
            path = Path.Combine(dataPath, "xmltv_cache.xml");
        }

        return path;
    }

    /// <summary>
    /// Create a mapping of channel IDs to their EPG identifiers based on stream info.
    /// </summary>
    /// <param name="streams">List of stream information.</param>
    /// <returns>Dictionary mapping EPG channel IDs to lists of stream IDs.</returns>
    public static Dictionary<string, HashSet<int>> BuildChannelMapping(IEnumerable<Models.StreamInfo> streams)
    {
        var mapping = new Dictionary<string, HashSet<int>>();
        foreach (var stream in streams)
        {
            // Use EPG channel ID if available, otherwise use stream ID
            string epgId = string.IsNullOrWhiteSpace(stream.EpgChannelId)
                ? stream.StreamId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : stream.EpgChannelId;

            if (!mapping.TryGetValue(epgId, out var streamIds))
            {
                streamIds = new HashSet<int>();
                mapping[epgId] = streamIds;
            }

            streamIds.Add(stream.StreamId);
        }

        return mapping;
    }
}
