# Offline example — authored stub, not model performance

This output was produced by the isolated fixture matrix. The authored mixed finding
cited System.String.Trim and a fabricated member. The guard retained only the real
name. Its existence does not establish that this suggestion applies to the fixture.

```markdown
# Code Turtle Review - sealed-archive

Diff baseline: `7d60ce8780b387685241c220d5aa7e1dd3d8ce340199fb771558a50d7c6412bb` - Files reviewed: 1

_Citation checks establish existence only; recommendations, relevance and locations are not verified._

## [Warning] Authored stub mixed citation
- **Persona:** SecurityAuditor
- **Location:** `C.cs:1`
- Fixture only; not a model recommendation
- **Symbols:** `System.String.Trim`

```
