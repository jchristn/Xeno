# Installing Xeno

Download the installer for your machine from the [latest release](https://github.com/jchristn/Xeno/releases/latest).
Every installer contains the whole Xeno client, self-contained, so nothing else (not even .NET) needs to be installed:

- the `xeno` command, on your PATH: the client worker, the local web dashboard, and the terminal app (`xeno tui`)
- the Xeno desktop app
- Xeno Agent, the tray agent, which starts when you sign in

The Xeno server is not in these installers; it runs in Docker (see the [README](README.md)). To build Xeno from
source instead, see [getting-started/](getting-started/) and [scripts/README.md](scripts/README.md).

> **Xeno is free, open-source software and is not code-signed** (signing certificates are an ongoing cost we choose
> not to pass on). Your operating system may warn that the developer is unidentified. That warning doesn't mean the
> download is damaged; to confirm a file is the one published, check it against `SHA256SUMS`.

## Which file to download

| Platform | File |
| --- | --- |
| Windows (x64) | `xeno-<version>-win-x64-setup.exe` |
| Windows (ARM64) | `xeno-<version>-win-arm64-setup.exe` |
| macOS (Apple Silicon) | `xeno-<version>-osx-arm64.pkg` |
| macOS (Intel) | `xeno-<version>-osx-x64.pkg` |
| Debian / Ubuntu | `xeno_<version>_amd64.deb` (or `_arm64.deb`) |
| Fedora / RHEL | `xeno-<version>-1.x86_64.rpm` (or `.aarch64.rpm`) |
| Any Linux | `xeno-<version>-x86_64.AppImage` (or `-aarch64.AppImage`) |
| Scripts and containers | `xeno-<version>-<rid>.tar.gz` (macOS and Linux) or `.zip` (Windows) |

Not sure which Mac you have? Apple menu > **About This Mac**: "Chip: Apple M..." is Apple Silicon, "Processor: Intel"
is Intel.

After installing, open a new terminal and run `xeno --version`.

## Windows

Run `xeno-<version>-win-x64-setup.exe` (or the ARM64 one). It shows the license, installs to
`C:\Program Files\Xeno`, adds that folder to the system PATH, and adds **Xeno**, **Xeno Agent**, and
**Xeno Terminal** to the Start menu. Leave **Start Xeno Agent when I sign in** checked to have the tray agent start
at sign-in.

### "Windows protected your PC"

Windows warns in two places. The browser comes first: Chrome and Edge may say the file *isn't commonly downloaded*.
In Chrome choose **Keep**; in Edge open the **...** menu on the download, choose **Keep**, then **Show more > Keep
anyway**. Then, when the installer runs, SmartScreen shows:

> **Windows protected your PC**
> Microsoft Defender SmartScreen prevented an unrecognized app from starting.

Click **More info**, then **Run anyway**. The publisher shows as Unknown publisher.

For the portable `.zip`, unblock it before extracting, or every extracted `.exe` gets the same prompt (or fails
silently from a terminal): right-click > **Properties** > **Unblock**, or in PowerShell:

```powershell
Unblock-File .\xeno-<version>-win-x64.zip
```

If Microsoft Defender (not SmartScreen) quarantines the file as malware, that is a false positive that self-contained
.NET apps sometimes trigger. Submit it at <https://www.microsoft.com/en-us/wdsi/filesubmission> as a software
developer; reclassification usually takes a day or two. Don't turn Defender off.

### Uninstall

**Settings > Apps > Installed apps > Xeno > Uninstall**. It stops the client, removes the PATH entry and the
sign-in item, and leaves your settings in `%USERPROFILE%\.xeno`.

## macOS

Open `xeno-<version>-osx-arm64.pkg` (or the Intel one). The installer shows the license, installs `Xeno.app` in
Applications, links `xeno` into `/usr/local/bin`, and starts Xeno Agent at login through a LaunchAgent. The `.dmg`
holds the same app for a drag-to-Applications install, but it cannot put `xeno` on your PATH or start the agent at
login, so prefer the `.pkg`.

### "Xeno" Not Opened

On macOS 15 (Sequoia) and later, opening the downloaded `.pkg` or app shows:

> **"Xeno" Not Opened**
> Apple could not verify "Xeno" is free of malware that may harm your Mac or compromise your privacy.

Older versions say *"Xeno" cannot be opened because it is from an unidentified developer*. To open it anyway:

1. Click **Done**. Do not click Move to Trash.
2. Open **System Settings > Privacy & Security** and scroll down to **Security**.
3. Next to *"Xeno" was blocked to protect your Mac*, click **Open Anyway**. The button only appears for about an
   hour after the blocked attempt; if it is missing, try to open Xeno again first.
4. Click **Open Anyway** again in the dialog that follows and enter your password. macOS remembers the choice, so you
   only do this once.

Control-click > **Open** used to bypass this prompt and no longer does on macOS 15 and later.

Terminal users can skip the prompt by removing the quarantine attribute the browser set on the download:

```bash
xattr -dr com.apple.quarantine ~/Downloads/xeno-<version>-osx-arm64.pkg   # the .pkg, before opening it
xattr -dr com.apple.quarantine /Applications/Xeno.app                      # the app, after copying it from the .dmg
xattr -d  com.apple.quarantine ./xeno                                      # a bare binary from the .tar.gz
sudo installer -pkg ~/Downloads/xeno-<version>-osx-arm64.pkg -target /    # installs the .pkg with no prompt at all
```

A file fetched with `curl` never gets the quarantine attribute, so `curl -L ... | tar xz` has no prompt.

### Uninstall

```bash
sudo "/Applications/Xeno.app/Contents/Resources/uninstall.sh"
```

It stops the client and the apps and removes the app, `/usr/local/bin/xeno`, the LaunchAgent, and the installer
receipt. Your settings in `~/.xeno` are kept.

## Linux

```bash
sudo apt install ./xeno_<version>_amd64.deb          # Debian, Ubuntu (the ./ matters; without it apt searches its repositories)
sudo dnf install ./xeno-<version>-1.x86_64.rpm        # Fedora, RHEL
```

Use `apt install` rather than `dpkg -i`, because only apt installs the declared dependencies (ICU and the X11 and
fontconfig libraries the desktop app needs). The packages install to `/opt/xeno`, link `xeno` into `/usr/bin`, add
**Xeno**, **Xeno Agent**, and **Xeno Terminal** to the application menu, and start Xeno Agent with each user's desktop
session (`/etc/xdg/autostart`).

The AppImage needs no install: `chmod +x xeno-<version>-x86_64.AppImage`, then run it to open the desktop app, or
pass a command: `./xeno-<version>-x86_64.AppImage xeno status`, `... tui`, or `... tray`. It runs from inside the
image, so it is not on your PATH and does not start at login; older distributions need `libfuse2` to mount it. For
a client that keeps running in the background, use the `.deb` or `.rpm`.

### Uninstall

```bash
sudo apt remove xeno       # or: sudo dnf remove xeno
```

Your settings in `~/.xeno` are kept.

## Portable archives

The `.tar.gz` and `.zip` archives hold the same self-contained files as the installers, for scripts, CI jobs, and
container images that pin a version:

```bash
curl -fsSL https://github.com/jchristn/Xeno/releases/download/v<version>/xeno-<version>-linux-x64.tar.gz | tar xz
./xeno-<version>-linux-x64/xeno --version
```

Keep the files together: `xeno`, `xeno-desktop`, and `xeno-tray` share the runtime in that folder and find each other
there.

## Checking a download

Every release has a `SHA256SUMS` file covering every asset. Download it into the same folder as the file and run:

```bash
shasum -a 256 -c SHA256SUMS --ignore-missing      # macOS
sha256sum -c SHA256SUMS --ignore-missing          # Linux
```

On Windows, compare the output of `Get-FileHash .\xeno-<version>-win-x64-setup.exe` with the file's line in
`SHA256SUMS`.

## If you installed Xeno from source before

The source scripts (`scripts/*/install.sh`) install `xeno` as a .NET tool in `~/.dotnet/tools` and, on macOS, the app
in `~/Applications/Xeno.app`. Those copies keep working beside an installer, but whichever `xeno` comes first on your
PATH wins. To use only the installed one, remove the tool with `dotnet tool uninstall --global xeno` and delete
`~/Applications/Xeno.app` (on Linux, `~/.local/share/xeno/app` and its menu entries). Your settings in `~/.xeno` are
shared by both.
