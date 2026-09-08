---
name: csharp-doc-gen
description: Generates or updates XML documentation comments for C# code based on pre-existing knowledge provided to the AI agent. Focuses on clarity, consistency, and adherence to a subset of Microsoft's official conventions.
version: 1.0.0
---

# CSharpDocGen Skill

## Purpose
This skill generates or updates XML documentation comments for C# code based on pre-existing knowledge provided to the AI agent. It focuses on clarity, consistency, and adherence to a subset of Microsoft's official conventions. The skill does not perform research beyond the provided knowledge; it only formats and applies that knowledge according to the rules below.

## Prerequisites
- The AI agent must have valid, pre-existing knowledge about the target type’s behavior, members, parameters, return values, exceptions (if any, though they are not documented), and other relevant details.
- If the knowledge is missing, incomplete, or irrelevant to the current code state, the skill must stop and report that it cannot proceed.

## Procedure
1. **Verify knowledge existence**: Confirm that the AI agent has sufficient information about the target type. If not, stop and report: "Insufficient information about the target type."
2. **Read target code**: Locate and read the target type (class, struct, enum, interface, delegate, record, etc.) and its members.
3. **Relevance check**: Ensure the pre-existing knowledge matches the actual code (signatures, names, types, behavior). If there is a mismatch, stop and report: "Pre-existing knowledge does not match the current code state."
4. **Generate/Update docs**: Using the rules below, write new XML documentation comments or update existing ones.

## Documentation Rules

### Tags to Use
- **`<summary>`**: Always required for all types and members (including public, protected, and optionally internal/private when scope is overridden). Describes purpose and key behavior.
- **`<param>`**: Required for every parameter of methods, constructors, delegates, and record primary constructors.
- **`<typeparam>`**: Required for every generic type parameter of generic types and methods. Focus on the role of the type parameter.
- **`<returns>`**: Required for all non-void methods, properties with a getter, and delegates. Describes the returned value.
- **`<remarks>`**: Optional. Used for additional information, including the "Special Considerations" section (see below). Also allowed for properties, events, and other members when extra context is needed.
- **`<inheritdoc>`**: Use when a member overrides or implements a base/interface member and the documentation would be identical (no additional details needed). When used, no other tags should be present on that member.
- **`<see cref="..."/>`**: Allowed for referencing types or members when it improves clarity. Do not overuse.
- **`<paramref name="..."/>`**: Allowed when referring to a parameter in text, if necessary for clarity.
- **`<see langword="null"/>`**: Use when explicitly stating that a value is `null`. Do not document nullability as a separate concept.

### Prohibited Tags and Practices
- **`<exception>`**: Do not document any exceptions, whether thrown directly or propagated.
- **`<example>`**, **`<code>`**, **`<list>`**, **`<para>`** (except for structuring `<remarks>` as described), and other advanced tags: Avoid unless strictly required for clarity. The skill should rely on plain text and the allowed tags above.
- No non-standard tags.

### Member-Specific Requirements

#### Types (class, struct, interface, enum, delegate, record)
- Always have `<summary>`.
- For records: the `<summary>` describes the type itself, not the whole record with all its members.
- For delegates: treated like a method signature; require `<summary>`, `<param>` for each parameter, and `<returns>`.
- For enums:
  - The enum type itself must have `<summary>`.
  - Enum members: if at least one member has non-trivial behavior or purpose, then all members must have a `<summary>`. If all are trivial, none need summaries. Only `<summary>` is allowed for enum members; no `<remarks>`.

#### Fields (including constants)
- Public and protected fields must have `<summary>`.
- No `<remarks>` for fields. Only `<summary>`.
- If scope includes internal/private fields (see Scope Override), same rule applies.

#### Properties
- Always have `<summary>`.
- May have `<remarks>` if special considerations apply.
- The getter’s return value is described in the property’s `<summary>` or `<remarks>`, not with a separate `<returns>` tag (Microsoft convention does not use `<returns>` on properties).

#### Methods and Constructors
- Always have `<summary>`.
- `<param>` for every parameter.
- `<returns>` if return type is not `void`.
- May have `<remarks>` for special considerations.

#### Events
- Documented like properties: always have `<summary>`, may have `<remarks>` if needed.
- Do not use `<param>` or `<returns>`.

#### Generic Type Parameters
- For generic types and methods, `<typeparam>` for each type parameter. Description focuses on the role of the type parameter (e.g., "The type of elements in the collection").

### Special Considerations Section
- This section is placed inside `<remarks>` as a clearly separated subsection, formatted as a bulleted list using `<list type="bullet">`.
- Omit the section entirely if there are no special considerations.
- Each bullet must start with one of the following standard introductory phrases, written in **bold**, followed by a colon and a space:
  - **Thread safety:**
  - **Performance:**
  - **Side effects:**
  - **Preconditions/Postconditions:**
  - **Other notes:**
- **Order**: Default order is as listed above. However, if a particular aspect is more important or central to the member, it may be moved earlier.
- **Placement**: Each consideration is placed at the most specific level where it applies (e.g., a thread-safety note that applies only to a method goes in that method’s `<remarks>`, not the type’s).
- **Content guidelines**:
  - **Thread safety**: Only include if non-obvious. Examples: a method is thread-safe while its class generally is not, or vice versa; type has non-trivial thread-safety properties (e.g., multiple writers/single reader, no reentrance); thread-safety is a significant part of the type’s logic.
  - **Performance**: Include any technical information that may surprise the user, such as time complexity, memory allocations, caching behavior, or hidden costs. If no such information exists, omit.
  - **Side effects**: Document observable side effects (e.g., writes to a file, modifies a passed-in collection, triggers events). Also document significant internal state changes if they are not obvious from the member name. It must be clear whether the effect is observable externally or internal only.
  - **Preconditions/Postconditions**: Use only if not already fully covered by `<param>` and `<returns>` descriptions. If a precondition/postcondition is part of the contract and not obvious, state it here (e.g., "Preconditions/Postconditions: The object must be initialized before calling this method.").
  - **Other notes**: Any other relevant information not fitting above.

## Formatting Rules
### Optional Parameters Section (Editable)
The following parameters are defined separately to allow future modifications:
- **Indentation**: Match the indentation level of the code block where the comment is inserted.
- **Blank lines**: No blank line between the XML doc comment and the declaration.
- **Maximum line length**: 120 characters. Break lines intelligently: try to break at sentence boundaries, then clause boundaries, then word boundaries. Avoid breaking inside a tag.
- **Line breaks**: Use smart line breaking to keep lines under the limit. Each `///` line should be a complete line of text if possible, but continuation lines are allowed.
- **Tag ordering**: For methods/constructors/delegates: `<summary>`, `<typeparam>` (if any), `<param>` (in parameter order), `<returns>`, `<remarks>` (if any). For properties/events: `<summary>`, `<remarks>` (if any). For types: `<summary>`, `<typeparam>` (if any), `<remarks>` (if any). This order is preferred but may be adjusted if it improves logical flow during updates.

### Standard Comment Style
- Use `///` on each line, placed directly above the declaration.
- No blank line between the comment and the code.
- Tags should be on their own lines, except `<summary>` and `<remarks>` may span multiple lines.
- Indent tags to align with the `///` prefix (i.e., `/// <summary>`).

## Updating Existing Documentation
- **Preserve if correct**: If an existing comment is accurate and complete, leave it unchanged. Do not rephrase or reorder solely for style.
- **Incomplete tag**: If a tag's content is partially correct but missing important information, rewrite the entire tag (do not append to the existing sentence). This ensures consistency and clarity.
- **Wrong information**: Correct any factual errors.
- **Reordering tags**: Allowed if it improves logical flow, but not required if the current order is acceptable.
- **Special considerations update**: If a `<remarks>` section contains a "Special Considerations" list, update it according to the rules above. If no such section exists and special considerations are now needed, add it.

## Scope Override
- **Default scope**: Document only `public` and `protected` members.
- **Override**: The user may request to include `internal` and/or `private` members. If overridden, the same documentation rules apply to those members as to public/protected ones.

## Language
- All documentation text must be written in English.

## Final Note
This skill is designed to be used by an AI agent that already possesses the necessary knowledge about the code. It does not perform code analysis beyond reading the target code to verify knowledge relevance. The output must strictly follow the rules above, ensuring that generated XML documentation is clear, consistent, and useful for developers.