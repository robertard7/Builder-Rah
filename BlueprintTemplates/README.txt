BlueprintTemplates Seed Corpus (SHIPS WITH APP)
================================================

Purpose
-------
This folder contains the shippable template corpus that the app ingests into SQLite
(BlueprintStoreSqlite) and indexes into Qdrant for reuse. The corpus is designed
to be deterministic, graph-gated, and settings-driven.

DO NOT hardcode providers/models/toolchains in templates.
Templates may reference Settings by *name* only (e.g., rolePreset: "fast").

Template levels
---------------
1) PACKS   (BlueprintTemplates/packs)
   - User-facing selections (what Qdrant should return by default)
   - Examples: "Desktop Forms App", "Invoice App", "CLI Tool", "REST API Service"
   - These reference recipes/atoms/graphs by ID.

2) RECIPES (BlueprintTemplates/recipes)
   - Mid-level reusable blocks (optional but helpful)
   - Examples: "Add File Menu + dirty flag", "Add SQLite wiring"

3) ATOMS   (BlueprintTemplates/atoms)
   - Engine-only primitives (NEVER user-facing)
   - Examples: preset.role.fast, task.add_file, task.replace_file
   - Must declare meta.visibility = "internal"

4) GRAPHS  (BlueprintTemplates/graphs)
   - Mermaid router/orchestrator graphs or fragments referenced by packs.

Drafts + Approval
-----------------
- drafts/ holds generated files awaiting human approval.
- Only files listed in manifest.json are considered ACTIVE and shipped/ingested.

manifest.json (Source of Truth)
-------------------------------
- manifest.json lists every active template file and its metadata.
- The app (or a watcher tool) MUST treat manifest.json as the authoritative index.
- Files not in manifest.json are ignored (no dead weight).
- Removing a file must remove it from manifest.json.

Auto-maintenance requirement
----------------------------
A watcher/maintainer (later code) must:
- Detect add/update/remove of *.json under packs/recipes/atoms/graphs
- Validate schema + naming
- Update manifest.json accordingly:
  - add entry on new file
  - update sha256 + updatedUtc on changes
  - remove entry when file is deleted

Visibility + Search policy
--------------------------
- Packs: meta.visibility = "public", priority high (100+)
- Recipes: meta.visibility = "public" (or "internal" if you want), priority medium
- Atoms: meta.visibility = "internal", priority low (<=10)

Qdrant indexing policy
----------------------
- Normal reuse search MUST filter to payload.visibility="public"
- Atoms are searchable only during "expand mode" (pack/recipe expansion)
  OR stored in a separate collection.

Schema (minimum fields per template file)
-----------------------------------------
All templates are JSON and must contain:
- id (string, unique)
- kind (pack | recipe | atom | graph | orchestrator_preset | taskboard | intent | map)
- meta.visibility ("public" | "internal")
- title (string)
- description (string)
- tags (array of strings)
- version (string, e.g. "v1")
- updatedUtc (ISO-8601)
- content (object)  <-- meaning depends on kind (pack/recipe/atom/graph)

Important bans
--------------
- No provider IDs in templates (ollama/openai/hf) unless template kind is INTERNAL tooling only,
  and even then it must reference settings by name instead of hardcoding.
- No model IDs in templates. Ever. Use Settings.

