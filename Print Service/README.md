# LocalPrintServerService

A small HTTP API that bridges remote callers to printers installed on a
Windows host. The app is a .NET 10 ASP.NET Core minimal-API service packaged
as a Windows service. It exists so other systems can drive Windows printers
over HTTP without depending on platform-specific printing APIs in the client.

The service is designed to run on the same machine as the target printers
and is bound to loopback by default. It has **no authentication**: anything
that can reach the listen port can submit print jobs. Do not expose the
listen port off-box; if remote access is required, front it with a reverse
proxy that terminates authentication.

## How it works

PDFs are received over HTTP and handed to a bundled
[SumatraPDF](https://www.sumatrapdfreader.org/) executable, which renders
them to the named Windows printer via its `-silent -print-to` mode. The
Windows print subsystem is queried directly (`PrinterSettings.InstalledPrinters`)
to enumerate the available printers.

```
HTTP client  ─►  LocalPrintServerService  ─►  SumatraPDF.exe  ─►  Windows spooler
```

Each `POST` writes the request body to a temporary `.pdf` file, invokes
SumatraPDF as a child process, drains its stdout/stderr, and deletes the
temp file. The response is returned once SumatraPDF exits — i.e. when the
job has been handed off to the Windows spooler, not when the page
physically prints.

Key invariants enforced in code:

- **Magic-byte gate.** Bodies are checked for the `%PDF-` prefix in
  [`PrintersEndpoints.LooksLikePdf`](LocalPrintServerService/PrintersEndpoints.cs)
  so obvious non-PDFs (JSON, HTML, binary garbage) never spawn a SumatraPDF
  process.
- **Bounded child lifetime.** The SumatraPDF process is killed after a
  30-second timeout in
  [`WindowsPrinterSpooler.PrintPdf`](LocalPrintServerService/WindowsPrinterSpooler.cs),
  with both pipes drained on background threads to avoid the
  `ReadToEnd`/`WaitForExit` deadlock.
- **Case-insensitive printer match.** Printer names are compared
  ordinal-case-insensitively against `PrinterSettings.InstalledPrinters` so
  callers do not have to match Windows' exact display casing.
- **Body size cap.** Kestrel rejects request bodies larger than 20 MB so a
  malicious or buggy client cannot exhaust memory.

## Endpoints

Base URL is controlled by the `Urls` key in `appsettings.json`. All paths
below are relative to that base.

| Method | Path                            | Body                                                         | Success | Errors            |
| ------ | ------------------------------- | ------------------------------------------------------------ | ------- | ----------------- |
| `GET`  | `/api/v1/printers`              | —                                                            | `200`   | —                 |
| `POST` | `/api/v1/printers/{printerName}`| `application/pdf` raw, or `multipart/form-data` with a file  | `204`   | `400`/`404`/`415`/`500` |

### Request body shapes

`POST /api/v1/printers/{printerName}` accepts two content types:

- `application/pdf` — raw PDF as the request body.
  ```
  curl --data-binary @file.pdf -H "Content-Type: application/pdf" \
       http://127.0.0.1:9000/api/v1/printers/MyPrinter
  ```
- `multipart/form-data` — PDF as the first file part (the shape browsers
  and Swagger UI / Scalar produce).

### Error responses

Errors return a JSON body of shape `{ "error": "...", "detail": "..." }`
where `detail` is omitted when null.

| Status | When                                                                                 |
| ------ | ------------------------------------------------------------------------------------ |
| `400`  | Multipart request with no file part, or body that is not a PDF.                      |
| `404`  | `printerName` does not match any installed printer.                                  |
| `415`  | `Content-Type` is neither `application/pdf` nor `multipart/form-data`.               |
| `500`  | SumatraPDF exited non-zero, timed out, or is missing on disk.                        |

### Interactive API reference

When `ApiDocumentation:Enabled` is `true` in `appsettings.json`, the
service exposes:

- `GET /openapi/v1.json` — the OpenAPI 3.x document.
- `GET /scalar/v1` — a Scalar-powered interactive API browser.

In dev these are reachable at e.g. `http://127.0.0.1:9000/scalar/v1`. Both
endpoints are unmapped when the flag is `false`.

## Configuration

Settings live in `appsettings.json` next to the binary. The host anchors
its content root to the binary directory so the file resolves whether the
exe runs interactively or under SCM. `appsettings.json` is gitignored —
deployments use `appsettings.example.json`, which the publish target copies
to `appsettings.json` next to the binary at publish time. Edit the copied
file per deployment.

Top-level keys:

| Key                | Required | Meaning                                                                                                  |
| ------------------ | -------- | -------------------------------------------------------------------------------------------------------- |
| `Urls`             | yes      | Semicolon-separated Kestrel listen URLs. Bind loopback (`127.0.0.1`); the API has no auth.               |
| `AllowedHosts`     | yes      | Semicolon-separated `Host` header allowlist. Defaults to `localhost;127.0.0.1`.                          |
| `ApiDocumentation` | yes      | Strongly typed; see below.                                                                               |
| `Cors`             | yes      | Strongly typed; see below.                                                                               |
| `Sumatra`          | no       | Optional override for the SumatraPDF executable path; see below.                                         |
| `Serilog`          | yes      | Standard Serilog `IConfiguration`-driven sink and level config.                                          |

`ApiDocumentation`:

| Key       | Type | Default | Meaning                                                                   |
| --------- | ---- | ------- | ------------------------------------------------------------------------- |
| `Enabled` | bool | `false` | Enables `/openapi/v1.json` and `/scalar/v1`. Leave `false` in production. |

`Cors` ([`CorsPolicyOptions`](LocalPrintServerService/CorsConfiguration.cs)):

| Key                | Type     | Default | Meaning                                                                            |
| ------------------ | -------- | ------- | ---------------------------------------------------------------------------------- |
| `AllowedOrigins`   | string[] | `[]`    | Use `["*"]` to allow any origin (forces `AllowCredentials = false` automatically). |
| `AllowedMethods`   | string[] | `[]`    | Use `["*"]` or omit to allow any method.                                           |
| `AllowedHeaders`   | string[] | `[]`    | Use `["*"]` or omit to allow any header.                                           |
| `ExposedHeaders`   | string[] | `[]`    | Optional response headers to expose to browser clients.                            |
| `AllowCredentials` | bool     | `false` | Auto-disabled when origins is `["*"]`.                                             |

`Sumatra`:

| Key    | Type   | Default                                | Meaning                                                                                       |
| ------ | ------ | -------------------------------------- | --------------------------------------------------------------------------------------------- |
| `Path` | string | `<bin>\SumatraPDF.exe` (empty in JSON) | Absolute path to SumatraPDF. Leave empty to use the copy bundled next to the service binary.  |

## Build and deploy

The project targets `net10.0-windows` and pins `x64`. The csproj is
configured for a self-contained single-file publish.

### Build

From the repo root, an elevated PowerShell prompt is not required:

```powershell
.\scripts\Build.ps1
```

This wraps `dotnet publish` with the project's standard configuration and
writes output to `<repo>\bin`. Override the destination with
`-OutputPath <path>` (must stay inside the repo root) or the build flavor
with `-Configuration Debug`.

Constraints to be aware of:

- The publish step copies `appsettings.example.json` to `appsettings.json`
  in the output directory. Edit that copied file with real values per
  deployment.
- The .NET 10 runtime is bundled (`SelfContained = true`), so the target
  machine does not need a separately installed runtime.
- The publish output does **not** include `SumatraPDF.exe`. Drop a copy
  next to the binary (or point `Sumatra:Path` at an existing install)
  before starting the service. Without it, `POST /api/v1/printers/{name}`
  returns `500`.

### Install as a Windows service

From an **elevated** PowerShell prompt, after publishing:

```powershell
.\scripts\Install-Service.ps1 -BinPath "C:\KempISPrintingService\LocalPrintServerService.exe"
```

What the script does
([source](scripts/Install-Service.ps1)):

1. Validates `-BinPath` points to an existing `.exe`.
2. Registers the `KempISPrintingService` event-log source in *Windows Logs →
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
| `-ServiceName` | `KempISPrintingService`                                                  |
| `-DisplayName` | `Kemp IS Printing Service`                                               |
| `-Description` | Service description shown in `services.msc`.                             |

The service runs as `LocalSystem` by default. If the target printers are
network printers installed only under a specific user profile, change the
logon account via `services.msc → Log On` to that user.

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
dotnet run --project LocalPrintServerService
```

The local `appsettings.json` (untracked) typically sets
`ApiDocumentation:Enabled = true`, so `/scalar/v1` is browsable. `Ctrl-C`
triggers a clean shutdown.

## Logging

The service writes to four places.

**1. Console** — `Information` and above. Visible when run with
`dotnet run` or attached to the SCM console. Errors go to stderr.

**2. `debug.log` (next to the binary)** — `Debug`-level only, rolled at
50 MB, ten files retained. Useful when reproducing issues locally.

**3. `error.log` (next to the binary)** — `Warning` and above, rolled at
50 MB, ten files retained. The authoritative on-disk diagnostic.

**4. Windows Application event log** — `Warning` and above only, under
*Event Viewer → Windows Logs → Application*, source `KempISPrintingService`.
Enabled only when the process is running under SCM (so dev `dotnet run`
does not require the event source to exist). The event source is created
by `Install-Service.ps1` at install time.

Sinks 1–3 are configured declaratively in the `Serilog` section of
`appsettings.json`. Sink 4 is added programmatically in
[`Program.cs`](LocalPrintServerService/Program.cs).
