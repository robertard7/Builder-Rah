Builder Rah

Builder Rah is a WinForms-based AI workflow runner that plans, executes, and packages generated program artifacts in a deterministic, cacheable way. It supports interactive UI usage, full headless operation, a REST API, and a CLI for automation and integrations.

At its core, Builder Rah turns natural-language jobs into reproducible software artifacts, complete with file trees, previews, and downloadable archives.

This is not a chat app. It is a build system that happens to talk to models.

What Builder Rah Does

Plans multi-step workflows from natural language input

Executes tools and providers deterministically

Generates real project files on disk

Packages results as cached, hash-addressed artifacts

Serves artifacts via UI, API, CLI, or headless mode

Tracks provider health, metrics, and events

If a job can produce code, Builder Rah can package it, cache it, and ship it.

Artifact System Overview

Artifact generation is a first-class feature.

Artifact Flow

A workflow plan includes one or more generation steps

The artifact generator runs or reuses a cached result

Generated files are written to:

Workflow/ProgramArtifacts/<timestamp>-<session>-<hash>/


A ZIP archive is created alongside the folder

Artifacts are cached by semantic SHA-256 hash

UI output cards expose:

Project tree

File previews

Summary metadata

Download links

When inputs match, artifacts are reused instead of regenerated.

Artifact Cache

Hash inputs include:

Job spec text

Constraints

Attachments

Tool outputs

Cache metadata lives at:

Workflow/ProgramArtifacts/cache/cache.json


Cache hits reuse:

ZIP archive

File tree

Previews

This makes runs reproducible and fast instead of expensive and chaotic.

Running the App
UI Mode (Default)
dotnet run


Starts the WinForms UI with full workflow, output, and artifact browsing.

Headless Mode (API Server)
dotnet run -- --headless


Runs Builder Rah as an API-only service. No UI. Suitable for automation, CI, or integrations.

One-Shot Headless Execution
dotnet run -- --headless --text "Build TODO API with auth and tests" --output ./out


Runs a single workflow

Waits for artifact completion

Copies the generated ZIP to ./out

Exits

REST API
Submit a Job

POST /api/jobs

{
  "text": "Build TODO API with tests",
  "session": "abc"
}


Session overrides are allowed only on this endpoint.

List Artifacts

GET /api/artifacts?session=<token>

Returns:

Artifact hashes

File tree previews

ZIP paths

Metadata for the active session

Download Artifacts

GET /api/artifacts/download?session=<token>&hash=<hash>

Streams the ZIP archive

If hash is omitted, the latest artifact is returned

Session mismatches return:

{ "error": "session_mismatch" }

Output Cards

GET /api/output?session=<token>

Returns all output cards, including artifact summaries and previews.

Session API (Headless)

The headless server exposes full session lifecycle control:

GET /sessions

POST /sessions

GET /sessions/{id}

GET /sessions/{id}/status

GET /sessions/{id}/plan

POST /sessions/{id}/message

POST /sessions/{id}/attachments

POST /sessions/{id}/run

POST /sessions/{id}/cancel

DELETE /sessions/{id}

Provider diagnostics:

GET /provider/metrics

GET /provider/events

See openapi.yaml for the complete schema.

CLI (rah)

Builder Rah includes a CLI for scripting and automation.

rah session list
rah session start --id <id>
rah session send --id <id> --message "text"
rah session status --id <id>
rah session plan --id <id>
rah session run --id <id>
rah session cancel --id <id>
rah session delete --id <id>

rah provider metrics
rah provider events


The CLI talks to the same API as the UI and headless server.

UI Features

Output tab shows artifact trees using a tree view

File selection shows inline previews

Artifact cards include:

Summary

File list

Download ZIP action

Provider health and metrics are surfaced in real time

Providers

Providers are modular, state-tracked execution backends.

Features include:

Enable/disable controls

Reachability checks

Retry and backoff

Metrics and event logging

Health surfaced in UI and API

The workflow routes execution based on provider availability instead of guessing.

Project Structure (Relevant Bits)
Workflow/
  ProgramArtifacts/
    cache/
    <timestamp>-<session>-<hash>/
Ui/
Api/
Cli/
Providers/
openapi.yaml

Philosophy (Short Version)

Deterministic over clever

Cached over regenerated

Observable over magical

Build artifacts, not vibes

If it generated files, you should be able to download them, hash them, and reuse them.

Builder Rah enforces that.
