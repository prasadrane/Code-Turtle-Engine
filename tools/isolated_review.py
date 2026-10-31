"""Fixture/saved-output orchestration only. Credentialed model transport belongs outside workers."""
import argparse
import difflib
import hashlib
import json
from pathlib import Path
import re
import tarfile
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


def compute_archive_diff_hunks_from_file(archive_fileobj, changed_files):
    """Compute diff hunks from a tar fileobj by generating unified diff for changed files."""
    hunks = []
    try:
        with tarfile.open(fileobj=archive_fileobj, mode='r') as tar:
            for rel_path in changed_files:
                norm_rel = rel_path.replace('\\', '/').lstrip('/')
                member = next((m for m in tar.getmembers() if m.name.replace('\\', '/').lstrip('/') == norm_rel), None)
                if member and member.isfile():
                    f = tar.extractfile(member)
                    if f:
                        lines = [line.decode('utf-8', errors='replace') for line in f.readlines()]
                        diff = list(difflib.unified_diff([], lines, fromfile='/dev/null', tofile=norm_rel))
                        diff_text = ''.join(diff)
                        file_hunks = extract_hunks_from_patch(rel_path, diff_text)
                        hunks.extend(file_hunks)
    except Exception:
        pass
    return hunks


def compute_archive_diff_hunks(archive_path, changed_files, patch=None):
    """Extract hunks from patch if supplied, or compute additions for changed files in archive."""
    if patch:
        if isinstance(patch, (str, Path)) and Path(patch).is_file():
            patch_text = Path(patch).read_text(encoding='utf-8', errors='replace')
        else:
            patch_text = str(patch)
        hunks = []
        for file_path in changed_files:
            hunks.extend(extract_hunks_from_patch(file_path, patch_text))
        return hunks

    archive_file = Path(archive_path)
    if not archive_file.is_file():
        return []

    try:
        with open(archive_file, 'rb') as f:
            return compute_archive_diff_hunks_from_file(f, changed_files)
    except Exception:
        return []


def review(image, archive, project, changed, findings, strict=False, mode='Strip', diff_hunks=None, patch=None):
    if diff_hunks is None:
        diff_hunks = compute_archive_diff_hunks(archive, changed, patch=patch)
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
                    patch=args.patch)
    Path(args.out).write_text(json.dumps(result, indent=2))
