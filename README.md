<p align="center">
  <img src="https://raw.githubusercontent.com/robertard7/Builder-Rah/main/docs/rah-logo.png" alt="Builder Rah Logo" width="200" />
  <br />
  <strong>Builder Rah</strong> — deterministic artifact generation + API + headless automation
</p>

<p align="center">
  <a href="https://github.com/robertard7/Builder-Rah/stargazers"><img src="https://img.shields.io/github/stars/robertard7/Builder-Rah?style=flat-square" /></a>
  <a href="https://github.com/robertard7/Builder-Rah/actions"><img src="https://img.shields.io/github/actions/workflow/status/robertard7/Builder-Rah/ci.yml?style=flat-square" /></a>
  <a href="https://github.com/robertard7/Builder-Rah/network/members"><img src="https://img.shields.io/github/forks/robertard7/Builder-Rah?style=flat-square" /></a>
  <a href="https://github.com/robertard7/Builder-Rah/blob/main/LICENSE"><img src="https://img.shields.io/github/license/robertard7/Builder-Rah?style=flat-square" /></a>
</p>

---

## 🚀 What is Builder Rah?

**Builder Rah** is a hybrid WinForms + headless build automation engine. It turns **text-driven jobs** into **reproducible code artifacts** with:

- deterministic execution
- cacheable project artifacts
- preview & downloadable zips
- REST API and CLI control
- session orchestration for workflows

It’s not just a code generator. It is a **build system that can be scripted, automated, and integrated** into larger tooling pipelines.

---

## 🧠 Core Concepts

**Artifacts**

A job produces a *artifact set*:

Workflow/ProgramArtifacts/<timestamp>-<session>-<hash>/


Each set includes:

- full project tree
- file previews
- a `.zip` archive
- semantic cache key (SHA-256)

➡ Artifacts are cached so repeated runs are fast and deterministic. :contentReference[oaicite:0]{index=0}

---

## 📡 REST API

### Submit a Job
**POST** `/api/jobs`

```json
{
  "text": "Build TODO API with auth and tests",
  "session": "abc"
}

This starts a new execution plan based on natural language.
List Artifacts

GET /api/artifacts?session=<token>

Returns metadata, preview trees, hashes, and ZIP paths.
Download Artifacts

GET /api/artifacts/download?session=<token>&hash=<hash>

Streams the ZIP. Latest if hash omitted.
🖥 UI Features

    Side-pane tree view of generated artifacts

    File previews on click

    Downloadable ZIP per artifact card

    Real-time workflow status

These make your generated projects easy to browse without cloning.
🧰 Headless Mode

Run without GUI:

dotnet run -- --headless

Submit jobs via API. Perfect for CI or automation.

One-shot generation

dotnet run -- --headless --text "Build TODO API with auth and tests" --output ./out

Produces an artifact zip and exits.
💬 Session API (headless)

Control sessions programmatically:
Endpoint	Description
GET /sessions	list all
POST /sessions	create new
GET /sessions/{id}/status	check state
POST /sessions/{id}/run	run workflow
POST /sessions/{id}/cancel	stop active session
DELETE /sessions/{id}	remove session

Plus provider diagnostics:

    GET /provider/metrics

    GET /provider/events 

Full schema in openapi.yaml.
📟 CLI (rah)

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

Uniform control surface for automation and tooling.
🧠 Design Philosophy

Builder Rah values:

    determinism over guesswork

    cache reuse over redundant generation

    automation over manual steps

    self-service artifacts over opaque code dumps

This is a tool for building consistent portable projects that anyone or anything can consume.
🔧 Getting Started

    Clone the repo

    Restore dependencies

    Run dotnet run

        UI mode by default

        Add --headless for API only

Artifacts appear under Workflow/ProgramArtifacts/ once jobs complete.
📄 Resources

    OpenAPI spec: openapi.yaml

    Example settings: appsettings.example.json

    API usage examples: see docs folder

🏷️ Tags

builder artifact-generation headless automation api cli
🛡 License

MIT © Robert Ard
