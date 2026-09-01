# extension-local-packager

Build a per-user Windows installer for an unpacked Chrome extension and its Native Messaging updater.

```powershell
npx --yes https://github.com/endlessbaum/extension-local-packager.git init
npx --yes https://github.com/endlessbaum/extension-local-packager.git validate
npx --yes https://github.com/endlessbaum/extension-local-packager.git build
```

`build` currently creates zero-install Windows bootstrap scripts (`Setup.cmd` and
`Uninstall.cmd`). Native Messaging host registration and standalone EXE generation
are the next implementation milestone.
