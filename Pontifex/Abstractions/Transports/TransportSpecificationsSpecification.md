# Transport Specifications Specification

| Field | Value |
| --- | --- |
| **Component** | `Pontifex` |
| **Applies to** | every transport `Specification.md` |

## 1. What a specification is for

A transport specification exists for exactly three purposes:

1. to state, normatively and completely, what one transport abstraction is;
2. to give the transport implementer an executable checklist;
3. to give the consumer of the transport, application logic, an executable
   checklist.

Everything in the document serves one of these purposes. A passage that serves
none of them is not part of the specification, however useful it is elsewhere.

This document governs the scope and form of specifications. It states no
transport behavior.

### 1.1 The description

The description is the complete, testable contract of the abstraction. It
covers:

- the public API;
- the model: what the client, server, handlers, and endpoints or handles are,
  and what unit of work exists;
- how that unit of work is established, routed, identified, and finished;
- lifecycle and terminal transitions;
- message framing and size limits;
- the meaning of every send result;
- what is guaranteed and what is not;
- concurrency and buffer ownership;
- the failure path;
- what is left to the implementation;
- the trust boundary.

The description is exhaustive. A conforming implementation is able to derive
every observable behavior from it, and an application is able to derive every
obligation from it. A behavior that application code can observe and that the
specification neither states nor declares implementation-defined is a defect in
the specification.

### 1.2 The checklists

The implementer checklist restates every normative requirement of the
description as a verification step. The application checklist restates every
obligation the abstraction places on application code.

A checklist item is written so that a reader can mark it without consulting the
description. It names the subject, the action, and the condition. It contains no
rationale and no cross-reference.

### 1.3 What a specification is not

A specification **MUST NOT** contain:

- rationale, motivation, design discussion, or examples beyond the minimum
  needed to fix a signature;
- history, changelog, revision table, version, status, or last-updated date;
- `TODO`, open questions, review notes, or future work;
- comparison with another transport;
- prose that repeats a rule already stated.

A type specification **MUST NOT** additionally contain:

- a glossary shared with another transport;
- carrier, wire format, configuration keys, deployment, or wrapper and
  converter design;
- known limitations of one implementation.

An implementation specification **MUST NOT** contain a section absent from its
structure in section 3.

### 1.4 Document kinds

| Kind | Subject | Structure |
| --- | --- | --- |
| **Type specification** | one abstract transport type | section 2 |
| **Shared specification** | one abstraction shared by two or more types | section 2, with the checklist exemption |
| **Implementation specification** | one implementation of one abstract type | section 3 |

A document describes exactly one subject. A shared abstraction **MUST NOT** be
described twice. An implementation **MUST NOT** be described in the type
specification it implements.

A type specification that has a shared specification **MUST** reference it by
relative link and **MUST NOT** restate it.

## 2. Type specification structure

Every type specification, and every shared specification, **MUST** contain the
following sections, in this order, with these numbers.

| # | Section | Required content |
| --- | --- | --- |
| 1 | Scope | One paragraph naming the abstraction and what it does not provide. A subsection `1.1 Applicability` listing every omitted topic of §4.2 with the property that makes it inapplicable. A subsection `1.2 References` listing every specification this document depends on. |
| 2 | Normative language | The paragraph below this table, verbatim, and nothing else. |
| 3 | Terms | Every term used in a normative sentence, defined locally or in the referenced shared specification. |
| 4 | Public contract | `csharp` declarations introduced by this abstraction, inherited members in prose, which types applications implement and which the transport supplies, and what is deliberately not exposed. |
| 5 | Model | The model class and the unit of work. Whether admission and peer lifecycle callbacks exist. A subsection `5.1 Responsibility split` with the table of §4.1. |
| 6 | Establishment and requests | Only the applicable topics of §4.2, in the listed order, numbered consecutively. |
| 7 | Lifecycle and failure | State diagram, state list, `Init`, `Start`, and `Stop` rules, `IsValid` and `IsStarted` rules, the callback matrix of §4.3, terminal ordering, and the effects of unrecoverable failure. |
| 8 | Message model and limits | `MessageMaxByteSize` definition with every excluded component named, empty-message validity, immutability, malformed and oversized input handling, boundary and content preservation. |
| 9 | Send results | Precedence list, one row for every `SendResult` value, every value the transport never returns, and the non-fatal rule. |
| 10 | Delivery guarantees | Guarantee level and scope, permitted outcomes, the meaning of `Ok`, duplicate and ordering scope, and the observations the application must never make. |
| 11 | Concurrency and buffer ownership | Per-handler serialization, non-reentrancy, cross-handler concurrency, factory serialization, ownership of sent and received buffers per result, throwing-handler rules, promptness. |
| 12 | Implementation-defined choices | A table of three columns: area, what is left open, and the invariant that remains binding. The rule that an open choice weakens nothing. |
| 13 | Security boundary | The absent protections, the trust level of every input, and the protection the application must add. |
| 14 | Conformance controls | When none exist, the single sentence `No conformance controls are defined.` Otherwise each control, checkpoint, and test mode with its hit condition and preconditions. |
| 15 | Transport-implementer checklist | Checklist covering every normative requirement of sections 4 to 14. |
| 16 | Application-author checklist | Checklist covering every obligation on application code. |

> The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHOULD**, **SHOULD
> NOT**, and **MAY** in this document are to be interpreted as described in
> [RFC 2119](https://datatracker.ietf.org/doc/html/rfc2119).

Section numbers **MUST** match the section table above. Subsections are numbered
`<section>.<n>` starting at 1. A section **MUST NOT** be omitted.

A shared specification uses this structure with one exception: each of its
sections 15 and 16 **MUST** contain exactly the sentence
`Checklists are defined by each type specification.` A shared specification
**MUST NOT** repeat a checklist item of a type specification.

## 3. Implementation specification structure

An implementation specification describes one carrier, wrapper, or converter.
It **MUST** reference the type specification it implements by relative link, and
it **MUST** contain the following sections, in this order.

| # | Section | Required content |
| --- | --- | --- |
| 1 | Scope | The abstract type implemented, the role as carrier, wrapper, or converter, what this implementation adds, and what it does not change. A subsection `1.1 References` listing the type specification and every other specification this document depends on. |
| 2 | Normative language | The RFC 2119 paragraph of section 2 of this document, verbatim. |
| 3 | Contract | The public type, how it is constructed, registered, or bound, and the abstraction it conforms to. |
| 4 | Scheme and negotiation | Canonical scheme name, negotiation name and version, the envelope in a `text` block, and the validation order. |
| 5 | Parameters | A five-column table: parameter, type and unit, required, default, and meaning. Every parameter marked as implementation configuration. |
| 6 | Lifecycle and teardown | Resources owned, start and stop hooks, linger and reconnect behavior, and release on teardown. |
| 7 | Failure, deviations, and protection | Failure mapping; every bounded deviation from the abstract contract; any protection added beyond the abstraction. |
| 8 | Implementation-defined choices | The choice this implementation makes for every open area that the abstract type declares. |
| 9 | Implementer checklist | Verification steps for this implementation. |

Every deviation in section 7 **MUST** name the requirement it deviates from,
state its cause and bounded impact, and state the tolerance it requires from
handlers. An implementation **MUST NOT** leave a deviation undeclared.

An implementation specification **MUST NOT** restate the abstract contract, and
**MUST NOT** redefine the guarantees of the type specification.

## 4. Content rules

### 4.1 Responsibility split

The `5.1 Responsibility split` table **MUST** use the columns
`| Topic | Guaranteed by <Name> | Application responsibility | Implementation-defined |`
and contain at least the rows `Delivery`, `Sending`, `Handlers`, `Routing`,
`Failure`, `Security`, and `Lifecycle`. Every cell **MUST** be a complete
phrase.

### 4.2 Establishment and request topics

Section 6 **MUST** contain only the applicable topics of this table, in this
order.

| Topic | Required content |
| --- | --- |
| Client association | The immutable client destination or route, its lifetime, and the filtering the client applies. |
| Server association and routing | The unit the server binds to, the factory signature, the equality and atomicity rule, the rebinding rule, and the null and throw outcomes. |
| Establishment and negotiation | The envelope in a `text` block, the parameter types, the validation order, the agreement rule, and the first handler callback. |
| Rejection and timeout | Every rejection reason, its client-observable mapping, deadline defaults with units, and the linger rule. |
| Admission and handler binding | Factory timing and serialization, the fresh-handler rule, the null and exception outcome, the single-stop rule, and the gate on regular messages. |
| Request acceptance | A decision table `first successful call → state → client-visible effect`, state persistence, and throwing-callback behavior. |
| Identifiers and correlation | Width, domain, starting value, monotonicity, wraparound, publication point, lifetime, and staleness. |
| Pending window and TTLs | Membership, removal causes, the single-winner rule, the expiry effect, and defaults with units. |
| Responses and completion | Multiplicity and order, a `trigger → terminal type` table, suppression of later causes, and the guarantee when delivery fails. |
| Cancellation | The exact effects, the callback-context rule, the server notification policy, and post-cancel handle behavior. |

Every state, outcome, and race introduced in section 6 **MUST** appear in a
table or an enumerated list. Every race **MUST** name the winner. Every
negotiated parameter **MUST** state its name, default, unit, configurability,
and whether it is immutable after establishment.

Section 6 **MUST NOT** describe wire encoding.

### 4.3 Lifecycle

Section 7 **MUST** contain a `mermaid` `stateDiagram-v2` over
`Constructed`, `Initialized` when `Init` exists, `Running`, `Stopping`, `Stopped`,
and `Invalid`, with every transition labeled by its trigger. It **MUST** mark
the terminal states.

The callback matrix **MUST** use the columns `| Situation | Handler callbacks |`
and cover at least:

- `Transport Start returns false`;
- `Start returns true and association fails before the first handler callback`;
- `Association established`;
- `Handler created but association start fails`;
- `Association rejected or timed out`.

The matrix **MUST** name every callback the handler receives and every callback
it does not receive. Section 7 **MUST** state that a failed `Start` invokes no
handler callback and that a handler terminal notification is scheduled before
`onStopped` begins.

### 4.4 Checklists

- Every requirement stated with **MUST** or **MUST NOT** in sections 4 to 14
  **MUST** be traceable to at least one implementer item.
- Every obligation on application code **MUST** be traceable to at least one
  application item.
- Each item **MUST** state one obligation in the imperative mood.
- Items **MUST** be ordered by lifecycle: initialize, send, receive, handle,
  stop.
- The type implementer checklist **MUST** contain 10 to 30 items. The
  application checklist **MUST** contain 6 to 20 items. The implementation
  checklist **MUST** contain 5 to 20 items.

## 5. Form

- The file **MUST** be UTF-8 Markdown named `Specification.md`, with LF line
  endings and a trailing newline.
- The document **MUST** have one level-1 title. A type specification uses
  `# <Name> Transport Specification`; a shared specification **MAY** use
  `# <Name> Common Transport Specification`; an implementation specification
  uses `# <Name> Specification`.
- Sections **MUST** use `## <N>. <Title>`. Subsections **MUST** use
  `### <N>.<M> <Title>`. Depth **MUST NOT** exceed four.
- Headings **MUST** use sentence case, except for identifiers.
- Section numbers **MUST** be stable; a new section is appended with the next
  free number.
- RFC 2119 keywords **MUST** be upper case and bold. A normative sentence
  **MUST** contain one keyword.
- A normative sentence **MUST** name its subject, use present tense, and use an
  exact quantifier: *exactly once*, *at most once*, *never*, *every*, *no*.
- Lower-case *must*, *shall*, *will*, *should*, *usually*, *typically*,
  *normally*, and *as needed* **MUST NOT** appear in normative text.
- Every identifier, type, member, enum value, path, and literal **MUST** be
  inline code. The first occurrence of a defined term **MUST** be bold.
- A table **MUST** have a header row, a delimiter row, at least one body row,
  and no empty cell; an empty concept is written `none`.
- A fenced block **MUST** declare `csharp`, `text`, `mermaid`, or `bash`.
- A `csharp` block **MUST** be declaration-only, compile against the public
  API, and contain no bodies and no elisions.
- Diagrams **MUST** be valid Mermaid. Prose is normative; a diagram that
  contradicts it is corrected in the same change.
- A cross-reference **MUST** cite the section number and title. A reference to
  another file **MUST** be a relative link.
- A References subsection **MUST** list every specification this document
  depends on, as a relative link, with one clause naming what is taken from it.
- A line **MUST NOT** exceed 100 characters, except in a table row.
- A specification **MUST NOT** contain an introduction, a summary, or a
  conclusion.

## 6. Terms

Each specification defines its terms for its own subject.

A word **MUST NOT** be assumed to carry the same meaning across transport
specifications. The same word may name different concepts in different
transports, and a term defined by one specification **MUST NOT** be imported
into another without a local definition.

A definition **MUST NOT** introduce an obligation. An obligation stated in a
definition **MUST** also appear in the body with a normative keyword.

## 7. Compliance check

A type specification is conformant when all of the following hold:

1. the structure of section 2 is present, in order, with the required numbers;
2. `1.1 Applicability` accounts for every omitted topic of §4.2;
3. `1.2 References` lists every specification this document depends on;
4. every term used normatively is defined in section 3;
5. every `csharp` block matches the public API and compiles;
6. every table is complete and every diagram is valid;
7. every normative requirement is covered by the implementer checklist;
8. every application obligation is covered by the application checklist;
9. no content prohibited by §1.3 is present;
10. no section restates a requirement of a shared specification.

An implementation specification is conformant when all of the following hold:

1. the structure of section 3 is present, in order;
2. `1.1 References` names the type specification and every other dependency;
3. it references its type specification and restates none of it;
4. every parameter has a type, unit, required flag, default, and meaning;
5. every deviation names the requirement, cause, impact, and handler tolerance;
6. every open area of the type specification has a stated choice;
7. its checklist covers every normative statement it makes.
