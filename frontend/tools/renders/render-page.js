// Draws the site's 3D model for one still picture: time of day and camera from the query string
// (?time=night&az=0.6&pol=1.36&distK=1&tyK=1&floors=20). render.mjs waits for window.__ready, then takes the screenshot.
// With complex.json next to this page (written by render.mjs from the blocks of the seed) — the whole complex «Rich
// Tower»: three blocks, the yard, all apartments free (a render shows the concept, not sales).
(function () {
  const q = new URLSearchParams(location.search);
  const num = (k) => (q.has(k) ? Number(q.get(k)) : undefined);
  const house = window.S.House3D.create(document.getElementById('stage'), {
    floors: num('floors') || 20, time: q.get('time') || 'dusk', autoRotate: false, overlay: false,
    view: { az: num('az'), pol: num('pol'), distK: num('distK'), tyK: num('tyK'), tx: num('tx') },
  });
  window.__house = house;
  const done = () => setTimeout(() => { window.__ready = true; }, Number(q.get('wait') || 3500));
  if (q.get('complex') !== '1') { done(); return; }
  fetch('complex.json', { cache: 'no-cache' }).then((r) => r.json()).then((c) => {
    house.setData(c.units, c.complex);
    // The camera after the scene is built (the view's distance depends on the complex's size).
    setTimeout(() => { if (q.has('view')) house.setView(q.get('view'), 0.01); done(); }, 600);
  }).catch((e) => { console.error(e); done(); });
})();
