import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location("prepare_web_offline", Path(__file__).resolve().parents[2] / "tools/prepare_web_offline.py")
setup = importlib.util.module_from_spec(spec)
spec.loader.exec_module(setup)


class OfflineSetupTests(unittest.TestCase):
    def test_stages_only_selected_archives_without_copying_cache_configuration(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            cache = root / "cache"
            package = cache / "example" / "1.0.0"
            package.mkdir(parents=True)
            with zipfile.ZipFile(package / "example.1.0.0.nupkg", "w") as archive:
                archive.writestr("example.nuspec", "<package />")
            (cache / "NuGet.Config").write_text("secret")
            (package / ".nupkg.metadata").write_text("private feed URL")
            assets = root / "assets.json"
            assets.write_text(json.dumps({"libraries": {"Example/1.0.0": {"type": "package"}, "App/1.0.0": {"type": "project"}}}))
            feed, count = setup.prepare(assets, cache, root / "output")
            self.assertEqual(count, 1)
            self.assertEqual([path.name for path in feed.iterdir()], ["example.1.0.0.nupkg"])
            self.assertEqual((feed / "example.1.0.0.nupkg").read_bytes(), (package / "example.1.0.0.nupkg").read_bytes())
            manifest = json.loads((feed.parent / "manifest.json").read_text())
            self.assertEqual(manifest[0]["package"], "Example/1.0.0")
            configuration = json.loads((feed.parent / "appsettings.WebOffline.json").read_text())
            self.assertEqual(configuration["WebDependencies"]["OfflineSources"], [str(feed)])

    def test_incomplete_cache_does_not_create_partial_feed(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            assets = root / "assets.json"
            assets.write_text(json.dumps({"libraries": {"Missing/1.0.0": {"type": "package"}}}))
            output = root / "output"
            with self.assertRaises(FileNotFoundError):
                setup.prepare(assets, root, output)
            self.assertFalse(output.exists())

    def test_rejects_package_path_traversal_without_creating_output(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            assets = root / "assets.json"
            assets.write_text(json.dumps({"libraries": {"../1.0.0": {"type": "package"}}}))
            with self.assertRaises(ValueError):
                setup.prepare(assets, root, root / "output")
            self.assertFalse((root / "output").exists())


if __name__ == "__main__":
    unittest.main()
