/* Service Worker of the site: lets the site be installed as an app (PWA) and opens pages without the network.
   Strategy — network first: online, every page, script and style comes from the server as before (no stale code
   after a deploy); the copy in the cache is used only when the network fails. Offline and never opened before —
   the page /offline.html.
   Never touched: the API (/api, /hubs, /health), files by signed links (/files), Swagger, other hosts (the API on
   Render, fonts, cdnjs) and non-GET requests. Tokens and API answers never reach the cache. */
const CACHE = 'sitora-v2';
const OFFLINE = '/offline.html';
const PRECACHE = [OFFLINE, '/css/styles.css', '/favicon.svg', '/manifest.json'];
const SKIP = ['/api/', '/hubs/', '/health', '/files/', '/swagger'];
const ASSETS = ['script', 'style', 'image', 'font', 'manifest'];

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE).then((c) => c.addAll(PRECACHE)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (event) => {
  event.waitUntil(caches.keys()
    .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
    .then(() => self.clients.claim()));
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return;
  if (SKIP.some((p) => url.pathname === p || url.pathname.startsWith(p))) return;
  const isPage = req.mode === 'navigate' && req.destination === 'document';
  if (!isPage && !ASSETS.includes(req.destination)) return;

  event.respondWith((async () => {
    try {
      // no-cache: always revalidate with the server (ETag → a cheap 304), so a deploy is never hidden by the HTTP cache.
      const res = await fetch(req, { cache: 'no-cache' });
      // Only whole successful answers of our own site; a 404 page or an error is not kept.
      if (res.ok && res.type === 'basic') {
        const copy = res.clone();
        caches.open(CACHE).then((c) => c.put(req, copy)).catch(() => {});
      }
      return res;
    } catch (e) {
      const cached = await caches.match(req, { ignoreSearch: isPage });
      if (cached) return cached;
      if (isPage) { const offline = await caches.match(OFFLINE); if (offline) return offline; }
      throw e;
    }
  })());
});
