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
};

export type ResilienceAlertRule = ResilienceAlertRuleRequest & {
  id: string;
  enabled: boolean;
};

export type ResilienceAlertEvent = {
  id: string;
  ruleId: string;
  message: string;
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

  async createAlertRule(rule: ResilienceAlertRuleRequest): Promise<ResilienceAlertRule> {
    return this.sendJson<ResilienceAlertRule>("/alerts/thresholds", {
      method: "POST",
      body: JSON.stringify(rule)
    });
  }

  async getAlerts(limit = 50): Promise<ResilienceAlertsResponse> {
    const query = new URLSearchParams({ limit: String(limit) });
    return this.getJson(`/alerts?${query.toString()}`);
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
