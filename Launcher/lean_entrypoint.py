#!/usr/bin/env python3

import json
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any, Dict


LAUNCHER_DIR = Path(__file__).resolve().parent
CONFIG_PATH = LAUNCHER_DIR / "config.json"
MARKET_HOURS_BACKUP = Path("/Lean/Data-market-hours")
SYMBOL_PROPERTIES_BACKUP = Path("/Lean/Data-symbol-properties")
MARKET_HOURS_TARGET = Path("/Lean/Data/market-hours")
SYMBOL_PROPERTIES_TARGET = Path("/Lean/Data/symbol-properties")
DEFAULT_MAIN_COMMAND = ["dotnet", "QuantConnect.Lean.Launcher.dll"]


def _copy_directory_contents(source: Path, target: Path) -> None:
    if not source.exists():
        return

    target.mkdir(parents=True, exist_ok=True)
    for item in source.iterdir():
        destination = target / item.name
        if item.is_dir():
            shutil.copytree(item, destination, dirs_exist_ok=True)
        else:
            shutil.copy2(item, destination)


def _restore_reference_data() -> None:
    _copy_directory_contents(MARKET_HOURS_BACKUP, MARKET_HOURS_TARGET)
    _copy_directory_contents(SYMBOL_PROPERTIES_BACKUP, SYMBOL_PROPERTIES_TARGET)


def _load_config() -> Dict[str, Any]:
    if not CONFIG_PATH.exists():
        return {}

    with CONFIG_PATH.open(encoding="utf-8") as file:
        return json.load(file)


def main() -> int:
    _restore_reference_data()
    _load_config()

    command = sys.argv[1:] or DEFAULT_MAIN_COMMAND
    process = subprocess.Popen(command, cwd=str(LAUNCHER_DIR))
    return process.wait()


if __name__ == "__main__":
    sys.exit(main())
