# Transport Formula Implementation Plan

## 1. Purpose

This document is the implementation plan for the `Pontifex.Factory` subsystem:
the Transport Formula language and its supporting registry, resolver, canonical
printer, and compiler boundary.

Normative specifications:

- `grammar.md` — syntax of Transport Formulas.
- `parsing.md` — parser, resolver, compiler, canonical printer, and error
  behavior.

This plan covers implementation structure, internal components, algorithms,
test strategy, milestones, and acceptance criteria. It does not implement any
subsystem.

---

## 2. Goals and non-goals

### Goals

- Replace URI parsing and ad-hoc per-constructor URI parsers with a single
  Transport Formula language.
- Keep the syntax layer independent of transports and metadata.
- Make metadata an immutable, validated registry.
- Make resolution the only place where syntax and metadata meet.
- Make canonical printing deterministic and alias-free.
- Keep compilation opaque and generic so `IDescription`, a typed builder, or a
  future representation can be used without changing the language.
- Provide a small bootstrap facade that hides component wiring.

### Non-goals

- Building transports.
- Choosing converter paths.
- Implementing concrete transport constructors.
- Replacing the current `TransportBuilder` in the same change.
- UI integration in `Pontifex.TestUI`.
- Removing `IDescription` in this phase.

---

## 3. Approved decisions that must not drift

These decisions are already reflected in `grammar.md` and `parsing.md`.

### Syntax and parsing

- The language is called **Transport Formula**; the source artifact is a
  **formula**.
- No `transport://` prefix.
- No comments.
- No implementation limits: no maximum depth, length, list size, or argument
  count.
- Whitespace is `SP`, `HTAB`, `CR`, `LF` outside quoted strings.
- Raw `CR`/`LF` inside quoted strings is an error.
- Identifiers are ASCII `[A-Za-z][A-Za-z0-9_-]*`, case-insensitive, canonical
  lowercase.
- Bare scalar characters exclude whitespace and `( ) [ ] { } , = "`.
- Quoted strings allow `\"` and `\\`; every other backslash sequence is an error.
- Casts use postfix `inner as TypeName`.
- `as` is contextual, not a reserved word.
- At most one cast per expression.
- Identity casts collapse during resolution.
- Lists are `[expr, ...]`; no nested lists; no scalars; no top-level list.
- Structs are `{ name=value, ... }`; named fields only; no trailing comma.
- Positional arguments must precede named arguments.
- Python-style positional binding: positionals fill positional-capable
  parameters in declaration order; named arguments fill remaining parameters.
- There are no default values in metadata. Constructors know what was omitted.
- No schema-based overload resolution.
- No converter-graph tie-breaking during resolution.

### Registry

- Canonical scheme names are unique.
- Aliases are registration-time policy.
- `SuggestedAliases` are hints and are not registered automatically.
- An alias may map to multiple constructors only if their types differ.
- Canonical preference applies after expected-type filtering.
- Type sets are `Any`, `Exactly`, and `OneOf`.
- Struct schemas contain only string fields and nested struct fields.

### Semantics

- The top-level expected type set is `Any`.
- A cast resolves its inner transport with `Any`; its output type is the cast
  target.
- Casts never select variants. Aliases and canonical names select variants.
- Parser validates that a cast target is a known type; it does not validate that
  a converter path exists.
- Homogeneous lists must contain one resolved transport type.
- Resolution is deterministic and does not backtrack.

### Canonicalization

- Canonical output uses canonical scheme names and canonical type names.
- Longest provided positional prefix is printed positionally; remaining
  provided parameters are named in declaration order.
- Named-only parameters are always named.
- Arguments are separated by `, ` (comma plus one space).
- No prefix, no trailing whitespace, no newline.
- Scalars are minimally quoted.
- Struct fields are printed in schema order.

### Compilation

- `ICompiler<T>` is opaque.
- `IDescription` support belongs to an adapter, not to the language core.
- Build-time conversion failure is `E_BLD_NO_CONVERSION_PATH`, outside the
  parser/printer contract.

### 3.1 Scaffold-to-spec reconciliation

The scaffold interfaces and DTOs were created before the final reread of
`grammar.md` and `parsing.md`. The implementation must reconcile these concrete
scaffold shapes with the normative algorithms. The following adjustments are
required and are part of the implementation, not deviations from the language.

**Names**

- Scaffold `AstArgument` is the concrete name for the document's `AstArg`.
- Scaffold `AstStructField` is the concrete name for the document's `AstField`.
- Scaffold `ICompiler<T>` is the concrete name for the document's
  `IResolvedTreeCompiler<T>`; `Compile(ResolvedFormula)` takes the root wrapper
  that contains the document's `ResolvedExpr` root.
- Scaffold `TransportConstructorInfo` is the normalized metadata view of the
  document's `ITransportConstructor`.
- Scaffold `IRegistry.FindCanonical` + `FindCandidates` provides the document's
  `Canonical(scheme) ∪ Aliases(scheme)` lookup.
- Scaffold `IRegistryBuilder` provides the document's registration entry point.
- `IResolver` and `ICanonicalizer` receive the registry/catalog through
  construction instead of taking them as per-call parameters; this is the
  dependency-injected equivalent of the document's function signatures.

These name mappings must be recorded when `parsing.md` is synchronized. Keep the
scaffold names unless the whole project is renamed in one change.

**Missing AST data**

Add the offsets required by the parser and resolver algorithms:

- `AstTransportCall.CloseParenOffset` — used for missing-required-parameter
  offsets.
- `AstList.OpenOffset` and `AstList.CloseOffset` — used for list min/max offsets
  and diagnostics.
- `AstStruct.OpenOffset` and `AstStruct.CloseOffset` — used for missing required
  field offsets and diagnostics.
- `SourceSpan.End` as `Start + Length` (or an equivalent internal helper) —
  used for span construction and token diagnostics.

`AstFormula` remains the root wrapper around `AstExpr`; that is compatible with
the document's "one expression" model.

**Missing resolved data**

- `ResolvedExpr` must expose `TransportType Type` for every node.
- `ResolvedCast.Type` is `ResolvedCast.Target`.
- `ResolvedTransport.Type` remains `Constructor.Type`.
- Identity cast collapse must retain the full cast span:
  `inner with Span = span(castExpression)`.

Without this, list homogeneity, expected-type checks, and printer invariants
cannot be implemented exactly as specified.

**Catalog and registry gaps**

- `ITransportTypeCatalog` must support canonical-name lookup for a type, not only
  name-to-type lookup. Add `TryGetCanonicalName` and `AllTypes` or equivalent
  members used by the printer and error formatter.
- `IRegistry` must expose `AllSchemeNames` — the sorted union of canonical and
  alias names — for `E_SEM_UNKNOWN_SCHEME`.
- `FindCandidates` must return canonical plus alias candidates; canonical
  preference remains a semantics concern.
- `TransportTypeSet` is currently a `Kind + Types` DTO. Add internal
  `TypeSetOps.IsAny`, `Contains`, and formatting helpers, or extend the DTO with
  equivalent members. Keep registration normalization (empty `OneOf` is invalid,
  duplicates removed).
- `TransportConstructorInfo` must be treated as the normalized metadata snapshot;
  the registry must not expose caller spelling.

**Error and exception shape**

- `FactoryError` replaces the document's `ParseError` name.
- The exception hierarchy is class-based (`FactoryException` base plus
  `RegistrationException`, `PrinterException`, `CompilerException`,
  `BuildException`) instead of record-based.
- `parsing.md` §12.1 must be synchronized during Milestone 0. The error codes,
  message templates, and offset rules do not change.

---

## 4. Solution and project layout

Existing projects:

```text
Pontifex.Factory/
  Pontifex.Factory.csproj
  grammar.md
  parsing.md
  implementation-plan.md
  SourceSpan.cs
  FactoryError.cs
  ITransportFactory.cs
  ITransportFactoryBuilder.cs
  TransportFactoryBootstrap.cs
  IsExternalInit.cs

  Parsing/
    IParser.cs
    FormulaToken.cs
    AstDtos.cs
    FormulaParseResult.cs

  Registry/
    IRegistry.cs
    ITransportTypeCatalog.cs
    MetadataDtos.cs

  Semantics/
    IResolver.cs
    ResolvedDtos.cs

  Canonicalization/
    ICanonicalizer.cs

  Compilation/
    ICompiler.cs

Tests/Pontifex.Factory.Tests/
  Pontifex.Factory.Tests.csproj
```

Planned internal folders:

```text
Pontifex.Factory/
  FactoryExceptions.cs          (FactoryException + phase subclasses)
  Internal/
    NameNormalizer.cs
    FormulaErrorFactory.cs
    TokenDisplay.cs
    TypeSetFormatter.cs

  Parsing/Internal/
    FormulaLexer.cs
    TokenCursor.cs
    FormulaParser.cs
    ParsingErrorFactory.cs

  Registry/Internal/
    TransportTypeCatalog.cs
    TransportRegistry.cs
    TransportRegistryBuilder.cs
    MetadataValidator.cs
    RegistryErrorFactory.cs

  Semantics/Internal/
    FormulaResolver.cs
    ConstructorSelector.cs
    ArgumentBinder.cs
    ParameterValidator.cs
    StructValidator.cs
    SemanticErrorFactory.cs

  Canonicalization/Internal/
    FormulaCanonicalizer.cs
    PositionalFormatter.cs
    ScalarFormatter.cs
    PrinterInvariantValidator.cs
    CanonicalizationErrorFactory.cs

  Bootstrap/
    TransportFactory.cs
    TransportFactoryBuilder.cs
    FactoryComponentFactory.cs
```

Optional, recommended companion project for the legacy adapter:

```text
Pontifex.Factory.Description/
  Pontifex.Factory.Description.csproj
  DescriptionCompiler.cs
  CastDescriptionAdapter.cs
```

Do not add `IDescription`, `ITransport`, `ILogger`, `IMemoryRental`, or concrete
transport references to `Pontifex.Factory` itself. The optional adapter project
may reference both `Pontifex` and `Pontifex.Factory`.

---

## 5. Dependency rules

```text
Parsing        → nothing (except shared internal helpers)
Registry       → TransportType and metadata DTOs
Semantics      → Parsing + Registry
Canonicalization → Semantics + Registry (type catalog/metadata)
Compilation    → Semantics
Bootstrap      → Parsing + Registry + Semantics + Canonicalization + Compilation
```

Rules:

- `Parsing` must not reference `Registry`, `Semantics`, or transport types.
- `Registry` must not reference `Parsing` or `Semantics`.
- `Semantics` may reference both `Parsing` and `Registry`.
- `Canonicalization` must not reference `Parsing`.
- `Compilation` must not reference `Parsing` or `Registry` except through
  `ResolvedFormula`'s embedded metadata.
- Public subsystem namespaces contain interfaces and DTOs only.
- Implementations are `internal` and live under `Internal` namespaces.
- Bootstrap is the only place that wires components together.

---

## 6. Shared infrastructure

Implement shared internals before the subsystems that use them.

### 6.1 `FactoryError` and exceptions

`FactoryError` already exists and is the single diagnostic DTO used by result
objects. Its phase and span identify the failing stage and source position.

Add a small exception hierarchy in the root bootstrap namespace, preserving the
names already specified in `parsing.md` §12.1:

- `FactoryException` — common base with `FactoryError Error { get; }`.
- `RegistrationException : FactoryException`
- `PrinterException : FactoryException`
- `CompilerException : FactoryException`
- `BuildException : FactoryException`

Each derived type carries phase `Registration`, `Printing`, `Compilation`, or
`Building` respectively. The base is useful for generic catch handlers; the
derived types make the failing stage explicit.

Policy:

- Parsing and resolution return `FormulaParseResult` / `FormulaResolveResult`
  with `FactoryError`; they do not throw for user input.
- Registration, canonicalization, and compilation throw the matching derived
  exception for configuration and invariant errors.
- Implementations must not create additional error formats.

Documentation sync: `parsing.md` §12.1 currently names a `ParseError` record and
record-based exception types. The scaffold uses `FactoryError` and a class-based
exception hierarchy; treat `FactoryError` as the concrete diagnostic name and
update that document section during Milestone 0.

### 6.2 `NameNormalizer`

Responsibilities:

- `IsIdentifier(string)` using `[A-Za-z][A-Za-z0-9_-]*`.
- `Normalize(string)` using ASCII lowercasing only.
- Never normalize scalar values or type names beyond catalog lookup.

Used by parser for scheme/parameter/field recognition, registry for registration,
and semantic error formatting.

### 6.3 `TokenDisplay`

Responsibilities:

- Render tokens deterministically for error messages.
- EOF is `<end of input>`.
- Escape `\`, `'`, `"`, CR, LF, HTAB, control code units, and surrogate code
  units using the exact rules in `parsing.md` §3.4.
- Truncate long text to the longest prefix of at most 40 UTF-16 code units that
  ends at an escape-unit boundary; never split `\\`, `\"`, `\r`, `\n`, `\t`,
  `\uXXXX`, or a surrogate escape pair. Append `...`.
- Used only by error factories.

### 6.4 `FormulaErrorFactory`

Responsibilities:

- Construct `FactoryError` with the correct code, message, phase, span.
- Own the exact message templates from `parsing.md`.
- Join expected alternatives and sorted name lists deterministically.
- Provide helpers for all error codes in §13 of `parsing.md`.

### 6.5 `TypeSetFormatter`

Responsibilities:

- Format `TransportTypeSet` for messages:
  - `Any` → `any transport type`
  - `Exactly(T)` → canonical type name
  - `OneOf(...)` → distinct names sorted and joined with ` or `
- Format variant lists and type lists.
- Used by semantics and registration errors.

### 6.6 `IsExternalInit`

Already present. Keep it internal. It is required for records on
`netstandard2.1`.

---

## 7. Parsing subsystem

### 7.1 Public contract

Existing:

```text
Pontifex.Factory.Parsing.IParser
FormulaParseResult Parse(string formula)
```

DTOs:

- `FormulaTokenKind`, `FormulaToken`
- `AstFormula`, `AstExpr`, `AstValue`
- `AstTransportCall`, `AstArgument`, `AstCast`
- `AstScalar`, `AstList`, `AstStruct`, `AstStructField`
- `FormulaParseResult`
- `SourceSpan`

Before implementation, add the AST offsets required by `parsing.md`:
`AstTransportCall.CloseParenOffset`, `AstList.OpenOffset/CloseOffset`,
`AstStruct.OpenOffset/CloseOffset`, and `SourceSpan.End`.

No public classes in `Pontifex.Factory.Parsing`.

### 7.2 Internal components

**`FormulaLexer`**

- Input: `string formula`.
- Output: ordered `IReadOnlyList<FormulaToken>` ending with `EndOfInput`.
- Owns whitespace skipping and quoted-string decoding.
- Does not throw for malformed input; returns a `FactoryError` or uses an
  internal result. Recommended shape:

```text
bool TryLex(string formula, out IReadOnlyList<FormulaToken> tokens,
            out FactoryError? error)
```

- Token offsets are UTF-16 code-unit offsets.
- `FormulaToken.RawText` stores the raw source slice for quoted strings and
  words; `Text` stores decoded text for quoted strings and raw text for words.

**`TokenCursor`**

- Holds the token list and current index.
- Operations: `Current`, `Peek(int)`, `Consume()`, `Match(kind)`,
  `MatchWord("as")`, `Expect(...)`.
- Never performs semantic checks.

**`FormulaParser`**

- Implements `IParser`.
- Calls `FormulaLexer` first; lexical error wins.
- Produces `AstFormula`.
- Owns all syntax rules and offsets.
- Does not know schemes, parameters, types, or validation.

**`ParsingErrorFactory`**

- Produces `E_LEX_*` and `E_SYN_*` errors with exact messages and spans.
- Owns the expected-string contexts from `parsing.md`.

### 7.3 Lexer algorithm

1. Iterate input by UTF-16 code unit.
2. Skip `SP`, `HTAB`, `CR`, `LF`.
3. On `"`, parse quoted string with the exact precedence from `parsing.md`
   §3.3:
   - unpaired surrogate outside backslash handling → `E_LEX_INVALID_UNICODE`;
   - raw CR/LF outside backslash handling → `E_LEX_RAW_NEWLINE_IN_STRING`;
   - closing `"` ends the token;
   - `\` consumes the next code unit:
     - EOF → `E_LEX_UNTERMINATED_STRING` at the opening quote;
     - `"` or `\` → decoded character;
     - anything else, including CR, LF, or an unpaired surrogate →
       `E_LEX_INVALID_ESCAPE` at the backslash.
4. On punctuation `( ) [ ] { } , =`, emit punctuation token.
5. Otherwise read a maximal `Word` until whitespace, punctuation, or `"`.
6. Reject unpaired surrogates in `Word` or quoted text with
   `E_LEX_INVALID_UNICODE` at the offending code unit.
7. Append `EndOfInput` at input length.
8. Return the token list or the first lexical error.

There is no comment handling. There are no limits.

### 7.4 Parser algorithm

Functions:

```text
Parse(input)                  → AstFormula or syntax error
ParseExpr()                   → AstExpr
ParseTransportCall()          → AstTransportCall
ParseArguments()              → (IReadOnlyList<AstArgument>, closeParenOffset)
ParseValue()                  → AstValue
ParseList()                   → AstList
ParseStruct()                 → AstStruct
ParseTypeName()               → FormulaToken
```

Rules:

- Top level requires an identifier scheme followed by `(`.
- `ParseExpr` parses one transport call and at most one `as` cast.
- `ParseArguments`:
  - empty `()` allowed;
  - named argument starts when the current token is a Word followed by `=`;
  - named argument name must be an identifier;
  - positional arguments must precede named arguments;
  - trailing comma is rejected;
  - missing comma/close is rejected.
- `ParseValue`:
  - `"` → `AstScalar` quoted;
  - `[` → list;
  - `{` → struct;
  - Word followed by `(` → `ParseExpr`;
  - other Word → `AstScalar` bare;
  - anything else → expected value.
- `ParseList`: elements are expressions; nested list gets
  `E_SYN_NESTED_LIST`; trailing comma rejected; empty list allowed.
- `ParseStruct`: named fields only; `=` required; trailing comma rejected;
  empty struct allowed; field values are `ParseValue`.
- `ParseTypeName`: identifier required; known type checked only in semantics.
- `as` is recognized only immediately after a transport expression.
- Second `as` → `E_SYN_CHAINED_CAST`.
- Top-level list/struct/scalar → expected transport expression.

### 7.5 Parsing errors

Implement every code from `parsing.md` §13.1 and §13.2:

- `E_LEX_UNTERMINATED_STRING`
- `E_LEX_INVALID_ESCAPE`
- `E_LEX_RAW_NEWLINE_IN_STRING`
- `E_LEX_INVALID_UNICODE`
- `E_SYN_EMPTY_INPUT`
- `E_SYN_UNEXPECTED_TOKEN`
- `E_SYN_TRAILING_COMMA`
- `E_SYN_POSITIONAL_AFTER_NAMED`
- `E_SYN_CHAINED_CAST`
- `E_SYN_NESTED_LIST`
- `E_SYN_TRAILING_INPUT`

Offset rules are exactly those in `parsing.md`.

### 7.6 Parsing tests

Test suites/groups:

- **Lexing**
  - whitespace, CR/LF, tabs;
  - words vs identifiers;
  - punctuation;
  - quoted strings: empty, commas, braces, equals, escapes;
  - invalid escape, unterminated string, raw newline, unpaired surrogate;
  - offsets.
- **AST**
  - every node shape;
  - spans;
  - quoted/bare scalar flags;
  - cast optionality;
  - positional/named argument shape.
- **Parser**
  - all valid forms from `grammar.md`;
  - nested calls;
  - lists and structs;
  - casts;
  - positional/named combinations;
  - all syntax errors with codes and offsets;
  - top-level invalid shapes;
  - chained casts;
  - trailing commas;
  - no registry configured.
- **AST data**
  - `AstTransportCall.CloseParenOffset`;
  - `AstList.OpenOffset` and `CloseOffset`;
  - `AstStruct.OpenOffset` and `CloseOffset`;
  - `SourceSpan.End`.
- **Diagnostics**
  - `displayToken` escaping, surrogate rendering, 40-unit truncation, and
    escape-boundary truncation;
  - `E_LEX_*` precedence around backslash, CR/LF, and unpaired surrogates.
- **Properties**
  - parser never throws for arbitrary strings;
  - all returned errors have an offset in `[0, input.Length]`;
  - AST spans are within input.

### 7.7 Acceptance criteria

- Every grammar example in `grammar.md` parses or fails as documented.
- No reference to `Registry`, `Semantics`, or `TransportType`.
- No public class in `Pontifex.Factory.Parsing`.
- All parser error messages match `parsing.md`.

---

## 8. Registry subsystem

### 8.1 Public contract

Existing:

```text
Pontifex.Factory.Registry.IRegistry
Pontifex.Factory.Registry.IRegistryBuilder
Pontifex.Factory.Registry.ITransportTypeCatalog
```

DTOs:

- `TransportTypeSet`, `TransportTypeSetKind`
- `ScalarValidator`
- `Param`, `StringParam`, `TransportParam`, `TransportListParam`, `StructParam`
- `StructSchema`, `StructField`, `StringField`, `NestedStructField`
- `TransportTypeInfo`
- `TransportConstructorInfo`
- `TransportRegistration`

No public classes in `Pontifex.Factory.Registry`.

### 8.2 Internal components

**`TransportTypeCatalog`**

- Implements `ITransportTypeCatalog`.
- Stores:
  - canonical name → `TransportTypeInfo`;
  - `TransportType` → canonical name;
  - `AllTypes` in canonical order.
- Lookup is ASCII case-insensitive.
- Supports name-to-type, type-to-canonical-name, and `AllTypes` lookup.
- Canonical names are validated identifiers and unique.
- Registration is performed through `TransportRegistryBuilder`.
- Every constructor type and every `Expected` member must be present in the
  catalog before registration succeeds (`E_REG_TYPE_NOT_IN_CATALOG`).
- Built catalog is immutable and safe for concurrent reads.

**`TransportRegistry`**

- Implements `IRegistry`.
- Immutable snapshot of:
  - type catalog;
  - ordered constructor list;
  - canonical scheme map;
  - alias map;
  - `AllSchemeNames` — sorted union of canonical and alias names.
- `FindCandidates(scheme)` returns canonical plus alias candidates in a
  deterministic order.
- `AllSchemeNames` is used for `E_SEM_UNKNOWN_SCHEME`.
- Does not filter by expected type and does not apply canonical preference;
  those belong to semantics.

**`TransportRegistryBuilder`**

- Implements `IRegistryBuilder`.
- Mutable during setup only.
- `RegisterType` and `RegisterTransport` validate and normalize.
- `Build()` validates global invariants and returns an immutable registry.
- Registration after `Build()` throws `RegistrationException` with
  `E_REG_BUILDER_SEALED` (see `parsing.md` §6.7 rule 12 and §13.4).
- `SuggestedAliases` are not automatically registered; `TransportRegistration`
  aliases are.

**`MetadataValidator`**

- Name validation and normalization.
- Parameter list validation:
  - unique names;
  - identifiers;
  - `TransportListParam.Min >= 0`;
  - `Max` null or `Max >= Min`;
  - type sets non-empty.
- Struct schema validation:
  - unique and valid field names;
  - supported field kinds;
  - recursive validation with a visited set, allowing shared/cyclic schema
    references without revalidation.
- Alias validation:
  - identifiers;
  - duplicates within one registration ignored after normalization;
  - alias name may not be registered twice for the same `Type` by different
    constructors;
  - a canonical constructor is not an alias mapping; an alias may target the
    same `Type` as the canonical constructor for the scheme, and canonical
    preference resolves that overlap.
- Type catalog validation:
  - canonical names valid and unique case-insensitively;
  - constructor and expected types present in the catalog.

**`RegistryErrorFactory`**

- Produces every `E_REG_*` error from `parsing.md` §13.4, including
  `E_REG_TYPE_NOT_IN_CATALOG`.
- Registration errors use `RegistrationException` with phase `Registration`.

### 8.3 Registration validation order

Implement the numbered order from `parsing.md` §6.7 exactly:

0. Build and validate the type catalog first.
1. Normalize canonical names, aliases, parameter names, and nested struct-field
   names.
2. Validate canonical name, parameter names in declaration order, then alias
   names in input order.
3. Reject duplicate canonical names.
4. Reject duplicate parameter names within a constructor.
5. Normalize type sets and reject empty `OneOf`.
6. Validate transport-list ranges.
7. Validate struct schemas recursively, in schema order, with a visited set.
8. Normalize aliases; ignore duplicates within one registration and aliases
   equal to the constructor's own canonical name.
9. Enforce alias-to-type rules as described in §8.2.
10. Leave `SuggestedAliases` unregistered unless supplied as aliases.
11. Reject constructor or expected types absent from the catalog.
12. Reject registration after `Build()` with `E_REG_BUILDER_SEALED`; this
    lifecycle guard is evaluated before rule 0.

The first failure is reported.

### 8.4 Alias table

Desired candidate order:

1. canonical constructor for the scheme, if any;
2. alias candidates sorted by canonical scheme name and then transport type.

The exact order is used for stable error messages. Resolution may still choose
canonical after expected filtering.

Registration rules follow `parsing.md` §6.7 rule 9 exactly:

- canonical `Name` is unique;
- alias may map to multiple constructors only with different `Type`s;
- alias equal to its own constructor’s canonical name is ignored;
- duplicate aliases in one registration are ignored;
- a canonical constructor is not an alias mapping;
- an alias may target the same `Type` as the scheme’s canonical constructor;
- `SuggestedAliases` are hints only.

### 8.5 Registry tests

- type catalog registration and lookup;
- duplicate type names;
- invalid type names;
- constructor/expected type absent from catalog;
- canonical scheme uniqueness;
- parameter normalization and duplicates;
- invalid parameter names;
- invalid list ranges;
- empty type sets;
- struct schema duplicates, invalid names, nested/shared schemas;
- alias normalization, collisions, same-type duplicates, and canonical overlap;
- `FindCandidates` content and order;
- `AllSchemeNames` content and order;
- immutability after `Build`;
- registration after build (`E_REG_BUILDER_SEALED` for `RegisterType` and
  `RegisterTransport`);
- registration validation order;
- all `E_REG_*` messages.

### 8.5 Acceptance criteria

- No reference to `Parsing` or `Semantics`.
- All registration errors match `parsing.md` §13.4.
- Metadata is immutable after `Build`.
- No public class in `Pontifex.Factory.Registry`.

---

## 9. Semantics subsystem

### 9.1 Public contract

Existing:

```text
Pontifex.Factory.Semantics.IResolver
FormulaResolveResult Resolve(AstFormula formula, TransportTypeSet expected)
```

DTOs:

- `FormulaResolveResult`
- `ResolvedFormula`
- `ResolvedExpr`, `ResolvedTransport`, `ResolvedCast`
- `ParamBinding`
- `ResolvedValue`, `ScalarValue`, `TransportValue`, `TransportListValue`,
  `StructValue`
- `StructFieldBinding`
- `ResolvedStructFieldValue`, `StringFieldValue`, `NestedStructFieldValue`

`ResolvedExpr` exposes `TransportType Type` for every node. `ResolvedTransport`
inherits `Constructor.Type`; `ResolvedCast` returns `Target`.

No public classes in `Pontifex.Factory.Semantics`.

### 9.2 Internal components

**`FormulaResolver`**

- Implements `IResolver`.
- Holds `IRegistry`.
- Orchestrates resolution; creates `ResolvedFormula`.
- Converts semantic failures to `FormulaResolveResult` with `FactoryError`.

**`ConstructorSelector`**

- Resolves a scheme token plus expected type set to exactly one constructor.
- Algorithm:
  1. normalize scheme;
  2. candidates = `registry.FindCandidates(scheme)` (canonical plus aliases);
  3. empty → `E_SEM_UNKNOWN_SCHEME`, using `registry.AllSchemeNames`;
  4. if expected is not `Any`, filter by `expected.Contains`; empty →
     `E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE`, with `available` formatted from the
     pre-filter candidate set;
  5. if `registry.FindCanonical(scheme)` survived filtering, select it;
  6. otherwise one candidate → select; more → `E_SEM_AMBIGUOUS_SCHEME`, with
     candidates after filtering.
- Canonical preference applies after filtering, for any expected set.
- No schema-based overload, no converter tie-break, no backtracking.

**`ArgumentBinder`**

- Computes positional parameter slots in declaration order.
- Binds positional arguments left-to-right.
- Binds named arguments by normalized name.
- Rejects:
  - too many positional arguments;
  - unknown parameter;
  - duplicate parameter;
  - missing required parameter.
- Produces bindings in declaration order, not source order.
- Missing required parameter offset is the closing `)`.

**`ParameterValidator`**

- Validates each bound parameter in declaration order.
- String parameter:
  - requires `AstScalar`;
  - runs `ScalarValidator`;
  - null valid; non-null or thrown validator → `E_SEM_VALIDATOR_FAILED`.
- Transport parameter:
  - requires `AstExpr`;
  - resolves with the parameter’s expected type set;
  - expected membership is guaranteed by `ResolveExpr`; there is no
    user-visible `E_SEM_*` error for a post-resolution membership violation.
    A violation is an internal inconsistency reported by the compiler as
    `E_CMP_INCONSISTENT_TREE`.
- Transport list parameter:
  - requires `AstList`;
  - checks `Min`/`Max` before elements;
  - resolves every element with the expected set;
  - checks homogeneity after all elements;
  - no backtracking.
- Struct parameter:
  - requires `AstStruct`;
  - delegates to `StructValidator`.

**`StructValidator`**

- Binds source fields by normalized name.
- Rejects unknown and duplicate fields in source order.
- Checks missing required fields in schema order.
- Validates provided fields in schema order:
  - `StringField` requires scalar and runs its validator;
  - `NestedStructField` requires struct and recurses.
- Produces `StructValue` fields in schema order.
- Maintains a dot-separated field path for diagnostics. Known schema segments
  are canonical lowercase; an unknown leaf uses raw input spelling. Nested
  errors report the full path and keep the top-level parameter name.
- No transports or lists inside struct fields: the field kind check produces
  the corresponding expected-shape error.

**`SemanticErrorFactory`**

- Produces every `E_SEM_*` error from `parsing.md` §13.3.
- Applies exact offset rules and deterministic formatting.

### 9.3 Cast resolution

For `AstExpr` with a cast:

1. Resolve the target type through `ITransportTypeCatalog`.
   - unknown → `E_SEM_UNKNOWN_TRANSPORT_TYPE`.
2. If expected set does not contain target → `E_SEM_CAST_TYPE_MISMATCH`.
3. Resolve the inner transport with `Any`.
4. If inner type equals target, return the inner transport with the span of the
   full cast expression (identity collapse retains the cast span).
5. Otherwise return `ResolvedCast` with `Type == target`.

The parser has already rejected chained casts.

Casts do not select aliases. `alpha(...) as TypeB` resolves `alpha` with `Any`;
it does not ask for a TypeB variant of `alpha`.

### 9.4 Resolution order

Implement exactly this order:

1. Resolve scheme candidates.
2. Bind arguments.
3. Validate parameters in declaration order.
4. Resolve transport parameters and list elements in order.
5. Check list homogeneity after element resolution.
6. Resolve struct binding errors before field values.
7. Produce `ResolvedFormula`.

No error recovery, no multiple errors, no backtracking.

### 9.5 Semantics tests

- canonical and alias selection with `Any`, `Exactly`, `OneOf`;
- canonical preference after filtering;
- alias-only ambiguity;
- unknown scheme and no-variant errors;
- positional binding, named binding, mixed prefix;
- named-only parameters;
- too many positionals;
- unknown, duplicate, missing parameters;
- string validators: valid, invalid, throwing, empty reason;
- expected scalar/transport/list/struct shape errors;
- list min, max, element type, homogeneity;
- struct unknown, duplicate, missing, nested, field validators, field kinds;
- casts: valid, unknown target, expected mismatch, identity collapse retaining
  the full cast span, inner resolution with `Any`;
- nested struct field paths in error messages;
- expected-type membership is not a user-visible semantic error;
- error precedence and offsets;
- resolved tree invariants, `ResolvedExpr.Type`, and span retention.

### 9.6 Acceptance criteria

- No reference to `IDescription` or transport construction.
- All `E_SEM_*` messages match `parsing.md`.
- No public class in `Pontifex.Factory.Semantics`.
- Resolver determinism and no backtracking are tested.

---

## 10. Canonicalization subsystem

### 10.1 Public contract

Existing:

```text
Pontifex.Factory.Canonicalization.ICanonicalizer
string Canonicalize(ResolvedFormula formula)
```

No public classes in `Pontifex.Factory.Canonicalization`.

### 10.2 Internal components

**`FormulaCanonicalizer`**

- Implements `ICanonicalizer`.
- Accepts `ResolvedFormula` and `ITransportTypeCatalog` (constructor injection).
- Uses `TryGetCanonicalName` for cast targets and `AllTypes`/canonical lookup for
  diagnostics.
- Builds output with `StringBuilder`.
- Throws `PrinterException` with an `E_PRN_*` error for invalid resolved trees.
- Validates `Provided` by reference identity of `ParamBinding.Param` and
  `StructFieldBinding.Field`, not by name.

**`PositionalFormatter`**

- Given a constructor and provided parameters:
  - positional parameters are those with `Positional = true`;
  - `n` is the longest prefix of positional parameters that are all provided;
  - emit `params[0..n-1]` positionally;
  - emit every remaining provided parameter named in declaration order.
- Named-only parameters are always named.
- A provided positional parameter that is not in the prefix is emitted named.

**`ScalarFormatter`**

- Empty string → `""`.
- Safe bare form if non-empty and contains no whitespace or `( ) [ ] { } , = "`.
- Otherwise quote and escape `\` and `"`.
- Reject CR/LF or an unpaired surrogate in a resolved scalar with
  `E_PRN_INVALID_SCALAR`, reporting the first offending UTF-16 code unit as
  `U+XXXX`.
- Never emit aliases or source spelling for names.

**`PrinterInvariantValidator`**

- Checks, in the order defined by `parsing.md` §10:
  - all required parameters present;
  - every provided parameter belongs to the constructor, by reference identity;
  - no duplicate provided parameters;
  - `Provided` is in declaration order;
  - `Type == Constructor.Type`;
  - per parameter in declaration order: value kind, expected-type membership,
    list arity/homogeneity, struct fields (unknown → missing → duplicate → order
    → kind);
  - canonical scheme, parameter, and field names;
  - cast target exists and differs from inner type;
  - scalar printability.
- Failures produce the matching `E_PRN_*` error, including
  `E_PRN_STRUCT_FIELD_DUPLICATE` and the `E_PRN_INCONSISTENT_TREE` detail set
  from `parsing.md` §13.5.

**`CanonicalizationErrorFactory`**

- Produces every `E_PRN_*` error from `parsing.md` §13.5.

### 10.3 Printing algorithm

```text
Canonicalize(ResolvedFormula):
  validate tree
  return PrintExpr(root)

PrintExpr(expr):
  if ResolvedCast:
    if inner.Type == target:
      error E_PRN_INCONSISTENT_TREE detail "identity cast node"
    if not catalog.TryGetCanonicalName(target, out canonicalTarget):
      error E_PRN_INVALID_CAST_TARGET
    return PrintTransport(inner) + " as " + canonicalTarget
  if ResolvedTransport: PrintTransport(transport)

PrintTransport(t):
  name + "(" + join(parts, ", ") + ")"
  parts = positional prefix values + remaining named parameters

PrintValue:
  scalar → ScalarFormatter
  transport → PrintExpr
  list → "[" + join(elements, ", ") + "]"
  struct → "{" + schema-ordered fields + "}"
```

No prefix, no trailing comma, no trailing whitespace, no newline.

### 10.4 Canonicalization tests

- every printer example from `parsing.md`;
- positional prefix rules, including omitted positional parameters;
- named-only interleaving;
- comma-space output;
- minimal quoting: empty, commas, equals, braces, quotes, backslashes,
  whitespace, Unicode, CR/LF, unpaired surrogates;
- lists and nested structs;
- identity casts absent;
- aliases normalized to canonical names;
- struct schema order and duplicate struct fields;
- provided-parameter order and non-canonical names;
- expected-type membership and constructor-type inconsistencies;
- all `E_PRN_*` invariant errors and `E_PRN_INCONSISTENT_TREE` details;
- round-trip:
  - `Parse(Print(t)) == t`;
  - `Print(Parse(Print(Parse(s)))) == Print(Parse(s))`;
  - output contains no raw CR/LF.

### 10.5 Acceptance criteria

- No reference to `Parsing`, `IRegistry`, or `IDescription`.
- Only `ResolvedFormula` + type catalog + metadata in resolved nodes.
- All `E_PRN_*` errors covered.
- No public class in `Pontifex.Factory.Canonicalization`.

---

## 11. Compilation subsystem

### 11.1 Public contract

Existing:

```text
Pontifex.Factory.Compilation.ICompiler<TOutput>
TOutput Compile(ResolvedFormula formula)
```

No public classes in `Pontifex.Factory.Compilation`.

### 11.2 Core compiler boundary

The core project defines only the generic interface. It must not reference:

- `IDescription`
- `IElement`
- `ITransport`
- `ILogger`
- `IMemoryRental`
- concrete transports

### 11.3 `IDescription` adapter

Recommended as a separate project:

```text
Pontifex.Factory.Description/
  Pontifex.Factory.Description.csproj
  DescriptionCompiler.cs
```

References:

- `Pontifex.Factory`
- `Pontifex` for existing descriptor types under `Pontifex.OldFactory`

Mapping, following `parsing.md` §9.1:

- `ResolvedTransport` → `Description` with:
  - `name` = canonical constructor name;
  - `type` = canonical type name, resolved through
    `ITransportTypeCatalog.TryGetCanonicalName`;
  - each provided parameter by declared name, with parameter keys prefixed
    `param.`:
    - `ScalarValue` → `StringElement`
    - `TransportValue` → `DescriptionElement`
    - `TransportListValue` → `ArrayElement` of `DescriptionElement`
    - `StructValue` → `DescriptionElement`
- `ResolvedCast` → internal adapter `Description` with `name = "convert"`,
  `type = canonical target`, `from = DescriptionElement(inner)`.
- struct-field keys are prefixed `field.`.
- absent parameters are absent.
- no default synthesis.
- the adapter reserves the scheme name `convert`; encountering a constructor
  with that canonical name produces `E_CMP_RESERVED_SCHEME`.
- the adapter’s `name`, `type`, and `from` keys are reserved.
- no `IDescription` type exists outside this adapter assembly.

The adapter validates the resolved-tree invariants it relies on and maps failures
to `CompilerException` with `E_CMP_*` codes. Its validation order follows the
printer’s order in `parsing.md` §10.

### 11.4 Build-time conversion

The adapter only serializes the cast node. The builder or converter graph
performs the actual conversion:

- identity casts are already collapsed;
- otherwise build inner and request a conversion path;
- no path → `E_BLD_NO_CONVERSION_PATH`;
- constructor failure → `E_BLD_CONSTRUCTOR_FAILED`.

The parser and resolver must not check converter paths.

### 11.5 Compilation tests

- `ResolvedTransport` to `IDescription` mapping for every value kind;
- nested transports and lists;
- nested structs;
- cast adapter shape and canonical target lookup;
- reserved `convert` scheme → `E_CMP_RESERVED_SCHEME`;
- invalid cast target → `E_CMP_INVALID_CAST_TARGET`;
- absent parameters remain absent;
- deterministic key order and `param.`/`field.` prefixes;
- invalid resolved trees produce `CompilerException` with `E_CMP_*`;
- no `IDescription` references outside the adapter assembly.

### 11.6 Acceptance criteria

- `Pontifex.Factory` remains free of descriptor and builder references.
- `IDescriptionCompiler` is replaceable without touching the language.
- All mapping rules and build-time boundary are tested.

---

## 12. Bootstrap subsystem

### 12.1 Public contract

Existing root interfaces:

```text
Pontifex.Factory.ITransportFactory
Pontifex.Factory.ITransportFactoryBuilder
```

The bootstrap also exposes a single public creation seam, because every
implementation type is internal:

```text
Pontifex.Factory.TransportFactoryBootstrap.CreateBuilder() -> ITransportFactoryBuilder
```

`CreateBuilder` returns interfaces only. It is the only public way to obtain a
builder; callers never see `TransportFactoryBuilder`, `TransportFactory`, or
`FactoryComponentFactory`.

Add the root exception hierarchy:

```text
Pontifex.Factory.FactoryException
Pontifex.Factory.RegistrationException
Pontifex.Factory.PrinterException
Pontifex.Factory.CompilerException
Pontifex.Factory.BuildException
```

The bootstrap is the only public place where concrete wiring lives.

### 12.2 Internal components

**`TransportFactoryBuilder`**

- Implements `ITransportFactoryBuilder`.
- Holds `IRegistryBuilder`.
- `Build()`:
  - freezes registry;
  - creates parser, resolver, canonicalizer;
  - returns `TransportFactory`;
  - is idempotent: repeated calls return the same `ITransportFactory`
    instance (matching `TransportRegistryBuilder.Build`);
  - fails if the registry is invalid. Registration errors surface during
    `RegisterType`/`RegisterTransport`, before `Build`.

**`TransportFactory`**

- Implements `ITransportFactory`.
- Holds `IParser`, `IResolver`, `ICanonicalizer`; the frozen registry is owned
  by the resolver and the canonicalizer.
- `Resolve(string, expected)`:
  1. parse;
  2. on error return parse result;
  3. resolve;
  4. return resolve result.
- `Canonicalize(string)`:
  1. parse + resolve;
  2. on parse/resolve error throw `FactoryException` with the same `FactoryError`;
  3. canonicalize; canonicalizer failures are `PrinterException`.
- `Compile<T>(string, ICompiler<T>, expected)`:
  1. parse + resolve;
  2. on parse/resolve error throw `FactoryException`;
  3. call the caller-supplied compiler. `ICompiler<T>` is opaque, so exceptions
     it raises propagate unchanged; a compiler that detects its own invariant
     failure may throw `CompilerException`.
- `FactoryError` phase must identify the failing stage.
- No hidden static state.

**`FactoryComponentFactory`**

- Internal wiring helper.
- Creates parser, resolver, canonicalizer from a built registry.
- Does not expose component instances publicly.

### 12.3 Exception and result policy

- `IParser.Parse` and `IResolver.Resolve` return result DTOs.
- `ITransportFactory.Resolve` returns a result DTO.
- `ITransportFactory.Canonicalize` and `Compile` throw `FactoryException`
  because they cannot return a partial result; the derived type identifies the
  stage.
- Registration throws `RegistrationException`.
- Printing throws `PrinterException`.
- Compilation throws `CompilerException` for invariant errors.
- Build-time conversion errors remain `E_BLD_*` / `BuildException` outside the
  formula core.

### 12.4 Bootstrap tests

- the public `CreateBuilder` seam returns a working builder;
- builder creates a working factory;
- two builders are independent (no hidden static registry);
- parser/resolver/canonicalizer wiring;
- parse errors pass through `Resolve`;
- resolve errors pass through `Canonicalize`/`Compile`;
- null expected type means `Any`;
- null formula is reported as empty input (`nonapproved-behaviour.md` §6.1);
- canonicalization errors wrapped as `PrinterException` with phase `Printing`
  (unreachable through the facade with a correct resolver; covered at the
  canonicalizer layer);
- compilation errors with phase `Compilation` (compiler-raised exceptions
  propagate unchanged);
- null compiler argument guard;
- `Build` twice returns the same factory;
- registry is sealed after build;
- facade does not expose `IDescription`.

### 12.5 Acceptance criteria

- A caller can use only `ITransportFactory` for resolve/canonicalize/compile.
- Subsystems remain independently usable for tests and tooling.
- No static registry or global state.
- All phases are represented correctly in `FactoryError`.

---

## 13. Cross-cutting error implementation

Implement a single error-catalogue module with one method per code:

```text
ErrorFactory.LexUnterminatedString(span)
ErrorFactory.SyntaxUnexpectedToken(expected, token)
ErrorFactory.SemanticUnknownScheme(scheme, span, known)
...
```

Rules:

- Messages are copied exactly from `parsing.md` §13.
- Placeholders are formatted deterministically.
- Name lists are sorted.
- Parameter/field lists use declaration/schema order.
- Offsets use `SourceSpan.Start`.
- EOF offset is input length.
- No localized resources in this phase.
- No “did you mean” suggestions.
- No error recovery.

Tests must assert both code and offset; message assertions are recommended for all
templates.

---

## 14. Testing strategy

### 14.1 Test project structure

The test project is currently bare. Before adding tests, follow
`Tests/RULES.md`. Recommended layout is one suite per subsystem:

```text
Tests/Pontifex.Factory.Tests/
  Parsing/
    README.md
    Coverage.md
    Tests/
  Registry/
    README.md
    Coverage.md
    Tests/
  Semantics/
    README.md
    Coverage.md
    Tests/
  Canonicalization/
    README.md
    Coverage.md
    Tests/
  Compilation/
    README.md
    Coverage.md
    Tests/
  Bootstrap/
    README.md
    Coverage.md
    Tests/
```

Each subsystem directory is its own suite root, so its `README.md` and
`Coverage.md` are owned by that suite. The project root itself is not a suite
unless a project-level suite is explicitly defined.

If a suite is not warranted yet, use loose test files outside suite directories;
promote them when they become cohesive, as `Tests/RULES.md` requires.

### 14.2 Test categories

- **Conformance** — every valid/invalid example in `grammar.md` and
  `parsing.md`.
- **Unit** — lexer, parser, registry validation, binder, validators, printer.
- **Error catalogue** — every code, message template, and offset.
- **Property** — parser never crashes; spans in range; round-trip stability.
- **Component isolation** — parsing tests with no registry; registry tests with
  no parser; printer tests with hand-built resolved trees.
- **Adapter** — `IDescription` mapping in the bridge project.

### 14.3 Determinism

- No real time, sockets, files, or transports.
- No mocking library; use hand-written test doubles as required by
  `Tests/RULES.md`.
- Tests are order-independent and isolated.

### 14.4 Coverage expectations

Aim for:

- all grammar productions;
- all error codes;
- all parameter kinds;
- all alias/type-set combinations;
- all canonicalization branches;
- all compiler value kinds;
- all facade phases.

---

## 15. Implementation sequence

### Milestone 0 — Shared infrastructure

- Add `FactoryException` and the `RegistrationException`, `PrinterException`,
  `CompilerException`, `BuildException` subclasses.
- Add `NameNormalizer`, `TokenDisplay`, `FormulaErrorFactory`,
  `TypeSetFormatter`.
- Reconcile `parsing.md` §12.1 terminology with `FactoryError` and the exception
  hierarchy.
- Add tests for formatting and normalization.
- Build passes.

### Milestone 1 — Parsing

- Implement lexer, cursor, parser, parsing errors.
- Add parsing tests and conformance examples.
- Parser works with no registry.
- Build/test passes.

### Milestone 2 — Registry

- Implement type catalog, registry, builder, metadata validation, registration
  errors.
- Add registry tests.
- Build/test passes.

### Milestone 3 — Semantics

- Implement constructor selector, binder, parameter/struct validators, resolver,
  semantic errors.
- Add resolution matrix tests.
- Build/test passes.

### Milestone 4 — Canonicalization

- Implement canonicalizer, positional formatter, scalar formatter, invariant
  validator.
- Add printer and round-trip tests.
- Build/test passes.

### Milestone 5 — Compilation

- Keep `ICompiler<T>` in core.
- Add `Pontifex.Factory.Description` adapter project if approved.
- Add mapping and boundary tests.
- Build/test passes.

### Milestone 6 — Bootstrap

- Implement `TransportFactoryBuilder`, `TransportFactory`, `FactoryComponentFactory`,
  and the public `TransportFactoryBootstrap.CreateBuilder` seam.
- Add facade tests.
- Build/test passes.

### Milestone 7 — Conformance and hardening

- Run the full error catalogue.
- Add property tests.
- Verify no forbidden dependencies with project references and code review.
- Verify `dotnet build` and `dotnet test` clean.
- Update `README.md`/`Coverage.md` per `Tests/RULES.md`.

---

## 16. Definition of done

- `grammar.md` and `parsing.md` are implemented without deviations.
- The scaffold-to-spec reconciliation in §3.1 is complete, including AST offsets,
  `SourceSpan.End`, `ResolvedExpr.Type`, catalog canonical-name lookup,
  `AllSchemeNames`, and type-set helpers.
- All subsystem public namespaces contain only interfaces and DTOs.
- No public implementation class exists in a subsystem namespace.
- Parsing has no registry dependency.
- Registry has no parsing/semantics dependency.
- Semantics is the only syntax/metadata join.
- Canonicalization has no parser dependency.
- Compilation is the only descriptor/builder boundary.
- No `IDescription` reference exists in `Pontifex.Factory`.
- All error codes from `parsing.md` are implemented and tested.
- Canonical round-trip properties pass.
- No implementation limits are introduced.
- No `transport://`, comments, aliases in canonical output, default synthesis,
  schema overload, or converter tie-breaking exists.
- All subsystems remain independently usable; the public creation seam is the
  only way to obtain a builder, and all bootstrap implementation types are
  internal.
- `ITransportFactory.Resolve` returns result DTOs; `Canonicalize` and `Compile`
  throw `FactoryException` with the failing stage's phase.
- `dotnet build` and `dotnet test` pass with zero warnings/errors.
- Test suites follow `Tests/RULES.md`.

---

## 17. Risks and open decisions

1. **Adapter project.** `Pontifex.Factory.Description` is recommended. If it is
   not created, the adapter must remain internal to `Pontifex.Factory` and must
   not leak descriptor types into public APIs.
2. **Exception hierarchy placement.** The exception hierarchy lives in the root
   bootstrap namespace. If the team wants zero public classes outside
   interfaces, replace exceptions with result DTOs or keep the hierarchy
   internal and expose only `FactoryError`.
3. **`FormulaToken` visibility.** It is currently public. Keep it public for
   parser tooling and diagnostics, or make it internal and expose only AST/errors.
4. **`TransportTypeSet` ergonomics.** The DTO has no static helpers. Internal
   factory methods can be added in `Internal` without changing the DTO shape.
5. **Legacy retirement.** `Pontifex.OldFactory` and the old `TransportBuilder`
   must remain untouched during this phase. Plan their retirement only after the
   new compiler adapter is proven.
6. **No limits vs stack depth.** The language defines no limits. The parser must
   not impose artificial ones; if recursive descent risks stack exhaustion on
   adversarial input, use an explicit stack or document the implementation
   strategy without adding a language limit.
7. **Span retention.** Resolved DTOs retain `SourceSpan`. Keep this for diagnostics
   and tests; it is not printed.
