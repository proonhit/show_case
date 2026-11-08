// Serves the generated site (.output/public) like a static host: /x → /x/index.html. No dependencies.
import { createReadStream, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { extname, join, normalize } from 'node:path';
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
        res.writeHead(200, { 'content-type': TYPES[extname(candidate)] ?? 'application/octet-stream' });
        createReadStream(candidate).pipe(res);
        return;
      }
    } catch { /* try the next one */ }
  }
  res.writeHead(404, { 'content-type': 'text/plain' }).end('Not found');
}).listen(port, () => console.log(`Showcase on http://localhost:${port}`));
