"""Frozen provenance, attempt accounting and reporting. No provider or response collection code."""
import argparse
import copy
import csv
import hashlib
import itertools
import json
import subprocess
import math
import os
import threading
import uuid
from pathlib import Path
import re
from urllib.parse import urlsplit


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), allow_nan=False).encode()


def sha(value):
    return hashlib.sha256(value).hexdigest()


def slots(manifest):
    return [dict(entry=entry, repetition=rep, group=group, persona=persona, arm=arm)
            for entry in manifest['entries'] for rep in range(1, manifest['repetitions']+1)
            for group, personas in manifest['reviewer_groups'].items() for persona in personas
            for arm in ['source', 'grounded']]


def file_hashes(spec, root):
    result = {}
    for category in ['corpus', 'labels', 'schema', 'rubric', 'harness']:
        paths = spec['files'].get(category)
        if not paths: raise ValueError('Missing provenance files: ' + category)
        result[category] = {}
        for name in paths:
            source = (Path(root) / name).resolve()
            if not source.is_relative_to(Path(root).resolve()): raise ValueError('Provenance path escapes run root')
            expanded = sorted(source.rglob('*')) if source.is_dir() else [source]
            for path in expanded:
                if not path.is_file(): continue
                resolved=path.resolve()
                if not resolved.is_relative_to(Path(root).resolve()): raise ValueError('Provenance link escapes run root')
                relative=str(path.relative_to(Path(root).resolve()))
                result[category][relative] = sha(path.read_bytes())
        if not result[category]: raise ValueError('Empty provenance category: ' + category)
    return result


def freeze(spec, root, commit):
    for field in ['model_id','model_confirmation','endpoint','generation_settings','budget']:
        if not spec.get(field): raise ValueError('Missing frozen input: ' + field)
    if not re.fullmatch('[0-9a-f]{40}', commit): raise ValueError('Require exact code commit')
    endpoint = urlsplit(spec['endpoint'])
    if endpoint.scheme != 'https' or endpoint.username or endpoint.password or endpoint.query or endpoint.fragment:
        raise ValueError('Endpoint provenance cannot contain credentials or query strings')
    if any('secret' in key.lower() or 'api_key' in key.lower() for key in spec['generation_settings']):
        raise ValueError('Do not put credentials in generation settings')
    if spec.get('evidence_category') not in ['fixture','empirical']:
        raise ValueError('Declare evidence category')
    if spec['evidence_category']=='empirical':
        actual=subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()
        dirty=subprocess.check_output(['git','-C',str(root),'status','--porcelain'],text=True).strip()
        if actual!=commit or dirty: raise ValueError('Empirical freeze requires exact clean code checkout')
    allowed_settings={'temperature','top_p','seed','max_output_tokens','reasoning_effort','stop'}
    if set(spec['generation_settings'])-allowed_settings: raise ValueError('Unsupported generation settings; never store credentials')
    if not isinstance(spec['generation_settings'].get('max_output_tokens'),int) or spec['generation_settings']['max_output_tokens']<1:
        raise ValueError('Require explicit output-token ceiling')
    manifest = copy.deepcopy(spec)
    manifest.update(protocol_version=1, code_commit=commit, hashes=file_hashes(spec, root))
    if manifest['repetitions'] < 1 or not manifest['entries'] or len(set(manifest['entries'])) != len(manifest['entries']):
        raise ValueError('Require unique entries and positive repetitions')
    if not manifest['reviewer_groups'] or any(not roles or len(roles) != len(set(roles)) for roles in manifest['reviewer_groups'].values()):
        raise ValueError('Require explicit reviewer groups')
    budget = manifest['budget']
    if not isinstance(budget.get('max_samples'),int) or not all(isinstance(budget.get(key),(int,float)) and math.isfinite(budget[key]) for key in ['max_usd','max_usd_per_attempt']):
        raise ValueError('Require finite explicit budget ceilings')
    if budget['max_samples'] < len(slots(manifest)) or budget['max_usd_per_attempt'] <= 0 or budget['max_usd'] <= 0:
        raise ValueError('Sample/cost budget insufficient or missing')
    if len(slots(manifest))*budget['max_usd_per_attempt'] > budget['max_usd'] + 1e-9:
        raise ValueError('Planned worst-case cost exceeds budget')
    manifest['manifest_sha256'] = sha(canonical(manifest))
    return manifest


def checked(manifest):
    copied = copy.deepcopy(manifest)
    expected = copied.pop('manifest_sha256', None)
    if expected != sha(canonical(copied)): raise ValueError('Manifest was changed after freezing')


def verify(manifest, root, commit):
    checked(manifest)
    if manifest['code_commit'] != commit or manifest['hashes'] != file_hashes(manifest, root):
        raise ValueError('Code or provenance files changed after freezing')


def pooled(rows):
    def total(name):
        values=[r.get(name) for r in rows]
        return sum(values) if values and all(v is not None for v in values) else None
    def ratio(numerator, denominator):
        n, d = total(numerator), total(denominator)
        return None if n is None or not d else n/d
    return dict(denominators={key:total(key) for key in ['findings','citations','matched','expected','invented','cited_findings','uncited_findings','rejected','payload_unsupported']},
                precision=ratio('matched','findings'), recall=ratio('matched','expected'),
                invented_rate=ratio('invented','citations'),
                citation_coverage=ratio('cited_findings','findings'))


class Ledger:
    def __init__(self, manifest, path=None):
        checked(manifest)
        self.manifest=copy.deepcopy(manifest)
        self.events=[]
        self.path=Path(path) if path is not None else None
        self._lock=threading.RLock()
        if self.path is not None:
            if self.path.exists(): raise ValueError('Resume an existing ledger with load; never overwrite it')
            self.save(self.path)

    def reserve(self, slot, prompt):
        with self._lock: return self._reserve(slot,prompt)

    def _reserve(self, slot, prompt):
        if self.manifest['evidence_category']=='empirical' and self.path is None:
            raise ValueError('Empirical reservation requires a durable ledger path')
        # Call BEFORE invoking any future collector; pending attempts remain accounted for.
        if slot not in slots(self.manifest): raise ValueError('Unknown sample slot')
        budget=self.manifest['budget']
        if len(self.events) >= budget['max_samples'] or (len(self.events)+1)*budget['max_usd_per_attempt'] > budget['max_usd']+1e-9:
            raise ValueError('Attempt budget exhausted before collection')
        if any(event['cost_ceiling_exceeded'] for event in self.events): raise ValueError('Cost overrun stops further attempts')
        if any(e['slot']==slot and e['status']=='complete' for e in self.events): raise ValueError('Duplicate complete sample')
        if any(e['slot']==slot and e['prompt_sha256']!=sha(prompt) for e in self.events):
            raise ValueError('Retry changed the frozen prompt')
        event=dict(slot=copy.deepcopy(slot), status='reserved', prompt_sha256=sha(prompt),
                   answer_sha256=None, actual_usd=None, cost_ceiling_exceeded=False,
                   metrics=None, guarded_metrics=None, guarded_source_answer_sha256=None,
                   model_id=self.manifest['model_id'], manifest_sha256=self.manifest['manifest_sha256'],
                   harness_hashes=self.manifest['hashes']['harness'], settings_sha256=sha(canonical(self.manifest['generation_settings'])),
                   collection_receipt=None)
        self.events.append(event)
        if self.path is not None: self.save(self.path)
        return len(self.events)-1

    def finish(self, ticket, status, answer, actual_usd=None, metrics=None, guarded=None, collection_receipt=None):
        with self._lock: return self._finish(ticket,status,answer,actual_usd,metrics,guarded,collection_receipt)

    def _finish(self, ticket, status, answer, actual_usd, metrics, guarded, collection_receipt):
        if not isinstance(ticket,int) or not 0<=ticket<len(self.events): raise ValueError('Unknown attempt')
        event=self.events[ticket]
        if event['status']!='reserved' or status not in ['complete','degraded','failure','cancelled','timeout']:
            raise ValueError('Unknown or completed attempt')
        if actual_usd is not None and actual_usd<0: raise ValueError('Invalid actual cost')
        if status in ['complete','degraded'] and answer is None: raise ValueError('Successful output requires answer bytes')
        if guarded is not None and event['slot']['arm']!='grounded': raise ValueError('Guard must derive from the same grounded answer')
        for result in [metrics,guarded]:
            if result is not None:
                if any(value is not None and (not isinstance(value,int) or value<0) for value in result.values()):
                    raise ValueError('Metrics require counts')
                for numerator,denominator in [('invented','citations'),('rejected','citations'),('payload_unsupported','citations'),('matched','findings'),('matched','expected')]:
                    if result.get(numerator) is not None and result.get(denominator) is not None and result[numerator]>result[denominator]:
                        raise ValueError('Inconsistent metric denominators')
        event.update(status=status, answer_sha256=sha(answer) if answer is not None else None,
                     actual_usd=actual_usd, cost_ceiling_exceeded=actual_usd is not None and actual_usd>self.manifest['budget']['max_usd_per_attempt'],
                     metrics=metrics, guarded_metrics=guarded,
                     guarded_source_answer_sha256=sha(answer) if guarded is not None else None,
                     collection_receipt=collection_receipt)
        if self.path is not None: self.save(self.path)
        return event

    def record(self, slot, status, prompt, answer, actual_usd=None, metrics=None, guarded=None):
        # Offline convenience for already saved fixtures; NOT a collection entry point.
        ticket=self.reserve(slot,prompt)
        return self.finish(ticket,status,answer,actual_usd,metrics,guarded)

    def save(self, path):
        path=Path(path)
        path.parent.mkdir(parents=True,exist_ok=True,mode=0o700)
        temporary=path.with_name('.'+path.name+'.'+uuid.uuid4().hex+'.tmp')
        try:
            with open(os.open(temporary,os.O_CREAT|os.O_EXCL|os.O_WRONLY,0o600),'wb') as output:
                output.write(canonical(dict(manifest=self.manifest,events=self.events)))
                output.flush(); os.fsync(output.fileno())
            os.replace(temporary,path)
        finally:
            temporary.unlink(missing_ok=True)

    @classmethod
    def load(cls, path):
        data=json.loads(Path(path).read_text())
        ledger=cls(data['manifest'])
        if len(data['events'])>ledger.manifest['budget']['max_samples']: raise ValueError('Ledger exceeds budget')
        for event in data['events']:
            if event['manifest_sha256']!=ledger.manifest['manifest_sha256'] or event['model_id']!=ledger.manifest['model_id'] or event['harness_hashes']!=ledger.manifest['hashes']['harness'] or event['settings_sha256']!=sha(canonical(ledger.manifest['generation_settings'])):
                raise ValueError('Event provenance differs from frozen manifest')
            if event['slot'] not in slots(ledger.manifest) or event['status'] not in ['reserved','complete','degraded','failure','cancelled','timeout']:
                raise ValueError('Ledger contains an unknown slot or status')
            if event['guarded_metrics'] is not None and (event['slot']['arm']!='grounded' or event['guarded_source_answer_sha256']!=event['answer_sha256']):
                raise ValueError('Guard answer identity mismatch')
        ledger.events=data['events']
        ledger.path=Path(path)
        return ledger

    def report(self):
        with self._lock: return self._report()

    def _report(self):
        complete=[e for e in self.events if e['status']=='complete']
        missing=[s for s in slots(self.manifest) if not any(e['slot']==s for e in complete)]
        repetitions=[rep for rep in range(1,self.manifest['repetitions']+1) if not any(s['repetition']==rep for s in missing)]
        # Repetitions with missing guard derivations cannot support a three-arm comparison.
        comparable=[rep for rep in repetitions if all(e['guarded_metrics'] is not None for e in complete
                     if e['slot']['repetition']==rep and e['slot']['arm']=='grounded') and all(e['metrics'] is not None for e in complete if e['slot']['repetition']==rep)]
        grouped={arm:[e['metrics'] for e in complete if e['slot']['arm']==arm and e['metrics'] is not None] for arm in ['source','grounded']}
        grouped['guarded-derived']=[e['guarded_metrics'] for e in complete if e['guarded_metrics'] is not None]
        return dict(evidence_category=self.manifest['evidence_category'], manifest_sha256=self.manifest['manifest_sha256'],
                    attempts=len(self.events), missing_samples=len(missing), missing_slots=missing,
                    complete_repetitions=repetitions, comparable_repetitions=comparable,
                    sampling_variation_claim_permitted=self.manifest['evidence_category']=='empirical' and len(comparable)>=2 and bool(self.manifest.get('independence_attestation')) and all(e.get('collection_receipt') for e in complete) and len({e.get('collection_receipt') for e in complete})==len(complete) and not any(e['cost_ceiling_exceeded'] for e in self.events),
                    per_repetition={str(rep): {arm:pooled([e['metrics'] for e in complete if e['slot']['repetition']==rep and e['slot']['arm']==arm and e['metrics'] is not None]) for arm in ['source','grounded']} for rep in range(1,self.manifest['repetitions']+1)},
                    guarded_per_repetition={str(rep):pooled([e['guarded_metrics'] for e in complete if e['slot']['repetition']==rep and e['guarded_metrics'] is not None]) for rep in range(1,self.manifest['repetitions']+1)},
                    outcomes={status:sum(e['status']==status for e in self.events) for status in ['reserved','complete','degraded','failure','cancelled','timeout']},
                    unknown_cost=any(e['actual_usd'] is None for e in self.events),
                    reserved_upper_bound_usd=len(self.events)*self.manifest['budget']['max_usd_per_attempt'],
                    known_cost_usd=sum(e['actual_usd'] or 0 for e in self.events),
                    cost_ceiling_exceeded=any(e['cost_ceiling_exceeded'] for e in self.events),
                    pooled={arm:pooled(rows) for arm,rows in grouped.items()},
                    per_reviewer_group={group:{arm:pooled([e['metrics'] for e in complete if e['slot']['group']==group and e['slot']['arm']==arm and e['metrics'] is not None]) for arm in ['source','grounded']} for group in self.manifest['reviewer_groups']},
                    controlled_single_reviewer_design=any(len(roles)==1 for roles in self.manifest['reviewer_groups'].values()) and any(len(roles)>1 for roles in self.manifest['reviewer_groups'].values()),
                    limits=['Hashes do not prove independent collection or model identity', 'Matcher disagreement requires adjudication', 'No generalization or business ROI established'])


def adjudication_template(path):
    with open(path,'w',newline='') as output:
        csv.writer(output).writerow(['sample_id','finding_id','arm','repetition','matcher_label','reviewer','disposition','rationale','missed_issue','sampling_reason'])


if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--spec', required=True)
    parser.add_argument('--root', required=True)
    parser.add_argument('--commit', required=True)
    parser.add_argument('--out', required=True)
    args=parser.parse_args()
    Path(args.out).write_text(json.dumps(freeze(json.loads(Path(args.spec).read_text()), args.root,args.commit),indent=2))
