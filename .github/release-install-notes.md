## Installing

> [!IMPORTANT]
> **Xeno is free, open-source software and is not code-signed** (signing certificates are an ongoing cost we choose
> not to pass on), so Windows and macOS block the first launch. Your operating system may warn that the developer is
> unidentified. That warning doesn't mean the download is damaged. The steps to allow Xeno are below. To confirm a
> file is the one published here, check it against `SHA256SUMS`.

Every installer puts the `xeno` command on your PATH and installs the Xeno desktop app and Xeno Agent, the tray agent,
which starts when you sign in. Nothing else needs to be installed; .NET is included.

### macOS: "Xeno" Not Opened

macOS shows *"Xeno" Not Opened. Apple could not verify "Xeno" is free of malware that may harm your Mac or
compromise your privacy.* with **Move to Trash** and **Done** buttons. To open it anyway:

1. Click **Done**. Do not click Move to Trash.
2. Open **System Settings > Privacy & Security** and scroll down to **Security**.
3. Next to *"Xeno" was blocked to protect your Mac*, click **Open Anyway**. The button only appears for about an
   hour after the blocked attempt; if it is missing, try to open Xeno again first.
4. Click **Open Anyway** again in the dialog that follows and enter your password. You only do this once.

Or skip the prompt in Terminal:

```bash
xattr -dr com.apple.quarantine ~/Downloads/xeno-{version}-osx-arm64.pkg   # before opening the .pkg
xattr -dr com.apple.quarantine /Applications/Xeno.app                     # after copying the app from the .dmg
sudo installer -pkg ~/Downloads/xeno-{version}-osx-arm64.pkg -target /    # installs the .pkg with no prompt at all
```

On macOS 15 and later, Control-click > **Open** no longer bypasses this prompt.

### Windows: "Windows protected your PC"

Click **More info**, then **Run anyway**. If your browser warns that the file *isn't commonly downloaded*, choose
**Keep** (in Edge: the **...** menu on the download, **Keep**, then **Show more > Keep anyway**). For the `.zip`, run
`Unblock-File .\xeno-{version}-win-x64.zip` in PowerShell (or right-click > **Properties** > **Unblock**) before
extracting it.

### Linux

No prompt. `sudo apt install ./xeno_{version}_amd64.deb` (the `./` matters), `sudo dnf install
./xeno-{version}-1.x86_64.rpm`, or `chmod +x` the AppImage and run it.

### Which file to download

| Platform | File |
| --- | --- |
| Windows (x64) | `xeno-{version}-win-x64-setup.exe` |
| Windows (ARM64) | `xeno-{version}-win-arm64-setup.exe` |
| macOS (Apple Silicon) | `xeno-{version}-osx-arm64.pkg` |
| macOS (Intel) | `xeno-{version}-osx-x64.pkg` |
| Debian / Ubuntu | `xeno_{version}_amd64.deb` (or `_arm64.deb`) |
| Fedora / RHEL | `xeno-{version}-1.x86_64.rpm` (or `.aarch64.rpm`) |
| Any Linux | `xeno-{version}-x86_64.AppImage` (or `-aarch64.AppImage`) |
| Scripts and containers | `xeno-{version}-<rid>.tar.gz` / `.zip` |

The `.dmg` is a drag-to-Applications copy of the same app; prefer the `.pkg`, which also adds `xeno` to your PATH
and starts Xeno Agent at login.

Not sure which Mac you have? Apple menu > **About This Mac**: "Chip: Apple M..." is Apple Silicon, "Processor:
Intel" is Intel.

Full instructions, including uninstalling: [INSTALL.md](https://github.com/jchristn/Xeno/blob/main/INSTALL.md). To
build from source, use the **Source code** archives below and follow
[the getting-started guides](https://github.com/jchristn/Xeno/tree/main/getting-started).
