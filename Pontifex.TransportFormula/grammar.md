# Transport Formula Grammar

## 1. Scope

A **Transport Formula** is a single expression that denotes how to construct a
transport. This document defines the **syntax** of Transport Formulas. It is
transport-agnostic: it does not mention concrete transports, schemes, parameters,
or conversions.

Semantic rules — scheme resolution, parameter binding, type checking, validation,
compilation, and canonical printing — are defined in `parsing.md`.

The grammar described here is a small expression language:

- a transport declaration is a named call with arguments;
- arguments may be positional or named;
- a value may be a transport expression, a list, a struct, or a scalar;
- a transport expression may be cast to a transport type with `as`;
- there is no prefix, no comments, and no line-oriented structure.

## 2. Input model

- Input is a .NET UTF-16 string. Unpaired surrogates are not Unicode scalar
  values and are a lexical error (`E_LEX_INVALID_UNICODE` in `parsing.md`),
  reported at the first offending code unit.
- The entire input is one expression. Leading and trailing whitespace is allowed.
- Offsets used in errors are **0-based UTF-16 code-unit offsets** from the first
  character of the input. The offset of the end of input is equal to the input
  length in UTF-16 code units.
- No Unicode normalization is performed. Values are preserved exactly.
- Identifiers are ASCII and case-insensitive for comparison; their canonical
  spelling is lowercase.
- Type names are identifiers and are case-insensitive for comparison; their
  canonical spelling is defined by the transport-type catalog.
- There are no implementation limits: no maximum depth, length, list size, or
  argument count is defined by the grammar.

There is no `transport://` prefix. The input starts directly with an expression.
Comments are not supported.

## 3. Character classes

```text
SP       = U+0020
HTAB     = U+0009
CR       = U+000D
LF       = U+000A
WS       = SP | HTAB | CR | LF

DELIM    = "(" | ")" | "[" | "]" | "{" | "}" | "," | "=" | '"'

BareScalarChar =
    any Unicode scalar value that is not in WS and not in DELIM

Identifier =
    [A-Za-z] [A-Za-z0-9_-]*
```

Properties:

- `Identifier` is ASCII-only.
- A `Word` token is a maximal non-empty run of `BareScalarChar`.
- A `Word` may or may not match `Identifier`. The parser checks the identifier
  shape only where the grammar requires an identifier.
- Values may contain Unicode. No normalization or case folding is applied to
  values.
- `CR` and `LF` are insignificant whitespace **outside** quoted strings.
- `CR` and `LF` are **not allowed** inside quoted strings. A raw newline inside
  quotes is a lexical error.

## 4. Tokens

The lexer produces the following tokens:

| Token | Description |
|---|---|
| `Word` | A maximal non-empty run of `BareScalarChar`. |
| `QuotedString` | A double-quoted string, decoded according to §4.2. |
| `(` `)` `[` `]` `{` `}` `,` `=` | Single-character punctuation. |
| `EOF` | End of input. |

Whitespace outside quoted strings is skipped between tokens. It is never part of
a `Word` or punctuation token; it is part of a `QuotedString` when it appears
inside the quotes.

### 4.1 Word tokens

A `Word` begins at the first non-whitespace character that is not `"` and is not
punctuation, and ends immediately before the next whitespace, punctuation, or
`"` character.

Examples of single `Word` tokens:

```text
alpha
alpha-beta
alpha_beta
127.0.0.1:9000
a\b
a|b
a+b
as
123
```

The same token may be interpreted as an identifier, a bare scalar, or the
contextual keyword `as`, depending on the syntactic position.

### 4.2 Quoted strings

```text
quoted      := '"' { quoted_char | escape } '"'
escape      := "\" '"' | "\" "\"
quoted_char := any Unicode scalar value except '"', "\", CR, LF
```

Rules:

- The opening and closing quotes are not part of the decoded value.
- `\"` decodes to `"`.
- `\\` decodes to `\`.
- Any other backslash sequence is a lexical error.
- A raw `CR` or `LF` inside quotes is a lexical error.
- An empty quoted string `""` is valid and denotes the empty string.
- A quoted string may contain commas, parentheses, brackets, braces, `=`,
  whitespace other than CR/LF, and any other Unicode scalar value.
- The decoded value is preserved exactly. There are no other escape sequences
  (no `\n`, `\t`, `\uXXXX`, etc.).

### 4.3 Contextual keyword `as`

`as` is not a reserved word. It is recognized as the cast keyword only when it
appears as a `Word` equal to `as` (ASCII case-insensitive) immediately after a
transport expression, where the grammar permits a cast.

In every other position, `as` is an ordinary `Word` and may be used as:

- a scheme name;
- a parameter or struct-field name;
- a bare scalar value.

## 5. Grammar

The following EBNF is normative. `ws` denotes zero or more whitespace
characters. Whitespace is allowed between any two tokens; it is omitted from the
production bodies for readability except where it clarifies separation.

```ebnf
input       := ws expr ws EOF

expr        := transport [ "as" type_name ]

transport   := identifier "(" [ args ] ")"

args        := positional { "," positional }
                 [ "," named { "," named } ]
             | named { "," named }

positional  := value
named       := identifier "=" value

value       := expr
             | list
             | struct
             | quoted
             | bare

list        := "[" [ list_element { "," list_element } ] "]"
list_element := expr
             | struct
             | quoted
             | bare

struct      := "{" [ field { "," field } ] "}"
field       := identifier "=" value

type_name   := identifier
identifier  := <Word matching Identifier>
bare        := <Word>
quoted      := <QuotedString>
```

Notes:

- `input` requires exactly one expression. Empty or whitespace-only input is not
  a valid input; it is reported as a missing transport expression.
- `args` is either entirely positional, entirely named, or a positional prefix
  followed by a named suffix. A positional argument may not follow a named one.
  This is enforced syntactically.
- `args` is absent for `name()`. `name()` is a valid transport declaration with
  zero arguments.
- `list` elements are `list_element` values: transport expressions, structs,
  quoted strings, or bare scalars. Nested lists are not valid list elements. An
  empty list `[]` is syntactically valid. The list shape is shared by transport
  lists and data arrays; the parameter or field kind decides which is required.
- A single transport is never implicitly treated as a one-element list, and a
  list is never implicitly treated as a single transport. Brackets are
  significant: `[alpha()]` and `alpha()` are different shapes.
- `struct` fields are always named (`identifier "=" value`). There are no
  positional struct fields. An empty struct `{}` is syntactically valid.
- `field` syntactically accepts any `value`, including transports and lists.
  Semantic struct schemas restrict field values; see `parsing.md`.
- `expr` permits at most one cast. A second `as` after a cast is a syntax error.
- A cast applies to the immediately preceding transport expression. Lists and
  structs are not transport expressions and cannot be cast.
- The top level is an `expr`, so a top-level value must be a transport
  expression (possibly cast), not a list, struct, or bare scalar.

## 6. Token disambiguation rules

The grammar is shape-directed. At a point where a `value` is expected:

1. If the next token is `"`, it is a `QuotedString` scalar.
2. If the next token is `[`, it is a list.
3. If the next token is `{`, it is a struct.
4. If the next token is a `Word` and the following token is `(`, it is a
   transport expression.
5. Otherwise, if the next token is a `Word`, it is a bare scalar.
6. Any other token at a value position is a syntax error.

At the start of an argument or struct field:

1. If the next token is a `Word` and the following token is `=`, it is a named
   argument or a struct field.
2. Otherwise, in an argument list, it is a positional argument; in a struct, it
   is a syntax error because struct fields must be named.

After a transport expression:

1. If the next token is a `Word` equal to `as` (case-insensitive), it begins a
   cast.
2. Otherwise, the expression ends.

At the top level, the first token must begin a transport expression. A leading
`[`, `{`, `"`, or a bare scalar that is not followed by `(` is a syntax error.

Consequences:

- `alpha` alone is a bare scalar, not a transport. A transport always has
  parentheses: `alpha()`.
- `alpha ( ... )` is a transport call; whitespace between the name and `(` is
  insignificant.
- `alpha(x=1)` is a named argument. To pass the literal text `x=1` as a scalar,
  quote it: `alpha("x=1")`.
- `alpha(as)` is a bare scalar value `as`, not a cast, because it is in value
  position and not preceded by a transport expression.
- `alpha() as TypeA` is a cast.
- `alpha() as TypeA as TypeB` is a syntax error: casts do not chain.

## 7. Syntax versus semantics

The grammar validates only shape and token structure. The following are
**semantic** and are defined in `parsing.md`:

- whether a scheme exists;
- which constructor a scheme or alias resolves to;
- parameter names, order, arity, requiredness, and `Positional` flags;
- whether an argument value has the kind required by a parameter;
- transport type names in casts;
- expected transport types and type sets;
- transport list minimum/maximum sizes, homogeneity, and element kinds;
- struct field names, requiredness, and field kinds;
- scalar validators;
- whether an integer scalar parses as a signed 64-bit integer and lies within its
  declared range;
- whether a decimal scalar is exactly representable and lies within its declared
  range.

The grammar intentionally permits expressions that are syntactically valid but
semantically invalid. Examples:

| Formula | Why it may be rejected semantically |
|---|---|
| `unknown-scheme()` | The scheme may not be registered. |
| `alpha(p=1, q=2)` | `p` or `q` may not be declared parameters. |
| `alpha({p=1})` | The struct schema may reject field `p`. |
| `alpha() as UnknownType` | `UnknownType` may not be a known transport type. |

## 8. Canonical lexical forms

Canonical printing is specified in `parsing.md`. For reference, a canonical
expression:

- has no `transport://` prefix;
- writes one space after each comma: `alpha(a, b, c)`;
- writes no space after `(` or before `)`, and no space around `=`;
- writes a cast as `inner as TypeName` with single spaces around `as`;
- writes identifiers (schemes, parameter names, struct field names) in
  lowercase;
- writes type names in the canonical spelling defined by the transport-type
  catalog;
- preserves string scalar values exactly;
- writes integers as invariant signed decimal text with no leading `+` and no
  leading zeros (except `0`) and no exponent;
- writes decimals in canonical plain form: no exponent notation, trailing zeros
  trimmed, and negative zero as `0`;
- quotes a scalar only when required by the lexical rules;
- contains no raw `CR` or `LF`.

## 9. Examples

Syntactically valid:

```text
alpha()
alpha(127.0.0.1:9000)
alpha("a, b", "x=y", "")
alpha(p=1, q=two)
alpha(1, 2, p=3, q=4)
alpha(beta(), gamma(x=1))
alpha([beta(), gamma(), delta()])
alpha([beta(), 1, {p=2}])
alpha({p=1, q={r=2}})
alpha() as TypeA
alpha(beta() as TypeA)
alpha([beta() as TypeA, gamma()])
alpha([], {})
alpha(a\b)
alpha(a|b)
```

Syntactically invalid:

| Formula | Why it is invalid |
|---|---|
| *(empty input)* | No expression. |
| `alpha` | Missing `(`. |
| `alpha(` | Missing `)` and missing value. |
| `alpha(,` | Missing value. |
| `alpha(a,)` | Trailing comma. |
| `alpha(a b)` | Missing comma. |
| `alpha(p=1, a)` | Positional after named. |
| `alpha([[beta()]])` | Nested list. |
| `alpha({p=1,})` | Trailing comma. |
| `alpha({p})` | Missing `=`. |
| `alpha() as` | Missing type name. |
| `alpha() as TypeA as TypeB` | Chained cast. |
| `[alpha()]` | Top-level list. |
| `{alpha()}` | Top-level struct. |

Semantic validity is intentionally not implied by these examples.

## 10. Non-goals

This grammar does not define:

- concrete transport schemes or type names;
- parameter names or their semantics;
- default values;
- implicit conversions;
- alias resolution;
- canonical printing rules in full;
- compiler or builder behavior.

Those are defined in `parsing.md`.
