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

- **Auditor** — reads the plan from disk and attacks it.
- **Implementator** — reads the plan from disk and implements exactly one stage.
- **Validator** — reads the plan stage and checks the resulting work.

Sub-agents never receive the orchestrator's reasoning or chat history. Pass them only what the prompt templates below specify: file paths, stage numbers, and the exact report format. This isolation is deliberate — it prevents the auditor from inheriting the author's assumptions and the implementator from inheriting the auditor's doubts.

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

Repeat until the stopping condition is met. Each iteration uses a **new** auditor sub-agent with no memory of previous iterations or of the orchestrator's reasoning.

### 2.1 Spawn the auditor

Use the auditor prompt template below, substituting the plan path and iteration number. The auditor reads the plan from disk itself; never paste the plan into the prompt.

### 2.2 Triage the report

The orchestrator evaluates **every** finding. An auditor can misread the plan, import outside assumptions, or invent a problem. For each finding:

1. Locate it in the plan (or the referenced code).
2. Decide: **valid** — the problem genuinely exists and would block, mislead, or force guess-work during implementation; or **invalid** — the auditor misread, the concern is already addressed elsewhere in the plan, or the point is stylistic preference.
3. Valid findings are fixed in the plan file immediately, at the smallest scope that resolves the problem. If a fix ripples across stages, update every affected stage in the same pass.
4. Invalid findings are recorded as rejected with a one-line reason. Do not fix them, do not argue with the auditor, and do not let them trigger churn.

Never "fix" a finding by adding vague reassurance. If the fix is to clarify, clarify concretely; if the fix is to add a missing step, add the step and its verification.

### 2.3 Severity

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

### 2.4 Stopping condition

Stop when both are true:

- The latest report ends with `Plan is implementation ready`.
- The orchestrator confirms zero major findings remain after triage.

Or stop when the iteration counter reaches **N = 5** (the user may set a different N when invoking the skill). Minor findings do not extend the loop; record them in the ledger and report them at the end.

If the cap is reached with major findings outstanding, the plan is **not ready**. Report and wait for the user. Do not start implementation.

### 2.5 Auditor prompt template

```text
You are an independent plan auditor. You have no prior context.

1. Read the plan at: <PLAN_PATH> (iteration <ITERATION>).
2. Audit it as if you were the person who must implement it with no further
   clarification and no access to the author.
3. Look specifically for:
   - contradictions between sections, stages, constraints, or non-goals;
   - missing implementation details that force research or guess-work;
   - stages that cannot be verified, or verification that does not prove the
     stage's objective;
   - wrong paths, type/member names, APIs, commands, or environment assumptions
     (inspect the repository to confirm references where needed);
   - ordering or dependency errors between stages;
   - assumptions presented as facts without evidence;
   - scope drift or unstated requirements.
4. Report every finding with:
   - location (section or stage number),
   - severity (major or minor),
   - the problem in one or two sentences,
   - the concrete fix.
   Do not pad the report: if a finding is speculative, omit it. Do not restate
   the plan or praise it.
5. End with exactly one line:
   Plan is implementation ready
   or
   Plan is not implementation ready
```

## Phase 3 — Cumulative report and permission gate

After the loop terminates, print a cumulative report:

- Number of iterations run and the cap.
- A table of every finding ever raised: iteration, location, severity, status (`fixed` / `rejected as invalid` / `minor, accepted`), and the resolution.
- Any minor findings left in place.
- The final resolution, stated plainly: **Plan is implementation ready** or **Plan is not implementation ready**.

Then stop and wait.

Implementation may begin only on an explicit, unambiguous instruction to implement, such as "start implementation", "execute the plan", or "go ahead and implement it". A bare "continue", "ok", or "proceed" is not permission — ask.

## Phase 4 — Execution

### 4.1 Preflight

1. Run `git status --porcelain`. The only permitted entry is the plan file itself.
2. If any other modified, staged, or untracked file exists, **stop** and report the list. Do not stash, clean, commit, or work around the user's changes.
3. Commit the plan file alone with a message such as `Add implementation plan`.

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

| Iteration | Phase | Location | Severity | Finding | Status | Resolution |
|-----------|-------|----------|----------|---------|--------|------------|

Status values: `fixed`, `rejected as invalid`, `minor, accepted`.

## Guardrails

- One plan file, one task. Never span unrelated work in a single plan.
- Never write the plan on top of unresolved questions.
- Never overwrite an existing plan file.
- Never hand the plan text to an auditor or implementator; give them the path.
- Never accept a finding without checking it. Never reject a finding without a reason.
- Never let a minor finding extend the audit loop.
- Never start implementation without explicit permission.
- Never edit the plan after implementation permission is granted. If it turns out to be wrong, stop, report, and let the user decide.
- Never commit unrelated files, failed stages, or multiple stages in one commit.
- Never proceed after the audit cap or the per-stage retry cap; report and wait.
- Do not create side files for reports — sub-agent reports are returned in their response.
- All plan and report text is written in English.
- This skill is manual-only. It never self-triggers and is not registered for automatic invocation.
