from io import BytesIO
from pathlib import Path
import sys
import tarfile
import unittest
from unittest.mock import patch
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools'))
import isolated_review

class IsolatedReviewTests(unittest.TestCase):
    def test_model_failure_after_compilation_never_becomes_verdict(self):
        def fail(payload): raise RuntimeError('authored failure')
        with patch.object(isolated_review, 'run', return_value={'ArchiveSha256':'a', 'Payload': {}}) as worker:
            with self.assertRaisesRegex(RuntimeError, 'authored failure'):
                isolated_review.review('image', 'archive', 'p', ['c'], fail)
            self.assertEqual(1, worker.call_count)

    def test_source_must_match_across_analysis_and_audit(self):
        with patch.object(isolated_review, 'run', side_effect=[{'ArchiveSha256':'a','Payload':{}}, {'ArchiveSha256':'b'}]):
            with self.assertRaisesRegex(RuntimeError, 'identity changed'):
                isolated_review.review('image','archive','p',['c'],[])

    def test_review_sends_diff_hunks_to_worker(self):
        with patch.object(isolated_review, 'run', return_value={'ArchiveSha256':'a', 'Payload': {}}) as worker:
            isolated_review.review('image', 'archive', 'p', ['c'], [], diff_hunks=[{'FilePath': 'c', 'StartLine': 1, 'LineCount': 10}])
            self.assertEqual(2, worker.call_count)
            for call in worker.call_args_list:
                req = call[0][2]
                self.assertIn('DiffHunks', req)
                self.assertEqual(req['DiffHunks'], [{'FilePath': 'c', 'StartLine': 1, 'LineCount': 10}])

    def test_extract_hunks_from_patch(self):
        patch_text = "@@ -10,4 +10,6 @@\n unchanged\n-removed\n+added1\n+added2\n unchanged\n+added3\n"
        hunks = isolated_review.extract_hunks_from_patch('File.cs', patch_text)
        self.assertEqual(len(hunks), 2)
        self.assertEqual(hunks[0], {'FilePath': 'File.cs', 'StartLine': 11, 'LineCount': 2})
        self.assertEqual(hunks[1], {'FilePath': 'File.cs', 'StartLine': 14, 'LineCount': 1})

    def test_compute_archive_diff_hunks_from_tar(self):
        buf = BytesIO()
        with tarfile.open(fileobj=buf, mode='w') as tar:
            data = b'line1\nline2\nline3\n'
            info = tarfile.TarInfo('src/Foo.cs')
            info.size = len(data)
            tar.addfile(info, BytesIO(data))
        buf.seek(0)
        hunks = isolated_review.compute_archive_diff_hunks_from_file(buf, ['src/Foo.cs'])
        self.assertEqual(len(hunks), 1)
        self.assertEqual(hunks[0], {'FilePath': 'src/Foo.cs', 'StartLine': 1, 'LineCount': 3})
