/* WCAG AA contrast check of every visible text on the site's public pages.
   How to run (no installs): open any page of the site in a browser (http://localhost:5500 with
   `node scripts/serve-web.mjs`, or the deployed site), open DevTools → Console, paste this whole file and run:
     await checkSite()                       // all public pages, light theme, 1280px wide
     await checkSite({ theme: 'dark' })      // the same in the dark theme
     await checkSite({ width: 375 })         // phone width
   Each page is loaded in a hidden iframe of the same origin; the result is a table: page, texts checked,
   texts below AA (4.5:1, or 3:1 for large text) and the worst ones. Text over a photo or gradient is not measured
   by colour; dark text over such a background is reported separately ("dark on image"). */
(function () {
  const PAGES = ['/', '/apartments', '/about', '/gallery', '/contact', '/register', '/login', '/404.html', '/payment.html', '/offline.html'];
  const parse = (c) => { const m = c && c.match(/rgba?\(([^)]+)\)/); if (!m) return null; const p = m[1].split(/[ ,/]+/).filter(Boolean).map(Number); return { r: p[0], g: p[1], b: p[2], a: p.length > 3 ? p[3] : 1 }; };
  const lum = (c) => { const f = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }; return 0.2126 * f(c.r) + 0.7152 * f(c.g) + 0.0722 * f(c.b); };
  const blend = (t, b) => ({ r: t.r * t.a + b.r * (1 - t.a), g: t.g * t.a + b.g * (1 - t.a), b: t.b * t.a + b.b * (1 - t.a), a: 1 });

  function checkDocument(d, w) {
    const bgOf = (el) => {
      const layers = [];
      for (let e = el; e; e = e.parentElement) {
        const cs = w.getComputedStyle(e); const c = parse(cs.backgroundColor);
        if (cs.backgroundImage !== 'none' && !(c && c.a >= 0.99)) return { img: true };
        if (c && c.a > 0) { layers.push(c); if (c.a >= 0.99) break; }
      }
      let base = { r: 255, g: 255, b: 255, a: 1 };
      for (let i = layers.length - 1; i >= 0; i--) base = blend(layers[i], base);
      return { color: base };
    };
    const fails = [], darkOnImage = [], seen = new Set(); let total = 0;
    const tw = d.createTreeWalker(d.body, NodeFilter.SHOW_TEXT);
    while (tw.nextNode()) {
      const txt = tw.currentNode.textContent.trim(); if (!txt) continue;
      const el = tw.currentNode.parentElement; if (!el || seen.has(el)) continue; seen.add(el);
      if (el.closest('script,style,noscript,svg')) continue;
      const cs = w.getComputedStyle(el); if (cs.visibility === 'hidden' || cs.display === 'none' || +cs.opacity === 0) continue;
      const r = el.getBoundingClientRect(); if (!r.width || !r.height) continue;
      total++;
      const fg = parse(cs.color), bg = bgOf(el);
      const name = ((typeof el.className === 'string' && el.className) || el.tagName) + ': ' + txt.slice(0, 32);
      if (!fg) continue;
      if (bg.img) { if (lum(fg) < 0.2) darkOnImage.push(name); continue; }
      const f = blend(fg, bg.color), L1 = lum(f), L2 = lum(bg.color);
      const ratio = (Math.max(L1, L2) + 0.05) / (Math.min(L1, L2) + 0.05);
      const size = parseFloat(cs.fontSize), large = size >= 24 || (size >= 18.66 && +cs.fontWeight >= 700);
      if (ratio < (large ? 3 : 4.5)) fails.push(ratio.toFixed(2) + ' ' + name);
    }
    fails.sort((a, b) => parseFloat(a) - parseFloat(b));
    return { total, fails, darkOnImage, hscroll: d.documentElement.scrollWidth > w.innerWidth };
  }

  async function checkSite(opts) {
    const o = Object.assign({ pages: PAGES, theme: 'light', width: 1280 }, opts);
    let saved = null; try { saved = localStorage.getItem('sitora.theme'); localStorage.setItem('sitora.theme', o.theme); } catch (e) { /* private mode */ }
    const rows = [];
    try {
      for (const p of o.pages) {
        const f = document.createElement('iframe');
        f.style.cssText = `width:${o.width}px;height:900px;position:fixed;left:-${o.width + 100}px;top:0`;
        f.src = p + (p.includes('?') ? '&' : '?') + 'contrast=' + Date.now();
        document.body.appendChild(f);
        await new Promise((r) => { f.onload = r; });
        await new Promise((r) => setTimeout(r, 1500));
        // Reveal animations: show everything as if scrolled to.
        f.contentDocument.querySelectorAll('.reveal, .section-reveal').forEach((e) => e.classList.add('visible', 'in'));
        const res = checkDocument(f.contentDocument, f.contentWindow);
        rows.push({ page: p, theme: f.contentDocument.documentElement.dataset.theme, texts: res.total, belowAA: res.fails.length, hscroll: res.hscroll, worst: res.fails.slice(0, 6).join(' | '), darkOnImage: res.darkOnImage.join(' | ') });
        f.remove();
      }
    } finally {
      try { if (saved === null) localStorage.removeItem('sitora.theme'); else localStorage.setItem('sitora.theme', saved); } catch (e) { /* ignore */ }
    }
    if (console.table) console.table(rows);
    return rows;
  }

  window.checkSite = checkSite;
  window.checkContrastDocument = checkDocument;
})();
