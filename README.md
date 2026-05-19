# Information system for accomodation in tourist camps
- Author: Filip Zukal
- Accademic year: 2025/2026


# Deployment

Docker Compose stack for KempIS: backend, frontend, Postgres, Redis, optional
Seq, and the LAN + WAN Caddy proxies.

## Setup

```powershell
Copy-Item .env.example .env
Copy-Item secrets\appsettings.Production.json.example secrets\appsettings.Production.json
notepad .env
notepad secrets\appsettings.Production.json
```

Drop `secrets\edoklady.pfx` in place if eDoklady is in use; leave the file
empty otherwise (the bind mount needs the path to exist).

The Cloudflare API token in `.env` must have `Zone:DNS:Edit` on the zones
covering `CADDY_LAN_DOMAIN` and `CADDY_WAN_DOMAIN`. Both proxies use it to
obtain Let's Encrypt certs via DNS-01.

### Configuring the backend

Backend settings live in two layers.

1. **Image defaults** (`KempIS Backend/src/Web.Api/appsettings.json`) — checked
   into the source tree, baked into the `web-api` image at build time. Change
   anything here and rebuild the image (`docker compose build web-api`).
2. **Production overrides** (`_deployment/secrets/appsettings.Production.json`)
   — bind-mounted read-only at `/app/appsettings.Production.json` by
   `compose.prod.yaml`. The example file at
   `secrets/appsettings.Production.json.example` documents every section.
   `ValidateOnStart` rejects startup if the `Camp`, `Retention`, or `Frontend`
   sections are blank.

Keys you'll touch most often:

| Section                                | Purpose                                                                                  |
| -------------------------------------- | ---------------------------------------------------------------------------------------- |
| `ConnectionStrings.Database` / `Redis` | Postgres + Redis endpoints (defaults point at the in-stack containers).                  |
| `Camp`                                 | Camp identity printed on bills, invoices, and emails.                                    |
| `Frontend.BaseUrl`                     | Public URL the SPA is served from; embedded in outgoing email links.                     |
| `Cors.AllowedOrigins`                  | Origins permitted to call the API from a browser.                                        |
| `Identity.Passkeys`                    | WebAuthn relying-party domain and allowed origins.                                       |
| `Smtp`                                 | Outgoing mail server (host/port/credentials/from address).                               |
| `Ubyport`                              | Czech police foreigner reporting credentials (leave blank to disable).                   |
| `EDoklady`                             | Czech eDoklady endpoint + PFX path; uses the secret mounted from `secrets/edoklady.pfx`. |
| `Mapy.ApiKey`                          | Mapy.cz geocoding API key.                                                               |
| `Reception`                            | Tablet pairing / Socket.IO tuning.                                                       |

Any value can also be set as an environment variable using
`Section__Key=value` (double underscore) — see `compose.dev.yaml` for
examples (`Frontend__BaseUrl`, `ConnectionStrings__Database`).

Secrets you do **not** want in `appsettings.Production.json`:

- `secrets/appsettings.Production.json` — passwords, API keys.
- `secrets/edoklady.pfx` — eDoklady client certificate.
- `.env` — Cloudflare token, Postgres password, Cloudflare Tunnel token.

All three live under `_deployment/secrets/` or `_deployment/.env` and are
gitignored.

### Configuring the frontend

The Angular SPA is a static bundle; configuration is compiled in. Edit
`KempIS Frontend/src/environments/environment.ts` before building the
production image:

```ts
export const environment: Environment = {
  apiBaseUrl: "https://api.olsovec.cz", // public API URL the SPA calls
  camp: {
    name: "Kemp Olšovec",
    phoneDisplay: "+420 ...",
    phoneTel: "+420...",                // tel: href, digits + leading +
    email: "rezervace@...",
    address: { street: "...", city: "...", zip: "..." },
  },
  skipAuth: false,                      // true only for first-run setup
};
```

`environment.development.ts` is the equivalent file used by `ng serve` /
`compose.dev.yaml`. The shape is enforced by `environment.type.ts`.

After editing, rebuild the image:

```powershell
docker compose -f compose.yaml -f compose.prod.yaml build frontend
```

The image bundles **both** language builds: Czech is served at `/` and
English at `/en/`. Caddy routing and cache headers live in
`KempIS Frontend/Caddyfile` (inside the frontend image, not the LAN/WAN proxy).

## Run

```powershell
# Dev (no proxies; FE via `npm start`)
docker compose -f compose.yaml -f compose.dev.yaml up

# Prod, LAN only
docker compose -f compose.yaml -f compose.prod.yaml --profile proxy up -d

# Prod, LAN + WAN on host ports
docker compose -f compose.yaml -f compose.prod.yaml -f compose.publication.host.yaml --profile proxy up -d

# Prod, LAN + WAN via Cloudflare Tunnel
docker compose -f compose.yaml -f compose.prod.yaml -f compose.publication.cloudflared.yaml --profile proxy up -d
```

Append `--profile seq` to any line above to start the Seq sidecar (UI on :8081
in dev).

## Mail responses (email templates)

Outbound mail bodies live in the backend source tree at:

```
KempIS Backend/src/Web.Api/EmailTemplates/<template-name>/<language>.txt
```

Existing templates:

- `reservation-confirmation/` — sent when a reservation is confirmed.
- `group-reservation-invitation/` — sent to each guest of a group booking.

File format: subject on the first line, a line with three dashes (`---`),
then the body. Placeholders use `{{Name}}` and are substituted by the
sender (e.g. `{{Number}}`, `{{From}}`, `{{To}}`, `{{GuestLink}}`).

```
Your reservation {{Number}} is confirmed
---
Hello,

your reservation has been confirmed.

Reservation number: {{Number}}
...
```

Each template ships with `cs.txt` and `en.txt`. The renderer falls back to
the default language (`EmailTemplates:DefaultLanguage`, default `en`) if a
locale file is missing.

To edit: change the `.txt` file under `KempIS Backend/src/Web.Api/EmailTemplates/`
and rebuild the `web-api` image (`docker compose build web-api`). The files
are `CopyToOutputDirectory=PreserveNewest` content, so a backend rebuild
picks them up — no code changes needed.

SMTP credentials and from-address are in `appsettings.Production.json` →
`Smtp` (see backend section above).

## PDF templates

PDFs are rendered server-side with Razor + Playwright (Chromium). Templates
live in the Infrastructure project:

```
KempIS Backend/src/Infrastructure/Documents/
├── Bills/
│   ├── Templates/
│   │   ├── Bill.cshtml          ← full bill / invoice / repair bill
│   │   └── BillSticker.cshtml   ← sticker placed on tent
│   └── Resources/
│       ├── BillResources.resx       (en, fallback)
│       └── BillResources.cs.resx    (cs)
└── FinancialClosings/
    └── Templates/
        └── FinancialClosingReport.cshtml
```

Each `.cshtml` is a standalone HTML document with inline `<style>`.
Localized strings come from the `.resx` siblings — add new keys in both
`.resx` files and reference them as `@Model.L["Key_Name"]` in the Razor
file.

To edit:

1. Modify the `.cshtml` (layout / CSS) and/or the `.resx` (text).
2. Rebuild the backend image: `docker compose build web-api`.
3. Trigger a regeneration in the app (bills re-render on demand).

Rendering pipeline: Razor → HTML string → Playwright `page.SetContentAsync`
→ `page.PdfAsync`. The Web.Api Dockerfile installs the Chromium runtime
deps; no separate browser image is needed.

## Routing

LAN serves the SPA and the full API; WAN exposes only the public-booking
endpoints below. The backend mounts every endpoint under `/api`
(`app.MapEndpoints(app.MapGroup("api"))` in `Program.cs`), so both proxies
forward paths verbatim — no prefix stripping or rewriting. Health checks
(`/health`) are mounted at the root and handled by a separate Caddy block.

| Method | WAN path                                |
| ------ | --------------------------------------- |
| GET    | `/api/availability`                     |
| GET    | `/api/nationalities`                    |
| GET    | `/api/reservations/{id}/guest`          |
| POST   | `/api/reservations/web`                 |
| POST   | `/api/reservations/{id}/guest/cancel`   |
| POST   | `/api/reservations/{id}/guest/check-in` |

Anything else through the WAN proxy returns 403. To add an endpoint, edit
`_deployment/caddy/wan/Caddyfile` and restart `caddy-wan`.

## Iframe embedding (WAN only)

LAN never serves framed content (`X-Frame-Options: DENY`, CSP
`frame-ancestors 'none'`). To allow a parent site to embed WAN content, set
both lines in `.env`:

```
WAN_FRAME_ANCESTORS='self' https://www.example.com
WAN_CORP=cross-origin
```

`frame-ancestors` controls which parents may embed; `Cross-Origin-Resource-Policy`
controls whether the browser hands the response to a cross-origin embedder.
They have to move together.

## Notes

- HTTP→HTTPS redirect is automatic on both proxies (Caddy stands up a 308
  listener on `:80` because the site addresses declare no scheme).
- Single-NIC host: if LAN and WAN both want `:80`/`:443`, either give them
  different bind addresses or non-standard LAN ports, or use the Cloudflare
  Tunnel overlay for WAN (no host ports needed).
- Backups: `docker exec kempis-postgres pg_dump -U $POSTGRES_USER $POSTGRES_DB`
  is the simplest option.
