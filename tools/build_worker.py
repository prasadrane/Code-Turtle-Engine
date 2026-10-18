"""Build a credential-free worker image from explicitly selected local runtime files; no pull."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess


def build(dotnet_root, published, context, packages=None):
    context = Path(context).resolve()
    if context.exists():
        raise ValueError('Use a fresh build context')
    root = context / 'rootfs'
    root.mkdir(parents=True)
    sdk = Path(dotnet_root).resolve()
    if not (sdk / "sdk/10.0.100").is_dir(): raise ValueError("SDK 10.0.100 is required")
    for folder in ['sdk', 'shared', 'host', 'packs', 'sdk-manifests', 'templates']:
        if (sdk / folder).exists():
            shutil.copytree(sdk / folder, root / 'opt/dotnet' / folder)
    shutil.copy2(sdk / 'dotnet', root / 'opt/dotnet/dotnet')
    # Only public system library directories, never /etc, HOME, caches or credentials.
    libraries = Path('/usr/lib/x86_64-linux-gnu')
    dest = root / 'usr/lib/x86_64-linux-gnu'
    dest.mkdir(parents=True)
    names = ['libc', 'libm', 'libstdc++', 'libgcc_s', 'libdl', 'libpthread', 'librt',
             'libz', 'libssl', 'libcrypto', 'libgssapi_krb5', 'libkrb5', 'libk5crypto',
             'libcom_err', 'libkrb5support', 'libkeyutils', 'libresolv', 'libatomic',
             'liblttng-ust', 'libnuma', 'libunwind', 'libssh2', 'libcurl', 'libnghttp2',
             'libidn2', 'librtmp', 'libpsl', 'libldap', 'liblber', 'libzstd', 'libbrotlidec', 'libbrotlicommon']
    for name in names:
        for library in libraries.glob(name + '.so*'):
            if library.is_file():
                shutil.copy2(library, dest / library.name, follow_symlinks=True)
    (root / 'lib64').mkdir()
    loader = Path('/lib64/ld-linux-x86-64.so.2')
    shutil.copy2(loader, root / 'lib64/ld-linux-x86-64.so.2')
    (root / 'lib').symlink_to('usr/lib')
    (root / 'bin').mkdir()
    for binary in ['bash', 'sleep']:
        shutil.copy2(shutil.which(binary), root / 'bin' / binary)
    (root / 'bin/sh').symlink_to('bash')
    (root / 'etc').mkdir()
    (root / 'etc/passwd').write_text('worker:x:10001:10001:worker:/work/home:/bin/sh\n')
    (root / 'etc/group').write_text('worker:x:10001:\n')
    (root / 'etc/os-release').write_text('ID=debian\nVERSION_ID=12\n')
    shutil.copytree(published, root / 'app')
    feed = root / 'opt/offline-feed'
    feed.mkdir()
    if packages:
        for path in Path(packages).glob('*.nupkg'):
            shutil.copy2(path, feed / path.name)
    # Installed cloud SDK files can be owner-only. Normalize public image permissions
    # so the unprivileged worker can traverse/read them without changing host files.
    for path in root.rglob('*'):
        if path.is_symlink(): continue
        path.chmod(0o755 if path.is_dir() or path.stat().st_mode & 0o111 else 0o644)
    dockerfile = Path(__file__).with_name('Worker.Dockerfile')
    shutil.copy2(dockerfile, context / 'Dockerfile')
    hashes = {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
              for p in sorted(root.rglob('*')) if p.is_file() and not p.is_symlink()}
    (context / 'runtime-manifest.json').write_text(json.dumps(hashes, indent=2))
    subprocess.run(['docker', '--host=unix:///var/run/docker.sock', 'build', '--network=none',
                    '--iidfile', str(context / 'image-id.txt'), str(context)], check=True)
    return (context / 'image-id.txt').read_text().strip()


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--dotnet-root', required=True)
    parser.add_argument('--published', required=True)
    parser.add_argument('--context', required=True)
    parser.add_argument('--packages')
    args = parser.parse_args()
    print(build(args.dotnet_root, args.published, args.context, args.packages))
