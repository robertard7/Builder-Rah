# Builder Rah Artifact Generation

This WinForms tool can now package generated program artifacts, expose them over an API, and run in headless script mode.

## Artifact flow

1. Plans that include generation steps trigger the artifact generator (or reuse cache).
2. Generated files are written under `Workflow/ProgramArtifacts/<timestamp>-<session>-<hash>/`.
3. A zip of the project is created beside the folder and cached by semantic hash.
4. Output cards include a project tree, file previews, a summary card, and a download card.

## API endpoints

- `GET /api/artifacts?session=<token>` — lists artifacts, tree previews, hashes, and zip paths for the active session.
- `GET /api/artifacts/download?session=<token>&hash=<hash>` — streams the artifact zip (latest if hash omitted).
- `GET /api/output?session=<token>` — existing output cards (includes new artifact cards).
- `POST /api/jobs` with `{ "text": "...", "session": "<token>" }` — submit work; session overrides are allowed only here.

Sessions must match the workflow’s current session token for artifact endpoints; mismatches return `session_mismatch`.

### Example (PowerShell)

```pwsh
Invoke-RestMethod "http://localhost:5050/api/jobs" -Method Post -Body '{\"text\":\"Build TODO API with tests\",\"session\":\"abc\"}' -ContentType "application/json"
Invoke-RestMethod "http://localhost:5050/api/artifacts?session=abc"
Invoke-WebRequest "http://localhost:5050/api/artifacts/download?session=abc" -OutFile artifacts.zip
```

### Example (curl)

```bash
curl -X POST http://localhost:5050/api/jobs -H "Content-Type: application/json" -d '{"text":"Build TODO API with tests","session":"abc"}'
curl http://localhost:5050/api/artifacts?session=abc
curl -o artifacts.zip "http://localhost:5050/api/artifacts/download?session=abc"
```

## UI updates

- Output tab shows project tree and file preview cards using a tree view.
- Select files in the tree to view previews; “Download ZIP” saves the generated archive for the selected artifact card.

## Headless/script mode

Run without UI to serve API-only workflows:

```bash
dotnet run -- --headless
```

The provider API will start using your configured settings; submit jobs via `/api/jobs`.

Generate artifacts directly without UI:

```bash
dotnet run -- --headless --text "Build TODO API with auth and tests" --output ./out
```

This runs the workflow once, waits for artifact completion, and copies the generated zip to `./out`.

## Headless session API

The headless server also exposes session endpoints for integrations:

- `GET /sessions`
- `POST /sessions`
- `GET /sessions/{id}`
- `GET /sessions/{id}/status`
- `GET /sessions/{id}/plan`
- `POST /sessions/{id}/message`
- `POST /sessions/{id}/attachments`
- `POST /sessions/{id}/run`
- `POST /sessions/{id}/cancel`
- `DELETE /sessions/{id}`
- `GET /provider/metrics`
- `GET /provider/events`

See `openapi.yaml` for the full schema.

## CLI

The CLI is available via `rah`:

```bash
rah session list
rah session start --id <id>
rah session send --id <id> --message "text"
rah session status --id <id>
rah session plan --id <id>
rah session cancel --id <id>
rah run --id <id>
```

## Caching

- Artifact sets are hashed by job spec, constraints, attachments, and tool outputs (SHA256).
- Cache is stored under `Workflow/ProgramArtifacts/cache/cache.json`.
- When a hash matches, the generator reuses the cached zip and previews instead of regenerating.
