"""Local orchestration tests; no Docker, GitHub or providers are contacted."""
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import types
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
spec = importlib.util.spec_from_file_location('github_isolated_review', ROOT / 'tools/github_isolated_review.py')
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


def inputs():
    repo = dict(full_name='owner/repository', private=False)
    event = dict(repository=repo, workflow_run=dict(event='pull_request', conclusion='success',
        head_sha='a' * 40, pull_requests=[dict(number=7)]))
    pr = dict(number=7, state='open', base=dict(repo=repo, sha='b' * 40), head=dict(sha='a' * 40))
    return event, pr


class PinnedReviewTests(unittest.TestCase):
    def test_valid_public_pr_returns_pinned_identity(self):
        self.assertEqual(('owner/repository', 'b'*40, 'a'*40), runner.pinned_pr(*inputs()))

    def test_stale_private_and_ambiguous_notifications_fail(self):
        for mutation in ('stale', 'private', 'ambiguous'):
            event, pr = inputs()
            if mutation == 'stale': pr['head']['sha'] = 'c'*40
            if mutation == 'private': pr['base']['repo']['private'] = True
            if mutation == 'ambiguous': event['workflow_run']['pull_requests'].append(dict(number=8))
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                runner.pinned_pr(event, pr)

    def test_source_paths_reject_escapes_and_unsupported_git_quoting(self):
        for value in ('../outside.cs', '/absolute.cs', 'a/../b.cs', '-option', 'a\n.cs', 'a".cs'):
            with self.subTest(value=value), self.assertRaises(ValueError): runner.validate_path(value)

    def test_model_and_audit_receive_same_archive_and_github_token_is_excluded(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            archive = root / 'source.tar'
            archive.write_bytes(b'sealed source')
            identity = runner.hashlib.sha256(archive.read_bytes()).hexdigest()
            calls = []
            def worker(image, source, request):
                calls.append((source, request))
                return dict(ArchiveSha256=identity, Payload={}) if request['Operation'] == 'Analyze' else dict(ArchiveSha256=identity, Markdown='verified')
            def model(command, **kwargs):
                self.assertNotIn('GH_TOKEN', kwargs['env'])
                self.assertNotIn('GITHUB_TOKEN', kwargs['env'])
                self.assertEqual('council', command[2])
                (root / 'findings.json').write_text('[]')
                (root / 'findings.personas.json').write_text('{"Successful": 2, "Expected": 3}')
                return types.SimpleNamespace(returncode=0)
            with patch.object(runner, 'run', worker), patch.object(runner.subprocess, 'run', model), patch.dict(runner.os.environ, {'GH_TOKEN':'test', 'GITHUB_TOKEN':'test'}):
                result = runner.review('sha256:'+'d'*64, root/'cli.dll', archive, 'App.csproj', ['A.cs'], [], root)
            self.assertEqual(identity, result)
            self.assertEqual([archive, archive], [call[0] for call in calls])
            self.assertTrue(calls[1][1]['Strict'])
            self.assertEqual('> Degraded review: 2/3 council personas succeeded.\n\nverified', (root/'review.md').read_text())
            self.assertFalse((root/'analysis.json').exists())

    def test_audit_identity_mismatch_never_writes_completed_report(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            archive = root/'source.tar'
            archive.write_bytes(b'sealed')
            identity = runner.hashlib.sha256(archive.read_bytes()).hexdigest()
            (root/'findings.json').write_text('[]')
            (root/'findings.personas.json').write_text('{"Successful": 3, "Expected": 3}')
            with patch.object(runner, 'run', side_effect=[dict(ArchiveSha256=identity, Payload={}), dict(ArchiveSha256='wrong', Markdown='unsafe')]), patch.object(runner.subprocess, 'run', return_value=types.SimpleNamespace(returncode=0)):
                with self.assertRaisesRegex(RuntimeError, 'identity changed'):
                    runner.review('image', root/'cli.dll', archive, 'App.csproj', ['A.cs'], [], root)
            self.assertFalse((root/'review.md').exists())

    def test_missing_participation_never_produces_completed_report(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            archive = root / 'source.tar'
            archive.write_bytes(b'sealed')
            identity = runner.hashlib.sha256(archive.read_bytes()).hexdigest()
            (root / 'findings.json').write_text('[]')
            with patch.object(runner, 'run', return_value=dict(ArchiveSha256=identity, Payload={})) as worker, patch.object(runner.subprocess, 'run', return_value=types.SimpleNamespace(returncode=0)):
                with self.assertRaisesRegex(RuntimeError, 'participation evidence unavailable'):
                    runner.review('image', root / 'cli.dll', archive, 'App.csproj', ['A.cs'], [], root)
            self.assertEqual(1, worker.call_count)
            self.assertFalse((root / 'review.md').exists())


if __name__ == '__main__': unittest.main()
