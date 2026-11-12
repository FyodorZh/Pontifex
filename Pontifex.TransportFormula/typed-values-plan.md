# Typed Values for the Factory — Sequenced Implementation Plan

## 1. Purpose and scope

This plan implements the agreed type-system improvements in the Transport
Formula parser, resolver, canonical printer, registry, and core registration
contract.

**In scope:** Factory type-system work only.

- unified resolved-value hierarchy;
- integer and decimal scalar kinds;
- generic list syntax;
- homogeneous data arrays inside struct fields;
- exact error catalogue, canonical printing, and round-trip behavior;
- updates to all existing Factory tests and documentation.

**Out of scope:** any application of the new types to real transports.

- no TCP, Direct, UDP, RR-UDP, zip, reconnectable, or logger registration;
- no transport constructor changes;
- no transport test-project changes;
- no host wiring;
- no UI or application use case;
- no legacy `IDescription`/URI retirement;
- no nested arrays, top-level arrays, array parameters, enum fields, `UInt64`,
  boolean fields, or struct array elements.

The plan is subordinate to `grammar.md`, `parsing.md`, and
`implementation-plan.md`. A step that changes normative behavior updates those
documents in the same change.

## 2. Settled decisions

### Values

- `ResolvedExpr : ResolvedValue`. It is the special expression-value branch; no
  `ExpressionValue` wrapper.
- `DataValue : ResolvedValue` is the pure-data branch.
- `ScalarValue : DataValue` has three kinds:
  - `StringValue(string Text)`;
  - `IntegerValue(long Value)`;
  - `DecimalValue(decimal Value)`.
- `StructValue : DataValue`.
- `ArrayValue : DataValue` with `IReadOnlyList<DataValue> Elements`.
- `TransportListValue : ResolvedValue` with
  `IReadOnlyList<ResolvedExpr> Elements`.
- `TransportValue` is removed. Transport parameters store `ResolvedExpr`
  directly.
- `StructFieldBinding.Value` is `DataValue`.
- `StringFieldValue` and `NestedStructFieldValue` are removed.

### Scalars

- Integers are `Int64` only. There is no `UInt64` scalar kind.
- `IntegerType` carries `long Min` and `long Max`.
- `DecimalType` carries `decimal Min` and `decimal Max`.
- `type`-like enumerations remain strings with validators. No enum field type.
- Decimal equality treats `1.50` and `1.5` as equal. Canonical output is `1.5`.
- Decimal input allows a sign, decimal point, and exponent. Parsing is exact: a
  literal that is not exactly representable as a `decimal` (overflow,
  underflow, or precision loss) is a parse failure, not an out-of-range value.
- Decimal canonical output never uses exponent notation; trailing zeros are
  trimmed and negative zero normalizes to `0`.
- Integer/decimal parse and range failures reuse `E_SEM_VALIDATOR_FAILED` with
  parameter or field context.

### Lists and arrays

- Arrays are homogeneous and have one declared `SingleType` element.
- `ArrayField` only. Top-level `ArrayParam` is deferred.
- `SingleType` has a closed hierarchy: `StringType`, `IntegerType`,
  `DecimalType`. Core and formula each have this hierarchy. Step 2’s
  `IntegerType`/`DecimalType` are the same classes and become `SingleType`
  subclasses in Step 4. `StringType` is for array elements only; scalar
  parameters/fields keep their existing validator properties. `StructType` does
  not exist in this phase.
- No nested arrays. `E_SYN_NESTED_LIST` remains.
- No top-level arrays. The root is still a transport expression.
- Arrays and transport lists share `[...]` syntax, the generic `AstList` node,
  count validation, and printing.
- `ArrayValue` holds `DataValue` in the DTO; valid resolved trees restrict
  elements to scalar leaves (`StringValue`/`IntegerValue`/`DecimalValue`).
  Nested arrays and struct elements are representable only for invariant tests
  and are rejected by validators/printer checks.
- `TransportListValue` holds only `ResolvedExpr`.
- `ArrayField.Min`/`Max` are direct properties; no `ListRange` DTO.

### Errors and compatibility

- Array count and element errors use `E_SEM_ARRAY_*` / `E_PRN_ARRAY_*`.
- A non-list value for an array field uses the existing struct-field shape
  family: `E_SEM_STRUCT_FIELD_EXPECTED_ARRAY`.
- An array field with a non-array resolved value uses
  `E_PRN_STRUCT_FIELD_KIND_MISMATCH`.
- Invalid transport-list elements become semantic errors after the generic list
  parser change.
- Integer/decimal shape errors reuse the existing scalar errors; parse and range
  failures reuse `E_SEM_VALIDATOR_FAILED`.
- Registration range errors use `E_REG_INVALID_*`.
- Printer range errors use `E_PRN_VALUE_OUT_OF_RANGE`; array printer invariants
  use `E_PRN_ARRAY_*`.
- Breaking changes are accepted. Every step fixes the tests it breaks and adds
  tests for new behavior.
- Tests follow `Tests/RULES.md`.

### Review decisions

The following decisions were fixed during plan review and override any
ambiguity elsewhere in this document.

- **Decimal literals are exact.** A literal that is not exactly representable
  as a `decimal` is a parse failure with reason `expected a decimal`. This
  includes overflow, underflow, and precision loss. `decimal.TryParse` alone is
  not sufficient: it accepts `1e-29` as `0` and rounds 29+ significant digits.
  Implement a dedicated exact coefficient/scale parser and pin the edge cases in
  Step 2.
- **`SingleType` is reused, not duplicated.** Core and formula each have a
  closed `SingleType` hierarchy. Step 2’s `IntegerType`/`DecimalType` are the
  same classes used by parameters and fields; Step 4 re-parents them under
  `SingleType`. `StringType` is introduced in Step 4 for array elements only;
  `StringParam`/`StringField` keep their own validator properties.
- **Array-field semantic validation is per element in index order.** Check
  min/max first, then for each element check shape
  (`E_SEM_ARRAY_ELEMENT_TYPE`) and then parse/range/validator
  (`E_SEM_VALIDATOR_FAILED`) before moving to the next element. The first error
  wins.
- **`TryGetArray` validates both schema field and value kind.** Empty arrays are
  valid. A present field that is not an `ArrayField`, or a value that is not an
  `ArrayValue`, is `E_CMP_INCONSISTENT_TREE`.
- **Migration coverage is exhaustive.** The omissions found during review are
  part of the relevant steps: `Compilation/Tests/TransportCompilerTests.cs`,
  `Semantics/Tests/StructValidatorTests.cs`,
  `Canonicalization/Tests/CanonicalizationErrorFactoryTests.cs`, the suite
  harness assertions/builders, `grammar.md` §1/§8, and the
  `parsing.md` §13.2/§14.2/§14.3 list-element examples.

## 3. Current baseline

- Branch: `pontifex-2.0-features/factory-2.0`.
- `Tests/Pontifex.Factory.Tests`: 612 tests green.
- `Factory.Bootstrap`: 42 tests.
- `Factory.Compilation`: 38 tests.
- Current resolved values:
  - `ScalarValue(string Text)` — parameter scalar;
  - `TransportValue(ResolvedExpr)` — parameter transport wrapper;
  - `TransportListValue`;
  - `StructValue`;
  - `StringFieldValue` / `NestedStructFieldValue` for struct fields, under the
    abstract `ResolvedStructFieldValue`.
- Current AST: `AstList` contains only `AstExpr`.
- Current metadata:
  - `StringParam`, `TransportParam`, `TransportListParam`, `StructParam`;
  - `StringField`, `NestedStructField`.

## 4. Rules for every step

1. The step is implemented, tested, documented, and committed as one change.
2. `dotnet build Pontifex.sln --nologo` succeeds with zero errors.
3. All existing test suites pass.
4. New tests are added and recorded in the owning suite’s `Coverage.md`.
5. No step leaves a half-migrated value model, parser, or registration layer.
6. No formula assembly reference to `ITransport`, `ILogger`, `IMemoryRental`,
   `IConvertersGraph`, `IDescription`, `IElement`, or `Pontifex.OldFactory`.
7. No transport project or transport test project is changed by this plan.
8. Normative docs are updated in the same step that changes behavior.
9. New test suites follow `Tests/RULES.md`; new suites are created with their
   first test, never as empty layers.
10. No real I/O, timers, threads, or network activity.

## 5. Step overview

| Step | Name | Depends on | Main outcome |
|---|---|---|---|
| 1 | Unified resolved value model | — | `ResolvedExpr` is a value; `DataValue`; one struct-field value model |
| 2 | Integer and decimal scalars | 1 | Typed scalar metadata, resolution, printing, and build-context access |
| 3 | Generic list syntax | 2 | `AstList` of `AstValue`; transport-list element errors become semantic |
| 4 | `ArrayField` and `ArrayValue` | 3 | Homogeneous scalar arrays inside struct fields |
| 5 | Documentation, conformance, and hardening | 4 | Closed error catalogue, round-trip, and full docs |

---

## 6. Detailed steps

### Step 1 — Unified resolved value model

**Goal:** Make expressions values and unify parameter and struct-field values
around `DataValue`, without adding new scalar or array kinds.

**Depends on:** nothing.

**Areas:**
- `Pontifex.TransportFormula/Semantics/ResolvedDtos.cs`
- `Pontifex.TransportFormula/Semantics/Internal/ParameterValidator.cs`
- `Pontifex.TransportFormula/Semantics/Internal/StructValidator.cs`
- `Pontifex.TransportFormula/Canonicalization/Internal/FormulaCanonicalizer.cs`
- `Pontifex.TransportFormula/Canonicalization/Internal/PrinterInvariantValidator.cs`
- `Pontifex.Factory/Compilation/Internal/TransportBuildContext.cs`
- Existing Factory tests and harnesses

**Work items:**
1. Add `DataValue : ResolvedValue`.
2. Make `ResolvedExpr : ResolvedValue`.
3. Replace the sealed `ScalarValue(string Text)` with:
   - `abstract record ScalarValue : DataValue`;
   - `sealed record StringValue(string Text) : ScalarValue`.
4. Make `StructValue : DataValue`.
5. Remove `TransportValue`. A `TransportParam` stores its `ResolvedExpr`
   directly in `ParamBinding.Value`.
6. Change `StructFieldBinding.Value` to `DataValue`.
7. Remove `ResolvedStructFieldValue`, `StringFieldValue`, and
   `NestedStructFieldValue`.
8. Update the resolver:
   - string parameters/fields produce `StringValue`;
   - transport parameters produce `ResolvedExpr`;
   - struct parameters/fields produce `StructValue`;
   - transport lists remain `TransportListValue`.
9. Update the canonical printer and invariant validator:
   - handle `ResolvedExpr` directly;
   - read struct fields as `DataValue` and check the schema field kind;
   - for `StringParam`/`StringField`, match `StringValue` only, not the
     `ScalarValue` base, so later integer/decimal kinds cannot slip through;
   - keep `StringValue` printing.
10. Update the build context:
    - `TryGetString` checks `StringValue` only, not the `ScalarValue` base;
    - `TryGetTransport` checks `ResolvedExpr`;
    - `TryGetStruct` checks `StructValue`;
    - struct-field views use `DataValue`.
11. Update all tests and harnesses that reference `ScalarValue`,
    `TransportValue`, `ResolvedStructFieldValue`, `StringFieldValue`, or
    `NestedStructFieldValue`.
12. Update resolved-kind naming for `StringValue` to `string` in
    printer/compiler messages. `scalar` remains an AST shape.

**Tests:**
- All existing Factory tests compile and pass with the unified model.
- Transport parameter values are `ResolvedExpr`.
- String parameters/fields are `StringValue`.
- Nested struct fields are `StructValue`.
- A struct field value is assignable to `DataValue` (compile-time assertion).
- Printer and round-trip tests for strings, transports, and structs remain
  green.

**Test migration map:**
- `Semantics/Tests/ParameterValidatorTests.cs` — replace `ScalarValue` with
  `StringValue`; replace `TransportValue` assertions with `ResolvedExpr`.
- `Semantics/Tests/FormulaResolverTests.cs` — replace `ScalarValue`/
  `TransportValue` construction and pattern matching.
- `Semantics/Tests/StructValidatorTests.cs` — replace `StringFieldValue`/
  `NestedStructFieldValue` with `StringValue`/`StructValue`; update expected
  messages only where the template intentionally changed (it does not in this
  step).
- `Semantics/Harness/SemanticsAssertions.cs` — update resolved-value assertions.
- `Canonicalization/Harness/CanonicalizationTestData.cs` and
  `CanonicalizationAssertions.cs` — update value builders/assertions. Keep the
  helper names `Scalar` (parameter value) and `StringValue` (field value)
  distinct; if either is renamed, add `Canonicalization/Tests/PositionalFormatterTests.cs`
  to this map because it calls `CanonicalizationTestData.Scalar`.
- `Canonicalization/Tests/PrinterInvariantValidatorTests.cs` — update value
  kinds and removed wrappers.
- `Canonicalization/Tests/FormulaCanonicalizerTests.cs` — update
  `CanonicalizationTestData` helper calls and any `TransportValue` usage.
- `Canonicalization/Tests/CanonicalizationErrorFactoryTests.cs` — update
  kind-mismatch expectations to the new `string` vocabulary; the factory
  template itself is unchanged.
- `Compilation/Tests/TransportCompilerTests.cs` — update the hard-coded
  `compiler expected value kind scalar ...` expectation to `string`.
- `Compilation/Harness/CompilationTestData.cs` — update value builders.
- `Bootstrap` tests only where DTO signatures leak.
- Update `Coverage.md` rows in the affected suites.

**Documentation:**
- `parsing.md` §7.5, §7.8, §8, §10.3, §10.5, §11.1, §13.3, §13.5.
- `implementation-plan.md` §3, §8.1, §9.1, §11.3.
- Affected `Coverage.md` files.
- `nonapproved-behaviour.md` if a test-pinned assumption changes.

**Acceptance:**
- Build and all Factory tests pass.
- `grep` finds no `TransportValue`, `ResolvedStructFieldValue`,
  `StringFieldValue`, or `NestedStructFieldValue`.
- `grep` finds no test expectation `value kind scalar`; resolved string values
  use `string`.
- `ResolvedExpr` is assignable to `ResolvedValue`.
- `StructFieldBinding.Value` is `DataValue`.
- No transport/application projects changed.

---

### Step 2 — Integer and decimal scalars

**Goal:** Add `Int64` and `decimal` parameters and struct fields with range
validation and canonical printing.

**Depends on:** Step 1.

**Areas:**
- `Pontifex/Factory/Registration/ScalarType.cs` (new: core `IntegerType`,
  `DecimalType`; Step 4 adds `SingleType`, `StringType`)
- `Pontifex/Factory/Registration/TransportParameter.cs`
- `Pontifex/Factory/Registration/StructSchema.cs`
- `Pontifex/Factory/Registration/ITransportBuildContext.cs`
- `Pontifex.TransportFormula/Registry/MetadataDtos.cs`
- `Pontifex.TransportFormula/Registry/Internal/MetadataValidator.cs`
- `Pontifex.TransportFormula/Registry/Internal/RegistryErrorFactory.cs`
- `Pontifex.TransportFormula/Semantics/ResolvedDtos.cs`
- `Pontifex.TransportFormula/Semantics/Internal/ParameterValidator.cs`
- `Pontifex.TransportFormula/Semantics/Internal/StructValidator.cs`
- `Pontifex.TransportFormula/Semantics/Internal/SemanticErrorFactory.cs`
- `Pontifex.TransportFormula/Canonicalization/Internal/FormulaCanonicalizer.cs`
- `Pontifex.TransportFormula/Canonicalization/Internal/PrinterInvariantValidator.cs`
- `Pontifex.TransportFormula/Canonicalization/Internal/ScalarFormatter.cs`
- `Pontifex.TransportFormula/Canonicalization/Internal/CanonicalizationErrorFactory.cs`
- `Pontifex.TransportFormula/Internal/DecimalText.cs` (exact parser and
  canonical formatter)
- `Pontifex.Factory/Bootstrap/CoreRegistrationAdapter.cs`
- `Pontifex.Factory/Compilation/Internal/TransportBuildContext.cs`

**Work items:**
1. Add core `IntegerType(long min, long max)` and
   `DecimalType(decimal min, decimal max)`.
   - Constructors accept unsorted bounds; they do not throw.
   - The formula registry owns range validation.
2. Add core `IntegerParameter`/`DecimalParameter` (using core
   `IntegerType`/`DecimalType`) and formula `IntegerParam`/`DecimalParam`
   (using formula `IntegerType`/`DecimalType`).
3. Add core `IntegerField`/`DecimalField` and formula equivalents, each using
   the corresponding namespace’s `IntegerType`/`DecimalType`.
   - Constructors accept unsorted bounds.
4. Extend `MetadataValidator.NormalizeParameters` and
   `NormalizeStructSchema` with explicit cases for the new parameter/field
   kinds, preserving type/ranges and declaration order.
5. Add `IntegerValue(long)` and `DecimalValue(decimal)` to the resolved tree.
6. Resolver:
   - require `AstScalar` for scalar kinds;
   - keep `E_SEM_EXPECTED_SCALAR` / `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` for
     shape (the existing message templates do not change);
   - parse integer with `NumberStyles.AllowLeadingSign` and invariant culture;
   - parse decimal **exactly**:
     - accept a sign, decimal point, and exponent;
     - do not allow leading/trailing whitespace inside numeric text;
     - accept quoted numeric scalars;
     - parse into sign, coefficient digits, and exponent, then reduce to a
       decimal coefficient and scale;
     - compare the reduced coefficient as digit text or `BigInteger` against
       `79228162514264337593543950335`; a 64-bit accumulator is insufficient;
     - remove trailing zero digits before the scale check, so `1.0e-28` is
       accepted and `0e-29`/`0e29` reduce to `0`;
     - accept only values whose reduced coefficient fits the 96-bit decimal
       coefficient and whose scale is 0..28;
     - negative zero parses to `0` and prints as `0`;
     - reject overflow, underflow, and precision loss as parse failures with
       reason `expected a decimal` (for example `1e29`, `1e-29`, `6e-29`, and
       `0.12345678901234567890123456789`);
     - `decimal.TryParse` alone is insufficient because it accepts `1e-29` as
       `0` and rounds 29+ significant digits; use the shared exact parser in
       `Internal/DecimalText.cs`;
   - enforce `IntegerType`/`DecimalType` ranges after successful exact parsing;
   - store typed values;
   - reuse `E_SEM_VALIDATOR_FAILED` with reason `expected an integer`
     (parse failure), `expected a decimal` (parse failure), or
     `out of range [<min>, <max>]` (parsed but outside);
   - a literal not representable in `long`/`decimal` is a parse failure, not an
     out-of-range value; it uses `expected an integer`/`expected a decimal`.
7. Canonical printer:
   - integer: invariant, bare;
   - decimal: canonical plain form via `DecimalText.Format` (trim trailing
     zeros, no exponent, normalize negative zero); do not use `"G29"`, which
     emits exponent notation for small values;
   - print `1.50` as `1.5`.
8. `PrinterInvariantValidator`:
   - validate value kind and range;
   - `StringParam`/`StringField` accept only `StringValue`;
     `IntegerParam`/`IntegerField` accept only `IntegerValue`;
     `DecimalParam`/`DecimalField` accept only `DecimalValue`;
   - use `E_PRN_VALUE_OUT_OF_RANGE` for out-of-range values;
   - validate decimal and string printability with the shared helpers.
9. `MetadataValidator`/`RegistryErrorFactory`:
   - `E_REG_INVALID_INTEGER_RANGE` and `E_REG_INVALID_DECIMAL_RANGE`;
   - use `DecimalText.Format` for decimal bounds;
   - validation order: a full type-set normalization pass, then a full scalar
     parameter range pass in declaration order; rule 7 struct fields in schema
     order, after field name/duplicate/kind.
10. `CoreRegistrationAdapter`: map core `IntegerType`/`DecimalType` to formula
    equivalents; do not map or replicate range validation.
11. Extend `ITransportBuildContext` with `TryGetInt64` and `TryGetDecimal`;
    extend `ITransportStructValue` with the same.
    - A wrong parameter kind is `E_CMP_PARAM_KIND_MISMATCH`.
    - A wrong struct-field kind is `E_CMP_INCONSISTENT_TREE`, matching the
      existing `TryGetString`/`TryGetStruct` behavior.
12. Extend the resolved-value kind vocabulary with `integer` and `decimal`
    only. Do not add `array` until Step 4.
13. Update `E_PRN_PARAM_KIND_MISMATCH`,
    `E_PRN_STRUCT_FIELD_KIND_MISMATCH`, and
    `E_CMP_PARAM_KIND_MISMATCH` to use the new kind names. The existing
    `parsing.md` §13.6 templates are unchanged except for `<expected>`/`<actual>`
    vocabulary.

**Tests:**
- Registry: valid/invalid integer and decimal ranges; validation order,
  including the cross-kind case: type sets are normalized for all parameters
  first, then scalar parameter ranges are checked in declaration order, so a
  later scalar range error precedes an earlier transport-list range error.
- Bootstrap: `CoreRegistrationAdapter` maps integer/decimal parameters and
  fields; unsorted core bounds reach `E_REG_INVALID_*` through
  `RegisterTransport`.
- Semantics: parsing, negatives, overflow, min/max boundaries, `1.50`, `1.5`,
  `1e3`, `.5`, `1.`, `+1`, `-0`, wrong shape, field paths. Quoted numeric
  scalars are accepted; numeric text with leading/trailing whitespace is
  rejected. Exact decimal edge cases: `1e-28` accepted; `1e-29`, `-1e-29`,
  `5e-29`, `6e-29`, `0.00000000000000000000000000001`, and 29+
  significant-digit fractions rejected with `expected a decimal`; `1e29`
  rejected; `decimal.MinValue` and `decimal.MaxValue` accepted; `1.0e-28`
  accepted; `0e-29`/`0e29` accepted as `0`; the 29-digit
  `7922816251426433759354395033.5` accepted (96-bit coefficient, scale 1). A
  literal not representable in `long`/`decimal` is a parse failure with reason
  `expected an integer`/`expected a decimal`; only a successfully parsed value
  outside `Min`/`Max` uses `out of range [<min>, <max>]`.
- Canonicalization: `1.50` → `1.5`; `1e3` → `1000`; `1e-28` printed in plain
  form; `decimal.MinValue`/`decimal.MaxValue` printed in plain form;
  `-0`/`-0.0` → `0`; `1.0`/`1.000…0` → `1`; exponent input canonicalized to
  plain output; round-trip.
- Compilation: `TryGetInt64`/`TryGetDecimal` deliver typed values; kind mismatch
  messages use the new vocabulary.
- Kind isolation: a string parameter/field bound to `IntegerValue` or
  `DecimalValue` reports the appropriate kind mismatch; an integer/decimal
  parameter/field bound to `StringValue` does the same.
- Existing string/transport/struct tests remain green.

**Test migration map:**
- `Registry/Tests/TransportRegistryBuilderTests.cs` — add integer/decimal
  parameter/field cases, exact `E_REG_*` messages, and validation order.
- `Registry/Tests/TransportTypeCatalogTests.cs` — no change expected.
- `Bootstrap/Tests/TransportFactoryBuilderTests.cs` — adapter mapping for
  integer/decimal parameters and fields; unsorted bounds reach `E_REG_INVALID_*`.
- `Semantics/Tests/ParameterValidatorTests.cs` — integer/decimal parsing and
  range failures.
- `Semantics/Tests/StructValidatorTests.cs` — integer/decimal fields and
  messages.
- `Semantics/Tests/SemanticErrorTests.cs` — new `E_SEM_*` cases; keep existing
  scalar-field templates.
- `Canonicalization/Tests/DecimalTextTests.cs` (new file; update
  `Canonicalization/README.md` directory layout and `Coverage.md`) — exact
  decimal parsing and canonical form, including underflow/rounding rejection.
  `ScalarFormatterTests` remains string-quoting-only.
- `Canonicalization/Tests/FormulaCanonicalizerTests.cs` — integer/decimal
  parameter and field printing.
- `Canonicalization/Tests/PrinterInvariantValidatorTests.cs` — range and kind
  errors.
- `Canonicalization/Tests/CanonicalizationErrorFactoryTests.cs` — new
  `E_PRN_VALUE_OUT_OF_RANGE` template.
- `Canonicalization/Harness/CanonicalizationTestData.cs` and
  `CanonicalizationAssertions.cs` — integer/decimal builders and assertions.
- `Semantics/Harness/SemanticsTestData.cs` and `SemanticsAssertions.cs` —
  integer/decimal metadata and value assertions.
- `Registry/Harness/RegistryTestData.cs` — integer/decimal parameter/field
  builders.
- `Bootstrap/Harness/BootstrapTestData.cs` — core integer/decimal builders.
- `Compilation/Tests/TransportCompilerTests.cs` — typed context access and kind
  messages.
- `Compilation/Harness/CompilationTestData.cs` — integer/decimal builders.
- Update `Coverage.md` files for Registry, Bootstrap, Semantics,
  Canonicalization, and Compilation.

**Documentation:**
- `grammar.md` §7, §8 — exact integer/decimal forms and canonical scalar text.
- `parsing.md` §6.2, §6.5, §6.7, §6.9, §7.5, §7.8, §8, §9.1, §10.2, §10.3,
  §10.5, §11.1, §13.3, §13.4, §13.5, §13.6, §14.
- `implementation-plan.md` §3, §8.1, §8.2, §8.3, §8.5, §9.1, §9.2, §9.5,
  §10.2, §10.3, §10.4, §11.3, §11.6, §12.4.
- Update `Canonicalization/README.md` layout/subject for `DecimalTextTests.cs`
  and add `DecimalText` to the `Coverage.md` subject group.
- Suite coverage tables.

**Acceptance:**
- Build and all Factory tests pass.
- Integer/decimal parameters and fields resolve, range-check, and print
  canonically.
- Exact decimal literals are enforced: `1e-29`, `6e-29`, and 29+ significant
  digits are parse failures; `1e-28`, `decimal.MinValue`, and `decimal.MaxValue`
  are accepted.
- `E_REG_INVALID_INTEGER_RANGE`/`E_REG_INVALID_DECIMAL_RANGE` are reachable and
  tested.
- No arrays or `SingleType` yet.

---

### Step 3 — Generic list syntax and transport-list adaptation

**Goal:** Make `AstList` a generic value list so arrays can use the same syntax
in Step 4, while keeping transport lists correct.

**Depends on:** Step 2.

**Areas:**
- `Pontifex.TransportFormula/Parsing/AstDtos.cs`
- `Pontifex.TransportFormula/Parsing/Internal/FormulaParser.cs`
- `Pontifex.TransportFormula/Parsing/Internal/ParsingErrorFactory.cs`
- `Pontifex.TransportFormula/Semantics/Internal/ParameterValidator.cs`
- `Pontifex.TransportFormula/Semantics/Internal/SemanticErrorFactory.cs`
- Parser, semantics, and printer tests

**Work items:**
1. Change `AstList.Elements` to `IReadOnlyList<AstValue>`.
2. Define the grammar production:
   `list_element := expr | struct | quoted | bare`
   so nested lists are syntactically excluded while scalars, structs, and
   expressions are allowed.
3. Parser parses generic list elements.
4. Keep `E_SYN_NESTED_LIST`; update its message to “nested lists are not
   allowed”.
5. Keep `E_SYN_TRAILING_COMMA`; use the generic context “list”, not “transport
   list”.
6. Change the list-element expected string from “a transport expression”
   (`FormulaParser.ParseList`) to “a value”; update the `parsing.md` §13.2
   “List element” row, the §14.2 rows, and `Parsing/Coverage.md`.
7. Keep top-level list syntax invalid.
8. Change `ParameterValidator.Shape(AstList)` to return `"list"` instead of
   `"transport list"`.
9. `TransportListParam` resolution:
   - require every element to be `AstExpr`;
   - otherwise `E_SEM_LIST_ELEMENT_TYPE` at the element offset;
   - keep min/max/homogeneity behavior.
10. Update parser/AST/error tests and docs for the error-phase shift:
    scalars/structs inside a transport list now parse and fail semantically.
    Update all hard-coded AST-shape assertions that say `transport list` to
    `list`, including `Semantics/Tests/StructValidatorTests.cs`.
11. Update `grammar.md` §7/§9 and `parsing.md` §13.2/§14.2 examples that
    currently declare list elements invalid. `alpha([beta(), 1])`,
    `alpha([alpha(), 1])`, and `alpha([alpha])` are now syntactically valid; for
    a transport list they fail semantically with `E_SEM_LIST_ELEMENT_TYPE`.
    Update the `parsing.md` §13.2 note that currently says `alpha([alpha])`
    reports `expected '('`.

**Tests:**
- `[alpha()]` still resolves as a transport list.
- `[1]` and `[{}]` in a transport list → `E_SEM_LIST_ELEMENT_TYPE`.
- `alpha([alpha])` and `alpha([alpha(), 1])` parse and fail semantically for a
  transport list, replacing the old syntax errors.
- Nested arrays → `E_SYN_NESTED_LIST`.
- Top-level list → syntax error.
- Shape errors report actual `"list"`.
- AST tests assert `AstList` elements are `AstValue`.
- Trailing comma and list-element expected strings use the generic list
  context.
- Transport-list round-trip remains green.

**Test migration map:**
- `Parsing/Tests/ParserTests.cs` — AST list element type, expected strings,
  trailing comma context, nested-list message, examples that move from syntax to
  semantics.
- `Semantics/Tests/ParameterValidatorTests.cs` — `E_SEM_LIST_ELEMENT_TYPE`.
- `Semantics/Tests/StructValidatorTests.cs` — AST-shape messages change from
  `transport list` to `list`.
- `Semantics/Tests/SemanticErrorTests.cs` — new semantic error rows and updated
  list-shape messages.
- `Canonicalization/Tests/FormulaCanonicalizerTests.cs` — transport list
  round-trip remains; no shape change.
- `parsing.md` §14.2 matrix, `Parsing/Coverage.md`, and `Semantics/Coverage.md`
  rows.

**Documentation:**
- `grammar.md` §1, §5, §6, §7, §9.
- `parsing.md` §4, §5.6, §7.5, §12.2, §13.2, §13.3, §14.2, §14.3. Move
  the `alpha([alpha(), 1])` and `alpha([alpha])` rows from syntax to semantic
  and add `E_SEM_LIST_ELEMENT_TYPE` examples.
- `implementation-plan.md` §3, §7.1, §7.4, §7.5.
- Coverage tables.

**Acceptance:**
- Build and all Factory tests pass.
- Transport lists work.
- Invalid transport-list elements are semantic errors.
- `grep` finds no Factory test assertion of the AST shape `transport list`;
  resolved-kind `transport list` assertions remain valid.
- No data arrays yet.

---

### Step 4 — `ArrayField` and `ArrayValue`

**Goal:** Add homogeneous scalar arrays inside struct fields.

**Depends on:** Step 3.

**Areas:**
- `Pontifex/Factory/Registration/ScalarType.cs` (Step 2 file; Step 4 adds
  `SingleType`, `StringType`)
- `Pontifex/Factory/Registration/StructSchema.cs`
- `Pontifex/Factory/Registration/ITransportBuildContext.cs`
- `Pontifex.TransportFormula/Registry/MetadataDtos.cs`
- `Pontifex.TransportFormula/Registry/Internal/MetadataValidator.cs`
- `Pontifex.TransportFormula/Registry/Internal/RegistryErrorFactory.cs`
- `Pontifex.TransportFormula/Semantics/ResolvedDtos.cs`
- `Pontifex.TransportFormula/Semantics/Internal/StructValidator.cs`
- `Pontifex.TransportFormula/Semantics/Internal/SemanticErrorFactory.cs`
- `Pontifex.TransportFormula/Canonicalization/Internal/FormulaCanonicalizer.cs`
- `Pontifex.TransportFormula/Canonicalization/Internal/PrinterInvariantValidator.cs`
- `Pontifex.TransportFormula/Canonicalization/Internal/CanonicalizationErrorFactory.cs`
- `Pontifex.Factory/Bootstrap/CoreRegistrationAdapter.cs`
- `Pontifex.Factory/Compilation/Internal/TransportBuildContext.cs`

**Work items:**
1. Add `SingleType` to both core (`Pontifex.Factory.Registration`) and formula
   (`Pontifex.TransportFormula.Registry`) metadata with an internal constructor
   and a closed public hierarchy: `StringType`, `IntegerType`, `DecimalType`.
   No `StructType`.
   - Step 2’s `IntegerType`/`DecimalType` are re-parented under `SingleType`;
     there are no duplicate integer/decimal types.
   - The abstract `SingleType` constructor is internal so the hierarchy stays
     closed; the concrete `StringType`/`IntegerType`/`DecimalType` constructors
     are public so transport packages can declare array fields.
   - `ArrayField.ElementType` uses the `SingleType` of its own namespace.
2. `StringType` carries `ScalarValidator? Validate`; the validator is applied
   per element. `StringType` is the array-element type only; `StringParam` and
   `StringField` keep their existing `Validate` properties.
3. Add `ArrayField : StructField` with:
   - `SingleType ElementType`;
   - `int Min`;
   - `int? Max`.
   Core `ArrayField` does not pre-validate ranges; the formula registry owns
   validation so `E_REG_INVALID_ARRAY_RANGE` is reachable through the adapter.
4. Add `ArrayValue : DataValue` with `IReadOnlyList<DataValue> Elements`. The
   DTO permits any `DataValue` so tests can build invalid trees; a valid resolved
   tree restricts elements to `StringValue`/`IntegerValue`/`DecimalValue`.
5. Add `ITransportArrayValue`:
   - `int Count`;
   - `TryGetString(int index, out string value)`;
   - `TryGetInt64(int index, out long value)`;
   - `TryGetDecimal(int index, out decimal value)`.
   Behavior:
   - empty arrays have `Count == 0` and are valid;
   - the parent `TryGetArray` returns `false` when the field is absent;
   - `TryGet*` returns `false` for a wrong element kind;
   - `TryGet*` throws `ArgumentOutOfRangeException` for `index < 0` or
     `index >= Count` (programmer error).
6. Add `bool TryGetArray(string fieldName, out ITransportArrayValue value)` to
   `ITransportStructValue`. If the field is present but its value is not an
   `ArrayValue`, or its schema field is not an `ArrayField`, throw
   `CompilerException` with `E_CMP_INCONSISTENT_TREE`.
7. Mirror `ArrayField` and the `SingleType` hierarchy in formula metadata; map
   `ArrayField` and each `SingleType` subclass (including `StringType`
   validators) in `CoreRegistrationAdapter`.
8. Extend `MetadataValidator.NormalizeStructSchema` with an `ArrayField` case,
   preserving `ElementType`, `Min`, and `Max`.
9. `MetadataValidator`:
   - `E_REG_INVALID_ARRAY_RANGE` when `Min < 0` or `Max < Min`;
   - `E_REG_INVALID_INTEGER_RANGE`/`E_REG_INVALID_DECIMAL_RANGE` for an
     `ArrayField.ElementType` with unsorted bounds;
   - validate in struct-schema rule 7, in schema order: field kind, array count
     range, element scalar range, then nested schema.
10. `StructValidator`:
    - require `AstList`; else `E_SEM_STRUCT_FIELD_EXPECTED_ARRAY`;
    - check min/max before elements (`E_SEM_ARRAY_MIN`/`E_SEM_ARRAY_MAX`);
    - validate elements in index order, first error wins:
      1. shape check: transport/list/struct element →
         `E_SEM_ARRAY_ELEMENT_TYPE`;
      2. value check: resolve the scalar element to
         `StringValue`/`IntegerValue`/`DecimalValue`; string validator failure
         or integer/decimal parse/range failure → `E_SEM_VALIDATOR_FAILED` in
         the field form
         `field '<path>[<index>]' of parameter '<param>' is invalid: <reason>`,
         where `<path>` is the full dot-separated field path (for example
         `config.values[2]`);
    - continue to the next element only after the current element succeeds.
11. Canonical printer:
    - print arrays as `[elem, elem]`;
    - homogeneous element type;
    - minimal string quoting, integer/decimal canonical formatting using the
      shared canonical decimal formatter.
12. `PrinterInvariantValidator`:
    - `E_PRN_ARRAY_ARITY`;
    - `E_PRN_ARRAY_ELEMENT_TYPE`;
    - `E_PRN_VALUE_OUT_OF_RANGE` for integer/decimal elements;
    - `E_PRN_INVALID_SCALAR` for string elements;
    - `E_PRN_STRUCT_FIELD_KIND_MISMATCH` when the field value is not an
      `ArrayValue`; `<expected>` is `array`;
    - validate array fields in order: arity, then for each element in index
      order, kind, then range/printability.
13. Define array field paths as `field.path[index]` for diagnostics.
14. Add `array` to the resolved-kind vocabulary in this step only.
15. No `ArrayParam`, no nested arrays, no `StructType`.

**Tests:**
- Registry: array field metadata, min/max, invalid range, validation order,
  element type scalar range.
- Bootstrap: `CoreRegistrationAdapter` maps `ArrayField`; unsorted
  `ArrayField.Min/Max` reaches `E_REG_INVALID_ARRAY_RANGE`; unsorted array
  element `IntegerType`/`DecimalType` reaches
  `E_REG_INVALID_INTEGER_RANGE`/`E_REG_INVALID_DECIMAL_RANGE`.
- Semantics: string/integer/decimal arrays; empty array; min/max; wrong element
  kind; transport element; nested array parse rejection; field paths with
  indices; array element parse/range failures; exact decimal underflow/rounding
  rejection inside arrays; string element validator; per-element first-error
  order.
- Canonicalization: array printing, empty array, round-trip, decimal/integer
  canonical forms inside arrays; string-element printability; array
  arity/kind/range invariants.
- Compilation: `TryGetArray` and typed element access; absent optional array;
  empty array; wrong element kind; out-of-range index throws; a present
  non-array schema field reports `E_CMP_INCONSISTENT_TREE`. Assert
  `ArgumentOutOfRangeException` on the `ITransportArrayValue` directly, outside
  a constructor, because the compiler wraps non-`CompilerException` failures as
  `E_BLD_CONSTRUCTOR_FAILED`.
- Existing tests remain green.

**Test migration map:**
- `Registry/Tests/TransportRegistryBuilderTests.cs` — array schema/range cases,
  element type scalar ranges, exact `E_REG_*` messages, validation order.
- `Bootstrap/Tests/TransportFactoryBuilderTests.cs` — adapter mapping for
  `ArrayField`; unsorted array/element ranges reach `E_REG_INVALID_*`.
- `Semantics/Tests/StructValidatorTests.cs` — array fields, per-element error
  order, and element errors.
- `Semantics/Tests/SemanticErrorTests.cs` — `E_SEM_ARRAY_*`.
- `Canonicalization/Tests/FormulaCanonicalizerTests.cs` — array printing.
- `Canonicalization/Tests/PrinterInvariantValidatorTests.cs` — `E_PRN_ARRAY_*`,
  `E_PRN_VALUE_OUT_OF_RANGE`, `E_PRN_INVALID_SCALAR`.
- `Canonicalization/Tests/CanonicalizationErrorFactoryTests.cs` — new array and
  range error templates.
- `Canonicalization/Harness/CanonicalizationTestData.cs` and
  `CanonicalizationAssertions.cs` — array builders and assertions.
- `Semantics/Harness/SemanticsTestData.cs` and `SemanticsAssertions.cs` — array
  metadata and value assertions.
- `Registry/Harness/RegistryTestData.cs` — array metadata builders.
- `Bootstrap/Harness/BootstrapTestData.cs` — core array field builders.
- `Compilation/Tests/TransportCompilerTests.cs` — array context access,
  `TryGetArray` invariants, and empty arrays.
- `Compilation/Harness/CompilationTestData.cs` — array builders.
- Update `Coverage.md` files for Registry, Bootstrap, Semantics,
  Canonicalization, and Compilation.

**Documentation:**
- `grammar.md` §5, §6, §7, §9.
- `parsing.md` §6.5, §6.7, §6.9, §7.5, §7.8, §7.9, §8, §9.1, §10.3,
  §10.5, §11.1, §12.2, §13.3, §13.4, §13.5, §13.6, §14.
- `implementation-plan.md` §3, §8.1, §8.2, §8.3, §8.5, §9.1, §9.2, §9.5,
  §10.2, §10.3, §10.4, §11.3, §11.6, §12.2, §12.4.
- Update `ITransportBuildContext` XML comment: struct fields may contain arrays,
  but not transports or transport lists.
- Add XML comments for `ITransportArrayValue` empty-array and out-of-range
  behavior.
- Suite coverage tables.

**Acceptance:**
- Build and all Factory tests pass.
- Arrays work only inside struct fields.
- A valid `ArrayValue` contains only scalar leaves; nested arrays and struct
  elements are rejected by semantic/printer invariants.
- Array semantic validation reports the first error in index order, and empty
  arrays are valid.
- `E_REG_INVALID_ARRAY_RANGE`, `E_SEM_STRUCT_FIELD_EXPECTED_ARRAY`,
  `E_SEM_ARRAY_*`, and `E_PRN_ARRAY_*` are tested.
- No `ArrayParam`, no nested arrays, no `StructType`.

---

### Step 5 — Documentation, conformance, and hardening

**Goal:** Close the error catalogue, update all normative docs, and run the full
conformance pass.

**Depends on:** Step 4.

**Work items:**
1. Update `grammar.md`: metadata-driven scalar typing, exact integer/decimal
   forms, generic lists, homogeneous arrays, no nested arrays, no transport
   elements.
2. Update `parsing.md`:
   - AST, resolved tree, validation, printer, error catalogue, conformance;
   - exact messages, offsets, phases, and placeholder sources;
   - kind vocabulary;
   - array element paths;
   - equality/round-trip definitions.
3. Update `implementation-plan.md` §3 (approved decisions), §8, §9, §10, §11,
   §12, §13, §14, §16.
4. Update all suite `README.md`/`Coverage.md` files.
5. Update `nonapproved-behaviour.md` with any new test-pinned assumptions.
6. Add property/round-trip tests:
   - `Print(t)` is stable:
     `Print(Resolve(Parse(Print(t)))) == Print(t)`;
   - extend the existing span-insensitive structural comparer
     `CanonicalizationAssertions.AssertTreesEqual` with `IntegerValue`,
     `DecimalValue`, and `ArrayValue` cases; it already compares lists and
     structs element-wise by reference-identity metadata;
   - default C# record equality compares `IReadOnlyList<T>` by reference and is
     not sufficient;
   - apply the property only to valid resolved trees and use the comparer for
     the selected round-trip/equality tests; document that `parsing.md` §11.2’s
     equality property uses the same span-insensitive comparison;
   - if a property test lives outside the Canonicalization suite, add a local
     copy or a suite-local helper because `Tests/RULES.md` HARNESS-05 forbids
     sibling suites from sharing harness.
7. Run the full error catalogue for new codes.
8. Verify dependency isolation.
9. Confirm `dotnet build` and `dotnet test` clean.
10. Run a repository-wide grep for removed type names and old kind literals
    (`TransportValue`, `StringFieldValue`, `NestedStructFieldValue`,
    `ResolvedStructFieldValue`, `value kind scalar`, and AST-shape
    `transport list`) and update or justify every match.

**Documentation matrix:**
- `grammar.md`: §5, §6, §7, §8, §9, conformance examples.
- `parsing.md`: §4, §5.6, §6.2, §6.5, §6.7, §6.9, §7.5, §7.8, §7.9, §8,
  §9.1, §10.2, §10.3, §10.5, §11.1, §11.2, §12.2, §13.2, §13.3, §13.4,
  §13.5, §13.6, §14.
- `implementation-plan.md`: §3, §7, §8, §9, §10, §11, §12, §13, §14, §16.
- All `Tests/Pontifex.Factory.Tests/*/Coverage.md` files.
- `Tests/Pontifex.Factory.Tests/nonapproved-behaviour.md`.

**Acceptance:**
- All documentation matches the implementation.
- All new error codes and messages are covered by tests.
- Exact decimal overflow, underflow, and precision-loss cases are covered.
- Round-trip properties pass.
- No stale removed type names or old kind literals remain.
- Full solution build and all Factory tests pass.
- No transport/application projects changed.

---

## 7. Error catalogue

Only the codes listed below are added or changed. Every other code in
`parsing.md` §13 remains unchanged, except for the Step 3 syntax wording:
`E_SYN_NESTED_LIST` message, the `E_SYN_TRAILING_COMMA` list context, and the
list-element `<expected>` string used by `E_SYN_UNEXPECTED_TOKEN`.

### 7.1 Kind vocabulary

AST shape (`ParameterValidator.Shape`):

- `scalar`
- `transport`
- `list`
- `struct`

Resolved value kind (printer/compiler messages):

- `string`
- `integer`
- `decimal`
- `struct`
- `array` (from Step 4)
- `transport`
- `transport list`

Semantic array element `<expected>` values: `a string`, `an integer`,
`a decimal`. Semantic array element `<actual>` is the AST shape.

Printer/compiler array element `<expected>`/`<actual>` values are resolved
kinds.

### 7.2 Semantic errors — phase `Resolution`

| Code | Condition | Message template | Offset |
|---|---|---|---|
| `E_SEM_EXPECTED_SCALAR` | scalar parameter got transport/list/struct | `parameter '<name>' expects a scalar value, got <shape>` | value start |
| `E_SEM_EXPECTED_TRANSPORT` | unchanged | unchanged | value start |
| `E_SEM_EXPECTED_TRANSPORT_LIST` | unchanged | unchanged | value start |
| `E_SEM_EXPECTED_STRUCT` | unchanged | unchanged | value start |
| `E_SEM_LIST_ELEMENT_TYPE` | transport-list element is not `AstExpr` | `transport list for '<name>' expects transport elements, got <shape>` | element start |
| `E_SEM_LIST_MIN` / `E_SEM_LIST_MAX` / `E_SEM_LIST_HOMOGENEITY` | unchanged | unchanged | unchanged |
| `E_SEM_VALIDATOR_FAILED` | string validator, integer/decimal parse or range, array element parse/range/string validator | parameter/field form with reason | value/element start |
| `E_SEM_STRUCT_FIELD_EXPECTED_SCALAR` | scalar struct field got transport/list/struct | `field '<path>' expects a scalar value, got <shape>` (existing template) | value start |
| `E_SEM_STRUCT_FIELD_EXPECTED_STRUCT` | unchanged | unchanged | value start |
| `E_SEM_STRUCT_FIELD_EXPECTED_ARRAY` | array field got non-list | `field '<path>' expects an array, got <shape>` | value start |
| `E_SEM_ARRAY_ELEMENT_TYPE` | array element shape is not scalar | `array element '<path>[<index>]' of parameter '<param>' expects <expected>, got <actual>` | element start |
| `E_SEM_ARRAY_MIN` | count < min | `array field '<path>' of parameter '<param>' requires at least <min> element(s), got <count>` | array `[` |
| `E_SEM_ARRAY_MAX` | count > max | `array field '<path>' of parameter '<param>' requires at most <max> element(s), got <count>` | array `[` |

`<path>` is the dot-separated field path; array element paths append
`[<index>]`. `<param>` is the canonical parameter name.

Integer/decimal element parse/range failures use `E_SEM_VALIDATOR_FAILED` with
reason `expected an integer` / `expected a decimal` / `out of range [<min>,
<max>]`. A string element validator failure uses the same code with the
validator reason. For an array element the field form is
`field '<path>[<index>]' of parameter '<param>' is invalid: <reason>`, where
`<path>` is the full dot-separated field path (for example `config.values[2]`).
Decimal parse failures include overflow, underflow, and precision loss;
`decimal.TryParse` accepting a rounded value is not sufficient.

Array-field validation order is count (`E_SEM_ARRAY_MIN`/`E_SEM_ARRAY_MAX`), then
each element in index order: shape (`E_SEM_ARRAY_ELEMENT_TYPE`), then
parse/range/validator (`E_SEM_VALIDATOR_FAILED`). The first error wins.

### 7.3 Registration errors — phase `Registration`

No source offset.

| Code | Condition | Message template |
|---|---|---|
| `E_REG_INVALID_INTEGER_RANGE` | integer parameter, field, or array element type `Min > Max` | `invalid integer range: min <min>, max <max>` |
| `E_REG_INVALID_DECIMAL_RANGE` | decimal parameter, field, or array element type `Min > Max` | `invalid decimal range: min <min>, max <max>` |
| `E_REG_INVALID_ARRAY_RANGE` | `Min < 0` or `Max < Min` | `invalid array range: min <min>, max <max>` |

`<max>` is `unbounded` when `Max` is `null`. Decimal `<min>`/`<max>` use the
same canonical decimal text as the printer.

Validation order:
- rule 5: a full pass normalizes type sets for all parameters, then a second
  full pass checks scalar parameter ranges in declaration order; consequently a
  later scalar range error precedes an earlier transport-list range error;
- rule 6: transport list ranges;
- rule 7: struct schemas, depth-first in schema order:
  field name, duplicate, kind, then:
  - array count range (`E_REG_INVALID_ARRAY_RANGE`);
  - array element scalar range (`E_REG_INVALID_INTEGER_RANGE` /
    `E_REG_INVALID_DECIMAL_RANGE`);
  - integer/decimal field range;
  - nested schema recursion.

Core `IntegerType`, `DecimalType`, `IntegerParameter`, `DecimalParameter`,
`IntegerField`, `DecimalField`, and `ArrayField` accept unsorted bounds and do
not throw. The formula registry owns range validation, making the `E_REG_*`
codes reachable through `CoreRegistrationAdapter`.

### 7.4 Printer errors — phase `Printing`

No source offset.

| Code | Condition | Message template |
|---|---|---|
| `E_PRN_VALUE_OUT_OF_RANGE` | resolved integer/decimal outside its type range | `resolved value for '<path>' is out of range: <value>` |
| `E_PRN_ARRAY_ARITY` | array count outside min/max | `resolved array field '<path>' of parameter '<param>' has <count> element(s), expected <range>` |
| `E_PRN_ARRAY_ELEMENT_TYPE` | array element kind mismatch | `resolved array element '<path>[<index>]' of parameter '<param>' expects <expected>, got <actual>` |
| `E_PRN_PARAM_KIND_MISMATCH` | existing code, updated kind vocabulary | existing template with new kinds |
| `E_PRN_STRUCT_FIELD_KIND_MISMATCH` | existing code; also used when an array field does not contain an `ArrayValue` | existing template with new kinds |
| `E_PRN_INVALID_SCALAR` | existing code; also used for a string array element containing CR, LF, or an unpaired surrogate | existing template |
| `E_PRN_INCONSISTENT_TREE` | other invariants | existing template |

`<path>` for `E_PRN_VALUE_OUT_OF_RANGE` is the parameter name for a top-level
parameter, or the dot-separated field path for a struct field. For an array
element, `<path>` includes the index: `field.path[<index>]`. `<param>` is the
canonical parameter name. `<value>` is invariant integer/decimal text; decimal
text uses the canonical decimal formatter. `<range>` is `min..max` or
`at least <min>`; `ArrayField.Min` is non-nullable. `<expected>`/`<actual>` in
`E_PRN_ARRAY_ELEMENT_TYPE` are resolved kinds.

Printer validation order for an array field: arity, then for each element in
index order, element kind, then range or string printability.
For an array field, `E_PRN_STRUCT_FIELD_KIND_MISMATCH` uses `<expected>` =
`array`.

### 7.5 Compiler errors — phase `Compilation`

No source offset. Existing `E_CMP_*` templates are unchanged except for the
kind vocabulary.

| Code | Condition | Message template |
|---|---|---|
| `E_CMP_PARAM_KIND_MISMATCH` | existing code | `compiler expected value kind <expected> for parameter '<name>', got <actual>` |
| `E_CMP_INCONSISTENT_TREE` | existing code | existing template |
| `E_CMP_DUPLICATE_PARAM` / `E_CMP_UNKNOWN_PARAM` | existing codes | unchanged |

`<expected>`/`<actual>` use the resolved-kind vocabulary. This is the only
`E_CMP` contract change in this plan. All other `E_CMP_*` codes are unchanged.

## 8. Test migration map

### Step 1
- `Semantics/Tests/ParameterValidatorTests.cs`
- `Semantics/Tests/FormulaResolverTests.cs`
- `Semantics/Tests/StructValidatorTests.cs`
- `Semantics/Harness/SemanticsAssertions.cs`
- `Canonicalization/Harness/CanonicalizationTestData.cs`
- `Canonicalization/Harness/CanonicalizationAssertions.cs`
- `Canonicalization/Tests/PrinterInvariantValidatorTests.cs`
- `Canonicalization/Tests/FormulaCanonicalizerTests.cs`
- `Canonicalization/Tests/CanonicalizationErrorFactoryTests.cs`
- `Compilation/Harness/CompilationTestData.cs`
- `Compilation/Tests/TransportCompilerTests.cs`
- Affected `Coverage.md` rows

### Step 2
- `Registry/Tests/TransportRegistryBuilderTests.cs`
- `Bootstrap/Tests/TransportFactoryBuilderTests.cs`
- `Semantics/Tests/ParameterValidatorTests.cs`
- `Semantics/Tests/StructValidatorTests.cs`
- `Semantics/Tests/SemanticErrorTests.cs`
- `Semantics/Harness/SemanticsTestData.cs`
- `Semantics/Harness/SemanticsAssertions.cs`
- `Canonicalization/Tests/DecimalTextTests.cs` (new; update README layout and
  Coverage)
- `Canonicalization/Tests/ScalarFormatterTests.cs` (string-only; no decimal
  cases)
- `Canonicalization/Tests/FormulaCanonicalizerTests.cs`
- `Canonicalization/Tests/PrinterInvariantValidatorTests.cs`
- `Canonicalization/Tests/CanonicalizationErrorFactoryTests.cs`
- `Canonicalization/Harness/CanonicalizationTestData.cs`
- `Canonicalization/Harness/CanonicalizationAssertions.cs`
- `Registry/Harness/RegistryTestData.cs`
- `Bootstrap/Harness/BootstrapTestData.cs`
- `Compilation/Tests/TransportCompilerTests.cs`
- `Compilation/Harness/CompilationTestData.cs`
- Affected `Coverage.md` rows

### Step 3
- `Parsing/Tests/ParserTests.cs`
- `Semantics/Tests/ParameterValidatorTests.cs`
- `Semantics/Tests/StructValidatorTests.cs`
- `Semantics/Tests/SemanticErrorTests.cs`
- `Canonicalization/Tests/FormulaCanonicalizerTests.cs` (transport-list
  round-trip)
- `parsing.md` §13.2 and §14.2 matrix
- `Parsing/Coverage.md` and `Semantics/Coverage.md` rows

### Step 4
- `Registry/Tests/TransportRegistryBuilderTests.cs`
- `Bootstrap/Tests/TransportFactoryBuilderTests.cs`
- `Semantics/Tests/StructValidatorTests.cs`
- `Semantics/Tests/SemanticErrorTests.cs`
- `Semantics/Harness/SemanticsTestData.cs`
- `Semantics/Harness/SemanticsAssertions.cs`
- `Canonicalization/Tests/FormulaCanonicalizerTests.cs`
- `Canonicalization/Tests/PrinterInvariantValidatorTests.cs`
- `Canonicalization/Tests/CanonicalizationErrorFactoryTests.cs`
- `Canonicalization/Harness/CanonicalizationTestData.cs`
- `Canonicalization/Harness/CanonicalizationAssertions.cs`
- `Registry/Harness/RegistryTestData.cs`
- `Bootstrap/Harness/BootstrapTestData.cs`
- `Compilation/Tests/TransportCompilerTests.cs`
- `Compilation/Harness/CompilationTestData.cs`
- Affected `Coverage.md` rows

### Step 5
- All Factory suites’ `Coverage.md`
- `nonapproved-behaviour.md`
- Property/round-trip tests

## 9. Test strategy

- Every step updates the tests it breaks and adds tests for new behavior.
- No real transports, sockets, timers, threads, or time.
- No mocking library; hand-written doubles only.
- Tests are order-independent and isolated.
- New suites follow `Tests/RULES.md`; no empty layers.
- Compile-time type assertions replace reflection-based negative tests.
- Exact messages are asserted for every new code.
- Decimal literals are tested for exact representability, not merely
  `decimal.TryParse` success; overflow, underflow, and precision loss are
  separate cases.
- Array semantic and printer tests pin the first-error order: per element in
  index order, shape before value.
- Each step greps for removed type names and old kind literals before handoff
  and updates every match or records why it is intentionally unchanged.
- Harness builders and assertions are updated in the same step as the
  production type they support.

## 10. Out of scope

- TCP, Direct, UDP, RR-UDP, zip, reconnectable, and logger registrations.
- Transport constructor changes.
- Transport test projects.
- `ArrayParam`.
- Nested arrays.
- `StructType` array elements.
- Enum fields.
- `UInt64` scalar kind.
- Boolean fields.
- Merging `ArrayValue` and `TransportListValue` element types.
- Host wiring, UI, and application use cases.
- Legacy `IDescription`/URI retirement.

## 11. Risks

1. **Step 1 is a broad breaking change.** It touches every resolved-tree test.
   It is intentionally one step to avoid a half-migrated model.
2. **Error-phase shift in Step 3.** Invalid transport-list elements move from
   syntax to semantic errors; tests and docs must change deliberately.
3. **Decimal exactness and canonical form.** `decimal.TryParse` accepts
   underflowing values as `0` and rounds long fractions. A dedicated exact
   parser must reject overflow, underflow, and precision loss. Trailing zeros,
   exponent input, negative zero, `1.`, `.5`, decimal min/max, and the
   `1e-28`/`1e-29` boundary must be pinned by tests.
4. **Kind vocabulary migration.** `scalar` remains an AST shape, while resolved
   values use `string`/`integer`/`decimal`. Existing tests that assert kind
   names must be updated in Steps 1/2; hard-coded messages in
   `TransportCompilerTests` are easy to miss.
5. **Array element paths and order.** `values[2]` must be defined in
   `parsing.md` and used consistently in semantic and printer errors. Semantic
   validation is per element in index order; printer validation is arity, then
   per-element kind, then range/printability.
6. **Core vs formula validation.** Core integer/decimal/array DTOs must not
   pre-validate ranges, or `E_REG_INVALID_*` is unreachable through the adapter.
   This differs from the existing `TransportListParameter` precedent and must
   be documented.
7. **No struct arrays.** `SingleType` is closed; `StructType` cannot be
   constructed in this phase.
8. **`SingleType` re-parenting.** Step 2’s `IntegerType`/`DecimalType` must
   remain the same classes in Step 4; do not introduce duplicate
   integer/decimal types when adding `SingleType`. Core and formula each need
   their own hierarchy.
9. **Test migration completeness.** The migration map is the checklist, not a
   suggestion. Harness assertions, unit-level error-factory tests, and
   hard-coded message strings must be updated in the same step.

## 12. Handoff checklist

For each step:

- [ ] Update/create tests first where practical.
- [ ] Implement the production change.
- [ ] Build the solution.
- [ ] Run the full Factory suite.
- [ ] Update `grammar.md` / `parsing.md` / `implementation-plan.md`.
- [ ] Update the owning suite `README.md` / `Coverage.md`.
- [ ] Update `nonapproved-behaviour.md` where needed.
- [ ] Grep for removed type names and old kind literals; update every match or
      record why it is intentionally unchanged.
- [ ] Verify exact decimal overflow/underflow/precision cases where Step 2 or
      later touches decimal parsing.
- [ ] Verify array semantic and printer first-error order where Step 4 touches
      arrays.
- [ ] Verify no transport/application project changed.
- [ ] Verify no forbidden formula-assembly references.
- [ ] Commit with a message naming the step.
