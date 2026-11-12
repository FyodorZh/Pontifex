# Tests — Rules

Normative rules for test suites in this repository.

## 0. Scope

- **RULES-01** These rules apply to every **Test Suit** and **Abstract Test Suit**.
- **RULES-02** A suite that follows a documented conformance model other than the Test
  Suit or Abstract Test Suit model is out of scope until a dedicated suite type is defined
  for it. These rules do not apply to it.
- **RULES-03** Loose test files are allowed. They are not suites and do not require a suite
  directory, `README.md`, or `Coverage.md`.
- **RULES-04** Normative words: **MUST** = required, **SHOULD** = default with a documented
  exception, **MAY** = optional.
- **RULES-05** Rule IDs are anchors only. The descriptive heading is the rule. IDs MUST NOT
  appear in test code. They MAY be used in reviews and in `RULES.md` cross-references.

## 1. Terms

- **TERM-01** **Test Suit** — a suite for one non-abstract type, or for a small tightly
  coupled group of non-abstract types.
- **TERM-02** **Abstract Test Suit** — a suite for an inheritance tree of abstract types
  without gaps.
- **TERM-03** **Layer** — a folder that owns the tests for one suite subject. A layer
  contains `Coverage.md`, optional `Harness/`, its test files, and, for an Abstract Test Suit,
  optional nested layers. In a Test Suit, the test files live under `Tests/`; in an Abstract
  Test Suit, they live directly in the layer directory.
- **TERM-04** **Root layer** — the layer at the suite root. Every suite has exactly one root
  layer.
- **TERM-05** **Inheritance parent** — the abstract type from which a layer’s abstract type
  derives.
- **TERM-06** **Group** — a Test Suit subfolder under the layer’s `Tests/` directory that
  organises tests inside the single layer. A group is not a layer.
- **TERM-07** **Grouping folder** — an Abstract Test Suit subfolder that organises layers.
  A grouping folder is not a layer.
- **TERM-08** **Harness** — optional test-only types used by a layer: stubs, dummies, fakes,
  recording doubles, test implementations of abstract types, and manual drivers.
- **TERM-09** **Coverage** — the per-layer `Coverage.md` record of what the layer proves and
  what remains uncovered.
- **TERM-10** **Fixture** — a test class carrying `[TestFixture]` or an equivalent NUnit
  fixture marker.
- **TERM-11** **Suite root category** — the single NUnit category that identifies the suite.
  It is the value substituted for `{TestSuitName}` in category names.
- **TERM-12** **Harness category** — the NUnit category `{TestSuitName}.Harness`, where
  `{TestSuitName}` is the suite root category.

## 2. Suite type selection

- **TYPE-01** Use a **Test Suit** when the subject is a single non-abstract type or a small
  tightly coupled group of non-abstract types.
- **TYPE-02** Use an **Abstract Test Suit** when the subject is an inheritance tree of
  abstract types.
- **TYPE-03** A suite MUST have exactly one suite subject: one non-abstract type,
  one small tightly coupled group of non-abstract types (TERM-01), or one
  abstract-type inheritance tree (TERM-02).
- **TYPE-04** A Test Suit that starts requiring nested layers MUST be split into multiple
  Test Suits or converted to an Abstract Test Suit.
- **TYPE-05** An Abstract Test Suit MUST NOT contain a layer whose subject is a non-abstract
  type. Concrete-type coverage belongs to a Test Suit or to a harness double used by an
  abstract layer.
- **TYPE-06** The suite type MUST be declared in the suite `README.md`.

## 3. Common suite structure

- **STRUCT-01** Every suite MUST have its own dedicated directory.
- **STRUCT-02** The suite root MUST contain `README.md`.
- **STRUCT-03** The suite root MUST contain the root layer: `Coverage.md`, optional
  `Harness/`, and one or more test files. In a Test Suit the test files MUST be under `Tests/`;
  in an Abstract Test Suit they MUST be directly in the layer directory.
- **STRUCT-04** Only the suite root has `README.md`. Layers and groups MUST NOT contain
  `README.md`.
- **STRUCT-05** Every layer MUST contain `Coverage.md`.
- **STRUCT-06** A layer MUST contain one or more suite test files. Empty layers are not
  allowed. Harness self-tests in `Harness/Tests/` do not satisfy this requirement.
- **STRUCT-07** A test file belongs to exactly one layer.
- **STRUCT-08** A test file inside a suite MUST belong to that suite. Loose test files MUST
  live outside suite directories.
- **STRUCT-09** A suite MUST declare one suite root category. Every fixture in the suite MUST
  carry that category, except a harness self-test fixture, which MUST carry the
  `{TestSuitName}.Harness` category instead.
- **STRUCT-10** Suite test files SHOULD use the suite root namespace. Harness types MAY use
  `<SuiteRootNamespace>.Harness`.
- **STRUCT-11** A layer’s exact directory path MUST be declared in the suite `README.md`.
  Directory nesting does not imply inheritance.
- **STRUCT-12** An Abstract Test Suit grouping folder MUST contain only layers or other
  grouping folders. It MUST NOT contain tests, `Coverage.md`, or `Harness/`.
- **STRUCT-13** A Test Suit group MUST be a subfolder of the layer’s `Tests/` directory and
  MUST contain only test files or nested groups. It MUST NOT contain layers, `Coverage.md`, or
  `Harness/`.
- **STRUCT-14** Every `*Tests.cs` file MUST contain exactly one fixture class named
  `<Subject>Tests`.
- **STRUCT-15** Test names MUST describe the scenario and expected result. A suite MUST use
  one naming convention consistently.

## 4. Test Suit layers

- **TS-01** A Test Suit MUST have exactly one layer, placed at the suite root.
- **TS-02** The root layer MUST cover the suite’s non-abstract type or tightly coupled group.
- **TS-03** A Test Suit layer MUST NOT contain nested layers.
- **TS-04** A Test Suit layer MAY contain groups under its `Tests/` directory for
  organisation. Groups MAY be nested.
- **TS-05** Groups MUST NOT have `Coverage.md`. The root layer’s `Coverage.md` covers every
  test in the layer, including tests in groups.
- **TS-06** Harness belongs to the root layer’s `Harness/`. Groups MUST NOT have their own
  `Harness/`.
- **TS-07** If the subject becomes an inheritance tree, the suite MUST be converted to an
  Abstract Test Suit.
- **TS-08** Every Test Suit test file MUST be placed under the root layer’s `Tests/`
  directory. The root layer MUST NOT contain `*Tests.cs` files directly.
- **TS-09** Test files MAY sit directly in `Tests/` or in groups. A Test Suit MUST still have
  exactly one layer; `Tests/` and its groups are part of that layer.
- **TS-10** `Coverage.md` and `Harness/` MUST remain at the layer root. `Tests/` MUST contain
  only test files and groups.

## 5. Abstract Test Suit layers

- **ATS-01** An Abstract Test Suit MUST contain one layer per abstract type in its selected
  inheritance tree.
- **ATS-02** The root layer MUST be placed at the suite root.
- **ATS-03** Every non-root layer MUST declare exactly one inheritance parent.
- **ATS-04** A layer MUST have exactly one abstract-type subject. A layer MUST NOT represent
  a family of sibling types.
- **ATS-05** A derived layer MUST NOT be added until its parent layer exists.
- **ATS-06** A layer MAY be placed in any subfolder below the suite root. Its exact path MUST
  be declared in the README layer table.
- **ATS-07** Grouping folders MAY organise layers. A grouping folder is not a layer and MUST
  NOT have `Coverage.md` or `Harness/`.
- **ATS-08** If an abstract type has no layer yet, the suite is incomplete. The missing layer
  MUST be listed as planned in the nearest existing ancestor layer’s `Coverage.md`. An empty
  layer MUST NOT be created.
- **ATS-09** A derived layer’s tests MUST cover only the contract delta and invariants that
  the derived type introduces.
- **ATS-10** A derived layer MUST NOT duplicate tests already owned by an ancestor layer.
- **ATS-11** An ancestor layer’s tests MUST NOT be removed or weakened to accommodate a
  derived layer.
- **ATS-12** The README layer table MUST be the authoritative declaration of layer name,
  subject, directory, inheritance parent, and coverage file.
- **ATS-13** The set of layers MUST cover every abstract type in the selected inheritance
  tree, or explicitly list the missing types as planned.

## 6. README rules

- **README-01** The suite `README.md` MUST be placed at the suite root.
- **README-02** `README.md` MUST contain a short rationale for the suite.
- **README-03** `README.md` MUST declare the suite type: Test Suit or Abstract Test Suit.
- **README-04** `README.md` MUST declare the root subject: the root abstract type for an
  Abstract Test Suit, or the concrete type or tightly coupled group for a Test Suit.
- **README-05** `README.md` MUST link the contract references: production type files, XML
  documentation, or a specification document.
- **README-06** `README.md` MUST contain the layer table. For an Abstract Test Suit the table
  MUST include layer name, subject, directory, inheritance parent, and coverage path. For a
  Test Suit the table MUST contain the single root layer and its coverage path.
- **README-07** `README.md` MUST define the layer naming convention.
- **README-08** `README.md` MUST contain the directory layout, including layers, grouping
  folders, groups (for a Test Suit, their `Tests/` subfolders), and harness directories
  (`Harness/` and, when present, `Harness/Tests/`).
- **README-09** `README.md` MUST describe the harness layout, including `Harness/Tests/` when
  harness self-tests exist.
- **README-10** `README.md` MUST link the root `Coverage.md` and explain that coverage is
  recorded per layer.
- **README-11** `README.md` MUST declare the suite root category, every additional filtering
  category used by the suite, and, when harness self-tests exist, the
  `{TestSuitName}.Harness` category.
- **README-12** `README.md` MUST contain the run and filter commands for the suite.
- **README-13** `README.md` MUST describe how to add a layer and how to add tests. For a Test
  Suit it MUST also describe where test files and groups go under `Tests/`.
- **README-14** `README.md` MUST list what is explicitly out of scope.
- **README-15** `README.md` MUST link to `Tests/RULES.md`.
- **README-16** `README.md` MUST NOT contain volatile coverage status. Status belongs in
  `Coverage.md`.
- **README-17** For a Test Suit, `README.md` MUST list the groups and their `Tests/`
  subfolders.
- **README-18** `README.md` MAY contain any additional information relevant to the particular
  suite, such as contract summaries, dependency-specific behaviour, profiling notes, or
  suite-specific conventions. Additional information MUST NOT contradict these rules, replace
  the layer table, or duplicate volatile coverage status. This rule applies to both Test Suit
  and Abstract Test Suit.

## 7. Coverage rules

- **COVERAGE-01** Every layer MUST have `Coverage.md` in the layer directory.
- **COVERAGE-02** `Coverage.md` MUST identify the layer, its subject, its directory, and, for
  a non-root Abstract Test Suit layer, its inheritance parent.
- **COVERAGE-03** `Coverage.md` MUST link the contract reference used by the layer.
- **COVERAGE-04** `Coverage.md` MUST contain a covered-tests table with: test name, contract
  clause or behaviour, short description of what the test proves, and non-extractable notes.
- **COVERAGE-05** The test description MUST state intent and contract meaning. It MUST NOT
  duplicate implementation steps that are obvious from the test code.
- **COVERAGE-06** Non-extractable notes MUST record information that is expensive or
  impossible to infer from test code, such as ownership rules, threading or determinism
  assumptions, dependency behaviour, harness assumptions, and known limitations.
- **COVERAGE-07** `Coverage.md` MUST contain a planned section for known missing tests or
  missing derived layers and the layer that owns each item.
- **COVERAGE-08** `Coverage.md` MUST contain a deferred-by-design section with the reason for
  every known gap that is intentionally not covered.
- **COVERAGE-09** A layer’s `Coverage.md` MUST NOT duplicate coverage already recorded by an
  ancestor layer.
- **COVERAGE-10** A Test Suit root `Coverage.md` MUST include tests that live in groups.
- **COVERAGE-11** `Coverage.md` MUST be updated in the same change that adds, removes, moves,
  or renames tests in that layer.
- **COVERAGE-12** `Coverage.md` records current coverage, not a changelog or a history of
  decisions.
- **COVERAGE-13** Missing layers MUST be recorded in the nearest existing ancestor layer’s
  `Coverage.md`; they MUST NOT be represented by empty layer folders.

## 8. Harness rules

- **HARNESS-01** A layer MAY contain a `Harness/` directory.
- **HARNESS-02** Harness code MUST be test-only. It MUST NOT contain production code or
  production behaviour.
- **HARNESS-03** Harness MAY contain stubs, dummies, fakes, recording doubles, scripted
  doubles, manual drivers, test buffers, and test implementations of abstract types.
- **HARNESS-04** A layer MAY reference harness from any ancestor layer.
- **HARNESS-05** Sibling layers MUST NOT reference each other’s harness.
- **HARNESS-06** When sibling layers need shared harness, the shared logic MUST move to the
  nearest common ancestor layer’s `Harness/`.
- **HARNESS-07** Grouping folders and Test Suit groups MUST NOT contain `Harness/`.
- **HARNESS-08** Harness self-tests MAY live under `Harness/Tests/` in a Test Suit or an
  Abstract Test Suit. They are optional; a harness does not require self-tests. They belong to
  the owning layer and MUST be listed in that layer’s `Coverage.md`. `Harness/Tests/` is not
  the suite’s `Tests/` directory and does not create a group. Harness self-tests MUST NOT be
  used to satisfy the layer’s minimum test requirement.
- **HARNESS-09** The repository MUST NOT use a mocking library. Test doubles are
  hand-written.
- **HARNESS-10** Harness types MUST expose only the surface required by the tests. They MUST
  NOT expose private production state.
- **HARNESS-11** Every harness self-test fixture MUST carry the `{TestSuitName}.Harness`
  category instead of the suite root category. The `{TestSuitName}.Harness` category MUST NOT
  be applied to non-harness tests.

## 9. Test construction rules

- **TEST-01** Tests MUST exercise the subject through its public and protected contract.
  Tests MUST NOT inspect private state and MUST NOT use reflection.
- **TEST-02** Tests MUST assert observable results only: return values, callbacks and
  reasons, captured outputs, ordering, counts, and contract-visible resource state.
- **TEST-03** The default suite run MUST be deterministic. Tests MUST NOT use real time,
  sleeping, uncontrolled background tasks, timers, real sockets, or real networks unless the
  contract explicitly requires them.
- **TEST-04** A nondeterministic dependency MUST be replaced through an existing or
  dedicated test seam: serial queue, clock, periodic driver, socket accessor, pool, logger,
  or equivalent.
- **TEST-05** Asynchronous hooks MUST be completed explicitly by the harness or test. Tests
  MUST NOT depend on a scheduling race.
- **TEST-06** Tests MUST be order-independent and isolated. Shared mutable state MUST be
  reset before or after each test.
- **TEST-07** Tests MUST follow the subject’s ownership rules. Rented buffers MUST be
  released exactly once. Captured buffers MUST be cloned when inspection must outlive the
  contract’s release point.
- **TEST-08** A layer’s tests MUST cover its documented failure paths and boundary
  conditions, including lifecycle guards, invalid input, concurrency boundaries, and
  exception isolation where applicable.
- **TEST-09** A derived layer’s tests MUST NOT repeat an ancestor layer’s tests.
- **TEST-10** Real I/O, integration, allocation, load, and other non-default facets MUST be
  placed in a separate group or layer and MUST use a declared category.
- **TEST-11** Test method names MUST use one consistent convention within the suite and MUST
  describe the scenario and expected result.
- **TEST-12** A fixture MUST carry the suite root category. A harness self-test fixture MUST
  carry the `{TestSuitName}.Harness` category instead.

## 10. Evolution rules

- **EVOL-01** A new layer requires its parent layer to exist first.
- **EVOL-02** A new layer MUST include its directory, `Coverage.md`, optional `Harness/`, one
  or more suite test files (for a Test Suit, under `Tests/`; for an Abstract Test Suit,
  directly in the layer directory), and its README layer-table row.
- **EVOL-03** Adding or changing a test MUST update the owning layer’s `Coverage.md` in the
  same change.
- **EVOL-04** Moving a layer MUST update the README layer table, the coverage path, and every
  documentation link that names the old path.
- **EVOL-05** Renaming a layer or subject MUST update the README, `Coverage.md`, namespace
  declarations where applicable, and every cross-reference.
- **EVOL-06** When a Test Suit needs nested layers, split it into multiple Test Suits or
  convert it to an Abstract Test Suit.
- **EVOL-07** When a Test Suit subject becomes an inheritance tree, convert the suite to an
  Abstract Test Suit and add layers parent-first.
- **EVOL-08** Removing a layer MUST update the README layer table and record the resulting
  gap as planned in the nearest existing ancestor’s `Coverage.md`.
- **EVOL-09** A contract change MUST be recorded in the contract reference before or in the
  same change as the affected tests.
- **EVOL-10** A test MUST NOT be deleted or weakened merely because a derived layer or a
  concrete subject does not satisfy it.
- **EVOL-11** Duplicate tests across ancestor and derived layers MUST be removed from the
  derived layer.
- **EVOL-12** Shared harness logic MUST NOT be copied into sibling layers. Move it to the
  nearest common ancestor layer.
- **EVOL-13** `README.md` and `Coverage.md` MUST describe the current structure and current
  coverage after every structural change.

## 11. Loose test files

- **LOOSE-01** Loose test files are allowed and do not require suite documentation.
- **LOOSE-02** A loose test file MUST NOT be placed inside a suite directory unless it
  belongs to that suite’s layer.
- **LOOSE-03** A loose test file MUST still follow the applicable test construction rules in
  §9.
- **LOOSE-04** When loose tests become a cohesive, reusable, or contract-driven group, they
  MUST be promoted to a Test Suit or Abstract Test Suit.
- **LOOSE-05** A promoted loose test file MUST be moved into the appropriate layer (for a Test
  Suit, under `Tests/` or a group) and recorded in that layer’s `Coverage.md`.

## 12. Compliance checklist

- **CHECK-01** The suite has one suite subject and one root layer.
- **CHECK-02** The suite root has `README.md`; every layer has `Coverage.md`.
- **CHECK-03** The README layer table declares every layer path and inheritance parent.
- **CHECK-04** No layer is empty; missing layers are recorded as planned in an ancestor’s
  coverage.
- **CHECK-05** No derived layer duplicates an ancestor layer’s tests.
- **CHECK-06** No sibling layer references another sibling’s harness.
- **CHECK-07** Every non-harness fixture carries the suite root category; every harness
  self-test fixture carries `{TestSuitName}.Harness` instead.
- **CHECK-08** The default suite run is deterministic; non-default facets are categorised.
- **CHECK-09** Buffer ownership and release are asserted where the contract defines them.
- **CHECK-10** `Coverage.md` records contract meaning and non-extractable notes, not a copy
  of test code.
- **CHECK-11** `README.md` and `Coverage.md` match the current directory structure and test
  set, including `Tests/` groups and harness self-tests.
- **CHECK-12** Test Suit test files live under `Tests/`; groups are subfolders of `Tests/`;
  `Coverage.md` and `Harness/` stay at the layer root. Harness self-tests, when present, live
  under `Harness/Tests/` and carry `{TestSuitName}.Harness`.
