<p align="center">
  <img src="https://raw.githubusercontent.com/robertard7/Builder-Rah/main/docs/rah-logo.png" alt="Builder Rah Logo" width="200" />
  <br />
  <strong>Builder Rah</strong> — deterministic artifact generation + API + headless automation
</p>

<p align="center">
  <a href="https://github.com/robertard7/Builder-Rah/stargazers"><img src="https://img.shields.io/github/stars/robertard7/Builder-Rah?style=flat-square" /></a>
  <a href="https://github.com/robertard7/Builder-Rah/actions"><img src="https://img.shields.io/github/actions/workflow/status/robertard7/Builder-Rah/ci.yml?style=flat-square" /></a>
  <a href="https://github.com/robertard7/Builder-Rah/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/robertard7/Builder-Rah/ci.yml?style=flat-square&label=TS%20Client%20CI" /></a>
  <a href="https://codecov.io/gh/robertard7/Builder-Rah"><img src="https://img.shields.io/codecov/c/github/robertard7/Builder-Rah/main?style=flat-square" /></a>
  <a href="https://codecov.io/gh/robertard7/Builder-Rah"><img src="https://img.shields.io/codecov/c/github/robertard7/Builder-Rah/main?flag=ts-client&style=flat-square" alt="TS Client Coverage" /></a>
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

### Resilience API examples

Reset resilience metrics:

```pwsh
Invoke-RestMethod "http://localhost:5050/metrics/resilience/reset" -Method Put
```

```bash
curl -X PUT http://localhost:5050/metrics/resilience/reset
```

Query resilience history range:

```pwsh
Invoke-RestMethod "http://localhost:5050/metrics/resilience/history?start=2026-01-11T01:00:00Z&end=2026-01-11T02:00:00Z"
```

```bash
curl "http://localhost:5050/metrics/resilience/history?start=2026-01-11T01:00:00Z&end=2026-01-11T02:00:00Z"
```

Create, list, and delete alert rules:

```pwsh
$rule = @{ name = "retry-spike"; openThreshold = 3; retryThreshold = 10; windowMinutes = 60; severity = "warning" } | ConvertTo-Json
Invoke-RestMethod "http://localhost:5050/alerts" -Method Post -Body $rule -ContentType "application/json"
Invoke-RestMethod "http://localhost:5050/alerts?limit=20"
Invoke-RestMethod "http://localhost:5050/alerts?ruleId=rule-1" -Method Delete
```

```bash
curl -X POST http://localhost:5050/alerts \
  -H "Content-Type: application/json" \
  -d '{"name":"retry-spike","openThreshold":3,"retryThreshold":10,"windowMinutes":60,"severity":"warning"}'
curl "http://localhost:5050/alerts?limit=20"
curl -X DELETE "http://localhost:5050/alerts?ruleId=rule-1"
```

### Resilience TypeScript client

Node usage:

```ts
import { ResilienceClient } from "@builder-rah/resilience-client";

const client = new ResilienceClient("http://localhost:5050");
const metrics = await client.getMetrics();
const history = await client.getHistoryRange("2026-01-11T01:00:00Z", "2026-01-11T02:00:00Z", { page: 1, perPage: 50 });
console.log(metrics, history.items.length);
```

Deno usage:

```ts
import { ResilienceClient } from "npm:@builder-rah/resilience-client";

const client = new ResilienceClient("http://localhost:5050");
const alerts = await client.getAlertsBySeverity("critical", 25);
console.log(alerts.events);
```

Browser usage:

```ts
import { ResilienceClient } from "@builder-rah/resilience-client";

const client = new ResilienceClient("https://api.example.com", "<token>");
const reset = await client.resetMetrics();
console.log(reset.ok);
```

### Resilience OpenAPI reference

All resilience endpoints return metadata-enveloped payloads. For example:

```json
{
  "metadata": {
    "version": "1",
    "timestamp": "2026-01-11T01:00:00Z",
    "requestId": "req-123"
  },
  "data": {
    "openCount": 1,
    "halfOpenCount": 0,
    "closedCount": 4,
    "retryAttempts": 2
  }
}
```

TypeScript snippet using the client:

```ts
const response = await client.getMetricsResponse();
console.log(response.metadata.requestId, response.data.openCount);
```

### Troubleshooting common errors

- `invalid_date_range`: `start` must be less than `end` for history requests.
- `threshold_required`: alert rules must include `openThreshold` or `retryThreshold`.
- `acknowledged_required`: alert acknowledgment requires `acknowledged: true`.

When using the TypeScript client, a `ResilienceClientError` includes `status`, `code`, and `documentation` fields for quick diagnosis.

## CLI

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
rah resilience alerts list --severity critical
rah resilience alert resolve <eventId>
```

## Resilience Prometheus metrics

The endpoint `GET /metrics/resilience/prometheus` exports the following metrics:

- `resilience_open_count`
- `resilience_half_open_count`
- `resilience_closed_count`
- `resilience_retry_attempts`
- `resilience_tool_open_count{tool="<toolId>"}`
- `resilience_tool_retry_attempts{tool="<toolId>"}`

Label usage is intentionally limited to avoid high-cardinality metrics.

## Caching

MIT © Robert Ard
