# Pontifex — Agent Instructions

Before creating, restructuring, or reviewing a test suite, read `Tests/RULES.md`. It is normative.

## Transport specifications

Before changing a transport abstraction or implementation, read its specification. It is normative.

- Abstraction: `Pontifex/Abstractions/Transports/<Model>/[<Family>/]Specification.md`; for RR also read the shared `RR/Specification.md`.
- Implementation: the `Specification.md` beside the carrier, wrapper, or converter, plus the abstraction it implements.
- Before writing or reviewing a specification, read `Pontifex/Abstractions/Transports/TransportSpecificationsSpecification.md`.

## Project requirements

Every `.csproj` states all four settings explicitly. No `latest`, no implicit defaults.

- `Nullable`: `enable` everywhere.
- `LangVersion`: `9` for netstandard2.1, `14` for net10.0.
- `ImplicitUsings`: `enable` in tests and `Pontifex.TestUI`, `disable` elsewhere.
- `TreatWarningsAsErrors`: `true` in libraries (netstandard2.1), `false` elsewhere.
