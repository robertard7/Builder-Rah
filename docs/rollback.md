# Rollback Plan

1. Stop the running service or job.
2. Restore the previous build artifact.
3. Restore session store backups (see deploy/backup_restore.md).
4. Restart and verify /healthz and /readyz endpoints.
