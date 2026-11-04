"""Trust boundary regressions for the opt-in PR review workflow."""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]


class ReviewWorkflowGateTests(unittest.TestCase):
    def test_notification_never_checks_out_or_compiles_pr_code(self):
        text = (ROOT / '.github/workflows/review-stage1-extract.yml').read_text()
        self.assertIn('permissions: {}', text)
        for forbidden in ('uses:', 'secrets.', 'dotnet ', 'upload-artifact', 'checkout'):
            self.assertNotIn(forbidden, text)

    def test_trusted_workflow_is_opt_in_read_only_and_ephemeral(self):
        text = (ROOT / '.github/workflows/review-stage2-triage.yml').read_text()
        self.assertIn("vars.CODETURTLE_REVIEW_ENABLED == 'true'", text)
        self.assertIn('runs-on: ubuntu-latest', text)
        self.assertIn('ref: ${{ github.sha }}', text)
        self.assertIn('persist-credentials: false', text)
        self.assertIn('github_isolated_review.py', text)
        self.assertNotIn('download-artifact', text)
        self.assertNotIn('write', text)
        self.assertNotIn('--payload', text)
        self.assertNotIn('dotnet run', text)
        scripts = re.findall(r'(?ms)^\s*run: \|\n(.*?)(?=^\s*- name:|\Z)', text)
        self.assertTrue(scripts)
        self.assertTrue(all('${{' not in script for script in scripts))

    def test_offline_feed_contains_trusted_solution_dependencies(self):
        text = (ROOT / '.github/workflows/review-stage2-triage.yml').read_text()
        # The default target includes test projects outside the CLI/worker publish closure.
        restore = text.index('dotnet restore CodeTurtleEngine.sln')
        collect = text.index('find "$HOME/.nuget/packages"')
        review = text.index('python3 tools/github_isolated_review.py')
        self.assertLess(restore, collect)
        self.assertLess(collect, review)


if __name__ == '__main__':
    unittest.main()
