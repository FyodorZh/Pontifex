---
name: plan-executor
description: Manual-only skill invoked after planning is complete. Turns an agreed task into a single implementation plan file, hardens it through repeated fresh-context audits, reports every finding, and only after explicit user permission executes the plan stage by stage with independent validation and one git commit per stage. Not automatic; never invoke without an explicit user request.
---

# Plan Executor

Turn an already-agreed task into a single implementation plan, harden that plan against an adversarial audit loop, and — only after explicit permission — execute it stage by stage with independent validation.

This skill starts **after** planning and clarification are finished. Its job is to convert shared understanding into a durable artifact, expose every hole in that artifact before code is written, and then drive implementation without improvisation.

## Roles

The agent running this skill is the **orchestrator**. It owns the plan file, the audit loop, the finding ledger, the git state, and all decisions about which findings are real.

Separate **fresh sub-agents** (new context, no inherited conversation) perform three narrowly scoped jobs:

- **Auditors** — three independent lenses that read the plan from disk and attack it.
- **Implementator** — reads the plan from disk and implements exactly one stage.
- **Validator** — reads the plan stage and checks the resulting work.

Auditors are instantiated as three fresh sub-agents per iteration; see Phase 2.

Sub-agents never receive the orchestrator's reasoning or chat history. Pass them only what the prompt templates below specify: file paths, stage numbers, and the exact report format. This isolation is deliberate — it prevents auditors from inheriting the author's assumptions and the implementator from inheriting the auditors' doubts.

## Phase 0 — Understanding gate

Before writing anything, answer this question explicitly in chat:

> Do I have a complete and comprehensive understanding of the task — its goal, scope boundaries, constraints, required behaviour, acceptance criteria, and the environment it must run in?

If the answer is anything other than an unqualified yes, **stop the skill immediately**. Do not write a plan, do not spawn sub-agents, do not fill gaps with assumptions. Ask the user targeted questions:

- At most 3 questions per batch, most important first.
- Group by area: scope, behaviour, constraints, verification, integration.
- Offer concrete variants when the user is unsure.
- Do not proceed until every open question is answered.

A plan written on top of an unresolved question is worse than no plan: the audit loop will either miss the gap or burn iterations guessing at it.

## Phase 1 — Plan authoring

Write one file, `plan.md` in the repository root by default. The user may name a different path when invoking the skill.

If the target file already exists, **stop and ask** whether to use a different path. Never overwrite, never auto-rename, never merge into an existing plan.

The plan is the only artifact this phase produces. Do not create a separate design document, checklist, or notes file.

### Required structure

1. **Title** — short, imperative.
2. **Goal** — one paragraph: what exists after the plan is complete that does not exist now.
3. **Context** — the minimum background an implementator with no chat history needs.
4. **Constraints** — technical, stylistic, and process rules that must hold (project conventions, compatibility targets, forbidden changes).
5. **Non-goals** — work explicitly out of scope, so no stage drifts into it.
6. **Assumptions** — every fact taken as given, each tied to its evidence (file, document, or user statement).
7. **Prerequisites** — tooling, credentials, services, or prior state needed before stage 1.
8. **Stages** — numbered, ordered, each following the skeleton below.
9. **Global definition of done** — the verifiable end state of the entire plan.

### Stage skeleton

Every stage must contain all of the following. A stage missing any of them is not ready for the audit loop.

- **Objective** — what this stage alone achieves.
- **Inputs** — what must already exist when the stage starts.
- **Actions** — a numbered, ordered list of concrete changes: exact files, types, members, commands, and configuration. No "update the module appropriately"; name what changes and how.
- **Verification** — the exact procedure proving the stage worked. Prefer runnable commands with expected results (build, test filter, linter, script). Where verification is manual, state precisely what to inspect and what correct output looks like.
- **Definition of done** — the observable condition that ends the stage.
- **Risks / notes** — only where a step is non-obvious or easy to get wrong.

### Sizing rules

- A stage must be independently implementable and independently verifiable.
- A stage must not depend on decisions deferred to a later stage.
- A stage must be small enough that its diff can be reviewed in one pass.
- If a stage cannot be verified without the next stage, merge them or re-cut the boundary.
- Prefer more, smaller stages over fewer, sprawling ones.

## Phase 2 — Adversarial refinement loop

Repeat until the stopping condition is met. Each iteration runs three **fresh** auditor sub-agents with different lenses and no memory of previous iterations, of the orchestrator's reasoning, or of each other's work.

### 2.1 Commit the plan before each iteration

Immediately before spawning the auditors, commit the current version of the plan file:

```text
git add <plan path>
git commit -m "Plan revision <N>"
```

where `<N>` is the iteration number about to run. Commit the plan file only — never stage or commit unrelated working-tree changes.

If the plan file has no changes relative to the previous commit (for example, the previous audit produced only minor findings that were left in place), skip the commit. Never create an empty commit.

This keeps every audited plan version recoverable and guarantees that a valid fix cannot be lost between iterations.

### 2.2 Spawn the auditors

Run **three** auditor sub-agents per iteration, concurrently and independently. One round of three auditors counts as one iteration.

Each auditor receives the same plan path and iteration number but a different lens:

1. **Logic and consistency** — contradictions between sections, stages, constraints, and non-goals; scope drift; unstated requirements; assumptions presented as facts without evidence; internal inconsistencies.
2. **Implementation sufficiency** — missing or underspecified actions that force research, design, or guess-work; wrong paths, type/member names, APIs, commands, or environment assumptions. This lens inspects the repository to confirm references.
3. **Verifiability and sequencing** — verification that cannot prove the stage's objective; missing expected results; ordering or dependency errors between stages; stages that are not independently implementable or verifiable; gaps in prerequisites and the definition of done.

Use the auditor prompt template below, substituting the lens name and mandate. The auditor reads the plan from disk itself; never paste the plan into the prompt, and never let one auditor see another's report.

Collect all three reports before triage. Deduplicate by location + problem; when lenses overlap, keep the strongest evidence and merge the fixes. The round is ready only under the stopping condition in 2.5.

### 2.3 Triage the reports

The orchestrator evaluates **every** deduplicated finding from the three reports. An auditor can misread the plan, import outside assumptions, or invent a problem. Lens disagreements are resolved against the plan text and the repository, not by majority. For each finding:

1. Locate it in the plan (or the referenced code).
2. Decide: **valid** — the problem genuinely exists and would block, mislead, or force guess-work during implementation; or **invalid** — the auditor misread, the concern is already addressed elsewhere in the plan, or the point is stylistic preference.
3. Valid findings are fixed in the plan file immediately, at the smallest scope that resolves the problem. If a fix ripples across stages, update every affected stage in the same pass.
4. Invalid findings are recorded as rejected with a one-line reason. Do not fix them, do not argue with the auditor, and do not let them trigger churn.

Never "fix" a finding by adding vague reassurance. If the fix is to clarify, clarify concretely; if the fix is to add a missing step, add the step and its verification.

### 2.4 Severity

Findings are classified **major** or **minor**.

**Major** — anything that could make implementation fail, stall, or silently go wrong:

- Contradictions between stages, requirements, or constraints.
- Missing or underspecified actions that force the implementator to design or research.
- Wrong file paths, type names, APIs, commands, or environment assumptions.
- A stage with no reliable verification, or verification that cannot prove the stage's objective.
- Ordering or dependency errors between stages.
- Scope that conflicts with the stated non-goals.
- Any place where a reasonable implementator would have to guess.

**Minor** — wording, formatting, redundancy, or clarity that does not change what gets built.

### 2.5 Stopping condition

Stop when both are true:

- Zero unresolved major findings remain after triage across all three lens reports. A major finding is resolved only when it is fixed in the plan or rejected as invalid with a recorded reason.
- All three reports end with `Plan is implementation ready`, or a report that still ends `Plan is not implementation ready` contains only findings the orchestrator rejected as invalid — with the rejection reasons recorded in the ledger.

Or stop when the iteration counter reaches **N = 5** (the user may set a different N when invoking the skill). Minor findings do not extend the loop; record them in the ledger and report them at the end.

If the cap is reached with major findings outstanding, the plan is **not ready**. Report and wait for the user. Do not start implementation.

Whenever the loop ends, the plan file on disk must be committed. If the final triage changed it and no further iteration will run, commit it as `Plan revision <N+1> (final)` so the exact audited state is recoverable.

### 2.6 Auditor prompt template

```text
You are one of three independent plan auditors. You have no prior context and
no knowledge of the other auditors or their findings.

Your lens: <LENS_NAME>
<LENS_MANDATE>

1. Read the plan at: <PLAN_PATH> (iteration <ITERATION>).
2. Audit the whole document through your lens, as if you were the person who
   must implement it with no further clarification and no access to the author.
3. Build a coverage matrix with one row for the front matter (title, goal,
   context, constraints, non-goals, assumptions, prerequisites, global
   definition of done) and one row for every stage. Report the matrix in full:

   | Item | Verdict | Evidence / justification | After second sweep |
   |------|---------|--------------------------|--------------------|
   | Front matter | `ok` or `#1` | What you checked | `confirmed ok` or `#1` |
   | Stage 1 | ... | ... | ... |

   - `Verdict` is `ok` or the number of a finding.
   - An `ok` requires a one-line justification naming what you checked. A bare
     `ok` is not acceptable.
   - Every stage must appear as its own row. Do not group or omit stages.
4. Do not stop when you find a blocker. Report every finding you can support,
   even if one already makes the plan unimplementable. Do not omit a finding
   because another is worse, and do not summarise or praise the plan.
5. Second sweep — mandatory, even if the first pass already found majors.
   Re-read the plan end to end. For every item marked `ok`, ask: "what would the
   implementator most likely have to guess, research, or improvise here?" If a
   concrete, supportable risk exists, add a finding and set the verdict to its
   number. Otherwise write `confirmed ok` in the last column. Do not conclude
   before every `ok` row has been re-examined.
6. For every finding report: location, severity (major or minor), the problem in
   one or two sentences, the concrete fix, and the plan text or repository
   evidence that supports it. Report only findings within your lens; the other
   auditors cover the rest. Omit speculative concerns — every finding must be
   traceable to evidence.
7. Only after the matrix is complete and the second sweep is recorded, end with
   exactly one line:
   Plan is implementation ready
   or
   Plan is not implementation ready
```

## Phase 3 — Cumulative report and permission gate

After the loop terminates, print a cumulative report:

- Number of iterations (audit rounds) run and the cap.
- A table of every finding ever raised: iteration, lens, location, severity, status (`fixed` / `rejected as invalid` / `minor, accepted`), and the resolution.
- Any minor findings left in place.
- The final resolution, stated plainly: **Plan is implementation ready** or **Plan is not implementation ready**.

If the resolution is not ready, implementation is not permitted. Report and wait for the user.

Then stop and wait.

Implementation may begin only on an explicit, unambiguous instruction to implement, such as "start implementation", "execute the plan", or "go ahead and implement it". A bare "continue", "ok", or "proceed" is not permission — ask.

## Phase 4 — Execution

### 4.1 Preflight

1. Run `git status --porcelain`. It must be empty — every plan revision was committed in Phase 2.
2. If the plan file or any other file is modified, staged, or untracked, **stop** and report the list. Do not stash, clean, commit, or work around the user's changes. A dirty tree here means the plan on disk is not the version that passed the audit.
3. Confirm the latest plan commit exists with `git log -1 --stat`.

The plan is now frozen. Do not edit it during execution.

### 4.2 Stage loop

Iterate the stages strictly in plan order.

**Step 1 — Implement.** Spawn a fresh implementator using the template below with the plan path and stage number. It implements only that stage, runs that stage's verification, and reports changes and results. It must not touch other stages or the plan file.

**Step 2 — Validate.** Spawn a fresh validator using the template below. It reads the stage from the plan, inspects the working tree diff, reruns the stage verification, and reports either readiness or a numbered problem list.

**Step 3 — Triage.** Apply the same validity test used in Phase 2 to every validator finding: confirm it against the plan and the code before acting. Pass only valid problems to the implementator.

**Step 4 — Fix loop.** If valid problems exist, spawn a fresh implementator to fix exactly those problems, then spawn a fresh validator again. Maximum **3 implement/validate rounds per stage**. On exhaustion: stop, do not commit the stage, report the unresolved problems and the current diff state, and wait for the user.

**Step 5 — Commit.** On a passing validation, commit the stage's changes with the message `<short description> (stage N)`. The description comes from the stage objective, in imperative mood, short enough for one git log line. Commit only the files that belong to this stage.

Never commit a stage that has not passed validation. Never commit multiple stages together. Never commit a failed stage's partial work.

### 4.3 Implementator prompt template

```text
You are implementing one stage of a pre-approved plan. You have no prior
context and no authority to change the plan.

1. Read the plan at: <PLAN_PATH>.
2. Implement stage <STAGE_NUMBER> only: follow its actions in order, touch only
   the files it names (or strictly necessary files it implies), and do not
   implement later stages or unrelated improvements.
3. Run the stage's verification procedure exactly as written and capture the
   output.
4. Do not modify the plan file. If the plan is wrong, ambiguous, or
   unimplementable, stop and report the problem instead of improvising.
5. Report:
   - files changed,
   - what was done per action,
   - verification commands and their output,
   - anything not completed and why.
```

### 4.4 Validator prompt template

```text
You are an independent validator. You have no prior context and did not write
this code.

1. Read stage <STAGE_NUMBER> of the plan at: <PLAN_PATH>.
2. Inspect the working tree diff for the stage's described changes.
3. Rerun the stage's verification procedure yourself and capture the output.
4. Check that the implementation:
   - does what the stage's objective and actions require, and nothing outside
     the stage's scope;
   - matches the plan's constraints, non-goals, and assumptions;
   - leaves the repository in the state later stages expect;
   - passes verification reproducibly.
5. Do not fix anything. Do not modify files.
6. Report exactly one of:
   - `Stage <STAGE_NUMBER> is ready` — optionally followed by non-blocking
     observations;
   - `Stage <STAGE_NUMBER> is not ready` — followed by a numbered list of
     concrete problems, each with file/line evidence and what must change.
```

## Phase 5 — Completion

When every stage has passed validation and been committed:

1. Delete the plan file.
2. Commit the deletion with a message such as `Remove completed implementation plan`.
3. Print the final cumulative report:
   - stages completed, in order, with their commit messages;
   - for each stage, the number of implement/validate rounds used;
   - all validator findings raised during execution and how each was resolved;
   - all problems found and fixed or rejected during the audit loop;
   - confirmation that the plan file is deleted and the working tree is clean.
4. Stop. Do not begin additional work.

## Finding ledger

Maintain one ledger for the whole run, used for both loops. Keep it in memory and reproduce it in the Phase 3 and Phase 5 reports.

| Iteration | Lens | Phase | Location | Severity | Finding | Status | Resolution |
|-----------|------|-------|----------|----------|---------|--------|------------|

Status values: `fixed`, `rejected as invalid`, `minor, accepted`.

## Guardrails

- One plan file, one task. Never span unrelated work in a single plan.
- Never write the plan on top of unresolved questions.
- Never overwrite an existing plan file.
- Commit the plan before every audit iteration; skip the commit only when the file is unchanged.
- Never hand the plan text to an auditor or implementator; give them the path.
- Run the three lens auditors independently and in parallel; never show one auditor another's report.
- Never accept a finding without checking it. Never reject a finding without a reason.
- Never let a minor finding extend the audit loop.
- Never start implementation without explicit permission.
- Never start implementation when the final resolution is not `Plan is implementation ready`.
- Never edit the plan after implementation permission is granted. If it turns out to be wrong, stop, report, and let the user decide.
- Never commit unrelated files, failed stages, or multiple stages in one commit.
- Never proceed after the audit cap or the per-stage retry cap; report and wait.
- Do not create side files for reports — sub-agent reports are returned in their response.
- All plan and report text is written in English.
- This skill is manual-only. It never self-triggers and is not registered for automatic invocation.
