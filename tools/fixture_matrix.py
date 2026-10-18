"""Exercise isolated compilation/guard paths with authored fixtures, never empirical answers."""
import argparse
import io
import json
from pathlib import Path
import tarfile
import time
from isolated_review import review
from worker_runtime import run

PROJECT = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>{}</Project>'


def archive(path, files):
    with tarfile.open(path, 'w') as output:
        for name, content in sorted(files.items()):
            data = content.encode()
            entry = tarfile.TarInfo(name)
            entry.size = len(data)
            entry.mode = 0o644
            output.addfile(entry, io.BytesIO(data))
    path.chmod(0o644)


def matrix(image, directory):
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    cases = {
        'normal': ('Fixture.csproj', ['C.cs'], {'Fixture.csproj': PROJECT.format(''),
                   'C.cs': 'namespace Fixture; public class C { public void M() {} }'}),
        'nuget-offline-fixture': ('Fixture.csproj', ['C.cs'], {'Fixture.csproj': PROJECT.format(
            '<ItemGroup><PackageReference Include="Turtle.OfflineFixture" Version="1.0.0" /></ItemGroup>'),
            'C.cs': 'namespace Fixture; public class C { public int M() => FixtureDependency.Helper.Real(); }'}),
        'multi-project': ('B/B.csproj', ['A/A.cs', 'B/B.cs'], {
            'A/A.csproj': PROJECT.format(''), 'A/A.cs': 'namespace Fixture; public class A { public int M() => 1; }',
            'B/B.csproj': PROJECT.format('<ItemGroup><ProjectReference Include="../A/A.csproj" /></ItemGroup>'),
            'B/B.cs': 'namespace Fixture; public class B { public int M() => new A().M(); }'}),
    }
    rows = []
    for name, (project, changed, files) in cases.items():
        source = directory / (name + '.tar')
        archive(source, files)
        findings = [dict(Persona='SecurityAuditor', Severity='Warning', Title='Authored stub mixed citation',
                        Detail='Fixture only; not a model recommendation', Location=changed[0] + ':1',
                        CitedSymbolFqns=['System.String.Trim', 'Fixture.Fabricated.Member'])]
        started = time.monotonic()
        result = review(image, source, project, changed, findings)
        kept = result['audit']['Outcome']['KeptFindings']
        if len(kept) != 1 or kept[0]['CitedSymbolFqns'] != ['System.String.Trim']:
            raise RuntimeError('Fixture guard contract regressed')
        (directory / (name + '.json')).write_text(json.dumps(result, indent=2))
        rows.append(dict(case=name, outcome='passed', archive_sha256=result['source_sha256'],
                         elapsed_seconds=time.monotonic() - started, model='authored-stub-no-provider'))
    # Model failure is injected after compilation; no provider calls or fabricated recovery.
    def failing_stub(payload):
        raise RuntimeError('Injected fixture model failure')
    try:
        review(image, directory / 'normal.tar', 'Fixture.csproj', ['C.cs'], failing_stub)
        raise AssertionError('A failing transport stub returned a verdict')
    except RuntimeError as error:
        if str(error) != 'Injected fixture model failure': raise
    rows.append(dict(case='model-failure', outcome='injected-stub-failure-no-verdict', evidence='stub-only'))
    report = dict(evidence_category='isolated-fixture-matrix', image=image, cases=rows,
                  live_paths_exercised=[], live_model_calls=0,
                  http_fixture_evidence='WebReviewPipelineTests + WebOracleWiringTests + HostedControlsTests',
                  cancellation_evidence='WorkerCanaries lifetime test and HostedControlsTests cancellation')
    (directory / 'report.json').write_text(json.dumps(report, indent=2))
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--image', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    print(json.dumps(matrix(args.image, args.out), indent=2))
