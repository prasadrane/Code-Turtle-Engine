from pathlib import Path
import sys
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

    def test_compute_diff_hunks_attributes_each_hunk_to_its_own_file(self):
        patch_text = (
            "diff --git a/src/A.cs b/src/A.cs\n--- a/src/A.cs\n+++ b/src/A.cs\n"
            "@@ -1,2 +1,3 @@\n keep\n+a-added\n keep\n"
            "diff --git a/src/B.cs b/src/B.cs\n--- a/src/B.cs\n+++ b/src/B.cs\n"
            "@@ -5,1 +5,2 @@\n keep\n+++ b-added-looks-like-header\n")
        hunks = isolated_review.compute_diff_hunks(['src/A.cs', 'src/B.cs', 'src/C.cs'], patch_text)
        self.assertEqual(hunks, [
            {'FilePath': 'src/A.cs', 'StartLine': 2, 'LineCount': 1},
            {'FilePath': 'src/B.cs', 'StartLine': 6, 'LineCount': 1}])

    def test_without_patch_reports_no_evidence_instead_of_whole_file_hunks(self):
        self.assertIsNone(isolated_review.compute_diff_hunks(['src/A.cs'], None))
        with patch.object(isolated_review, 'run', return_value={'ArchiveSha256':'a', 'Payload': {}}) as worker:
            isolated_review.review('image', 'archive', 'p', ['src/A.cs'], [])
            for call in worker.call_args_list:
                self.assertIsNone(call[0][2]['DiffHunks'])
