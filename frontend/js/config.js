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
    phone: '+992 00 000 00 00',
    telegram: '@sitora_sales',
    address: 'Душанбе, пр. Рудаки, 124',
    hours: '',
  },
};
