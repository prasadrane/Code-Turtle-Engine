"""Stage only selected NuGet archives from an explicit local cache; never restore/download."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil


def prepare(assets_file, cache_directory, output_directory):
    cache = Path(cache_directory).resolve(strict=True)
    if not cache.is_dir():
        raise ValueError("The explicit package cache must be a directory")
    output = Path(output_directory).absolute()
    if output.exists():
        raise ValueError("Choose a fresh output directory")
    assets = json.loads(Path(assets_file).read_text(encoding="utf-8-sig"))
    selected = []
    for identity, metadata in assets["libraries"].items():
        if metadata.get("type") != "package":
            continue
        parts = identity.split("/")
        if len(parts) != 2 or any(not re.fullmatch(r"[A-Za-z0-9_.+-]+", part) or part in (".", "..") for part in parts):
            raise ValueError("Invalid package identity in assets file")
        package, version = (part.lower() for part in parts)
        filename = f"{package}.{version}.nupkg"
        archive = (cache / package / version / filename).resolve(strict=True)
        if not archive.is_relative_to(cache) or not archive.is_file():
            raise ValueError("Package archive must be inside the explicit cache")
        selected.append((identity, archive, filename))
    # Validate all inputs before creating anything. Do not copy NuGet.Config,
    # extracted package content, source URLs, or cached credential material.
    output.mkdir(parents=True)
    feed = output / "feed"
    feed.mkdir()
    manifest = []
    for identity, archive, filename in sorted(selected):
        destination = feed / filename
        shutil.copyfile(archive, destination)
        manifest.append({"package": identity, "file": filename,
                         "sha256": hashlib.sha256(destination.read_bytes()).hexdigest()})
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    configuration = {"WebDependencies": {"Enabled": True, "OfflineSources": [str(feed)], "TimeoutSeconds": 45}}
    (output / "appsettings.WebOffline.json").write_text(json.dumps(configuration, indent=2) + "\n", encoding="utf-8")
    return feed, len(selected)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assets", required=True, help="Existing project.assets.json from the trusted target project")
    parser.add_argument("--cache", required=True, help="Explicit local NuGet global-packages directory")
    parser.add_argument("--output", required=True, help="Fresh local output directory (for example work/web-offline)")
    args = parser.parse_args()
    try:
        feed, count = prepare(args.assets, args.cache, args.output)
    except (ValueError, OSError, KeyError) as error:
        parser.exit(1, f"Offline setup failed: {error}\n")
    print(f"Staged {count} package archives into {feed}")
    print("Set Access__Mode=TrustedLocal, WebDependencies__Enabled=true and")
    print(f"WebDependencies__OfflineSources__0={feed}")
    print("The generated appsettings.WebOffline.json is a reference; the application does not load it automatically.")
