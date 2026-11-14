// Draws the site's 3D model for one still picture: time of day and camera from the query string
// (?time=night&az=0.6&pol=1.36&distK=1&tyK=1&floors=20). render.mjs waits for window.__ready, then takes the screenshot.
(function () {
  const q = new URLSearchParams(location.search);
  const num = (k) => (q.has(k) ? Number(q.get(k)) : undefined);
  const house = window.S.House3D.create(document.getElementById('stage'), {
    floors: num('floors') || 20, time: q.get('time') || 'dusk', autoRotate: false, overlay: false,
    view: { az: num('az'), pol: num('pol'), distK: num('distK'), tyK: num('tyK'), tx: num('tx') },
  });
  window.__house = house;
  // The environment map is rebuilt a moment after the start; a few seconds later the picture is final.
  setTimeout(() => { window.__ready = true; }, Number(q.get('wait') || 3500));
})();
