# Xeno.Publisher

Builds the Xeno installers and package-manager manifests from [`publisher.json`](../../publisher.json), following
`~/Code/Agents/requirements/INSTALLERS.md`. It is the only place packaging logic lives: `build-installers.sh`,
`build-installers.bat`, and [`.github/workflows/release.yml`](../../.github/workflows/release.yml) all run it.

```bash
dotnet run --project src/Xeno.Publisher -c Release -- --channel installers --sums
```

| Option | Meaning |
| --- | --- |
| `--channel <ids>` | Channels to run, comma-separated. `installers` means every installer channel this host builds. |
| `--rid <rid>` | Build one runtime instead of the channel's runtimes. |
| `--version <v>` | Override the version (default: `<Version>` in `src/Directory.Build.props`). |
| `--output <dir>` | Output folder (default: `installers/<version>`). |
| `--clean` | Rebuild the local web dashboard even when `dashboard/dist-local` exists. |
| `--sums` | Write `SHA256SUMS` for the output folder. |
| `--keep-staging` | Keep the `.staging` scratch folder. |
| `--list` | List the channels. |
| `--print-version` | Print the version and exit. |

## Channels

| Channel | Host | Produces |
| --- | --- | --- |
| `inno` | Windows (Inno Setup 6) | `xeno-<v>-win-<arch>-setup.exe`: license page, logo, PATH entry, Start menu shortcuts, Xeno Agent at sign-in |
| `macos` | macOS | `xeno-<v>-osx-<arch>.pkg` (license, welcome, `/usr/local/bin/xeno`, LaunchAgent) and `.dmg` |
| `debrpm` | Linux (fpm, rpm) | `xeno_<v>_<arch>.deb`, `xeno-<v>-1.<arch>.rpm`: `/opt/xeno`, `/usr/bin/xeno`, menu entries, XDG autostart |
| `appimage` | Linux (appimagetool) | `xeno-<v>-<arch>.AppImage` |
| `archive` | any (its own OS) | `xeno-<v>-<rid>.zip` or `.tar.gz` with the bare payload |
| `winget`, `scoop`, `choco`, `brewcask` | any | Manifests referencing the built installers by URL and SHA-256 |
| `aptyum` | Linux | apt and yum repository trees for GitHub Pages |
| `dotnettool` | any | The `xeno` .NET tool package |
| `snap`, `flatpak` | | Declared but not implemented |

Every installer channel starts from one payload per runtime: the manifest's pre-build steps (the local web dashboard
that `xeno` embeds), then a self-contained `dotnet publish` of `src/Xeno.Desktop`, which brings `xeno` and `xeno-tray`
into the same folder so the three share one runtime. A channel refuses to run on the wrong host and names the tools it
is missing with the command that installs each.

The package-manager channels read installers another channel already built, so run them after downloading the
installer artifacts into the output folder. In CI they run only on a tag when the repository variable
`ENABLE_PACKAGE_MANAGERS` is `true`.

## Building a release without CI

When GitHub Actions is unavailable, every asset can still be built from one Mac with Docker:

- **macOS:** `./build-installers.sh`.
- **Windows:** the `inno` channel normally runs only on Windows. Set `XENO_ISCC` to `src/Xeno.Publisher/iscc-docker.sh`, which runs Inno Setup's compiler under Wine in the `amake/innosetup` image; the script's paths are then written as Wine `Z:` paths. Build the `.zip` archives with `--channel archive --rid win-x64` (and `win-arm64`).
- **Linux:** run the Publisher with `--channel debrpm,appimage,archive` in a `mcr.microsoft.com/dotnet/sdk:10.0` container with `fpm`, `rpm`, and `appimagetool` installed. Copy the repository into the container's own filesystem first; MSBuild stalls on a macOS bind mount.

Put every file in one folder, run `--sums --output <folder>` (with no channel) to write `SHA256SUMS`, and create the release from the tagged commit.

## Signing

Xeno is not code-signed, by policy. Every target in `publisher.json`'s `signing` block has `isConfigured: false`:
Windows files stay unsigned and macOS bundles and executables are ad-hoc signed (`codesign --sign -`), which Apple
silicon needs to run them and which keeps Gatekeeper from reporting a download as damaged. Setting `isConfigured` and
providing the named secrets turns real signing on without code changes.

## Tests

`src/Test.Shared/PublisherTestSuites.cs` (suite `Publisher`) checks the committed manifest, the single version source,
asset names, every rendered installer file, the generated artwork, and option parsing:

```bash
dotnet run --project src/Test.Automated -- --suite Publisher
```
