// Serves the generated site (.output/public) like a static host (Cloudflare Pages):
// /x → /x/index.html, brotli/gzip for text files. No dependencies.
import { createReadStream, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { extname, join, normalize } from 'node:path';
import { createBrotliCompress, createGzip } from 'node:zlib';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../.output/public', import.meta.url));
const port = Number(process.env.PORT ?? 5175);
const TYPES = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json',
  '.glb': 'model/gltf-binary', '.png': 'image/png', '.jpg': 'image/jpeg', '.webp': 'image/webp', '.svg': 'image/svg+xml',
  '.pdf': 'application/pdf', '.xml': 'application/xml', '.txt': 'text/plain', '.ico': 'image/x-icon',
};

createServer((req, res) => {
  const path = normalize(decodeURIComponent(new URL(req.url ?? '/', 'http://x').pathname)).replace(/^(\.\.[/\\])+/, '');
  for (const candidate of [join(root, path), join(root, path, 'index.html'), join(root, `${path}.html`)]) {
    try {
      if (statSync(candidate).isFile()) {
        const type = TYPES[extname(candidate)] ?? 'application/octet-stream';
        const accept = String(req.headers['accept-encoding'] ?? '');
        const text = /^(text\/|application\/(json|xml)|image\/svg)/.test(type);
        const encoding = !text ? null : accept.includes('br') ? 'br' : accept.includes('gzip') ? 'gzip' : null;
        const immutable = candidate.includes(`${'/'}_nuxt${'/'}`);
        res.writeHead(200, {
          'content-type': type,
          'cache-control': immutable ? 'public, max-age=31536000, immutable' : 'public, max-age=0, must-revalidate',
          ...(encoding ? { 'content-encoding': encoding, vary: 'accept-encoding' } : {}),
        });
        const file = createReadStream(candidate);
        if (encoding === 'br') file.pipe(createBrotliCompress()).pipe(res);
        else if (encoding === 'gzip') file.pipe(createGzip()).pipe(res);
        else file.pipe(res);
        return;
      }
    } catch { /* try the next one */ }
  }
  res.writeHead(404, { 'content-type': 'text/plain' }).end('Not found');
}).listen(port, () => console.log(`Showcase on http://localhost:${port}`));
