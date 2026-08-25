# MediaGrab

MediaGrab is a URL-analysis and authorized-download web application built with ASP.NET Core, EF Core, and
PostgreSQL. A signed-in user submits a URL; MediaGrab validates and normalizes it, matches it against a
registry of **authorized** media providers, and - once approved - downloads the file through a background
worker and hands it back as a resumable, streamed file. MediaGrab is explicitly designed to never bypass DRM,
authentication, paywalls, or platform access controls: unsupported or unauthorized URLs are simply rejected.

This document covers both phases delivered so far.

## Architecture

```
MediaGrab.sln
MediaGrab.Domain/           Entities and enums. No dependencies on anything else.
MediaGrab.Application/      Interfaces, DTOs, and contracts (IMediaProvider, IUrlValidator, IProviderRegistry,
                             IDownloadJobService, IFileStorageService, IDownloadQueue). Depends only on Domain.
MediaGrab.Infrastructure/   EF Core DbContext + configurations, UrlValidator, the SSRF-hardened HTTP client
                             factory, ProviderRegistry + DirectFileProvider, DownloadJobService, local file
                             storage, and the background workers (download processor, cleanup sweep).
MediaGrab.Web/               ASP.NET Core entry point: Program.cs, Controllers (MVC + API + Admin), Razor
                             Pages (Account), Views, wwwroot. Depends on all three above.
MediaGrab.Tests/            xUnit tests for validation, SSRF guarding, providers, storage, and job service.
```

Domain and Application know nothing about EF Core, PostgreSQL, or ASP.NET Core, so business rules can be
tested and reasoned about in isolation. Infrastructure is the only place that talks to the database, the
filesystem, or the network. Web is the only place that knows about HTTP.

### Provider architecture

`IMediaProvider` is the contract every authorized source implements: `CanHandleAsync`, `AnalyzeAsync`,
`GetMetadataAsync`/`GetAvailableFormatsAsync` (folded into `AnalyzeAsync`'s `MediaMetadata.Formats` as of
Phase 2), and `DownloadAsync`. `ProviderRegistry` is injected with every registered `IMediaProvider` and picks
the right one for a submitted URL. Adding a new provider is:

1. Implement `IMediaProvider`.
2. Add one line to `MediaGrab.Web/Extensions/ServiceCollectionExtensions.cs`:
   `services.AddScoped<IMediaProvider, YourNewProvider>();`

No other file needs to change. `DirectFileProvider` is the working reference implementation, covering plain,
publicly accessible file links (`.mp4`, `.mp3`, `.pdf`, `.zip`, images, etc.).

## Prerequisites

- Visual Studio 2022 (17.12+) with the **ASP.NET and web development** workload, or the .NET 10 SDK for the
  CLI
- .NET 10 SDK
- PostgreSQL 14+ running locally or reachable over the network
- Git

## Opening the project in Visual Studio

1. Double-click `MediaGrab.sln`, or **File > Open > Project/Solution**.
2. Set **MediaGrab.Web** as the startup project if it isn't already (right-click it > *Set as Startup
   Project*).
3. Let NuGet restore packages on first load.

## Configuring PostgreSQL

Create a database and role, e.g. from `psql`:

```sql
CREATE DATABASE mediagrab;
CREATE USER mediagrab_user WITH PASSWORD 'choose-a-strong-password';
GRANT ALL PRIVILEGES ON DATABASE mediagrab TO mediagrab_user;
```

**Never put the real password in `appsettings.json` or `appsettings.Development.json`** - both are tracked by
Git. Use user-secrets (development) or an environment variable instead.

### Option A - user-secrets (recommended for local development)

From the `MediaGrab.Web` folder:

```
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=mediagrab;Username=mediagrab_user;Password=choose-a-strong-password"
```

Or in Visual Studio: right-click **MediaGrab.Web > Manage User Secrets**.

### Option B - environment variable

```
setx ConnectionStrings__DefaultConnection "Host=localhost;Port=5432;Database=mediagrab;Username=mediagrab_user;Password=choose-a-strong-password"
```

(Restart Visual Studio/your terminal afterward so it picks up the new value.)

`appsettings.Example.json` documents every setting the app expects, without real credentials - copy values
from there into whichever mechanism you choose, never into a committed file.

## Running the database migrations

No migration has been checked in yet (the schema changed several times across both phases), so create the
first one now, from the `MediaGrab.Web` folder, pointing at `MediaGrab.Infrastructure` where the `DbContext`
lives:

```
cd MediaGrab.Web
dotnet ef migrations add InitialCreate --project ..\MediaGrab.Infrastructure --startup-project .
dotnet ef database update --project ..\MediaGrab.Infrastructure --startup-project .
```

Don't have the EF Core CLI tool? `dotnet tool install --global dotnet-ef`.

In Visual Studio's **Package Manager Console** (set Default project to `MediaGrab.Infrastructure`):

```
Add-Migration InitialCreate -StartupProject MediaGrab.Web
Update-Database -StartupProject MediaGrab.Web
```

This creates the Identity tables (`AspNetUsers`, `AspNetRoles`, etc.) alongside `DownloadJobs`,
`DownloadHistories`, `MediaFiles`, `SupportedProviders`, and `SystemSettings`.

## Running the app locally

- **Visual Studio**: `F5` (or `Ctrl+F5` without the debugger). The `https` launch profile opens your browser
  automatically.
- **CLI**: from `MediaGrab.Web`, `dotnet run`, then open the HTTPS URL printed in the console.

On first startup the app automatically ensures an `Administrator` role exists (see **Admin access** below) -
it never assigns it to anyone automatically.

## Trying it out

1. Register an account from the homepage (you'll be signed in automatically).
2. Paste a direct file URL (e.g. `https://example.com/file.mp4`) into the **Analyze** box. MediaGrab validates
   it, rejects it outright if it targets a private/internal address, and - if a provider matches - performs a
   real HTTP HEAD/GET to confirm the resource exists and read its size/content-type.
3. Start the download. The job is queued, picked up by the background worker, streamed to temporary local
   storage with a hard size cap enforced mid-transfer (not just trusted from the response header), and marked
   `Completed`.
4. Poll `/api/media/status/{id}` (or refresh the dashboard) to watch it move through
   `Queued -> Processing -> Completed`, then download the finished file - it streams straight from disk with
   resumable range support and is deleted immediately after a successful transfer.
5. Visit `/Dashboard`, `/Dashboard/History`, and `/Dashboard/Settings` (signed-in only).

### Admin access

`/admin` is protected by a `RequireAdministrator` policy tied to the `Administrator` role. The role is created
automatically at startup, but no user is ever added to it automatically. To make your own account an admin,
run this once against the database after registering:

```sql
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id" FROM "AspNetUsers" u, "AspNetRoles" r
WHERE u."Email" = 'you@example.com' AND r."Name" = 'Administrator';
```

`/admin` shows job/user/provider counts, a recent-downloads table, and the live queue depth and configured
limits.

## Running the tests

```
dotnet test
```

Covers: URL validation (scheme/length/host/SSRF targets), the SSRF connect-time guard (`SsrfGuardTests`),
provider matching (`ProviderRegistryTests`, `DirectFileProviderTests`), local file storage
(`LocalFileStorageServiceTests`), filename sanitization, the bounded download queue, and job-service
orchestration.

## Configuration reference

All limits live under the `MediaGrab` section of `appsettings.json` (bound to `MediaGrabOptions`) - nothing
is hard-coded in application code:

| Setting | Default | Purpose |
|---|---|---|
| `MaxFileSizeBytes` | 2 GB | Hard cap on a single file; enforced from headers at analysis time **and** mid-stream during download, since a server can omit or lie about `Content-Length`. |
| `MaxProcessingTimeSeconds` | 900 | A job stuck longer than this is force-failed rather than holding a worker slot forever. |
| `MaxConcurrentJobs` | 4 | How many downloads the background worker runs at once. |
| `MaxQueuedJobs` | 200 | Bounded queue capacity - once full, new submissions get a `503` instead of growing memory unbounded. |
| `FileRetentionMinutes` | 60 | How long a completed, undownloaded file is kept before cleanup. |
| `CleanupIntervalSeconds` | 120 | How often the cleanup sweep runs. |
| `RateLimits.*` | see `appsettings.json` | Per-minute limits for analyze, download-start, file-download, and general API calls. |

## Security notes

- **URL validation** (`UrlValidator`): HTTP/HTTPS only, max length 2048, rejects requests targeting
  localhost, loopback, or private/link-local IP ranges (including the `169.254.169.254` cloud metadata
  address).
- **SSRF, closed at connect time, not just validation time**: `SsrfSafeHttpClientFactory` gives every provider
  an `HttpClient` whose `SocketsHttpHandler` resolves DNS and re-checks the resolved IP itself inside its
  `ConnectCallback`, immediately before opening the TCP connection - and rejects the connection if *any*
  resolved address is private/reserved. This closes the DNS-rebinding gap that URL-time validation alone
  cannot: a hostname that resolves safely at validation time but points at a private IP moments later at
  connect time. Automatic redirects are disabled so a redirect can never sneak past this check unvalidated.
- **Size limits enforced twice**: once from the declared `Content-Length` (fast rejection), and again as
  bytes are actually streamed to disk, so a server cannot lie about size to exhaust storage.
- **Filenames are always sanitized** (`FileNameSanitizer`) and files are addressed by opaque storage keys
  (`IFileStorageService`), never raw paths supplied by a URL or user input.
- **Files are deleted immediately after a successful download** (in `MediaController.File`'s
  `Response.OnCompleted`), with `FileCleanupService` as a periodic backup sweep for expired, abandoned, or
  orphaned files/jobs - it never touches a file that's mid-stream.
- **Rate limiting** is applied per endpoint class (analyze, download-start, file-download, general API) via
  `AddMediaGrabRateLimiting`.
- Passwords are hashed by ASP.NET Core Identity; MediaGrab never stores or logs plaintext passwords.
- The global exception handler returns a generic message and trace ID to the browser; full details are only
  ever logged server-side (raw messages are only returned to the client in Development).
- Baseline security headers, HSTS outside Development, and CSRF (antiforgery) protection on forms remain from
  Phase 1.
- No secrets or credentials are committed - see `.gitignore` and `appsettings.Example.json`.

## Git / GitHub

```
git init
git add .
git commit -m "MediaGrab Phase 2"
git branch -M main
git remote add origin <your-repo-url>
git push -u origin main
```

`.github/workflows/build.yml` runs `dotnet restore`, `build`, and `test` on every push/PR to `main`.

## What's complete

**Phase 1** - layered architecture; domain entities and EF Core mappings; PostgreSQL wired through EF Core
with connection strings sourced only from configuration/env vars; the `IMediaProvider`/`IProviderRegistry`
extension point; `UrlValidator`; ASP.NET Core Identity (register/login/logout, hashed passwords, cookie auth);
initial API scaffolding; global exception handling; structured logging; baseline security headers; the full
page set (Home, Download, Supported Sites, About, FAQ, Contact, Privacy, Terms, Login, Register, Dashboard,
History, Settings); initial xUnit tests.

**Phase 2** - real provider logic: `DirectFileProvider.AnalyzeAsync` performs an authorized HTTP HEAD (falling
back to a headers-only GET) to confirm a resource exists and read its size/content-type without downloading
the whole file, and `DownloadAsync` streams the body straight to storage with no full-file buffering. Added on
top of that: connect-time SSRF hardening (`SsrfSafeHttpClientFactory`/`SsrfGuard`); a bounded background job
queue and worker (`DownloadQueue`, `DownloadProcessingWorker`) with per-job timeouts and progress reporting;
local file storage behind an opaque-key abstraction (`IFileStorageService`/`LocalFileStorageService`); a
periodic cleanup service for expired/abandoned files (`FileCleanupService`); a real, resumable, streamed
file-download endpoint with immediate post-download cleanup; per-endpoint rate limiting; and an admin area
(`/admin`, role-gated) with job/user/provider overviews and system limits.

## What's still ahead

- Additional authorized providers beyond direct file links
- Wiring the Dashboard/History views to real per-user job data (currently placeholder views; the API already
  returns real data)
- Email confirmation for new accounts and a real contact form
- Admin management actions (enable/disable a provider, adjust limits) beyond the current read-only views
- Horizontal scaling considerations for the in-memory download queue (currently single-instance; a
  multi-instance deployment would need a durable/shared queue)
