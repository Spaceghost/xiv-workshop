#!/usr/bin/env python3
"""Zip build/plugin into release/: latest.zip, XivWorkshop-<version>.zip, pluginmaster.json.

  tools/package.py v0.1.0     the tag must name the manifest's AssemblyVersion
"""
import json
import shutil
import sys
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
plugin = root / "build/plugin"
out = root / "release"
repo = "Spaceghost/xiv-workshop"
tag = sys.argv[1]

manifest = json.loads((root / "src/XivWorkshop/XivWorkshop.json").read_text())
version = manifest["AssemblyVersion"]
if tag.lstrip("v").split("-")[0] not in (version, version.removesuffix(".0")):
    raise SystemExit(f"tag {tag} does not name AssemblyVersion {version}")

files = ["XivWorkshop.dll", "XivWorkshop.json", "XivWorkshop.deps.json", "XivWorkshop.pdb",
         "xiv_workshop.dll", "lua/workshop.lua"] + [p.relative_to(plugin).as_posix() for p in sorted(plugin.glob("examples/*.json"))]
missing = [f for f in files if not (plugin / f).is_file()]
if missing:
    raise SystemExit(f"build/plugin is missing {missing}; run tools/build.sh windows")

shutil.rmtree(out, ignore_errors=True)
out.mkdir()
zip_path = out / "latest.zip"
with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
    for f in files:
        z.write(plugin / f, f)
shutil.copy2(zip_path, out / f"XivWorkshop-{version}.zip")

testing = "-" in tag
url = f"https://github.com/{repo}/releases/download/{tag}/latest.zip"
entry = {**manifest, "RepoUrl": f"https://github.com/{repo}", "DownloadLinkInstall": url, "DownloadLinkUpdate": url}
if testing:
    entry |= {"TestingAssemblyVersion": version, "DownloadLinkTesting": url}
name = "pluginmaster-testing.json" if testing else "pluginmaster.json"
(out / name).write_text(json.dumps([entry], indent=2) + "\n")
print(f"packaged XivWorkshop {version} ({len(files)} files) -> {out}")
