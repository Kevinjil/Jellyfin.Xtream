namespace Jellyfin.Xtream.Api.Models;

/// <summary>
/// Request model for logging configuration changes.
/// </summary>
public class LogConfigChangeRequest
{
    /// <summary>
    /// Gets or sets the name of the configuration page that was updated.
    /// </summary>
    public string Page { get; set; } = string.Empty;
}
