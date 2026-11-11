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
production code. `InternalsVisibleTo("Pontifex.Factory.Tests")` is part of the
production project; no test-only production seam exists.

## Contract references

- [grammar.md](../../../Pontifex.Factory/grammar.md) — normative syntax.
- [parsing.md](../../../Pontifex.Factory/parsing.md) — lexing, parser algorithms,
  display rules, diagnostics, and error catalogue.
- [implementation-plan.md](../../../Pontifex.Factory/implementation-plan.md) —
  parsing milestone, test expectations, and reconciliation decisions.
- Production contract and DTOs:
  [IParser.cs](../../../Pontifex.Factory/Parsing/IParser.cs),
  [FormulaToken.cs](../../../Pontifex.Factory/Parsing/FormulaToken.cs),
  [AstDtos.cs](../../../Pontifex.Factory/Parsing/AstDtos.cs),
  [FormulaParseResult.cs](../../../Pontifex.Factory/Parsing/FormulaParseResult.cs),
  [SourceSpan.cs](../../../Pontifex.Factory/SourceSpan.cs),
  [FactoryError.cs](../../../Pontifex.Factory/FactoryError.cs).
- Internal subjects:
  [FormulaLexer.cs](../../../Pontifex.Factory/Parsing/Internal/FormulaLexer.cs),
  [TokenCursor.cs](../../../Pontifex.Factory/Parsing/Internal/TokenCursor.cs),
  [FormulaParser.cs](../../../Pontifex.Factory/Parsing/Internal/FormulaParser.cs),
  [ParsingErrorFactory.cs](../../../Pontifex.Factory/Parsing/Internal/ParsingErrorFactory.cs),
  [TokenDisplay.cs](../../../Pontifex.Factory/Internal/TokenDisplay.cs).

## Layers

This is a Test Suit with exactly one layer at the suite root.

| Layer | Subject | Directory | Inheritance parent | Coverage |
|---|---|---|---|---|
| `Parsing` | Lexer, cursor, parser, parsing errors, and token display group | `Tests/Pontifex.Factory.Tests/Parsing` | — (root) | [`Coverage.md`](Coverage.md) |

## Naming conventions

- Layer name = subsystem name: `Parsing`.
- Test file = `<Component>Tests.cs`; fixture class = `<Component>Tests`.
- Test methods = `MethodName_Scenario_ExpectedResult`. Property, lifecycle, and
  diagnostic-helper tests may omit a separate scenario and use
  `Subject_ExpectedResult` when the subject itself is the scenario, for example
  `EndOfInput_IsTheDocumentedMarker`.
- Namespace = `Pontifex.Factory.Tests.Parsing` for every file in the suite.
- Every fixture carries `[Category("Factory.Parsing")]`.

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

There are no groups. All test files live directly under `Tests/`. If grouping
is added later, groups must be subfolders of `Tests/`; they are organisational
folders, not layers, and they must not contain `Coverage.md` or `Harness/`.

## Harness layout and sharing

The suite has no `Harness/` directory and no harness self-tests. Assertions and
small AST helpers are private static methods inside their fixtures. There is no
mocking library; no test doubles are currently needed because the subjects are
deterministic and have no external dependencies.

If shared test-only doubles become necessary, they belong in the root layer's
`Harness/` and follow the harness-sharing rules in `Tests/RULES.md`. Harness
self-tests, if added, would live in `Harness/Tests/` and carry
`Factory.Parsing.Harness` instead of the suite root category.

## Coverage

Coverage is recorded per layer in the root layer's [Coverage.md](Coverage.md).
The single layer records every test, the contract clause it proves, and known
gaps.

## Categories

- Suite root category: `Factory.Parsing`. Every fixture in this suite carries it.
- Additional filtering categories: none currently.
- Harness category: `Factory.Parsing.Harness` is reserved for future harness
  self-tests and is not used today.

## Running

Run the whole test project:

```
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj
```

Filter to this suite:

```
dotnet test Tests/Pontifex.Factory.Tests/Pontifex.Factory.Tests.csproj --filter "TestCategory=Factory.Parsing"
```

## Adding tests

1. Put the test under `Tests/`, or under a new group folder inside `Tests/` if the
   suite grows cohesive subgroups.
2. Use only the public contract of `IParser` and the internal contract exposed by
   `InternalsVisibleTo`; do not use reflection or inspect private state.
3. Use one naming convention and descriptive scenario/expected-result names.
4. Cover both the positive grammar shape and the exact error code, message, and
   UTF-16 offset.
5. Update [Coverage.md](Coverage.md) in the same change.

## Adding a layer

A Test Suit has exactly one layer. If the subject becomes an inheritance tree or
requires genuinely separate layers, split this suite into multiple Test Suits or
convert it to an Abstract Test Suit instead of adding a nested layer here.

## Out of scope

- Registry, type catalog, parameter metadata, constructors, and registration.
- Semantics and resolution.
- Canonical printing.
- Compilation and the `IDescription` adapter.
- Bootstrap wiring and `ITransportFactory`.
- Transport building and concrete transports.
- Culture-sensitive behaviour, normalization, and case folding of scalar values.
- Adversarial resource limits, performance, allocation, and concurrency. The
  grammar defines no limits; the suite uses a small deterministic corpus rather
  than stress input.

The lexer and parser are tested without a registry, catalog, or transport type.

## Rules

This suite follows [Tests/RULES.md](../../RULES.md).

Known test assumptions that are not part of the normative contract are recorded in
[nonapproved-behaviour.md](../nonapproved-behaviour.md).
