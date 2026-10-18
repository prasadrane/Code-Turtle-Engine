from pathlib import Path
import sys
import tempfile
import unittest
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/'tools'))
import package_beta

class BetaPackageTests(unittest.TestCase):
    def test_reproducible_package_verifies_binaries_and_documents(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary); cli=root/'cli'; worker=root/'worker'
            cli.mkdir(); worker.mkdir()
            (cli/'app.dll').write_bytes(b'fixture-binary')
            (worker/'worker.dll').write_bytes(b'fixture-worker')
            first=root/'first.tgz'; second=root/'second.tgz'
            package_beta.build(cli,worker,first,'a'*40)
            (cli/'app.dll').touch()
            package_beta.build(cli,worker,second,'a'*40)
            self.assertEqual(first.read_bytes(),second.read_bytes())
            manifest=package_beta.verify(first)
            self.assertEqual('a'*40,manifest['code_commit'])
            self.assertIn('BETA.md',manifest['files'])
            self.assertIn('cli/app.dll',manifest['files'])
    def test_publish_symlink_cannot_bundle_host_files(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary); cli=root/'cli'; cli.mkdir()
            (root/'canary').write_text('harmless-outside-publish')
            (cli/'escape').symlink_to(root/'canary')
            with self.assertRaises(ValueError): package_beta.build(cli,cli,root/'out.tgz','a'*40)
