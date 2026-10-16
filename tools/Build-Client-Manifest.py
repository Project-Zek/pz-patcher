"""Generate this patcher's MD5 manifest from a versioned client payload.

Run from the repository root after adding all files to client/<snapshot>.
Default, Zeal and pz_ui skin folders are supported, including optional Extras; pz_ui backup files are excluded.
Music and player settings are intentionally excluded from this release channel.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
from urllib.parse import quote

# Remove retired overrides from existing clients and exclude them from downloads.
REMOVED_CLIENT_FILES = ("uifiles/pz_ui/EQUI_AAWindow.xml",)


def build(root, snapshot, version):
    if not re.fullmatch(r"[A-Za-z0-9._-]{1,64}", snapshot) or snapshot in {".", ".."}:
        raise ValueError("Use a simple version folder name, for example 1.0.17.")
    payload = root / "client" / snapshot
    if not payload.is_dir() or payload.is_symlink():
        raise ValueError("Missing ordinary client/" + snapshot + " folder.")
    rows = []
    names = set()
    removed_names = {name.lower() for name in REMOVED_CLIENT_FILES}
    for path in sorted(payload.rglob("*"), key=lambda p: p.as_posix().lower()):
        if path.is_symlink():
            raise ValueError("Links are not allowed: " + str(path))
        if not path.is_file():
            continue
        name = path.relative_to(payload).as_posix()
        lower = name.lower()
        if lower in removed_names:
            continue
        if lower.startswith("uifiles/pz_ui/") and ".bak" in path.name.lower():
            continue
        if path.suffix.lower() in {".mp3", ".wav", ".xmi", ".mid", ".midi", ".emt"}:
            raise ValueError("Music stays in the separate music pack: " + name)
        if lower not in {"zeal.asi", "eqgame.dll", "eqw.dll", "d3d8.dll", "spells_en.txt", "eqstr_en.txt", "eqhost.txt", "uifiles/default/equi_aawindow.xml"} and not lower.startswith(("uifiles/zeal/", "uifiles/pz_ui/")):
            raise ValueError("Unapproved release path (music/settings/base archives stay separate): " + name)
        if lower in names or any(part in {".", ".."} or part.endswith((".", " ")) for part in name.split("/")):
            raise ValueError("Duplicate or unsafe release path: " + name)
        if not re.fullmatch(r"[A-Za-z0-9_./ ()+-]+", name):
            raise ValueError("Unsupported release filename: " + name)
        names.add(lower)
        data = path.read_bytes()
        if not data or len(data) > 25 * 1024 * 1024:
            raise ValueError("Empty file or file too large for GitHub browser upload: " + name)
        rows.append({"path": name, "md5": hashlib.md5(data).hexdigest(), "bytes": len(data)})
    required = {"zeal.asi", "eqgame.dll", "eqw.dll", "d3d8.dll"}
    if not required <= names:
        raise ValueError("Release is missing runtime files: " + str(sorted(required - names)))
    zeal_hash = hashlib.sha256((payload / "Zeal.asi").read_bytes()).hexdigest()
    if zeal_hash.encode("ascii") not in (payload / "eqgame.dll").read_bytes():
        raise ValueError("eqgame.dll must be rebuilt with this Zeal.asi SHA-256 before publishing the pair.")
    prefix = "https://raw.githubusercontent.com/Project-Zek/pz-patcher/main/client/" + quote(snapshot, safe="") + "/"
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d")
    manifest = "version: " + json.dumps(version) + "\ndownloadprefix: " + prefix + "\ndownloads:\n"
    for row in rows:
        manifest += "- name: " + json.dumps(row["path"]) + "\n  md5: " + row["md5"] + "\n  date: \"" + stamp + "\"\n  size: " + str(row["bytes"]) + "\n"
    manifest += "deletes:\n"
    for name in REMOVED_CLIENT_FILES:
        manifest += "- name: " + json.dumps(name) + "\n"
    return manifest, rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--snapshot", required=True, help="Existing folder name under client/")
    parser.add_argument("--version", required=True, help="Human-readable release label")
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    try:
        manifest, rows = build(args.root.resolve(), args.snapshot, args.version)
        (args.root / "filelist.yml").write_text(manifest, encoding="utf-8", newline="\n")
    except (ValueError, OSError) as error:
        parser.exit(1, "Manifest not written: " + str(error) + "\n")
    print("filelist.yml: " + str(len(rows)) + " files, " + str(sum(row["bytes"] for row in rows)) + " bytes; MD5 calculated from exact file bytes.")


if __name__ == "__main__":
    main()
