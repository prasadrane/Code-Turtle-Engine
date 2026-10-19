// Regenerate the illustrative browser fixture from real, deterministic local Git commits.
// No model requests, external repositories, or historical benchmark data are involved.
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, join, dirname, basename, sep } from 'node:path';
import { execFileSync } from 'node:child_process';

const directory = mkdtempSync(join(tmpdir(), 'turtle-workspace-fixture-'));
const env = { ...process.env, GIT_AUTHOR_NAME: 'Code Turtle Fixture', GIT_AUTHOR_EMAIL: 'fixture@example.invalid',
  GIT_COMMITTER_NAME: 'Code Turtle Fixture', GIT_COMMITTER_EMAIL: 'fixture@example.invalid',
  GIT_AUTHOR_DATE: '2026-01-01T00:00:00Z', GIT_COMMITTER_DATE: '2026-01-01T00:00:00Z' };
const git = (...args) => execFileSync('git', ['-c', 'core.hooksPath=/dev/null', '-c', 'core.autocrlf=false', '-c', 'commit.gpgSign=false', ...args], { cwd: directory, env, encoding: 'utf8' }).trim();
const base = {
  'src/OrderRepository.cs': `using System.Data.Common;

namespace SampleStore;

public sealed class OrderRepository(DbConnection connection)
{
    public async Task<DbDataReader> FindByCustomerAsync(string customerName)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM orders WHERE customer = @name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = customerName;
        command.Parameters.Add(parameter);
        return await command.ExecuteReaderAsync();
    }
}
`,
  'src/OrderService.cs': `namespace SampleStore;

public sealed record Order(decimal Total);

public sealed class OrderService
{
    public int CountLargeOrders(IEnumerable<Order> orders, decimal threshold)
    {
        return orders.Count(order => order.Total >= threshold);
    }
}
`,
};
const head = { ...base,
  'src/OrderRepository.cs': base['src/OrderRepository.cs'].replace(/        command.CommandText =[\s\S]*?        return await/, `        command.CommandText = "SELECT * FROM orders WHERE customer = '" + customerName + "'";
        return await`),
  'src/OrderService.cs': base['src/OrderService.cs'].replace('return orders.Count(order => order.Total >= threshold);', 'var matches = orders.Where(order => order.Total >= threshold).ToList();\n        return matches.Count;'),
};
try {
  git('init', '--quiet');
  const commit = files => {
    for (const [path, text] of Object.entries(files)) { mkdirSync(dirname(join(directory, path)), { recursive: true }); writeFileSync(join(directory, path), text); }
    git('add', '.'); git('commit', '--quiet', '-m', 'Illustrative source fixture'); return git('rev-parse', 'HEAD');
  };
  const baseSha = commit(base);
  const headSha = commit(head);
  const files = Object.keys(head).map(path => {
    const patch = execFileSync('git', ['diff', '--no-ext-diff', '--no-textconv', '--unified=3', baseSha, headSha, '--', path], { cwd: directory, encoding: 'utf8' });
    const hunks = [];
    let hunk, oldLine = 0, newLine = 0;
    for (const line of patch.split('\n')) {
      const match = /^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@/.exec(line);
      if (match) { oldLine = Number(match[1]); newLine = Number(match[2]); hunk = { header: line, lines: [] }; hunks.push(hunk); }
      else if (hunk && /^[ +\-]/.test(line)) {
        hunk.lines.push({ kind: line[0] === '+' ? 'added' : line[0] === '-' ? 'removed' : 'context',
          oldLine: line[0] === '+' ? null : oldLine++, newLine: line[0] === '-' ? null : newLine++, text: line.slice(1) });
      }
    }
    return { path, oldPath: path, status: 'Modified', analyzed: false, source: head[path], hunks };
  });
  const findings = [{ persona: 'SecurityAuditor', severity: 'Critical', title: 'Parameterize the customer query',
    detail: 'Customer input is concatenated into SQL. Use a command parameter so the database treats the name as a value.',
    location: 'src/OrderRepository.cs:10', citedSymbolFqns: [] },
  { persona: 'AllocationsPerformance', severity: 'Warning', title: 'Avoid materializing a temporary list',
    detail: 'This method only returns a count. Count(predicate) avoids creating a list of every matching order.',
    location: 'src/OrderService.cs:9', citedSymbolFqns: [] }];
  const artifact = { schemaVersion: 1, snapshot: { repository: 'sample-store/orders', pullNumber: 42, title: 'Simplify order lookup and reporting', baseSha, headSha,
    mergeBaseSha: baseSha, comparisonKind: 'merge-base-to-head' }, files, anchors: [
      { findingIndex: 0, path: 'src/OrderRepository.cs', startLine: 10, endLine: 10, provenance: 'FixtureLine' },
      { findingIndex: 1, path: 'src/OrderService.cs', startLine: 9, endLine: 9, provenance: 'FixtureLine' }],
    unavailableReason: null };
  const personas = ['AllocationsPerformance', 'SecurityAuditor', 'IdiomaticArchitect'].map(persona => ({ persona, findings: findings.filter(finding => finding.persona === persona) }));
  const events = [{ delay: 0, event: { type: 'phase', phase: 'validating', message: 'Opening the illustrative sample. No model calls are made.' } },
    { delay: 500, event: { type: 'phase', phase: 'cloning', message: 'Loading source and diffs from a versioned fixture.' } },
    { delay: 1200, event: { type: 'phase', phase: 'deliberating', message: 'Playing authored reviewer notes. This is not a live review.' } },
    ...personas.flatMap((persona, index) => [{ delay: 1500 + index * 1000, event: { type: 'persona_started', persona: persona.persona, character: ['Speedy', 'Sheldon', 'Sensei'][index] } },
      ...persona.findings.map(finding => ({ delay: 2200 + index * 1000, event: { type: 'persona_finding', persona: persona.persona, character: ['Speedy', 'Sheldon', 'Sensei'][index], finding } })),
      { delay: 2700 + index * 1000, event: { type: 'persona_completed', persona: persona.persona, character: ['Speedy', 'Sheldon', 'Sensei'][index], findingCount: persona.findings.length, quip: 'Authored sample perspective.' } }]),
    { delay: 5000, event: { type: 'completed', markdown: '# Code Turtle review\n\n## Illustrative recommendations\n\n' + findings.map(finding => `- **${finding.severity}: ${finding.title}** (${finding.location})\n  ${finding.detail}`).join('\n') + '\n\nCitation evidence is not evaluated in this authored fixture.',
      verdict: { personas, synthesized: findings, guardAudit: [] } } }];
  events.sort((left, right) => left.delay - right.delay);
  const output = resolve('web/src/data/workspace-changes.v1.json');
  writeFileSync(output, JSON.stringify(artifact, null, 2) + '\n');
  writeFileSync(resolve('web/src/data/workspace-replay.v1.json'), JSON.stringify(events, null, 2) + '\n');
  console.log(`Generated fixture ${baseSha.slice(0, 8)} → ${headSha.slice(0, 8)} with ${files.length} changed files. Historical replay untouched.`);
} finally {
  const target = resolve(directory);
  if (!target.startsWith(resolve(tmpdir()) + sep) || !basename(target).startsWith('turtle-workspace-fixture-')) throw new Error('Unexpected fixture directory');
  rmSync(target, { recursive: true, force: true });
}
