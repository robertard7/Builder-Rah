# Builder-Rah Resilience Client

The TypeScript client wraps the resilience endpoints exposed by the headless API.

## Install

```bash
npm install @builder-rah/resilience-client
```

## Usage

```ts
import { ResilienceClient } from "@builder-rah/resilience-client";

const client = new ResilienceClient("http://localhost:5050");
const metrics = await client.getMetrics();
console.log(metrics);
```

## Scripts

- `npm run build` compiles the client into `dist/`.
- `npm run test` runs the unit/integration tests via `node --test`.
- `npm run generate:openapi-types` regenerates OpenAPI types for contract checks.
