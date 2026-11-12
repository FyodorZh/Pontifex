# Pontifex — Agent Instructions

Before creating, restructuring, or reviewing a test suite, read `Tests/RULES.md`. It is normative.

## Project requirements

Every `.csproj` states all four settings explicitly. No `latest`, no implicit defaults.

- `Nullable`: `enable` everywhere.
- `LangVersion`: `9` for netstandard2.1, `14` for net10.0.
- `ImplicitUsings`: `enable` in tests and `Pontifex.TestUI`, `disable` elsewhere.
- `TreatWarningsAsErrors`: `true` in libraries (netstandard2.1), `false` elsewhere.
