/* Sitora front-end settings.
   API_BASE: address of the Sitora API, e.g. 'http://localhost:5194/api/v1'.
   Empty (the default) runs the built-in demo backend: no server needed; the address of a real API
   is entered in the page itself (gear button, bottom right) and remembered in the browser.
   ORG_SLUG: short name of the developer whose buildings and USD rate the public site shows
   ('nekruz' is the default organization, «Некруз Резиденс»). Empty — buildings of all developers on the platform. */
window.SITORA_CONFIG = {
  // The site served by the API itself (Render, Docker, dotnet run) talks to its own /api/v1: its CSP (connect-src 'self')
  // allows nothing else. The static copy on Vercel has no API of its own — see API_BASE_BY_HOST below.
  API_BASE: '/api/v1',
  ORG_SLUG: 'nekruz',
  DEFAULT_BUILDING_ID: null,
  CONTACT: {
    phone: '+992 98 363 70 70',
    telegram: '@nekru01',
    address: 'г. Душанбе, пр. Борбад, 15',
    // Contact page: the manager who answers, and the office hours day by day (day keys: monFri, sat, sun; null — day off).
    manager: 'Некруз',
    schedule: [['monFri', '9:00–19:00'], ['sat', '10:00–17:00'], ['sun', null]],
  },
  // Numbers of the house, prices and the installment terms are not set here: they come only from the API
  // (the catalog, GET /buildings → installment, set by the developer in the CRM).
  // Sites without their own API (the static copy on Vercel) use the API on Render. The site served by the API itself
  // (localhost, Docker) keeps its own /api/v1: its Content-Security-Policy (connect-src 'self') would block another host.
  // The API must allow the site in Cors:AllowedOrigins (on Render: Cors__AllowedOrigins__0=https://binosoz.vercel.app).
  API_BASE_BY_HOST: { 'binosoz.vercel.app': 'https://binosoz-1.onrender.com/api/v1' },
};
// The web fonts are linked with media="print" so they never hold up the first paint (index.html etc.); this first deferred
// script turns them on. They are preloaded, so they apply at once; until then the fallback fonts show (display=swap).
document.querySelectorAll('link[data-fonts]').forEach((l) => { l.media = 'all'; });
(function () {
  const cfg = window.SITORA_CONFIG;
  const api = cfg.API_BASE_BY_HOST[location.hostname];
  if (api) cfg.API_BASE = api;
})();
