/* Sitora front-end settings.
   API_BASE: address of the Sitora API, e.g. 'http://localhost:5194/api/v1'.
   Leave it empty to run the built-in demo backend (no server needed).
   The address can also be changed in the page itself (gear button, bottom right).
   ORG_SLUG: short name of the developer whose buildings and USD rate the public site shows
   ('sitora' is the default organization). Empty — buildings of all developers on the platform. */
window.SITORA_CONFIG = {
  API_BASE: 'http://localhost:5194/api/v1',
  ORG_SLUG: 'sitora',
  DEFAULT_BUILDING_ID: null,
  CONTACT: {
    phone: '+992 00 000 00 00',
    telegram: '@sitora_sales',
    address: 'Душанбе, пр. Рудаки, 124',
    hours: '',
  },
};
