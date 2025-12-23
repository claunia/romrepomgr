<div style="text-align: center;">
  <img src="logo.png" alt="logo"/>
</div>

# ROM Repository Manager

ROM Repository Manager is a versatile tool designed to manage your cold storage of ROM sets effortlessly. Whether you're a retro gaming enthusiast or a dedicated collector, ROM Repository Manager streamlines the process of organizing, compressing, and deduplicating your ROM sets.

## Features

* **Organize ROMs**: Automatically sort and categorize ROM files based on the metadata found in widely available DAT files.
* **Deduplicate**: Optimize your storage by ensuring each ROM is stored only once, even if it appears in multiple sets or duplicates within the same set.
* **Compress:** Reduce the size of your ROMs with advanced compression options. Choose LZIP for maximum space savings or ZSTD for faster performance, both surpassing the typical ZIP files used in ROM sets.
* **Cross-Platform**: Compatible with Windows, macOS, and Linux.
* **User-Friendly Interface**: Intuitive and easy-to-use interface for seamless management. Choose between the desktop application for direct access or deploy it on a server to use through your browser (e.g., in a NAS). The choice is yours!
* **Virtual filesystem**: A standout feature of our desktop application is its ability to provide direct access to the ROM set from our repository. This is achieved through a virtual filesystem, allowing you to view and interact with the ROM set in its original folder structure without the need for extraction.

## Applications

ROM Repository Manager contains two different applications:

* **Desktop Application**: Built using .NET, this application provides a robust and feature-rich interface for managing ROM repositories on your computer.
* **Blazor Application**: A web-based application built with .NET Blazor, offering seamless access to ROM management features through your browser.

Both applications are available as pre-compiled binaries in the releases section. Users do not need to compile anything themselves.

## Usage

1. Launch the application.
2. Import the DAT files to get started.
3. Import your ROM sets.
4. Let ROM Repository Manager store your ROMs deduplicated and compressed.

## How does it compare?

When comparing with the ROM set from MAME 0.278, which occupies 78.2Gb compressed, 169Gb uncompressed:

* Using LZIP compression, ROM Repository Manager reduces the size to **68.5Gb**.
* Using ZSTD compression, the size is reduced to **71.4Gb**.

### Let the filesystem do it

A filesystem can improve our deduplication, adding block-level optimization on top of our file-level strategy, and achieving slightly better results. For example:

* **ZFS**: Reduces the repository to **71.3Gb** with ZSTD compression.
* **btrfs**: Reduces the repository to **85.6Gb**.

While these options deliver marginal gains in space and speed, they come at the cost of significantly higher RAM usage. Consider them only if your system has the resources to support it.

## Blazor Application Configuration

To configure the Blazor application, you need to modify the `appsettings.json` file. Below are the valid fields you can change, along with their purposes:

- **LogLevel**: Adjusts the level of detail written to the logs. Valid values are`Information` and `Debug`

- **LogFile**: Specifies the location where the log is written.

- **Repository**: Defines the location where the repository resides.

- **ImportRoms**: Sets the location where the application will look for ROMs to import.

- **ImportDats**: Sets the location where the application will look for DATs to import.

- **ExportRoms**: Specifies the location where the application will export ROMs.

- **ExportDats**: Specifies the location where the application will export DATs.

- **Database**: Defines the location where the repository database will reside.

- **Temporary**: Specifies the location where the application will store temporary files.

- **CompressionType**: Determines the repository compression algorithm used. This setting only applies to newly added ROMs. Valid values are `Lzip`, `Zstd` and `None`

## Contributing

Contributions are welcome! If you have ideas for new features or improvements, feel free to open an issue or submit a pull request.

## License

This project is licensed under the GPL version 3. See the LICENSE file for details.

---

For more information, visit the [GitHub repository](https://github.com/claunia/RomRepoMgr).

© 2020-2026 [Nat Portillo](https://www.natportillo.es)

Happy ROM managing!
