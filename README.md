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
