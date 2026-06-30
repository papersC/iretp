# Deployment Guide

How to pull this repo and run it so every page works — and what to change for a
real (internet-facing) deployment.

---

## 1. Quick start — populated demo (recommended)

Requires Docker. From the repo root:

```bash
git pull
docker compose up -d --build
```

Then open **http://localhost:5010** (or `http://<server-ip>:5010` on a remote host).

| Service | URL | Notes |
|---|---|---|
| Web portal (Blazor) | http://localhost:5010 | the app |
| Public WebAPI | http://localhost:5000 | Open Data API + Swagger at `/api-docs` |
| Admin API | http://localhost:5002 | internal, policy-gated |
| SQL Server | localhost,1433 | `sa` / password in `docker-compose.yml` |

`docker compose` runs in **Development** mode, so on first boot WebAPI:
- auto-applies all EF Core migrations (creates the schema), and
- seeds demo data → **every public page is populated**.

Admin login: `admin@dld.gov.ae` / `Admin@DLD2026!`

Stop with `docker compose down` (add `-v` to also drop the database volume).

---

## 2. How settings differ between environments

This is the part to get right. Behaviour is driven by `ASPNETCORE_ENVIRONMENT`:

| | `Development` (what `docker compose` sets) | `Production` (what the Docker images default to) |
|---|---|---|
| **Demo data seeding** | ✅ seeds on startup → pages populated | ❌ no seeding → pages empty until real DLD data is loaded |
| **Schema migration** | ✅ runs automatically | ✅ runs automatically (every env) |
| **Internal-staff MFA** | off (password-only, for demos) | enforced (RFP 10.2.1) |
| **Swagger UI** | full surface | Open Data doc only |
| **AI Agent (Azure OpenAI)** | endpoint config present (`appsettings.Development.json`); needs the API key to be live | no AI config in base settings → uses deterministic fallback |

> If your friend wants the **populated demo**, keep `ASPNETCORE_ENVIRONMENT=Development`
> (the default in `docker-compose.yml`). A `Production` deploy starts with an
> empty database by design — real rows come from DLD source systems.

---

## 3. Production / internet-facing deploy — required overrides

`appsettings.json` ships with **placeholders only**. For any non-localhost deploy,
set these as **environment variables** on the host (nesting uses `__`). They override
the file without editing it:

| Env var | Set on | Why |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | WebAPI + AdminAPI | The committed default (`.\SQLEXPRESS;Trusted_Connection=True`) is Windows/SQL-Express only. Point both APIs at the real SQL Server. |
| `Jwt__Key` | WebAPI + AdminAPI | **Must be byte-for-byte identical on both** — WebAPI issues tokens, AdminAPI validates them. 32+ chars. The committed value is a shared placeholder; replace it for anything reachable from the internet. |
| `AI__primary__ApiKey` | WebAPI | *Optional.* Enables the Azure OpenAI agent (`mootori-openai` / `gpt-4o`). Without it the AI Agent still works via a deterministic fallback. |
| `ASPNETCORE_ENVIRONMENT` | all | `Development` for the seeded demo; `Production` for a clean DB. |

Example (Linux/host env, or `docker compose` override file):

```bash
export ConnectionStrings__DefaultConnection="Server=db.example;Database=IRETP;User Id=iretp_app;Password=...;TrustServerCertificate=True"
export Jwt__Key="<32+ char random secret, same on both APIs>"
export AI__primary__ApiKey="<azure-openai-key>"   # optional
```

---

## 4. Notes & gotchas

- **Live notifications (SignalR):** the browser connects directly to the WebAPI
  hub. On a remote deploy where the browser can't reach the WebAPI URL, push
  notifications **silently fall back to polling** — no error. To get live push,
  expose the WebAPI publicly and set `ApiSettings__WebApiUrl` on the Web service
  to a browser-reachable URL.
- **Reverse proxy:** if you front the Web app with nginx/IIS, proxy port 5010 and
  **enable WebSocket upgrade** — Blazor Server needs a persistent socket.
- **Health checks:** `GET /healthz/live` (liveness) and `GET /healthz/ready`
  (DB + jobs) on the APIs — handy for load-balancer probes.
- **First boot:** AdminAPI may briefly return fallback data until WebAPI finishes
  the initial migration; it self-heals within seconds.
