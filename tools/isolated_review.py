"""Fixture/saved-output orchestration only. Credentialed model transport belongs outside workers."""
import argparse
import hashlib
import json
from pathlib import Path
from worker_runtime import run


def review(image, archive, project, changed, findings, strict=False, mode='Strip'):
    base = dict(Project=project, ChangedFiles=changed, Findings=[], Guard=mode, Strict=strict)
    analysis = run(image, archive, dict(base, Operation='Analyze'))
    # Tests can inject a failing stub after compilation; no provider transport lives here.
    if callable(findings): findings = findings(analysis['Payload'])
    audit = run(image, archive, dict(base, Operation='Audit', Findings=findings))
    if analysis['ArchiveSha256'] != audit['ArchiveSha256']:
        raise RuntimeError('Source identity changed between analysis and guard')
    return {'evidence_category': 'fixture-or-saved-output', 'source_sha256': analysis['ArchiveSha256'],
            'analysis': analysis, 'audit': audit, 'raw_findings': findings,
            'live_model_calls': 0, 'source_identity_kind': 'archive-hash-not-live-PR'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--image', required=True)
    parser.add_argument('--archive', required=True)
    parser.add_argument('--project', required=True)
    parser.add_argument('--changed', nargs='+', required=True)
    parser.add_argument('--fixture-findings', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    result = review(args.image, args.archive, args.project, args.changed,
                    json.loads(Path(args.fixture_findings).read_text()))
    Path(args.out).write_text(json.dumps(result, indent=2))
