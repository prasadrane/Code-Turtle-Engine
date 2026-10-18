import importlib.util
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('worker_runtime', ROOT / 'tools/worker_runtime.py')
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)

class WorkerPolicyTests(unittest.TestCase):
    def test_policy_is_closed_and_mounts_only_archive(self):
        args = worker.command('sha256:' + 'a'*64, '/tmp/input.tar', 'turtle-test')
        self.assertIn('--network=none', args)
        self.assertIn('--read-only', args)
        self.assertIn('--cap-drop=ALL', args)
        self.assertIn('--security-opt=no-new-privileges', args)
        self.assertIn('--memory=1g', args)
        self.assertIn('--pids-limit=64', args)
        self.assertIn('--user=10001:10001', args)
        self.assertEqual(1, sum(a.startswith('--mount=') for a in args))
        self.assertNotIn('--privileged', args)
        self.assertNotIn('/var/run/docker.sock', ' '.join(a for a in args if a.startswith('--mount')))
    def test_unpinned_image_and_unsafe_path_fail(self):
        for image, path in [('latest', '/tmp/a.tar'), ('sha256:'+'a'*64, '/tmp/a,b.tar')]:
            with self.assertRaises(ValueError): worker.command(image, path, 'turtle-test')

    def test_two_worker_admission_limit_and_release(self):
        with worker.worker_slot():
            with worker.worker_slot():
                with self.assertRaisesRegex(RuntimeError, 'capacity'):
                    with worker.worker_slot(): pass
        with worker.worker_slot(): pass
