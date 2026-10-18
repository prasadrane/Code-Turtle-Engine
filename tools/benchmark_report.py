"""Report an existing provenance ledger; never collect or synthesize responses."""
import argparse
import json
from pathlib import Path
from benchmark_protocol import Ledger, adjudication_template

if __name__ == '__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--ledger',required=True)
    parser.add_argument('--out',required=True)
    parser.add_argument('--adjudication-template')
    args=parser.parse_args()
    Path(args.out).write_text(json.dumps(Ledger.load(args.ledger).report(),indent=2))
    if args.adjudication_template: adjudication_template(args.adjudication_template)
