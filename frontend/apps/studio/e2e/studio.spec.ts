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
  await page.locator('input[type=file][accept=".glb,.fbx"]').setInputFiles(cube);
  await expect(page.getByRole('status')).toContainText('Đã nhận cube.glb');
  await expect(page.getByText('v1 · Gốc · .glb')).toBeVisible();
  // The Worker runs the pipeline: stats are read, an optimized copy is made.
  await expect(page.getByText('Hoàn tất')).toBeVisible({ timeout: 60_000 });
  await expect(page.getByText(/12 triangles · 1 meshes · 1 materials · maps: baseColor, metallicRoughness, normal, occlusion/)).toBeVisible();
  await expect(page.getByText('v1 · Tối ưu · .glb')).toBeVisible();

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

  // Thumbnail straight from the viewer, an opening view and viewer settings, then an extra image.
  await page.getByRole('button', { name: 'Chụp thumbnail' }).click();
  await expect(page.getByText('Đã lưu thumbnail từ khung xem.')).toBeVisible();
  await expect(page.getByRole('img', { name: 'Thumbnail hiện tại' })).toBeVisible();
  await page.getByRole('button', { name: 'Dùng góc này' }).click();
  await expect(page.getByText('Góc nhìn mở đầu: đã chọn')).toBeVisible();
  await page.getByLabel('Chế độ map mặc định').selectOption('wireframe');
  await page.getByRole('button', { name: 'Xám' }).click();
  await page.getByRole('button', { name: 'Lưu cài đặt' }).click();
  await expect(page.getByRole('button', { name: 'Đã lưu' }).last()).toBeVisible();
  await page.getByLabel('Loại ảnh').selectOption('Render');
  await page.locator('input[type=file][multiple]').setInputFiles({ name: 'render.png', mimeType: 'image/png', buffer: PNG_1PX });
  await page.getByLabel('Chú thích tiếng Việt ảnh 1').fill('Ảnh render');
  await page.getByLabel('Chú thích tiếng Việt ảnh 1').press('Tab');
  await expect(page.getByRole('img', { name: 'Ảnh render' })).toBeVisible();
  await page.screenshot({ path: `${shots}/05b-viewer-settings-images.png`, fullPage: true });

  // The saved default mode is what the preview opens with.
  await page.reload();
  await expect(modes.getByRole('button', { name: 'Khung lưới' })).toHaveAttribute('aria-pressed', 'true', { timeout: 30_000 });

  // Publish → visible on the public API.
  await page.getByRole('button', { name: 'Publish' }).click();
  await expect(page.getByText('Published').first()).toBeVisible();
  const pub = await request.get(`/api/public/models/e2e-cube-${stamp}`);
  expect(pub.status()).toBe(200);

  // The list and dashboard show it.
  await page.getByRole('link', { name: 'Model 3D' }).first().click();
  await expect(page.getByRole('link', { name: title, exact: true })).toBeVisible();
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
  await expect(page.getByRole('link', { name: title, exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth), 'no sideways scrolling on a phone').toBeLessThanOrEqual(390);
  await page.screenshot({ path: `${shots}/10-models-phone.png`, fullPage: true });
});

test('a file that only pretends to be .glb is refused', async ({ page }) => {
  await login(page);
  await page.getByRole('link', { name: 'Upload model mới' }).click();
  await page.getByLabel('Tên (bắt buộc)').fill(`Fake ${Date.now().toString(36)}`);
  await page.getByRole('button', { name: 'Tạo model' }).click();
  await expect(page).toHaveURL(/\/models\/[0-9a-f-]{36}$/);
  await page.locator('input[type=file][accept=".glb,.fbx"]').setInputFiles({ name: 'fake.glb', mimeType: 'model/gltf-binary', buffer: Buffer.from('<html>not a model</html>') });
  await expect(page.getByRole('alert')).toContainText('nội dung không phải .glb/.fbx hợp lệ');
});

// A real 1×1 PNG and a minimal PDF for the About page uploads.
const PNG_1PX = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==', 'base64');
const PDF = Buffer.from('%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n');

test('categories & tags: add, rename, delete', async ({ page }) => {
  const stamp = Date.now().toString(36);
  await login(page);
  await page.getByRole('link', { name: 'Danh mục & tag' }).click();
  await expect(page.getByRole('heading', { name: 'Danh mục & tag' })).toBeVisible();
  // Seeded starter categories are there.
  await expect(page.getByText('Characters', { exact: true })).toBeVisible();

  const tags = page.getByRole('region', { name: /^Tag/ });
  await tags.getByRole('button', { name: 'Thêm tag' }).click();
  await tags.getByLabel('Tên EN').fill(`Hard surface ${stamp}`);
  await tags.getByLabel('Tên VI').fill(`Bề mặt cứng ${stamp}`);
  await expect(tags.getByLabel('Slug')).toHaveValue(`hard-surface-${stamp}`);
  await tags.getByRole('button', { name: 'Thêm', exact: true }).click();
  await expect(tags.getByText(`Hard surface ${stamp}`)).toBeVisible();

  await tags.getByRole('button', { name: `Sửa Hard surface ${stamp}` }).click();
  await tags.getByLabel('Tên EN').fill(`Hard-surface ${stamp}`);
  await tags.getByRole('button', { name: 'Lưu' }).click();
  await expect(tags.getByText(`Hard-surface ${stamp}`)).toBeVisible();
  await page.screenshot({ path: `${shots}/11-taxonomy.png` });

  const del = tags.getByRole('button', { name: `Xoá Hard-surface ${stamp}` });
  await del.click();
  await tags.getByRole('button', { name: `Bấm lần nữa để xoá Hard-surface ${stamp}` }).click();
  await expect(tags.getByText(`Hard-surface ${stamp}`)).toHaveCount(0);
});

test('about page: profile, links, avatar and CV', async ({ page, request }) => {
  await login(page);
  await page.getByRole('link', { name: 'Trang About' }).click();
  await page.getByLabel('Tên hiển thị').fill('E2E Artist');
  await page.getByLabel('Dòng giới thiệu ngắn').fill('3D artist — props');
  await page.getByRole('tab', { name: 'Tiếng Việt' }).click();
  await page.getByLabel('Dòng giới thiệu ngắn').fill('Hoạ sĩ 3D — đồ vật');
  await page.getByRole('button', { name: 'Thêm liên kết' }).click();
  await page.getByLabel('Địa chỉ liên kết 1').fill('https://www.artstation.com/example');
  await page.getByRole('button', { name: 'Lưu thay đổi' }).click();
  await expect(page.getByRole('button', { name: 'Đã lưu' })).toBeVisible();

  // A bad link is caught before saving, in Vietnamese; the message goes away once the row is removed.
  await page.getByRole('button', { name: 'Thêm liên kết' }).click();
  await page.getByLabel('Địa chỉ liên kết 2').fill('javascript:alert(1)');
  await page.getByRole('button', { name: 'Lưu thay đổi' }).click();
  const linkError = page.getByText(/phải là địa chỉ http\(s\):\/\/… hoặc email/);
  await expect(linkError).toBeVisible();
  // Removing the bad row brings the form back to what is saved, so there is nothing left to save.
  await page.getByRole('button', { name: /Xoá liên kết/ }).last().click();
  await expect(linkError).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Đã lưu' })).toBeDisabled();

  await page.locator('input[type=file][accept^="image"]').setInputFiles({ name: 'me.png', mimeType: 'image/png', buffer: PNG_1PX });
  await expect(page.getByRole('img', { name: 'Ảnh đại diện hiện tại' })).toBeVisible();
  await page.locator('input[type=file][accept="application/pdf"]').setInputFiles({ name: 'cv.pdf', mimeType: 'application/pdf', buffer: PDF });
  await expect(page.getByRole('link', { name: 'CV.pdf' })).toBeVisible();
  await page.screenshot({ path: `${shots}/12-about.png` });

  // A "PDF" that is really an image is refused.
  await page.locator('input[type=file][accept="application/pdf"]').setInputFiles({ name: 'fake.pdf', mimeType: 'application/pdf', buffer: PNG_1PX });
  await expect(page.getByRole('alert')).toContainText('CV phải là file PDF thật');

  const pub = await (await request.get('/api/public/profile')).json();
  expect(pub.headlineVi).toBe('Hoạ sĩ 3D — đồ vật');
  expect(pub.links.artstation).toBe('https://www.artstation.com/example');
  expect((await request.get(pub.cvUrl)).headers()['content-type']).toContain('application/pdf');
});
