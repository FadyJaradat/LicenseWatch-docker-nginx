# LicenseWatch

LicenseWatch is a Docker-first ASP.NET Core MVC app for managing license compliance, reporting, and optimization. All development runs inside containers.

## Prerequisites
- Docker Desktop
- VS Code (recommended)

## Run (Docker-first)
```bash
docker compose --profile dev build
docker compose --profile dev up -d
```

URLs:
- http://localhost:8080/
- http://localhost:8080/health
- http://localhost:8080/health/ready
- http://localhost:8080/version
- http://localhost:8080/admin

LAN HTTPS (NGINX reverse proxy):
- HTTPS: https://<host-ip-or-dns>/
- HTTP redirects to HTTPS on port 80
- NGINX health: http://<host-ip-or-dns>/nginx-health

Logs:
```bash
docker compose logs -f --tail=200 licensewatch-web
```

Stop:
```bash
docker compose down
```

## Run (Production-like profile)
```bash
docker compose --profile prod build
docker compose --profile prod up -d
```

## Migrations
Apply App DB migrations from the Admin UI (backup recommended first):
1. Sign in as SystemAdmin.
2. Go to `/admin/migrations`.
3. Review pending migrations and click "Apply App DB migrations".

The legacy `/admin/database` page still shows connection status and migration lists.

## Tests (in Docker)
```bash
docker compose --profile test run --rm licensewatch-test dotnet test LicenseWatch.slnx -c Release
```

## UI/UX Quality Bar
Every UI change must pass the acceptance checklist and follow Codex guardrails:
- `docs/ux/UX_ACCEPTANCE_CHECKLIST.md`
- `docs/ux/CODEX_UX_GUARDRAILS.md`

## Versioning
Versioning is defined in `Directory.Build.props`:
- `VersionPrefix` is the semantic version (e.g., `4.0.0`).
- `InformationalVersion` automatically appends a commit hash when available.
- Build timestamp is embedded as assembly metadata.

Update the version by editing `Directory.Build.props`.

## CI Quick Start
```bash
./ops/ci.sh
```

```powershell
.\ops\ci.ps1
```

## CI Pipeline (GitHub Actions)
The repo includes `.github/workflows/ci.yml`:
- Runs restore/build/test in Release.
- Builds the Docker image.
- Runs a container smoke test on `/health`.

No secrets are required.

## Release Process (Suggested)
1. Update `Directory.Build.props` with the next version.
2. Run `./ops/ci.sh` (or `.\\ops\\ci.ps1`) locally.
3. Build the production image: `docker build -t licensewatch:<version> .`
4. Verify `/version` and `/health` in the container.

## Dependency Security Checks
```bash
dotnet list package --vulnerable
dotnet list package --outdated
```

## Reverse Proxy Notes
LicenseWatch enables forwarded headers for reverse proxies:
- `X-Forwarded-For` and `X-Forwarded-Proto` are honored.
- Configure your proxy to pass these headers.

## Data Storage
All persistent data lives under `/app/App_Data` inside the container (mapped to a Docker volume):
- Identity DB: `/app/App_Data/licensewatch.identity.db`
- Domain DB: `/app/App_Data/licensewatch.app.db`
- Hangfire DB: `/app/App_Data/hangfire.db`
- Uploads: `/app/App_Data/uploads/`
- Imports: `/app/App_Data/imports/tmp/`
- Data Protection keys: `/app/App_Data/keys/`
- Bootstrap settings: `/app/App_Data/bootstrap.json`
- In-app backups: `/app/App_Data/backups/`

## Admin Seed Configuration
Admin user seeding uses these configuration keys (set via environment variables in Docker):
- `Security:SeedAdmin:Email`
- `Security:SeedAdmin:Password`
- `Security:SeedAdmin:Enabled` (set to `false` in production)

Example override:
```bash
Security__SeedAdmin__Email=admin@licensewatch.local
Security__SeedAdmin__Password=ChangeMe!123!
Security__SeedAdmin__Enabled=true
```

Connection string overrides:
```bash
ConnectionStrings__IdentityDb=Data\ Source=/app/App_Data/licensewatch.identity.db
ConnectionStrings__AppDb=Data\ Source=/app/App_Data/licensewatch.app.db
```

## Backups & Restore
Admin backup UI:
- Visit `/admin/maintenance` and click **Backup now** to generate a zip archive.
- Download the archive from the backup list.

CLI backup scripts (recommended for offline recovery):
```bash
./ops/backup.sh
```

Windows PowerShell:
```powershell
.\ops\backup.ps1
```

Script backups are saved under the repo root: `./backups/`.
For the prod profile container, set `LICENSEWATCH_CONTAINER=licensewatch-web-prod`.

Restore (offline):
```bash
./ops/restore.sh ./backups/licensewatch-backup-YYYYMMDD-HHMMSS.tar.gz
```

```powershell
.\ops\restore.ps1 -Archive .\backups\licensewatch-backup-YYYYMMDD-HHMMSS.tar.gz
```
If your App_Data volume name is nonstandard, set `LICENSEWATCH_VOLUME` before running restore.

## Troubleshooting
- **Port 8080 already in use**: Stop the conflicting service or change the host port in `docker-compose.yml`.
- **/health/ready is unhealthy**: Ensure migrations are applied and `/app/App_Data` is writable.
- **Cannot login**: Verify `Security:SeedAdmin:*` environment variables and restart the container.
- **Bootstrap settings not decryptable**: Ensure `/app/App_Data/keys` is persisted across restarts.
- **Tests fail in Docker**: Re-run with `dotnet test -v n` and review output.
