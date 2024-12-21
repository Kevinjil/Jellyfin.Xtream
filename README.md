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
The following credentials should be set correctly in the plugin configuration on the admin dashboard.

| Property | Description                                                                               |
| -------- | ----------------------------------------------------------------------------------------- |
| Base URL | The URL of the API endpoint excluding the trailing slash, including protocol (http/https) |
| Username | The username used to authenticate to the API                                              |
| Password | The password used to authenticate to the API                                              |

## Known problems

### Loss of confidentiality

Jellyfin publishes the remote paths in the API and in the default user interface.
As the Xtream format for remote paths includes the username and password, anyone that can access the library will have access to your credentials.
Use this plugin with caution on shared servers.
