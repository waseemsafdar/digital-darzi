# MCP Servers for DigitalDarziApi

Claude Code auto-loads [`.mcp.json`](.mcp.json) from the repo root. It wires up four
servers tailored to this stack (.NET 8 Web API + Neon PostgreSQL). All secrets are
referenced as **environment variables** — nothing sensitive lives in the committed file.

| Server        | Purpose                                                        | Needs |
|---------------|---------------------------------------------------------------|-------|
| `neon`        | Manage the Neon project, create DB branches, run SQL, schema  | `NEON_API_KEY` |
| `postgres`    | Read-only schema browsing + queries against the live DB       | `DATABASE_URL` |
| `context7`    | Up-to-date docs for EF Core / ASP.NET Core / AutoMapper etc.  | — (none) |
| `github`      | Issues / PRs / Actions / code search (hosted GitHub MCP)      | `GITHUB_PAT` |

## One-time setup — set the environment variables

Run in **PowerShell** (persists for your Windows user):

```powershell
# Neon API key — create at https://console.neon.tech  → Account settings → API keys
[Environment]::SetEnvironmentVariable("NEON_API_KEY", "napi_xxxxxxxx", "User")

# Postgres URL — converted from ConnectionStrings:DefaultConnection in appsettings.json
[Environment]::SetEnvironmentVariable("DATABASE_URL", "postgresql://neondb_owner:npg_Zq0Tmk6ditnI@ep-winter-night-ai0b0p8i-pooler.c-4.us-east-1.aws.neon.tech:5432/neondb?sslmode=require", "User")

# GitHub PAT — create at https://github.com/settings/tokens (repo + read:org scopes)
[Environment]::SetEnvironmentVariable("GITHUB_PAT", "ghp_xxxxxxxx", "User")
```

Then **restart Claude Code / VS Code** so it picks up the new env vars, and approve the
servers when prompted (project MCP servers require a one-time trust confirmation).

## Verify

- `/mcp` in Claude Code lists the servers and their connection status.
- Ask: "list the tables in the database" (exercises `postgres`/`neon`).
- Ask: "look up the latest EF Core `HasQueryFilter` docs" (exercises `context7`).

## Security note ⚠️

`API/appsettings.json` currently commits the **Neon DB password in plaintext**.
Recommend moving it to user-secrets or an env var:

```powershell
cd API
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>"
```

## Skills (already available — nothing to install)

Claude Code ships these skills in-session; just invoke them:
`/code-review`, `/security-review`, `/verify`, `/run`, `/simplify`, `/deep-research`.
For this API, `/code-review` (before commits) and `/security-review` (auth/JWT/SQL
surface) are the most relevant.
