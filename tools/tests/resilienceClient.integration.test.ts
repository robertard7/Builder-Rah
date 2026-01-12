import { afterEach, test } from "node:test";
import assert from "node:assert/strict";
import http from "node:http";
import { ResilienceClient } from "../resilienceClient";

const startServer = async (handler: http.RequestListener) => {
  const server = http.createServer(handler);
  await new Promise<void>((resolve) => server.listen(0, resolve));
  const address = server.address();
  if (!address || typeof address === "string") throw new Error("Failed to bind server");
  const baseUrl = `http://127.0.0.1:${address.port}`;
  return { server, baseUrl };
};

test("client methods serialize requests and parse responses", async () => {
  const requests: Array<{ method?: string; url: string; body?: string }> = [];

  const { server, baseUrl } = await startServer((req, res) => {
    const bodyChunks: Buffer[] = [];
    req.on("data", (chunk) => bodyChunks.push(chunk));
    req.on("end", () => {
      const body = bodyChunks.length ? Buffer.concat(bodyChunks).toString("utf8") : undefined;
      requests.push({ method: req.method, url: req.url ?? "", body });
    });

    const url = new URL(req.url ?? "", "http://localhost");
    res.setHeader("Content-Type", "application/json");

    if (url.pathname === "/metrics/resilience") {
      res.writeHead(200);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: "2026-01-11T01:00:00Z", requestId: "req-metrics" },
          data: { openCount: 1, halfOpenCount: 0, closedCount: 2, retryAttempts: 3 }
        })
      );
      return;
    }

    if (url.pathname === "/metrics/resilience/history") {
      res.writeHead(200);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: "2026-01-11T01:00:00Z", requestId: "req-history" },
          total: 2,
          page: Number(url.searchParams.get("page") ?? "1"),
          perPage: Number(url.searchParams.get("perPage") ?? "50"),
          items: [
            {
              timestamp: "2026-01-11T01:00:00Z",
              metrics: { openCount: 1, halfOpenCount: 0, closedCount: 2, retryAttempts: 3 }
            }
          ]
        })
      );
      return;
    }

    if (url.pathname === "/metrics/resilience/reset") {
      res.writeHead(200);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: "2026-01-11T01:00:00Z", requestId: "req-reset" },
          ok: true,
          resetAt: "2026-01-11T01:10:00Z"
        })
      );
      return;
    }

    if (url.pathname === "/alerts" && req.method === "POST") {
      res.writeHead(201);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: "2026-01-11T01:00:00Z", requestId: "req-alert" },
          data: { id: "rule-1", name: "critical", openThreshold: 2, retryThreshold: 1, windowMinutes: 60, severity: "critical", enabled: true }
        })
      );
      return;
    }

    if (url.pathname === "/alerts/thresholds") {
      res.writeHead(201);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: "2026-01-11T01:00:00Z", requestId: "req-threshold" },
          data: { id: "rule-2", name: "warning", openThreshold: 1, retryThreshold: 0, windowMinutes: 30, severity: "warning", enabled: true }
        })
      );
      return;
    }

    if (url.pathname === "/alerts" && req.method === "GET") {
      res.writeHead(200);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: "2026-01-11T01:00:00Z", requestId: "req-alerts" },
          rules: [
            {
              id: "rule-1",
              name: "critical",
              openThreshold: 2,
              retryThreshold: 1,
              windowMinutes: 60,
              severity: "critical",
              enabled: true,
              recentEvents: []
            }
          ],
          events: [
            {
              id: "evt-1",
              ruleId: "rule-1",
              message: "circuit open",
              severity: "critical",
              triggeredAt: "2026-01-11T01:05:00Z",
              openDelta: 2,
              retryDelta: 1,
              acknowledged: false
            }
          ]
        })
      );
      return;
    }

    if (url.pathname === "/alerts/rule-1" && req.method === "PATCH") {
      res.writeHead(200);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: "2026-01-11T01:00:00Z", requestId: "req-update" },
          data: { id: "rule-1", name: "critical", openThreshold: 3, retryThreshold: 2, windowMinutes: 45, severity: "critical", enabled: false }
        })
      );
      return;
    }

    if (url.pathname === "/alerts/events/evt-1" && req.method === "PATCH") {
      res.writeHead(200);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: "2026-01-11T01:00:00Z", requestId: "req-ack" },
          data: {
            id: "evt-1",
            ruleId: "rule-1",
            message: "circuit open",
            severity: "critical",
            triggeredAt: "2026-01-11T01:05:00Z",
            openDelta: 2,
            retryDelta: 1,
            acknowledged: true,
            acknowledgedAt: "2026-01-11T01:06:00Z"
          }
        })
      );
      return;
    }

    if (url.pathname === "/alerts" && req.method === "DELETE") {
      res.writeHead(200);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: "2026-01-11T01:00:00Z", requestId: "req-delete" },
          ok: true,
          ruleId: url.searchParams.get("ruleId")
        })
      );
      return;
    }

    res.writeHead(404);
    res.end(JSON.stringify({ error: { code: "not_found", message: "not found" } }));
  });

  const client = new ResilienceClient(baseUrl);

  const metrics = await client.getMetrics({ state: "open", minRetryAttempts: 2 });
  assert.equal(metrics.openCount, 1);

  const history = await client.getHistory({ start: "2026-01-11T01:00:00Z", end: "2026-01-11T02:00:00Z", page: 2, perPage: 10 });
  assert.equal(history.page, 2);
  assert.equal(history.perPage, 10);

  const historyRange = await client.getHistoryRange("2026-01-11T01:00:00Z", "2026-01-11T02:00:00Z", { page: 3, perPage: 20 });
  assert.equal(historyRange.page, 3);

  const historyPage = await client.getHistoryPage(4, 15, { minutes: 30 });
  assert.equal(historyPage.page, 4);

  const reset = await client.resetMetrics();
  assert.equal(reset.ok, true);

  const resetPost = await client.resetMetricsPost();
  assert.equal(resetPost.ok, true);

  const created = await client.createAlert({ name: "critical", openThreshold: 2, retryThreshold: 1, windowMinutes: 60, severity: "critical" });
  assert.equal(created.id, "rule-1");

  const createdRule = await client.createAlertRule({ name: "warning", openThreshold: 1, retryThreshold: 0, windowMinutes: 30, severity: "warning" });
  assert.equal(createdRule.id, "rule-2");

  const alerts = await client.getAlerts(25, { severity: "critical", includeAcknowledged: false });
  assert.equal(alerts.events.length, 1);

  const alertsBySeverity = await client.getAlertsBySeverity("critical", 10);
  assert.equal(alertsBySeverity.events.length, 1);

  const updated = await client.updateAlertRule("rule-1", { enabled: false, openThreshold: 3, retryThreshold: 2, windowMinutes: 45 });
  assert.equal(updated.enabled, false);

  const acked = await client.acknowledgeAlert("evt-1");
  assert.equal(acked.acknowledged, true);

  const deleted = await client.deleteAlerts("rule-1");
  assert.equal(deleted.ok, true);

  assert.ok(requests.some((entry) => entry.url.includes("state=open")));
  assert.ok(requests.some((entry) => entry.url.includes("minRetryAttempts=2")));
  assert.ok(requests.some((entry) => entry.url.includes("severity=critical")));
  assert.ok(requests.some((entry) => entry.url.includes("includeAcknowledged=false")));

  const createBody = requests.find((entry) => entry.url.startsWith("/alerts") && entry.method === "POST")?.body;
  assert.ok(createBody);
  assert.ok(createBody?.includes("openThreshold"));

  await new Promise<void>((resolve) => server.close(() => resolve()));
});

test("watch helpers yield sequences", async () => {
  let metricsCount = 0;
  let alertCount = 0;

  const { server, baseUrl } = await startServer((req, res) => {
    const url = new URL(req.url ?? "", "http://localhost");
    res.setHeader("Content-Type", "application/json");

    if (url.pathname === "/metrics/resilience") {
      metricsCount += 1;
      res.writeHead(200);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: new Date().toISOString(), requestId: `req-${metricsCount}` },
          data: { openCount: metricsCount, halfOpenCount: 0, closedCount: 0, retryAttempts: metricsCount }
        })
      );
      return;
    }

    if (url.pathname === "/alerts") {
      alertCount += 1;
      res.writeHead(200);
      res.end(
        JSON.stringify({
          metadata: { version: "1", timestamp: new Date().toISOString(), requestId: `req-alert-${alertCount}` },
          rules: [],
          events: [
            {
              id: `evt-${alertCount}`,
              ruleId: "rule",
              message: "event",
              severity: "warning",
              triggeredAt: new Date().toISOString(),
              openDelta: 1,
              retryDelta: 0,
              acknowledged: false
            }
          ]
        })
      );
      return;
    }

    res.writeHead(404);
    res.end();
  });

  const client = new ResilienceClient(baseUrl);

  const metricsIterator = client.watchMetrics(10);
  const firstMetrics = await metricsIterator.next();
  const secondMetrics = await metricsIterator.next();
  await metricsIterator.return?.();
  assert.equal(firstMetrics.value?.openCount, 1);
  assert.equal(secondMetrics.value?.openCount, 2);

  const csvIterator = client.watchCsv(10);
  const csvHeader = await csvIterator.next();
  const csvRow = await csvIterator.next();
  await csvIterator.return?.();
  assert.ok(csvHeader.value?.startsWith("capturedAt,openCount"));
  assert.ok(csvRow.value?.includes(",1,"));

  const alertIterator = client.watchAlerts(10);
  const firstAlert = await alertIterator.next();
  const secondAlert = await alertIterator.next();
  await alertIterator.return?.();
  assert.equal(firstAlert.value?.[0].id, "evt-1");
  assert.equal(secondAlert.value?.[0].id, "evt-2");

  let observed = 0;
  await Promise.race([
    new Promise<void>((resolve) => {
      const subscription = client.watchAlertsObservable(10).subscribe(() => {
        observed += 1;
        if (observed >= 2) {
          subscription.unsubscribe();
          resolve();
        }
      });
    }),
    new Promise<void>((_resolve, reject) => setTimeout(() => reject(new Error("timeout")), 500))
  ]);

  await new Promise<void>((resolve) => server.close(() => resolve()));
});
