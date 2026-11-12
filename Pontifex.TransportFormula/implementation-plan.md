# Transport Formula and Factory Implementation Plan

## 1. Purpose

This document is the implementation plan for the Transport Formula language and
the runtime factory that compiles formulas into live transports.

Target assemblies:

- `Pontifex` — core transport types plus the transport-facing registration
  contract in `Pontifex.Factory.Registration`.
- `Pontifex.TransportFormula` — grammar, parser, registry, resolver, canonical
  printer, generic compiler boundary, and formula error types.
- `Pontifex.Factory` — direct transport compiler, runtime constructor registry,
  bootstrap, and public facade.
- `Tests/Pontifex.Factory.Tests` — conformance and component test suites.

After the Step 1 project split, the formula sources and this plan live in
`Pontifex.TransportFormula` and the runtime facade lives in `Pontifex.Factory`.
Section 4 records the transition; source paths in this document describe the
target layout unless they explicitly say "current".

Normative specifications:

- `grammar.md` — syntax of Transport Formulas.
- `parsing.md` — parser, resolver, compiler, canonical printer, and error
  behavior.

This plan covers implementation structure, internal components, algorithms,
test strategy, milestones, and acceptance criteria. It does not implement any
subsystem.

### Document status

Implemented and covered by the existing suites:

- lexer, parser, AST, and syntax/lexical errors;
- transport type catalog and metadata registry;
- resolver, argument binder, parameter and struct validation;
- canonical printer and invariant validation;
- bootstrap facade over the formula pipeline;
- core transport registration contract in `Pontifex/Factory/Registration`;
- project decomposition into `Pontifex.TransportFormula` and `Pontifex.Factory`;
- direct `ITransport` compiler and runtime constructor registry;
- `Compilation` test suite (54 tests);
- bootstrap integration (`RegisterTransport`, `CreateCompiler`,
  `CoreRegistrationAdapter`, registry sealing).

Not implemented yet:

- migration of concrete transport constructors to the core registration
  contract;
- optional `TransportTypeId` decoupling and its binding layer.

---

## 2. Goals and non-goals

### Goals

- Replace URI parsing and ad-hoc per-constructor URI parsers with a single
  Transport Formula language.
- Keep the syntax layer independent of transports and metadata.
- Make metadata an immutable, validated registry.
- Make resolution the only place where syntax and metadata meet.
- Make canonical printing deterministic and alias-free.
- Compile resolved formulas directly into live `ITransport` instances.
- Keep the formula assembly free of `ITransport`, logger, memory, and converter
  references.
- Let transport packages register with the factory while depending only on the
  core `Pontifex` assembly.
- Provide a small bootstrap facade that hides component wiring.

### Non-goals

- Replacing the legacy `TransportBuilder` and `Pontifex.OldFactory` in this
  change.
- Migrating every concrete transport constructor in this change.
- Removing the legacy descriptor types before the new compiler is proven.
- UI integration in `Pontifex.TestUI`.
- Changing the Transport Formula syntax, resolution rules, or canonical output.

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
- Lists are `[list_element, ...]`, where an element may be a transport
  expression, struct, quoted string, or bare scalar; no nested lists; no
  top-level list.
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
- Struct schemas contain only string fields, integer fields, decimal fields,
  array fields, and nested struct fields.
- Integer, decimal, and array ranges are validated by the formula registry, not
  by core DTO constructors, so `E_REG_INVALID_INTEGER_RANGE`,
  `E_REG_INVALID_DECIMAL_RANGE`, and `E_REG_INVALID_ARRAY_RANGE` are reachable
  through `CoreRegistrationAdapter`.

### Semantics

- Resolved values have two branches: `DataValue` is pure data (strings, integers,
  decimals, structs, and arrays); `ResolvedExpr` is the expression-value branch.
  Transport parameters store `ResolvedExpr` directly; struct fields store
  `DataValue`.
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

- `ICompiler<T>` is generic and opaque; it lives in the formula assembly.
- The runtime factory implements `ITransportCompiler : ICompiler<ITransport>`.
- Transport packages implement the core registration contract in
  `Pontifex.Factory.Registration`; they do not reference the formula or factory
  assemblies.
- `IDescription`, `IElement`, and the legacy builder are not part of the new
  compilation path.
- Constructor lookup failure is `E_BLD_CONSTRUCTOR_NOT_FOUND`.
- Constructor or converter failure is `E_BLD_CONSTRUCTOR_FAILED` with the
  original exception preserved as the inner exception.
- Build-time conversion failure is `E_BLD_NO_CONVERSION_PATH`.
- Compiler invariant violations use `E_CMP_*` and `CompilerException`.
- The parser and resolver never check converter availability.

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
- `parsing.md` §12.1 documents the concrete `FactoryError` record and the
  class-based hierarchy. Error codes, message templates, and offset rules do not
  change.

### 3.2 Direct-compiler revision decisions

These decisions supersede the earlier `IDescription` adapter plan.

- **Project split.** The formula language moves to `Pontifex.TransportFormula`.
  The `Pontifex.Factory` name is reused by the runtime assembly that references
  both `Pontifex` and `Pontifex.TransportFormula`.
- **No descriptor adapter.** `IDescription`, `IElement`, `DescriptionElement`,
  `ArrayElement`, and `Pontifex.OldFactory` are not referenced by the new
  compiler or by the formula assembly.
- **Direct result.** `TransportCompiler` returns `ITransport`.
- **Transport-owned registration.** The transport-facing contract lives in
  `Pontifex.Factory.Registration`, so transport packages reference only
  `Pontifex`.
- **One registration source.** The facade registers a core
  `ITransportRegistration` once and derives both the formula metadata and the
  runtime constructor entry from it.
- **Lazy nested construction.** Nested transport values are exposed as
  `NestedTransportFactory` delegates, not as pre-built instances. This preserves
  reconnect-style transports that need to create a fresh nested transport later.
- **Per-compiler role.** `TransportRole` is compiler configuration, not formula
  data. The resolved tree remains side-neutral.
- **Compiler creation seam.** The facade exposes a way to create a
  `ITransportCompiler` from its frozen runtime registry; callers do not construct
  the concrete compiler or registry.
- **Converter policy.** The facade owns the converter graph used by the compiler;
  the formula layer never validates converter paths.

---

## 4. Solution and project layout

Historical layout before Step 1 (for migration context):

```text
Pontifex/
  Factory/Registration/        core transport registration contract
  Abstractions/                TransportType, ITransport, ...
  Converters/                  IConvertersGraph, converters

Pontifex.Factory/              formula + bootstrap today
  grammar.md
  parsing.md
  implementation-plan.md
  SourceSpan.cs
  FactoryError.cs
  FactoryExceptions.cs
  IsExternalInit.cs
  ITransportFactory.cs
  ITransportFactoryBuilder.cs
  TransportFactoryBootstrap.cs
  Parsing/
  Registry/
  Semantics/
  Canonicalization/
  Compilation/
    ICompiler.cs
  Bootstrap/

Tests/Pontifex.Factory.Tests/
  Parsing/
  Registry/
  Semantics/
  Canonicalization/
  Bootstrap/
```

Target layout (Steps 1-3 implemented; Step 4 bootstrap wiring pending):

```text
Pontifex/
  Factory/Registration/        transport-facing registration contract
    ITransportRegistration.cs
    TransportRegistration.cs
    ITransportBuildContext.cs
    TransportParameter.cs
    TransportTypeSet.cs
    StructSchema.cs
    ScalarValidator.cs

Pontifex.TransportFormula/     renamed formula assembly
  Pontifex.TransportFormula.csproj
  grammar.md
  parsing.md
  implementation-plan.md
  SourceSpan.cs
  FactoryError.cs
  FactoryExceptions.cs
  IsExternalInit.cs
  Parsing/
    IParser.cs
    FormulaToken.cs
    AstDtos.cs
    FormulaParseResult.cs
    Internal/...
  Registry/
    IRegistry.cs
    ITransportTypeCatalog.cs
    MetadataDtos.cs
    Internal/...
  Semantics/
    IResolver.cs
    ResolvedDtos.cs
    Internal/...
  Canonicalization/
    ICanonicalizer.cs
    Internal/...
  Compilation/
    ICompiler.cs               generic only
  Internal/
    NameNormalizer.cs
    FormulaErrorFactory.cs
    TokenDisplay.cs
    TypeSetFormatter.cs

Pontifex.Factory/              runtime factory assembly
  Pontifex.Factory.csproj
  ITransportFactory.cs
  ITransportFactoryBuilder.cs
  TransportFactoryBootstrap.cs
  Compilation/
    ITransportCompiler.cs
    TransportCompiler.cs
    Internal/TransportBuildContext.cs
  ITransportConstructorRegistry.cs
  TransportConstructorRegistry.cs
  TransportTypeBindings.cs      only if TypeId decoupling is adopted
  Bootstrap/
    TransportFactory.cs
    TransportFactoryBuilder.cs
    FactoryComponentFactory.cs
    CoreRegistrationAdapter.cs

Tests/Pontifex.Factory.Tests/
  Parsing/
  Registry/
  Semantics/
  Canonicalization/
  Bootstrap/
  Compilation/                  created with its first test file
```

Transition rules:

- The move is a rename plus namespace update, not a rewrite. Formula behavior and
  DTOs do not change. Formula namespaces become `Pontifex.TransportFormula.*`
  (`Parsing`, `Registry`, `Semantics`, `Canonicalization`, `Compilation`, and
  shared root types). Runtime namespaces stay under `Pontifex.Factory.*`.
- `Pontifex.TransportFormula` must not reference `ITransport`, `ILogger`,
  `IMemoryRental`, `IConvertersGraph`, or concrete transports. Until the
  optional `TransportTypeId` decoupling is adopted, it references `Pontifex`
  only for `TransportType`.
- `Pontifex.Factory` references `Pontifex` and `Pontifex.TransportFormula`.
- Transport packages reference `Pontifex` only and implement
  `Pontifex.Factory.Registration.ITransportRegistration`.
- Test suite READMEs keep their current links until the move lands; the move
  commit updates those links. Suite namespaces stay
  `Pontifex.Factory.Tests.<Subsystem>` and categories stay
  `Factory.<Subsystem>` unless the test project itself is renamed.

Do not create a `Pontifex.Factory.Description` project. The descriptor adapter
is obsolete and must not be reintroduced.

---

## 5. Dependency rules

```text
Core registration       → Pontifex only (TransportType, ITransport, logging, memory)
Formula Parsing         → shared formula helpers
Formula Registry        → TransportType (current) or TransportTypeId (optional)
Formula Semantics       → Parsing + Registry
Formula Canonicalization → Semantics + Registry (type catalog/metadata)
Formula Compilation     → Semantics (generic ICompiler<T> only)
Runtime Compilation     → Pontifex + formula Semantics + core registration
Bootstrap               → formula + runtime compilation
```

Rules:

- Core registration lives in `Pontifex` and has no formula dependency.
- Formula Parsing must not reference Registry, Semantics, or transport types.
- Formula Registry must not reference Parsing or Semantics.
- Formula Semantics may reference both Parsing and Registry.
- Formula Canonicalization must not reference Parsing.
- Formula Compilation must not reference Parsing, Registry, `ITransport`, logging,
  or memory.
- Runtime Compilation is the only place that joins a resolved tree with live
  transports and converters.
- Bootstrap is the only place that wires formula and runtime components together.
- Public formula namespaces contain interfaces and DTOs only. Runtime
  implementation classes may be public only when they are the documented
  compiler or registry surface; bootstrap wiring classes remain internal.

---

## 6. Shared infrastructure

Implement shared internals before the subsystems that use them.

### 6.1 `FactoryError` and exceptions

`FactoryError` already exists and is the single diagnostic DTO used by result
objects. Its phase and span identify the failing stage and source position.

Add a small exception hierarchy in the formula assembly root namespace,
preserving the names already specified in `parsing.md` §12.1:

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

`parsing.md` §12.1 documents the concrete `FactoryError` record and the
class-based hierarchy. The runtime factory assembly reuses these types; it does
not define a second error model.

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
- Provide helpers for all formula error codes in §13 of `parsing.md`.
- Build and compiler errors (`E_BLD_*`, `E_CMP_*`) are constructed by the runtime
  compiler, not by this factory.

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
Pontifex.TransportFormula.Parsing.IParser
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

No public classes in `Pontifex.TransportFormula.Parsing`.

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
- `ParseList`: elements are generic `ParseValue` results; nested list gets
  `E_SYN_NESTED_LIST` with message `nested lists are not allowed`; trailing
  comma rejected with context `list`; empty list allowed.
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
  - positional/named argument shape;
  - `AstList.Elements` accepts expressions, structs, and scalars but never
    nested lists.
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
- No public class in `Pontifex.TransportFormula.Parsing`.
- All parser error messages match `parsing.md`.

---

## 8. Registry subsystem

### 8.1 Public contract

Existing:

```text
Pontifex.TransportFormula.Registry.IRegistry
Pontifex.TransportFormula.Registry.IRegistryBuilder
Pontifex.TransportFormula.Registry.ITransportTypeCatalog
```

DTOs:

- `TransportTypeSet`, `TransportTypeSetKind`
- `ScalarValidator`
- `SingleType`, `StringType`, `IntegerType`, `DecimalType`
- `Param`, `StringParam`, `IntegerParam`, `DecimalParam`, `TransportParam`,
  `TransportListParam`, `StructParam`
- `StructSchema`, `StructField`, `StringField`, `IntegerField`, `DecimalField`,
  `ArrayField`, `NestedStructField`
- `TransportTypeInfo`
- `TransportConstructorInfo`
- `TransportRegistration`

No public classes in `Pontifex.TransportFormula.Registry`.

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
  - integer and decimal parameter ranges have `Min <= Max`;
  - `TransportListParam.Min >= 0`;
  - `Max` null or `Max >= Min`;
  - type sets non-empty.
- Struct schema validation:
  - unique and valid field names;
  - supported field kinds;
  - array count ranges and element scalar ranges;
  - integer and decimal field ranges have `Min <= Max`;
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
5. Normalize type sets for all parameters and reject empty `OneOf`; then check
   integer and decimal parameter ranges in declaration order. A full type-set
   pass precedes the full scalar-range pass.
6. Validate transport-list ranges.
7. Validate struct schemas recursively, in schema order, with a visited set:
   field name, duplicate, kind, array count range, array element scalar range,
   field range, then nested schema recursion.
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
- integer and decimal parameter and field ranges, including validation order;
- array field count ranges, element scalar ranges, and schema order;
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
- No public class in `Pontifex.TransportFormula.Registry`.

---

## 9. Semantics subsystem

### 9.1 Public contract

Existing:

```text
Pontifex.TransportFormula.Semantics.IResolver
FormulaResolveResult Resolve(AstFormula formula, TransportTypeSet expected)
```

DTOs:

- `FormulaResolveResult`
- `ResolvedFormula`
- `ResolvedExpr`, `ResolvedTransport`, `ResolvedCast`
- `ParamBinding`
- `ResolvedValue`, `DataValue`, `ResolvedExpr`, `ScalarValue`, `StringValue`,
  `IntegerValue`, `DecimalValue`, `TransportListValue`, `StructValue`,
  `ArrayValue`
- `StructFieldBinding`

`ResolvedExpr` exposes `TransportType Type` for every node. `ResolvedTransport`
inherits `Constructor.Type`; `ResolvedCast` returns `Target`. A transport
parameter binds to its `ResolvedExpr` directly, and `StructFieldBinding.Value`
is a `DataValue`.

No public classes in `Pontifex.TransportFormula.Semantics`.

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
- Integer parameter:
  - requires `AstScalar`;
  - parses with `NumberStyles.AllowLeadingSign` and invariant culture;
  - parse failure → `E_SEM_VALIDATOR_FAILED` reason `expected an integer`;
  - range failure after parsing → reason `out of range [<min>, <max>]`.
- Decimal parameter:
  - requires `AstScalar`;
  - parses exactly through `DecimalText.TryParse`; overflow, underflow, and
    precision loss are parse failures;
  - parse failure → `E_SEM_VALIDATOR_FAILED` reason `expected a decimal`;
  - range failure after parsing → reason `out of range [<min>, <max>]`.
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
  - requires every element to be `AstExpr`; otherwise
    `E_SEM_LIST_ELEMENT_TYPE` at the element start with the actual AST shape;
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
  - `IntegerField` requires scalar, parses exactly as `Int64`, and checks range;
  - `DecimalField` requires scalar, parses exactly as `decimal`, and checks
    range;
  - `ArrayField` requires a list, checks count, then validates every element in
    index order: shape, then string/integer/decimal value and range;
  - `NestedStructField` requires struct and recurses.
- Produces `StructValue` fields in schema order.
- Maintains a dot-separated field path for diagnostics. Known schema segments
  are canonical lowercase; an unknown leaf uses raw input spelling. Nested
  errors report the full path and keep the top-level parameter name.
- No transports or transport lists inside struct fields; array fields accept
  homogeneous scalar lists. The field kind check produces the corresponding
  expected-shape error.

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
- integer parsing, range, and overflow cases;
- decimal exact parsing, canonical forms, underflow, overflow, precision loss,
  and range cases;
- array count, element kind, indexed field paths, and string/integer/decimal
  element cases;
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

- No reference to transports, converters, or the direct compiler.
- All `E_SEM_*` messages match `parsing.md`.
- No public class in `Pontifex.TransportFormula.Semantics`.
- Resolver determinism and no backtracking are tested.

---

## 10. Canonicalization subsystem

### 10.1 Public contract

Existing:

```text
Pontifex.TransportFormula.Canonicalization.ICanonicalizer
string Canonicalize(ResolvedFormula formula)
```

No public classes in `Pontifex.TransportFormula.Canonicalization`.

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
    integer/decimal range, list arity/homogeneity, struct fields (unknown →
    missing → duplicate → order → kind → range), array arity → element kind →
    element range/printability;
  - canonical scheme, parameter, and field names;
  - cast target exists and differs from inner type;
  - string printability.
- Uses `DecimalText` for canonical decimal text and range messages.
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
  string → ScalarFormatter
  integer → invariant Int64 text
  decimal → canonical plain decimal text
  array → "[" + join(elements, ", ") + "]"
  ResolvedExpr → PrintExpr
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
- integer canonical text: no leading `+`, no leading zeros, no exponent;
- decimal canonical text: trailing zeros trimmed, negative zero as `0`, no
  exponent, decimal min/max and the smallest scale in plain form;
- arrays: empty, string quoting, integer/decimal canonical elements, count,
  element kind, and indexed paths in errors;
- `DecimalText` exact parsing, underflow/rounding rejection, and round-trip;
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

- No reference to `Parsing`, the registry, transports, or the direct compiler.
- Only `ResolvedFormula` + type catalog + metadata in resolved nodes.
- All `E_PRN_*` errors covered.
- No public class in `Pontifex.TransportFormula.Canonicalization`.

---

## 11. Compilation subsystem

### 11.1 Generic compiler boundary

The formula assembly defines only:

```text
Pontifex.TransportFormula.Compilation.ICompiler<TOutput>
TOutput Compile(ResolvedFormula formula)
```

The formula assembly does not reference `ITransport`, `IConvertersGraph`,
`ILogger`, `IMemoryRental`, or the core registration contract.

### 11.2 Direct transport compiler

Public runtime contract:

```text
Pontifex.Factory.Compilation.ITransportCompiler : ICompiler<ITransport>
    TransportRole Role { get; }

Pontifex.Factory.Compilation.TransportCompiler
    TransportCompiler(
        ITransportConstructorRegistry constructors,
        IConvertersGraph converters,
        ILogger logger,
        IMemoryRental memory,
        TransportRole role)
```

`TransportCompiler` is the documented public compilation class in
`Pontifex.Factory`. Its internal `TransportBuildContext : ITransportBuildContext`
maps resolved values to the core registration contract.

The runtime constructor registry:

```text
Pontifex.Factory.ITransportConstructorRegistry
    ITransportRegistration? Find(string canonicalScheme, TransportType type)

Pontifex.Factory.TransportConstructorRegistry
    void Register(ITransportRegistration registration)
    void Seal()
```

- The key is `(normalized canonical scheme, core TransportType)`.
- `Register` rejects a duplicate key with `ArgumentException`.
- `Seal` freezes the registry. `Register` after `Seal` throws
  `InvalidOperationException`; `Find` remains available.
- `Find` returns `null` for an unknown key.
- The registry is populated from the same core registrations that feed the
  formula registry; the bootstrap calls `Seal` when the factory is built.
- The resolved constructor metadata and the runtime registration must agree on
  `Name` and `Type`. The adapter normalizes the name before storing it.

If the optional `TransportTypeId` decoupling is adopted, the compiler also takes
`ITransportTypeBindings` and maps cast targets to core `TransportType`. Until
then, `ResolvedCast.Target` and `TransportConstructorInfo.Type` are already core
`TransportType` values and no binding layer is needed.

### 11.3 Core transport registration contract

The transport-facing contract lives in `Pontifex.Factory.Registration` and is
described normatively in `parsing.md` §6.9. Summary:

```text
ITransportRegistration
    string Name
    TransportType Type
    IReadOnlyList<TransportParameter> Parameters
    IReadOnlyList<string> Aliases
    ITransport Create(ITransportBuildContext context)

ITransportBuildContext
    TransportRole Role
    ILogger Logger
    IMemoryRental Memory
    bool TryGetString(string name, out string value)
    bool TryGetInt64(string name, out long value)
    bool TryGetDecimal(string name, out decimal value)
    bool TryGetTransport(string name, out NestedTransportFactory factory)
    bool TryGetTransportList(string name, out IReadOnlyList<NestedTransportFactory> factories)
    bool TryGetStruct(string name, out ITransportStructValue value)
```

`ITransportStructValue` also exposes `TryGetArray`; `ITransportArrayValue`
exposes `Count` and indexed `TryGetString`/`TryGetInt64`/`TryGetDecimal`.

Parameter DTOs:

- `StringParameter`
- `IntegerParameter`
- `DecimalParameter`
- `NestedTransportParameter`
- `TransportListParameter`
- `StructParameter`
- `TransportTypeSet` (`Any`, `Exactly`, `OneOf`)
- `SingleType`, `StringType`, `IntegerType`, `DecimalType`
- `StructSchema`, `StringField`, `IntegerField`, `DecimalField`, `ArrayField`,
  `NestedStructField`
- `ScalarValidator`

Rules:

- Parameter and field names are canonical lowercase.
- `TryGet*` returns `false` when an optional value is absent; required values are
  guaranteed by resolution.
- `TryGetTransport` returns a lazy factory from the stored `ResolvedExpr`. Each
  invocation compiles the corresponding resolved subtree into a fresh transport.
- `TryGetTransportList` preserves element order and returns one factory per
  element.
- Struct values expose only string fields, integer fields, decimal fields, array
  fields, and nested structs.
- The constructor owns every nested transport it creates; on failure it must
  stop or discard partially created transports before rethrowing.
- `Create` is called exactly once per resolved transport expression.

### 11.4 Compilation algorithm

```text
Compile(ResolvedFormula formula):
    if formula is null: ArgumentNullException
    if formula.Root is null:
        E_CMP_INCONSISTENT_TREE "unspecified invariant violation"
    return CompileExpr(formula.Root)

CompileExpr(expr):
    ResolvedTransport -> CompileTransport(expr)
    ResolvedCast      -> CompileCast(expr)
    other             -> E_CMP_INCONSISTENT_TREE "unspecified invariant violation"

CompileTransport(t):
    registration = constructors.Find(t.Constructor.Name, t.Constructor.Type)
    if registration is null:
        E_BLD_CONSTRUCTOR_NOT_FOUND
            "transport constructor '<scheme>' is not registered"
    context = TransportBuildContext(t, role, logger, memory, this)
    try:
        return registration.Create(context)
    catch CompilerException:
        rethrow
    catch exception:
        E_BLD_CONSTRUCTOR_FAILED
            "transport constructor '<scheme>' failed"
            inner = exception

CompileCast(c):
    inner = CompileExpr(c.Inner)
    if inner.Type == c.Target:
        return inner
    convert = converters.TryConvert(
        inner.Type, c.Target, () => inner, memory, logger)
    if convert is null:
        E_BLD_NO_CONVERSION_PATH
            "no conversion path from <from> to <to>"
    try:
        return convert()
    catch exception:
        E_BLD_CONSTRUCTOR_FAILED
            "transport constructor '<inner scheme>' failed"
            inner = exception
```

- Build errors use `BuildException` with phase `Building`.
- Compiler invariant errors use `CompilerException` with phase `Compilation`.
- Constructor and converter failures always preserve the original exception as
  `InnerException`.
- A nested factory invoked inside `Create` may throw its own `BuildException`;
  the outer constructor boundary wraps it as the parent constructor failure with
  that exception preserved as the inner exception.
- A `CompilerException` raised by a build-context accessor inside `Create` is a
  compiler invariant failure and propagates unchanged with phase `Compilation`;
  the constructor boundary does not relabel it as `E_BLD_CONSTRUCTOR_FAILED`.
- The compiler does not recover, retry, or partially succeed.
- The compiler is deterministic and has no static state.

### 11.5 Optional invariant checks

The resolver guarantees a valid tree. The compiler MAY perform cheap defensive
checks:

- null root;
- unknown resolved node kind;
- registration `Type` differs from `t.Constructor.Type`;
- created transport `Type` differs from the registration type.

Failures use `E_CMP_INCONSISTENT_TREE` with a detail from the closed set in
`parsing.md` §13.6. The compiler MUST NOT duplicate the full printer validation
pass.

### 11.6 Compilation tests

The `Compilation` Test Suit is implemented under
`Tests/Pontifex.Factory.Tests/Compilation/` with 54 tests. The shape and matrix
below are the coverage contract; keep them in sync when adding cases.

Suite shape:

```text
Compilation/
  README.md
  Coverage.md
  Harness/
    CompilationTestData.cs
    FakeTransport.cs
    RecordingConvertersGraph.cs
    RecordingRegistration.cs
  Tests/
    TransportCompilerTests.cs
    TransportConstructorRegistryTests.cs
```

- Suite type: Test Suit; root subject is the tightly coupled direct-compilation
  group (`TransportCompiler`, `TransportConstructorRegistry`, and the internal
  `TransportBuildContext`). One root layer.
- Suite root category: `Factory.Compilation`.
- Namespace: `Pontifex.Factory.Tests.Compilation`.
- Harness namespace: `Pontifex.Factory.Tests.Compilation.Harness`.
- Test names: `Method_Scenario_ExpectedResult`.
- No mocking library; hand-written doubles only.
- Core registration DTO construction is covered through the adapter mapping and
  the build-context accessor cases. A separate suite for the core DTOs is
  optional and outside this test project.

Required cases:

Registry:

- register and find by `(scheme, type)`;
- duplicate `(scheme, type)` throws;
- unknown pair returns `null`;
- registration with the same scheme and a different type is distinct;
- null registration is rejected;
- `Register` after `Seal` throws `InvalidOperationException`;
- registration after factory build is rejected through the bootstrap.

Compiler input and lookup:

- null formula throws `ArgumentNullException`;
- null root produces `E_CMP_INCONSISTENT_TREE`;
- unknown constructor produces `E_BLD_CONSTRUCTOR_NOT_FOUND` with phase
  `Building` and the exact message;
- registered constructor is called exactly once;
- returned transport is the constructor's result.

Build context:

- role, logger, and memory are visible to `Create`;
- string parameter value is passed decoded;
- integer and decimal parameter values are passed typed;
- absent optional string, integer, or decimal returns `false`;
- nested transport returns a lazy factory;
- invoking the nested factory creates a transport;
- invoking it twice creates two independent transports;
- transport list returns factories in source order;
- struct returns string, integer, and decimal fields, arrays, and nested structs;
- array fields expose `Count` and typed indexed accessors; empty arrays are valid;
- absent optional struct field returns `false`;
- parameter and field lookup is by canonical lowercase name;
- a wrong parameter kind reports `E_CMP_PARAM_KIND_MISMATCH` with resolved kinds
  (`string`, `integer`, `decimal`, `transport`, `transport list`, `struct`,
  `array`).

Failure paths:

- constructor throws -> `E_BLD_CONSTRUCTOR_FAILED`, phase `Building`, original
  exception preserved;
- nested constructor failure -> parent failure with the nested exception as the
  inner exception;
- converter throws -> `E_BLD_CONSTRUCTOR_FAILED` for the inner scheme;
- no conversion path -> `E_BLD_NO_CONVERSION_PATH` with core transport type
  names;
- unknown resolved node -> `E_CMP_INCONSISTENT_TREE`.

Casts:

- identity cast returns the inner transport without invoking the converter;
- changing cast invokes the converter;
- the converter receives the inner type, target type, a factory that returns the
  compiled inner transport, logger, and memory;

Bootstrap integration, covered in either this suite or the Bootstrap suite but
not both:

- `RegisterTransport(ITransportRegistration)` registers formula metadata and a
  runtime constructor from one call;
- a core-registered transport resolves and compiles through the facade;
- `CreateCompiler` returns a compiler with the requested role;
- a formula-only registration resolves but compilation reports
  `E_BLD_CONSTRUCTOR_NOT_FOUND`;
- the runtime registry is sealed after `Build`.

Acceptance criteria:

- every `E_BLD_*` and `E_CMP_*` compiler path is covered;
- all context value kinds are covered;
- lazy nested factory behaviour is covered;
- compilation is deterministic and leaves no static state;
- the suite follows `Tests/RULES.md`.

---

## 12. Bootstrap subsystem

### 12.1 Public contract

Runtime interfaces:

```text
Pontifex.Factory.ITransportFactory
Pontifex.Factory.ITransportFactoryBuilder
```

Public creation seam:

```text
Pontifex.Factory.TransportFactoryBootstrap.CreateBuilder(
    IConvertersGraph? converters = null) -> ITransportFactoryBuilder
```

`converters` defaults to `ConvertersGraph.Default`. The builder is the only
public way to configure a factory; concrete bootstrap classes stay internal.

Registration and compiler creation:

```text
ITransportFactoryBuilder
    IRegistryBuilder Registry { get; }
    void RegisterTransport(ITransportRegistration registration)
    ITransportFactory Build()

ITransportFactory
    FormulaResolveResult Resolve(string formula, TransportTypeSet? expected = null)
    string Canonicalize(string formula)
    ITransportCompiler CreateCompiler(
        TransportRole role, ILogger logger, IMemoryRental memory)
    TOutput Compile<TOutput>(
        string formula, ICompiler<TOutput> compiler, TransportTypeSet? expected = null)
```

- `RegisterTransport(ITransportRegistration)` is the preferred registration API
  for transports that will be compiled.
- `Registry.RegisterTransport(...)` remains available for metadata-only and
  formula-only tooling; such registrations resolve and print but report
  `E_BLD_CONSTRUCTOR_NOT_FOUND` if compiled.
- `CreateCompiler` binds the frozen runtime registry, the converter graph, the
  role, and the build environment. Callers never construct `TransportCompiler`
  or `TransportConstructorRegistry` themselves.
- `Compile<T>` remains the generic escape hatch for caller-supplied compilers.

Exception hierarchy in the formula assembly root namespace
(`Pontifex.TransportFormula`; the runtime factory assembly reuses it):

```text
FactoryException
RegistrationException
PrinterException
CompilerException
BuildException
```

### 12.2 Internal components

**`TransportFactoryBuilder`**

- Implements `ITransportFactoryBuilder`.
- Holds:
  - the formula `IRegistryBuilder`;
  - the runtime `TransportConstructorRegistry`;
  - the converter graph.
- `RegisterTransport(ITransportRegistration)`:
  1. adapts core metadata to formula `TransportRegistration`;
  2. normalizes the scheme name;
  3. calls `Registry.RegisterTransport(...)`;
  4. stores the core registration in the runtime registry under
     `(normalized name, Type)`.
- `Build()`:
  - freezes the formula registry;
  - seals the runtime registry;
  - creates parser, resolver, and canonicalizer;
  - returns `TransportFactory`;
  - is idempotent: repeated calls return the same factory.

**`TransportFactory`**

- Implements `ITransportFactory`.
- Holds the parser, resolver, canonicalizer, frozen runtime registry, converter
  graph, and no mutable state.
- `Resolve` and `Canonicalize` are unchanged from the formula pipeline.
- `CreateCompiler(role, logger, memory)` returns
  `new TransportCompiler(runtimeRegistry, converters, logger, memory, role)`.
- `Compile<T>` parses, resolves, and calls the caller-supplied compiler.
- `FactoryError` phase identifies the failing stage.

**`CoreRegistrationAdapter`**

- Internal.
- Maps `Pontifex.Factory.Registration` DTOs to formula `Param`,
  `TransportTypeSet`, `StructSchema`, `SingleType`, `StringType`, `ArrayField`,
  and `TransportRegistration`.
- Maps `StringType.Validate` per element and `ArrayField` count ranges; it does
  not re-validate integer, decimal, or array ranges, so the formula registry's
  `E_REG_*` codes remain reachable through the adapter.
- Maps core `Aliases` to formula `TransportRegistration.Aliases`; the
  `SuggestedAliases` hint list is left empty because the host explicitly
  registered them.
- Normalizes names only; it does not add validation beyond the formula
  registry's existing registration rules.
- Preserves declaration order.
- The core DTO and the formula DTO are both named `TransportRegistration`; use a
  namespace alias (for example `CoreRegistration`) inside the adapter.

**`FactoryComponentFactory`**

- Internal wiring helper.
- Creates parser, resolver, canonicalizer, and compiler from built components.
- Does not leak concrete instances publicly.

### 12.3 Exception and result policy

- Parser and resolver return result DTOs.
- `Resolve` returns a result DTO.
- `Canonicalize` and `Compile` throw `FactoryException` with the failing stage.
- Registration throws `RegistrationException`.
- Printing throws `PrinterException`.
- Compilation invariant errors throw `CompilerException`.
- Constructor and conversion failures throw `BuildException` with `E_BLD_*`.
- Legacy descriptor errors are not part of this path.

### 12.4 Bootstrap tests

Existing tests remain valid with updated paths. Add:

- `CreateBuilder` uses `ConvertersGraph.Default` when no graph is supplied;
- `CreateBuilder` uses an injected graph;
- `RegisterTransport(ITransportRegistration)` registers metadata and a runtime
  constructor;
- `RegisterTransport` normalizes the scheme;
- `RegisterTransport` maps core integer and decimal parameters and fields to
  formula metadata, and unsorted core bounds reach `E_REG_INVALID_*`;
- `RegisterTransport` maps array fields and their element types, including
  `StringType` validators, and unsorted array/element bounds reach
  `E_REG_INVALID_ARRAY_RANGE`/`E_REG_INVALID_INTEGER_RANGE`/
  `E_REG_INVALID_DECIMAL_RANGE`;
- formula-only registration resolves but compilation reports
  `E_BLD_CONSTRUCTOR_NOT_FOUND`;
- `CreateCompiler` returns the requested role;
- `CreateCompiler` uses the frozen runtime registry;
- `Build` seals both registries;
- two builders are independent;
- `Compile` through the facade uses the created compiler and returns a live
  transport;
- parse/resolve errors still prevent compiler invocation;
- compiler exceptions propagate unchanged;
- null compiler argument guard;
- null formula policy;
- `Build` twice returns the same factory.

### 12.5 Acceptance criteria

- A caller can resolve, canonicalize, create a compiler, and compile through
  `ITransportFactory`.
- Runtime components are obtainable only through interfaces or the documented
  public compiler/registry classes.
- Formula and runtime registries cannot drift because a core registration is
  the single source.
- No static registry or global state.
- All phases are represented correctly in `FactoryError`.

---

## 13. Cross-cutting error implementation

Implement a single error-catalogue module with one method per code:

```text
ErrorFactory.LexUnterminatedString(span)
ErrorFactory.SyntaxUnexpectedToken(expected, token)
ErrorFactory.SemanticUnknownScheme(scheme, span, known)
ErrorFactory.BuildConstructorNotFound(scheme)
ErrorFactory.BuildConstructorFailed(scheme)
ErrorFactory.BuildNoConversionPath(from, to)
ErrorFactory.CompilerInconsistentTree(detail)
ErrorFactory.CompilerDuplicateParam(name)
ErrorFactory.CompilerUnknownParam(name)
ErrorFactory.CompilerParamKindMismatch(name, expected, actual)
...
```

Rules:

- Messages are copied exactly from `parsing.md` §13.
- `E_BLD_*` errors use `BuildException`; `E_CMP_*` errors use
  `CompilerException`.
- `E_BLD_TYPE_NOT_BOUND` is added only if the optional `TransportTypeId`
  binding layer is adopted.
- The adapter-only `E_CMP_RESERVED_SCHEME` and `E_CMP_INVALID_CAST_TARGET` codes
  are removed from the compiler catalogue. `E_PRN_INVALID_CAST_TARGET` remains a
  printer error.
- Placeholders are formatted deterministically.
- Name lists are sorted.
- Parameter/field lists use declaration/schema order.
- Offsets use `SourceSpan.Start`.
- EOF offset is input length.
- Compiler and build errors have no source offset.
- No localized resources in this phase.
- No "did you mean" suggestions.
- No error recovery.

Tests must assert both code and offset for parser/resolver errors, and code,
phase, and message for compiler/build errors.

---

## 14. Testing strategy

### 14.1 Test project structure

The test project is not bare: `Parsing`, `Registry`, `Semantics`,
`Canonicalization`, `Compilation`, and `Bootstrap` suites exist. Before adding
tests, follow `Tests/RULES.md`. The target layout is one suite per subsystem:

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
    Harness/
    Tests/
  Bootstrap/
    README.md
    Coverage.md
    Tests/
```

Each subsystem directory is its own suite root, so its `README.md` and
`Coverage.md` are owned by that suite. The project root itself is not a suite
unless a project-level suite is explicitly defined.

The `Compilation` suite is implemented; keep its matrix in sync with §11.6 when
adding compiler cases.

If a suite is not warranted yet, use loose test files outside suite directories;
promote them when they become cohesive, as `Tests/RULES.md` requires.

### 14.2 Test categories

- **Conformance** — every valid/invalid example in `grammar.md` and
  `parsing.md`.
- **Unit** — lexer, parser, registry validation, binder, validators, printer.
- **Error catalogue** — every code, message template, and offset.
- **Property** — parser never crashes; spans in range; round-trip stability;
  `Print(Resolve(Parse(Print(t)))) == Print(t)` over valid resolved trees using
  span-insensitive structural equality; exact decimal parse/format round-trip.
- **Component isolation** — parsing tests with no registry; registry tests with
  no parser; printer tests with hand-built resolved trees.
- **Compiler** — direct `ITransport` compilation, context value access, lazy
  nested factories, casts, converter failures, and build/compiler error phases.
- **Registration adapter** — core `ITransportRegistration` to formula metadata
  mapping, including name normalization and declaration order.

### 14.3 Determinism

- No real time, sockets, files, or transports.
- No mocking library; use hand-written test doubles as required by
  `Tests/RULES.md`.
- Tests are order-independent and isolated.

### 14.4 Coverage expectations

Aim for:

- all grammar productions;
- all error codes;
- all parameter kinds, including integer, decimal, and array-field element kinds;
- all alias/type-set combinations;
- all canonicalization branches, including numeric scalars and arrays;
- all compiler value kinds and build-context accessors, including typed array
  element access;
- lazy nested transport factory behaviour;
- cast identity, conversion, and failure paths;
- all `E_BLD_*` and `E_CMP_*` compiler codes;
- all facade phases and registration paths.

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

### Milestone 5 — Compilation and project split

- Keep `ICompiler<T>` in the formula assembly.
- Rename the current `Pontifex.Factory` formula project to
  `Pontifex.TransportFormula` and update namespaces, solution entries, and test
  project references.
- Create the runtime `Pontifex.Factory` project referencing `Pontifex` and
  `Pontifex.TransportFormula`.
- Keep the core registration contract in `Pontifex.Factory.Registration`.
- Implement the runtime `TransportCompiler`, `ITransportCompiler`,
  `ITransportConstructorRegistry`, and `TransportConstructorRegistry`.
- Add `E_BLD_CONSTRUCTOR_NOT_FOUND`; keep `E_BLD_CONSTRUCTOR_FAILED` and
  `E_BLD_NO_CONVERSION_PATH`.
- Add the `Compilation` Test Suit with the matrix in §11.6.
- Build/test passes.
- **Status:** implemented and verified (54 Compilation tests; 863 total tests).

### Milestone 6 — Bootstrap integration

- Implement `CoreRegistrationAdapter` from core registrations to formula
  metadata.
- Extend `ITransportFactoryBuilder` with
  `RegisterTransport(ITransportRegistration)`.
- Seal both the formula registry and the runtime constructor registry on
  `Build`.
- Add `ITransportFactory.CreateCompiler(role, logger, memory)`.
- Wire the runtime compiler through `FactoryComponentFactory`.
- Update the Bootstrap suite for registration, compiler creation, and compile
  integration.
- Build/test passes.
- **Status:** implemented and verified (Full Factory suite covers typed scalars,
  exact decimals, generic lists, and arrays).

### Milestone 7 — Conformance and hardening

- Run the full error catalogue.
- Add property tests.
- Verify no forbidden dependencies with project references and code review.
- Verify `dotnet build` and `dotnet test` clean.
- Update `README.md`/`Coverage.md` per `Tests/RULES.md`.
- **Status:** implemented; the error catalogue, exact decimal cases, array
  first-error order, and round-trip properties are covered.

---

## 16. Definition of done

- `grammar.md` and `parsing.md` are implemented without deviations.
- The scaffold-to-spec reconciliation in §3.1 is complete, including AST offsets,
  `SourceSpan.End`, `ResolvedExpr.Type`, catalog canonical-name lookup,
  `AllSchemeNames`, and type-set helpers.
- The formula/runtime split in §4 is complete.
- All formula public namespaces contain only interfaces and DTOs.
- No public implementation class exists in a formula subsystem namespace.
- Parsing has no registry dependency.
- Registry has no parsing/semantics dependency.
- Semantics is the only syntax/metadata join.
- Canonicalization has no parser dependency.
- Formula compilation contains only the generic `ICompiler<T>` and references no
  transport, converter, logging, or memory types.
- Runtime compilation is the only place that builds live transports.
- No `IDescription`, `IElement`, or `Pontifex.OldFactory` reference exists in the
  formula assembly or the runtime compiler.
- Transport packages can register through
  `Pontifex.Factory.Registration.ITransportRegistration` while referencing only
  `Pontifex`.
- All error codes from `parsing.md` that a resolver-produced tree can reach are
  implemented and tested, including `E_BLD_CONSTRUCTOR_NOT_FOUND`,
  `E_BLD_CONSTRUCTOR_FAILED`, and `E_BLD_NO_CONVERSION_PATH`. The catalogued
  but unimplemented compiler codes `E_CMP_DUPLICATE_PARAM` and
  `E_CMP_UNKNOWN_PARAM` are not emitted for any tree; `Compilation/Coverage.md`
  records that deferral.
- The `Compilation` suite covers registry lookup, build-context accessors, lazy
  nested factories, casts, conversion failures, and compiler invariants.
- Canonical round-trip properties pass, using span-insensitive structural
  equality; decimal values compare numerically and arrays compare element-wise.
- Metadata supports integer and decimal scalar parameters/fields and homogeneous
  string, integer, and decimal arrays inside struct fields.
- Decimal literals are parsed exactly; overflow, underflow, and precision loss
  are parse failures; canonical decimal output has no exponent and trims trailing
  zeros.
- No `ArrayParam`, nested arrays, or `StructType` exists.
- No implementation limits are introduced.
- No `transport://`, comments, aliases in canonical output, default synthesis,
  schema overload, or converter tie-breaking exists.
- All subsystems remain independently usable; the public creation seam is the
  only way to obtain a builder, and bootstrap implementation types are internal.
- `ITransportFactory.Resolve` returns result DTOs; `Canonicalize` and `Compile`
  throw `FactoryException` with the failing stage's phase.
- `dotnet build` and `dotnet test` pass with zero warnings/errors.
- Test suites follow `Tests/RULES.md`.

---

## 17. Risks and open decisions

1. **Project split timing.** The split is required before the compiler can live
   in a transport-free formula assembly. The minimum-churn alternative is to keep
   one project temporarily, but that makes the formula package reference
   `ITransport`, converters, logging, and memory. If the split is deferred, record
   it explicitly as a temporary exception rather than silently widening the
   formula project's dependencies.
2. **Type identifiers.** The formula layer currently uses the core
   `TransportType` enum. The optional `TransportTypeId` decoupling lets the
   formula assembly drop its `Pontifex` reference but adds
   `ITransportTypeBindings` and `E_BLD_TYPE_NOT_BOUND` to the compiler. Decide
   before implementing the Compilation suite, because the binding layer changes
   the compiler constructor and several tests.
3. **Compiler creation seam.** The plan uses
   `ITransportFactory.CreateCompiler(role, logger, memory)`. An alternative is to
   expose convenience `BuildServer`/`BuildClient` methods, or to let callers
   construct `TransportCompiler` directly. Keep one seam only; duplicating them
   makes lifecycle and registry sealing harder to test.
4. **Converter graph configuration.** The plan defaults to
   `ConvertersGraph.Default` and allows an injected graph through
   `CreateBuilder`. Decide whether the graph is frozen at builder creation or
   passed to `CreateCompiler`; do not make it mutable after `Build`.
5. **Exception hierarchy placement.** The hierarchy lives in the formula
   assembly root namespace. If the team wants zero public classes outside
   interfaces, replace exceptions with result DTOs or keep the hierarchy
   internal and expose only `FactoryError`.
6. **`FormulaToken` visibility.** It is currently public. Keep it public for
   parser tooling and diagnostics, or make it internal and expose only
   AST/errors.
7. **Legacy retirement.** `Pontifex.OldFactory`, `IDescription`, `IElement`, and
   the old `TransportBuilder` must remain untouched during this phase. Plan their
   retirement only after the new compiler and transport registrations are
   proven.
8. **No limits vs stack depth.** The language defines no limits. The parser must
   not impose artificial ones; if recursive descent risks stack exhaustion on
   adversarial input, use an explicit stack or document the implementation
   strategy without adding a language limit.
9. **Span retention.** Resolved DTOs retain `SourceSpan`. Keep this for
   diagnostics and tests; it is not printed.
10. **Registration source of truth.** The facade must derive formula metadata
    and the runtime constructor registry from one core registration. Do not add a
    second public registration API that can create a formula-only entry silently
    expecting compilation to work.

---

## 18. Handoff checklist

**Status:** Steps 1-3 are implemented and verified. Start at Step 4.

Execute in this order. Each step must leave `dotnet build` and the existing
`dotnet test` suites green before the next step starts.

### Step 1 — Split the projects (done)

- Rename the current `Pontifex.Factory` formula project to
  `Pontifex.TransportFormula`.
- Rename its root namespace and subsystem namespaces to
  `Pontifex.TransportFormula.*`.
- Keep `ICompiler<T>` in `Pontifex.TransportFormula.Compilation`.
- Move `grammar.md`, `parsing.md`, and `implementation-plan.md` with the formula
  project; update links in every suite README.
- Create the runtime `Pontifex.Factory` project with references to `Pontifex` and
  `Pontifex.TransportFormula`.
- Add `InternalsVisibleTo` for `Pontifex.Factory.Tests` to both assemblies.
- Update `Pontifex.sln` and the test project references.
- Verify the formula assembly has no `ITransport`, `ILogger`, `IMemoryRental`,
  `IConvertersGraph`, or `IDescription` reference.

### Step 2 — Add the runtime compiler (done)

- Add `ITransportCompiler`, `TransportCompiler`,
  `ITransportConstructorRegistry`, and `TransportConstructorRegistry`.
- Implement the internal `TransportBuildContext` over `ResolvedTransport` and
  `ResolvedValue`.
- Add `BuildException` factories for `E_BLD_CONSTRUCTOR_NOT_FOUND` and keep the
  existing `E_BLD_CONSTRUCTOR_FAILED` and `E_BLD_NO_CONVERSION_PATH`.
- Keep null-root and unknown-node checks on `E_CMP_INCONSISTENT_TREE`.
- Make the constructor registry immutable after the factory is built.

### Step 3 — Create the Compilation suite (done)

- Create `Tests/Pontifex.Factory.Tests/Compilation/` with root `README.md`,
  root `Coverage.md`, `Harness/`, and `Tests/` in one change.
- Add `FakeTransport`, `RecordingConvertersGraph`, and the resolved-tree data
  helpers.
- Implement the matrix in §11.6.
- Use category `Factory.Compilation` and namespace
  `Pontifex.Factory.Tests.Compilation`.
- Update `Coverage.md` in the same change.

### Step 4 — Integrate the bootstrap (done)

- Add `ITransportFactoryBuilder.RegisterTransport(ITransportRegistration)`.
- Add the internal `CoreRegistrationAdapter`.
- Seal both registries in `Build`.
- Add `ITransportFactory.CreateCompiler(role, logger, memory)`.
- Wire the runtime registry and converter graph through
  `FactoryComponentFactory`.
- Extend the Bootstrap suite with the integration cases in §12.4.

### Step 5 — Prove one transport end to end (next)

- Migrate one small transport registration first, preferably a direct or TCP
  constructor, to `ITransportRegistration`.
- Register it through the facade and compile a real formula into an `ITransport`.
- Record any contract gap found here before migrating the remaining transports.
- Do not delete the legacy constructor or URI parser yet.

### Step 6 — Documentation and hardening

- Update all suite links after the project move.
- Run the full error catalogue for `E_BLD_*` and `E_CMP_*`.
- Verify no formula assembly reference leaks into the runtime compiler tests.
- Confirm the definition of done in §16.
- Update `nonapproved-behaviour.md` with any new test-pinned assumptions.
