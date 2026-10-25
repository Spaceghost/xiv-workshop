#!/usr/bin/env python3
"""Stage a local Dalamud developer plugin; optionally register it while offline."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
import stat
import tempfile
import uuid

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--register", action="store_true")
parser.add_argument("--destination", type=Path, default=Path.home() / "dalamud-plugins/XivWorkshop")
parser.add_argument("--config", type=Path, default=Path.home() / ".xlcore/dalamudConfig.json")
parser.add_argument("--wine-drive", default="Z:")
args = parser.parse_args()

for proc in Path("/proc").glob("[0-9]*/comm"):
    try:
        if proc.read_text().strip().lower() in ("ffxiv_dx11.exe", "ffxiv.exe"):
            raise SystemExit("Close FFXIV before staging or registering the developer plugin.")
    except (FileNotFoundError, PermissionError, ProcessLookupError):
        pass

files = ["XivWorkshop.dll", "XivWorkshop.json", "XivWorkshop.deps.json", "XivWorkshop.pdb",
         "xiv_workshop.dll", "lua/workshop.lua", "examples/starter.json"]
for name in files:
    if not (root / "build/plugin" / name).is_file():
        raise SystemExit("Build the complete plugin first: tools/build.sh all")

config = None
if args.register:
    config = json.loads(args.config.read_text())
    if not isinstance(config.get("DevPluginLoadLocations", {}).get("$values"), list):
        raise SystemExit("Unrecognized Dalamud developer-plugin config; nothing changed.")

stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S.%fZ")
backup = Path.home() / ".cache/xiv-workshop/backups" / stamp
backup.mkdir(parents=True, mode=0o700)
destination = args.destination.resolve()
if destination.exists():
    shutil.copytree(destination, backup / "plugin")
destination.mkdir(parents=True, exist_ok=True)
for name in files:
    target = destination / name
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(root / "build/plugin" / name, target)

if config is not None:
    saved_config = backup / "dalamudConfig.json"
    shutil.copy2(args.config, saved_config)
    saved_config.chmod(0o600)
    dll = args.wine_drive + str(destination / "XivWorkshop.dll").replace("/", "\\")
    entries = config["DevPluginLoadLocations"]["$values"]
    entry = next((entry for entry in entries if entry.get("Path", "").casefold() == dll.casefold()), None)
    if entry is None:
        entries.append({"$type": "Dalamud.Configuration.DevPluginLocationSettings, Dalamud",
                        "Path": dll, "IsEnabled": True, "Nickname": "XIV Workshop"})
    else:
        entry["IsEnabled"] = True
    settings = config.setdefault("DevPluginSettings", {
        "$type": "System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib],[Dalamud.Configuration.Internal.DevPluginSettings, Dalamud]], System.Private.CoreLib"})
    settings.setdefault(dll, {
        "$type": "Dalamud.Configuration.Internal.DevPluginSettings, Dalamud",
        "StartOnBoot": True, "NotifyForErrors": True, "AutomaticReloading": True,
        "WorkingPluginId": str(uuid.uuid4()),
        "DismissedValidationProblems": {
            "$type": "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib",
            "$values": []}})
    mode = stat.S_IMODE(args.config.stat().st_mode)
    fd, name = tempfile.mkstemp(prefix="workshop-", dir=args.config.parent)
    try:
        with os.fdopen(fd, "w") as stream:
            json.dump(config, stream, indent=2)
            stream.write("\n")
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(name, mode)
        os.replace(name, args.config)
    finally:
        if os.path.exists(name):
            os.unlink(name)
    print("Registered:", dll)
print("Staged:", destination)
print("Backup:", backup)
