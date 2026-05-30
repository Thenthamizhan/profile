import { test, expect, type Page } from "@playwright/test";

// Seeded dev fixtures (db/init/03_seed_dev.sql + 05_seed_ats.sql).
const JOB = "01900000-0000-7000-8000-00000000f001";
const APP = "01900000-0000-7000-8000-00000000aa01"; // Jasmine Tan, stage "applied"
const APP_DETAIL = `/recruitment/${JOB}/applications/${APP}`;

/// Logs in via the dev sign-in form (seeded tenant/user pre-filled) and waits until the app shell
/// for the authenticated area is visible.
async function login(page: Page) {
  await page.goto("/login");
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.waitForURL("**/employees");
  await expect(page.getByRole("heading", { name: "Employees" })).toBeVisible();
}

test.describe("Offers & scorecards UI", () => {
  test("offer lifecycle: create → send → accept hires the candidate", async ({ page }) => {
    await login(page);
    await page.goto(APP_DETAIL);

    // Panels render (permission-aware: the seeded hr_admin holds offer.* / interview.*)
    await expect(page.getByRole("heading", { name: "Offers" })).toBeVisible();
    await expect(page.getByRole("heading", { name: /Interviews/ })).toBeVisible();

    // Create a draft offer. Offers accumulate across runs (local DB persists), so we always act on
    // the NEWEST row — ListAsync orders by created_at DESC, so the first offer-row is this run's.
    await page.getByPlaceholder("8500.00").fill("9200");
    await page.getByRole("button", { name: "Create draft offer" }).click();

    const newest = page.getByTestId("offer-row").first();
    await expect(newest).toHaveAttribute("data-offer-status", "draft");

    // Send it → becomes "sent"
    await newest.getByRole("button", { name: "Send" }).click();
    await expect(page.getByTestId("offer-row").first()).toHaveAttribute("data-offer-status", "sent");

    // Accept it → becomes "accepted"
    await page.getByTestId("offer-row").first().getByRole("button", { name: "Accept" }).click();
    await expect(page.getByTestId("offer-row").first()).toHaveAttribute("data-offer-status", "accepted");

    // Accepting hired the candidate → the board shows the card in the Hired column
    await page.goto(`/recruitment/${JOB}`);
    const hiredCol = page.locator("div").filter({ hasText: /^Hired/ }).first();
    await expect(hiredCol).toContainText("Jasmine Tan");
  });

  test("scorecard: schedule an interview and submit a weighted rollup", async ({ page }) => {
    await login(page);
    await page.goto(APP_DETAIL);

    // Schedule an interview (interviews accumulate across runs; act on the NEWEST row, which is
    // ordered created_at DESC and is freshly un-scored).
    await page.locator('input[type="datetime-local"]').fill("2026-06-15T10:00");
    await page.getByRole("button", { name: "Schedule interview" }).click();

    const row = page.getByTestId("interview-row").first();
    await expect(row).toHaveAttribute("data-has-scorecard", "false");

    // Fill this row's three default competencies: scores 5 / 4 / 3 with weights 2 / 1 / 1
    // weighted avg = (2*5 + 1*4 + 1*3) / 4 = 17/4 = 4.25
    const scoreBoxes = row.locator('input[placeholder="1-5"]');
    await scoreBoxes.nth(0).fill("5");
    await scoreBoxes.nth(1).fill("4");
    await scoreBoxes.nth(2).fill("3");
    await row.locator('select[name="recommendation"]').selectOption("hire");
    await row.getByRole("button", { name: "Submit scorecard" }).click();

    // After submit the row shows the rollup badge (4.25) and flips to scored
    const scored = page.getByTestId("interview-row").first();
    await expect(scored).toContainText(/score 4\.25/);
    await expect(scored).toContainText("hire");
  });

  test("application detail renders both panels for an authorized user", async ({ page }) => {
    // The seeded hr_admin holds offer.* + interview.*, so both panels render (not the
    // "You lack …" fallback). Negative-path RBAC is covered by the API integration tests.
    await login(page);
    await page.goto(APP_DETAIL);
    await expect(page.getByRole("heading", { name: "Application" })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Offers" })).toBeVisible();
    await expect(page.getByRole("heading", { name: /Interviews/ })).toBeVisible();
    await expect(page.getByText("You lack")).toHaveCount(0);
  });
});
