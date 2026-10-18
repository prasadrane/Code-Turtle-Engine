import json
from pathlib import Path
import sys
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools'))
from known_pr import preflight

class KnownPrTests(unittest.TestCase):
    def test_missing_details_never_claim_live_evidence(self):
        result = preflight({})
        self.assertFalse(result['ready_for_live_review'])
        self.assertEqual([], result['live_paths_exercised'])
        self.assertIn('model_id', result['pending_gates'])
    def test_live_switch_cannot_spend(self):
        with self.assertRaises(ValueError): preflight({'execute_live': True})
