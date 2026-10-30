# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Working rules and the review-package delivery process are in AGENTS.md (Turkish) and apply here too:

@AGENTS.md

## Repository layout

Monorepo for an industrial chemical WMS:

- `backend/` — ASP.NET Core Web API (.NET 10, EF Core 10 + Npgsql, PostgreSQL 16, JWT, SignalR). Solution: `backend/backend.sln`.
- `backend.Tests/` — xUnit tests (Moq, SQLite in-memory, Testcontainers PostgreSQL). Lives outside `backend/` but is included in `backend.sln`.
- `web-portal/` — React 19 + Vite + MUI admin dashboard (plain JS/JSX, no TypeScript).
- `mobile-terminal/` — Expo Router / React Native barcode terminal (QR + Code 128 via expo-camera).
- `ocr-service/` — Python FastAPI + PaddleOCR service, used by the backend only inside Compose.
- `docker-compose.yml` + `Caddyfile` — full stack; Caddy is the only service publishing ports (80/443).

## Commands

```bash
# Backend
dotnet build backend/backend.sln
cd backend && dotnet run                      # http://localhost:5059, Scalar docs at /scalar/v1 (Development only)

# Backend tests (Testcontainers tests need Docker; the filter skips them)
dotnet test backend.Tests/backend.Tests.csproj --filter "Category!=Testcontainers"
dotnet test backend.Tests/backend.Tests.csproj                                   # all, incl. PostgreSQL containers
dotnet test backend.Tests/backend.Tests.csproj --filter "FullyQualifiedName~UnitServiceTests.SomeTestName"

# EF Core migrations (design-time factory reads backend/appsettings*.json + env vars)
cd backend && dotnet ef migrations add <Name>

# Web portal (Vite proxies /api and /hubs to http://localhost:5059; override with VITE_DEV_API_TARGET)
cd web-portal && npm ci && npm run dev
cd web-portal && npm test                     # node --test tests/*.test.js
cd web-portal && node --test tests/users.test.js
cd web-portal && npm run lint                 # oxlint
cd web-portal && npm run build

# Mobile terminal
cd mobile-terminal && npm ci && npm start
cd mobile-terminal && npm test

# Full stack
cp .env.example .env && docker compose up --build -d
docker compose --profile dev up --build -d    # also starts Mailpit (localhost:8025 / 1025)
```

CI (`.github/workflows/ci.yml`) runs: backend Release build + `dotnet test backend/backend.sln`, web-portal `npm test` + `npm run build`, mobile `npm test` + `npx expo export --platform web`, and `docker compose config`.

## Backend architecture

- **Layering:** `Controllers/` → `Services/Interfaces` (`I*Service`) → `Services/Implementations` → `Data/MtsDbContext`. DTOs live in `DTOs/` as C# `record`s; entities in `Entities/`. All services are registered explicitly in `Program.cs`.
- **Errors:** services throw `NotFoundException` (404), `BusinessRuleException` (400, carries `ErrorCode` + `Details`, e.g. `BATCH_MISMATCH`), or `OcrServiceUnavailableException` (503). `Middlewares/GlobalExceptionHandlerMiddleware` maps them to a JSON `{ statusCode, message, errorCode, details, timestamp }`. In Production, other exceptions fall through to a generic 500 without details. Clients (`web-portal/src/apiError.js`, `mobile-terminal/src/domain/consumeErrors.js`) depend on `errorCode`.
- **Authorization:** a fallback policy requires an authenticated `Admin` or `Operator` on every endpoint; public endpoints must opt out with `[AllowAnonymous]`, Admin-only ones use `[Authorize(Roles = "Admin")]`. The README's "Endpoint → role matrix" is the reference and is covered by `AuthorizationEndpointTests`.
- **Token invalidation:** access tokens carry the user's `SecurityStamp`, checked per request (`Common/Security/AccessTokenSecurityStampValidator`). Password/role/active changes must rotate the stamp (`SecurityStampHelper`) and revoke refresh tokens (`RefreshTokenRevocation`).
- **Concurrency:** `Unit.Version` is an EF concurrency token. Rack capacity and relocation checks lock address rows with `SELECT ... FOR UPDATE` via `AddressLockHelper` (ordered by id to avoid deadlocks); on SQLite it falls back to a plain query, so real concurrency behavior is only exercised by the Testcontainers tests.
- **Startup:** `Program.cs` runs `Database.MigrateAsync()` then `DbInitializer.SeedAsync` (roles, admin, optional demo user via `DEMO_USER_ENABLED`, demo inventory via `DemoDataSeeder` when `DEMO_DATA_ENABLED`). Non-production falls back to built-in dev JWT secret / `admin123` / local Postgres; Production requires `JWT_SECRET`, `ADMIN_PASSWORD` (when no active admin), and a non-default DB password (`ProductionConfigurationValidator`).
- **Background work:** low-stock events go through an in-memory `Channel` (`ILowStockChannel`) consumed by `LowStockAlertBackgroundService` (MailKit email). `RefreshTokenCleanupBackgroundService` purges expired refresh tokens.
- **Realtime:** SignalR hub at `/hubs/traceability` (`Hubs/TraceabilityHub`); services broadcast via `IHubContext` (e.g. `UnitService` on consume).
- **Barcodes:** payloads are plain text — `BAT-{BatchNo}`, `UNIT-{BatchNo}-{index:D3}`, `ADR-{Code}` — parsed in `Common/Barcodes`. Visual format (QR vs Code 128) is a system setting resolved by `BarcodeGeneratorFactory` over `IBarcodeGeneratorStrategy` implementations; changing format never changes payloads.
- **Consume flow:** `POST /api/units/{barcode}/consume` requires body `{ "batchBarcode": "BAT-..." }`; batch must match the unit's batch (batch-first scanning enforced server-side).
- **OCR:** `IOcrService` either calls `ocr-service` (`Ocr:Mode=Remote`) or returns fixture text (`Ocr:Mode=Sample`, Development default); `LabelParserService` extracts lot/expiry from the text.
- **DB naming:** tables are snake_case via `ToTable(...)` in `MtsDbContext`; column names are mostly the C# property names (quoted, e.g. `"Id"`) — check the model snapshot before writing raw SQL.

## Tests

- `SqliteTestDatabase` gives an in-memory SQLite `MtsDbContext` (`EnsureCreated`, no migrations) for service-level tests.
- Classes tagged `[Trait("Category", "Testcontainers")]` share a PostgreSQL container via `PostgresTestCollection` and cover locking/concurrency and Postgres-specific behavior.
- Frontend/mobile tests use Node's built-in `node:test` and target pure modules in `src/domain/` and similar — keep logic testable there rather than inside JSX components.

## Frontend notes

- `web-portal/src/api.js` is the shared axios instance (base `/api`), attaches the bearer token from `localStorage.user`, and handles single-flight refresh on 401. SignalR is in `hooks/useSignalR.js` (`domain/signalrAuth.js` detects unauthorized errors).
- Route guards: `components/ProtectedRoute.jsx`, `components/AdminRoute.jsx`; role logic in `domain/access.js`.
- UI strings are bilingual (TR/EN) via `LanguageContext` in both web and mobile.
- The mobile terminal's API base URL is set at runtime on its Settings screen (LAN IP for physical devices); tokens are kept in SecureStore.
