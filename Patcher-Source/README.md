# Project Zek patcher source — 1.0.19.0

The patcher downloads and repairs the client files listed in the current MD5 manifest, then launches the game and closes. Graphics, Zeal frame limit and resolution choices are remembered. All integrated Classic fixes and optional music settings remain included. Windows controls monitor refresh; there is no refresh selector.

App.config points to the current Project-Zek/pz-patcher manifest. Publish the manifest and all referenced files before distributing the configured patcher. See the repository's HOSTING.md for the upload order and future updates. The separate music pack is optional for Play.

Applying Classic music saves `ClassicMusic=TRUE` under `[Options]` in `eqclient.ini`, and enables `ClassicMusic` and `ClassicMenuMusic` under `[Zeal]` in `zeal.ini`. Players do not need to enable the Zeal music option in game. Other preferences are preserved, and Restore returns both files to their previous state. Selecting music and spell effects together saves both sets of preferences.

Build an online executable with VS2022 and .NET Framework 4.8 tools:

    .\Build-Patcher.ps1 -Online -ManifestUrl "https://raw.githubusercontent.com/Project-Zek/pz-patcher/main/filelist.yml"

Without -Online, the source defaults to explicit offline test mode. The script uses the sibling pinned Dependency-Cache when available; otherwise it restores NuGet dependencies. The complete native Zeal, eqgame.dll and EQW sources are in the separate All-Fixes-1.0.17 source bundle. This repository folder contains the patcher source.

The patcher uses the downloaded manifest's current MD5 values rather than fixed launch SHA lists. Zeal.asi and eqgame.dll must still be released as a matched native pair. Appearance changes rebuild the user's local assets through Classic fixes; base client archives and music are not part of the core hosted patch.

Version 1.0.18 supports extended Windows paths for nested patch downloads and backups after normal manifest path validation. This allows optional pz_ui Extras to install under longer client folder paths. No system registry changes are needed. Native client files remain the matched 1.0.17 pair.

Version 1.0.19 refreshes the server manifest whenever Patch is pressed, before reading or downloading client entries. A failed refresh stops the patch instead of applying a stale file list. Metadata requests disable caching, and GitHub raw metadata uses a unique query to avoid stale CDN responses. The existing validated client transaction and long-path support are retained.
