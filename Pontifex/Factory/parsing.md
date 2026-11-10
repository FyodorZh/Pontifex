# Transport Formula Parser and Canonical Printer

## 1. Scope and terminology

This document defines the **normative behavior** of the Transport Formula parser,
resolver, compiler boundary, and canonical printer. Syntax is defined in
`grammar.md`; this document assumes it.

Terms used below:

- **Transport Formula** (or **formula**) — the source expression described by
  `grammar.md`. It denotes how to construct a transport.
- **Input** — the formula text.
- **Token** — a lexical unit produced from the input.
- **AST** — the syntax tree produced by the parser. It contains no semantic
  information.
- **Resolved tree** — the typed, metadata-bound tree produced by the resolver.
- **Compiler** — an opaque consumer of a resolved tree. It may produce
  `IDescription`, a typed builder tree, or anything else. The parser and printer
  do not depend on the compiler.
- **Canonical printer** — a function that turns a resolved tree into a canonical
  formula.
- **MUST**, **MUST NOT**, **SHOULD** are normative.

All offsets are **0-based UTF-16 code-unit offsets** from the start of the input.
The end-of-input offset is the input length in UTF-16 code units.

The grammar defines no implementation limits: no maximum input length, nesting
depth, list length, or argument count. Implementations MUST NOT impose
language-level limits. If an implementation imposes a resource limit outside this
specification, it must report an implementation-defined error; such an error is
not part of this document.

---

## 2. Processing pipeline

```text
source
  │
  ├─ 1. Lexing            → tokens (lexical errors)
  ├─ 2. Parsing           → AST (syntax errors)
  ├─ 3. Resolution        → resolved tree (semantic errors)
  ├─ 4. Compiler          → opaque output (compiler/build errors)
  └─ 5. Builder           → live transports (build errors)

resolved tree
  └─ Canonical printer    → canonical formula
```

Phase order is strict:

1. **Lex the entire input.** The first lexical error aborts the parse.
2. **Parse the token stream.** The first syntax error aborts the parse.
3. **Resolve the AST.** Resolution starts only after the entire input is
   syntactically valid. The first semantic error aborts resolution.
4. **Compile** the resolved tree (optional, outside this document).
5. **Build** transports (outside this document).

The parser and printer are independent of the compiler and of any concrete
transport implementation. The grammar is transport-agnostic.

---

## 3. Lexing

### 3.1 Tokens

The token kinds are:

```text
Word, QuotedString, LParen, RParen, LBracket, RBracket,
LBrace, RBrace, Comma, Equals, EOF
```

Punctuation characters map one-to-one:

```text
(  LParen      )  RParen
[  LBracket    ]  RBracket
{  LBrace      }  RBrace
,  Comma       =  Equals
```

Whitespace is skipped and never appears in a token. The whitespace characters
are `SP`, `HTAB`, `CR`, and `LF` (see `grammar.md`).

### 3.2 Word tokens

A `Word` starts at the first non-whitespace character that is not `"` and is not
punctuation, and ends immediately before the next whitespace, punctuation, or
`"`.

- A `Word` is non-empty by construction.
- A `Word` may contain Unicode scalar values.
- A `Word` is an **identifier** iff it matches:

  ```text
  [A-Za-z][A-Za-z0-9_-]*
  ```

- Identifier comparison is ASCII case-insensitive. The canonical spelling of an
  identifier is its lowercase form.
- Unpaired UTF-16 surrogates are invalid. A lexer must report `E_LEX_INVALID_UNICODE`
  at the first offending code unit.

### 3.3 Quoted strings

On `"`:

1. Record the opening quote offset.
2. Decode until the matching `"`. At each code unit, apply these rules in order:
   a. Unpaired surrogate → `E_LEX_INVALID_UNICODE` at that code unit.
   b. Raw `CR` or `LF` → `E_LEX_RAW_NEWLINE_IN_STRING` at that code unit.
   c. `"` → end of the token.
   d. `\`:
      - If EOF follows, report `E_LEX_UNTERMINATED_STRING` at the opening quote.
      - If the next code unit is `"` or `\`, consume it and append the decoded
        character.
      - Otherwise report `E_LEX_INVALID_ESCAPE` at the backslash. `<sequence>`
        is the backslash plus the following source code unit, rendered with the
        escaping rules from §3.4.
   e. Any other code unit is appended to the decoded value.
3. EOF before the closing quote → `E_LEX_UNTERMINATED_STRING` at the opening
   quote.
4. The token's decoded text is the content without the surrounding quotes.

A `QuotedString` token preserves its raw source slice for diagnostics.

### 3.4 Token display

For deterministic error messages, define `displayToken(token)`:

- `EOF` → `<end of input>`.
- Punctuation → `'<char>'`.
- `Word` → `'<escaped text>'`.
- `QuotedString` → `'<escaped decoded text>'`.

Escaping used inside the quotes:

```text
\  →  \\
'  →  \'
"  →  \"
CR →  \r
LF →  \n
HTAB → \t
other control code unit → \uXXXX
```

If the escaped text is longer than 40 UTF-16 code units, truncate it to the first
37 code units and append `...`.

Lists of expected tokens or names are joined with ` or ` in the order defined by
the relevant rule. Sets of names are sorted lexicographically for determinism.

---

## 4. AST

The AST represents syntax only. It contains no schemes, parameter bindings,
types, or validation results.

```text
SourceSpan        { int Start; int Length; int End => Start + Length; }

AstValue          (abstract)  { SourceSpan Span; }
AstExpr           : AstValue   { AstTransportCall Call; AstCast? Cast; }
AstList           : AstValue   { IReadOnlyList<AstExpr> Elements;
                                 int OpenOffset; int CloseOffset; }
AstStruct         : AstValue   { IReadOnlyList<AstField> Fields;
                                 int OpenOffset; int CloseBraceOffset; }
AstScalar         : AstValue   { string Text; bool WasQuoted; }

AstTransportCall  { string Scheme; Token SchemeToken;
                    IReadOnlyList<AstArg> Args; int CloseParenOffset;
                    SourceSpan Span; }

AstCast           { string TypeName; Token TypeToken; SourceSpan Span; }

AstArg            { string? Name; Token? NameToken; AstValue Value;
                    SourceSpan Span; }

AstField          { string Name; Token NameToken; AstValue Value;
                    SourceSpan Span; }
```

AST invariants:

- `AstExpr.Cast` is `null` or one cast; there is no chain.
- `AstArg.Name` is `null` for positional arguments and non-null for named
  arguments. Named arguments form a suffix; the parser enforces this.
- `AstList.Elements` contains only `AstExpr`; never scalars, structs, or nested
  lists.
- `AstStruct.Fields` are named; field names are identifier tokens.
- No AST node contains resolved types, constructors, or validators.
- `Span` covers the full source range of the node and is used for semantic
  errors. Every `AstValue` node carries a span. `AstList` also records the
  opening and closing bracket offsets; `AstStruct` records the opening brace and
  the closing brace offsets.
- Scheme, argument-name, and field-name strings are stored exactly as written;
  normalization happens during resolution. The corresponding tokens are retained
  for diagnostics.

---

## 5. Parser

The parser consumes the token stream produced in §3 and produces an `AstExpr`.
It MUST reject every input that does not match the grammar in `grammar.md`.

### 5.1 Top level

```text
Parse(input):
    tokens = Lex(input)                 # lexical errors abort
    if tokens.current is EOF:
        error E_SYN_EMPTY_INPUT at offset 0
    if tokens.current is not Word or not IsIdentifier(current.Text):
        error E_SYN_UNEXPECTED_TOKEN
              expected "a transport expression"
              at current.Offset
    expr = ParseExpr()
    if tokens.current is not EOF:
        error E_SYN_TRAILING_INPUT at tokens.current.Offset
    return expr
```

The top level is exactly one expression. A top-level list, struct, scalar, or
quoted string is not an expression and is rejected as shown above.

`Parse` performs syntax only and returns `AstExpr`. It does not take a registry or
catalog and does not resolve anything.

### 5.2 Expressions and casts

```text
ParseExpr():
    call = ParseTransportCall()
    if tokens.current is Word equal to "as" (case-insensitive):
        asToken = consume()
        typeToken = ParseTypeName()
        if tokens.current is Word equal to "as" (case-insensitive):
            error E_SYN_CHAINED_CAST at tokens.current.Offset
        cast = AstCast(typeToken.Text, typeToken,
                       SpanFrom(call.Span.Start, typeToken.End))
        return AstExpr(call, cast, SpanFrom(call.Span.Start, typeToken.End))
    return AstExpr(call, null, call.Span)
```

`as` is contextual. A `Word` whose text is `as` but which is not in the position
immediately after a transport call is an ordinary word.

### 5.3 Transport calls

```text
ParseTransportCall():
    if tokens.current is not Word or not IsIdentifier(current.Text):
        error E_SYN_UNEXPECTED_TOKEN
              expected "a transport scheme identifier"
              at current.Offset
    schemeToken = consume()
    if tokens.current is not LParen:
        error E_SYN_UNEXPECTED_TOKEN expected "'('" at current.Offset
    consume LParen
    args, closeParen = ParseArgs()
    return AstTransportCall(schemeToken.Text, schemeToken, args,
                            closeParen.Offset,
                            SpanFrom(schemeToken.Start, closeParen.End))
```

### 5.4 Arguments

`ParseArgs` is called after `(` and consumes the closing `)`.

```text
ParseArgs():
    args = []
    seenNamed = false

    if tokens.current is RParen:
        close = consume()
        return [], close

    if tokens.current is not Word
       and not QuotedString
       and not LBracket
       and not LBrace:
        error E_SYN_UNEXPECTED_TOKEN
              expected "a value or ')'"
              at tokens.current.Offset

    loop:
        if tokens.current is not Word
           and not QuotedString
           and not LBracket
           and not LBrace:
            error E_SYN_UNEXPECTED_TOKEN
                  expected "a value"
                  at tokens.current.Offset

        if tokens.current is Word and tokens.peek(1) is Equals:
            if not IsIdentifier(current.Text):
                error E_SYN_UNEXPECTED_TOKEN
                      expected "an identifier"
                      at current.Offset
            nameToken = consume()
            consume Equals
            value = ParseValue()
            args.add(AstArg(nameToken.Text, nameToken, value,
                            SpanFrom(nameToken.Start, value.Span.End)))
            seenNamed = true
        else:
            if seenNamed:
                error E_SYN_POSITIONAL_AFTER_NAMED at current.Offset
            value = ParseValue()
            args.add(AstArg(null, null, value, value.Span))

        if tokens.current is Comma:
            comma = consume()
            if tokens.current is RParen:
                error E_SYN_TRAILING_COMMA context "argument list"
                      at comma.Offset
            continue

        if tokens.current is RParen:
            close = consume()
            return args, close

        error E_SYN_UNEXPECTED_TOKEN expected "',' or ')'" at current.Offset
```

Consequences:

- `f()` has zero arguments.
- `f(a)` and `f(a, b)` are positional.
- `f(a=1)` and `f(a=1, b=2)` are named.
- `f(a, b=1)` is a positional prefix followed by named.
- `f(a=1, b)` is rejected with `E_SYN_POSITIONAL_AFTER_NAMED`.
- A trailing comma is rejected; it is never ignored.

### 5.5 Values

```text
ParseValue():
    switch tokens.current.Kind:
        QuotedString:
            token = consume()
            return AstScalar(token.Text, WasQuoted = true,
                             SpanFrom(token.Start, token.End))
        LBracket:
            return ParseList()
        LBrace:
            return ParseStruct()
        Word:
            if tokens.peek(1) is LParen:
                return ParseExpr()
            token = consume()
            return AstScalar(token.Text, WasQuoted = false,
                             SpanFrom(token.Start, token.End))
        default:
            error E_SYN_UNEXPECTED_TOKEN expected "a value" at current.Offset
```

A `Word` followed by `(` is a transport expression. A `Word` not followed by `(`
is a bare scalar. This is the only shape decision; it does not consult the
registry.

### 5.6 Lists

```text
ParseList():
    open = consume LBracket
    if tokens.current is RBracket:
        close = consume()
        return AstList([], open.Offset, close.Offset,
                       SpanFrom(open.Start, close.End))

    elements = []
    loop:
        if tokens.current is LBracket:
            error E_SYN_NESTED_LIST at current.Offset
        if tokens.current is not Word or not IsIdentifier(current.Text):
            error E_SYN_UNEXPECTED_TOKEN
                  expected "a transport expression"
                  at current.Offset
        elements.add(ParseExpr())

        if tokens.current is Comma:
            comma = consume()
            if tokens.current is RBracket:
                error E_SYN_TRAILING_COMMA context "transport list"
                      at comma.Offset
            continue

        if tokens.current is RBracket:
            close = consume()
            return AstList(elements, open.Offset, close.Offset,
                           SpanFrom(open.Start, close.End))

        error E_SYN_UNEXPECTED_TOKEN expected "',' or ']'" at current.Offset
```

List elements are expressions. Scalars, structs, and nested lists are rejected
syntactically.

### 5.7 Structs

```text
ParseStruct():
    open = consume LBrace
    if tokens.current is RBrace:
        close = consume()
        return AstStruct([], open.Offset, close.Offset,
                         SpanFrom(open.Start, close.End))

    fields = []
    loop:
        if tokens.current is not Word or not IsIdentifier(current.Text):
            error E_SYN_UNEXPECTED_TOKEN
                  expected "a struct field name"
                  at current.Offset
        nameToken = consume()

        if tokens.current is not Equals:
            error E_SYN_UNEXPECTED_TOKEN expected "'='" at current.Offset
        consume Equals

        value = ParseValue()
        fields.add(AstField(nameToken.Text, nameToken, value,
                            SpanFrom(nameToken.Start, value.Span.End)))

        if tokens.current is Comma:
            comma = consume()
            if tokens.current is RBrace:
                error E_SYN_TRAILING_COMMA context "struct"
                      at comma.Offset
            continue

        if tokens.current is RBrace:
            close = consume()
            return AstStruct(fields, open.Offset, close.Offset,
                             SpanFrom(open.Start, close.End))

        error E_SYN_UNEXPECTED_TOKEN expected "',' or '}'" at current.Offset
```

Struct fields are named; there are no positional fields. The grammar permits any
`value` shape here; semantic struct schemas restrict the allowed shapes (§7.8).

### 5.8 Type names

```text
ParseTypeName():
    if tokens.current is not Word or not IsIdentifier(current.Text):
        error E_SYN_UNEXPECTED_TOKEN
              expected "a transport type name"
              at current.Offset
    return consume()
```

Type-name validity is semantic (§7.7).

### 5.9 Syntax error precedence

The parser fails at the first syntax error it encounters while scanning
left-to-right. `E_SYN_UNEXPECTED_TOKEN` uses the expected-string contexts in the
table in §13.2. No error recovery is attempted.

---

## 6. Registry and metadata

### 6.1 Constructors

```csharp
interface ITransportConstructor
{
    string Name { get; }                    // canonical scheme
    TransportType Type { get; }
    IReadOnlyList<Param> Params { get; }    // declaration order
    IReadOnlyList<string> SuggestedAliases { get; } // optional hints
}
```

- `Name` MUST be an identifier and is normalized to lowercase.
- Canonical `Name` values MUST be unique within a registry.
- `Params` MAY be empty. Parameter names MUST be identifiers, normalized to
  lowercase, and unique within the constructor.
- `Type` is the fixed transport type produced by the constructor. Every
  constructor has one fixed type.

### 6.2 Parameters

```csharp
abstract class Param
{
    string Name;          // lowercase identifier, unique
    bool Required;        // must be provided
    bool Positional;      // may be filled by a positional argument
}

sealed class StringParam : Param
{
    ScalarValidator? Validate;
}

sealed class TransportParam : Param
{
    TransportTypeSet Expected;
}

sealed class TransportListParam : Param
{
    TransportTypeSet Expected;
    bool Homogeneous;     // all elements must share one resolved type
    int Min;
    int? Max;             // null = unbounded
}

sealed class StructParam : Param
{
    StructSchema Schema;
}
```

There are no default values. If a parameter is absent and `Required` is false, the
constructor receives no value for it; the constructor applies its own defaults
later. The parser never synthesizes values.

`Required` and `Positional` are independent.

### 6.3 Type sets

```csharp
readonly struct TransportTypeSet
{
    static TransportTypeSet Any;
    static TransportTypeSet Exactly(TransportType type);
    static TransportTypeSet OneOf(params TransportType[] types);
    bool Contains(TransportType type);
}
```

- `Any` contains every transport type.
- `Exactly(T)` contains only `T`.
- `OneOf(...)` MUST contain at least one type; duplicates are normalized away.
- `OneOf()` with zero elements is a registration error.

### 6.4 Validators

```csharp
delegate string? ScalarValidator(string value);
```

- Input is the decoded scalar text (quotes removed, escapes resolved).
- Return `null` for valid.
- Return a non-null, non-empty error fragment for invalid.
- The parser adds parameter/field and offset context.
- Validators MUST be deterministic, side-effect-free, and thread-safe.
- Validators MUST NOT throw. If one throws, the parser reports
  `E_SEM_VALIDATOR_FAILED` with reason `validation failed`.
- If a validator returns an empty string, the parser treats it as invalid and
  uses the reason `validation failed`.
- A throwing struct-field validator uses the field form of the message:
  `field '<path>' of parameter '<param>' is invalid: validation failed`,
  where `<path>` is the field path defined in §7.8.

### 6.5 Struct schemas

```csharp
sealed class StructSchema
{
    IReadOnlyList<StructField> Fields;   // schema order
}

abstract class StructField
{
    string Name;          // lowercase identifier, unique
    bool Required;
}

sealed class StringField : StructField
{
    ScalarValidator? Validate;
}

sealed class NestedStructField : StructField
{
    StructSchema Schema;
}
```

Struct fields are named-only. There are no positional fields, no defaults, and no
transport or list fields.

### 6.6 Type catalog

The resolver uses a transport-type catalog:

```csharp
interface ITransportTypeCatalog
{
    bool TryResolve(string name, out TransportType type, out string canonicalName);
    string GetCanonicalName(TransportType type);
}
```

- Lookup is ASCII case-insensitive.
- `canonicalName` is the exact spelling printed by the canonical printer.
- Catalog names MUST be identifiers and MUST be unique case-insensitively.
- The grammar does not enumerate concrete types; the catalog is configuration.

### 6.7 Registration

```csharp
RegisterTransport(ITransportConstructor constructor,
                  IEnumerable<string>? aliases = null);
```

Rules:

1. Normalize the canonical name and all aliases to lowercase.
2. Validate canonical name, parameter names, alias names, and struct field names
   as identifiers.
3. Reject duplicate canonical names.
4. Reject duplicate parameter names within a constructor.
5. Normalize `TransportTypeSet` values; reject empty `OneOf`.
6. Validate `TransportListParam`: `Min >= 0`; `Max == null` or `Max >= Min`.
7. Validate struct schemas: unique field names, valid identifiers, supported
   field kinds.
8. Normalize aliases; duplicates within the same registration are ignored. An
   alias equal to the constructor's own canonical name is ignored.
9. This rule applies only to alias-to-constructor mappings registered for the
   same alias name. The canonical constructor of a scheme is not an alias mapping.
   An alias may therefore target a constructor whose `Type` equals the canonical
   constructor's `Type` for the same scheme; canonical preference resolves that
   overlap at resolution. An alias name may not be registered twice for the same
   `Type` by different constructors.
10. `SuggestedAliases` are not automatically registered. A host MAY pass them (or
    any other aliases) to `RegisterTransport`.

Checks run in the numbered order above, and the first failure is reported.
Within rule 2, check the canonical name first, then parameter names in declaration
order, then alias names in input order, then struct field names recursively in
schema order. Within rule 7, visit fields in schema order and validate the name
as an identifier before uniqueness and kind. Registration errors are listed in
§13.4. They occur when the registry is built, not while parsing.

### 6.8 Registry lookup

```csharp
interface ITransportRegistry
{
    ITransportConstructor? Canonical(string normalizedScheme);
    IReadOnlyList<ITransportConstructor> Aliases(string normalizedScheme);
    void RegisterTransport(...);
}
```

- `Canonical(scheme)` returns the constructor whose canonical `Name` equals the
  normalized scheme, or `null`.
- `Aliases(scheme)` returns all constructors that registered the normalized
  scheme as an alias. The canonical constructor is not part of this result.
- Both methods require an already-normalized lowercase scheme.
- `constructor.Params.Find(normalizedName)` is a case-insensitive lookup over
  normalized parameter names and returns `null` when no parameter matches.

---

## 7. Resolution

Resolution consumes a syntactically valid AST, registry metadata, and the type
catalog, and produces a resolved tree. It performs all semantic checks.

Entry points:

```text
Parse(input)                                  -> AstExpr
Resolve(ast, registry, catalog)               -> ResolvedExpr
ParseAndResolve(input, registry, catalog)     -> ResolvedExpr
```

`ParseAndResolve` is exactly `Resolve(Parse(input), registry, catalog)`.

### 7.1 Expected types

Every expression is resolved with an **expected type set**:

- At top level: `TransportTypeSet.Any`.
- For a `TransportParam`: the parameter's `Expected`.
- For each element of a `TransportListParam`: the parameter's `Expected`.
- For the inner transport of a cast: `TransportTypeSet.Any`.
- Struct fields have no transport expected type.

### 7.2 Expressions and casts

```text
ResolveExpr(ast, expected):
    if ast.Cast is null:
        return ResolveTransport(ast.Call, expected)

    if not catalog.TryResolve(ast.Cast.TypeName, out target, out canonicalName):
        error E_SEM_UNKNOWN_TRANSPORT_TYPE at ast.Cast.TypeToken.Offset

    if not expected.Contains(target):
        error E_SEM_CAST_TYPE_MISMATCH at ast.Cast.TypeToken.Offset
              target = target, expected = expected

    inner = ResolveTransport(ast.Call, TransportTypeSet.Any)

    if inner.Type == target:
        return inner                         # identity cast collapses

    return ResolvedCast(inner, target, span(ast))
```

Consequences:

- A cast never selects a transport variant. `alpha(...) as TypeB` resolves
  `alpha` with `Any`, then converts its result to `TypeB`.
- To select a variant, use a canonical scheme or an alias that resolves under the
  expected type.
- Identity casts are removed from the resolved tree.
- Chained casts are a syntax error and never reach resolution.

### 7.3 Transport resolution

```text
ResolveTransport(call, expected):
    scheme = normalize(call.Scheme)
    canonical = registry.Canonical(scheme)
    allCandidates = (canonical is null ? [] : [canonical])
                    ∪ registry.Aliases(scheme)

    if allCandidates is empty:
        error E_SEM_UNKNOWN_SCHEME at call.SchemeToken.Offset

    candidates = allCandidates
    if expected != Any:
        candidates = allCandidates.Where(c => expected.Contains(c.Type))
        if candidates is empty:
            error E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE
                  at call.SchemeToken.Offset
                  available = FormatVariants(allCandidates)

    if canonical is not null and candidates.Contains(canonical):
        candidates = { canonical }

    if candidates.Count > 1:
        error E_SEM_AMBIGUOUS_SCHEME at call.SchemeToken.Offset

    constructor = candidates.Single()
    bind arguments (§7.4)
    validate parameters in declaration order (§7.5)
    return ResolvedTransport(constructor, constructor.Type, provided, span(call))
```

Rules:

- The expected set filters candidates first. This is what makes aliases
  context-sensitive.
- Canonical preference applies after filtering, for any expected set: if the
  scheme has a canonical constructor and that constructor survived filtering, it
  wins even if aliases also survived.
- If no canonical candidate survived, one remaining candidate is selected; more
  than one is ambiguous.
- An alias-only scheme with `Any` and one candidate resolves to that candidate.
- An alias-only scheme with `Any` and multiple candidates is ambiguous.
- `E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE` reports `available` from `allCandidates`,
  the set before expected-type filtering.
- Within one alias group, all candidate types are distinct. A canonical
  constructor and an alias of the same type may still share a scheme name; the
  canonical-preference rule above resolves that overlap deterministically.

### 7.4 Argument binding

```text
positionalParams = constructor.Params.Where(p => p.Positional).ToList()
positionalArgs   = call.Args.Where(a => a.Name is null).ToList()

if positionalArgs.Count > positionalParams.Count:
    error E_SEM_TOO_MANY_POSITIONAL
          at positionalArgs[positionalParams.Count].Value.Span.Start

bound = dictionary<Param, AstValue>

for i in 0 .. positionalArgs.Count-1:
    bound[positionalParams[i]] = positionalArgs[i].Value

for each named arg in source order:
    name = normalize(arg.Name)
    param = constructor.Params.Find(name)
    if param is null:
        error E_SEM_UNKNOWN_PARAM at arg.NameToken.Offset
    if bound.Contains(param):
        error E_SEM_DUPLICATE_PARAM at arg.NameToken.Offset
    bound[param] = arg.Value

for each param in constructor.Params in declaration order:
    if not bound and param.Required:
        error E_SEM_MISSING_REQUIRED_PARAM
              at call.CloseParenOffset

provided = constructor.Params
    .Where(p => bound.Contains(p))
    .Select(p => ParamBinding(p, bound[p]))
    .ToList()
```

Named arguments may target positional and named-only parameters. Positional
arguments fill only parameters with `Positional = true`, in declaration order. A
named argument that targets a parameter already filled positionally is a
duplicate. `provided` is exactly the supplied parameters, in declaration order;
it is stored as `ResolvedTransport.Provided`.

### 7.5 Parameter validation

Parameters are validated in **declaration order**, and only bound parameters are
visited.

```text
for each param in constructor.Params in declaration order:
    if param is not bound: continue
    value = bound[param]

    switch param:
        StringParam:
            if value is not AstScalar:
                error E_SEM_EXPECTED_SCALAR at value.Span.Start
                      actual = Shape(value)
            reason = null
            if param.Validate is not null:
                reason = param.Validate(value.Text)
            if reason is not null:
                error E_SEM_VALIDATOR_FAILED at value.Span.Start
                      context = parameter '<param.Name>', reason
            store ScalarValue(value.Text)

        TransportParam:
            if value is not AstExpr:
                error E_SEM_EXPECTED_TRANSPORT at value.Span.Start
                      actual = Shape(value)
            resolved = ResolveExpr(value, param.Expected)
            store TransportValue(resolved)

        TransportListParam:
            if value is not AstList:
                error E_SEM_EXPECTED_TRANSPORT_LIST at value.Span.Start
                      actual = Shape(value)

            count = value.Elements.Count
            if count < param.Min:
                error E_SEM_LIST_MIN at value.OpenOffset
                      count = count, min = param.Min
            if param.Max is not null and count > param.Max:
                error E_SEM_LIST_MAX at value.OpenOffset
                      count = count, max = param.Max

            resolvedElements = []
            for each element in value.Elements in order:
                resolved = ResolveExpr(element, param.Expected)
                resolvedElements.add(resolved)

            if param.Homogeneous and resolvedElements.Count > 1:
                firstType = resolvedElements[0].Type
                for i in 1..resolvedElements.Count-1:
                    if resolvedElements[i].Type != firstType:
                        error E_SEM_LIST_HOMOGENEITY
                              at resolvedElements[i].Span.Start
                              found = all distinct element types,
                                      in element order

            store TransportListValue(resolvedElements)

        StructParam:
            if value is not AstStruct:
                error E_SEM_EXPECTED_STRUCT at value.Span.Start
                      actual = Shape(value)
            store StructValue(ResolveStruct(value, param.Schema, param.Name, []))
```

`Shape(value)` is exactly one of:

```text
"scalar"          for AstScalar
"transport"       for AstExpr (including casts)
"transport list"  for AstList
"struct"          for AstStruct
```

### 7.6 Expected-type guarantees

`ResolveExpr` filters candidates by the expected set and rejects cast targets
outside that set. Therefore every resolved result passed to a transport
parameter is guaranteed to be in the parameter's expected set. There is no
user-visible `E_SEM_*` error for an expected-type mismatch after resolution; a
violation is an internal invariant failure and MUST be reported as
`E_CMP_INCONSISTENT_TREE` by the compiler.

### 7.7 Type names

Type names are resolved through the transport-type catalog:

```text
if not catalog.TryResolve(typeName, out target, out canonicalName):
    error E_SEM_UNKNOWN_TRANSPORT_TYPE at type token offset
```

Type names are case-insensitive. The canonical spelling is `canonicalName` and is
used by the printer.

### 7.8 Struct resolution

```text
ResolveStruct(astStruct, schema, paramName, parentPath):
    bound = dictionary<StructField, AstValue>

    # 1. Bind source fields in source order.
    for each field in astStruct.Fields in source order:
        name = normalize(field.Name)
        schemaField = schema.Fields.Find(name)
        if schemaField is null:
            path = JoinPath(parentPath, field.Name)       # raw leaf
            error E_SEM_UNKNOWN_STRUCT_FIELD at field.NameToken.Offset
                  path = path, expected = schema field names
        path = JoinPath(parentPath, schemaField.Name)     # canonical segment
        if bound.Contains(schemaField):
            error E_SEM_DUPLICATE_STRUCT_FIELD at field.NameToken.Offset
                  path = path
        bound.add(schemaField, field.Value)

    # 2. Missing required fields.
    for each schemaField in schema.Fields in schema order:
        if not bound.Contains(schemaField) and schemaField.Required:
            path = JoinPath(parentPath, schemaField.Name)
            error E_SEM_MISSING_REQUIRED_STRUCT_FIELD
                  at astStruct.CloseBraceOffset
                  path = path, parameter = paramName

    # 3. Validate provided fields in schema order.
    result = []
    for each schemaField in schema.Fields in schema order:
        if not bound.Contains(schemaField): continue
        value = bound[schemaField]
        path = JoinPath(parentPath, schemaField.Name)

        switch schemaField:
            StringField:
                if value is not AstScalar:
                    error E_SEM_STRUCT_FIELD_EXPECTED_SCALAR at value.Span.Start
                          path = path, actual = Shape(value)
                reason = null
                if schemaField.Validate is not null:
                    reason = schemaField.Validate(value.Text)
                if reason is not null:
                    error E_SEM_VALIDATOR_FAILED at value.Span.Start
                          context = field '<path>' of parameter '<paramName>'
                          reason
                result.add(StringFieldValue(schemaField, value.Text))

            NestedStructField:
                if value is not AstStruct:
                    error E_SEM_STRUCT_FIELD_EXPECTED_STRUCT at value.Span.Start
                          path = path, actual = Shape(value)
                nested = ResolveStruct(value, schemaField.Schema, paramName, path)
                result.add(NestedStructFieldValue(schemaField, nested))

    return result
```

`JoinPath(parentPath, name)` joins the segments with `.`; an empty parent path
yields `name`. Struct values contain only strings and nested structs. A transport
or list in a struct field is rejected by the scalar/struct shape checks.

### 7.9 Resolution order and determinism

- Resolution is pre-order: a transport is resolved before its parameters.
- Candidate resolution happens before argument binding.
- Binding errors are reported in this order: too many positional; then named
  arguments in source order, checking the name first (`E_SEM_UNKNOWN_PARAM`) and
  then whether the target is already bound (`E_SEM_DUPLICATE_PARAM`); then the
  first missing required parameter in declaration order.
- Parameter validation follows declaration order.
- List arity is checked before elements; elements are resolved in order;
  homogeneity is checked after all elements.
- Struct binding errors are reported in source order, then missing required in
  schema order, then field value validation in schema order.
- No backtracking is performed. If a candidate is selected by canonical/alias
  rules, it is final. A homogeneity failure is not resolved by retrying a
  different candidate.

---

## 8. Resolved tree

```text
ResolvedExpr           (abstract) { TransportType Type; SourceSpan Span; }
ResolvedTransport      : ResolvedExpr
ResolvedCast           : ResolvedExpr

ResolvedTransport
{
    ITransportConstructor Constructor;
    IReadOnlyList<ParamBinding> Provided;   // declaration order
}

ParamBinding
{
    Param Param;
    ResolvedValue Value;
}

ResolvedValue          (abstract)
ScalarValue            : ResolvedValue { string Text; }
TransportValue         : ResolvedValue { ResolvedExpr Expr; }
TransportListValue     : ResolvedValue { IReadOnlyList<ResolvedExpr> Elements; }
StructValue            : ResolvedValue { IReadOnlyList<StructFieldBinding> Fields; }

ResolvedCast
{
    ResolvedTransport Inner;
    TransportType Target;
}

StructFieldBinding
{
    StructField Field;
    ResolvedStructFieldValue Value;
}

ResolvedStructFieldValue (abstract)
StringFieldValue         { string Text; }
NestedStructFieldValue   { IReadOnlyList<StructFieldBinding> Fields; }
```

Invariants:

- `Provided` contains exactly the parameters that were supplied. Absent
  parameters are absent; no defaults are inserted.
- `Provided` is ordered by `Constructor.Params` declaration order, not source
  order.
- `ResolvedTransport.Type == Constructor.Type`.
- `ResolvedCast.Type == ResolvedCast.Target`.
- `ResolvedCast.Inner.Type != ResolvedCast.Target` (identity casts are collapsed).
- `TransportListValue.Elements` satisfies the parameter's `Min`/`Max`; if
  `Homogeneous`, all element types are equal.
- `StructValue.Fields` are in schema order and contain exactly the provided
  fields, each at most once. Required fields are present. Field value kinds match
  the schema.
- All names are canonical: scheme names lowercase, parameter names lowercase,
  struct field names lowercase, type names canonical.
- Every `ResolvedExpr`, including every list element and cast inner node, carries
  a `Span` inherited from `ResolvedExpr`.
- For every `TransportValue` bound to a `TransportParam`, the value's `Type` is
  in the parameter's `Expected` set. For every element of a
  `TransportListValue` bound to a `TransportListParam`, the element's `Type` is
  in the parameter's `Expected` set.
- Spans are retained for diagnostics.
- No AST-only information (original alias, source case, quoting style, positional
  versus named form) is retained.

---

## 9. Compiler boundary

The compiler is an **opaque module** that consumes a resolved tree. It is not part
of the parser or printer.

```csharp
interface IResolvedTreeCompiler<TOutput>
{
    TOutput Compile(ResolvedExpr root);
}
```

- The compiler receives a valid resolved tree.
- The compiler MUST NOT be required by the lexer, parser, resolver, or canonical
  printer.
- The compiler MAY produce `IDescription`, a typed builder tree, or any other
  representation.
- If the compiler detects an invariant violation, it reports a `CompilerException`
  with an `E_CMP_*` code (§13.6). A correct parser never produces such a tree.

### 9.1 `IDescription` adapter (example, not normative)

The current builder consumes `IDescription`, a string-keyed bag of `IElement`.
An implementation MAY provide an adapter with this mapping:

```text
CompileExpr(ResolvedExpr e):
    if e is ResolvedCast:
        return Description {
            "name" = "convert",                 # internal adapter node only
            "type" = catalog.GetCanonicalName(e.Target),
            "from" = DescriptionElement(CompileTransport(e.Inner))
        }
    if e is ResolvedTransport:
        return CompileTransport(e)

CompileTransport(ResolvedTransport t):
    d = Description {
        "name" = t.Constructor.Name,
        "type" = catalog.GetCanonicalName(t.Type)
    }
    for each binding in t.Provided in declaration order:
        d.Add(binding.Param.Name, CompileValue(binding.Param, binding.Value))
    return d

CompileValue(param, value):
    ScalarValue            -> StringElement(value.Text)
    TransportValue         -> DescriptionElement(CompileExpr(value.Expr))
    TransportListValue     -> ArrayElement(value.Elements.Select(CompileExpr)
                                                  .Select(DescriptionElement))
    StructValue            -> DescriptionElement(CompileStruct(value))

CompileStruct(value):
    d = Description
    for each field binding in schema order:
        StringFieldValue       -> d.Add(field.Name, StringElement(text))
        NestedStructFieldValue -> d.Add(field.Name,
                                        DescriptionElement(CompileStruct(nested)))
    return d
```

Notes:

- `convert` exists only inside this adapter. It is not formula syntax and has no
  scheme registration. A different compiler may represent casts differently or
  call the builder directly.
- Absent parameters are never added.
- The adapter assumes the resolved tree is valid; duplicate keys, unknown
  parameters, and kind mismatches are compiler invariant errors.
- The adapter’s `name` and `type` keys are reserved. A parameter literally named
  `name` or `type` would collide; an implementation of this adapter must prefix
  parameter keys (for example `param.<Name>`) or reject those names.
- `IDescription` is not the parser’s or printer’s model. It is an adapter output.

### 9.2 Build-time cast conversion

When a builder or compiler builds a `ResolvedCast`:

1. Build `Inner`.
2. If `Inner.Type == Target`, use the inner transport (identity casts should
   already have been collapsed; this is defense in depth).
3. Otherwise ask the converter graph for a path from `Inner.Type` to `Target`.
4. If no path exists, report `E_BLD_NO_CONVERSION_PATH`.

The parser MUST NOT validate converter paths. It validates only that the target
type exists. Converter availability is build-time configuration.

---

## 10. Canonical printer

The canonical printer turns a resolved tree into a canonical formula. Its input
is the resolved tree plus the constructor/parameter metadata reachable from that
tree and the `ITransportTypeCatalog` needed to print cast targets. It does not
depend on the AST, on `IDescription`, or on the compiler.

If the input tree violates the invariants in §8, the printer throws
`PrinterException` with an `E_PRN_*` code (§13.5). A correct resolver never
produces such a tree. Before printing, the printer validates all invariants:
required parameters, declared and unique provided parameters, value kinds,
expected-type membership, list arity and homogeneity, struct fields, cast
targets, and scalar printability.

### 10.1 Expression

```text
PrintExpr(expr):
    if expr is ResolvedCast:
        if expr.Inner.Type == expr.Target:
            error E_PRN_INCONSISTENT_TREE detail "identity cast node"
        return PrintTransport(expr.Inner) + " as " +
               catalog.GetCanonicalName(expr.Target)
    if expr is ResolvedTransport:
        return PrintTransport(expr)
```

There is no prefix and no trailing whitespace or newline.

### 10.2 Transport

```text
PrintTransport(t):
    parts = []

    positional = t.Constructor.Params.Where(p => p.Positional).ToList()
    n = largest k such that every positional[i] with i < k is present in t.Provided

    emitted = set()

    for i in 0 .. n-1:
        param = positional[i]
        binding = t.Provided.FirstOrDefault(b => b.Param == param)
        parts.add(PrintValue(param, binding.Value))
        emitted.add(param)

    for each param in t.Constructor.Params in declaration order:
        binding = t.Provided.FirstOrDefault(b => b.Param == param)
        if binding is not null and param not in emitted:
            parts.add(param.Name + "=" + PrintValue(param, binding.Value))

    return t.Constructor.Name + "(" + join(parts, ", ") + ")"
```

Consequences:

- The canonical scheme is `Constructor.Name` (lowercase). Aliases never appear.
- Absent parameters are omitted.
- The longest provided prefix of positional-capable parameters is printed
  positionally; all other provided parameters are printed named.
- A positional-capable parameter that is provided but not in the longest prefix
  (because an earlier positional-capable parameter is absent) is printed named.
- Named-only parameters are always printed named.
- Named-only parameters may be printed after positional parameters that are
  declared later. This is intentional and allowed by the grammar; declaration
  order is not globally preserved when named-only parameters interleave with
  positional ones.
- Arguments are separated by `, ` (comma + one space).
- Empty argument lists print as `()`.
- No trailing comma.

### 10.3 Values

```text
PrintValue(param, value):
    ScalarValue        -> PrintScalar(value.Text)
    TransportValue     -> PrintExpr(value.Expr)
    TransportListValue -> "[" + join(value.Elements.Select(PrintExpr), ", ") + "]"
    StructValue        -> "{" + join(PrintStructFields(value), ", ") + "}"
```

Struct fields are printed in schema order:

```text
PrintStructFields(value):
    parts = []
    for each field binding in value.Fields in schema order:
        if StringFieldValue:
            parts.add(field.Name + "=" + PrintScalar(text))
        if NestedStructFieldValue:
            parts.add(field.Name + "={" + join(PrintStructFields(nested), ", ") + "}")
    return parts
```

Empty lists print `[]`; empty structs print `{}`.

### 10.4 Scalars

```text
PrintScalar(text):
    if text contains CR, LF, or an unpaired surrogate:
        error E_PRN_INVALID_SCALAR code = offending UTF-16 code unit
    if text == "":
        return '""'
    if text contains any character in WS or DELIM:
        return '"' + Escape(text) + '"'
    return text
```

`Escape` replaces:

```text
\  ->  \\
"  ->  \"
```

No other escapes are emitted. Raw `CR` and `LF` cannot occur in a valid resolved
scalar; if one does, the printer reports `E_PRN_INVALID_SCALAR` before the
quoting decision. `U+<code>` is formatted as `U+` followed by exactly four
uppercase hexadecimal digits of the UTF-16 code unit.

### 10.5 Canonicalization summary

The printer normalizes:

- aliases → canonical scheme names;
- identifier case → lowercase for schemes, parameters, struct fields;
- type names → catalog canonical spelling;
- whitespace and comma spacing;
- positional/named form → longest provided positional prefix + named remainder;
- named argument order → declaration order after the positional prefix;
- struct field order → schema order;
- quoting → minimal safe form;
- identity casts → absent (they never exist in a resolved tree).

The printer preserves:

- scalar values exactly, including case, Unicode, and leading/trailing spaces
  (quoted as needed);
- presence versus absence of optional parameters;
- the order of transport list elements;
- cast nodes that actually change the type.

### 10.6 Printer examples

Assume a registry where:

- `alpha`: `input` (Transport, positional, required), `mode` (String, positional,
  optional), `timeout` (String, positional = false, optional), type `TypeA`.
- `beta`: `value` (String, positional, required), type `TypeB`.
- `gamma`: `items` (TransportList, positional, required, min 2), type `TypeC`.
- `structy`: `config` (Struct, positional, required; fields `name` String required,
  `nested` nested Struct optional with field `a` String optional), type `TypeA`.

| Formula | Meaning |
|---|---|
| `alpha(beta(x))` | Input positional; other params absent. |
| `alpha(beta(x), fast)` | Input and mode positional. |
| `alpha(beta(x), timeout=5s)` | Input positional; timeout named-only. |
| `alpha(beta(x), fast, timeout=5s)` | Input and mode positional; timeout named. |
| `alpha(beta("a, b"), "x=y", timeout=1s)` | Quoted scalars. |
| `alpha(beta(x) as TypeA)` | Cast inside an argument. |
| `alpha(beta(x)) as TypeB` | Top-level cast. |
| `gamma([alpha(beta(x)), alpha(beta(x))])` | Homogeneous list. |
| `structy({name=x, nested={a=1}})` | Nested struct. |

---

## 11. Round-trip and equality

### 11.1 Resolved-tree equality

Two resolved trees are equal iff:

- corresponding transports have the same canonical constructor `Name` and same
  `Type`;
- the set of provided parameters is identical, with equal values;
- transport list elements are equal and in the same order;
- struct fields are equal; field order is compared in schema order;
- cast nodes have equal inner trees and equal target types;
- scalar text is identical (ordinal comparison);
- absent parameters are absent in both trees.

Equality ignores spans, original aliases, source case, original quoting, and the
original positional/named form.

### 11.2 Required properties

Let `R` be a registry and `C` a type catalog.

For every resolved tree `t` produced with `R` and `C`:

```text
ParseAndResolve(Print(t), R, C) == t
```

For every syntactically and semantically valid source `s`:

```text
Print(ParseAndResolve(Print(ParseAndResolve(s, R, C)), R, C))
    == Print(ParseAndResolve(s, R, C))
```

For every resolved tree `t`, `Print(t)` is syntactically valid and contains no
raw `CR` or `LF`.

### 11.3 Deliberate canonicalization

The following source differences produce the same canonical output and the same
resolved tree:

- `ALPHA(beta(x))` and `alpha(beta(x))`;
- `alpha( beta(x) ,  x )` and `alpha(beta(x), x)`;
- `alpha(beta(x), mode=x)` and `alpha(beta(x), x)`;
- `alpha(beta(x), timeout=1s)` and
  `alpha(input=beta(x), timeout=1s)` (named order);
- `alpha(beta(x)) as TypeA` and `alpha(beta(x))` (identity cast collapsed);
- a canonical scheme name and an alias that resolves to the same constructor
  under the expected type;
- struct fields in any source order (resolved/canonical order is schema order);
- a bare scalar and its quoted equivalent when quoting is unnecessary;
- different whitespace, including CR/LF.

---

## 12. Error model

### 12.1 Error objects

```csharp
sealed record ParseError(string Code, string Message, int Offset)
{
    public override string ToString()
        => $"{Code}: {Message} (offset {Offset})";
}

sealed record PrinterException(string Code, string Message);
sealed record CompilerException(string Code, string Message);
sealed record RegistrationException(string Code, string Message);
sealed record BuildException(string Code, string Message);
```

- Parser/lexer/semantic errors use `ParseError` and always include an offset.
- Registration, printer, compiler, and build errors have no source offset.
- Messages are the exact templates in §13, with placeholders substituted.
- No error recovery, no multiple errors, no warnings.

### 12.2 Offset rules

- Token-based errors: offset of the offending token start.
- EOF errors: offset equal to input length.
- `E_SYN_EMPTY_INPUT` (including whitespace-only input) is an exception: its
  offset is always `0`.
- Missing required parameter: offset of the closing `)` token.
- Missing required struct field: offset of the closing `}` token.
- Too many positional arguments: offset of the first extra positional value.
- Trailing comma: offset of the comma.
- Positional after named: offset of the first token of the offending positional
  value.
- Chained cast: offset of the second `as` token.
- Nested list: offset of the nested `[` token.
- Trailing input: offset of the first extra token.
- List minimum/maximum: offset of the list's `[` token.
- List homogeneity: offset of the first element whose resolved type differs from
  the first element's type.
- Struct unknown/duplicate field: offset of the field name token.
- Validator failure: offset of the scalar value token.
- Cast type mismatch and unknown cast type: offset of the cast type token.
- Unknown/ambiguous scheme and no-variant: offset of the scheme token.
- Unknown/duplicate parameter: offset of the parameter name token.

### 12.3 Error precedence

1. Lexing errors are reported before any parsing error. The entire input is
   lexed first.
2. Syntax errors are reported before any semantic error. Parsing completes
   successfully before resolution starts.
3. Semantic errors are reported in the order defined in §7.9.
4. Registration errors occur when the registry is built, before any parse.
5. Printer errors occur only for invalid resolved trees, which a correct resolver
   never produces.
6. Compiler and build errors are outside the parser/printer contract.

---

## 13. Error catalog

### 13.1 Lexical errors

| Code | Condition | Message template | Offset |
|---|---|---|---|
| `E_LEX_UNTERMINATED_STRING` | Closing quote not found | `unterminated quoted string` | Opening quote |
| `E_LEX_INVALID_ESCAPE` | Backslash followed by a code unit other than `"` or `\`; this takes precedence for that code unit | `invalid escape sequence '<sequence>'; only \" and \\ are allowed` | Backslash |
| `E_LEX_RAW_NEWLINE_IN_STRING` | Raw CR or LF encountered by the decode loop outside a valid escape | `raw newline is not allowed inside a quoted string` | Newline character |
| `E_LEX_INVALID_UNICODE` | Unpaired surrogate encountered by the decode loop outside a valid escape | `invalid Unicode scalar value` | Offending code unit |

Lexer precedence:

1. At a code unit outside backslash handling: unpaired surrogate, then raw
   CR/LF.
2. A backslash consumes the following code unit. If that unit is not `"` or `\`,
   including CR, LF, or an unpaired surrogate, the result is
   `E_LEX_INVALID_ESCAPE` at the backslash.
3. A backslash at EOF is `E_LEX_UNTERMINATED_STRING` at the opening quote.

### 13.2 Syntax errors

Generic token error:

```text
E_SYN_UNEXPECTED_TOKEN:
    expected <expected>, got <token>
```

Expected-string contexts:

| Parser point | `<expected>` |
|---|---|
| Top level, token cannot start a transport expression | `a transport expression` |
| Start of a transport call (scheme) | `a transport scheme identifier` |
| After the scheme | `'('` |
| First token inside `(` that is not `)` | `a value or ')'` |
| Named-argument name that is not an identifier | `an identifier` |
| After an argument value | `',' or ')'` |
| Value position | `a value` |
| List element | `a transport expression` |
| After a list element | `',' or ']'` |
| Struct field name | `a struct field name` |
| After a struct field name | `'='` |
| After a struct field value | `',' or '}'` |
| Cast type | `a transport type name` |

Specific syntax errors:

| Code | Condition | Message template | Offset |
|---|---|---|---|
| `E_SYN_EMPTY_INPUT` | Input is empty or whitespace only | `expected a transport expression` | `0` |
| `E_SYN_TRAILING_COMMA` | Comma immediately following an argument, list element, or struct field, and immediately before the matching `)`, `]`, or `}` | `trailing comma is not allowed in <context>` | Comma |
| `E_SYN_POSITIONAL_AFTER_NAMED` | Positional argument after a named argument | `positional argument is not allowed after a named argument` | First token of the positional value |
| `E_SYN_CHAINED_CAST` | Second `as` in one expression | `chained casts are not allowed` | Second `as` |
| `E_SYN_NESTED_LIST` | `[` as a list element | `nested transport lists are not allowed` | Nested `[` |
| `E_SYN_TRAILING_INPUT` | Non-EOF token after the top-level expression | `unexpected trailing input` | First extra token |

`<context>` is exactly one of `argument list`, `transport list`, `struct`.
A comma with no preceding argument, element, or field is not a trailing comma;
it is reported as `E_SYN_UNEXPECTED_TOKEN` (for example, `alpha(,)` and
`alpha([,])`).

### 13.3 Semantic errors

Formatting helpers used in messages:

- `FormatTypeSet(set)`:
  - `Any` → `any transport type`
  - `Exactly(T)` → `T`
  - `OneOf(...)` → distinct canonical names sorted, joined with ` or `
- `FormatVariants(candidates)` → entries `name (Type)`, where `name` is the
  constructor's canonical `Name` and `Type` is the canonical type name, sorted by
  name then type, joined with `, `.
- `FormatParamExpectation(params)` → if non-empty: `one of: <names>`, where the
  names are parameter names in declaration order, each enclosed in single quotes,
  joined with `, `; if empty: `the scheme has no parameters`.
- `FormatFieldExpectation(schema)` → if non-empty: `one of: <names>`, where the
  names are field names in schema order, each enclosed in single quotes, joined
  with `, `; if empty: `the struct has no fields`.
- `FormatKnownSchemes()` → if non-empty: `known schemes: <list>`, where list is
  canonical names sorted and joined with `, `; if empty:
  `no schemes are registered`.
- `FormatKnownTypes()` → if non-empty: `known types: <list>`, where list is
  canonical names sorted and joined with `, `; if empty:
  `no transport types are registered`.
- `FormatTypes(types)` → distinct canonical names in first-encounter order,
  joined with `, `. For `E_SEM_LIST_HOMOGENEITY`, `<types>` is all distinct
  resolved element types of the list, in element order.

| Code | Condition | Message template | Offset |
|---|---|---|---|
| `E_SEM_UNKNOWN_SCHEME` | No canonical or alias candidates | `unknown transport scheme '<scheme>'; <known>` | Scheme token |
| `E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE` | Candidates exist but expected filter removes all | `scheme '<scheme>' has no variant for expected transport type <set>; available: <variants>` | Scheme token |
| `E_SEM_AMBIGUOUS_SCHEME` | More than one candidate remains | `scheme '<scheme>' is ambiguous; candidates: <variants>` | Scheme token |
| `E_SEM_UNKNOWN_PARAM` | Named argument name not declared | `unknown parameter '<name>' for scheme '<scheme>'; <expected>` | Parameter name token |
| `E_SEM_DUPLICATE_PARAM` | Parameter bound twice | `parameter '<name>' is specified more than once` | Duplicate parameter name token |
| `E_SEM_MISSING_REQUIRED_PARAM` | Required parameter not bound | `missing required parameter '<name>' for scheme '<scheme>'` | Closing `)` |
| `E_SEM_TOO_MANY_POSITIONAL` | More positional args than positional params | `too many positional arguments for scheme '<scheme>'; expected at most <n>` | First extra positional value |
| `E_SEM_EXPECTED_SCALAR` | String parameter received non-scalar | `parameter '<name>' expects a scalar value, got <shape>` | Value start |
| `E_SEM_EXPECTED_TRANSPORT` | Transport parameter received non-expression | `parameter '<name>' expects a transport, got <shape>` | Value start |
| `E_SEM_EXPECTED_TRANSPORT_LIST` | Transport-list parameter received non-list | `parameter '<name>' expects a transport list, got <shape>` | Value start |
| `E_SEM_EXPECTED_STRUCT` | Struct parameter received non-struct | `parameter '<name>' expects a struct, got <shape>` | Value start |
| `E_SEM_LIST_MIN` | List count below minimum | `transport list for '<name>' requires at least <min> element(s), got <count>` | List `[` |
| `E_SEM_LIST_MAX` | List count above maximum | `transport list for '<name>' requires at most <max> element(s), got <count>` | List `[` |
| `E_SEM_LIST_HOMOGENEITY` | Homogeneous list contains different types | `transport list for '<name>' must contain one transport type; found <types>` | First differing element |
| `E_SEM_UNKNOWN_STRUCT_FIELD` | Struct field not in schema | `unknown field '<path>' in struct for parameter '<param>'; <expected>` | Field name token |
| `E_SEM_DUPLICATE_STRUCT_FIELD` | Struct field specified twice | `field '<path>' is specified more than once` | Duplicate field name token |
| `E_SEM_MISSING_REQUIRED_STRUCT_FIELD` | Required struct field absent | `missing required field '<path>' in struct for parameter '<param>'` | Closing `}` |
| `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` | String field received non-scalar | `field '<path>' expects a scalar value, got <shape>` | Value start |
| `E_SEM_STRUCT_FIELD_EXPECTED_STRUCT` | Nested struct field received non-struct | `field '<path>' expects a struct, got <shape>` | Value start |
| `E_SEM_UNKNOWN_TRANSPORT_TYPE` | Cast type not in catalog | `unknown transport type '<name>'; <known>` | Type token |
| `E_SEM_CAST_TYPE_MISMATCH` | Cast target outside expected set | `cast produces transport type <target>, but expected <set>` | Type token |
| `E_SEM_VALIDATOR_FAILED` | `Validate` returned non-null or threw | `parameter '<name>' is invalid: <reason>` | Value start |
| `E_SEM_VALIDATOR_FAILED` | Struct field validator failed | `field '<path>' of parameter '<param>' is invalid: <reason>` | Value start |

For a thrown validator, `<reason>` is exactly `validation failed`.

`<shape>` is exactly one of `scalar`, `transport`, `transport list`, `struct`.

`<known>` is `FormatKnownSchemes()` for unknown schemes and
`FormatKnownTypes()` for unknown transport types.

`<expected>` is `FormatParamExpectation(params)` for unknown parameters and
`FormatFieldExpectation(schema)` for unknown struct fields.

`E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE` formats `available` from `allCandidates`
before expected-type filtering. `E_SEM_AMBIGUOUS_SCHEME` formats `candidates`
after expected-type filtering, at the point of the error.

Placeholder sources:

| Placeholder | Source |
|---|---|
| `<scheme>` | Raw scheme identifier as written in the input, preserving case. |
| `<name>` in `E_SEM_UNKNOWN_PARAM`, `E_SEM_DUPLICATE_PARAM`, and `E_SEM_UNKNOWN_TRANSPORT_TYPE` | Raw identifier as written in the input, preserving case. |
| `<name>` in `E_SEM_MISSING_REQUIRED_PARAM`, `E_SEM_EXPECTED_SCALAR`, `E_SEM_EXPECTED_TRANSPORT`, `E_SEM_EXPECTED_TRANSPORT_LIST`, `E_SEM_EXPECTED_STRUCT`, `E_SEM_LIST_MIN`, `E_SEM_LIST_MAX`, and `E_SEM_LIST_HOMOGENEITY` | Canonical declared parameter name (lowercase). |
| `<path>` in struct-field errors | Dot-separated field path. Segments for known schema fields are canonical lowercase; the final segment of an unknown field is raw input spelling. |
| `<param>` | Canonical declared parameter name (lowercase). |
| `<target>`, `<actual>`, `<type>`, `<from>`, `<to>`, `<set>`, `<variants>`, `<types>` | Canonical transport-type names and formatted lists. |
| `<shape>` | One of `scalar`, `transport`, `transport list`, `struct`. |

In `E_SEM_VALIDATOR_FAILED`, `<name>` is the canonical parameter name for a
parameter validator; `<path>` is the field path for a struct-field validator.

There is no user-visible semantic error for a resolved transport type outside a
parameter's expected set: `ResolveExpr` guarantees membership. An internal
violation is reported as `E_CMP_INCONSISTENT_TREE` by the compiler.

### 13.4 Registration errors

Registration errors use `RegistrationException` and have no offset.

| Code | Condition | Message template |
|---|---|---|
| `E_REG_INVALID_CANONICAL_NAME` | Canonical scheme is not an identifier | `invalid canonical scheme name '<name>'` |
| `E_REG_DUPLICATE_CANONICAL_NAME` | Canonical scheme already registered | `canonical scheme '<name>' is already registered` |
| `E_REG_INVALID_PARAM_NAME` | Parameter name is not an identifier | `invalid parameter name '<name>'` |
| `E_REG_DUPLICATE_PARAM_NAME` | Duplicate parameter name in one constructor | `parameter '<name>' is declared more than once in scheme '<scheme>'` |
| `E_REG_INVALID_ALIAS_NAME` | Alias is not an identifier | `invalid alias name '<name>'` |
| `E_REG_DUPLICATE_ALIAS_FOR_TYPE` | Same alias name already registered for the same type by a different constructor | `alias '<name>' for transport type <type> is already registered` |
| `E_REG_EMPTY_TYPE_SET` | `OneOf` has no types | `transport type set must contain at least one type` |
| `E_REG_INVALID_LIST_RANGE` | `Min < 0`, or `Max < Min` | `invalid transport list range: min <min>, max <max>` |
| `E_REG_DUPLICATE_STRUCT_FIELD` | Duplicate struct field name | `struct field '<name>' is declared more than once` |
| `E_REG_INVALID_STRUCT_FIELD_NAME` | Struct field name is not an identifier | `invalid struct field name '<name>'` |
| `E_REG_INVALID_TYPE_NAME` | Transport type name is not an identifier | `invalid transport type name '<name>'` |
| `E_REG_DUPLICATE_TYPE_NAME` | Catalog contains duplicate type name | `transport type name '<name>' is already registered` |

For `E_REG_INVALID_LIST_RANGE`, `<max>` is `unbounded` when `Max` is `null`.

### 13.5 Printer errors

Printer errors use `PrinterException` and have no offset.

| Code | Condition | Message template |
|---|---|---|
| `E_PRN_INCONSISTENT_TREE` | Generic invariant violation | `resolved tree is inconsistent: <detail>` |
| `E_PRN_MISSING_REQUIRED_PARAM` | Required parameter absent | `resolved transport '<scheme>' is missing required parameter '<name>'` |
| `E_PRN_UNKNOWN_PARAM` | Provided parameter not declared | `resolved transport '<scheme>' contains unknown parameter '<name>'` |
| `E_PRN_DUPLICATE_PARAM` | Same parameter provided twice | `resolved transport '<scheme>' contains parameter '<name>' more than once` |
| `E_PRN_PARAM_KIND_MISMATCH` | Value kind does not match parameter kind | `parameter '<name>' of scheme '<scheme>' has value kind <actual>, expected <expected>` |
| `E_PRN_LIST_ARITY` | List count outside `Min`/`Max` | `transport list parameter '<name>' of scheme '<scheme>' has <count> element(s), expected <range>` |
| `E_PRN_LIST_HOMOGENEITY` | Homogeneous list has mixed types | `transport list parameter '<name>' of scheme '<scheme>' mixes transport types` |
| `E_PRN_STRUCT_FIELD_UNKNOWN` | Struct contains undeclared field | `struct parameter '<name>' of scheme '<scheme>' contains unknown field '<field>'` |
| `E_PRN_STRUCT_FIELD_MISSING` | Required struct field absent | `struct parameter '<name>' of scheme '<scheme>' is missing required field '<field>'` |
| `E_PRN_STRUCT_FIELD_DUPLICATE` | Same struct field appears twice | `struct parameter '<name>' of scheme '<scheme>' contains field '<field>' more than once` |
| `E_PRN_STRUCT_FIELD_KIND_MISMATCH` | Struct field value kind mismatch | `field '<field>' of struct parameter '<name>' has value kind <actual>, expected <expected>` |
| `E_PRN_INVALID_CAST_TARGET` | Cast target not in catalog | `cast target '<type>' is invalid` |
| `E_PRN_INVALID_SCALAR` | Scalar contains CR, LF, or an unpaired surrogate | `scalar value contains a character that cannot be printed: U+<code>` |

`<range>` is `<min>..<max>` if `Max` is set, otherwise `at least <min>`.

For `E_PRN_INVALID_SCALAR`, `<code>` is exactly four uppercase hexadecimal digits
of the UTF-16 code unit, prefixed with `U+`. An identity cast node is an
inconsistent-tree violation and uses `E_PRN_INCONSISTENT_TREE` with detail
`identity cast node`. Printer messages use canonical scheme, parameter, field,
and type names.

### 13.6 Compiler and build errors

These are outside the parser/printer contract but complete the boundary.

| Code | Source | Message template |
|---|---|---|
| `E_CMP_INCONSISTENT_TREE` | Compiler | `compiler received an inconsistent resolved tree: <detail>` |
| `E_CMP_DUPLICATE_PARAM` | Compiler | `compiler received duplicate parameter '<name>'` |
| `E_CMP_UNKNOWN_PARAM` | Compiler | `compiler received unknown parameter '<name>'` |
| `E_CMP_PARAM_KIND_MISMATCH` | Compiler | `compiler expected value kind <expected> for parameter '<name>', got <actual>` |
| `E_BLD_NO_CONVERSION_PATH` | Builder | `no conversion path from <from> to <to>` |
| `E_BLD_CONSTRUCTOR_FAILED` | Builder | `transport constructor '<scheme>' failed` |

---

## 14. Conformance examples and test matrix

The examples below use hypothetical metadata to stay transport-agnostic.

Registry:

```text
alpha   : TypeA
          input   Transport      positional required
          mode    String         positional optional
          timeout String         named-only optional

beta    : TypeB
          value   String         positional required

gamma   : TypeC
          items   TransportList  positional required, min 2, max 3, homogeneous

outer   : TypeC
          input   Transport      positional required, expected Exactly(TypeB)

structy : TypeA
          config  Struct         positional required
                  name   String required
                  nested Struct optional
                         a String optional
```

Alias: `alpha` is also an alias for `beta` (`TypeB`). Canonical `alpha` remains
`TypeA`.

Type catalog: `TypeA`, `TypeB`, `TypeC`.

Additional fixtures for the matrix:

```text
shared       alias for beta (TypeB) and gamma (TypeC); no canonical constructor
validated    TypeB; value String positional required; validator rejects "bad"
mixed        TypeA; flag String named-only optional; input Transport positional required
opt          TypeA; a String positional optional; b String positional optional
bag          TypeA; items TransportList positional optional, min 0
emptyStruct  TypeA; config Struct positional required; schema has no fields
single       TypeA; item TransportList positional required, min 1, max 1
deep         TypeA
             config Struct required
                    outer Struct required
                          inner Struct required
                                value String required
```

### 14.1 Valid parses

| Source | Resolved meaning |
|---|---|
| `alpha(beta(x))` | `alpha` TypeA; input = beta |
| `alpha(beta(x), fast)` | input and mode positional |
| `alpha(beta(x), timeout=5s)` | input positional, timeout named |
| `alpha(beta(x), fast, timeout=5s)` | input + mode positional, timeout named |
| `outer(alpha(x))` | inner `alpha` resolves to alias `beta` because expected is TypeB |
| `alpha(beta(x)) as TypeB` | inner TypeA, cast to TypeB |
| `alpha(beta(x)) as TypeA` | identity cast collapses to `alpha(beta(x))` |
| `gamma([alpha(beta(x)), alpha(beta(x))])` | homogeneous TypeA list |
| `structy({name=x, nested={a=1}})` | nested struct |
| `beta("a, b")` | quoted scalar with comma |
| `beta("")` | empty scalar |

### 14.2 Syntax errors

| Source | Code | Offset |
|---|---|---|
| `` (empty) | `E_SYN_EMPTY_INPUT` | 0 |
| `alpha` | `E_SYN_UNEXPECTED_TOKEN` expected `'('` | 5 |
| `alpha(` | `E_SYN_UNEXPECTED_TOKEN` expected `a value or ')'` | 6 |
| `alpha(a,)` | `E_SYN_TRAILING_COMMA` | 7 |
| `alpha(,)` | `E_SYN_UNEXPECTED_TOKEN` expected `a value or ')'` | 6 |
| `alpha([,])` | `E_SYN_UNEXPECTED_TOKEN` expected `a transport expression` | 7 |
| `alpha(a b)` | `E_SYN_UNEXPECTED_TOKEN` expected `',' or ')'` | 8 |
| `alpha(p=1, a)` | `E_SYN_POSITIONAL_AFTER_NAMED` | 11 |
| `alpha([alpha(), 1])` | `E_SYN_UNEXPECTED_TOKEN` expected `a transport expression` | 16 |
| `alpha([[alpha()]])` | `E_SYN_NESTED_LIST` | 7 |
| `alpha({p})` | `E_SYN_UNEXPECTED_TOKEN` expected `'='` | 8 |
| `alpha({p=1,})` | `E_SYN_TRAILING_COMMA` | 10 |
| `alpha() as` | `E_SYN_UNEXPECTED_TOKEN` expected `a transport type name` | 10 |
| `alpha() as TypeA as TypeB` | `E_SYN_CHAINED_CAST` | 17 |
| `[alpha()]` | `E_SYN_UNEXPECTED_TOKEN` expected `a transport expression` | 0 |
| `alpha() extra` | `E_SYN_TRAILING_INPUT` | 8 |

### 14.3 Semantic errors

| Source | Code |
|---|---|
| `nosuch()` | `E_SEM_UNKNOWN_SCHEME` |
| `alpha()` | `E_SEM_MISSING_REQUIRED_PARAM` |
| `alpha(beta(x), q=1)` | `E_SEM_UNKNOWN_PARAM` |
| `alpha(beta(x), input=beta(y))` | `E_SEM_DUPLICATE_PARAM` |
| `alpha(beta(x), fast, extra)` | `E_SEM_TOO_MANY_POSITIONAL` |
| `alpha(1)` | `E_SEM_EXPECTED_TRANSPORT` |
| `structy(1)` | `E_SEM_EXPECTED_STRUCT` |
| `beta([alpha(beta(x))])` | `E_SEM_EXPECTED_SCALAR` |
| `gamma([alpha(beta(x))])` | `E_SEM_LIST_MIN` |
| `gamma([alpha(beta(x)), alpha(beta(x)), alpha(beta(x)), alpha(beta(x))])` | `E_SEM_LIST_MAX` |
| `gamma([alpha(beta(x)), beta(y)])` | `E_SEM_LIST_HOMOGENEITY` |
| `outer(gamma([alpha(beta(x)), alpha(beta(x))]))` | `E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE` |
| `shared(x)` | `E_SEM_AMBIGUOUS_SCHEME` |
| `validated(bad)` | `E_SEM_VALIDATOR_FAILED` |
| `structy({name=x, extra=1})` | `E_SEM_UNKNOWN_STRUCT_FIELD` |
| `structy({name=x, nested={unknown=1}})` | `E_SEM_UNKNOWN_STRUCT_FIELD` (path `nested.unknown`) |
| `structy({name=x, name=y})` | `E_SEM_DUPLICATE_STRUCT_FIELD` |
| `structy({})` | `E_SEM_MISSING_REQUIRED_STRUCT_FIELD` |
| `deep({outer={inner={}}})` | `E_SEM_MISSING_REQUIRED_STRUCT_FIELD` (path `outer.inner.value`) |
| `structy({name=alpha(beta(x))})` | `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` |
| `structy({name={}})` | `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` |
| `structy({name=x, nested=1})` | `E_SEM_STRUCT_FIELD_EXPECTED_STRUCT` |
| `deep({outer={inner={value=beta(x)}}})` | `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` (path `outer.inner.value`) |
| `alpha(beta(x)) as TypeZ` | `E_SEM_UNKNOWN_TRANSPORT_TYPE` |
| `outer(alpha(beta(x)) as TypeC)` | `E_SEM_CAST_TYPE_MISMATCH` |
| `outer(beta(x) as TypeA)` | `E_SEM_CAST_TYPE_MISMATCH` |
| `beta(1)` where `1` is a scalar and `value` is String | valid |

> Some rows depend on registry details; tests must define the full registry
> explicitly.

### 14.4 Canonical printer

| Resolved tree | Canonical output |
|---|---|
| `alpha(input=beta(x))` | `alpha(beta(x))` |
| `alpha(input=beta(x), mode=fast)` | `alpha(beta(x), fast)` |
| `alpha(input=beta(x), timeout=5s)` | `alpha(beta(x), timeout=5s)` |
| `alpha(input=beta(x), mode=fast, timeout=5s)` | `alpha(beta(x), fast, timeout=5s)` |
| `alpha(input=beta(x) as TypeA)` | `alpha(beta(x) as TypeA)` |
| `gamma(items=[alpha(beta(x)), alpha(beta(x))])` | `gamma([alpha(beta(x)), alpha(beta(x))])` |
| `structy(config={name=x, nested={a=1}})` | `structy({name=x, nested={a=1}})` |
| `beta(value="a, b")` | `beta("a, b")` |
| `beta(value="")` | `beta("")` |
| `beta(value="x=y")` | `beta("x=y")` |
| `beta(value="plain")` | `beta(plain)` |
| `alpha(input=alpha(beta(x)))` | `alpha(alpha(beta(x)))` |
| `mixed(input=beta(x), flag=fast)` | `mixed(beta(x), flag=fast)` |
| `opt(b=two)` | `opt(b=two)` |
| `bag(items=[])` | `bag([])` |
| `emptyStruct(config={})` | `emptyStruct({})` |
| `single(item=[beta(x)])` | `single([beta(x)])` |

### 14.5 Lexical errors

| Source | Code | Offset |
|---|---|---|
| `alpha("x)` | `E_LEX_UNTERMINATED_STRING` | 6 |
| `alpha("x\q")` | `E_LEX_INVALID_ESCAPE` | 8 |
| `alpha("x<LF>y")` where `<LF>` is a literal U+000A | `E_LEX_RAW_NEWLINE_IN_STRING` | 8 |
| `alpha("x<U+D800>y")` where `<U+D800>` is a literal unpaired surrogate | `E_LEX_INVALID_UNICODE` | 8 |

### 14.6 Registration errors

| Setup | Code |
|---|---|
| Canonical scheme `Alpha` already registered as `alpha` | `E_REG_DUPLICATE_CANONICAL_NAME` |
| Canonical scheme `1alpha` | `E_REG_INVALID_CANONICAL_NAME` |
| Parameter name `1input` | `E_REG_INVALID_PARAM_NAME` |
| Parameters `input` and `INPUT` | `E_REG_DUPLICATE_PARAM_NAME` |
| Alias name `1alias` | `E_REG_INVALID_ALIAS_NAME` |
| Alias `shared` registered twice for `TypeA` | `E_REG_DUPLICATE_ALIAS_FOR_TYPE` |
| `OneOf()` with no types | `E_REG_EMPTY_TYPE_SET` |
| `TransportListParam` with `Min = -1` | `E_REG_INVALID_LIST_RANGE` |
| Struct fields `name` and `NAME` | `E_REG_DUPLICATE_STRUCT_FIELD` |
| Struct field name `1name` | `E_REG_INVALID_STRUCT_FIELD_NAME` |
| Catalog type name `1Type` | `E_REG_INVALID_TYPE_NAME` |
| Catalog type name `TypeA` registered twice | `E_REG_DUPLICATE_TYPE_NAME` |

### 14.7 Printer invariant errors

| Tree condition | Code |
|---|---|
| Required parameter absent | `E_PRN_MISSING_REQUIRED_PARAM` |
| Provided parameter not declared | `E_PRN_UNKNOWN_PARAM` |
| Parameter provided twice | `E_PRN_DUPLICATE_PARAM` |
| Parameter value kind mismatch | `E_PRN_PARAM_KIND_MISMATCH` |
| List count outside `Min`/`Max` | `E_PRN_LIST_ARITY` |
| Homogeneous list has mixed types | `E_PRN_LIST_HOMOGENEITY` |
| Struct contains undeclared field | `E_PRN_STRUCT_FIELD_UNKNOWN` |
| Required struct field absent | `E_PRN_STRUCT_FIELD_MISSING` |
| Struct field appears twice | `E_PRN_STRUCT_FIELD_DUPLICATE` |
| Struct field value kind mismatch | `E_PRN_STRUCT_FIELD_KIND_MISMATCH` |
| Cast target not in catalog | `E_PRN_INVALID_CAST_TARGET` |
| Transport value outside the parameter's expected set | `E_PRN_INCONSISTENT_TREE` |
| Scalar contains CR/LF or an unpaired surrogate | `E_PRN_INVALID_SCALAR` |
| Identity cast node | `E_PRN_INCONSISTENT_TREE` |

A correct parser never produces these trees.

---

## 15. Out of scope

This document does not specify:

- how transports are built;
- how the converter graph chooses paths;
- how concrete constructors read their parameters;
- how aliases are chosen by an application;
- how `IDescription` is consumed;
- any concrete transport scheme or parameter name.

The parser and canonical printer are complete without those.
