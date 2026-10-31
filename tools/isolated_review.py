"""Fixture/saved-output orchestration only. Credentialed model transport belongs outside workers."""
import argparse
import hashlib
import json
from pathlib import Path
import re
from worker_runtime import run

HUNK_HEADER_REGEX = re.compile(r'^@@\s+-(?:\d+)(?:,\d+)?\s+\+(\d+)(?:,(\d+))?\s+@@')


def extract_hunks_from_patch(file_path, patch_text):
    """Extract diff hunks matching GitDiffProvider.ExtractHunksFromPatch."""
    hunks = []
    if not patch_text:
        return hunks

    head_line = 0
    current_run_start = None
    current_run_count = 0
    in_hunk = False

    def flush_run():
        nonlocal current_run_start, current_run_count
        if current_run_start is not None and current_run_count > 0:
            hunks.append({
                'FilePath': file_path,
                'StartLine': current_run_start,
                'LineCount': current_run_count
            })
            current_run_start = None
            current_run_count = 0

    for raw_line in patch_text.split('\n'):
        line = raw_line.rstrip('\r')
        if line.startswith('@@'):
            flush_run()
            match = HUNK_HEADER_REGEX.match(line)
            if match:
                head_line = int(match.group(1))
                in_hunk = True
            else:
                in_hunk = False
            continue

        if not in_hunk or len(line) == 0:
            continue

        if line[0] == '+':
            if current_run_start is None:
                current_run_start = head_line
                current_run_count = 1
            else:
                current_run_count += 1
            head_line += 1
        elif line[0] == '-':
            # Deleted line: head line does not advance
            pass
        elif line[0] == ' ':
            flush_run()
            head_line += 1
        else:
            flush_run()

    flush_run()
    return hunks


def split_patch_by_file(patch_text):
    """Map each head-side path ('+++ b/<path>') in a unified git diff to its own section text."""
    sections, current, lines, in_header = {}, None, [], False
    for raw_line in patch_text.split('\n'):
        line = raw_line.rstrip('\r')
        if line.startswith('diff --git '):
            if current is not None:
                sections[current] = '\n'.join(lines)
            current, lines, in_header = None, [], True
            continue
        if in_header and line.startswith('+++ '):
            target = line[4:].strip()
            current = None if target == '/dev/null' else target[2:] if target.startswith('b/') else target
            continue
        if line.startswith('@@'):
            in_header = False
        lines.append(line)
    if current is not None:
        sections[current] = '\n'.join(lines)
    return sections


def compute_diff_hunks(changed_files, patch_text):
    """Hunks for changed files from a unified git diff, or None when no diff evidence was supplied.

    The archive is a head-only snapshot, so changed lines cannot be derived from it; returning None
    lets the worker report missing evidence instead of treating whole files as changed.
    """
    if not patch_text:
        return None
    normalize = lambda path: path.replace('\\', '/').lstrip('/')
    sections = {normalize(path): text for path, text in split_patch_by_file(patch_text).items()}
    hunks = []
    for file_path in changed_files:
        section = sections.get(normalize(file_path))
        if section is not None:
            hunks.extend(extract_hunks_from_patch(file_path, section))
    return hunks


def review(image, archive, project, changed, findings, strict=False, mode='Strip', diff_hunks=None, patch_text=None):
    if diff_hunks is None:
        diff_hunks = compute_diff_hunks(changed, patch_text)
    base = dict(Project=project, ChangedFiles=changed, Findings=[], Guard=mode, Strict=strict, DiffHunks=diff_hunks)
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
    parser.add_argument('--patch', default=None)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    result = review(args.image, args.archive, args.project, args.changed,
                    json.loads(Path(args.fixture_findings).read_text()),
                    patch_text=Path(args.patch).read_text(encoding='utf-8') if args.patch else None)
    Path(args.out).write_text(json.dumps(result, indent=2))
