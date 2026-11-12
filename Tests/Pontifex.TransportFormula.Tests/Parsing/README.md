# Parsing Test Suite

Contract tests for the Transport Formula lexical and syntax layer. The suite proves
that the lexer and parser implement `grammar.md` and the parsing portions of
`parsing.md` exactly: tokenization, quoted-string decoding, contextual `as`,
AST shapes and spans, and every lexical and syntax error code, message, and
offset.

## Suite type

**Test Suit.** The subject is a small, tightly coupled group of non-abstract
types that form one subsystem:

- `FormulaLexer`
- `TokenCursor` (exercised indirectly through `FormulaParser`)
- `FormulaParser` (implements `IParser`)
- `ParsingErrorFactory`
- `TokenDisplay` (shared internal formatting helper owned by the parsing tests)

Root subject: the Transport Formula parsing subsystem group above.

Internal implementations are tested through the same assembly boundary used by
production code. `InternalsVisibleTo("Pontifex.TransportFormula.Tests")` is part of the
production project; no test-only production seam exists.

## Contract references

Paths below reflect the split layout: formula sources live under
`Pontifex.TransportFormula`, runtime sources under `Pontifex.Factory`.

- [grammar.md](../../../Pontifex.TransportFormula/grammar.md) — normative syntax.
- [parsing.md](../../../Pontifex.TransportFormula/parsing.md) — lexing, parser algorithms,
  display rules, diagnostics, and error catalogue.
- Production contract and DTOs:
  [IParser.cs](../../../Pontifex.TransportFormula/Parsing/IParser.cs),
  [FormulaToken.cs](../../../Pontifex.TransportFormula/Parsing/FormulaToken.cs),
  [AstDtos.cs](../../../Pontifex.TransportFormula/Parsing/AstDtos.cs),
  [FormulaParseResult.cs](../../../Pontifex.TransportFormula/Parsing/FormulaParseResult.cs),
  [SourceSpan.cs](../../../Pontifex.TransportFormula/SourceSpan.cs),
  [FactoryError.cs](../../../Pontifex.TransportFormula/FactoryError.cs).
- Internal subjects:
  [FormulaLexer.cs](../../../Pontifex.TransportFormula/Parsing/Internal/FormulaLexer.cs),
  [TokenCursor.cs](../../../Pontifex.TransportFormula/Parsing/Internal/TokenCursor.cs),
  [FormulaParser.cs](../../../Pontifex.TransportFormula/Parsing/Internal/FormulaParser.cs),
  [ParsingErrorFactory.cs](../../../Pontifex.TransportFormula/Parsing/Internal/ParsingErrorFactory.cs),
  [TokenDisplay.cs](../../../Pontifex.TransportFormula/Internal/TokenDisplay.cs).

## Layers

| Layer | Subject | Directory | Inheritance parent | Coverage |
|---|---|---|---|---|
| `Parsing` | Lexer, cursor, parser, parsing errors, and token display group | `Tests/Pontifex.TransportFormula.Tests/Parsing` | — (root) | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Layer name = subsystem name: `Parsing`.
- Test file = `<Component>Tests.cs`; fixture class = `<Component>Tests`.
- Test methods = `MethodName_Scenario_ExpectedResult`. Property, lifecycle, and
  diagnostic-helper tests may omit a separate scenario and use
  `Subject_ExpectedResult` when the subject itself is the scenario, for example
  `EndOfInput_IsTheDocumentedMarker`.
- Namespace = `Pontifex.TransportFormula.Tests.Parsing` for every file in the suite.
- Every fixture carries `[Category("TransportFormula.Parsing")]`.

## Directory layout

```
Parsing/
├── README.md
├── Coverage.md
└── Tests/
    ├── LexerTests.cs
    ├── ParserTests.cs
    └── TokenDisplayTests.cs
```

`Tests/` belongs to the single `Parsing` layer. It currently contains no groups.

## Groups

There are no groups. All test files live directly under `Tests/`.

## Harness layout and sharing

The suite has no `Harness/` directory and no harness self-tests. Assertions and
small AST helpers are private static methods inside their fixtures. No test
doubles are currently needed because the subjects are deterministic and have no
external dependencies.

## Coverage

Coverage is recorded per layer in the root layer's [Coverage.md](Coverage.md).
The single layer records every test, the contract clause it proves, and known
gaps.

## Categories

- Suite root category: `TransportFormula.Parsing`. Every fixture in this suite carries it.
- Additional filtering categories: none currently.
- Harness category: `TransportFormula.Parsing.Harness` is reserved for future harness
  self-tests and is not used today.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.TransportFormula.Tests/Pontifex.TransportFormula.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.TransportFormula.Tests/Pontifex.TransportFormula.Tests.csproj --filter "TestCategory=TransportFormula.Parsing"
```

## Adding tests

1. Put the test under `Tests/`, or under a new group folder inside `Tests/` if the
   suite grows cohesive subgroups.
2. Use the public contract of `IParser` and the internal contract exposed by
   `InternalsVisibleTo`.
3. Use the suite naming convention described above.
4. Cover both the positive grammar shape and the exact error code, message, and
   UTF-16 offset.
5. Record the test in [Coverage.md](Coverage.md).

## Adding a layer

This suite has one layer; adding a layer is not applicable. If the subject
changes shape, see [Tests/RULES.md](../../RULES.md).

## Out of scope

- Registry, type catalog, parameter metadata, constructors, and registration.
- Semantics and resolution.
- Canonical printing.
- Compilation and the direct transport compiler; those belong to the
  `Factory.Compilation` suite.
- Bootstrap wiring and `ITransportFactory`.
- Transport building and concrete transports.
- Culture-sensitive behaviour, normalization, and case folding of scalar values.
- Adversarial resource limits, performance, allocation, and concurrency. The
  grammar defines no limits; the suite uses a small deterministic corpus rather
  than stress input.

The lexer and parser are tested without a registry, catalog, or transport type.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).
