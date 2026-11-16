// Example E2E spec template for Aroklon POS (WPF Desktop)
// NOTE: This is a template. WPF desktop apps cannot be tested with Playwright.
// Use WinAppDriver or manual testing instead.
// Reference: /root/.config/opencode/skills/replica-test/e2e.example.spec.ts

import { test, expect } from '@playwright/test';

test.describe('Aroklon POS - Example (NOT RUNNABLE on WPF)', () => {
  test('F03-H1: Cash sale happy path', async ({ page }) => {
    // This test would run against a web app. For WPF, use WinAppDriver instead.
    // Example structure (adapted for WPF):
    //
    // const app = await application.launch({ command: 'WinAppDriver.exe' });
    // const window = await app.window({ regex: /POS/ });
    // await window.getByRole('button', { name: 'Kasir' }).click();
    // await window.getByRole('textbox', { name: 'Cari produk' }).fill('Roti');
    // await window.getByRole('button', { name: 'Checkout' }).click();
    // await expect(window.getByRole('text', { name: 'Struk' })).toBeVisible();
    //
    // throw new Error('Playwright does not support WPF desktop apps');
  });
});
