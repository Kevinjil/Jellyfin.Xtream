#pragma warning disable
// Custom XMLTV guide support for Jellyfin.Xtream.
// This file is part of the Custom EPG overlay and remains GPL-3.0-or-later,
// consistent with the upstream Jellyfin.Xtream project.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using MediaBrowser.Controller.LiveTv;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// A channel exposed by an XMLTV file.
/// </summary>
public sealed class XmlTvChannelInfo
{
    /// <summary>Gets or sets the XMLTV channel id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the preferred display name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Loads and caches a local XMLTV guide. The cache is rebuilt only when the file path,
/// last-write time, or length changes.
/// </summary>
public static class XmlTvGuideService
{
    private static readonly object CacheLock = new();
    private static XmlTvCache? _cache;

    /// <summary>Returns all channels in the configured XMLTV file.</summary>
    public static IReadOnlyList<XmlTvChannelInfo> GetChannels(string path)
    {
        return EnsureCache(path).Channels;
    }

    /// <summary>Returns Jellyfin guide programs for one mapped XMLTV channel.</summary>
    public static IEnumerable<ProgramInfo> GetPrograms(
        string path,
        string epgId,
        string jellyfinChannelId,
        DateTime startDateUtc,
        DateTime endDateUtc)
    {
        XmlTvCache cache = EnsureCache(path);
        if (!cache.Programs.TryGetValue(epgId, out List<XmlTvProgramData>? programs))
        {
            return [];
        }

        return programs
            .Where(program => program.EndDateUtc >= startDateUtc && program.StartDateUtc < endDateUtc)
            .Select(program => ToProgramInfo(program, jellyfinChannelId));
    }

    private static ProgramInfo ToProgramInfo(XmlTvProgramData program, string jellyfinChannelId)
    {
        List<string> categories = program.Categories;
        bool isMovie = HasCategory(categories, "movie") || HasCategory(categories, "film");
        bool isNews = HasCategory(categories, "news");
        bool isSports = HasCategory(categories, "sport");
        bool isKids = HasCategory(categories, "children") || HasCategory(categories, "kids");

        return new ProgramInfo
        {
            Id = string.Format(CultureInfo.InvariantCulture, "{0}_{1:O}", jellyfinChannelId, program.StartDateUtc),
            ChannelId = jellyfinChannelId,
            StartDate = program.StartDateUtc,
            EndDate = program.EndDateUtc,
            Name = string.IsNullOrWhiteSpace(program.Title) ? "Unknown" : program.Title,
            Overview = program.Description,
            EpisodeTitle = program.SubTitle,
            Genres = categories,
            ProductionYear = program.ProductionYear,
            SeasonNumber = program.SeasonNumber,
            EpisodeNumber = program.EpisodeNumber,
            IsSeries = !isMovie && (program.SeasonNumber.HasValue || program.EpisodeNumber.HasValue || !string.IsNullOrWhiteSpace(program.SubTitle)),
            IsRepeat = program.IsPreviouslyShown && !program.IsNew,
            IsPremiere = program.IsPremiere,
            IsLive = program.IsLive,
            IsMovie = isMovie,
            IsNews = isNews,
            IsSports = isSports,
            IsKids = isKids,
            ImageUrl = program.ImageUrl,
            HasImage = !string.IsNullOrWhiteSpace(program.ImageUrl),
        };
    }

    private static bool HasCategory(IEnumerable<string> categories, string value)
    {
        return categories.Any(category => category.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private static XmlTvCache EnsureCache(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("The custom XMLTV path is empty.");
        }

        string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
        var file = new FileInfo(fullPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The configured XMLTV file was not found.", fullPath);
        }

        lock (CacheLock)
        {
            if (_cache is not null
                && string.Equals(_cache.Path, fullPath, StringComparison.OrdinalIgnoreCase)
                && _cache.LastWriteTimeUtc == file.LastWriteTimeUtc
                && _cache.Length == file.Length)
            {
                return _cache;
            }

            try
            {
                XmlTvCache replacement = Load(fullPath, file.LastWriteTimeUtc, file.Length);
                _cache = replacement;
                return replacement;
            }
            catch when (_cache is not null
                        && string.Equals(_cache.Path, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                // If a guide generator is replacing the XMLTV file while Jellyfin refreshes,
                // keep serving the last successfully parsed copy instead of dropping the guide.
                return _cache;
            }
        }
    }

    private static XmlTvCache Load(string path, DateTime lastWriteTimeUtc, long length)
    {
        var channels = new Dictionary<string, XmlTvChannelInfo>(StringComparer.Ordinal);
        var programs = new Dictionary<string, List<XmlTvProgramData>>(StringComparer.Ordinal);

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            CloseInput = true,
        };

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1024 * 128, FileOptions.SequentialScan);
        using XmlReader reader = XmlReader.Create(stream, settings);

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.Name.Equals("channel", StringComparison.OrdinalIgnoreCase))
            {
                XmlTvChannelInfo? channel = ReadChannel(reader);
                if (channel is not null)
                {
                    channels[channel.Id] = channel;
                }
            }
            else if (reader.Name.Equals("programme", StringComparison.OrdinalIgnoreCase))
            {
                XmlTvProgramData? program = ReadProgram(reader);
                if (program is null)
                {
                    continue;
                }

                if (!programs.TryGetValue(program.ChannelId, out List<XmlTvProgramData>? list))
                {
                    list = [];
                    programs[program.ChannelId] = list;
                }

                list.Add(program);
            }
        }

        foreach (List<XmlTvProgramData> list in programs.Values)
        {
            list.Sort((left, right) => left.StartDateUtc.CompareTo(right.StartDateUtc));
        }

        List<XmlTvChannelInfo> orderedChannels = channels.Values
            .OrderBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(channel => channel.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new XmlTvCache(path, lastWriteTimeUtc, length, orderedChannels, programs);
    }

    private static XmlTvChannelInfo? ReadChannel(XmlReader reader)
    {
        string? id = reader.GetAttribute("id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var displayNames = new List<string>();
        using XmlReader subtree = reader.ReadSubtree();
        while (subtree.Read())
        {
            if (subtree.NodeType == XmlNodeType.Element
                && subtree.Name.Equals("display-name", StringComparison.OrdinalIgnoreCase))
            {
                string value = subtree.ReadElementContentAsString().Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    displayNames.Add(value);
                }
            }
        }

        string displayName = displayNames
            .Where(value => value.Length > 0 && !char.IsDigit(value[0]))
            .OrderByDescending(value => value.Length)
            .FirstOrDefault()
            ?? displayNames.OrderByDescending(value => value.Length).FirstOrDefault()
            ?? id;

        return new XmlTvChannelInfo
        {
            Id = id,
            Name = displayName,
        };
    }

    private static XmlTvProgramData? ReadProgram(XmlReader reader)
    {
        string? channelId = reader.GetAttribute("channel");
        string? startText = reader.GetAttribute("start");
        string? stopText = reader.GetAttribute("stop");
        if (string.IsNullOrWhiteSpace(channelId)
            || !TryParseXmlTvDate(startText, out DateTime startDateUtc)
            || !TryParseXmlTvDate(stopText, out DateTime endDateUtc))
        {
            return null;
        }

        string title = string.Empty;
        string? subTitle = null;
        string? description = null;
        string? imageUrl = null;
        int? productionYear = null;
        int? seasonNumber = null;
        int? episodeNumber = null;
        bool previouslyShown = false;
        bool isNew = false;
        bool isPremiere = false;
        bool isLive = false;
        var categories = new List<string>();

        using XmlReader subtree = reader.ReadSubtree();
        while (subtree.Read())
        {
            if (subtree.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (subtree.Name.Equals("title", StringComparison.OrdinalIgnoreCase))
            {
                title = subtree.ReadElementContentAsString().Trim();
            }
            else if (subtree.Name.Equals("sub-title", StringComparison.OrdinalIgnoreCase))
            {
                subTitle = NullIfEmpty(subtree.ReadElementContentAsString());
            }
            else if (subtree.Name.Equals("desc", StringComparison.OrdinalIgnoreCase))
            {
                description = NullIfEmpty(subtree.ReadElementContentAsString());
            }
            else if (subtree.Name.Equals("category", StringComparison.OrdinalIgnoreCase))
            {
                string category = subtree.ReadElementContentAsString().Trim();
                if (!string.IsNullOrWhiteSpace(category) && !categories.Contains(category, StringComparer.OrdinalIgnoreCase))
                {
                    categories.Add(category);
                }
            }
            else if (subtree.Name.Equals("icon", StringComparison.OrdinalIgnoreCase))
            {
                imageUrl ??= NullIfEmpty(subtree.GetAttribute("src"));
            }
            else if (subtree.Name.Equals("date", StringComparison.OrdinalIgnoreCase))
            {
                string date = subtree.ReadElementContentAsString().Trim();
                if (date.Length >= 4 && int.TryParse(date[..4], NumberStyles.None, CultureInfo.InvariantCulture, out int year))
                {
                    productionYear = year;
                }
            }
            else if (subtree.Name.Equals("episode-num", StringComparison.OrdinalIgnoreCase))
            {
                string? system = subtree.GetAttribute("system");
                string value = subtree.ReadElementContentAsString().Trim();
                if (string.Equals(system, "xmltv_ns", StringComparison.OrdinalIgnoreCase))
                {
                    ParseXmlTvNs(value, out seasonNumber, out episodeNumber);
                }
            }
            else if (subtree.Name.Equals("previously-shown", StringComparison.OrdinalIgnoreCase))
            {
                previouslyShown = true;
            }
            else if (subtree.Name.Equals("new", StringComparison.OrdinalIgnoreCase))
            {
                isNew = true;
            }
            else if (subtree.Name.Equals("premiere", StringComparison.OrdinalIgnoreCase))
            {
                isPremiere = true;
            }
            else if (subtree.Name.Equals("live", StringComparison.OrdinalIgnoreCase))
            {
                isLive = true;
            }
        }

        return new XmlTvProgramData(
            channelId,
            startDateUtc,
            endDateUtc,
            title,
            subTitle,
            description,
            categories,
            imageUrl,
            productionYear,
            seasonNumber,
            episodeNumber,
            previouslyShown,
            isNew,
            isPremiere,
            isLive);
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ParseXmlTvNs(string value, out int? seasonNumber, out int? episodeNumber)
    {
        seasonNumber = null;
        episodeNumber = null;
        string[] parts = value.Split('.');
        if (parts.Length > 0 && TryReadXmlTvNsPart(parts[0], out int season))
        {
            seasonNumber = season + 1;
        }

        if (parts.Length > 1 && TryReadXmlTvNsPart(parts[1], out int episode))
        {
            episodeNumber = episode + 1;
        }
    }

    private static bool TryReadXmlTvNsPart(string value, out int number)
    {
        string primary = value.Split('/')[0].Trim();
        return int.TryParse(primary, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
    }

    private static bool TryParseXmlTvDate(string? value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string timestamp = parts[0];
        string format = timestamp.Length >= 14 ? "yyyyMMddHHmmss" : "yyyyMMddHHmm";
        if (timestamp.Length > 14)
        {
            timestamp = timestamp[..14];
        }

        if (!DateTime.TryParseExact(timestamp, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime local))
        {
            return false;
        }

        TimeSpan offset = TimeSpan.Zero;
        if (parts.Length > 1 && TryParseOffset(parts[1], out TimeSpan parsedOffset))
        {
            offset = parsedOffset;
        }

        utc = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), offset).UtcDateTime;
        return true;
    }

    private static bool TryParseOffset(string value, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        if (value.Length < 5 || (value[0] != '+' && value[0] != '-'))
        {
            return false;
        }

        string digits = value[1..].Replace(":", string.Empty, StringComparison.Ordinal);
        if (digits.Length < 4
            || !int.TryParse(digits[..2], NumberStyles.None, CultureInfo.InvariantCulture, out int hours)
            || !int.TryParse(digits.Substring(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out int minutes))
        {
            return false;
        }

        offset = new TimeSpan(hours, minutes, 0);
        if (value[0] == '-')
        {
            offset = -offset;
        }

        return true;
    }

    private sealed class XmlTvCache(
        string path,
        DateTime lastWriteTimeUtc,
        long length,
        List<XmlTvChannelInfo> channels,
        Dictionary<string, List<XmlTvProgramData>> programs)
    {
        public string Path { get; } = path;
        public DateTime LastWriteTimeUtc { get; } = lastWriteTimeUtc;
        public long Length { get; } = length;
        public List<XmlTvChannelInfo> Channels { get; } = channels;
        public Dictionary<string, List<XmlTvProgramData>> Programs { get; } = programs;
    }

    private sealed record XmlTvProgramData(
        string ChannelId,
        DateTime StartDateUtc,
        DateTime EndDateUtc,
        string Title,
        string? SubTitle,
        string? Description,
        List<string> Categories,
        string? ImageUrl,
        int? ProductionYear,
        int? SeasonNumber,
        int? EpisodeNumber,
        bool IsPreviouslyShown,
        bool IsNew,
        bool IsPremiere,
        bool IsLive);
}
#pragma warning restore
