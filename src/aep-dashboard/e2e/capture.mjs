// Captures dashboard screenshots to docs/screenshots/ for the README.
//
// Prerequisite: the dashboard served at BASE_URL (default http://localhost:4200) with the
// committed sample dataset in public/data/. Run `npm start` in one shell, then `npm run
// screenshots` in another.

import { chromium } from 'playwright';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';
import { mkdir } from 'node:fs/promises';

const baseUrl = process.env.BASE_URL ?? 'http://localhost:4200';
const here = dirname(fileURLToPath(import.meta.url));
const outDir = resolve(here, '../../../docs/screenshots');

async function main() {
  await mkdir(outDir, { recursive: true });

  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 1400 }, deviceScaleFactor: 2 });

  await page.goto(baseUrl, { waitUntil: 'networkidle' });
  await page.waitForSelector('.cards .score', { timeout: 15000 });

  // Select the regressed scenario up front so the whole view tells the regression story.
  const injectionRow = page.locator('table.matrix tbody tr', { hasText: 'injection' }).first();
  await injectionRow.click();
  await page.waitForTimeout(400);

  // Tight crop of the dashboard (the .page container auto-sizes to content).
  await page.locator('.page').screenshot({ path: resolve(outDir, 'dashboard-overview.png') });
  console.log('captured dashboard-overview.png');

  // Close-up of the side-by-side scenario detail.
  await page.locator('section.panel', { hasText: '— detail' }).screenshot({
    path: resolve(outDir, 'dashboard-scenario-detail.png'),
  });
  console.log('captured dashboard-scenario-detail.png');

  await browser.close();
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
