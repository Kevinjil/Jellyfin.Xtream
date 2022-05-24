# Jellyfin.Xtream

The Jellyfin.Xtream plugin can be used to integrate the content provided by an [Xtream-compatible API](https://xtream-ui.org/api-xtreamui-xtreamcode/) in your [Jellyfin](https://jellyfin.org/) instance.

## Configuration

The plugin requires connection information for an [Xtream-compatible API](https://xtream-ui.org/api-xtreamui-xtreamcode/). The following credentials should be set correctly in the plugin configuration on the admin dashboard.

| Property | Description |
|----------|-------------|
| Base URL | The URL of the API endpoint excluding the trailing slash, including protocol (http/https) |
| Username | The username used to authenticate to the API |
| Password | The password used to authenticate to the API |

## Known problems

### Loss of confidentiality
Jellyfin publishes the remote paths in the API and in the default user interface. As the Xtream format for remote paths includes the username and password, anyone that can access the library will have access to your credentials. Use this plugin with caution on shared servers.
