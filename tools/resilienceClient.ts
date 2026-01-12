export type CircuitMetricsSnapshot = {
  openCount: number;
  halfOpenCount: number;
  closedCount: number;
  retryAttempts: number;
};

export type ResilienceMetricsSample = {
  timestamp: string;
  metrics: CircuitMetricsSnapshot;
};

export type ResilienceAlertRuleRequest = {
  name?: string;
  openThreshold: number;
  retryThreshold: number;
  windowMinutes: number;
  severity?: "warning" | "critical";
};

export type ResilienceAlertRule = ResilienceAlertRuleRequest & {
  id: string;
  severity: "warning" | "critical";
  enabled: boolean;
};

export type ResilienceAlertEvent = {
  id: string;
  ruleId: string;
  message: string;
  severity: "warning" | "critical";
  triggeredAt: string;
  openDelta: number;
  retryDelta: number;
};

export type ResilienceAlertsResponse = {
  rules: ResilienceAlertRule[];
  events: ResilienceAlertEvent[];
};

export type ApiOkResponse = { ok: boolean; resetAt: string };

export class ResilienceClient {
  constructor(private readonly baseUrl: string, private readonly token?: string) {}

  async getMetrics(params?: { state?: string; minRetryAttempts?: number }): Promise<CircuitMetricsSnapshot> {
    const query = new URLSearchParams();
    if (params?.state) query.set("state", params.state);
    if (params?.minRetryAttempts !== undefined) query.set("minRetryAttempts", String(params.minRetryAttempts));
    return this.getJson(`/metrics/resilience${query.toString() ? `?${query}` : ""}`);
  }

  async getHistory(minutes = 60, limit = 300): Promise<ResilienceMetricsSample[]> {
    const query = new URLSearchParams({ minutes: String(minutes), limit: String(limit) });
    return this.getJson(`/metrics/resilience/history?${query.toString()}`);
  }

  async resetMetrics(): Promise<ApiOkResponse> {
    return this.sendJson<ApiOkResponse>("/metrics/resilience/reset", { method: "PUT" });
  }

  async resetMetricsPost(): Promise<ApiOkResponse> {
    return this.sendJson<ApiOkResponse>("/metrics/resilience/reset", { method: "POST" });
  }

  async createAlertRule(rule: ResilienceAlertRuleRequest): Promise<ResilienceAlertRule> {
    return this.sendJson<ResilienceAlertRule>("/alerts/thresholds", {
      method: "POST",
      body: JSON.stringify(rule)
    });
  }

  async createAlert(rule: ResilienceAlertRuleRequest): Promise<ResilienceAlertRule> {
    return this.sendJson<ResilienceAlertRule>("/alerts", {
      method: "POST",
      body: JSON.stringify(rule)
    });
  }

  async getAlerts(limit = 50): Promise<ResilienceAlertsResponse> {
    const query = new URLSearchParams({ limit: String(limit) });
    return this.getJson(`/alerts?${query.toString()}`);
  }

  async deleteAlerts(ruleId?: string): Promise<ApiOkResponse> {
    const query = new URLSearchParams();
    if (ruleId) query.set("ruleId", ruleId);
    return this.sendJson<ApiOkResponse>(`/alerts${query.toString() ? `?${query}` : ""}`, {
      method: "DELETE"
    });
  }

  async *watchMetrics(intervalMs = 2000): AsyncGenerator<CircuitMetricsSnapshot> {
    while (true) {
      yield await this.getMetrics();
      await new Promise((resolve) => setTimeout(resolve, intervalMs));
    }
  }

  async *watchCsv(intervalMs = 2000): AsyncGenerator<string> {
    yield "capturedAt,openCount,halfOpenCount,closedCount,retryAttempts";
    for await (const metrics of this.watchMetrics(intervalMs)) {
      const capturedAt = new Date().toISOString();
      yield `${capturedAt},${metrics.openCount},${metrics.halfOpenCount},${metrics.closedCount},${metrics.retryAttempts}`;
    }
  }

  watchMetricsObservable(intervalMs = 2000): Observable<CircuitMetricsSnapshot> {
    return createObservable(async (next, stop) => {
      for await (const metrics of this.watchMetrics(intervalMs)) {
        if (stop.aborted) return;
        next(metrics);
      }
    });
  }

  private async getJson<T>(path: string): Promise<T> {
    const res = await fetch(this.baseUrl + path, { headers: this.headers() });
    if (!res.ok) throw new Error(`Request failed: ${res.status}`);
    return res.json() as Promise<T>;
  }

  private async sendJson<T>(path: string, init: RequestInit): Promise<T> {
    const res = await fetch(this.baseUrl + path, {
      ...init,
      headers: {
        ...this.headers(),
        "Content-Type": "application/json"
      }
    });
    if (!res.ok) throw new Error(`Request failed: ${res.status}`);
    return res.json() as Promise<T>;
  }

  private headers(): HeadersInit {
    return this.token ? { "X-Builder-Token": this.token } : {};
  }
}

export type Observable<T> = {
  subscribe: (next: (value: T) => void) => { unsubscribe: () => void };
};

const createObservable = <T>(producer: (next: (value: T) => void, stop: AbortController) => Promise<void>): Observable<T> => ({
  subscribe: (next) => {
    const stop = new AbortController();
    void producer(next, stop);
    return { unsubscribe: () => stop.abort() };
  }
});
