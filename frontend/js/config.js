/* Sitora front-end settings.
   API_BASE: address of the Sitora API, e.g. 'http://localhost:5194/api/v1'.
   Empty (the default) runs the built-in demo backend: no server needed; the address of a real API
   is entered in the page itself (gear button, bottom right) and remembered in the browser.
   ORG_SLUG: short name of the developer whose buildings and USD rate the public site shows
   ('nekruz' is the default organization, «Некруз Резиденс»). Empty — buildings of all developers on the platform. */
window.SITORA_CONFIG = {
  API_BASE: '',
  ORG_SLUG: 'nekruz',
  DEFAULT_BUILDING_ID: null,
  // Numbers for the landing's stats bar when the API is unavailable; with the API the real ones are shown.
  DEFAULT_FLOORS: 20,
  DEFAULT_APTS: 160,
  CONTACT: {
    phone: '+992 98 363 70 70',
    telegram: '@nekru01',
    address: 'г. Душанбе, пр. Борбад, 15',
    hours: 'Пн–Сб 9:00–19:00',
    // Contact page: the manager who answers, and the office hours day by day (day keys: monFri, sat, sun; null — day off).
    manager: 'Некруз',
    schedule: [['monFri', '9:00–19:00'], ['sat', '10:00–17:00'], ['sun', null]],
  },
  // Apartment types on the landing when the API is unavailable; with the API the real areas and prices are shown.
  // [rooms (0 — studio, 3 — three and more), area from, area to, price from]
  DEFAULT_TYPES: [[0, 28, 35, 250000], [1, 45, 58, 340000], [2, 65, 80, 480000], [3, 90, 110, 650000]],
  // Installment terms: the landing shows them, the catalog's calculator uses them as its limits.
  INSTALLMENT: { downFrom: 10, years: 10 },
  // Sites without their own API (the static copy on Vercel) use the API on Render. The site served by the API itself
  // (localhost, Docker) keeps its own /api/v1: its Content-Security-Policy (connect-src 'self') would block another host.
  // The API must allow the site in Cors:AllowedOrigins (on Render: Cors__AllowedOrigins__0=https://binosoz.vercel.app).
  API_BASE_BY_HOST: { 'binosoz.vercel.app': 'https://binosoz-1.onrender.com/api/v1' },
};
(function () {
  const cfg = window.SITORA_CONFIG;
  const api = cfg.API_BASE_BY_HOST[location.hostname];
  if (api) cfg.API_BASE = api;
})();
