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
- **Compiler** — a consumer of a resolved tree. The generic contract is
  `ICompiler<T>` in `Pontifex.TransportFormula.Compilation`. The runtime factory
  provides `TransportCompiler : ICompiler<ITransport>`, which builds live
  transports through the core registration contract. The parser and printer do
  not depend on either implementation.
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
  └─ 4. Compiler          → live transport tree (compiler/build errors)

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
LBrace, RBrace, Comma, Equals, EndOfInput
```

```csharp
sealed record FormulaToken(
    FormulaTokenKind Kind,
    string Text,          // decoded text for QuotedString; raw text for Word
    string RawText,       // raw source slice, retained for diagnostics
    SourceSpan Span);     // Start + Length; End => Start + Length
```

`FormulaTokenKind` is `Word`, `QuotedString`, `LParen`, `RParen`, `LBracket`,
`RBracket`, `LBrace`, `RBrace`, `Comma`, `Equals`, `EndOfInput`.

For `EndOfInput`, `Span.Start == Span.End == input length`.

Punctuation characters map one-to-one:

```text
(  LParen      )  RParen
[  LBracket    ]  RBracket
{  LBrace      }  RBrace
,  Comma       =  Equals
```

Whitespace outside quoted strings is skipped and never appears in a `Word` or
punctuation token; it may appear inside a `QuotedString`. The whitespace characters
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
        is the backslash plus the following Unicode scalar value (a valid
        surrogate pair counts as one scalar; an unpaired surrogate counts as one
        code unit), rendered with the escaping rules from §3.4.
   e. Any other code unit is appended to the decoded value.
3. EOF before the closing quote → `E_LEX_UNTERMINATED_STRING` at the opening
   quote.
4. The token's decoded text is the content without the surrounding quotes.

A `QuotedString` token preserves its raw source slice for diagnostics in
`RawText`.

### 3.4 Token display

For deterministic error messages, define `displayToken(token)`:

- `EndOfInput` → `<end of input>`.
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
surrogate code unit → \uXXXX
```

If the escaped text is longer than 40 UTF-16 code units, truncate it to the
longest prefix that is at most 40 code units and ends at an escape-unit
boundary. Never cut inside `\\`, `\"`, `\r`, `\n`, `\t`, or `\uXXXX`, or between
the two escapes of a surrogate pair. Append `...`.

Lists of expected tokens or names are joined with ` or ` in the order defined by
the relevant rule. Sets of names are sorted lexicographically for determinism.

---

## 4. AST

The AST represents syntax only. It contains no schemes, parameter bindings,
types, or validation results.

```text
SourceSpan        { int Start; int Length; int End => Start + Length; }
SpanFrom(start, end) => SourceSpan { Start = start; Length = end - start }

AstFormula        { AstExpr Root; SourceSpan Span; }

AstValue          (abstract)  { SourceSpan Span; }
AstExpr           : AstValue   { AstTransportCall Call; AstCast? Cast; }
AstList           : AstValue   { IReadOnlyList<AstValue> Elements;
                                 int OpenOffset; int CloseOffset; }
AstStruct         : AstValue   { IReadOnlyList<AstStructField> Fields;
                                 int OpenOffset; int CloseOffset; }
AstScalar         : AstValue   { string Text; bool WasQuoted; }

AstTransportCall  { string Scheme; SourceSpan SchemeSpan;
                    IReadOnlyList<AstArgument> Arguments; int CloseParenOffset;
                    SourceSpan Span; }

AstCast           { string TypeName; SourceSpan TypeSpan; SourceSpan Span; }

AstArgument       { string? Name; SourceSpan? NameSpan; AstValue Value;
                    SourceSpan Span; }

AstStructField    { string Name; SourceSpan NameSpan; AstValue Value;
                    SourceSpan Span; }
```

AST invariants:

- `AstExpr.Cast` is `null` or one cast; there is no chain.
- `AstArgument.Name` is `null` for positional arguments and non-null for named
  arguments. Named arguments form a suffix; the parser enforces this.
- `AstList.Elements` contains `AstExpr`, `AstScalar`, or `AstStruct` values;
  nested lists are never present.
- `AstStruct.Fields` are named; field names are identifier tokens.
- No AST node contains resolved types, constructors, or validators.
- `Span` covers the full source range of the node and is used for semantic
  errors. Every `AstValue` node carries a span. `AstList` also records the
  opening and closing bracket offsets; `AstStruct` records the opening brace and
  the closing brace offsets.
- Scheme, argument-name, and field-name strings are stored exactly as written;
  normalization happens during resolution. The corresponding token spans are
  retained for diagnostics.

---

## 5. Parser

The parser consumes the token stream produced in §3 and produces an `AstExpr`.
It MUST reject every input that does not match the grammar in `grammar.md`.

### 5.1 Top level

```text
Parse(input):
    tokens = Lex(input)                 # lexical errors abort
    if tokens.current is EndOfInput:
        error E_SYN_EMPTY_INPUT at offset 0
    if tokens.current is not Word or not IsIdentifier(current.Text):
        error E_SYN_UNEXPECTED_TOKEN
              expected "a transport expression"
              at current.Offset
    expr = ParseExpr()
    if tokens.current is not EndOfInput:
        error E_SYN_TRAILING_INPUT at tokens.current.Offset
    return AstFormula(expr, expr.Span)
```

The top level is exactly one expression. A top-level list, struct, scalar, or
quoted string is not an expression and is rejected as shown above.

`Parse` performs syntax only and returns `FormulaParseResult`. On success,
`FormulaParseResult.Formula` is an `AstFormula` whose `Root` is the parsed
`AstExpr` and whose `Span` is the root span; on failure, `FormulaParseResult.Error`
carries the `FactoryError` and `Formula` is `null`. `Parse` does not throw for
user input. It does not take a registry or catalog and does not resolve anything.

### 5.2 Expressions and casts

```text
ParseExpr():
    call = ParseTransportCall()
    if tokens.current is Word equal to "as" (case-insensitive):
        asToken = consume()
        typeToken = ParseTypeName()
        if tokens.current is Word equal to "as" (case-insensitive):
            error E_SYN_CHAINED_CAST at tokens.current.Offset
        cast = AstCast(typeToken.Text, SpanFrom(typeToken.Start, typeToken.End),
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
    return AstTransportCall(schemeToken.Text, SpanFrom(schemeToken.Start, schemeToken.End),
                            args, closeParen.Offset,
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
            args.add(AstArgument(nameToken.Text, SpanFrom(nameToken.Start, nameToken.End), value,
                                 SpanFrom(nameToken.Start, value.Span.End)))
            seenNamed = true
        else:
            if seenNamed:
                error E_SYN_POSITIONAL_AFTER_NAMED at current.Offset
            value = ParseValue()
            args.add(AstArgument(null, null, value, value.Span))

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
                  message "nested lists are not allowed"

        if not CanStartValue(tokens.current):
            error E_SYN_UNEXPECTED_TOKEN
                  expected "a value"
                  at current.Offset
        elements.add(ParseValue())

        if tokens.current is Comma:
            comma = consume()
            if tokens.current is RBracket:
                error E_SYN_TRAILING_COMMA context "list"
                      at comma.Offset
            continue

        if tokens.current is RBracket:
            close = consume()
            return AstList(elements, open.Offset, close.Offset,
                           SpanFrom(open.Start, close.End))

        error E_SYN_UNEXPECTED_TOKEN expected "',' or ']'" at current.Offset
```

List elements are generic values: transport expressions, structs, quoted
strings, or bare scalars. Nested lists are rejected syntactically. The parser
does not decide whether a list is a transport list or a data array; that is a
semantic decision made from the parameter or field kind.

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
        fields.add(AstStructField(nameToken.Text, SpanFrom(nameToken.Start, nameToken.End), value,
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
sealed record TransportConstructorInfo(
    string Name,                            // canonical scheme
    TransportType Type,
    IReadOnlyList<Param> Params,            // declaration order
    IReadOnlyList<string> SuggestedAliases); // optional hints
```

- `Name` MUST be an identifier and is normalized to lowercase. The registry
  stores a normalized metadata view; resolvers and the printer see canonical
  names, not the caller's original spelling.
- Canonical `Name` values MUST be unique within a registry.
- `Params` MAY be empty. Parameter names MUST be identifiers, normalized to
  lowercase, and unique within the constructor.
- `Type` is the fixed transport type produced by the constructor. Every
  constructor has one fixed type.

`TransportConstructorInfo` here is the formula registry's normalized metadata
view. Transport packages do not implement it directly; they implement the core
registration contract in §6.9, and the factory adapter produces this view.

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

sealed class IntegerParam : Param
{
    IntegerType Type;     // inclusive Min/Max; long bounds
}

sealed class DecimalParam : Param
{
    DecimalType Type;     // inclusive Min/Max; decimal bounds
}

// IntegerType { long Min; long Max; }
// DecimalType { decimal Min; decimal Max; }

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

Integer parameters use `Int64` only. There is no `UInt64` scalar kind. A scalar
literal for an integer parameter MUST parse as a signed 64-bit integer with
`NumberStyles.AllowLeadingSign` in the invariant culture; otherwise the value is
a parse failure. A successfully parsed value outside `IntegerType.Min`/`Max` is
an out-of-range failure.

A decimal parameter accepts a sign, decimal point, and exponent. Parsing is
exact: the literal MUST be exactly representable as a `System.Decimal`, including
its 96-bit coefficient and 0..28 scale. Overflow, underflow, and precision loss
are parse failures, not out-of-range values. A successfully parsed value outside
`DecimalType.Min`/`Max` is an out-of-range failure. Canonical decimal output
never uses exponent notation, trims trailing zeros, and normalizes negative zero
to `0`. Consequently `1.50` and `1.5` compare equal and both print as `1.5`.
Quoted numeric scalars are accepted; the decoded text is parsed identically.
Leading or trailing whitespace inside numeric text is not accepted. Accepted
forms include `+1`, `-0`, `.5`, `1.`, and zero with any exponent (for example
`0e29` or `0e-1000001`); a zero coefficient makes the exponent irrelevant.

### 6.3 Type sets

```csharp
enum TransportTypeSetKind { Any, Exactly, OneOf }

sealed record TransportTypeSet(
    TransportTypeSetKind Kind,
    IReadOnlyList<TransportType> Types)
{
    bool IsAny { get; }                     // Kind == Any
    bool Contains(TransportType type);
}
```

- `Kind = Any` contains every transport type and has `IsAny = true` and an empty
  `Types`.
- `Kind = Exactly` contains only the single listed type.
- `Kind = OneOf` contains the listed types.
- The formula DTO does not normalize or validate on construction. Registration
  normalizes duplicates away, and `OneOf` or `Exactly` with zero types is rejected
  at registration with `E_REG_EMPTY_TYPE_SET`.
- The core registration contract (§6.9) exposes a value-type `TransportTypeSet`
  with `Any`, `Exactly(...)`, and `OneOf(...)` helpers. `OneOf(...)` with zero
  elements throws there; the factory adapter then produces the formula view.

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
- Integer and decimal parse and range failures reuse `E_SEM_VALIDATOR_FAILED`
  with reasons `expected an integer`, `expected a decimal`, and
  `out of range [<min>, <max>]`; they do not use a `ScalarValidator`.
- Decimal range bounds use the canonical decimal text defined in §6.2.

Define `InvokeValidator(validator, text)`:

```text
if validator is null: return null
try:
    result = validator(text)
    if result is null: return null
    return result is empty ? "validation failed" : result
catch:
    return "validation failed"
```

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

sealed class IntegerField : StructField
{
    IntegerType Type;
}

sealed class DecimalField : StructField
{
    DecimalType Type;
}

sealed class ArrayField : StructField
{
    SingleType ElementType;   // StringType, IntegerType, or DecimalType
    int Min;
    int? Max;                 // null = unbounded
}

sealed class NestedStructField : StructField
{
    StructSchema Schema;
}

abstract class SingleType
{
    // closed hierarchy: StringType, IntegerType, DecimalType
}

sealed class StringType : SingleType
{
    ScalarValidator? Validate;
}
```

Struct fields are named-only. There are no positional fields, no defaults, and no
transport or transport-list fields. Integer and decimal fields use the same
exact parsing and range rules as parameters. Array fields are homogeneous and
contain only string, integer, or decimal scalar leaves; nested arrays and struct
elements are rejected. A `StringType` validator applies per element. `ArrayField`
carries its own count range; there is no separate `ListRange` DTO.

### 6.6 Type catalog

The resolver uses a transport-type catalog:

```csharp
sealed record TransportTypeInfo(
    TransportType Type,
    string CanonicalName);

interface ITransportTypeCatalog
{
    IReadOnlyList<TransportTypeInfo> Types { get; }
    IReadOnlyList<TransportType> AllTypes { get; }
    bool TryResolve(string name, out TransportTypeInfo? type);
    bool TryGetCanonicalName(TransportType type, out string? canonicalName);
    string GetCanonicalName(TransportType type);
}
```

- Lookup is ASCII case-insensitive.
- `TryResolve` returns `true` and a `TransportTypeInfo` (`Type` and
  `CanonicalName`) when the name is known, otherwise `false` and `null`.
- `TryGetCanonicalName` returns `false` when the type is absent from the catalog.
  `GetCanonicalName` MUST only be called for a type present in the catalog.
- `canonicalName` is the exact spelling printed by the canonical printer.
- Catalog names MUST be identifiers and MUST be unique case-insensitively.
- `Types` is the complete set of catalog entries in registration order, each with
  its canonical name. `AllTypes` is the same set in the same order, projected to
  `TransportType`.
- Every `TransportType` used by a registered constructor or in an
  `Expected`/`OneOf` set MUST have a canonical name in the catalog. Registration
  rejects a type that is absent from the catalog.
- The grammar does not enumerate concrete types; the catalog is configuration.

### 6.7 Registration

```csharp
void RegisterType(TransportTypeInfo type);
void RegisterTransport(TransportRegistration registration);

sealed record TransportRegistration(
    TransportConstructorInfo Constructor,
    IReadOnlyList<string> Aliases);
```

Registration is performed on an `IRegistryBuilder`; `Build()` returns the
immutable `IRegistry`.

Rules:

0. Validate each registered transport type when `RegisterType` is called: names
   are identifiers and unique case-insensitively (`E_REG_INVALID_TYPE_NAME`,
   `E_REG_DUPLICATE_TYPE_NAME`). Catalog entries must be registered before the
   transports that reference them.
1. Normalize the canonical name, aliases, parameter names, and all nested
   struct-field names to lowercase.
2. Validate canonical name, parameter names, and alias names as identifiers.
3. Reject duplicate canonical names.
4. Reject duplicate parameter names within a constructor.
5. Normalize all `TransportTypeSet` values in declaration order; reject an empty
   `OneOf` or an empty `Exactly` set as `E_REG_EMPTY_TYPE_SET`. Then validate
   `IntegerParam`/`DecimalParam` ranges in declaration order: `Min <= Max`
   (`E_REG_INVALID_INTEGER_RANGE`, `E_REG_INVALID_DECIMAL_RANGE`).
6. Validate `TransportListParam`: `Min >= 0`; `Max == null` or `Max >= Min`.
7. Validate struct schemas recursively: field names are valid identifiers and
   unique within their schema. Field kinds are constrained by the
   `StringField`/`IntegerField`/`DecimalField`/`ArrayField`/`NestedStructField`
   metadata types. For each field, after its name, uniqueness, and kind, validate
   an array count range (`E_REG_INVALID_ARRAY_RANGE`), then an array element
   scalar range, then an integer or decimal field range, then recurse into a
   nested schema. Validation visits each distinct `StructSchema` instance once
   using a visited set; shared or cyclic schema references are permitted and are
   not revalidated.
8. Normalize aliases; duplicates within the same registration are ignored. An
   alias equal to the constructor's own canonical name is ignored.
9. This rule applies only to alias-to-constructor mappings registered for the
   same alias name. The canonical constructor of a scheme is not an alias mapping.
   An alias may therefore target a constructor whose `Type` equals the canonical
   constructor's `Type` for the same scheme; canonical preference resolves that
   overlap at resolution. An alias name may not be registered twice for the same
   `Type` by different constructors.
10. `SuggestedAliases` are normalized with ASCII uppercase-to-lowercase folding
    and are not automatically registered. A host MAY pass them (or any other
    aliases) to `RegisterTransport`. Suggested aliases are not validated as
    identifiers.
11. Reject a constructor `Type` or any `Expected` member that is absent from the
    type catalog. Within this rule, the constructor type is checked first, then
    each `Expected` member in parameter declaration order.
12. Registration after `Build()` is forbidden. `RegisterType` and
    `RegisterTransport` throw `RegistrationException` with
    `E_REG_BUILDER_SEALED`; a built registry is immutable.

Registration is incremental: rules 1–11 are evaluated on each
`RegisterTransport` call in the numbered order, and the first failure for that
call is reported. Rule 12 is a lifecycle guard, not a validation rule: it is
evaluated before every other rule on every registration call. A transport that
references a type not yet registered fails rule 11 at that point.
Within rule 2, check the canonical name first, then parameter names in declaration
order, then alias names in input order. Struct schemas are validated by rule 7:
visit fields in schema order and validate the name as an identifier before
uniqueness and kind. Registration errors are listed in §13.4. They occur when
registration is attempted, not while parsing.

### 6.8 Registry lookup

```csharp
interface IRegistry
{
    ITransportTypeCatalog Types { get; }
    IReadOnlyList<TransportConstructorInfo> Constructors { get; }
    IReadOnlyList<string> AllSchemeNames { get; }
    TransportConstructorInfo? FindCanonical(string normalizedScheme);
    IReadOnlyList<TransportConstructorInfo> FindCandidates(string normalizedScheme);
}
```

- `FindCanonical(scheme)` returns the constructor whose canonical `Name` equals
  the normalized scheme, or `null`.
- `FindCandidates(scheme)` returns the canonical constructor first (when one
  exists), followed by every constructor that registered the normalized scheme as
  an alias, sorted by constructor name and then canonical type name. The result
  is empty when the scheme is unknown.
- `Constructors` is the frozen list in registration order.
- `AllSchemeNames` is the sorted union of all canonical and alias names.
- Both lookup methods require an already-normalized lowercase scheme; they do not
  normalize, filter by expected type, or apply canonical preference.
- `FindByName(list, normalizedName)` performs a case-insensitive lookup over
  normalized names in `Params`, `StructSchema.Fields`, or equivalent lists and
  returns `null` when no entry matches. It is a helper, not part of the registry
  interface.

### 6.9 Core transport registration contract

The transport-facing registration contract is defined in the core `Pontifex`
assembly under `Pontifex.Factory.Registration`. It is the only metadata contract
that transport packages reference; the formula registry is an adapter view of it.

```csharp
interface ITransportRegistration
{
    string Name { get; }
    TransportType Type { get; }
    IReadOnlyList<TransportParameter> Parameters { get; }
    IReadOnlyList<string> Aliases { get; }
    ITransport Create(ITransportBuildContext context);
}
```

- `Name`, `Type`, `Parameters`, and `Aliases` map to the formula
  `TransportConstructorInfo` metadata in §6.1–§6.5.
- The factory adapter normalizes names to lowercase; the core DTO does not
  require pre-normalized input.
- `Aliases` are proposed aliases. The factory host registers them; the core DTO
  does not register anything by itself.
- `Create` is invoked only by the direct compiler (§9). The formula parser,
  resolver, and printer never call it.
- `TransportParameter` and its subclasses mirror `Param`; `TransportTypeSet`,
  `StructSchema`, `ScalarValidator`, `IntegerType`, `DecimalType`, `SingleType`,
  and `StringType` mirror their formula counterparts. Core
  `IntegerType`/`DecimalType`, `IntegerParameter`/`DecimalParameter`,
  `IntegerField`/`DecimalField`, and `ArrayField` accept unsorted bounds and do
  not throw; the formula registry owns range validation. The one exception is
  core `TransportListParameter`, whose constructor rejects `Min < 0` and
  `Max < Min` with `ArgumentOutOfRangeException`. Consequently
  `E_REG_INVALID_LIST_RANGE` is reachable only by constructing the formula DTO
  directly; the core adapter cannot produce it.
- `ITransportBuildContext` exposes `TryGetString`, `TryGetInt64`,
  `TryGetDecimal`, `TryGetTransport`, `TryGetTransportList`, and `TryGetStruct`.
  `ITransportStructValue` exposes `TryGetString`, `TryGetInt64`,
  `TryGetDecimal`, `TryGetStruct`, and `TryGetArray`. `ITransportArrayValue`
  exposes `Count` and indexed `TryGetString`/`TryGetInt64`/`TryGetDecimal`.
- A transport package implementing this contract depends only on `Pontifex`.

The runtime adapter in `Pontifex.Factory` converts the core DTOs to the formula
`TransportRegistration` and registers the same object with the runtime
constructor registry. There is no second, independently maintained metadata
definition.

---

## 7. Resolution

Resolution consumes a syntactically valid AST, registry metadata, and the type
catalog, and produces a resolved tree. It performs all semantic checks.

Entry points:

```text
Parse(input)                                  -> FormulaParseResult
Resolve(astFormula, expected)                 -> FormulaResolveResult
```

`IResolver` is constructed with an `IRegistry`; `Resolve` takes the parsed
`AstFormula` and an expected `TransportTypeSet` and returns `FormulaResolveResult`,
which carries either a `ResolvedFormula` (with `Root`) or a `FactoryError`. The
formula assembly does not define a `ParseAndResolve` convenience method; compose
`Parse` and `Resolve`, or use the `Pontifex.Factory` facade. A caller that wants
the default top-level behaviour passes a `TransportTypeSet` with `Kind = Any`.
In the algorithms below, `span(x)` is `x.Span`, and `normalize(s)` applies ASCII
uppercase-to-lowercase folding for identifiers.

An `IParser` instance is reusable sequentially. Its implementation keeps a
cursor as instance state, so it is not thread-safe; callers must either
serialize calls to one instance or use a separate instance per concurrent
operation.

### 7.1 Expected types

Every expression is resolved with an **expected type set**:

- At top level: the caller-supplied expected set; the default is
  `TransportTypeSet` with `Kind = Any`.
- For a `TransportParam`: the parameter's `Expected`.
- For each element of a `TransportListParam`: the parameter's `Expected`.
- For the inner transport of a cast: `TransportTypeSet.Any`.
- Struct fields have no transport expected type.

### 7.2 Expressions and casts

```text
ResolveExpr(ast, expected):
    if ast.Cast is null:
        return ResolveTransport(ast.Call, expected)

    if not catalog.TryResolve(ast.Cast.TypeName, out typeInfo):
        error E_SEM_UNKNOWN_TRANSPORT_TYPE at ast.Cast.TypeSpan.Start
    target = typeInfo.Type

    if not expected.Contains(target):
        error E_SEM_CAST_TYPE_MISMATCH at ast.Cast.TypeSpan.Start
              target = target, expected = expected

    inner = ResolveTransport(ast.Call, TransportTypeSet.Any)

    if inner.Type == target:
        return inner with Span = span(ast)   # identity cast collapses,
                                             # retaining the full cast span

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
    allCandidates = registry.FindCandidates(scheme)   # canonical first, then aliases

    if allCandidates is empty:
        error E_SEM_UNKNOWN_SCHEME at call.SchemeSpan.Start

    candidates = allCandidates
    if not expected.IsAny:
        candidates = allCandidates.Where(c => expected.Contains(c.Type))
        if candidates is empty:
            error E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE
                  at call.SchemeSpan.Start
                  available = FormatVariants(allCandidates)

    canonical = registry.FindCanonical(scheme)
    if canonical is not null and candidates.Contains(canonical):
        candidates = { canonical }

    if candidates.Count > 1:
        error E_SEM_AMBIGUOUS_SCHEME at call.SchemeSpan.Start

    constructor = candidates.Single()
    bind arguments (§7.4)
    validate parameters in declaration order (§7.5)
    return ResolvedTransport(constructor, provided, span(call))
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
- Within one alias group — that is, within the aliases returned by
  `registry.FindCandidates(scheme)` after the canonical entry — all
  candidate types are distinct (rule 9 of §6.7). The canonical constructor may
  carry the same `Type` as an alias; canonical preference resolves that overlap
  deterministically.

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
    param = FindByName(constructor.Params, name)
    if param is null:
        error E_SEM_UNKNOWN_PARAM at arg.NameSpan.Start
    if bound.Contains(param):
        error E_SEM_DUPLICATE_PARAM at arg.NameSpan.Start
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
            reason = InvokeValidator(param.Validate, value.Text)
            if reason is not null:
                error E_SEM_VALIDATOR_FAILED at value.Span.Start
                      context = parameter '<param.Name>', reason
            store StringValue(value.Text)

        IntegerParam:
            if value is not AstScalar:
                error E_SEM_EXPECTED_SCALAR at value.Span.Start
                      actual = Shape(value)
            if not TryParseInt64(value.Text, invariant, AllowLeadingSign):
                error E_SEM_VALIDATOR_FAILED at value.Span.Start
                      context = parameter '<param.Name>',
                      reason = "expected an integer"
            if parsed < param.Type.Min or parsed > param.Type.Max:
                error E_SEM_VALIDATOR_FAILED at value.Span.Start
                      context = parameter '<param.Name>',
                      reason = "out of range [<min>, <max>]"
            store IntegerValue(parsed)

        DecimalParam:
            if value is not AstScalar:
                error E_SEM_EXPECTED_SCALAR at value.Span.Start
                      actual = Shape(value)
            if not TryParseExactDecimal(value.Text):
                error E_SEM_VALIDATOR_FAILED at value.Span.Start
                      context = parameter '<param.Name>',
                      reason = "expected a decimal"
            if parsed < param.Type.Min or parsed > param.Type.Max:
                error E_SEM_VALIDATOR_FAILED at value.Span.Start
                      context = parameter '<param.Name>',
                      reason = "out of range [<min>, <max>]"
            store DecimalValue(parsed)

        TransportParam:
            if value is not AstExpr:
                error E_SEM_EXPECTED_TRANSPORT at value.Span.Start
                      actual = Shape(value)
            resolved = ResolveExpr(value, param.Expected)
            store resolved

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
                if element is not AstExpr:
                    error E_SEM_LIST_ELEMENT_TYPE at element.Span.Start
                          actual = Shape(element)
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
"list"            for AstList
"struct"          for AstStruct
```

### 7.6 Expected-type guarantees

`ResolveExpr` filters candidates by the expected set and rejects cast targets
outside that set. Therefore every resolved result passed to a transport
parameter is guaranteed to be in the parameter's expected set. There is no
user-visible `E_SEM_*` error for an expected-type mismatch after resolution: a
violation is a resolved-tree invariant. A compiler that performs the optional
checks in §9.5 reports it as `E_CMP_INCONSISTENT_TREE`.

### 7.7 Type names

Type names are resolved through the transport-type catalog:

```text
if not catalog.TryResolve(typeName, out typeInfo):
    error E_SEM_UNKNOWN_TRANSPORT_TYPE at type token offset
target = typeInfo.Type
canonicalName = typeInfo.CanonicalName
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
        schemaField = FindByName(schema.Fields, name)
        if schemaField is null:
            path = JoinPath(parentPath, field.Name)       # raw leaf
            error E_SEM_UNKNOWN_STRUCT_FIELD at field.NameSpan.Start
                  path = path, expected = schema field names
        path = JoinPath(parentPath, schemaField.Name)     # canonical segment
        if bound.Contains(schemaField):
            error E_SEM_DUPLICATE_STRUCT_FIELD at field.NameSpan.Start
                  path = path
        bound.add(schemaField, field.Value)

    # 2. Missing required fields.
    for each schemaField in schema.Fields in schema order:
        if not bound.Contains(schemaField) and schemaField.Required:
            path = JoinPath(parentPath, schemaField.Name)
            error E_SEM_MISSING_REQUIRED_STRUCT_FIELD
                  at astStruct.CloseOffset
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
                reason = InvokeValidator(schemaField.Validate, value.Text)
                if reason is not null:
                    error E_SEM_VALIDATOR_FAILED at value.Span.Start
                          context = field '<path>' of parameter '<paramName>'
                          reason
                result.add(StructFieldBinding(
                    schemaField, StringValue(value.Text)))

            IntegerField:
                if value is not AstScalar:
                    error E_SEM_STRUCT_FIELD_EXPECTED_SCALAR at value.Span.Start
                          path = path, actual = Shape(value)
                parse exactly as an integer; on parse failure:
                    error E_SEM_VALIDATOR_FAILED at value.Span.Start
                          context = field '<path>' of parameter '<paramName>'
                          reason = "expected an integer"
                on range failure:
                    error E_SEM_VALIDATOR_FAILED at value.Span.Start
                          context = field '<path>' of parameter '<paramName>'
                          reason = "out of range [<min>, <max>]"
                result.add(StructFieldBinding(
                    schemaField, IntegerValue(parsed)))

            DecimalField:
                if value is not AstScalar:
                    error E_SEM_STRUCT_FIELD_EXPECTED_SCALAR at value.Span.Start
                          path = path, actual = Shape(value)
                parse exactly as a decimal; on parse failure:
                    error E_SEM_VALIDATOR_FAILED at value.Span.Start
                          context = field '<path>' of parameter '<paramName>'
                          reason = "expected a decimal"
                on range failure:
                    error E_SEM_VALIDATOR_FAILED at value.Span.Start
                          context = field '<path>' of parameter '<paramName>'
                          reason = "out of range [<min>, <max>]"
                result.add(StructFieldBinding(
                    schemaField, DecimalValue(parsed)))

            ArrayField:
                if value is not AstList:
                    error E_SEM_STRUCT_FIELD_EXPECTED_ARRAY at value.Span.Start
                          path = path, actual = Shape(value)
                count = value.Elements.Count
                if count < field.Min:
                    error E_SEM_ARRAY_MIN at value.OpenOffset
                if field.Max is not null and count > field.Max:
                    error E_SEM_ARRAY_MAX at value.OpenOffset
                elements = []
                for index in 0 .. count-1 in order:
                    element = value.Elements[index]
                    elementPath = "<path>[<index>]"
                    if element is not AstScalar:
                        error E_SEM_ARRAY_ELEMENT_TYPE at element.Span.Start
                              path = elementPath
                              parameter = paramName
                              expected = "a string" | "an integer" | "a decimal"
                              actual = Shape(element)
                    switch field.ElementType:
                        StringType:
                            reason = InvokeValidator(type.Validate, element.Text)
                        IntegerType:
                            reason = exact int64 parse and range check
                        DecimalType:
                            reason = exact decimal parse and range check
                    if reason is not null:
                        error E_SEM_VALIDATOR_FAILED at element.Span.Start
                              context = field '<elementPath>' of parameter '<paramName>'
                    elements.add(scalar value)
                result.add(StructFieldBinding(
                    schemaField, ArrayValue(elements)))

            NestedStructField:
                if value is not AstStruct:
                    error E_SEM_STRUCT_FIELD_EXPECTED_STRUCT at value.Span.Start
                          path = path, actual = Shape(value)
                nested = ResolveStruct(value, schemaField.Schema, paramName, path)
                result.add(StructFieldBinding(
                    schemaField, StructValue(nested)))

    return result
```

`JoinPath(parentPath, name)` joins the segments with `.`; an empty parent path
yields `name`. Struct values contain only strings, integers, decimals, arrays,
and nested structs. A transport in a struct field, or a list in a non-array
field, is rejected by the scalar/struct/array shape checks.

### 7.9 Resolution order and determinism

- For a cast expression, the target type is resolved and checked against the
  expected set before the inner transport is resolved with `Any`.
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
  schema order, then field value validation in schema order. Array count is
  checked before elements; elements are validated in index order.
- No backtracking is performed. If a candidate is selected by canonical/alias
  rules, it is final. A homogeneity failure is not resolved by retrying a
  different candidate.

---

## 8. Resolved tree

```text
ResolvedValue               (abstract)
DataValue                   (abstract) : ResolvedValue
ResolvedExpr                (abstract) : ResolvedValue { TransportType Type; SourceSpan Span; }
ResolvedTransport           : ResolvedExpr
ResolvedCast                : ResolvedExpr

ResolvedTransport
{
    TransportConstructorInfo Constructor;
    IReadOnlyList<ParamBinding> Provided;   // declaration order
    SourceSpan Span;
}

ParamBinding
{
    Param Param;
    ResolvedValue Value;
}

ScalarValue                 (abstract) : DataValue
StringValue                 : ScalarValue { string Text; }
IntegerValue                : ScalarValue { long Value; }
DecimalValue                : ScalarValue { decimal Value; }
StructValue                 : DataValue { IReadOnlyList<StructFieldBinding> Fields; }
ArrayValue                  : DataValue { IReadOnlyList<DataValue> Elements; }
TransportListValue          : ResolvedValue { IReadOnlyList<ResolvedExpr> Elements; }

ResolvedCast
{
    ResolvedTransport Inner;
    TransportType Target;
}

StructFieldBinding
{
    StructField Field;
    DataValue Value;
}
```

Invariants:

- `Provided` contains exactly the parameters that were supplied. Absent
  parameters are absent; no defaults are inserted.
- `Provided` is ordered by `Constructor.Params` declaration order, not source
  order.
- Each `ParamBinding.Param` is reference-equal to the corresponding element of
  `Constructor.Params`; each `StructFieldBinding.Field` is reference-equal to a
  field of the corresponding schema.
- `ResolvedTransport.Type == Constructor.Type`.
- `ResolvedCast.Type == ResolvedCast.Target`.
- `ResolvedCast.Inner.Type != ResolvedCast.Target` (identity casts are collapsed).
- `TransportListValue.Elements` satisfies the parameter's `Min`/`Max`; if
  `Homogeneous`, all element types are equal.
- `StructValue.Fields` are in schema order and contain exactly the provided
  fields, each at most once. Required fields are present. Field value kinds match
  the schema.
- An `IntegerValue` lies within its `IntegerType` range; a `DecimalValue` lies
  within its `DecimalType` range. Decimal equality is numeric: `1.50m` and
  `1.5m` are equal.
- `ArrayValue.Elements` are homogeneous scalar leaves (`StringValue`,
  `IntegerValue`, or `DecimalValue`), satisfy the `ArrayField` count range, and
  satisfy the element type range or string validator. Nested arrays and struct
  elements never occur in a valid tree.
- All names are canonical: scheme names lowercase, parameter names lowercase,
  struct field names lowercase, type names canonical.
- Every `ResolvedExpr`, including every list element and cast inner node, carries
  a `Span` inherited from `ResolvedExpr`.
- For every `ResolvedExpr` bound to a `TransportParam`, the value's `Type` is
  in the parameter's `Expected` set. For every element of a
  `TransportListValue` bound to a `TransportListParam`, the element's `Type` is
  in the parameter's `Expected` set.
- Spans are retained for diagnostics.
- No AST-only information (original alias, source case, quoting style, positional
  versus named form) is retained.

---

## 9. Compiler boundary

The compiler consumes a resolved tree. It is not part of the parser or printer.
Two layers exist:

- `Pontifex.TransportFormula.Compilation.ICompiler<TOutput>` — the generic,
  opaque formula-assembly boundary. The earlier name
  `IResolvedTreeCompiler<TOutput>` maps to this interface; `Compile` takes the
  `ResolvedFormula` wrapper.
- `Pontifex.Factory.Compilation.ITransportCompiler` — the direct runtime
  implementation, `ICompiler<ITransport>`.

The formula assembly MUST NOT reference `ITransport`, `IConvertersGraph`,
`ILogger`, `IMemoryRental`, or concrete transports.

The compiler receives a valid resolved tree. A correct parser/resolver pair never
produces an invalid one. If the compiler detects an invariant violation, it
reports `CompilerException` with an `E_CMP_*` code (§13.6). Build failures report
`BuildException` with an `E_BLD_*` code (§13.6).

### 9.1 Core registration contract

Transport packages register through `Pontifex.Factory.Registration` (§6.9), not
through formula DTOs:

```csharp
interface ITransportRegistration
{
    string Name { get; }
    TransportType Type { get; }
    IReadOnlyList<TransportParameter> Parameters { get; }
    IReadOnlyList<string> Aliases { get; }
    ITransport Create(ITransportBuildContext context);
}
```

The factory adapter maps this to the formula `TransportConstructorInfo` metadata.
`Name` and `Type` identify the constructor; `Parameters` and `Aliases` populate
the registry. `Create` is the only transport-construction entry point.

The build context is core-only:

```csharp
enum TransportRole { Server, Client }
delegate ITransport NestedTransportFactory();

interface ITransportBuildContext
{
    TransportRole Role { get; }
    ILogger Logger { get; }
    IMemoryRental Memory { get; }

    bool TryGetString(string parameterName, out string value);
    bool TryGetInt64(string parameterName, out long value);
    bool TryGetDecimal(string parameterName, out decimal value);
    bool TryGetTransport(string parameterName, out NestedTransportFactory factory);
    bool TryGetTransportList(
        string parameterName,
        out IReadOnlyList<NestedTransportFactory> factories);
    bool TryGetStruct(string parameterName, out ITransportStructValue value);
}

interface ITransportStructValue
{
    bool TryGetString(string fieldName, out string value);
    bool TryGetInt64(string fieldName, out long value);
    bool TryGetDecimal(string fieldName, out decimal value);
    bool TryGetStruct(string fieldName, out ITransportStructValue value);
    bool TryGetArray(string fieldName, out ITransportArrayValue value);
}

interface ITransportArrayValue
{
    int Count { get; }
    bool TryGetString(int index, out string value);
    bool TryGetInt64(int index, out long value);
    bool TryGetDecimal(int index, out decimal value);
}
```

Rules:

- Parameter and field names are canonical lowercase names from the resolved
  metadata.
- `TryGet*` returns `false` when an optional parameter or field is absent.
  Required values are guaranteed by resolution.
- `NestedTransportFactory` compiles the corresponding resolved subtree on each
  invocation. Each invocation produces a fresh transport. This is required by
  reconnect-style transports.
- `TryGetTransportList` preserves element order and returns one factory per
  element.
- A `TryGet*` accessor whose stored value has the wrong value kind reports
  `E_CMP_PARAM_KIND_MISMATCH` with the resolved kinds. A struct-field accessor
  whose stored value has the wrong field kind reports `E_CMP_INCONSISTENT_TREE`.
  Because resolution validates schema kinds, a mismatch can only arise from a
  hand-built or corrupted resolved tree.
- Struct values contain only strings, integers, decimals, arrays, and nested
  structs. A present field whose schema kind is not `ArrayField`, or whose value
  is not an `ArrayValue`, is `E_CMP_INCONSISTENT_TREE` in `TryGetArray`.
- An empty array is valid and has `Count == 0`. An element accessor returns
  `false` for a wrong element kind. An index outside `0..Count-1` throws
  `ArgumentOutOfRangeException` as a programmer error.
- The transport constructor owns every nested transport it creates. If creation
  fails after some nested transports were created, the constructor must stop or
  discard them before rethrowing.
- `Create` is called exactly once per compilation of a resolved transport
  expression. A nested transport factory compiles its subtree afresh when
  invoked, so a second invocation calls `Create` again. The compiler does not
  call `Create` speculatively.

### 9.2 Direct compiler components

The runtime factory assembly defines:

```csharp
interface ITransportCompiler : ICompiler<ITransport>
{
    TransportRole Role { get; }
}

interface ITransportConstructorRegistry
{
    ITransportRegistration? Find(string canonicalScheme, TransportType type);
}
```

The compiler is constructed with the frozen constructor registry, the converter
graph, the logger, the memory rental, and the role. It has no static state.

The constructor registry is keyed by the normalized canonical scheme plus the
core `TransportType`. Duplicate keys are rejected at registration time. The
registry is sealed when the factory is built.

If the optional `TransportTypeId` decoupling is adopted, the compiler also maps
formula type ids to core `TransportType` through a binding table. Until then,
resolved cast targets and constructor types are already core `TransportType`
values.

### 9.3 Compilation algorithm

```text
Compile(formula):
    if formula is null: ArgumentNullException
    if formula.Root is null:
        CompilerException E_CMP_INCONSISTENT_TREE
            "compiler received an inconsistent resolved tree: unspecified invariant violation"
    return CompileExpr(formula.Root)

CompileExpr(expr):
    ResolvedTransport -> CompileTransport(expr)
    ResolvedCast      -> CompileCast(expr)
    other             -> E_CMP_INCONSISTENT_TREE

CompileTransport(t):
    registration = registry.Find(t.Constructor.Name, t.Constructor.Type)
    if registration is null:
        BuildException E_BLD_CONSTRUCTOR_NOT_FOUND
            "transport constructor '<scheme>' is not registered"
    context = TransportBuildContext(t, role, logger, memory, this)
    try:
        return registration.Create(context)
    catch CompilerException:
        rethrow
    catch exception:
        BuildException E_BLD_CONSTRUCTOR_FAILED
            "transport constructor '<scheme>' failed"
            inner = exception

CompileCast(c):
    inner = CompileExpr(c.Inner)
    if inner.Type == c.Target:
        return inner
    convert = converters.TryConvert(
        inner.Type, c.Target, () => inner, memory, logger)
    if convert is null:
        BuildException E_BLD_NO_CONVERSION_PATH
            "no conversion path from <from> to <to>"
    try:
        return convert()
    catch exception:
        BuildException E_BLD_CONSTRUCTOR_FAILED
            "transport constructor '<inner scheme>' failed"
            inner = exception
```

- Casts compile depth-first: inner first, then conversion.
- Constructor and converter failures preserve the original exception as
  `InnerException`.
- A nested factory invoked inside `Create` may throw its own build error; the
  outer constructor boundary wraps it as the parent constructor failure.
- A `CompilerException` raised by a build-context accessor inside `Create` is a
  resolved-tree invariant violation, not a constructor failure. It propagates
  unchanged with phase `Compilation` and MUST NOT be wrapped as
  `E_BLD_CONSTRUCTOR_FAILED`.
- No recovery, retry, or partial result.
- The compiler never synthesizes absent parameters and never applies defaults.
- The compiler does not validate converter paths during parsing or resolution.

### 9.4 Build-time cast conversion

1. Compile the inner transport.
2. If `inner.Type == Target`, return the inner transport. Identity casts are
   already collapsed during resolution; this check is defense in depth.
3. Otherwise request a conversion factory from `IConvertersGraph.TryConvert`
   using the inner type, target type, an inner factory, the memory rental, and
   the logger.
4. No factory -> `E_BLD_NO_CONVERSION_PATH`.
5. Converter failure -> `E_BLD_CONSTRUCTOR_FAILED` for the cast's inner scheme.

The parser and resolver MUST NOT check converter availability. Converter
availability is build-time configuration.

### 9.5 Compiler invariants

The following are invariant violations, not user errors:

- null root;
- unknown resolved node kind;
- registration name/type disagreeing with resolved constructor metadata;
- created transport type disagreeing with the registration type, if checked.

They use `E_CMP_INCONSISTENT_TREE` with a detail from the closed set in §13.6.
The compiler MAY perform these checks; it MUST NOT duplicate the full printer
validation pass. Parameter and struct shape errors are resolution errors, not
compiler errors. If the optional checks are omitted, a registration whose
name/type pair does not match the resolved constructor is reported as
`E_BLD_CONSTRUCTOR_NOT_FOUND` because the registry lookup is keyed by both
scheme and type. `ResolvedTransport.Type` is derived from `Constructor.Type`, so
`constructor type mismatch` cannot be produced through the formula DTOs; it is a
defensive detail only.

---

## 10. Canonical printer

The canonical printer turns a resolved tree into a canonical formula. Its input
is the resolved tree plus the constructor/parameter metadata reachable from that
tree and the `ITransportTypeCatalog` needed to print cast targets. It does not
depend on the AST, on the core registration contract, or on the compiler.

If the input tree violates the invariants in §8, the printer throws
`PrinterException` with an `E_PRN_*` code (§13.5). A correct resolver never
produces such a tree. Before printing, the printer validates all invariants:
required parameters, declared and unique provided parameters, value kinds,
integer/decimal ranges, `Type == Constructor.Type`, expected-type membership,
list arity and homogeneity, struct field membership and schema order, canonical
names, cast targets, and string printability. Generic invariant violations use
`E_PRN_INCONSISTENT_TREE` with one of the details defined in §13.5.

Validation is depth-first over values. For a cast expression, identity and
cast-target checks precede validation of the inner transport. Within each
transport, the scheme name must be canonical before any other check. Then
required parameters are visited in declaration order; unknown provided
parameter; duplicate provided parameter; out-of-order provided parameter;
`Type == Constructor.Type`; transport type present in the catalog. Then each
provided parameter is visited in declaration order. For each parameter, its name
must be canonical before its value kind, integer/decimal range, expected-set
membership, list arity/homogeneity, or struct fields. Struct fields are visited
in the order unknown → missing → duplicate → order; each provided field's name
must be canonical before its kind and range. String printability is checked
after the value's other checks. A non-canonical name therefore wins over a
simultaneous value-kind mismatch.

### 10.1 Expression

```text
PrintExpr(expr):
    if expr is ResolvedCast:
        if expr.Inner.Type == expr.Target:
            error E_PRN_INCONSISTENT_TREE detail "identity cast node"
        if not catalog.TryGetCanonicalName(expr.Target, out canonicalTarget):
            error E_PRN_INVALID_CAST_TARGET code = expr.Target.ToString()
        return PrintTransport(expr.Inner) + " as " + canonicalTarget
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

The printer emits names from normalized metadata; it does not repair noncanonical
names. Registration stores a normalized metadata view used by the resolved tree
and printer. A noncanonical name in a hand-built tree is an invariant violation.

The printer matches `Provided` entries to parameters by reference identity of
`ParamBinding.Param` (and struct fields by `StructFieldBinding.Field`), not by
name. A binding whose `Param`/`Field` is not an element of the metadata, even if
the names match, is `E_PRN_UNKNOWN_PARAM`/`E_PRN_STRUCT_FIELD_UNKNOWN`.

Consequences:

- The canonical scheme is `Constructor.Name` (already normalized). Aliases never
  appear.
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
    StringValue        -> PrintScalar(value.Text)
    IntegerValue       -> PrintInteger(value.Value)
    DecimalValue       -> PrintDecimal(value.Value)
    ArrayValue         -> "[" + join(value.Elements.Select(PrintDataValue), ", ") + "]"
    ResolvedExpr       -> PrintExpr(value)
    TransportListValue -> "[" + join(value.Elements.Select(PrintExpr), ", ") + "]"
    StructValue        -> "{" + join(PrintStructFields(value), ", ") + "}"
```

Struct fields are printed in schema order:

```text
PrintStructFields(value):
    # Precondition: value.Fields are in schema order and match the schema.
    parts = []
    for each binding in value.Fields in schema order:
        field = binding.Field
        if binding.Value is StringValue:
            parts.add(field.Name + "=" + PrintScalar(binding.Value.Text))
        if binding.Value is IntegerValue:
            parts.add(field.Name + "=" + PrintInteger(binding.Value.Value))
        if binding.Value is DecimalValue:
            parts.add(field.Name + "=" + PrintDecimal(binding.Value.Value))
        if binding.Value is ArrayValue:
            parts.add(field.Name + "=[" +
                      join(binding.Value.Elements.Select(PrintDataValue), ", ") + "]")
        if binding.Value is StructValue:
            parts.add(field.Name + "={" +
                      join(PrintStructFields(binding.Value), ", ") + "}")
    return parts
```

Empty lists print `[]`; empty structs print `{}`.

`PrintInteger(value)` is the invariant signed decimal text of the `Int64` with no
leading `+`, no leading zeros (except `0`), and no exponent.

`PrintDecimal(value)` is the canonical decimal text: no exponent notation,
trailing zeros are trimmed, and negative zero prints as `0`. For example,
`1.50m` prints as `1.5`, `1e3` prints as `1000`, and `1e-28` prints as
`0.0000000000000000000000000001`.

`PrintDataValue(value)` prints a scalar leaf using `PrintScalar`, `PrintInteger`,
or `PrintDecimal`. Array elements are scalar leaves in a valid tree.

### 10.4 Scalars

```text
PrintScalar(text):
    if text contains CR, LF, or an unpaired surrogate:
        error E_PRN_INVALID_SCALAR
              code = first offending UTF-16 code unit in text order
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
- identifier names → normalized lowercase metadata (the printer does not
  lowercase on the fly);
- type names → catalog canonical spelling;
- whitespace and comma spacing;
- positional/named form → longest provided positional prefix + named remainder;
- named argument order → declaration order after the positional prefix;
- struct field order → schema order;
- integer scalar text → invariant signed decimal without a leading `+` or
  redundant leading zeros;
- decimal scalar text → canonical plain form (no exponent, trailing zeros
  trimmed, negative zero normalized);
- arrays → `[elem, elem]` with canonical scalar elements and minimal quoting;
- quoting → minimal safe form;
- identity casts → absent (they never exist in a resolved tree).

The printer preserves:

- string values exactly, including case, Unicode, and leading/trailing spaces
  (quoted as needed);
- presence versus absence of optional parameters;
- the order of transport list elements;
- cast nodes that actually change the type.

### 10.6 Printer examples

Assume a registry where:

- `alpha`: `input` (Transport, positional, required, expected any), `mode`
  (String, positional, optional), `timeout` (String, positional = false,
  optional), type `TypeA`.
- `beta`: `value` (String, positional, required), type `TypeB`.
- `gamma`: `items` (TransportList, positional, required, min 2, expected any),
  type `TypeC`.
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
- string value text is identical (ordinal comparison);
- integer values are equal;
- decimal values are numerically equal (`1.50m` equals `1.5m`);
- arrays have equal elements in the same order;
- absent parameters are absent in both trees.

Equality ignores spans, original aliases, source case, original quoting, and the
original positional/named form.

### 11.2 Required properties

Let `R` be a registry and `C` its type catalog, and let `ResolveParse(s)` mean
`Resolve(Parse(s), Any)` with `R` and `C`.

For every resolved tree `t` produced with `R` and `C`:

```text
ResolveParse(Print(t)) == t
```

Equality is the span-insensitive structural equality defined in §11.1, not
default C# record equality: collection members such as `Provided`,
`TransportListValue.Elements`, `StructValue.Fields`, and `ArrayValue.Elements`
are compared element-wise, and decimal values compare numerically. Round-trip
tests use a suite-local comparer that implements this definition.

For every syntactically and semantically valid source `s`:

```text
Print(ResolveParse(Print(ResolveParse(s))))
    == Print(ResolveParse(s))
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
- `outer(alpha(x))` and `outer(beta(x))` when the alias resolves to `beta`;
- `structy({nested={a=1}, name=x})` and
  `structy({name=x, nested={a=1}})` (schema order is `name`, then `nested`);
- a bare scalar and its quoted equivalent when quoting is unnecessary;
- different whitespace, including CR/LF.

---

## 12. Error model

### 12.1 Error objects

The concrete diagnostic DTO is `FactoryError` in the formula assembly:

```csharp
enum FactoryPhase
{
    Lexing, Parsing, Resolution, Registration, Printing, Compilation, Building
}

sealed record FactoryError(
    string Code, string Message, FactoryPhase Phase, SourceSpan? Span);
```

Exceptions live in the formula assembly root namespace and are class-based:

```csharp
class FactoryException : Exception
{
    FactoryError Error { get; }
}

sealed class RegistrationException : FactoryException { }
sealed class PrinterException : FactoryException { }
sealed class CompilerException : FactoryException { }
sealed class BuildException : FactoryException { }
```

- Parser, lexer, and semantic results carry `FactoryError` with the matching
  phase and a non-null span; they do not throw for user input.
- Registration, printing, compiler, and build failures throw the matching
  exception; the phase is set by the exception type.
- `BuildException` carries `E_BLD_*`; `CompilerException` carries `E_CMP_*`.
- `FactoryException` is the common catch type; the derived type identifies the
  stage.
- Messages are the exact templates in §13, with placeholders substituted.
- The legacy `ParseError` record and record-based exception sketch are obsolete.
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
- List element type: offset of the offending element.
- Array element type and element parse/range/validator failure: offset of the
  offending element.
- Array minimum/maximum: offset of the array's `[` token.
- Struct field shape for an array field: offset of the value start.
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
| `E_LEX_INVALID_UNICODE` | Unpaired UTF-16 surrogate encountered while lexing a `Word` or quoted string, outside a valid escape | `invalid Unicode scalar value` | Offending code unit |

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
| List element | `a value` |
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
| `E_SYN_NESTED_LIST` | `[` as a list element | `nested lists are not allowed` | Nested `[` |
| `E_SYN_TRAILING_INPUT` | Non-EOF token after the top-level expression | `unexpected trailing input` | First extra token |

`<context>` is exactly one of `argument list`, `list`, `struct`.
A comma with no preceding argument, element, or field is not a trailing comma;
it is reported as `E_SYN_UNEXPECTED_TOKEN` (for example, `alpha(,)` and
`alpha([,])`).

A list element may be a transport expression, struct, quoted string, or bare
scalar. Nested lists are excluded syntactically. Consequently `alpha([alpha])`,
`alpha([alpha(), 1])`, and `alpha([beta(), {p=1}])` are syntactically valid; a
transport-list parameter rejects non-expression elements during resolution.

### 13.3 Semantic errors

Formatting helpers used in messages:

- `FormatTypeSet(set)`:
  - if `set.IsAny` → `any transport type`
  - otherwise → distinct canonical names of `set.Types`, sorted and joined with
    ` or `
- `FormatVariants(candidates)` → entries `name (Type)`, where `name` is the
  constructor's canonical `Name` and `Type` is the canonical type name, sorted by
  name then type, joined with `, `.
- `FormatParamExpectation(params)` → if non-empty: `one of: <names>`, where the
  names are parameter names in declaration order, each enclosed in single quotes,
  joined with `, `; if empty: `the scheme has no parameters`.
- `FormatFieldExpectation(schema)` → if non-empty: `one of: <names>`, where the
  names are field names in schema order, each enclosed in single quotes, joined
  with `, `; if empty: `the struct has no fields`.
- `FormatKnownSchemes()` → if `registry.AllSchemeNames` is non-empty:
  `known schemes: <list>`, where list is all canonical and alias names, sorted
  and joined with `, `; if empty: `no schemes are registered`.
- `FormatKnownTypes()` → if `catalog.AllTypes` is non-empty:
  `known types: <list>`, where list is the canonical names of all catalog types,
  sorted and joined with `, `; if empty: `no transport types are registered`.
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
| `E_SEM_EXPECTED_SCALAR` | Scalar parameter received non-scalar | `parameter '<name>' expects a scalar value, got <shape>` | Value start |
| `E_SEM_EXPECTED_TRANSPORT` | Transport parameter received non-expression | `parameter '<name>' expects a transport, got <shape>` | Value start |
| `E_SEM_EXPECTED_TRANSPORT_LIST` | Transport-list parameter received non-list | `parameter '<name>' expects a transport list, got <shape>` | Value start |
| `E_SEM_EXPECTED_STRUCT` | Struct parameter received non-struct | `parameter '<name>' expects a struct, got <shape>` | Value start |
| `E_SEM_LIST_ELEMENT_TYPE` | Transport-list element is not a transport expression | `transport list for '<name>' expects transport elements, got <shape>` | Element start |
| `E_SEM_LIST_MIN` | List count below minimum | `transport list for '<name>' requires at least <min> element(s), got <count>` | List `[` |
| `E_SEM_LIST_MAX` | List count above maximum | `transport list for '<name>' requires at most <max> element(s), got <count>` | List `[` |
| `E_SEM_LIST_HOMOGENEITY` | Homogeneous list contains different types | `transport list for '<name>' must contain one transport type; found <types>` | First differing element |
| `E_SEM_UNKNOWN_STRUCT_FIELD` | Struct field not in schema | `unknown field '<path>' in struct for parameter '<param>'; <expected>` | Field name token |
| `E_SEM_DUPLICATE_STRUCT_FIELD` | Struct field specified twice | `field '<path>' is specified more than once` | Duplicate field name token |
| `E_SEM_MISSING_REQUIRED_STRUCT_FIELD` | Required struct field absent | `missing required field '<path>' in struct for parameter '<param>'` | Closing `}` |
| `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` | Scalar field received non-scalar | `field '<path>' expects a scalar value, got <shape>` | Value start |
| `E_SEM_STRUCT_FIELD_EXPECTED_STRUCT` | Nested struct field received non-struct | `field '<path>' expects a struct, got <shape>` | Value start |
| `E_SEM_STRUCT_FIELD_EXPECTED_ARRAY` | Array field received non-list | `field '<path>' expects an array, got <shape>` | Value start |
| `E_SEM_ARRAY_ELEMENT_TYPE` | Array element is not a scalar | `array element '<path>[<index>]' of parameter '<param>' expects <expected>, got <actual>` | Element start |
| `E_SEM_ARRAY_MIN` | Array count below minimum | `array field '<path>' of parameter '<param>' requires at least <min> element(s), got <count>` | Array `[` |
| `E_SEM_ARRAY_MAX` | Array count above maximum | `array field '<path>' of parameter '<param>' requires at most <max> element(s), got <count>` | Array `[` |
| `E_SEM_UNKNOWN_TRANSPORT_TYPE` | Cast type not in catalog | `unknown transport type '<name>'; <known>` | Type token |
| `E_SEM_CAST_TYPE_MISMATCH` | Cast target outside expected set | `cast produces transport type <target>, but expected <set>` | Type token |
| `E_SEM_VALIDATOR_FAILED` | `Validate` returned non-null/empty or threw | `parameter '<name>' is invalid: <reason>` | Value start |
| `E_SEM_VALIDATOR_FAILED` | Struct field validator failed | `field '<path>' of parameter '<param>' is invalid: <reason>` | Value start |
| `E_SEM_VALIDATOR_FAILED` | Integer parameter parse or range failure | `parameter '<name>' is invalid: <reason>` | Value start |
| `E_SEM_VALIDATOR_FAILED` | Decimal parameter parse or range failure | `parameter '<name>' is invalid: <reason>` | Value start |
| `E_SEM_VALIDATOR_FAILED` | Integer struct-field parse or range failure | `field '<path>' of parameter '<param>' is invalid: <reason>` | Value start |
| `E_SEM_VALIDATOR_FAILED` | Decimal struct-field parse or range failure | `field '<path>' of parameter '<param>' is invalid: <reason>` | Value start |

For a thrown validator or an empty validator result, `<reason>` is exactly
`validation failed`.

For integer parameters and fields, `<reason>` is `expected an integer` for a
parse failure or `out of range [<min>, <max>]` when a successfully parsed value
is outside the declared inclusive range. For decimal parameters and fields,
`<reason>` is `expected a decimal` for a parse failure (including overflow,
underflow, and precision loss) or `out of range [<min>, <max>]`. Range bounds use
invariant integer text and canonical decimal text respectively. Array element
failures use the same reasons and the field form with the indexed path, for
example `field 'values[2]' of parameter 'config' is invalid: expected an integer`.

`<shape>` is exactly one of `scalar`, `transport`, `list`, `struct`.

`<known>` is `FormatKnownSchemes()` for unknown schemes and
`FormatKnownTypes()` for unknown transport types.

`<expected>` is `FormatParamExpectation(params)` for unknown parameters and
`FormatFieldExpectation(schema)` for unknown struct fields. For a nested unknown
field, `<expected>` lists the schema at the nesting level where the offending
field was found; `<param>` remains the top-level parameter name.

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
| `<target>`, `<type>`, `<from>`, `<to>`, `<set>`, `<variants>`, `<types>` | Canonical transport-type names and formatted lists. |
| `<shape>` | One of `scalar`, `transport`, `list`, `struct`. |

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
| `E_REG_EMPTY_TYPE_SET` | `OneOf` or `Exactly` has no types | `transport type set must contain at least one type` |
| `E_REG_INVALID_LIST_RANGE` | `Min < 0`, or `Max < Min` | `invalid transport list range: min <min>, max <max>` |
| `E_REG_INVALID_INTEGER_RANGE` | Integer parameter, field, or array element type `Min > Max` | `invalid integer range: min <min>, max <max>` |
| `E_REG_INVALID_DECIMAL_RANGE` | Decimal parameter, field, or array element type `Min > Max` | `invalid decimal range: min <min>, max <max>` |
| `E_REG_INVALID_ARRAY_RANGE` | Array field `Min < 0` or `Max < Min` | `invalid array range: min <min>, max <max>` |
| `E_REG_DUPLICATE_STRUCT_FIELD` | Duplicate struct field name | `struct field '<name>' is declared more than once` |
| `E_REG_INVALID_STRUCT_FIELD_NAME` | Struct field name is not an identifier | `invalid struct field name '<name>'` |
| `E_REG_INVALID_TYPE_NAME` | Transport type name is not an identifier | `invalid transport type name '<name>'` |
| `E_REG_DUPLICATE_TYPE_NAME` | Catalog contains duplicate type name | `transport type name '<name>' is already registered` |
| `E_REG_TYPE_NOT_IN_CATALOG` | Constructor type or expected type is absent from the catalog | `transport type <type> is not present in the type catalog` |
| `E_REG_BUILDER_SEALED` | Builder has already been built | `registry builder has already been built` |

For `E_REG_INVALID_LIST_RANGE`, `<max>` is `unbounded` when `Max` is `null`.
The same applies to `E_REG_INVALID_ARRAY_RANGE`.
Integer and decimal range bounds use invariant integer text and canonical decimal
text respectively.

Registration validation order for scalar ranges: rule 5 normalizes every type set
for all parameters first, then checks integer and decimal parameter ranges in
declaration order. Rule 6 then checks transport-list ranges. Consequently a later
scalar parameter range error precedes an earlier transport-list range error.
Rule 7 checks struct fields in schema order: field name, duplicate, kind, array
count range, array element scalar range, field range, then nested schema
recursion.

Placeholder sources for registration errors:

- `<name>` and `<scheme>` in invalid-name errors use the original supplied
  spelling, preserving case.
- `<name>` and `<scheme>` in duplicate errors use canonical lowercase.
- `<type>` is a catalog canonical type name when the type exists. For
  `E_REG_TYPE_NOT_IN_CATALOG` and for a duplicate-alias error whose type is
  absent from the catalog, it is the transport type's `ToString()` value.

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
| `E_PRN_STRUCT_FIELD_UNKNOWN` | Struct contains undeclared field | `struct parameter '<name>' of scheme '<scheme>' contains unknown field '<path>'` |
| `E_PRN_STRUCT_FIELD_MISSING` | Required struct field absent | `struct parameter '<name>' of scheme '<scheme>' is missing required field '<path>'` |
| `E_PRN_STRUCT_FIELD_DUPLICATE` | Same struct field appears twice | `struct parameter '<name>' of scheme '<scheme>' contains field '<path>' more than once` |
| `E_PRN_STRUCT_FIELD_KIND_MISMATCH` | Struct field value kind mismatch | `field '<path>' of struct parameter '<name>' has value kind <actual>, expected <expected>` |
| `E_PRN_VALUE_OUT_OF_RANGE` | Resolved integer or decimal outside its declared range | `resolved value for '<path>' is out of range: <value>` |
| `E_PRN_ARRAY_ARITY` | Array count outside `Min`/`Max` | `resolved array field '<path>' of parameter '<param>' has <count> element(s), expected <range>` |
| `E_PRN_ARRAY_ELEMENT_TYPE` | Array element kind mismatch | `resolved array element '<path>[<index>]' of parameter '<param>' expects <expected>, got <actual>` |
| `E_PRN_INVALID_CAST_TARGET` | Cast target not in catalog | `cast target '<type>' is invalid` |
| `E_PRN_INVALID_SCALAR` | Scalar contains CR, LF, or an unpaired surrogate | `scalar value contains a character that cannot be printed: U+<code>` |

`<range>` is `<min>..<max>` if `Max` is set, otherwise `at least <min>`.

For printer messages:

- `<scheme>` and known `<path>` segments are canonical lowercase. `<name>` in
  `E_PRN_UNKNOWN_PARAM` is the stored parameter name; `<name>` elsewhere in
  printer messages is canonical.
- `<actual>` and `<expected>` in kind-mismatch errors are resolved value kinds:
  `string`, `integer`, `decimal`, `transport`, `transport list`, `struct`, or
  `array`.
- `<path>` for `E_PRN_VALUE_OUT_OF_RANGE` is the canonical parameter name for a
  parameter or the dot-separated field path for a struct field; an array element
  path includes `[<index>]`. `<value>` is invariant integer text or canonical
  decimal text.
- `E_PRN_ARRAY_ARITY` validates count before element kinds. An array element kind
  is validated before its range or string printability. `<expected>` is the
  resolved element kind and `<actual>` is the resolved kind of the element value.
- `<path>` is a dot-separated struct-field path of `StructField.Name` values.
  Known schema segments are canonical; an unknown leaf uses its stored name.
- `<detail>` in `E_PRN_INCONSISTENT_TREE` is one of `identity cast node`,
  `expected-type membership violation`, `constructor type mismatch`,
  `non-canonical name`, `struct field order violation`,
  `provided parameter order violation`, `transport type not in catalog`, or
  `unspecified invariant violation`.
- `<type>` is the catalog canonical type name when the type exists. In
  `E_PRN_INVALID_CAST_TARGET`, it is the transport type's `ToString()` value.
- `<code>` in `E_PRN_INVALID_SCALAR` is exactly four uppercase hexadecimal digits
  of the first offending UTF-16 code unit in text order. The message template
  supplies the `U+` prefix.

An identity cast node is an inconsistent-tree violation and uses
`E_PRN_INCONSISTENT_TREE` with detail `identity cast node`. Printer messages use
canonical scheme, parameter, field, and type names.

### 13.6 Compiler and build errors

These are outside the parser/printer contract but complete the boundary.

| Code | Source | Message template |
|---|---|---|
| `E_CMP_INCONSISTENT_TREE` | Compiler | `compiler received an inconsistent resolved tree: <detail>` |
| `E_CMP_DUPLICATE_PARAM` | Compiler | `compiler received duplicate parameter '<name>'` |
| `E_CMP_UNKNOWN_PARAM` | Compiler | `compiler received unknown parameter '<name>'` |
| `E_CMP_PARAM_KIND_MISMATCH` | Compiler | `compiler expected value kind <expected> for parameter '<name>', got <actual>` |
| `E_BLD_CONSTRUCTOR_NOT_FOUND` | Build | `transport constructor '<scheme>' is not registered` |
| `E_BLD_CONSTRUCTOR_FAILED` | Build | `transport constructor '<scheme>' failed` |
| `E_BLD_NO_CONVERSION_PATH` | Build | `no conversion path from <from> to <to>` |
| `E_BLD_TYPE_NOT_BOUND` | Build | `transport type '<id>' is not bound to a core transport type` |

`E_BLD_TYPE_NOT_BOUND` is present only when the optional `TransportTypeId`
binding layer is adopted. The former adapter-only codes
`E_CMP_RESERVED_SCHEME` and `E_CMP_INVALID_CAST_TARGET` are removed from the
compiler catalogue; `E_PRN_INVALID_CAST_TARGET` remains a printer error.
`E_CMP_DUPLICATE_PARAM` and `E_CMP_UNKNOWN_PARAM` are catalogued but not
implemented; the direct compiler consumes a resolver-produced tree and does not
emit them for any tree.

In `E_CMP_PARAM_KIND_MISMATCH`, `<actual>` and `<expected>` are resolved value
kinds: `string`, `integer`, `decimal`, `transport`, `transport list`, `struct`,
or `array`. In
`E_CMP_INCONSISTENT_TREE`, `<detail>` uses the same closed set as
`E_PRN_INCONSISTENT_TREE`. Elsewhere in compiler/build messages, `<scheme>` is
canonical lowercase; `<name>` is canonical except in `E_CMP_UNKNOWN_PARAM`, where
it is the stored parameter name; `<from>` and `<to>` are core transport type
names (`TransportType.ToString()`); a catalog-aware compiler may substitute the
catalog canonical names; `<id>` is a formula transport type id. Compiler
invariant checks, if performed, follow the printer validation order defined in
§10.

---

## 14. Conformance examples and test matrix

The examples below use hypothetical metadata to stay transport-agnostic.

Registry:

```text
alpha   : TypeA
          input   Transport      positional required, expected any
          mode    String         positional optional
          timeout String         named-only optional

beta    : TypeB
          value   String         positional required

gamma   : TypeC
          items   TransportList  positional required, min 2, max 3,
                                 homogeneous, expected any

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
                    with reason "value must not be 'bad'"
mixed        TypeA; flag String named-only optional;
                    input Transport positional required, expected any
opt          TypeA; a String positional optional; b String positional optional
bag          TypeA; items TransportList positional optional, min 0, expected any
emptyStruct  TypeA; config Struct positional required; schema has no fields
single       TypeA; item TransportList positional required, min 1, max 1,
                    expected any
dual         TypeA; input Transport positional required,
                    expected OneOf(TypeA, TypeB)
deep         TypeA
             config Struct positional required
                    outer Struct required
                          inner Struct required
                                value String required
numbers      TypeA
             count Integer positional optional, range [-10, 10]
             ratio Decimal positional optional, range [-1.5, 1.5]
numberStruct TypeA
             config Struct positional required
                    count Integer required, range [0, 10]
                    ratio Decimal optional, range [-1.5, 1.5]
                    nested Struct optional
                           value Integer range [-1, 1]
wide         TypeA
             value Decimal positional required,
                    range [decimal.MinValue, decimal.MaxValue]
arrays       TypeA
             config Struct positional required
                    names  Array(String) min 0, max 2
                    counts Array(Integer [-1, 1]) min 1, max 3, required
                    ratios Array(Decimal [-1.5, 1.5]) min 0
nestedArrays TypeA
             config Struct positional required
                    nested Struct required
                           values Array(Integer [0, 1]) min 1, required
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
| `alpha([alpha(), 1, {p=2}])` | syntactically valid generic list; semantic validity depends on parameter kind |
| `structy({name=x, nested={a=1}})` | nested struct |
| `beta("a, b")` | quoted scalar with comma |
| `beta("")` | empty scalar |
| `numbers(5, 1.50)` | integer 5 and decimal 1.5 |
| `numbers(-10, -1.5)` | inclusive integer and decimal lower bounds |
| `wide(1e-28)` | exact decimal at the smallest representable scale |
| `arrays({counts=[-1, 1], ratios=[1.50, -0.5]})` | integer and decimal arrays |
| `arrays({counts=[0], names=["a, b", c]})` | string array with minimal quoting |

### 14.2 Syntax errors

| Source | Code | Offset |
|---|---|---|
| `` (empty) | `E_SYN_EMPTY_INPUT` | 0 |
| *(spaces only)* | `E_SYN_EMPTY_INPUT` | 0 |
| `alpha` | `E_SYN_UNEXPECTED_TOKEN` expected `'('` | 5 |
| `alpha(` | `E_SYN_UNEXPECTED_TOKEN` expected `a value or ')'` | 6 |
| `alpha(a,)` | `E_SYN_TRAILING_COMMA` | 7 |
| `alpha(,)` | `E_SYN_UNEXPECTED_TOKEN` expected `a value or ')'` | 6 |
| `alpha([,])` | `E_SYN_UNEXPECTED_TOKEN` expected `a value` | 7 |
| `alpha(a b)` | `E_SYN_UNEXPECTED_TOKEN` expected `',' or ')'` | 8 |
| `alpha(p=1, a)` | `E_SYN_POSITIONAL_AFTER_NAMED` | 11 |
| `alpha([[alpha()]])` | `E_SYN_NESTED_LIST` | 7 |
| `alpha({p})` | `E_SYN_UNEXPECTED_TOKEN` expected `'='` | 8 |
| `alpha({p=1,})` | `E_SYN_TRAILING_COMMA` | 10 |
| `alpha() as` | `E_SYN_UNEXPECTED_TOKEN` expected `a transport type name` | 10 |
| `alpha() as TypeA as TypeB` | `E_SYN_CHAINED_CAST` | 17 |
| `[alpha()]` | `E_SYN_UNEXPECTED_TOKEN` expected `a transport expression` | 0 |
| `alpha() extra` | `E_SYN_TRAILING_INPUT` | 8 |

### 14.3 Semantic errors

| Source | Code | Offset |
|---|---|---|
| `nosuch()` | `E_SEM_UNKNOWN_SCHEME` | 0 |
| `alpha()` | `E_SEM_MISSING_REQUIRED_PARAM` | 6 |
| `alpha(beta(x), q=1)` | `E_SEM_UNKNOWN_PARAM` | 15 |
| `alpha(beta(x), input=beta(y))` | `E_SEM_DUPLICATE_PARAM` | 15 |
| `alpha(beta(x), fast, extra)` | `E_SEM_TOO_MANY_POSITIONAL` | 21 |
| `alpha(1)` | `E_SEM_EXPECTED_TRANSPORT` | 6 |
| `gamma(beta(x))` | `E_SEM_EXPECTED_TRANSPORT_LIST` | 6 |
| `structy(1)` | `E_SEM_EXPECTED_STRUCT` | 8 |
| `beta([alpha(beta(x))])` | `E_SEM_EXPECTED_SCALAR` | 5 |
| `gamma([alpha(beta(x))])` | `E_SEM_LIST_MIN` | 6 |
| `gamma([alpha(beta(x)), alpha(beta(x)), alpha(beta(x)), alpha(beta(x))])` | `E_SEM_LIST_MAX` | 6 |
| `gamma([alpha(beta(x)), beta(y)])` | `E_SEM_LIST_HOMOGENEITY` | 23 |
| `alpha([alpha(), 1])` | `E_SEM_EXPECTED_TRANSPORT` (shape `list`) | 6 |
| `alpha([alpha])` | `E_SEM_EXPECTED_TRANSPORT` (shape `list`) | 6 |
| `gamma([alpha(beta(x)), 1])` | `E_SEM_LIST_ELEMENT_TYPE` (shape `scalar`) | 23 |
| `gamma([alpha(beta(x)), {}])` | `E_SEM_LIST_ELEMENT_TYPE` (shape `struct`) | 23 |
| `outer(gamma([alpha(beta(x)), alpha(beta(x))]))` | `E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE` | 6 |
| `shared(x)` | `E_SEM_AMBIGUOUS_SCHEME` | 0 |
| `validated(bad)` | `E_SEM_VALIDATOR_FAILED` | 10 |
| `structy({name=x, extra=1})` | `E_SEM_UNKNOWN_STRUCT_FIELD` | 17 |
| `structy({name=x, nested={unknown=1}})` | `E_SEM_UNKNOWN_STRUCT_FIELD` (path `nested.unknown`) | 25 |
| `structy({name=x, name=y})` | `E_SEM_DUPLICATE_STRUCT_FIELD` | 17 |
| `structy({})` | `E_SEM_MISSING_REQUIRED_STRUCT_FIELD` | 9 |
| `deep({outer={inner={}}})` | `E_SEM_MISSING_REQUIRED_STRUCT_FIELD` (path `outer.inner.value`) | 20 |
| `structy({name=alpha(beta(x))})` | `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` | 14 |
| `structy({name={}})` | `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` | 14 |
| `structy({name=x, nested=1})` | `E_SEM_STRUCT_FIELD_EXPECTED_STRUCT` | 24 |
| `deep({outer={inner={value=beta(x)}}})` | `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` (path `outer.inner.value`) | 26 |
| `alpha(beta(x)) as TypeZ` | `E_SEM_UNKNOWN_TRANSPORT_TYPE` | 18 |
| `outer(alpha(beta(x)) as TypeC)` | `E_SEM_CAST_TYPE_MISMATCH` | 24 |
| `outer(beta(x) as TypeA)` | `E_SEM_CAST_TYPE_MISMATCH` | 17 |
| `dual(beta(x) as TypeC)` | `E_SEM_CAST_TYPE_MISMATCH` | 16 |
| `beta(1)` where `1` is a scalar and `value` is String | valid | — |
| `numbers(11)` | `E_SEM_VALIDATOR_FAILED` (count out of range) | 8 |
| `numbers(1.5)` | `E_SEM_VALIDATOR_FAILED` (count not an integer) | 8 |
| `numbers(0, 1e-29)` | `E_SEM_VALIDATOR_FAILED` (ratio not exactly representable) | 11 |
| `numberStruct({count=x})` | `E_SEM_VALIDATOR_FAILED` (count not an integer) | 20 |
| `numberStruct({count=1, nested={value=2}})` | `E_SEM_VALIDATOR_FAILED` (path `nested.value`) | 37 |
| `arrays({counts=1})` | `E_SEM_STRUCT_FIELD_EXPECTED_ARRAY` | 15 |
| `arrays({counts=[]})` | `E_SEM_ARRAY_MIN` | 15 |
| `arrays({counts=[0, 0, 0, 0]})` | `E_SEM_ARRAY_MAX` | 15 |
| `arrays({counts=[beta()]})` | `E_SEM_ARRAY_ELEMENT_TYPE` (shape `transport`) | 16 |
| `arrays({counts=[2]})` | `E_SEM_VALIDATOR_FAILED` (path `counts[0]`) | 16 |
| `nestedArrays({nested={values=[0, 2]}})` | `E_SEM_VALIDATOR_FAILED` (path `nested.values[1]`) | 33 |

Representative exact messages:

| Source | Exact message |
|---|---|
| `alpha()` | `E_SEM_MISSING_REQUIRED_PARAM: missing required parameter 'input' for scheme 'alpha' (offset 6)` |
| `alpha(beta(x), fast, extra)` | `E_SEM_TOO_MANY_POSITIONAL: too many positional arguments for scheme 'alpha'; expected at most 2 (offset 21)` |
| `gamma([alpha(beta(x))])` | `E_SEM_LIST_MIN: transport list for 'items' requires at least 2 element(s), got 1 (offset 6)` |
| `outer(gamma([alpha(beta(x)), alpha(beta(x))]))` | `E_SEM_NO_VARIANT_FOR_EXPECTED_TYPE: scheme 'gamma' has no variant for expected transport type TypeB; available: gamma (TypeC) (offset 6)` |
| `shared(x)` | `E_SEM_AMBIGUOUS_SCHEME: scheme 'shared' is ambiguous; candidates: beta (TypeB), gamma (TypeC) (offset 0)` |
| `dual(beta(x) as TypeC)` | `E_SEM_CAST_TYPE_MISMATCH: cast produces transport type TypeC, but expected TypeA or TypeB (offset 16)` |
| `gamma([alpha(beta(x)), beta(y)])` | `E_SEM_LIST_HOMOGENEITY: transport list for 'items' must contain one transport type; found TypeA, TypeB (offset 23)` |
| `deep({outer={inner={}}})` | `E_SEM_MISSING_REQUIRED_STRUCT_FIELD: missing required field 'outer.inner.value' in struct for parameter 'config' (offset 20)` |
| `structy({name=alpha(beta(x))})` | `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR: field 'name' expects a scalar value, got transport (offset 14)` |
| `validated(bad)` | `E_SEM_VALIDATOR_FAILED: parameter 'value' is invalid: value must not be 'bad' (offset 10)` |
| `numbers(11)` | `E_SEM_VALIDATOR_FAILED: parameter 'count' is invalid: out of range [-10, 10] (offset 8)` |
| `numbers(1.5)` | `E_SEM_VALIDATOR_FAILED: parameter 'count' is invalid: expected an integer (offset 8)` |
| `numbers(0, 1e-29)` | `E_SEM_VALIDATOR_FAILED: parameter 'ratio' is invalid: expected a decimal (offset 11)` |
| `numberStruct({count=x})` | `E_SEM_VALIDATOR_FAILED: field 'count' of parameter 'config' is invalid: expected an integer (offset 20)` |

> Some rows depend on registry details; tests must define the full registry
> explicitly.

### 14.4 Canonical printer

The first column is source or tree notation. Source notation is normalized to a
resolved tree before printing; a resolved tree's `Provided` list is in
declaration order.

| Source / tree notation | Canonical output |
|---|---|
| `alpha(input=beta(x))` | `alpha(beta(x))` |
| `alpha(input=beta(x), mode=fast)` | `alpha(beta(x), fast)` |
| `alpha(input=beta(x), timeout=5s)` | `alpha(beta(x), timeout=5s)` |
| `alpha(timeout=5s, input=beta(x))` | `alpha(beta(x), timeout=5s)` |
| `alpha(input=beta(x), mode=fast, timeout=5s)` | `alpha(beta(x), fast, timeout=5s)` |
| `alpha(input=beta(x) as TypeA)` | `alpha(beta(x) as TypeA)` |
| `gamma(items=[alpha(beta(x)), alpha(beta(x))])` | `gamma([alpha(beta(x)), alpha(beta(x))])` |
| `structy(config={name=x, nested={a=1}})` | `structy({name=x, nested={a=1}})` |
| `beta(value="a, b")` | `beta("a, b")` |
| `beta(value="")` | `beta("")` |
| `beta(value="x=y")` | `beta("x=y")` |
| `beta(value="plain")` | `beta(plain)` |
| `beta(value="a\"b")` | `beta("a\"b")` |
| `beta(value="a\\b")` | `beta(a\b)` |
| `beta(value="a<HTAB>b")` | `beta("a<HTAB>b")` |
| `beta(value="  x  ")` | `beta("  x  ")` |
| `alpha(input=alpha(beta(x)))` | `alpha(alpha(beta(x)))` |
| `mixed(input=beta(x), flag=fast)` | `mixed(beta(x), flag=fast)` |
| `opt(b=two)` | `opt(b=two)` |
| `bag(items=[])` | `bag([])` |
| `emptyStruct(config={})` | `emptystruct({})` |
| `single(item=[beta(x)])` | `single([beta(x)])` |
| `numbers(count=5, ratio=1.50)` | `numbers(5, 1.5)` |
| `numbers(count=+5, ratio=-0.0)` | `numbers(5, 0)` |
| `wide(value=1e3)` | `wide(1000)` |
| `wide(value=1e-28)` | `wide(0.0000000000000000000000000001)` |
| `numberStruct(config={count=5, ratio=1.50})` | `numberstruct({count=5, ratio=1.5})` |
| `arrays(config={counts=[-1, 1], ratios=[1.50]})` | `arrays({counts=[-1, 1], ratios=[1.5]})` |
| `arrays(config={counts=[0], names=[]})` | `arrays({names=[], counts=[0]})` |

In the `HTAB` row, `<HTAB>` denotes a literal U+0009 character.

### 14.5 Lexical errors

| Source | Code | Offset |
|---|---|---|
| `alpha("x)` | `E_LEX_UNTERMINATED_STRING` | 6 |
| `alpha("x\q")` | `E_LEX_INVALID_ESCAPE` | 8 |
| `alpha("x<LF>y")` where `<LF>` is a literal U+000A | `E_LEX_RAW_NEWLINE_IN_STRING` | 8 |
| `alpha("x<CR>y")` where `<CR>` is a literal U+000D | `E_LEX_RAW_NEWLINE_IN_STRING` | 8 |
| `alpha("x\<CR>y")` where `\<CR>` is a backslash followed by U+000D | `E_LEX_INVALID_ESCAPE` | 8 |
| `alpha("x<U+D800>y")` where `<U+D800>` is a literal unpaired surrogate | `E_LEX_INVALID_UNICODE` | 8 |
| `alpha(<U+D800>)` with a literal unpaired surrogate in a bare Word | `E_LEX_INVALID_UNICODE` | 6 |
| `alpha(1) "unterminated` | `E_LEX_UNTERMINATED_STRING` | 9 |

### 14.6 Registration errors

| Setup | Code |
|---|---|
| Canonical scheme `Alpha` already registered as `alpha` | `E_REG_DUPLICATE_CANONICAL_NAME` |
| Canonical scheme `1alpha` | `E_REG_INVALID_CANONICAL_NAME` |
| Parameter name `1input` | `E_REG_INVALID_PARAM_NAME` |
| Parameters `input` and `INPUT` | `E_REG_DUPLICATE_PARAM_NAME` |
| Alias name `1alias` | `E_REG_INVALID_ALIAS_NAME` |
| Alias `shared` registered twice for `TypeA` | `E_REG_DUPLICATE_ALIAS_FOR_TYPE` |
| `OneOf()` with no types, or an empty `Exactly` set | `E_REG_EMPTY_TYPE_SET` |
| `TransportListParam` with `Min = -1` | `E_REG_INVALID_LIST_RANGE` |
| `IntegerParam`, `IntegerField`, or an array element `IntegerType` with `Min > Max` | `E_REG_INVALID_INTEGER_RANGE` |
| `DecimalParam`, `DecimalField`, or an array element `DecimalType` with `Min > Max` | `E_REG_INVALID_DECIMAL_RANGE` |
| `ArrayField` with `Min < 0` or `Max < Min` | `E_REG_INVALID_ARRAY_RANGE` |
| Struct fields `name` and `NAME` | `E_REG_DUPLICATE_STRUCT_FIELD` |
| Struct field name `1name` | `E_REG_INVALID_STRUCT_FIELD_NAME` |
| Catalog type name `1Type` | `E_REG_INVALID_TYPE_NAME` |
| Catalog type name `TypeA` registered twice | `E_REG_DUPLICATE_TYPE_NAME` |
| Constructor type or expected type absent from the catalog | `E_REG_TYPE_NOT_IN_CATALOG` |
| Register after `Build` | `E_REG_BUILDER_SEALED` |

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
| Integer or decimal resolved value outside its declared range | `E_PRN_VALUE_OUT_OF_RANGE` |
| Array count outside `Min`/`Max` | `E_PRN_ARRAY_ARITY` |
| Array element kind mismatch | `E_PRN_ARRAY_ELEMENT_TYPE` |
| Cast target not in catalog | `E_PRN_INVALID_CAST_TARGET` |
| Transport value outside the parameter's expected set | `E_PRN_INCONSISTENT_TREE` |
| `ResolvedTransport.Type != Constructor.Type` | `E_PRN_INCONSISTENT_TREE` |
| Non-canonical scheme, parameter, or field name | `E_PRN_INCONSISTENT_TREE` |
| Provided parameters not in declaration order | `E_PRN_INCONSISTENT_TREE` |
| Struct fields not in schema order | `E_PRN_INCONSISTENT_TREE` |
| Scalar contains CR/LF or an unpaired surrogate | `E_PRN_INVALID_SCALAR` |
| Identity cast node | `E_PRN_INCONSISTENT_TREE` |

A correct parser never produces these trees.

### 14.8 Additional diagnostic and canonicalization examples

`displayToken` examples:

| Token | Display |
|---|---|
| Word `a\b` | `'a\\b'` |
| Quoted string containing HTAB | `'a\tb'` |
| Quoted string containing U+1F600 | `'\uD83D\uDE00'` |

A fifty-character `Word` made only of `a` displays as forty `a` characters
followed by `...`.

Representative non-semantic error messages:

| Source | Exact message |
|---|---|
| Duplicate canonical `alpha` | `E_REG_DUPLICATE_CANONICAL_NAME: canonical scheme 'alpha' is already registered` |
| Missing constructor type `TypeZ` | `E_REG_TYPE_NOT_IN_CATALOG: transport type TypeZ is not present in the type catalog` |
| Printer tree missing required `input` | `E_PRN_MISSING_REQUIRED_PARAM: resolved transport 'alpha' is missing required parameter 'input'` |
| Printer tree with identity cast | `E_PRN_INCONSISTENT_TREE: resolved tree is inconsistent: identity cast node` |
| Printer tree with expected-type violation | `E_PRN_INCONSISTENT_TREE: resolved tree is inconsistent: expected-type membership violation` |
| Printer tree with constructor type mismatch (defensive; unreachable through the resolved DTOs) | `E_PRN_INCONSISTENT_TREE: resolved tree is inconsistent: constructor type mismatch` |
| Printer tree with non-canonical name | `E_PRN_INCONSISTENT_TREE: resolved tree is inconsistent: non-canonical name` |
| Printer tree with out-of-order Provided | `E_PRN_INCONSISTENT_TREE: resolved tree is inconsistent: provided parameter order violation` |
| Printer tree with out-of-order struct fields | `E_PRN_INCONSISTENT_TREE: resolved tree is inconsistent: struct field order violation` |
| Printer scalar containing CR | `E_PRN_INVALID_SCALAR: scalar value contains a character that cannot be printed: U+000D` |
| Printer tree with an out-of-range integer | `E_PRN_VALUE_OUT_OF_RANGE: resolved value for 'count' is out of range: 11` |
| Printer tree with an out-of-range decimal field | `E_PRN_VALUE_OUT_OF_RANGE: resolved value for 'config.ratio' is out of range: 1.6` |
| Printer tree with a short array | `E_PRN_ARRAY_ARITY: resolved array field 'values' of parameter 'config' has 0 element(s), expected 1..2` |
| Printer tree with a wrong array element kind | `E_PRN_ARRAY_ELEMENT_TYPE: resolved array element 'values[0]' of parameter 'config' expects integer, got string` |
| Printer tree with a cast target absent from the catalog | `E_PRN_INVALID_CAST_TARGET: cast target '<TransportType.ToString()>' is invalid` |
| Compiler tree with null root | `E_CMP_INCONSISTENT_TREE: compiler received an inconsistent resolved tree: unspecified invariant violation` |
| Compiler tree with unknown parameter `x` | `E_CMP_UNKNOWN_PARAM: compiler received unknown parameter 'x'` |
| Compiler tree with duplicate parameter `input` | `E_CMP_DUPLICATE_PARAM: compiler received duplicate parameter 'input'` |
| Compiler tree with a string value for transport parameter `input` | `E_CMP_PARAM_KIND_MISMATCH: compiler expected value kind transport for parameter 'input', got string` |
| Compiler tree with inconsistent constructor type (defensive; unreachable through the resolved DTOs) | `E_CMP_INCONSISTENT_TREE: compiler received an inconsistent resolved tree: constructor type mismatch` |
| Build cast from `<from>` to `<to>` without a path | `E_BLD_NO_CONVERSION_PATH: no conversion path from <from> to <to>` |
| Constructor `alpha` not registered | `E_BLD_CONSTRUCTOR_NOT_FOUND: transport constructor 'alpha' is not registered` |
| Constructor `alpha` fails | `E_BLD_CONSTRUCTOR_FAILED: transport constructor 'alpha' failed` |
| Build cast target `TypeZ` not bound (optional binding layer) | `E_BLD_TYPE_NOT_BOUND: transport type 'TypeZ' is not bound to a core transport type` |

In the two build rows, `<from>`, `<to>`, and the cast-target name are
`TransportType.ToString()` values, not catalog canonical names: the direct
compiler has no catalog.

---

## 15. Out of scope

This document does not specify:

- how concrete transports are implemented beyond the core registration contract;
- how the converter graph chooses paths;
- how concrete constructors read their parameters;
- how aliases are chosen by an application;
- any concrete transport scheme or parameter name.

The parser and canonical printer are complete without those.
