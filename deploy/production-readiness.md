# Production Readiness Checklist

- [ ] Build succeeds (`dotnet build`).
- [ ] Tests pass (`dotnet test`).
- [ ] OpenAPI validation passes (`scripts/validate-openapi.ps1`).
- [ ] Security scan passes (`dotnet list package --vulnerable`).
- [ ] Backups taken (see deploy/backup_restore.md).
- [ ] /healthz and /readyz pass after deploy.
