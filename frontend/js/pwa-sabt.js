/* PWA: registers the Service Worker (/sw.js) — the site can be installed as an app and opens pages without the network.
   Only over HTTPS or on localhost (browsers allow Service Workers nowhere else); errors are not shown to the visitor. */
(function () {
  if (!('serviceWorker' in navigator) || !window.isSecureContext) return;
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js', { scope: '/' }).catch(() => { /* the site works without it */ });
  });
})();
