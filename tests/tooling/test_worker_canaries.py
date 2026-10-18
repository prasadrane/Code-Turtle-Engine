import io
import json
import os
from pathlib import Path
import sys
import tarfile
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools'))
from worker_runtime import run

IMAGE = os.environ.get('TURTLE_WORKER_IMAGE')

@unittest.skipUnless(IMAGE, 'Runtime isolation requires an explicitly prepared local worker image')
class WorkerCanaries(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.archive = Path(self.directory.name) / 'input.tar'
    def tearDown(self):
        self.directory.cleanup()
    def archive_files(self, files):
        with tarfile.open(self.archive, 'w') as archive:
            for name, content in files.items():
                raw = content.encode()
                entry = tarfile.TarInfo(name)
                entry.size = len(raw)
                archive.addfile(entry, io.BytesIO(raw))
        self.archive.chmod(0o644)
    def request(self, operation='Analyze'):
        return dict(Operation=operation, Project='Fixture.csproj', ChangedFiles=['C.cs'], Findings=[])
    def test_runtime_mount_credential_network_and_cgroup_controls(self):
        self.archive_files({})
        os.environ['TURTLE_HOST_CANARY'] = 'harmless-host-only-value'
        try:
            result = run(IMAGE, self.archive, self.request('Probe'))
        finally:
            del os.environ['TURTLE_HOST_CANARY']
        for name in ['WriteBlocked', 'NetworkBlocked', 'CanaryAbsent', 'CredentialsAbsent', 'NoNewPrivileges', 'SeccompEnforced', 'CapabilitiesDropped']:
            self.assertTrue(result[name], name)
        self.assertEqual(1073741824, int(result['MemoryMax']))
        self.assertEqual(64, int(result['PidsMax']))
        quota, period = map(int, result['CpuMax'].split())
        self.assertLessEqual(quota / period, 1)
    def test_build_target_cannot_read_host_canary_or_write_root(self):
        self.archive_files({'Fixture.csproj': '''<Project Sdk="Microsoft.NET.Sdk">
        <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
        <Target Name="HarmlessCanary" BeforeTargets="Restore">
        <Error Condition="'$(TURTLE_HOST_CANARY)' != ''" Text="Host environment leaked" />
        <Error Condition="Exists('/workspace/.cloud-setup/activate.sh')" Text="Host filesystem leaked" />
        <WriteLinesToFile File="/outside-workspace-canary" Lines="harmless" ContinueOnError="WarnAndContinue" />
        <Error Condition="Exists('/outside-workspace-canary')" Text="Root was writable" />
        </Target></Project>''', 'C.cs': 'namespace Fixture; public class C { public void M() {} }'})
        os.environ['TURTLE_HOST_CANARY'] = 'harmless-host-only-value'
        try:
            result = run(IMAGE, self.archive, self.request())
        finally:
            del os.environ['TURTLE_HOST_CANARY']
        self.assertEqual(1, len(result['Payload']['Files']))
    def test_archive_escape_and_missing_dependency_fail_closed(self):
        self.archive_files({'../escape': 'harmless'})
        with self.assertRaises(RuntimeError): run(IMAGE, self.archive, self.request())
        self.archive_files({'Fixture.csproj': '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>',
                            'C.cs': 'class C { Missing.Type field; }'})
        with self.assertRaises(RuntimeError): run(IMAGE, self.archive, self.request())
    def test_lifetime_is_enforced_and_container_is_removed(self):
        self.archive_files({'Fixture.csproj': '''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            <Target Name="BoundedDelay" BeforeTargets="Restore"><Exec Command="sleep 30" /></Target></Project>''', 'C.cs': 'class C {}'})
        with self.assertRaisesRegex(RuntimeError, 'lifetime limit'):
            run(IMAGE, self.archive, self.request(), timeout=2)

    def test_memory_exhaustion_is_bounded_and_observable(self):
        self.archive_files({})
        with self.assertRaisesRegex(RuntimeError, 'worker failed'):
            run(IMAGE, self.archive, self.request('MemoryProbe'), timeout=30)
