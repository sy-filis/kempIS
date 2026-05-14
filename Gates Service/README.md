# KempISGatesService

A small HTTP API that creates, replaces, and deletes cards in the legacy
Kemp IS gate-system databases. The app is a .NET 10 ASP.NET Core minimal-API
service packaged as a Windows service. It exists so other systems can
manage gate-card state over HTTP without modifying the original gate
application or its on-disk Jet 4.0 / Access (`.mdb`) schemas.

The service is designed to run on the same machine as the gate application
and is bound to loopback by default. It has **no authentication**: anything
that can reach the listen port can mutate the card table. Do not expose
the listen port off-box; if remote access is required, front it with a
reverse proxy that terminates authentication.

## How it works

Two `.mdb` files sit next to each other on disk:

- **Users DB** (`users.mdb` by default) — the `Card` table holds one row per
  gate card. The service writes this table.
- **Events DB** (`events.mdb` by default) — the `Events` table holds an audit
  row for every change. The service appends one row per successful card
  write, plus one row on startup and one on graceful shutdown.

```
HTTP client  ─►  KempISGatesService  ─►  users.mdb   (Card)
                                    └─►  events.mdb  (Events)
```

Each mutating request performs both writes (card + audit event). If the
audit-event write fails after the card write succeeded, the response is
`500` so the caller knows the two databases may be momentarily out of
sync; transient OleDb failures are retried first (see
`Databases.RetryCount`).

Key invariants enforced in code:

- **Atomic upsert.** The legacy "exists-check → hard-delete → insert"
  sequence runs inside a single OleDb transaction in
  [`CardRepository.Upsert`](KempISGatesService/Data/CardRepository.cs).
  A mid-sequence failure rolls back; the row is never silently lost.
- **Per-key serialization.** Concurrent requests targeting the same card
  key are serialized via a `SemaphoreSlim` in
  [`CardService`](KempISGatesService/Services/CardService.cs), closing the
  exists-check / delete TOCTOU window and preserving audit ordering.
- **Transient retries.** OleDb operations are retried up to
  `Databases.RetryCount` times by
  [`OleDbRetry`](KempISGatesService/Infrastructure/OleDbRetry.cs) so brief
  contention with the legacy application (file lock, antivirus scan) does
  not surface as a 500.

## Endpoints

Base URL is controlled by the `Urls` key in `appsettings.json`. All paths
below are relative to that base.
### Date range

The legacy schema stores timestamps as 32-bit signed seconds since
`2000-01-01 00:00:00` *server local time* (see
[`LegacyTime`](KempISGatesService/Infrastructure/LegacyTime.cs)). The
representable window is roughly **2000-01-01** to **2068-01-19**, local
time. Timestamps outside that window are rejected with `400`.

### Interactive API reference

When `ApiDocumentation:Enabled` is `true` in `appsettings.json`, the
service exposes:

- `GET /openapi/v1.json` — the OpenAPI 3.x document.
- `GET /scalar/v1` — a Scalar-powered interactive API browser.

In dev these are reachable at e.g. `http://localhost:5000/scalar/v1`. Both
endpoints are unmapped when the flag is `false`.

## Configuration

Settings live in `appsettings.json` next to the binary. The host anchors
its content root to the binary directory so the file resolves whether the
exe runs interactively or under SCM. `appsettings.json` is gitignored —
deployments use `appsettings.example.json`, which the publish target copies
to `appsettings.json` next to the binary at publish time. Edit the copied
file per deployment.

Top-level keys:

| Key                  | Required | Meaning                                                                                                 |
| -------------------- | -------- | ------------------------------------------------------------------------------------------------------- |
| `Urls`               | yes      | Semicolon-separated Kestrel listen URLs. Bind loopback (`localhost` / `127.0.0.1`); the API has no auth.|
| `AllowedHosts`       | yes      | Semicolon-separated `Host` header allowlist. Defaults to `localhost;127.0.0.1`.                         |
| `ApiDocumentation`   | yes      | Strongly typed; see below.                                                                              |
| `Cors`               | yes      | Strongly typed; see below.                                                                              |
| `Databases`          | yes      | Strongly typed; see below.                                                                              |
| `Serilog`            | yes      | Standard Serilog `IConfiguration`-driven sink and level config.                                         |

`ApiDocumentation`:

| Key       | Type   | Default | Meaning                                                                                |
| --------- | ------ | ------- | -------------------------------------------------------------------------------------- |
| `Enabled` | bool   | `false` | Enables `/openapi/v1.json` and `/scalar/v1`. Leave `false` in production.              |

`Cors` ([`CorsPolicyOptions`](KempISGatesService/Infrastructure/CorsConfiguration.cs)):

| Key                | Type       | Default | Meaning                                                                                  |
| ------------------ | ---------- | ------- | ---------------------------------------------------------------------------------------- |
| `AllowedOrigins`   | string[]   | `[]`    | Use `["*"]` to allow any origin (forces `AllowCredentials = false` automatically).       |
| `AllowedMethods`   | string[]   | `[]`    | Use `["*"]` or omit to allow any method.                                                 |
| `AllowedHeaders`   | string[]   | `[]`    | Use `["*"]` or omit to allow any header.                                                 |
| `ExposedHeaders`   | string[]   | `[]`    | Optional response headers to expose to browser clients.                                  |
| `AllowCredentials` | bool       | `false` | Auto-disabled when origins is `["*"]`.                                                   |

`Databases` ([`DatabaseOptions`](KempISGatesService/Options/DatabaseOptions.cs)):

| Key                     | Type   | Default     | Meaning                                                                                       |
| ----------------------- | ------ | ----------- | --------------------------------------------------------------------------------------------- |
| `DatabaseDirectory`     | string | *(none)*    | Folder containing both `.mdb` files. Required. Trailing slash optional.                       |
| `DatabasePassword`      | string | *(empty)*   | File-level password set on both MDBs (Jet `Database Password`).                               |
| `UsersDatabaseFileName` | string | `users.mdb` | File name inside `DatabaseDirectory`.                                                         |
| `EventsDatabaseFileName`| string | `events.mdb`| File name inside `DatabaseDirectory`.                                                         |
| `EventOperator`         | string | `WEB_API`   | Written to `Events.Operator`; lets you distinguish API writes from the legacy app's writes.   |
| `RetryCount`            | int    | `2`         | Additional attempts on transient `OleDbException`. `0` disables retries.                      |

## Build and deploy

The project targets `net10.0-windows` and pins `x86`. It must be built and
deployed as 32-bit because the `Microsoft.Jet.OLEDB.4.0` provider has no
64-bit version. The csproj is configured for a self-contained single-file
publish.

### Build

From the repo root, in an elevated PowerShell prompt is not required:

```powershell
.\scripts\Build.ps1
```

This wraps `dotnet publish` with the project's standard configuration and
writes output to `<repo>\bin`. Override the destination with
`-OutputPath <path>` (must stay inside the repo root) or the build flavor
with `-Configuration Debug`.

Constraints to be aware of:

- Do **not** pass `-r win-x64` to `dotnet publish`. The csproj already
  pins `win-x86` and the Jet provider will not load under x64.
- The publish step copies `appsettings.example.json` to `appsettings.json`
  in the output directory. Edit that copied file with real values per
  deployment.
- The .NET 10 runtime is bundled (`SelfContained = true`), so the target
  machine does not need a separately installed runtime.

### Install as a Windows service

From an **elevated** PowerShell prompt, after publishing:

```powershell
.\scripts\Install-Service.ps1 -BinPath "C:\KempISGatesService\KempISGatesService.exe"
```

What the script does
([source](scripts/Install-Service.ps1)):

1. Validates `-BinPath` points to an existing `.exe`.
2. Registers the `KempISGatesService` event-log source in *Windows Logs →
   Application* if it does not exist.
3. Stops and removes any prior registration with the same name.
4. Registers the service with `start= auto`.
5. Sets the description shown in `services.msc`.
6. Configures SCM recovery: restart three times with a 60-second delay,
   reset the failure counter after 24 hours.
7. Starts the service and waits up to 10 seconds for it to reach
   `Running`.

Optional overrides:

| Parameter      | Default                                                                  |
| -------------- | ------------------------------------------------------------------------ |
| `-ServiceName` | `KempISGatesService`                                                     |
| `-DisplayName` | `Kemp IS Gates Service`                                                  |
| `-Description` | Service description shown in `services.msc`.                             |

The service runs as `LocalSystem` by default. If the MDBs live on a share,
change the logon account via `services.msc → Log On` to one with read/write
on that share.

### Uninstall

```powershell
.\scripts\Uninstall-Service.ps1
```

The script ([source](scripts/Uninstall-Service.ps1)) stops the service,
deregisters it, and polls for SCM to release the name. If `services.msc`
or Event Viewer is open with the service selected, the entry may linger as
"marked for deletion" until those windows are closed.

### Run interactively (development)

```powershell
dotnet run --project KempISGatesService
```

The local `appsettings.json` (untracked) typically sets
`ApiDocumentation:Enabled = true`, so `/scalar/v1` is browsable. `Ctrl-C`
triggers a clean shutdown and writes a `ProgramEnd` event.

## Logging

The service writes to four places.

**1. Console** — `Information` and above. Visible when run with
`dotnet run` or attached to the SCM console. Errors go to stderr.

**2. `debug.log` (next to the binary)** — `Debug`-level only, rolled at
50 MB, ten files retained. Useful when reproducing issues locally.

**3. `error.log` (next to the binary)** — `Warning` and above, rolled at
50 MB, ten files retained. The authoritative on-disk diagnostic.

**4. Windows Application event log** — `Warning` and above only, under
*Event Viewer → Windows Logs → Application*, source `KempISGatesService`.
Enabled only when the process is running under SCM (so dev `dotnet run`
does not require the event source to exist). The event source is created
by `Install-Service.ps1` at install time.

Sinks 1–3 are configured declaratively in the `Serilog` section of
`appsettings.json`. Sink 4 is added programmatically in
[`Program.cs`](KempISGatesService/Program.cs).
