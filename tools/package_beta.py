"""Create/verify a deterministic local beta artifact. No publication, outreach or deployment."""
import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path
import re
import tarfile

ROOT=Path(__file__).resolve().parents[1]


def build(cli, worker, out, commit, packages=None):
    if not re.fullmatch('[0-9a-f]{40}',commit): raise ValueError('Require exact source commit')
    files={}
    for prefix,folder in [('cli',Path(cli).resolve()),('worker',Path(worker).resolve())]:
        for path in sorted(folder.rglob('*')):
            if path.is_symlink(): raise ValueError('Publish symlinks cannot escape into a beta artifact')
            if path.is_file(): files[prefix+'/'+str(path.relative_to(folder))]=path.read_bytes()
    for path in sorted((ROOT/'tools').glob('*')):
        if path.is_file() and path.suffix in ['.py','.json','.Dockerfile']:
            files['tools/'+path.name]=path.read_bytes()
    for path in sorted((ROOT/'docs/beta').glob('*')):
        if path.is_file(): files[path.name]=path.read_bytes()
    files['turtle/rubric_v1.md']=(ROOT/'turtle/rubric_v1.md').read_bytes()
    files['global.json']=(ROOT/'global.json').read_bytes()
    files['LICENSE']=(ROOT/'LICENSE').read_bytes()
    files['turtle-beta']=b'#!/bin/sh\nset -eu\ncd "$(dirname "$0")"\nexport TURTLE_HOME="${TURTLE_HOME:-$PWD/state}"\nexec dotnet ./cli/CodeTurtleEngine.Cli.dll "$@"\n'
    if packages:
        for path in sorted(Path(packages).glob('*.nupkg')): files['offline-feed/'+path.name]=path.read_bytes()
    manifest=dict(format_version=1,code_commit=commit,evidence='unpublished-beta-preparation',
                  files={name:hashlib.sha256(data).hexdigest() for name,data in sorted(files.items())})
    files['manifest.json']=json.dumps(manifest,sort_keys=True,indent=2).encode()
    with open(out,'wb') as target:
        with gzip.GzipFile(filename='',fileobj=target,mode='wb',mtime=0) as compressed:
            with tarfile.open(fileobj=compressed,mode='w') as archive:
                for name,data in sorted(files.items()):
                    entry=tarfile.TarInfo(name)
                    entry.size=len(data); entry.mtime=0
                    entry.mode=0o755 if name=='turtle-beta' else 0o644
                    archive.addfile(entry,io.BytesIO(data))
    return manifest


def verify(path):
    files={}
    with tarfile.open(path,'r:gz') as archive:
        for member in archive:
            if not member.isfile() or member.name.startswith('/') or '..' in Path(member.name).parts or member.name in files:
                raise ValueError('Unsafe or duplicate package member')
            if member.size>256*1024*1024: raise ValueError('Package member exceeds limit')
            files[member.name]=archive.extractfile(member).read()
    manifest=json.loads(files.pop('manifest.json'))
    actual={name:hashlib.sha256(data).hexdigest() for name,data in files.items()}
    if actual!=manifest['files']: raise ValueError('Package file hashes do not match')
    return manifest


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--verify')
    parser.add_argument('--cli')
    parser.add_argument('--worker')
    parser.add_argument('--packages')
    parser.add_argument('--out')
    parser.add_argument('--commit')
    args=parser.parse_args()
    if args.verify: print(json.dumps(verify(args.verify),indent=2))
    else:
        if not all([args.cli,args.worker,args.out,args.commit]): parser.error('Supply cli, worker, out and commit')
        build(args.cli,args.worker,args.out,args.commit,args.packages)
