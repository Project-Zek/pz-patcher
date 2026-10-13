# Connect the Project Zek patcher to GitHub

The current manifest publishes client/1.0.18 with 388 client downloads. The patcher remains version 1.0.17. The new matched Zeal.asi and eqgame.dll include the Windows MIDI exception fix, the character-selection UI guard, improved crash-dump preservation, and removal of the wolf testing login message. Wolf-form behavior is retained. The remaining client files, including the optional pz_ui skin, server spells and eqhost, are copied byte-for-byte from 1.0.17. Music remains a separate optional download. Players still need an existing TAKPv22 client.

The current GitHub source identifies itself as 1.0.14 and uses the older repository address. Repository-files/Patcher-Source replaces it with the complete current 1.0.17 patcher source. The matching source and symbols for the current native pair are in the Project-Zek-Crash-Fix-Quiet-Login-2026-10-13 bundle. The preceding crash-fix pair was reported stable in game; the quiet-login revision passed its build and binary checks.

## Upload to Project-Zek/pz-patcher

1. Open the repository's main branch. Choose Add file, then Upload files. Drag Repository-files/Patcher-Source into the upload area and commit it. This updates the existing source folder; do not put the outer Repository-files folder into GitHub.
2. In another upload, drag Repository-files/client into the repository root and commit it. Keep the complete client/1.0.18 folder layout. These are individual downloads; the patcher cannot extract a client ZIP from the manifest.
3. Upload .gitattributes, tools and HOSTING.md from Repository-files. These are maintainer files, not player downloads. Upload filelist.yml LAST, after the entire client folder exists on GitHub. With Git/PRs, one commit containing the whole snapshot and manifest is also fine.
4. Open the manifest address below. It must show the YAML text rather than a GitHub webpage. In the manifest, each file's name is appended to downloadprefix to form its download address.

Manifest address:
https://raw.githubusercontent.com/Project-Zek/pz-patcher/main/filelist.yml

Example client download:
https://raw.githubusercontent.com/Project-Zek/pz-patcher/main/client/1.0.18/Zeal.asi

## Publish the patcher download

Create a GitHub Release using a new tag such as v1.0.17. Attach Release-assets/Project-Zek-Patcher-1.0.17.zip. It contains only Project-Zek-Patcher.exe and its configured .exe.config. Optionally attach those two individual files as well. GitHub's automatic Source code archives are not the player download.

Players extract both patcher files into their existing client folder, close the game, open Project-Zek-Patcher.exe, press Patch, then Play. Apply any wanted appearance options through Classic fixes. The separate music pack is optional for Play. Settings such as graphics, resolution and frame limit remain local.

The configured patcher will show a download error until filelist.yml and its referenced files have been published. This package has not been uploaded or published on your behalf. After upload, test Patch in a disposable fresh TAKPv22 copy, then login, world entry, zoning and camp. Do not install a different Zeal.asi without its matching eqgame.dll.

## Server allow-list checksums for this update

The two numeric values in the Variables table remain unchanged:

| Variable | File checked | Verified value |
|---|---|---|
| MacPC_AllowedExeChecksums | eqgame.exe | 12296941591618340 |
| MacPC_AllowedSpellChecksums | spells_en.txt | 8329946737700409 |

Both were recalculated from the current client files and match the supplied database values. This update changes neither file. These 64-bit checksums are separate from the MD5 hashes in filelist.yml. The new eqgame.dll also embeds and verifies the SHA-256 of the exact new Zeal.asi: be5d17b051e62135de48b3ef9a49232a807389575f1c2449e545052ddf118784. This internal approval was updated during the rebuild. No database allow-list change is needed for this native pair update.

## Future client updates

Create a NEW folder such as client/1.0.19. Populate it with the full intended runtime/UI set and current server spells_en.txt and eqhost.txt. Never edit an already published snapshot. Build Zeal first, update its approved SHA in eqgame.dll source, then rebuild eqgame.dll. Keep that pair together.

With Python 3 installed, run from the repository root:

    python tools/Build-Client-Manifest.py --snapshot 1.0.19 --version PZ-Client-1.0.19

The script hashes exact bytes, checks the native pair and regenerates filelist.yml. It rejects music, player settings and base-client archives. Upload the new folder first and publish the new manifest last. The same patcher/config will use the updated MD5 values; it has no pinned launch SHA lists to replace.

The .gitattributes rule prevents Git from changing line endings in hashed client files. Preserve path capitalization. Keep music tracks and their EMT/support files in the separate music release. To remove an obsolete file, review and add its FileEntry to deletes; this initial manifest deletes nothing.

## Rebuild the patcher

From Patcher-Source in PowerShell:

    .\Build-Patcher.ps1 -Online -ManifestUrl "https://raw.githubusercontent.com/Project-Zek/pz-patcher/main/filelist.yml"

Use VS2022 with .NET Framework 4.8 tools. Build-Patcher.ps1 restores NuGet dependencies if the sibling pinned Dependency-Cache is absent. The -Online switch matters; the default source build is the explicit offline test mode. App.config in this source is already configured for the new repository.

GitHub upload instructions: https://docs.github.com/en/repositories/working-with-files/managing-files/adding-a-file-to-a-repository
GitHub release instructions: https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository
