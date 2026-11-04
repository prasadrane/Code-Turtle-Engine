"""Trusted workflow runner: acquire objects, seal once, analyze/model/audit without PR checkout.

Only public github.com repositories are supported. No PR artifact is accepted and no
review is posted. The worker is the only process that restores or compiles PR code.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import tempfile
import time
import urllib.request

MAX_ARCHIVE = 64 * 1024 * 1024
MAX_METADATA = 2 * 1024 * 1024


def run(image, archive, request):
    # Keep metadata validation importable on Windows; actual workers require Linux.
    from worker_runtime import run as run_worker
    return run_worker(image, archive, request)


def validate_revision(value):
    if not isinstance(value, str) or not re.fullmatch(r'[0-9a-f]{40}', value):
        raise ValueError('Expected a full Git revision')
    return value


def validate_path(value):
    if (not isinstance(value, str) or not re.fullmatch(r'[A-Za-z0-9_./ -]+', value)
            or value.startswith(('/', '-')) or '..' in PurePosixPath(value).parts):
        raise ValueError('Unsupported or unsafe source path')
    return value


def pinned_pr(event, pr):
    """Reject reruns for stale revisions, ambiguous associations and private acquisition."""
    repository = event['repository']['full_name']
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository):
        raise ValueError('Unsupported repository')
    workflow = event['workflow_run']
    references = workflow.get('pull_requests', [])
    if len(references) != 1 or references[0]['number'] != pr['number']:
        raise ValueError('Exactly one matching PR association is required')
    if workflow.get('event') != 'pull_request' or workflow.get('conclusion') != 'success':
        raise ValueError('Expected successful PR notification')
    if (pr['state'] != 'open' or pr['base']['repo']['full_name'] != repository
            or pr['base']['repo']['private'] or event['repository']['private']):
        raise ValueError('Only open public PR acquisition is supported')
    head = validate_revision(pr['head']['sha'])
    if head != validate_revision(workflow['head_sha']):
        raise ValueError('PR changed after notification; wait for the new review run')
    return repository, validate_revision(pr['base']['sha']), head


def read_pr(event, token):
    repo = event['repository']['full_name']
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repo):
        raise ValueError('Unsupported repository')
    references = event['workflow_run'].get('pull_requests', [])
    if len(references) != 1 or type(references[0]['number']) is not int:
        raise ValueError('Exactly one PR association is required')
    url = f'https://api.github.com/repos/{repo}/pulls/{references[0]["number"]}'
    request = urllib.request.Request(url, headers={
        'Authorization': 'Bearer ' + token, 'Accept': 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28', 'User-Agent': 'Code-Turtle-Engine'})
    # This fixed API route never needs a redirect. Do not forward its token elsewhere.
    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, req, fp, code, msg, headers, newurl):
            raise ValueError('Unexpected GitHub API redirect')
    with urllib.request.build_opener(NoRedirect).open(request, timeout=30) as response:
        raw = response.read(MAX_METADATA + 1)
    if len(raw) > MAX_METADATA:
        raise ValueError('PR metadata exceeds limit')
    return json.loads(raw)


def git_command(directory, *arguments):
    # Source objects are data. Ignore credential helpers, global configuration,
    # replacement refs, hooks, templates, alternate object stores and caller GIT_*.
    environment = {'PATH': os.environ['PATH'], 'HOME': str(directory),
                   'GIT_CONFIG_NOSYSTEM': '1', 'GIT_CONFIG_GLOBAL': '/dev/null',
                   'GIT_TERMINAL_PROMPT': '0', 'GIT_NO_REPLACE_OBJECTS': '1',
                   'LANG': 'C.UTF-8'}
    command = ['git', '-c', 'core.hooksPath=/dev/null', '-c', 'core.quotePath=false',
               '-c', 'protocol.allow=never', '-c', 'protocol.https.allow=always',
               '-c', 'http.followRedirects=false', '-C', str(directory), *arguments]
    return command, environment


def git(directory, *arguments, limit=8 * 1024 * 1024):
    command, environment = git_command(directory, *arguments)
    # Files avoid unbounded communicate() buffers; timeout bounds acquisition life.
    with tempfile.TemporaryFile() as output, tempfile.TemporaryFile() as errors:
        process = subprocess.Popen(command, env=environment, stdout=output, stderr=errors)
        try:
            started = time.monotonic()
            while process.poll() is None:
                if (time.monotonic() - started > 120 or output.tell() > limit
                        or errors.tell() > 8 * 1024 * 1024):
                    raise RuntimeError('Pinned source acquisition resource limit exceeded')
                time.sleep(.05)
        except BaseException:
            process.kill()
            process.wait()
            raise
        if process.returncode:
            raise RuntimeError('Pinned source acquisition failed')
        if output.tell() > limit:
            raise ValueError('Pinned source evidence exceeds limit')
        output.seek(0)
        return output.read(limit + 1)


def acquire(directory, repository, base, head):
    from isolated_review import compute_diff_hunks
    git(directory, 'init', '--bare', '--template=')
    remote = f'https://github.com/{repository}.git'
    git(directory, 'fetch', '--no-tags', '--no-recurse-submodules', remote, base, head)
    merge = validate_revision(git(directory, 'merge-base', base, head).decode().strip())
    archive = directory / 'source.tar'
    archive.write_bytes(git(directory, 'archive', '--format=tar', head, limit=MAX_ARCHIVE))
    names = git(directory, 'diff', '--name-only', '-z', '--diff-filter=ACMR', merge, head,
                '--', '*.cs').decode('utf-8').split('\0')
    changed = [validate_path(path) for path in names if path]
    patch = git(directory, 'diff', '--no-ext-diff', '--no-textconv', '--unified=0',
                merge, head, '--', '*.cs').decode('utf-8')
    return archive, changed, compute_diff_hunks(changed, patch), merge


def review(image, cli, archive, project, changed, hunks, out):
    identity = hashlib.sha256(archive.read_bytes()).hexdigest()
    request = dict(Project=validate_path(project), ChangedFiles=changed, Findings=[],
                   Guard='Strip', Strict=True, DiffHunks=hunks)
    analysis = run(image, archive, dict(request, Operation='Analyze'))
    if analysis['ArchiveSha256'] != identity:
        raise RuntimeError('Analysis source identity mismatch')
    analysis_path = out / 'analysis.json'
    findings_path = out / 'findings.json'
    analysis_path.write_text(json.dumps(analysis), encoding='utf-8')
    # No GitHub token or PR-controlled working directory/configuration reaches the CLI.
    environment = {key: value for key, value in os.environ.items()
                   if key not in ('GH_TOKEN', 'GITHUB_TOKEN') and not key.startswith('GIT_')}
    result = subprocess.run(['dotnet', str(Path(cli).resolve()), 'council', '--analysis',
                             str(analysis_path), '--out', str(findings_path)],
                            cwd=Path(cli).resolve().parent, env=environment,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=300)
    if result.returncode or not findings_path.is_file() or findings_path.stat().st_size > 1024 * 1024:
        raise RuntimeError('Trusted model council failed')
    participation_path = findings_path.with_suffix('.personas.json')
    if not participation_path.is_file() or participation_path.stat().st_size > 1024:
        raise RuntimeError('Council participation evidence unavailable')
    participation = json.loads(participation_path.read_text(encoding='utf-8'))
    succeeded, expected = participation.get('Successful'), participation.get('Expected')
    if type(succeeded) is not int or type(expected) is not int or expected != 3 or not 1 <= succeeded <= expected:
        raise RuntimeError('Invalid council participation evidence')
    findings = json.loads(findings_path.read_text(encoding='utf-8'))
    audit = run(image, archive, dict(request, Operation='Audit', Findings=findings))
    if audit['ArchiveSha256'] != identity or hashlib.sha256(archive.read_bytes()).hexdigest() != identity:
        raise RuntimeError('Source identity changed between analysis and audit')
    markdown = audit['Markdown']
    if succeeded < expected:
        markdown = f'> Degraded review: {succeeded}/{expected} council personas succeeded.\n\n' + markdown
    (out / 'review.md').write_text(markdown, encoding='utf-8')
    (out / 'audit.json').write_text(json.dumps(audit, indent=2), encoding='utf-8')
    # Retain findings/audit/coverage, but avoid uploading the complete source payload.
    analysis_path.unlink()
    return identity


def main():
    parser = argparse.ArgumentParser()
    for name in ('event', 'image-file', 'cli', 'project', 'out'):
        parser.add_argument('--' + name, required=True)
    args = parser.parse_args()
    event = json.loads(Path(args.event).read_text(encoding='utf-8'))
    pr = read_pr(event, os.environ.pop('GH_TOKEN'))
    repository, base, head = pinned_pr(event, pr)
    out = Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=False)
    with tempfile.TemporaryDirectory(prefix='turtle-pr-') as temporary:
        archive, changed, hunks, merge = acquire(Path(temporary), repository, base, head)
        evidence = dict(repository=repository, base_sha=base, head_sha=head, merge_base_sha=merge)
        if changed:
            evidence['archive_sha256'] = review(Path(args.image_file).read_text().strip(),
                args.cli, archive, args.project, changed, hunks, out)
            evidence['status'] = 'isolated-review-completed'
        else:
            evidence['status'] = 'no-changed-csharp-files'
            (out / 'review.md').write_text('No changed C# source files; model review was not run.\n')
        (out / 'provenance.json').write_text(json.dumps(evidence, indent=2))


if __name__ == '__main__':
    try:
        main()
    except Exception:
        # Metadata/source/tool/provider diagnostics are never safe workflow annotations.
        raise SystemExit('Pinned isolated review failed; no completed report was published.')
