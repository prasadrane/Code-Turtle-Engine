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
