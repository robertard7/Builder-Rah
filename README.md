# Builder Rah Artifact Generation

This WinForms tool can now package generated program artifacts, expose them over an API, and run in headless script mode.

## Artifact flow

1. Plans that include generation steps trigger the artifact generator.
2. Generated files are written under `Workflow/ProgramArtifacts/<timestamp>-<session>/`.
3. A zip of the project is created beside the folder.
4. Output cards include a project tree, file previews, and a download card.

## API endpoints

- `GET /api/artifacts?session=<token>` — lists artifacts, tree previews, and zip paths for the active session.
- `GET /api/artifacts/download?session=<token>` — streams the most recent artifact zip for the session.
- `GET /api/output?session=<token>` — existing output cards (includes new artifact cards).
- `POST /api/jobs` with `{ "text": "...", "session": "<token>" }` — submit work; session overrides are allowed only here.

Sessions must match the workflow’s current session token for artifact endpoints; mismatches return `session_mismatch`.

## UI updates

- Output tab shows project tree and file preview cards.
- “Download ZIP” saves the generated archive for the selected artifact card.

## Headless/script mode

Run without UI to serve API-only workflows:

```bash
dotnet run -- --headless
```

The provider API will start using your configured settings; submit jobs via `/api/jobs`.
