import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';

// The data the site was built from: assertions follow whatever is published.
const site = JSON.parse(readFileSync(new URL('../data/site.json', import.meta.url), 'utf8'));
const shots = 'test-results/screens';
const first = site.models.find((m: { modelUrl: string | null }) => m.modelUrl) ?? site.models[0];
const titleOf = (m: { translations: { locale: string; title: string }[] }, l: string) =>
  m.translations.find((t) => t.locale === l)?.title ?? m.translations.find((t) => t.locale === 'en')!.title;

test('root sends visitors to the English site', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveURL(/\/en\/?$/);
});

test('home lists every published model and filters by category', async ({ page }) => {
  await page.goto('/en');
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  await expect(page.locator('#works a[href*="/en/models/"]')).toHaveCount(site.models.length);
  await expect(page.getByRole('link', { name: new RegExp(titleOf(first, 'en')) }).first()).toBeVisible();
  await page.screenshot({ path: `${shots}/s01-home-en.png`, fullPage: true });
});

test('detail page: live 3D view with map modes, specs, language switch', async ({ page }) => {
  await page.goto(`/en/models/${first.slug}`);
  await expect(page.getByRole('heading', { level: 1, name: titleOf(first, 'en') })).toBeVisible();
  const modes = page.getByRole('group', { name: 'Map mode' });
  await expect(modes.getByRole('button', { name: /Wireframe/ })).toBeEnabled({ timeout: 30_000 });
  await expect(page.getByText('Technical specs')).toBeVisible();
  await page.getByRole('button', { name: 'Turntable: rotate 360°' }).click();
  await page.screenshot({ path: `${shots}/s02-detail-en.png` });

  await page.locator('.stage').press('5'); // 5 = roughness
  if (await modes.getByRole('button', { name: /Roughness/ }).isEnabled()) {
    await expect(modes.getByRole('button', { name: /Roughness/ })).toHaveAttribute('aria-pressed', 'true');
  }

  await page.getByRole('link', { name: 'VI', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/vi/models/${first.slug}`));
  await expect(page.getByRole('heading', { level: 1, name: titleOf(first, 'vi') })).toBeVisible();
  await expect(page.getByRole('group', { name: 'Chế độ map' })).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('lang', /vi/);
});

test('about page and light theme', async ({ page }) => {
  await page.goto('/vi/about');
  await expect(page.getByRole('heading', { level: 1, name: site.profile.displayName })).toBeVisible();
  if (site.profile.cvUrl) await expect(page.getByRole('link', { name: 'Tải CV (PDF)' })).toHaveAttribute('href', site.profile.cvUrl);
  await page.getByRole('button', { name: 'Chuyển sang giao diện sáng' }).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light'); // remembered
  await page.screenshot({ path: `${shots}/s03-about-vi-light.png`, fullPage: true });
});

test('phone width: no sideways scrolling', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  for (const path of ['/en', `/en/models/${first.slug}`, '/en/about']) {
    await page.goto(path);
    expect(await page.evaluate(() => document.documentElement.scrollWidth), path).toBeLessThanOrEqual(390);
  }
  await page.goto(`/en/models/${first.slug}`);
  await expect(page.getByRole('group', { name: 'Map mode' }).getByRole('button', { name: /Final/ })).toBeEnabled({ timeout: 30_000 });
  await page.screenshot({ path: `${shots}/s04-detail-phone.png` });
});
