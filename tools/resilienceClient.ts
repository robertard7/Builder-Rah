export type CircuitMetricsSnapshot = {
  openCount: number;
  halfOpenCount: number;
  closedCount: number;
  retryAttempts: number;
};

export type ResilienceState = "open" | "halfopen" | "closed";

export type ResilienceSeverity = "warning" | "critical";

export type ResilienceMetricsByTool = Record<string, CircuitMetricsSnapshot>;

export type ResilienceMetricsSample = {
  timestamp: string;
  metrics: CircuitMetricsSnapshot;
};

export type ResilienceHistoryResponse = {
  total: number;
  page: number;
  perPage: number;
  items: ResilienceMetricsSample[];
};

export type ResilienceAlertRuleRequest = {
  name?: string;
  openThreshold: number;
  retryThreshold: number;
  windowMinutes: number;
  severity?: ResilienceSeverity;
};

export type ResilienceAlertRuleUpdate = {
  name?: string;
  openThreshold?: number;
  retryThreshold?: number;
  windowMinutes?: number;
  severity?: ResilienceSeverity;
  enabled?: boolean;
};

export type ResilienceAlertRule = ResilienceAlertRuleRequest & {
  id: string;
  severity: ResilienceSeverity;
  enabled: boolean;
};

export type ResilienceAlertRuleSummary = ResilienceAlertRule & {
  recentEvents: ResilienceAlertEvent[];
};

export type ResilienceAlertEvent = {
  id: string;
  ruleId: string;
  message: string;
  severity: ResilienceSeverity;
  triggeredAt: string;
  openDelta: number;
  retryDelta: number;
  acknowledged: boolean;
  acknowledgedAt?: string;
};

export type ResilienceAlertsResponse = {
  rules: ResilienceAlertRuleSummary[];
  events: ResilienceAlertEvent[];
};

export type ResilienceResetResponse = { ok: boolean; resetAt: string };

export type AlertDeleteResponse = { ok: boolean; ruleId?: string };

export class ResilienceClient {
  constructor(private readonly baseUrl: string, private readonly token?: string) {}

  async getMetrics(params?: { state?: ResilienceState; minRetryAttempts?: number }): Promise<CircuitMetricsSnapshot> {
    const query = new URLSearchParams();
    if (params?.state) query.set("state", params.state);
    if (params?.minRetryAttempts !== undefined) query.set("minRetryAttempts", String(params.minRetryAttempts));
    return this.getJson(`/metrics/resilience${query.toString() ? `?${query}` : ""}`);
  }

  async getHistory(options?: {
    minutes?: number;
    limit?: number;
    start?: string;
    end?: string;
    bucketMinutes?: number;
    page?: number;
    perPage?: number;
  }): Promise<ResilienceHistoryResponse> {
    const query = new URLSearchParams();
    if (options?.minutes !== undefined) query.set("minutes", String(options.minutes));
    if (options?.limit !== undefined) query.set("limit", String(options.limit));
    if (options?.start) query.set("start", options.start);
    if (options?.end) query.set("end", options.end);
    if (options?.bucketMinutes !== undefined) query.set("bucketMinutes", String(options.bucketMinutes));
    if (options?.page !== undefined) query.set("page", String(options.page));
    if (options?.perPage !== undefined) query.set("perPage", String(options.perPage));
    return this.getJson(`/metrics/resilience/history?${query.toString()}`);
  }

  async getHistoryRange(start: string, end: string, options?: { limit?: number; bucketMinutes?: number; page?: number; perPage?: number }): Promise<ResilienceHistoryResponse> {
    return this.getHistory({ start, end, ...options });
  }

  async resetMetrics(): Promise<ResilienceResetResponse> {
    return this.sendJson<ResilienceResetResponse>("/metrics/resilience/reset", { method: "PUT" });
  }

  async resetMetricsPost(): Promise<ResilienceResetResponse> {
    return this.sendJson<ResilienceResetResponse>("/metrics/resilience/reset", { method: "POST" });
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

  async getAlerts(limit = 50, options?: { severity?: ResilienceSeverity; includeAcknowledged?: boolean }): Promise<ResilienceAlertsResponse> {
    const query = new URLSearchParams({ limit: String(limit) });
    if (options?.severity) query.set("severity", options.severity);
    if (options?.includeAcknowledged !== undefined) query.set("includeAcknowledged", String(options.includeAcknowledged));
    return this.getJson(`/alerts?${query.toString()}`);
  }

  async getAlertsBySeverity(severity: ResilienceSeverity, limit = 50): Promise<ResilienceAlertsResponse> {
    return this.getAlerts(limit, { severity });
  }

  async deleteAlerts(ruleId?: string): Promise<AlertDeleteResponse> {
    const query = new URLSearchParams();
    if (ruleId) query.set("ruleId", ruleId);
    return this.sendJson<AlertDeleteResponse>(`/alerts${query.toString() ? `?${query}` : ""}`, {
      method: "DELETE"
    });
  }

  async updateAlertRule(ruleId: string, update: ResilienceAlertRuleUpdate): Promise<ResilienceAlertRule> {
    return this.sendJson<ResilienceAlertRule>(`/alerts/${ruleId}`, {
      method: "PATCH",
      body: JSON.stringify(update)
    });
  }

  async acknowledgeAlert(eventId: string): Promise<ResilienceAlertEvent> {
    return this.sendJson<ResilienceAlertEvent>(`/alerts/events/${eventId}`, {
      method: "PATCH",
      body: JSON.stringify({ acknowledged: true })
    });
  }

  async *watchAlerts(intervalMs = 5000, options?: { severity?: ResilienceSeverity; includeAcknowledged?: boolean }): AsyncGenerator<ResilienceAlertEvent[]> {
    while (true) {
      const response = await this.getAlerts(50, options);
      yield response.events;
      await new Promise((resolve) => setTimeout(resolve, intervalMs));
    }
  }

  watchAlertsObservable(intervalMs = 5000, options?: { severity?: ResilienceSeverity; includeAcknowledged?: boolean }): Observable<ResilienceAlertEvent[]> {
    return createObservable(async (next, stop) => {
      for await (const events of this.watchAlerts(intervalMs, options)) {
        if (stop.aborted) return;
        next(events);
      }
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
