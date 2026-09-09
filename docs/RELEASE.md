# Release

## Version source

`Directory.Build.props` is the source for assembly, informational, file, and
four-part MSIX versions. The checked-in manifest identity is kept aligned, while
MSBuild supplies `AppxPackageVersion` during package generation. Glass is at
`0.9.0` (`0.9.0.0` for MSIX) for Phase 5 release-candidate progression.

## Build models

The default developer build is unpackaged:

```powershell
dotnet restore Glass.sln
dotnet build src/Glass.App/Glass.App.csproj -c Release -p:Platform=x64
```

Production release packaging is self-contained single-project MSIX. For an
individual architecture:

```powershell
dotnet publish src/Glass.App/Glass.App.csproj -c Release -r win-x64 `
  -p:Platform=x64 -p:GlassPackageMode=Packaged `
  -p:AppxPackageSigningEnabled=false
```

Use `win-arm64`/`ARM64` or `win-x86`/`x86` for the other outputs. Single-project
MSIX emits one package per invocation; the release workflow uses Windows SDK
MakeAppx to combine the three same-identity packages into one `.msixbundle`.

## Release workflow

`.github/workflows/release-package.yml` runs on manual dispatch and `v*` tags.
It restores and tests, builds all three architecture packages, creates and
unpacks the bundle for semantic validation, conditionally signs, optionally
generates App Installer metadata, and uploads artifacts. It does not create or
publish a GitHub Release.

Set these repository values for a distributable direct release:

- variable `GLASS_PACKAGE_PUBLISHER`: production certificate subject;
- variable `GLASS_RELEASE_BASE_URI`: HTTPS directory containing the bundle and
  `Glass.appinstaller`;
- secret `GLASS_SIGNING_PFX_BASE64`: base64-encoded public-release PFX;
- secret `GLASS_SIGNING_PASSWORD`: PFX password.

The certificate subject must match the package publisher. Signing credentials
are external release prerequisites. An unsigned artifact must never be described
as a public production installer.

## App Installer updates

`scripts/Generate-AppInstaller.ps1` substitutes the real publisher, version, bundle
URI, and App Installer URI into the checked-in template. Windows App Installer
checks on launch no more often than every eight hours and also uses its supported
background update task. Glass does not run an updater service.

The release host must serve the `.appinstaller` and `.msixbundle` over HTTPS with
stable paths. Microsoft Store distribution is an alternative channel using the
same package and source tree; Store policy controls updates in that channel.

## Pre-release checklist

1. Advance the version in `Directory.Build.props` and align the checked-in
   manifest version.
2. Confirm all Windows CI jobs pass.
3. Run the package workflow and inspect all architecture MSIX outputs.
4. Confirm package publisher and production certificate subject match.
5. Verify signature and install/update/uninstall on supported real hardware.
6. Publish artifacts only after Phase 5 manual QA approves the release candidate.
