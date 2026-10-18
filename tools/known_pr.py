"""Preflight for a future known-PR validation. This preparation tool cannot call providers."""
import argparse
import json
from pathlib import Path
import re

REQUIRED = ['pr_url', 'expected_base_sha', 'expected_head_sha', 'model_id', 'endpoint',
            'generation_settings', 'sample_budget', 'cost_budget_usd', 'isolation_report', 'acquisition_proxy_policy']


def preflight(config):
    pending = [name for name in REQUIRED if config.get(name) is None]
    for field in ['expected_base_sha', 'expected_head_sha']:
        if config.get(field) is not None and not re.fullmatch('[0-9a-f]{40}', config[field]):
            pending.append(field + ': invalid revision')
    if config.get('execute_live'):
        raise ValueError('Live execution is not implemented/authorized by this preparation runner')
    return {'evidence_category': 'preflight-only', 'pending_gates': pending,
            'live_paths_exercised': [], 'ready_for_live_review': False}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--config', default=str(Path(__file__).with_name('known_pr.json')))
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    result = preflight(json.loads(Path(args.config).read_text()))
    Path(args.out).write_text(json.dumps(result, indent=2))
