/* Home page: Three.js and the 3D model (modeli-3d-bino-script.js) load only when the 3D section is near the screen,
   or after 3 seconds if the page has no such section. A separate file, not an inline script: the site's
   Content-Security-Policy (script-src 'self' https://cdnjs.cloudflare.com) blocks inline code. */
(function () {
  var loaded = false;
  function load3D() {
    if (loaded) return; loaded = true;
    var s = document.createElement('script');
    s.src = 'https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js';
    s.onload = function () {
      var s2 = document.createElement('script');
      s2.src = '/js/modeli-3d-bino-script.js';
      document.head.appendChild(s2);
    };
    document.head.appendChild(s);
  }
  var ob = window.IntersectionObserver && new IntersectionObserver(function (entries) {
    if (entries[0].isIntersecting) { load3D(); ob.disconnect(); }
  }, { rootMargin: '300px' });
  function start() {
    var el = document.getElementById('model3d-wrap') || document.querySelector('.wow-3d');
    if (el && ob) ob.observe(el); else setTimeout(load3D, 3000);
  }
  // Loaded with defer: the DOM is parsed, but DOMContentLoaded may not have fired yet.
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
})();
