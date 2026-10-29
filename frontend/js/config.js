/* Sitora front-end settings.
   API_BASE: address of the Sitora API, e.g. 'http://localhost:5194/api/v1'.
   Empty (the default) runs the built-in demo backend: no server needed; the address of a real API
   is entered in the page itself (gear button, bottom right) and remembered in the browser.
   ORG_SLUG: short name of the developer whose buildings and USD rate the public site shows
   ('sitora' is the default organization). Empty — buildings of all developers on the platform. */
window.SITORA_CONFIG = {
  API_BASE: '',
  ORG_SLUG: 'sitora',
  DEFAULT_BUILDING_ID: null,
  // Numbers for the landing's stats bar when the API is unavailable; with the API the real ones are shown.
  DEFAULT_FLOORS: 16,
  DEFAULT_APTS: 128,
  CONTACT: {
    phone: '+992 98 363 70 70',
    telegram: '@nekru01',
    address: 'Душанбе, пр. Рудаки, 124',
    hours: 'Пн–Сб 9:00–19:00',
    // Contact page: the manager who answers, and the office hours day by day (day keys: monFri, sat, sun; null — day off).
    manager: 'Некру',
    schedule: [['monFri', '9:00–19:00'], ['sat', '10:00–17:00'], ['sun', null]],
  },
  // Apartment types on the landing when the API is unavailable; with the API the real areas and prices are shown.
  // [rooms (0 — studio, 3 — three and more), area from, area to, price from]
  DEFAULT_TYPES: [[0, 28, 35, 250000], [1, 45, 58, 340000], [2, 65, 80, 480000], [3, 90, 110, 650000]],
  // Installment terms: the landing shows them, the catalog's calculator uses them as its limits.
  INSTALLMENT: { downFrom: 10, years: 10 },
};
