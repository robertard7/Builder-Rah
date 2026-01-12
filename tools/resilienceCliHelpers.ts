import type {
  ResilienceAlertEvent,
  ResilienceAlertRule,
  ResilienceAlertRuleRequest,
  ResilienceAlertRuleUpdate,
  ResilienceHistoryResponse,
  ResilienceMetricsResponse,
  ResilienceSeverity
} from "./resilienceClient";
import { ResilienceClient } from "./resilienceClient";

export type ResilienceHistoryCsvRow = {
  timestamp: string;
  openCount: number;
  halfOpenCount: number;
  closedCount: number;
  retryAttempts: number;
};

export const createResilienceClient = (baseUrl: string, token?: string): ResilienceClient =>
  new ResilienceClient(baseUrl, token);

export const listAlerts = async (
  client: ResilienceClient,
  options?: { severity?: ResilienceSeverity; includeAcknowledged?: boolean; limit?: number }
): Promise<ResilienceAlertEvent[]> => {
  const response = await client.getAlerts(options?.limit ?? 50, {
    severity: options?.severity,
    includeAcknowledged: options?.includeAcknowledged
  });
  return response.events;
};

export const resolveAlert = async (client: ResilienceClient, eventId: string): Promise<ResilienceAlertEvent> =>
  client.acknowledgeAlert(eventId);

export const createAlertRule = async (client: ResilienceClient, rule: ResilienceAlertRuleRequest): Promise<ResilienceAlertRule> =>
  client.createAlert(rule);

export const updateAlertRule = async (
  client: ResilienceClient,
  ruleId: string,
  update: ResilienceAlertRuleUpdate
): Promise<ResilienceAlertRule> => client.updateAlertRule(ruleId, update);

export const deleteAlertRule = async (client: ResilienceClient, ruleId: string): Promise<boolean> => {
  const response = await client.deleteAlerts(ruleId);
  return response.ok;
};

export const getMetricsAndHistory = async (
  client: ResilienceClient,
  options?: { historyMinutes?: number; historyLimit?: number }
): Promise<{ metrics: ResilienceMetricsResponse; history: ResilienceHistoryResponse }> => {
  const [metrics, history] = await Promise.all([
    client.getMetricsResponse(),
    client.getHistory({ minutes: options?.historyMinutes, limit: options?.historyLimit })
  ]);
  return { metrics, history };
};

export const formatHistoryCsv = (history: ResilienceHistoryResponse): string => {
  const header = "timestamp,openCount,halfOpenCount,closedCount,retryAttempts";
  const rows = history.items.map(
    (item) =>
      `${item.timestamp},${item.metrics.openCount},${item.metrics.halfOpenCount},${item.metrics.closedCount},${item.metrics.retryAttempts}`
  );
  return [header, ...rows].join("\n");
};

export const mapHistoryCsvRows = (history: ResilienceHistoryResponse): ResilienceHistoryCsvRow[] =>
  history.items.map((item) => ({
    timestamp: item.timestamp,
    openCount: item.metrics.openCount,
    halfOpenCount: item.metrics.halfOpenCount,
    closedCount: item.metrics.closedCount,
    retryAttempts: item.metrics.retryAttempts
  }));
