import { expect, test, type Page } from '@playwright/test';
import { fileURLToPath } from 'node:url';

const password = process.env.E2E_PASSWORD ?? 'smoke-password';
const cube = fileURLToPath(new URL('./fixtures/cube.glb', import.meta.url));
const shots = 'test-results/screens';

async function login(page: Page) {
  await page.goto('/models');
  await expect(page).toHaveURL(/\/login\?next=/);
  await page.getByLabel('Mật khẩu').fill(password);
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page).toHaveURL(/\/models$/);
}

test('wrong password shows an error', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Mật khẩu').fill('not-the-password');
  await page.getByRole('button', { name: 'Đăng nhập' }).click();
  await expect(page.getByRole('alert')).toHaveText('Sai mật khẩu.');
  await page.screenshot({ path: `${shots}/01-login-error.png` });
});

test('log in → create model → upload .glb → preview map modes → publish', async ({ page, request }) => {
  const stamp = Date.now().toString(36);
  const title = `E2E Cube ${stamp}`;

  await login(page);
  await page.getByRole('link', { name: 'Upload model mới' }).click();
  await expect(page).toHaveURL(/\/models\/new$/);

  // English title drives the slug.
  await page.getByLabel('Tên (bắt buộc)').fill(title);
  await expect(page.getByLabel('Slug (đường dẫn)')).toHaveValue(`e2e-cube-${stamp}`);
  await page.getByRole('tab', { name: /Tiếng Việt/ }).click();
  await page.getByLabel('Tên', { exact: true }).fill(`Khối lập phương ${stamp}`);
  await page.getByLabel('Phần mềm').fill('Blender');
  await page.screenshot({ path: `${shots}/02-new-model-form.png` });
  await page.getByRole('button', { name: 'Tạo model' }).click();
  await expect(page).toHaveURL(/\/models\/[0-9a-f-]{36}$/);
  const id = page.url().split('/').pop()!;

  // Upload through tus; the server checks the bytes and records version 1.
  await page.locator('input[type=file]').setInputFiles(cube);
  await expect(page.getByRole('status')).toContainText('Đã nhận cube.glb');
  await expect(page.getByText('v1 · .glb')).toBeVisible();

  // Preview loads in WebGL; the cube has every map, so all 7 modes are enabled.
  const modes = page.getByRole('group', { name: 'Chế độ map' });
  await expect(modes.getByRole('button', { name: 'Độ nhám' })).toBeEnabled({ timeout: 30_000 });
  await expect(page.getByText('12 tris')).toBeVisible();
  for (const name of ['Hoàn chỉnh', 'Khung lưới', 'Màu gốc', 'Kim loại', 'Độ nhám', 'Normal', 'AO']) {
    await expect(modes.getByRole('button', { name, exact: true })).toBeEnabled();
  }
  await page.getByRole('button', { name: 'Tự xoay' }).click(); // stop the turntable for a steady screenshot
  await page.screenshot({ path: `${shots}/03-edit-final.png` });
  await modes.getByRole('button', { name: 'Độ nhám' }).click();
  await expect(modes.getByRole('button', { name: 'Độ nhám' })).toHaveAttribute('aria-pressed', 'true');
  await page.screenshot({ path: `${shots}/04-edit-roughness.png` });
  await page.locator('.stage').press('2'); // keyboard: 2 = wireframe
  await expect(modes.getByRole('button', { name: 'Khung lưới' })).toHaveAttribute('aria-pressed', 'true');
  await page.screenshot({ path: `${shots}/05-edit-wireframe.png` });

  // Publish → visible on the public API.
  await page.getByRole('button', { name: 'Publish' }).click();
  await expect(page.getByText('Published').first()).toBeVisible();
  const pub = await request.get(`/api/public/models/e2e-cube-${stamp}`);
  expect(pub.status()).toBe(200);

  // The list and dashboard show it.
  await page.getByRole('link', { name: 'Model 3D' }).first().click();
  await expect(page.getByRole('link', { name: title })).toBeVisible();
  await page.screenshot({ path: `${shots}/06-models-list.png` });
  await page.getByRole('link', { name: 'Dashboard' }).click();
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible();
  await page.screenshot({ path: `${shots}/07-dashboard-dark.png` });
  await page.getByRole('button', { name: 'Giao diện sáng' }).click();
  await page.screenshot({ path: `${shots}/08-dashboard-light.png` });
  await page.goto(`/models/${id}`);
  await expect(modes.getByRole('button', { name: 'Độ nhám' })).toBeEnabled({ timeout: 30_000 });
  await page.screenshot({ path: `${shots}/09-edit-light.png` });

  // Phone width: sidebar collapses into a menu.
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/models');
  await expect(page.getByRole('button', { name: 'Mở menu' })).toBeVisible();
  await page.screenshot({ path: `${shots}/10-models-phone.png`, fullPage: true });
});

test('a file that only pretends to be .glb is refused', async ({ page }) => {
  await login(page);
  await page.getByRole('link', { name: 'Upload model mới' }).click();
  await page.getByLabel('Tên (bắt buộc)').fill(`Fake ${Date.now().toString(36)}`);
  await page.getByRole('button', { name: 'Tạo model' }).click();
  await expect(page).toHaveURL(/\/models\/[0-9a-f-]{36}$/);
  await page.locator('input[type=file]').setInputFiles({ name: 'fake.glb', mimeType: 'model/gltf-binary', buffer: Buffer.from('<html>not a model</html>') });
  await expect(page.getByRole('alert')).toContainText('nội dung không phải .glb/.fbx hợp lệ');
});
