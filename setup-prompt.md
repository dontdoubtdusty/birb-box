My CLAUDE.md already has the rules I gave you earlier. Do not write anything until I approve your drafts.

Step 1: Read the current CLAUDE.md and look at the project (structure, solution/csproj files, existing docs, whether a claude/ folder exists). Do not edit anything yet.

Step 2: Show me the changes below as drafts, then wait for my OK.

A. CLAUDE.md changes
- Remove the "every 5th session, remind me to do a de-tangling pass" rule. Keep the session counter.
- Add this section:

## Wrap up
When I say "wrap up", do all of these:
1. Update claude/file-map.md if files changed.
2. Add a dated entry to claude/session-log.md (what was done, what is next) and add 1 to the session counter.
3. Propose claude/design.md updates if a change made it wrong or finished a backlog item.
4. Bump the version by 0.01 if a feature was finished.
5. Optionally suggest 1-2 small "try it yourself" tasks. Never block on them.
6. Give me a short summary.

- Shorten the whole file: merge duplicate rules, cut filler, keep my wording where it is already tight, and keep any project-specific facts (build/run/test commands, architecture notes). Show me the full revised file, not a diff.

B. Create any of these that do not exist yet (skip any that already do, and tell me):
- claude/file-map.md: one line per source file saying what it is for, generated from the actual code. Skip build output and generated folders.
- claude/session-log.md: a "Session counter: 0" line at the top, then an entries section. Add a first entry for this setup session and set the counter to 1.
- claude/design.md: sections for Vision, Current state, Backlog, Open decisions, Out of scope. Fill in Current state from the code. Do not invent the other sections: draft what you can infer, mark guesses clearly, and ask me about the rest.

C. Version number
Find where the project's version lives (for example the csproj Version property). If there is no single clear place, ask me with multiple choice where to put it. Report the current version.

After I approve, write the changes and tell me what you changed and created.