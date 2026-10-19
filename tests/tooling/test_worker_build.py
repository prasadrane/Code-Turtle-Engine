import importlib.util
import json
from pathlib import Path
import re
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SDK_VERSION = json.loads((ROOT / 'global.json').read_text())['sdk']['version']
spec = importlib.util.spec_from_file_location('build_worker', ROOT / 'tools/build_worker.py')
worker_builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker_builder)


class WorkerBuildContractTests(unittest.TestCase):
    def test_ci_installs_exact_worker_sdk(self):
        # Roll-forward can select a newer feature band without installing the image's SDK.
        workflow = (ROOT / '.github/workflows/ci.yml').read_text()
        job = re.search(r'^  worker-isolation:\n(.*?)(?=^  \S|\Z)',
                        workflow, re.MULTILINE | re.DOTALL)
        self.assertIsNotNone(job, 'Worker isolation must remain a CI job')
        setup = re.search(r'^      - uses: actions/setup-dotnet@[^\n]+\n(.*?)(?=^      -|\Z)',
                          job.group(1), re.MULTILINE | re.DOTALL)
        self.assertIsNotNone(setup, 'Worker isolation must install its SDK')
        self.assertRegex(setup.group(1),
                         rf'(?m)^          dotnet-version: [\'\"]?{re.escape(SDK_VERSION)}[\'\"]?\s*$')

    def test_newer_sdk_alone_cannot_satisfy_worker_image_contract(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            family, feature = SDK_VERSION.rsplit('.', 1)
            newer_sdk = f'{family}.{int(feature) + 100}'
            (root / 'dotnet/sdk' / newer_sdk).mkdir(parents=True)
            with self.assertRaisesRegex(ValueError, rf'SDK {re.escape(SDK_VERSION)} is required'):
                worker_builder.build(root / 'dotnet', root / 'published', root / 'context')
