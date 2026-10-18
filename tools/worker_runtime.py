"""Credential-free, network-denied worker. A missing control is an error, never host fallback."""
import argparse
from contextlib import contextmanager
import fcntl
import tempfile
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import threading
import time
import uuid

DOCKER = ['docker', '--host=unix:///var/run/docker.sock']
MAX_OUTPUT = 8 * 1024 * 1024


def command(image, archive, name):
    if not re.fullmatch(r'sha256:[0-9a-f]{64}', image):
        raise ValueError('Use a locally verified immutable Docker image ID')
    archive = str(Path(archive).resolve())
    if ',' in archive or '\n' in archive:
        raise ValueError('Unsafe archive path')
    return DOCKER + ['run', '--name=' + name, '--pull=never', '--network=none',
        '--read-only', '--cap-drop=ALL', '--security-opt=no-new-privileges',
        '--user=10001:10001', '--cpus=1', '--memory=1g', '--memory-swap=1g',
        '--pids-limit=64', '--ulimit=nofile=256:256', '--ipc=none',
        '--tmpfs=/work:rw,nosuid,nodev,size=512m,mode=1777',
        '--tmpfs=/tmp:rw,nosuid,nodev,size=64m,mode=1777',
        '--mount=type=bind,src=' + archive + ',dst=/input/source.tar,readonly',
        '--env=HTTP_PROXY=', '--env=HTTPS_PROXY=', '--env=ALL_PROXY=', '--env=FTP_PROXY=',
        '--env=http_proxy=', '--env=https_proxy=', '--env=all_proxy=', '--env=ftp_proxy=',
        '--env=NO_PROXY=', '--env=no_proxy=', '--env=HOME=/work/home',
        '--env=DOTNET_CLI_HOME=/work/home', '--env=NUGET_PACKAGES=/work/packages',
        '--env=DOTNET_CLI_TELEMETRY_OPTOUT=1', '--env=DOTNET_NOLOGO=1',
        '--env=TURTLE_LIVE=0', '-i', image]


@contextmanager
def worker_slot():
    directory = Path(tempfile.gettempdir()) / ('codeturtle-worker-' + str(os.geteuid()))
    directory.mkdir(mode=0o700, exist_ok=True)
    info = directory.lstat()
    if directory.is_symlink() or info.st_uid != os.geteuid() or info.st_mode & 0o077:
        raise RuntimeError('Unsafe worker admission directory')
    handle = None
    for slot in range(2):
        candidate = os.open(directory / str(slot), os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
        try:
            fcntl.flock(candidate, fcntl.LOCK_EX | fcntl.LOCK_NB)
            handle = candidate
            break
        except BlockingIOError:
            os.close(candidate)
    if handle is None:
        raise RuntimeError('Worker capacity exceeded')
    try:
        yield
    finally:
        fcntl.flock(handle, fcntl.LOCK_UN)
        os.close(handle)


def run(image, archive, request, timeout=120):
    with worker_slot():
        return _run(image, archive, request, timeout)


def _run(image, archive, request, timeout):
    if not 0 < timeout <= 300:
        raise ValueError('Worker lifetime must be within 300 seconds')
    path = Path(archive)
    if not path.is_file() or path.stat().st_size > 64 * 1024 * 1024:
        raise ValueError('Archive must be a regular file at most 64 MiB')
    identity = hashlib.sha256(path.read_bytes()).hexdigest()
    name = 'turtle-' + uuid.uuid4().hex
    proc = subprocess.Popen(command(image, path, name), stdin=subprocess.PIPE,
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    streams = [bytearray(), bytearray()]
    overflow = threading.Event()

    def drain(pipe, data):
        while chunk := pipe.read(8192):
            if len(data) + len(chunk) > MAX_OUTPUT:
                overflow.set()
            else:
                data.extend(chunk)
    readers = [threading.Thread(target=drain, args=(pipe, data), daemon=True)
               for pipe, data in zip([proc.stdout, proc.stderr], streams)]
    for thread in readers:
        thread.start()
    started = time.monotonic()
    try:
        encoded = json.dumps(request).encode()
        if len(encoded) > 1024 * 1024:
            raise ValueError('Worker request exceeds 1 MiB')
        def supply():
            try:
                proc.stdin.write(encoded)
                proc.stdin.close()
            except (BrokenPipeError, OSError, ValueError):
                pass
        writer = threading.Thread(target=supply, daemon=True)
        writer.start()
        while proc.poll() is None:
            if overflow.is_set() or time.monotonic() - started > timeout:
                raise RuntimeError('Worker output or lifetime limit exceeded')
            time.sleep(.02)
        for thread in readers:
            thread.join(timeout=2)
        if overflow.is_set() or proc.returncode != 0:
            # Never expose raw MSBuild, Docker or source-controlled diagnostics.
            raise RuntimeError('Isolated worker failed (exit %s)' % proc.returncode)
        result = json.loads(streams[0])
        if result.get('ArchiveSha256') != identity:
            raise RuntimeError('Worker source identity mismatch')
        return result
    finally:
        subprocess.run(DOCKER + ['rm', '-f', name], stdout=subprocess.DEVNULL,
                       stderr=subprocess.DEVNULL, timeout=15, check=False)
        if proc.poll() is None:
            proc.kill()
        proc.wait(timeout=15)
        for thread in readers:
            thread.join(timeout=2)
        for pipe in [proc.stdin, proc.stdout, proc.stderr]:
            if not pipe.closed: pipe.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--image', required=True)
    parser.add_argument('--archive', required=True)
    parser.add_argument('--request', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    result = run(args.image, args.archive, json.loads(Path(args.request).read_text()))
    Path(args.out).write_text(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
