# Windows prompt maintenance guidelines

These notes lock in the Codex workflow rules for the Windows toolbox prompts stored in this folder.

## Core rules
- Always read the existing `.txt` prompt files before editing anything.
- Only create a new prompt file when a tool id from `windows.tools.json` is missing entirely.
- Never rename tool ids. The file name **must** be `<toolId>.txt` and the TOOL ID line inside the file must match exactly.
- Do not drop the `windows.` prefix for Windows tools; it distinguishes them from Linux prompt files that may also live here.

## Update process
1. Start from `windows.tools.json` (source of truth) and confirm the ordered tool id list.
2. Map each prompt by its TOOL ID line; fix mismatches before changing names.
3. If a file exists but the name disagrees with the TOOL ID, rename the file to match the TOOL ID instead of changing the id.
4. If content needs edits, preserve the existing command template semantics—no regeneration or invention of new commands.
5. Keep prompts between 100–300 lines by expanding guidance sections without altering TOOL ID, CATEGORY, or PURPOSE.

## Anti-regeneration guardrails
- Do not regenerate prompts from scratch; extend or correct the existing file in place.
- Do not invent new tool ids or variants; adhere strictly to the manifest.
- When in doubt, prefer renaming/mapping existing files over writing fresh content.
