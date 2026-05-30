import { defineConfig, devices } from "@playwright/test";

// E2E config. Brings up BOTH the ASP.NET Core API (5080) and the Next.js app (3000) and points the
// web server at the API via SAHAHR_API_URL (the run-requirement documented in README). Targets the
// local Docker Postgres (localhost:5544), which is seeded fresh from db/init (incl ATS permissions).
//
// Prereqs: `pnpm infra:up` (Postgres+Redis) and `dotnet`/`pnpm` on PATH.
// Run:     pnpm -C apps/web exec playwright test

const API_PORT = 5080;
const WEB_PORT = 3000;
const API_BASE = `http://127.0.0.1:${API_PORT}`;

const DB_APP = "Host=localhost;Port=5544;Database=sahahr;Username=sahahr_app;Password=sahahr_app_pw;SSL Mode=Disable";
const DB_OWNER = "Host=localhost;Port=5544;Database=sahahr;Username=sahahr_owner;Password=sahahr_dev_pw;SSL Mode=Disable";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [["list"]],
  timeout: 60_000,
  expect: { timeout: 15_000 },
  use: {
    baseURL: `http://localhost:${WEB_PORT}`,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      // ASP.NET Core API against local Docker Postgres
      command:
        "dotnet run --project ../api/src/SahaHR.Api --no-launch-profile",
      url: `${API_BASE}/health`,
      timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: API_BASE,
        ConnectionStrings__Default: DB_APP,
        ConnectionStrings__Migrator: DB_OWNER,
        Jwt__Issuer: "https://dev.sahahr.local",
        Jwt__Audience: "sahahr-api",
        Jwt__SigningKey: "dev-only-change-me-must-be-at-least-32-characters-long",
      },
    },
    {
      // Next.js app (dev mode for fast startup), pointed at the API
      command: "pnpm dev",
      url: `http://localhost:${WEB_PORT}/login`,
      timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      env: { SAHAHR_API_URL: API_BASE },
    },
  ],
});
