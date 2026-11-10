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
2. Decode until the matching `"`:
   - `\"` → `"`.
   - `\\` → `\`.
   - Any other backslash sequence → `E_LEX_INVALID_ESCAPE` at the backslash.
   - A backslash immediately before EOF is treated as an unterminated string:
     `E_LEX_UNTERMINATED_STRING` at the opening quote.
   - Raw `CR` or `LF` → `E_LEX_RAW_NEWLINE_IN_STRING` at the newline.
   - Unpaired surrogate → `E_LEX_INVALID_UNICODE` at the offending code unit.
   - EOF before the closing quote → `E_LEX_UNTERMINATED_STRING` at the opening
     quote.
3. The token's decoded text is the content without the surrounding quotes.

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
SourceSpan        { int Start; int Length; }

AstValue          (abstract)
AstExpr           : AstValue   { AstTransportCall Call; AstCast? Cast; }
AstList           : AstValue   { IReadOnlyList<AstExpr> Elements; int OpenOffset; }
AstStruct         : AstValue   { IReadOnlyList<AstField> Fields; int CloseBraceOffset; }
AstScalar         : AstValue   { string Value; bool WasQuoted; }

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
- Spans cover the source range of the node and are used for semantic errors.
- Scheme, parameter, and field names are stored as written; normalization happens
  during resolution.

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

### 5.2 Expressions and casts

```text
ParseExpr():
    call = ParseTransportCall()
    if tokens.current is Word equal to "as" (case-insensitive):
        asToken = consume()
        typeToken = ParseTypeName()
        if tokens.current is Word equal to "as":
            error E_SYN_CHAINED_CAST at tokens.current.Offset
        return AstExpr(call, AstCast(typeToken.Text, typeToken))
    return AstExpr(call, null)
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
    args, closeParenOffset = ParseArgs()
    return AstTransportCall(normalize(schemeToken.Text), schemeToken,
                            args, closeParenOffset)
```

### 5.4 Arguments

`ParseArgs` is called after `(` and consumes the closing `)`.

```text
ParseArgs():
    args = []
    seenNamed = false

    if tokens.current is RParen:
        return [], consume().Offset

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
            args.add(named nameToken.Text, value)
            seenNamed = true
        else:
            if seenNamed:
                error E_SYN_POSITIONAL_AFTER_NAMED at current.Offset
            value = ParseValue()
            args.add(positional value)

        if tokens.current is Comma:
            comma = consume()
            if tokens.current is RParen:
                error E_SYN_TRAILING_COMMA context "argument list"
                      at comma.Offset
            continue

        if tokens.current is RParen:
            close = consume()
            return args, close.Offset

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
            return AstScalar(token.Text, WasQuoted = true)
        LBracket:
            return ParseList()
        LBrace:
            return ParseStruct()
        Word:
            if tokens.peek(1) is LParen:
                return ParseExpr()
            token = consume()
            return AstScalar(token.Text, WasQuoted = false)
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
        return AstList([], open.Offset)

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
            consume()
            return AstList(elements, open.Offset)

        error E_SYN_UNEXPECTED_TOKEN expected "',' or ']'" at current.Offset
```

List elements are expressions. Scalars, structs, and nested lists are rejected
syntactically.

### 5.7 Structs

```text
ParseStruct():
    open = consume LBrace
    if tokens.current is RBrace:
        return AstStruct([], open.Offset)

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
        fields.add(AstField(normalize(nameToken.Text), nameToken, value))

        if tokens.current is Comma:
            comma = consume()
            if tokens.current is RBrace:
                error E_SYN_TRAILING_COMMA context "struct"
                      at comma.Offset
            continue

        if tokens.current is RBrace:
            close = consume()
            return AstStruct(fields, close.Offset)

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
9. An alias may map to multiple constructors only if their `Type` values differ.
   An alias mapping to the same `Type` more than once is a registration error.
10. `SuggestedAliases` are not automatically registered. A host MAY pass them (or
    any other aliases) to `RegisterTransport`.

Registration errors are listed in §13.4. They occur when the registry is built,
not while parsing.

---

## 7. Resolution

Resolution consumes a syntactically valid AST and registry metadata and produces
a resolved tree. It performs all semantic checks.

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

    target = catalog.Resolve(ast.Cast.TypeName)
    if target is unknown:
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
    candidates = registry.Canonical(scheme) ∪ registry.Aliases(scheme)

    if candidates is empty:
        error E_SEM_UNKNOWN_SCHEME at call.SchemeToken.Offset

    if expected != Any:
        candidates = candidates.Where(c => expected.Contains(c.Type))
        if candidates is empty:
            error E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE
                  at call.SchemeToken.Offset

    canonical = registry.Canonical(scheme)
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
- Registration guarantees that no two candidates share the same `Type`, so after
  filtering by an `Exactly(T)` set at most one candidate remains.

### 7.4 Argument binding

```text
positionalParams = constructor.Params.Where(p => p.Positional).ToList()
positionalArgs   = call.Args.Where(a => a.Name is null).ToList()

if positionalArgs.Count > positionalParams.Count:
    error E_SEM_TOO_MANY_POSITIONAL
          at positionalArgs[positionalParams.Count].Value.Span.Start

bind positionalArgs[i] to positionalParams[i]

bound = dictionary<Param, AstValue>

for each named arg in source order:
    name = normalize(arg.Name)
    param = constructor.Params.Find(name)
    if param is null:
        error E_SEM_UNKNOWN_PARAM at arg.NameToken.Offset
    if bound.Contains(param):
        error E_SEM_DUPLICATE_PARAM at arg.NameToken.Offset
    bind arg.Value to param

for each param in constructor.Params in declaration order:
    if not bound and param.Required:
        error E_SEM_MISSING_REQUIRED_PARAM
              at call.CloseParenOffset
```

Named arguments may target positional and named-only parameters. Positional
arguments fill only parameters with `Positional = true`, in declaration order. A
named argument that targets a parameter already filled positionally is a
duplicate.

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
            if not param.Expected.Contains(resolved.Type):
                error E_SEM_TRANSPORT_TYPE_NOT_ALLOWED at value.Span.Start
                      actual = resolved.Type, expected = param.Expected
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
                if not param.Expected.Contains(resolved.Type):
                    error E_SEM_TRANSPORT_TYPE_NOT_ALLOWED at element.Span.Start
                          actual = resolved.Type, expected = param.Expected
                resolvedElements.add(resolved)

            if param.Homogeneous and resolvedElements.Count > 1:
                firstType = resolvedElements[0].Type
                for i in 1..resolvedElements.Count-1:
                    if resolvedElements[i].Type != firstType:
                        error E_SEM_LIST_HOMOGENEITY
                              at resolvedElements[i].Span.Start
                              found = distinct types in encounter order

            store TransportListValue(resolvedElements)

        StructParam:
            if value is not AstStruct:
                error E_SEM_EXPECTED_STRUCT at value.Span.Start
                      actual = Shape(value)
            store StructValue(ResolveStruct(value, param.Schema, param.Name))
```

`Shape(value)` is exactly one of:

```text
"scalar"          for AstScalar
"transport"       for AstExpr (including casts)
"transport list"  for AstList
"struct"          for AstStruct
```

### 7.6 Expected-type checks for transport parameters

`ResolveExpr` already filters candidates by the expected set. The explicit
`Contains` checks in §7.5 are a defense-in-depth invariant check; in a correct
implementation they never fire for a plain transport. They can fire only if a
cast target is outside the set, which `ResolveExpr` rejects earlier with
`E_SEM_CAST_TYPE_MISMATCH`.

### 7.7 Type names

Type names are resolved through the transport-type catalog:

```text
target = catalog.Resolve(typeName)
if target is unknown:
    error E_SEM_UNKNOWN_TRANSPORT_TYPE at type token offset
```

Type names are case-insensitive. The canonical spelling is the catalog's exact
spelling and is used by the printer.

### 7.8 Struct resolution

```text
ResolveStruct(astStruct, schema, paramName):
    bound = dictionary<StructField, AstValue>

    # 1. Bind source fields in source order.
    for each field in astStruct.Fields in source order:
        name = normalize(field.Name)
        schemaField = schema.Fields.Find(name)
        if schemaField is null:
            error E_SEM_UNKNOWN_STRUCT_FIELD at field.NameToken.Offset
                  expected = schema field names
        if bound.Contains(schemaField):
            error E_SEM_DUPLICATE_STRUCT_FIELD at field.NameToken.Offset
        bound.add(schemaField, field.Value)

    # 2. Missing required fields.
    for each schemaField in schema.Fields in schema order:
        if not bound.Contains(schemaField) and schemaField.Required:
            error E_SEM_MISSING_REQUIRED_STRUCT_FIELD
                  at astStruct.CloseBraceOffset
                  field = schemaField.Name, parameter = paramName

    # 3. Validate provided fields in schema order.
    result = []
    for each schemaField in schema.Fields in schema order:
        if not bound.Contains(schemaField): continue
        value = bound[schemaField]

        switch schemaField:
            StringField:
                if value is not AstScalar:
                    error E_SEM_STRUCT_FIELD_EXPECTED_SCALAR at value.Span.Start
                          field = schemaField.Name, actual = Shape(value)
                reason = schemaField.Validate(value.Text)
                if reason is not null:
                    error E_SEM_VALIDATOR_FAILED at value.Span.Start
                          context = field '<field>' of parameter '<paramName>'
                          reason
                result.add(StringFieldValue(schemaField, value.Text))

            NestedStructField:
                if value is not AstStruct:
                    error E_SEM_STRUCT_FIELD_EXPECTED_STRUCT at value.Span.Start
                          field = schemaField.Name, actual = Shape(value)
                nested = ResolveStruct(value, schemaField.Schema, paramName)
                result.add(NestedStructFieldValue(schemaField, nested))

    return result
```

Struct values contain only strings and nested structs. A transport or list in a
struct field is rejected by the scalar/struct shape checks.

### 7.9 Resolution order and determinism

- Resolution is pre-order: a transport is resolved before its parameters.
- Candidate resolution happens before argument binding.
- Binding errors are reported in this order: too many positional, unknown named
  (source order), duplicate named (source order), missing required (declaration
  order, first missing).
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
ResolvedExpr           (abstract)
ResolvedTransport      : ResolvedExpr
ResolvedCast           : ResolvedExpr

ResolvedTransport
{
    ITransportConstructor Constructor;
    TransportType Type;
    IReadOnlyList<ParamBinding> Provided;   // declaration order
    SourceSpan Span;
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
    SourceSpan Span;
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
- `ResolvedCast.Inner.Type != ResolvedCast.Target` (identity casts are collapsed).
- `TransportListValue.Elements` satisfies the parameter's `Min`/`Max`; if
  `Homogeneous`, all element types are equal.
- `StructValue.Fields` are in schema order and contain exactly the provided
  fields. Required fields are present. Field value kinds match the schema.
- All names are canonical: scheme names lowercase, parameter names lowercase,
  struct field names lowercase, type names canonical.
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
is the resolved tree; it uses the constructor/parameter metadata reachable from
that tree. It does not depend on the AST, on `IDescription`, or on the compiler.

If the input tree violates the invariants in §8, the printer throws
`PrinterException` with an `E_PRN_*` code (§13.5). A correct resolver never
produces such a tree.

### 10.1 Expression

```text
PrintExpr(expr):
    if expr is ResolvedCast:
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
        parts.add(PrintValue(param, t.Provided[param]))
        emitted.add(param)

    for each param in t.Constructor.Params in declaration order:
        if param is present in t.Provided and param not in emitted:
            parts.add(param.Name + "=" + PrintValue(param, t.Provided[param]))

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
scalar; if one does, the printer reports `E_PRN_INVALID_SCALAR`.

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
  `nested` nested Struct optional), type `TypeA`.

```text
alpha(beta(x))                          # input positional; other params absent
alpha(beta(x), fast)                    # input and mode positional
alpha(beta(x), timeout=5s)              # input positional; timeout named-only
alpha(beta(x), fast, timeout=5s)        # input, mode positional; timeout named
alpha(beta("a, b"), "x=y", timeout=1s)  # quoted scalars
alpha(beta(x) as TypeB)                 # cast inside an argument
alpha() as TypeB                        # top-level cast
gamma([alpha(), alpha()])               # homogeneous list
structy({name=x, nested={a=1}})         # nested struct
```

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

For every resolved tree `t`:

```text
Parse(Print(t)) == t
```

For every syntactically and semantically valid source `s`:

```text
Print(Parse(Print(Parse(s)))) == Print(Parse(s))
```

For every resolved tree `t`, `Print(t)` is syntactically valid and contains no
raw `CR` or `LF`.

### 11.3 Deliberate canonicalization

The following source differences produce the same canonical output and the same
resolved tree:

- `ALPHA()` and `alpha()`;
- `alpha( beta() ,  x )` and `alpha(beta(), x)`;
- `alpha(beta(), mode=x)` and `alpha(beta(), x)`;
- `alpha(beta(), timeout=1s)` and `alpha(timeout=1s, beta())` (named order);
- `alpha() as TypeA` and `alpha()` (identity cast collapsed);
- `alpha(beta())` and any alias of `alpha` that resolves to the same constructor;
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
| `E_LEX_INVALID_ESCAPE` | Backslash not followed by `"` or `\` | `invalid escape sequence '<sequence>'; only \" and \\ are allowed` | Backslash |
| `E_LEX_RAW_NEWLINE_IN_STRING` | Raw CR or LF inside quotes | `raw newline is not allowed inside a quoted string` | Newline character |
| `E_LEX_INVALID_UNICODE` | Unpaired surrogate | `invalid Unicode scalar value` | Offending code unit |

### 13.2 Syntax errors

Generic token error:

```text
E_SYN_UNEXPECTED_TOKEN:
    expected <expected>, got <token>
```

Expected-string contexts:

| Parser point | `<expected>` |
|---|---|
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
| `E_SYN_TRAILING_COMMA` | Comma immediately before `)`, `]`, or `}` | `trailing comma is not allowed in <context>` | Comma |
| `E_SYN_POSITIONAL_AFTER_NAMED` | Positional argument after a named argument | `positional argument is not allowed after a named argument` | First token of the positional value |
| `E_SYN_CHAINED_CAST` | Second `as` in one expression | `chained casts are not allowed` | Second `as` |
| `E_SYN_NESTED_LIST` | `[` as a list element | `nested transport lists are not allowed` | Nested `[` |
| `E_SYN_TRAILING_INPUT` | Non-EOF token after the top-level expression | `unexpected trailing input` | First extra token |

`<context>` is exactly one of `argument list`, `transport list`, `struct`.

### 13.3 Semantic errors

Formatting helpers used in messages:

- `FormatTypeSet(set)`:
  - `Any` → `any transport type`
  - `Exactly(T)` → `T`
  - `OneOf(...)` → distinct canonical names sorted, joined with ` or `
- `FormatVariants(candidates)` → entries `name (Type)`, sorted by name then type,
  joined with `, `.
- `FormatParamExpectation(params)` → if non-empty: `one of: <names>`, where names
  are parameter names in declaration order, quoted, joined with `, `; if empty:
  `the scheme has no parameters`.
- `FormatFieldExpectation(schema)` → if non-empty: `one of: <names>`, where names
  are field names in schema order, quoted, joined with `, `; if empty:
  `the struct has no fields`.
- `FormatKnownSchemes()` → if non-empty: `known schemes: <list>`, where list is
  canonical names sorted and joined with `, `; if empty:
  `no schemes are registered`.
- `FormatKnownTypes()` → if non-empty: `known types: <list>`, where list is
  canonical names sorted and joined with `, `; if empty:
  `no transport types are registered`.
- `FormatTypes(types)` → distinct canonical names in first-encounter order,
  joined with `, `.

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
| `E_SEM_TRANSPORT_TYPE_NOT_ALLOWED` | Resolved type outside expected set | `transport type <actual> is not allowed here; expected <set>` | Expression start |
| `E_SEM_UNKNOWN_STRUCT_FIELD` | Struct field not in schema | `unknown field '<name>' in struct for parameter '<param>'; <expected>` | Field name token |
| `E_SEM_DUPLICATE_STRUCT_FIELD` | Struct field specified twice | `field '<name>' is specified more than once` | Duplicate field name token |
| `E_SEM_MISSING_REQUIRED_STRUCT_FIELD` | Required struct field absent | `missing required field '<name>' in struct for parameter '<param>'` | Closing `}` |
| `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` | String field received non-scalar | `field '<name>' expects a scalar value, got <shape>` | Value start |
| `E_SEM_STRUCT_FIELD_EXPECTED_STRUCT` | Nested struct field received non-struct | `field '<name>' expects a struct, got <shape>` | Value start |
| `E_SEM_UNKNOWN_TRANSPORT_TYPE` | Cast type not in catalog | `unknown transport type '<name>'; <known>` | Type token |
| `E_SEM_CAST_TYPE_MISMATCH` | Cast target outside expected set | `cast produces transport type <target>, but expected <set>` | Type token |
| `E_SEM_VALIDATOR_FAILED` | `Validate` returned non-null or threw | `parameter '<name>' is invalid: <reason>` | Value start |
| `E_SEM_VALIDATOR_FAILED` | Struct field validator failed | `field '<name>' of parameter '<param>' is invalid: <reason>` | Value start |

For a thrown validator, `<reason>` is exactly `validation failed`.

`<shape>` is exactly one of `scalar`, `transport`, `transport list`, `struct`.

`<known>` is `FormatKnownSchemes()` for unknown schemes and
`FormatKnownTypes()` for unknown transport types.

`<expected>` is `FormatParamExpectation(params)` for unknown parameters and
`FormatFieldExpectation(schema)` for unknown struct fields.

### 13.4 Registration errors

Registration errors use `RegistrationException` and have no offset.

| Code | Condition | Message template |
|---|---|---|
| `E_REG_INVALID_CANONICAL_NAME` | Canonical scheme is not an identifier | `invalid canonical scheme name '<name>'` |
| `E_REG_DUPLICATE_CANONICAL_NAME` | Canonical scheme already registered | `canonical scheme '<name>' is already registered` |
| `E_REG_INVALID_PARAM_NAME` | Parameter name is not an identifier | `invalid parameter name '<name>'` |
| `E_REG_DUPLICATE_PARAM_NAME` | Duplicate parameter name in one constructor | `parameter '<name>' is declared more than once in scheme '<scheme>'` |
| `E_REG_INVALID_ALIAS_NAME` | Alias is not an identifier | `invalid alias name '<name>'` |
| `E_REG_DUPLICATE_ALIAS_FOR_TYPE` | Same alias already registered for the same type | `alias '<name>' for transport type <type> is already registered` |
| `E_REG_EMPTY_TYPE_SET` | `OneOf` has no types | `transport type set must contain at least one type` |
| `E_REG_INVALID_LIST_RANGE` | `Min < 0`, or `Max < Min` | `invalid transport list range: min <min>, max <max>` |
| `E_REG_DUPLICATE_STRUCT_FIELD` | Duplicate struct field name | `struct field '<name>' is declared more than once` |
| `E_REG_INVALID_STRUCT_FIELD_NAME` | Struct field name is not an identifier | `invalid struct field name '<name>'` |
| `E_REG_INVALID_TYPE_NAME` | Transport type name is not an identifier | `invalid transport type name '<name>'` |
| `E_REG_DUPLICATE_TYPE_NAME` | Catalog contains duplicate type name | `transport type name '<name>' is already registered` |

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
| `E_PRN_STRUCT_FIELD_KIND_MISMATCH` | Struct field value kind mismatch | `field '<field>' of struct parameter '<name>' has value kind <actual>, expected <expected>` |
| `E_PRN_INVALID_CAST_TARGET` | Cast target not in catalog | `cast target '<type>' is invalid` |
| `E_PRN_INVALID_SCALAR` | Scalar contains CR/LF or unprintable data | `scalar value contains a character that cannot be printed: U+<code>` |

`<range>` is `<min>..<max>` if `Max` is set, otherwise `at least <min>`.

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

### 14.1 Valid parses

| Source | Resolved meaning |
|---|---|
| `alpha(beta(x))` | `alpha` TypeA; input = beta |
| `alpha(beta(x), fast)` | input and mode positional |
| `alpha(beta(x), timeout=5s)` | input positional, timeout named |
| `alpha(beta(x), fast, timeout=5s)` | input + mode positional, timeout named |
| `outer(alpha(x))` | inner `alpha` resolves to alias `beta` because expected is TypeB |
| `alpha() as TypeB` | inner TypeA, cast to TypeB |
| `alpha() as TypeA` | identity cast collapses to `alpha()` |
| `gamma([alpha(), alpha()])` | homogeneous TypeA list |
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
| `alpha(q=1)` | `E_SEM_UNKNOWN_PARAM` |
| `alpha(beta(), input=beta())` | `E_SEM_DUPLICATE_PARAM` |
| `alpha(beta(), q=1)` | `E_SEM_UNKNOWN_PARAM` |
| `alpha(1)` | `E_SEM_EXPECTED_TRANSPORT` |
| `beta([alpha()])` | `E_SEM_EXPECTED_SCALAR` |
| `gamma([alpha()])` | `E_SEM_LIST_MIN` |
| `gamma([alpha(), beta()])` | `E_SEM_LIST_HOMOGENEITY` |
| `structy({name=x, extra=1})` | `E_SEM_UNKNOWN_STRUCT_FIELD` |
| `structy({name=x, name=y})` | `E_SEM_DUPLICATE_STRUCT_FIELD` |
| `structy({})` | `E_SEM_MISSING_REQUIRED_STRUCT_FIELD` |
| `structy({name=alpha()})` | `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` |
| `structy({name={}})` | `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` |
| `alpha() as TypeZ` | `E_SEM_UNKNOWN_TRANSPORT_TYPE` |
| `outer(alpha() as TypeC)` | `E_SEM_CAST_TYPE_MISMATCH` |
| `beta(x) as TypeA` | `E_SEM_CAST_TYPE_MISMATCH` when expected is TypeB |
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
| `alpha(input=beta(x) as TypeB)` | `alpha(beta(x) as TypeB)` |
| `gamma(items=[alpha(), alpha()])` | `gamma([alpha(), alpha()])` |
| `structy(config={name=x, nested={a=1}})` | `structy({name=x, nested={a=1}})` |
| `beta(value="a, b")` | `beta("a, b")` |
| `beta(value="")` | `beta("")` |
| `beta(value="x=y")` | `beta("x=y")` |
| `beta(value="plain")` | `beta(plain)` |
| `alpha(input=alpha(x) as TypeA)` where inner `alpha` is TypeA | `alpha(alpha(x))` (identity cast removed) |

### 14.5 Printer invariant errors

Constructing a resolved tree with a missing required parameter, unknown
parameter, wrong value kind, list arity violation, mixed homogeneous list, unknown
struct field, missing required struct field, or invalid cast target must produce
the corresponding `E_PRN_*` error. A correct parser never produces these trees.

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
