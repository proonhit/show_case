// Prepares the data the static Showcase is built from.
//   API_URL=http://localhost:5080 node scripts/fetch-data.mjs   → published content from the API
//   node scripts/fetch-data.mjs                                 → the sample in sample/ (CI, first run)
// Writes data/site.json and downloads every file the pages show into public/media/,
// so the deployed site never calls the API.
import { cp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const dataFile = join(root, 'data', 'site.json');
const mediaDir = join(root, 'public', 'media');
const api = process.env.API_URL?.replace(/\/$/, '');
const siteUrl = (process.env.SITE_URL ?? 'http://localhost:5175').replace(/\/$/, '');

/** sitemap.xml (every page in both languages, with hreflang alternates) and robots.txt. */
async function writeSeoFiles(models) {
  const pages = ['', '/about', ...models.map((m) => `/models/${m.slug}`)];
  const lastmod = (path) => {
    const m = models.find((x) => path === `/models/${x.slug}`);
    return (m?.updatedAt ?? new Date().toISOString()).slice(0, 10);
  };
  const urls = pages.flatMap((path) => ['en', 'vi'].map((l) => `  <url>
    <loc>${siteUrl}/${l}${path}</loc>
    <lastmod>${lastmod(path)}</lastmod>
    <xhtml:link rel="alternate" hreflang="en" href="${siteUrl}/en${path}"/>
    <xhtml:link rel="alternate" hreflang="vi" href="${siteUrl}/vi${path}"/>
    <xhtml:link rel="alternate" hreflang="x-default" href="${siteUrl}/en${path}"/>
  </url>`));
  await writeFile(join(root, 'public', 'sitemap.xml'), `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">
${urls.join('\n')}
</urlset>
`);
  await writeFile(join(root, 'public', 'robots.txt'), `User-agent: *\nAllow: /\n\nSitemap: ${siteUrl}/sitemap.xml\n`);
}

await rm(mediaDir, { recursive: true, force: true });
await mkdir(dirname(dataFile), { recursive: true });

if (!api) {
  await cp(join(root, 'sample', 'media'), mediaDir, { recursive: true });
  const sample = await readFile(join(root, 'sample', 'site.json'));
  await writeFile(dataFile, sample);
  await writeSeoFiles(JSON.parse(sample.toString()).models);
  console.log('Showcase data: sample (set API_URL to build from the API)');
  process.exit(0);
}

const get = async (path) => {
  const res = await fetch(api + path);
  if (!res.ok) throw new Error(`${path}: HTTP ${res.status}`);
  return res.json();
};
const EXT = { 'image/png': 'png', 'image/jpeg': 'jpg', 'image/webp': 'webp', 'application/pdf': 'pdf', 'model/gltf-binary': 'glb' };

/** Downloads an API file into public/media/<folder>/<name>.<ext> and returns its site URL. */
async function download(url, folder, name) {
  if (!url) return null;
  const res = await fetch(api + url);
  if (!res.ok) {
    console.warn(`  skipped ${url}: HTTP ${res.status}`);
    return null;
  }
  const ext = EXT[(res.headers.get('content-type') ?? '').split(';')[0]] ?? 'bin';
  const file = `${name}.${ext}`;
  await mkdir(join(mediaDir, folder), { recursive: true });
  await writeFile(join(mediaDir, folder, file), Buffer.from(await res.arrayBuffer()));
  return `/media/${folder}/${file}`;
}

const [profile, categories, tags, models] = await Promise.all([
  get('/api/public/profile'), get('/api/public/categories'), get('/api/public/tags'), get('/api/public/models'),
]);

profile.avatarUrl = await download(profile.avatarUrl, 'profile', 'avatar');
profile.cvUrl = await download(profile.cvUrl, 'profile', 'cv');
for (const m of models) {
  m.modelUrl = await download(m.modelUrl, 'models', m.slug);
  m.mobileModelUrl = await download(m.mobileModelUrl, 'models', `${m.slug}-mobile`);
  m.thumbnailUrl = await download(m.thumbnailUrl, 'thumbs', m.slug);
  for (const img of m.images) {
    img.url = await download(img.url, 'images', img.id);
    img.smallUrl = await download(img.smallUrl, 'images', `${img.id}-small`);
  }
}

const site = { generatedAt: new Date().toISOString(), profile, categories, tags, models };
await writeFile(dataFile, JSON.stringify(site, null, 2));
await writeSeoFiles(models);
console.log(`Showcase data: ${models.length} published models from ${api}`);
