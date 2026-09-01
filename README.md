# extension-local-packager

Build a per-user Windows installer for an unpacked Chrome extension and its Native Messaging updater.

```powershell
npx --yes https://github.com/endlessbaum/extension-local-packager.git init
npx --yes https://github.com/endlessbaum/extension-local-packager.git validate
npx --yes https://github.com/endlessbaum/extension-local-packager.git build
```

On Windows, `build` creates `Setup.exe` and `Uninstall.exe` using the compiler included
with Windows PowerShell/.NET Framework. No .NET SDK is required. Setup embeds the
Native Messaging updater, registers it under HKCU, downloads and verifies `dist.zip`,
and adds an entry to Windows Installed Apps. The generated CMD/PowerShell scripts are
also retained as a transparent fallback.

## Configuration

```json
{
  "appId": "youtube-discovery",
  "displayName": "YouTube Discovery",
  "publisherId": "endlessbaum",
  "extension": { "manifest": "./dist/manifest.json" },
  "update": {
    "manifestUrl": "https://raw.githubusercontent.com/USER/REPOSITORY/main/latest.json"
  }
}
```

The extension manifest must contain a stable `key`. The packager derives the Chrome
Extension ID from it and restricts the Native Messaging host to that origin.

The remote metadata document is:

```json
{
  "version": "1.2.0",
  "url": "https://github.com/USER/REPOSITORY/releases/download/v1.2.0/dist.zip",
  "sha256": "64 lowercase or uppercase hexadecimal characters"
}
```

`dist.zip` must contain `manifest.json` at its root. Both the initial installation and
updates verify the ZIP checksum, manifest version, and extension key before replacing
the installed extension. Failed replacements restore the previous directory.

## Calling the updater from the extension

Add `nativeMessaging` to the extension permissions, then call the generated host name:

```js
const result = await chrome.runtime.sendNativeMessage(
  'com.endlessbaum.youtube_discovery',
  { action: 'update' }
);

if (result.ok && result.needsReload) chrome.runtime.reload();
```

The host only accepts `checkUpdate` and `update`; it cannot execute arbitrary commands.
