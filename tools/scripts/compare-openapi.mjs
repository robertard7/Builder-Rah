import fs from "node:fs";
import path from "node:path";

const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");
const openapiTypesPath = path.join(root, "openapi-types.d.ts");
const clientPath = path.join(root, "resilienceClient.ts");

if (!fs.existsSync(openapiTypesPath)) {
  console.error("openapi-types.d.ts missing. Run npm run generate:openapi-types first.");
  process.exit(1);
}

const openapiTypes = fs.readFileSync(openapiTypesPath, "utf8");
const clientSource = fs.readFileSync(clientPath, "utf8");

const requiredSchemas = [
  "ResilienceMetricsResponse",
  "ResilienceHistoryResponse",
  "ResilienceAlertRuleResponse",
  "ResilienceAlertEventResponse",
  "ResilienceAlertsResponse",
  "ResilienceResetResponse",
  "ResilienceDeleteResponse"
];

const missingFromOpenApi = requiredSchemas.filter((schema) => !openapiTypes.includes(schema));
const missingFromClient = requiredSchemas.filter((schema) => !clientSource.includes(schema));

if (missingFromOpenApi.length > 0) {
  console.error(`Missing schemas in openapi-types.d.ts: ${missingFromOpenApi.join(", ")}`);
  process.exit(1);
}

if (missingFromClient.length > 0) {
  console.error(`Missing schema wrappers in resilienceClient.ts: ${missingFromClient.join(", ")}`);
  process.exit(1);
}

console.log("OpenAPI client schema wrappers verified.");
