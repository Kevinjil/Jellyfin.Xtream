# Jellyfin.Xtream
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/Kevinjil/Jellyfin.Xtream/total)
![GitHub Downloads (all assets, latest release)](https://img.shields.io/github/downloads/Kevinjil/Jellyfin.Xtream/latest/total)
![GitHub commits since latest release](https://img.shields.io/github/commits-since/Kevinjil/Jellyfin.Xtream/latest)
![Dynamic YAML Badge](https://img.shields.io/badge/dynamic/yaml?url=https%3A%2F%2Fraw.githubusercontent.com%2FKevinjil%2FJellyfin.Xtream%2Frefs%2Fheads%2Fmaster%2Fbuild.yaml&query=targetAbi&label=Jellyfin%20ABI)
![Dynamic YAML Badge](https://img.shields.io/badge/dynamic/yaml?url=https%3A%2F%2Fraw.githubusercontent.com%2FKevinjil%2FJellyfin.Xtream%2Frefs%2Fheads%2Fmaster%2Fbuild.yaml&query=framework&label=.NET%20framework)
The Jellyfin.Xtream plugin can be used to integrate the content provided by an [Xtream-compatible API](https://xtream-ui.org/api-xtreamui-xtreamcode/) in your [Jellyfin](https://jellyfin.org/) instance.

## Installation

The plugin can be installed using a custom plugin repository.
To add the repository, follow these steps:

1. Open your admin dashboard and navigate to `Plugins`.
1. Select the `Repositories` tab on the top of the page.
1. Click the `+` symbol to add a repository.
1. Enter `Jellyfin.Xtream` as the repository name.
1. Enter [`https://kevinjil.github.io/Jellyfin.Xtream/repository.json`](https://kevinjil.github.io/Jellyfin.Xtream/repository.json) as the repository url.
1. Click save.

To install or update the plugin, follow these steps:

1. Open your admin dashboard and navigate to `Plugins`.
1. Select the `Catalog` tab on the top of the page.
1. Under `Live TV`, select `Jellyfin Xtream`.
1. (Optional) Select the desired plugin version.
1. Click `Install`.
1. Restart your Jellyfin server to complete the installation.

## Configuration

The plugin requires connection information for an [Xtream-compatible API](https://xtream-ui.org/api-xtreamui-xtreamcode/).
The following credentials should be set correctly in the `Credentials` plugin configuration tab on the admin dashboard.

| Property | Description                                                                               |
| -------- | ----------------------------------------------------------------------------------------- |
| Base URL | The URL of the API endpoint excluding the trailing slash, including protocol (http/https) |
| Username | The username used to authenticate to the API                                              |
| Password | The password used to authenticate to the API                                              |

### Live TV

1. Open the `Live TV` configuration tab.
1. Select the categories, or individual channels within categories, you want to be available.
1. Click `Save` on the bottom of the page.
1. Open the `TV Overrides` configuration tab.
1. Modify the channel numbers, names, and icons if desired.
1. Click `Save` on the bottom of the page.

### External XMLTV EPG

Jellyfin.Xtream can optionally use an external XMLTV file for Live TV guide data while continuing to use Xtream for channel discovery and stream playback.

This is useful when an Xtream provider has missing or incomplete EPG information, or when a separate XMLTV source provides richer metadata such as program descriptions.

#### EPG modes

Three guide modes are available:

- **Xtream EPG** — Uses the EPG supplied by the Xtream provider. This remains the default behavior.
- **Custom XMLTV** — Uses the configured XMLTV file for mapped channels instead of the Xtream EPG.
- **Custom XMLTV + Xtream fallback** — Uses XMLTV for mapped channels and falls back to the Xtream EPG when no XMLTV mapping is configured.

The XMLTV setting affects guide information only. Channel playback continues to use the existing Xtream stream.

#### Configure an XMLTV file

1. Open the Jellyfin admin dashboard.
1. Navigate to `Plugins` → `Jellyfin Xtream` → `TV Overrides`.
1. Under `Custom EPG`, select the desired EPG mode.
1. Enter the full local path to the XMLTV file on the Jellyfin server.
1. Click `Save`.
1. Reopen the `TV Overrides` page so the plugin can load the XMLTV channel list.

Example XMLTV path on Windows:

```text
D:\EPG\guide.xmltv
```

The Jellyfin server process must have permission to read the configured file.

#### Map Xtream channels to XMLTV channels

Each Xtream channel has an optional `Custom EPG ID` field.

Select or enter the XMLTV channel ID that should provide guide information for that Xtream channel.

For example:

```text
Xtream Channel: CBS
Custom EPG ID: EPG123.10367.schedulesdirect.org
```

The XMLTV file should contain a corresponding channel entry:

```xml
<channel id="EPG123.10367.schedulesdirect.org">
    <display-name>CBS</display-name>
</channel>
```

Programme entries that use the same channel ID will then be supplied to Jellyfin for the mapped Xtream channel.

After saving the mappings, open `Dashboard` → `Live TV` and run `Refresh Guide Data`.

Depending on the XMLTV source, guide information may include:

- Program titles
- Program descriptions
- Episode titles
- Categories
- Season and episode numbers
- Start and end times
- Program images

#### Building from source

The project currently targets .NET 9.

Clone or download the repository and run:

```powershell
dotnet restore
dotnet build Jellyfin.Xtream.sln -c Release
```

The resulting plugin DLL will normally be located at:

```text
Jellyfin.Xtream\bin\Release\net9.0\Jellyfin.Xtream.dll
```

#### Installing a custom build

Before replacing an existing Jellyfin.Xtream installation:

1. Stop the Jellyfin server.
1. Back up the currently installed `Jellyfin.Xtream.dll`.
1. Replace it with the newly compiled `Jellyfin.Xtream.dll`.
1. Start Jellyfin again.

To roll back, stop Jellyfin, restore the original DLL, and restart Jellyfin.

> External XMLTV support changes guide-data retrieval only. Existing Xtream stream playback remains unchanged.

### Video On-Demand

1. Open the `Video On-Demand` configuration tab.
1. Enable `Show this channel to users`.
1. Select the categories, or individual videos within categories, you want to be available.
1. Click `Save` on the bottom of the page.

### Series

1. Open the `Series` configuration tab.
1. Enable `Show this channel to users`.
1. Select the categories, or individual series within categories, you want to be available.
1. Click `Save` on the bottom of the page.

### TV Catchup

1. Open the `Live TV` configuration tab.
1. Enable `Show the catch-up channel to users`.
1. Click `Save` on the bottom of the page.

## Known problems

### Loss of confidentiality

Jellyfin publishes the remote paths in the API and in the default user interface.
As the Xtream format for remote paths includes the username and password, anyone that can access the library will have access to your credentials.
Use this plugin with caution on shared servers.

## Troubleshooting

Make sure you have correctly configured your [Jellyfin networking](https://jellyfin.org/docs/general/networking/):

1. Open your admin dashboard and navigate to `Networking`.
2. Correctly configure your `Published server URIs`.
   For example: `all=https://jellyfin.example.com`
