// Статик-сервер для папки frontend/ в разработке, без зависимостей: node scripts/serve-web.mjs [порт]
// Отдаёт файлы как есть и без кэша, чтобы правки JS и CSS были видны после обновления страницы.
import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { extname, join, normalize, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('../frontend', import.meta.url)));
const port = Number(process.argv[2] ?? process.env.PORT ?? 5500);

const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
  '.ico': 'image/x-icon',
  '.woff2': 'font/woff2',
  '.glb': 'model/gltf-binary',
};

createServer(async (req, res) => {
  try {
    const path = decodeURIComponent(new URL(req.url ?? '/', 'http://x').pathname);
    let file = normalize(join(root, path));
    // Не выпускаем запрос за пределы frontend/.
    if (file !== root && !file.startsWith(root + sep)) {
      res.writeHead(403).end();
      return;
    }
    const found = await stat(file).catch(() => null);
    if (found?.isDirectory()) file = join(file, 'index.html');
    // Адреса страниц без .html (/login, /crm), как на Vercel и в API: по ним ведут ссылки сайта.
    else if (!found && !extname(file)) file += '.html';
    const body = await readFile(file);
    res.writeHead(200, { 'Content-Type': types[extname(file).toLowerCase()] ?? 'application/octet-stream', 'Cache-Control': 'no-store' });
    res.end(body);
  } catch {
    res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' }).end('Не найдено');
  }
}).listen(port, () => console.log(`frontend/ доступен на http://localhost:${port}`));
