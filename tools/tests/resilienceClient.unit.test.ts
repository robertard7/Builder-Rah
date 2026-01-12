import { afterEach, beforeEach, test } from "node:test";
import assert from "node:assert/strict";
import http from "node:http";
import { ResilienceClient, ResilienceClientError, isResilienceClientError } from "../resilienceClient";

const createServer = async (handler: http.RequestListener) => {
  const server = http.createServer(handler);
  await new Promise<void>((resolve) => server.listen(0, resolve));
  const address = server.address();
  if (!address || typeof address === "string") throw new Error("Failed to bind server");
  const baseUrl = `http://127.0.0.1:${address.port}`;
  return { server, baseUrl };
};

let server: http.Server | undefined;
let baseUrl = "";

beforeEach(async () => {
  const result = await createServer((req, res) => {
    const url = new URL(req.url ?? "", "http://localhost");
    res.setHeader("Content-Type", "application/json");

    if (url.pathname === "/metrics/resilience") {
      res.writeHead(200);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: new Date().toISOString(), requestId: "req-1" },
          data: { openCount: 1, halfOpenCount: 0, closedCount: 4, retryAttempts: 2 }
        })
      );
      return;
    }

    if (url.pathname === "/metrics/resilience/history") {
      res.writeHead(200);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: new Date().toISOString(), requestId: "req-2" },
          total: 1,
          page: Number(url.searchParams.get("page") ?? "1"),
          perPage: Number(url.searchParams.get("perPage") ?? "50"),
          items: [
            {
              timestamp: "2026-01-11T01:00:00Z",
              metrics: { openCount: 2, halfOpenCount: 1, closedCount: 3, retryAttempts: 5 }
            }
          ]
        })
      );
      return;
    }

    if (url.pathname === "/metrics/resilience/reset" && req.method === "PUT") {
      res.writeHead(200);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: new Date().toISOString(), requestId: "req-3" },
          ok: true,
          resetAt: "2026-01-11T02:00:00Z"
        })
      );
      return;
    }

    res.writeHead(404);
    res.end(
      JSON.stringify({
        metadata: { version: "1", timestamp: new Date().toISOString(), requestId: "req-404" },
        error: { code: "not_found", message: "not found", details: { docs: "README.md#resilience-api-examples" } }
      })
    );
  });

  server = result.server;
  baseUrl = result.baseUrl;
});

afterEach(async () => {
  if (!server) return;
  await new Promise<void>((resolve) => server?.close(() => resolve()));
  server = undefined;
});

test("getMetrics returns metrics snapshot", async () => {
  const client = new ResilienceClient(baseUrl);
  const metrics = await client.getMetrics();
  assert.equal(metrics.openCount, 1);
  assert.equal(metrics.retryAttempts, 2);
});

test("getHistory supports pagination", async () => {
  const client = new ResilienceClient(baseUrl);
  const response = await client.getHistory({ page: 2, perPage: 5 });
  assert.equal(response.page, 2);
  assert.equal(response.perPage, 5);
  assert.equal(response.items.length, 1);
});

test("resetMetrics uses put", async () => {
  const client = new ResilienceClient(baseUrl);
  const response = await client.resetMetrics();
  assert.equal(response.ok, true);
  assert.equal(response.resetAt, "2026-01-11T02:00:00Z");
});

test("getMetricsWithHistory combines responses", async () => {
  const client = new ResilienceClient(baseUrl);
  const combined = await client.getMetricsWithHistory({ historyPage: 2, historyPerPage: 10 });
  assert.equal(combined.metrics.data.openCount, 1);
  assert.equal(combined.history.page, 2);
});

test("errors include metadata and docs", async () => {
  const client = new ResilienceClient(baseUrl);
  let thrown: ResilienceClientError | undefined;

  try {
    await client.deleteAlerts();
  } catch (error) {
    if (isResilienceClientError(error)) thrown = error;
  }

  assert.ok(thrown);
  assert.equal(thrown?.status, 404);
  assert.equal(thrown?.code, "not_found");
  assert.equal(thrown?.documentation, "README.md#resilience-api-examples");
});
