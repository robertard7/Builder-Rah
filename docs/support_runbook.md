# Support Runbook

## Common checks
- Verify service health: `GET /healthz` and `GET /readyz`.
- Check provider status: `GET /provider/metrics` and `/provider/events`.
- Verify session status: `GET /sessions/{id}/status`.

## Common fixes
- Provider offline: retry and confirm provider settings.
- Stuck session: cancel with `POST /sessions/{id}/cancel`.
- Corrupt session: delete and recreate session.
