import copy
from pathlib import Path
import sys
import tempfile
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools'))
import benchmark_protocol as protocol

class ProtocolTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        for name in ['corpus','labels','schema','rubric','harness']:
            (self.root / name).write_text(name)
        self.spec = dict(evidence_category='fixture', model_id='authored-fixture-model',
            model_confirmation='fixture-no-provider', endpoint='https://fixture.invalid/api',
            generation_settings={'temperature':0, 'max_output_tokens':100},
            files={name:[name] for name in ['corpus','labels','schema','rubric','harness']},
            entries=['e'], repetitions=1, reviewer_groups={'council':['a','b','c']},
            budget={'max_samples':6, 'max_usd':1, 'max_usd_per_attempt':0.1})
    def tearDown(self): self.temp.cleanup()
    def test_missing_model_budget_and_changed_files_cannot_freeze_or_validate(self):
        spec=copy.deepcopy(self.spec); spec['model_id']=None
        with self.assertRaises(ValueError): protocol.freeze(spec,self.root,'a'*40)
        spec=copy.deepcopy(self.spec); spec['budget']['max_samples']=5
        with self.assertRaises(ValueError): protocol.freeze(spec,self.root,'a'*40)
        manifest=protocol.freeze(self.spec,self.root,'a'*40)
        (self.root/'rubric').write_text('changed')
        with self.assertRaises(ValueError): protocol.verify(manifest,self.root,'a'*40)
    def test_guard_derivation_missing_slots_and_null_metrics_are_visible(self):
        manifest=protocol.freeze(self.spec,self.root,'a'*40)
        ledger=protocol.Ledger(manifest)
        slot=protocol.slots(manifest)[0]
        ledger.record(slot,'complete',b'prompt',b'answer',actual_usd=None,
                      metrics={'findings':0,'citations':0,'invented':0,'matched':0,'expected':1},
                      guarded={'findings':0,'citations':0,'invented':0,'matched':0,'expected':1} if slot['arm']=='grounded' else None)
        report=ledger.report()
        self.assertFalse(report['complete_repetitions'])
        self.assertTrue(report['unknown_cost'])
        self.assertIsNone(report['pooled'][slot['arm']]['precision'])
        self.assertEqual(5, report['missing_samples'])
    def test_attempt_budget_and_provenance_tampering_fail_closed(self):
        manifest=protocol.freeze(self.spec,self.root,'a'*40)
        ledger=protocol.Ledger(manifest)
        slot=protocol.slots(manifest)[0]
        for _ in range(6): ledger.record(slot,'failure',b'p',None,actual_usd=0)
        with self.assertRaises(ValueError): ledger.record(slot,'failure',b'p',None,actual_usd=0)
        bad=copy.deepcopy(manifest); bad['model_id']='other-model'
        with self.assertRaises(ValueError): protocol.Ledger(bad)
    def test_reservation_precedes_output_and_incomplete_attempt_stays_visible(self):
        manifest=protocol.freeze(self.spec,self.root,'a'*40)
        ledger=protocol.Ledger(manifest)
        slot=protocol.slots(manifest)[0]
        ticket=ledger.reserve(slot,b'prompt')
        self.assertEqual(1,ledger.report()['outcomes']['reserved'])
        ledger.finish(ticket,'cancelled',None,actual_usd=0)
        self.assertEqual(1,ledger.report()['outcomes']['cancelled'])
        with self.assertRaises(ValueError): ledger.reserve(slot,b'changed prompt')
    def test_saved_ledger_preserves_guard_answer_identity_and_cost_overruns(self):
        manifest=protocol.freeze(self.spec,self.root,'a'*40)
        ledger=protocol.Ledger(manifest)
        slot=next(s for s in protocol.slots(manifest) if s['arm']=='grounded')
        ticket=ledger.reserve(slot,b'p')
        ledger.finish(ticket,'complete',b'a',actual_usd=0.2,metrics={'findings':0},guarded={'findings':0})
        self.assertTrue(ledger.report()['cost_ceiling_exceeded'])
        with self.assertRaises(ValueError): ledger.reserve(protocol.slots(manifest)[0],b'p')
        path=self.root/'ledger.json'; ledger.save(path)
        loaded=protocol.Ledger.load(path)
        self.assertEqual(ledger.report(),loaded.report())
    def test_durable_reservation_survives_reload_and_concurrent_budget_is_bounded(self):
        from concurrent.futures import ThreadPoolExecutor
        manifest=protocol.freeze(self.spec,self.root,'a'*40)
        path=self.root/'durable.json'
        ledger=protocol.Ledger(manifest,path)
        slot=protocol.slots(manifest)[0]
        def attempt(_):
            try: return ledger.reserve(slot,b'prompt')
            except ValueError: return None
        with ThreadPoolExecutor(max_workers=8) as executor:
            tickets=list(executor.map(attempt,range(8)))
        self.assertEqual(6,sum(ticket is not None for ticket in tickets))
        resumed=protocol.Ledger.load(path)
        self.assertEqual(6,resumed.report()['outcomes']['reserved'])
        with self.assertRaises(ValueError): protocol.Ledger(manifest,path)
    def test_corpus_directory_content_and_escaping_links_are_frozen(self):
        directory=self.root/'sources'; directory.mkdir(); (directory/'A.cs').write_text('class A {}')
        self.spec['files']['corpus']=['sources']
        manifest=protocol.freeze(self.spec,self.root,'a'*40)
        (directory/'A.cs').write_text('class B {}')
        with self.assertRaises(ValueError): protocol.verify(manifest,self.root,'a'*40)
        with tempfile.TemporaryDirectory() as outside:
            target=Path(outside)/'canary'; target.write_text('harmless')
            (directory/'escape').symlink_to(target)
            with self.assertRaises(ValueError): protocol.freeze(self.spec,self.root,'a'*40)
