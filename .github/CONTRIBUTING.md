# How to contribute
Any help towards improving Auto Center is greatly appreciated — whether it's new features, bug fixes, or translations!

## Developing
To start developing, load the repository into Visual Studio 2022 (or your IDE of choice). Make sure that Auto Center is closed (it's a single-instance app), and start writing!

Make sure to create a new [fork](https://github.com/jihedkdiss/AutoCenter/fork) of this repository when starting. You can then edit any code in your fork (using branches if needed).

### Build
```
dotnet build AutoCenter/AutoCenter.csproj /p:Platform=x64
```

### Build configurations
- **Debug** — local development
- **Release** — Microsoft Store upload bundle (`.msixupload`). Microsoft signs this on Partner Center, so we don't sign locally.
- **GitHub Release** — sideloadable bundle (`.msixbundle` + `.cer`) signed with the self-signed dev cert in `AutoCenter/AutoCenter_TemporaryKey.pfx`.

The `GITHUB_RELEASE` compile-time symbol is defined for the `GitHub Release` configuration so code can branch on the distribution channel if ever needed (`#if GITHUB_RELEASE`).

### Signing certificate (GitHub Release only)

`AutoCenter/AutoCenter_TemporaryKey.pfx` is a **self-signed code-signing certificate** with `Subject = CN=596D3380-CDB9-4F95-8A87-41D57AB91BFE` (matching the manifest publisher). It is committed to the repo on purpose — it's the only practical way to sign sideloaded MSIX bundles for FOSS Windows projects. The matching public certificate `AutoCenter_TemporaryKey.cer` is what end users install into Trusted Root before the bundle becomes installable.

The certificate's thumbprint is pinned in `AutoCenter.csproj` so every release uses the same cert; users only need to trust it once. **Do not regenerate it without good reason** — doing so forces every existing user to re-trust the new cert on their next update.

If you do need to regenerate (e.g. after expiry — current cert is valid until **May 2036**):
```powershell
cd AutoCenter
.\generate_signing_cert.ps1
```
Update the `<PackageCertificateThumbprint>` in `AutoCenter.csproj` to the new thumbprint printed by the script, and announce the change in your release notes.

### Submitting changes
Once you're ready with your changes, submit a [pull request](https://github.com/jihedkdiss/AutoCenter/compare) describing what's changed, and tag any related issues.

AI usage: AI tools may be used to assist with programming, provided that you design the solution yourself and carefully review the generated code for issues and redundancy. Heavy reliance on AI without a clear understanding of what's going on with your changes will not be merged.

## Marketing assets

Screenshots and promotional artwork live in [`docs/images/`](../docs/images/). They serve three audiences at once:

| Asset | Used by |
|---|---|
| `home-page.png` (clean, ~1858×1042) | Pages site lead, README hero, OG/Twitter card, Microsoft Store screenshot 1 |
| `fluent-design-showcase.png` (clean) | Pages site, README, Microsoft Store screenshot 2 |
| `advanced-settings-showcase.png` (in Windows shell) | Pages site, README, Microsoft Store screenshot 3 |
| `placement-settings-showcase.png` (in Windows shell) | README hero, Microsoft Store screenshot 4 |

**Microsoft Store requirements** (Partner Center → App submission → Store listings → Screenshots):
- **Minimum** 1366×768; **maximum** 2160×1440
- 16:9 aspect ratio strongly recommended
- PNG, JPEG, or JPG; under 2 MB each
- At least one required, up to ten per device family

When you replace or add images, regenerate the manifest icons too:
```bash
python tools/generate_icon.py
```
