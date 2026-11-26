/* offline.html in the language the visitor chose (localStorage 'sitora.lang'; A-25). No API and no dictionaries: the
   page is shown without a network, so its few texts live here, and this file is in the Service Worker's precache.
   Without this script the page stays Russian with a line in Tajik and English under it. */
(function () {
  const T = {
    ru: { title: 'Нет подключения', text: 'Проверьте интернет и обновите страницу. Страницы, которые вы уже открывали, доступны и без сети.', reload: 'Обновить', brand: 'Rich Tower' },
    tg: { title: 'Пайвастшавӣ нест', text: 'Интернетро санҷед ва саҳифаро аз нав кушоед. Саҳифаҳое, ки шумо аллакай кушода будед, бе шабака ҳам дастрасанд.', reload: 'Аз нав кушодан', brand: 'Rich Tower' },
    en: { title: 'No connection', text: 'Check the internet and reload the page. The pages you have already opened work without the network.', reload: 'Reload', brand: 'Rich Tower' },
  };
  let lang = 'ru';
  try { const v = window.localStorage.getItem('sitora.lang'); if (v && T[v]) lang = v; } catch (e) { /* storage may be blocked */ }
  try { const q = new URLSearchParams(location.search).get('lang'); if (q && T[q]) lang = q; } catch (e) { /* old browser */ }
  const x = T[lang];
  document.documentElement.lang = lang;
  document.title = x.title + ' — ' + x.brand;
  document.querySelectorAll('[data-l]').forEach((el) => { el.textContent = x[el.getAttribute('data-l')]; });
  document.querySelectorAll('[data-fallback]').forEach((el) => { el.hidden = true; });
  const reload = document.querySelector('[data-l="reload"]');
  if (reload) reload.addEventListener('click', (e) => { e.preventDefault(); location.reload(); });
})();
