/* Sitora 3D tower: realistic residential building on three.js r128.
   Instanced windows are the pick targets (one per apartment). */
(function () {
  const S = (window.S = window.S || {});
  const T = window.THREE;

  const FLOOR_H = 3.4, GROUND_H = 5.5, W = 38, D = 26;
  const BAY = { 0: [-W / 4, D / 2, 0], 1: [W / 4, D / 2, 0] }; // placeholder, rebuilt below

  // apartment slot -> face placement: [x, z, rotationY, bayWidth]
  const SLOTS = [
    [-W / 4, D / 2, 0, W / 2],
    [W / 4, D / 2, 0, W / 2],
    [W / 2, D / 4, Math.PI / 2, D / 2],
    [W / 2, -D / 4, Math.PI / 2, D / 2],
    [W / 4, -D / 2, Math.PI, W / 2],
    [-W / 4, -D / 2, Math.PI, W / 2],
    [-W / 2, -D / 4, -Math.PI / 2, D / 2],
    [-W / 2, D / 4, -Math.PI / 2, D / 2],
  ];

  const STATUS_COLOR = {
    free: 0x2fd6a3,
    booked: 0xffb030,
    sold: 0x252c3b,
    onrequest: 0x6f8bff,
  };

  const PRESETS = {
    // Dushanbe midday: strong Central Asian sun, pale blue sky, cream facade looks brilliant white
    day: {
      top: 0x3a8ae0, horizon: 0xd8ecf8, bottom: 0x8aaa7a, fog: 0xc8dff0,
      sunColor: 0xfff5e0, sunI: 2.4, sunPos: [80, 100, 70], hemiSky: 0xc8e0f4, hemiGround: 0x788868, hemiI: 0.65,
      glow: 0.18, lamp: 0, stars: 0, cloud: 0.85, exposure: 0.92, env: 1.1,
    },
    // Golden hour Dushanbe — warm orange sky, building glows
    dusk: {
      top: 0x1a2c6e, horizon: 0xf2a066, bottom: 0x3b3f55, fog: 0x8b86a4,
      sunColor: 0xffa860, sunI: 2.1, sunPos: [-120, 26, 70], hemiSky: 0x6f86c9, hemiGround: 0x4a4a55, hemiI: 0.6,
      glow: 0.85, lamp: 1.0, stars: 0.35, cloud: 0.55, exposure: 1.05, env: 1.0,
    },
    // Dushanbe night: deep navy sky, windows glow warmly, street lamps orange
    night: {
      top: 0x030810, horizon: 0x142040, bottom: 0x08100e, fog: 0x081220,
      sunColor: 0x7090d0, sunI: 0.3, sunPos: [60, 70, 40], hemiSky: 0x1a2a60, hemiGround: 0x060a08, hemiI: 0.25,
      glow: 1.4, lamp: 1.9, stars: 1, cloud: 0.08, exposure: 1.05, env: 0.6,
    },
  };

  const lin = (hex) => new T.Color(hex).convertSRGBToLinear();

  function rnd(seed) {
    let s = seed >>> 0;
    return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296);
  }

  function canvasTex(w, h, draw, repeat) {
    const c = document.createElement('canvas');
    c.width = w; c.height = h;
    draw(c.getContext('2d'), w, h);
    const t = new T.CanvasTexture(c);
    t.encoding = T.sRGBEncoding;
    t.wrapS = t.wrapT = T.RepeatWrapping;
    t.anisotropy = 4;
    if (repeat) t.repeat.set(repeat[0], repeat[1]);
    return t;
  }

  function plasterTexture(r) {
    // Warm cream stucco — Dushanbe apartment standard (similar to Ayni, Somoni districts)
    return canvasTex(512, 512, (g, w, h) => {
      g.fillStyle = '#ece6d8'; g.fillRect(0, 0, w, h); // base: warm cream
      // Subtle grain / texture noise
      for (let i = 0; i < 8000; i++) {
        const v = 210 + (r() * 35 | 0);
        g.fillStyle = `rgba(${v},${v - 3},${v - 10},${0.05 + r() * 0.09})`;
        g.fillRect(r() * w, r() * h, 1 + r() * 2, 1 + r() * 2);
      }
      // Faint weathering / runoff streaks
      for (let i = 0; i < 30; i++) {
        g.fillStyle = `rgba(100,88,70,${0.01 + r() * 0.015})`;
        g.fillRect(r() * w, 0, 1 + r() * 3, h);
      }
      // Panel joint lines (precast panel seams, every 2 m)
      g.fillStyle = 'rgba(60,55,48,0.18)';
      g.fillRect(0, h - 3, w, 3);       // bottom slab line
      g.fillRect(0, 0, 2, h);            // left panel joint
      g.fillRect(w / 2, 0, 2, h);        // center panel joint
      g.fillRect(w - 2, 0, 2, h);        // right edge
    });
  }

  function asphaltTexture(r) {
    return canvasTex(256, 256, (g, w, h) => {
      g.fillStyle = '#3a3d44'; g.fillRect(0, 0, w, h);
      for (let i = 0; i < 6000; i++) {
        const v = 40 + (r() * 50 | 0);
        g.fillStyle = `rgba(${v},${v},${v + 4},${0.25 + r() * 0.3})`;
        g.fillRect(r() * w, r() * h, 1 + r(), 1 + r());
      }
    });
  }

  function grassTexture(r) {
    return canvasTex(256, 256, (g, w, h) => {
      g.fillStyle = '#4d6e3d'; g.fillRect(0, 0, w, h);
      for (let i = 0; i < 9000; i++) {
        const gr = 90 + (r() * 60 | 0);
        g.fillStyle = `rgba(${50 + (r() * 30 | 0)},${gr},${40},${0.25 + r() * 0.3})`;
        g.fillRect(r() * w, r() * h, 1, 2 + r() * 3);
      }
    });
  }

  function paverTexture(r) {
    return canvasTex(256, 256, (g, w, h) => {
      g.fillStyle = '#b9b4aa'; g.fillRect(0, 0, w, h);
      g.strokeStyle = 'rgba(70,66,60,0.35)'; g.lineWidth = 2;
      for (let x = 0; x <= w; x += 64) { g.beginPath(); g.moveTo(x, 0); g.lineTo(x, h); g.stroke(); }
      for (let y = 0; y <= h; y += 32) { g.beginPath(); g.moveTo(0, y); g.lineTo(w, y); g.stroke(); }
      for (let i = 0; i < 1500; i++) {
        g.fillStyle = `rgba(80,75,68,${r() * 0.12})`;
        g.fillRect(r() * w, r() * h, 2, 2);
      }
    });
  }

  function skylineTextures(seed) {
    const cells = [];
    const r = rnd(seed);
    for (let y = 0; y < 32; y++) for (let x = 0; x < 16; x++) cells.push([r(), r(), r()]);
    const alb = canvasTex(256, 512, (g, w, h) => {
      g.fillStyle = '#8a8f99'; g.fillRect(0, 0, w, h);
      cells.forEach((c, i) => { const x = (i % 16) * 16, y = Math.floor(i / 16) * 16; g.fillStyle = '#1d2433'; g.fillRect(x + 3, y + 3, 10, 10); });
    });
    const em = canvasTex(256, 512, (g, w, h) => {
      g.fillStyle = '#000'; g.fillRect(0, 0, w, h);
      cells.forEach((c, i) => { if (c[0] > 0.62) { const x = (i % 16) * 16, y = Math.floor(i / 16) * 16; g.fillStyle = `rgb(255,${180 + (c[1] * 60 | 0)},${110 + (c[2] * 70 | 0)})`; g.fillRect(x + 3, y + 3, 10, 10); } });
    });
    return { alb, em };
  }

  function signTexture(text) {
    return canvasTex(1024, 192, (g, w, h) => {
      g.fillStyle = '#0d1426'; g.fillRect(0, 0, w, h);
      g.fillStyle = '#f2c46a';
      g.font = '700 112px Unbounded, Inter, Arial, sans-serif';
      g.textAlign = 'center'; g.textBaseline = 'middle';
      g.fillText(text, w / 2, h / 2 + 6);
      g.strokeStyle = 'rgba(242,196,106,.5)'; g.lineWidth = 4;
      g.strokeRect(12, 12, w - 24, h - 24);
    });
  }

  function cloudTexture() {
    return canvasTex(256, 128, (g, w, h) => {
      const grd = g.createRadialGradient(w / 2, h / 2, 4, w / 2, h / 2, w / 2);
      grd.addColorStop(0, 'rgba(255,255,255,0.95)');
      grd.addColorStop(0.5, 'rgba(255,255,255,0.45)');
      grd.addColorStop(1, 'rgba(255,255,255,0)');
      g.fillStyle = grd; g.fillRect(0, 0, w, h);
      for (let i = 0; i < 5; i++) {
        const gx = 40 + i * 42, gy = h / 2 + (i % 2 ? 6 : -6);
        const gg = g.createRadialGradient(gx, gy, 2, gx, gy, 46);
        gg.addColorStop(0, 'rgba(255,255,255,0.7)'); gg.addColorStop(1, 'rgba(255,255,255,0)');
        g.fillStyle = gg; g.fillRect(0, 0, w, h);
      }
    });
  }

  function vnoise(x, z, seed) {
    const h = (a, b) => {
      let n = (a * 374761393 + b * 668265263 + seed * 144665) | 0;
      n = (n ^ (n >> 13)) * 1274126177;
      return ((n ^ (n >> 16)) >>> 0) / 4294967295;
    };
    const xi = Math.floor(x), zi = Math.floor(z), xf = x - xi, zf = z - zi;
    const u = xf * xf * (3 - 2 * xf), v = zf * zf * (3 - 2 * zf);
    const a = h(xi, zi), b = h(xi + 1, zi), c = h(xi, zi + 1), d = h(xi + 1, zi + 1);
    return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
  }

  function create(container, opts) {
    opts = opts || {};
    const handlers = { select: opts.onSelectApartment || (() => {}), floor: opts.onSelectFloor || (() => {}), hover: opts.onHover || (() => {}) };
    const rand = rnd(7);

    const renderer = new T.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    // Phones and tablets draw at most 1.5 device pixels per CSS pixel: sharp enough, and much lighter for a budget GPU.
    const smallScreen = (window.matchMedia && window.matchMedia('(max-width: 1024px), (pointer: coarse)').matches);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, smallScreen ? 1.5 : 2));
    renderer.outputEncoding = T.sRGBEncoding;
    renderer.toneMapping = T.ACESFilmicToneMapping;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = T.PCFSoftShadowMap;
    renderer.domElement.className = 'house3d-canvas';
    renderer.domElement.style.touchAction = 'none';
    container.appendChild(renderer.domElement);

    const scene = new T.Scene();
    const camera = new T.PerspectiveCamera(38, 1, 0.5, 2500);
    scene.fog = new T.Fog(lin(0x8b86a4), 140, 1100);

    // ---------- lights ----------
    const hemi = new T.HemisphereLight(0xffffff, 0x444444, 0.6);
    scene.add(hemi);
    const sun = new T.DirectionalLight(0xffffff, 2);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    const sc = sun.shadow.camera;
    sc.left = -75; sc.right = 75; sc.top = 75; sc.bottom = -75; sc.near = 10; sc.far = 420;
    sun.shadow.bias = -0.0004; sun.shadow.normalBias = 0.05;
    scene.add(sun); scene.add(sun.target);

    // ---------- sky ----------
    const skyMat = new T.ShaderMaterial({
      side: T.BackSide, depthWrite: false, fog: false,
      uniforms: {
        top: { value: lin(0x1a2c6e) }, horizon: { value: lin(0xf2a066) }, bottom: { value: lin(0x3b3f55) },
        sunDir: { value: new T.Vector3(0, 1, 0) }, sunColor: { value: lin(0xffa860) },
      },
      vertexShader: 'varying vec3 vDir; void main(){ vDir = normalize(position); gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
      fragmentShader: `uniform vec3 top; uniform vec3 horizon; uniform vec3 bottom; uniform vec3 sunDir; uniform vec3 sunColor; varying vec3 vDir;
        void main(){ vec3 d = normalize(vDir); float h = d.y;
          vec3 c = h > 0.0 ? mix(horizon, top, pow(h, 0.5)) : mix(horizon, bottom, pow(-h, 0.35));
          float s = max(dot(d, normalize(sunDir)), 0.0);
          c += sunColor * (pow(s, 90.0) * 1.4 + pow(s, 8.0) * 0.28);
          gl_FragColor = vec4(c, 1.0);
          #include <tonemapping_fragment>
          #include <encodings_fragment>
        }`,
    });
    const sky = new T.Mesh(new T.SphereGeometry(1800, 32, 18), skyMat);
    sky.renderOrder = -10;
    scene.add(sky);

    const starGeo = new T.BufferGeometry();
    {
      const p = [];
      for (let i = 0; i < 900; i++) {
        const u = rand() * Math.PI * 2, v = Math.acos(rand() * 0.92 + 0.08);
        p.push(1700 * Math.sin(v) * Math.cos(u), 1700 * Math.cos(v), 1700 * Math.sin(v) * Math.sin(u));
      }
      starGeo.setAttribute('position', new T.Float32BufferAttribute(p, 3));
    }
    const starMat = new T.PointsMaterial({ color: 0xffffff, size: 3.2, sizeAttenuation: false, transparent: true, opacity: 0, fog: false, depthWrite: false });
    scene.add(new T.Points(starGeo, starMat));

    const cloudTex = cloudTexture();
    const clouds = [];
    for (let i = 0; i < 8; i++) {
      const m = new T.SpriteMaterial({ map: cloudTex, transparent: true, opacity: 0.5, fog: false, depthWrite: false });
      const sp = new T.Sprite(m);
      sp.scale.set(300 + rand() * 260, 90 + rand() * 40, 1);
      sp.position.set(-700 + rand() * 1400, 170 + rand() * 120, -300 - rand() * 600);
      sp.userData.v = 1.2 + rand() * 1.8;
      scene.add(sp); clouds.push(sp);
    }

    // ---------- ground & terrain ----------
    const grassT = grassTexture(rand); grassT.repeat.set(90, 90);
    const lawn = new T.Mesh(new T.PlaneGeometry(900, 700), new T.MeshStandardMaterial({ map: grassT, color: lin(0xa3ab94), roughness: 1, metalness: 0 }));
    lawn.rotation.x = -Math.PI / 2; lawn.position.set(0, 0, -130); lawn.receiveShadow = true;
    scene.add(lawn);

    const asphT = asphaltTexture(rand); asphT.repeat.set(120, 4);
    const road = new T.Mesh(new T.PlaneGeometry(900, 14), new T.MeshStandardMaterial({ map: asphT, roughness: 0.92 }));
    road.rotation.x = -Math.PI / 2; road.position.set(0, 0.03, 40); road.receiveShadow = true;
    scene.add(road);
    {
      const dash = new T.InstancedMesh(new T.BoxGeometry(5, 0.02, 0.28), new T.MeshStandardMaterial({ color: lin(0xe7e2d2), roughness: 0.7 }), 90);
      const m = new T.Matrix4();
      for (let i = 0; i < 90; i++) { m.makeTranslation(-440 + i * 10, 0.07, 40); dash.setMatrixAt(i, m); }
      dash.frustumCulled = false; scene.add(dash);
    }
    const paveT = paverTexture(rand); paveT.repeat.set(60, 3);
    const walk = new T.Mesh(new T.BoxGeometry(900, 0.14, 8), new T.MeshStandardMaterial({ map: paveT, roughness: 0.9 }));
    walk.position.set(0, 0.07, 30); walk.receiveShadow = true; scene.add(walk);
    const paveT2 = paverTexture(rand); paveT2.repeat.set(10, 5);
    const plaza = new T.Mesh(new T.BoxGeometry(64, 0.16, 20), new T.MeshStandardMaterial({ map: paveT2, roughness: 0.85 }));
    plaza.position.set(0, 0.08, D / 2 + 10); plaza.receiveShadow = true; scene.add(plaza);

    // mountains (Hissar-style ridges behind the city)
    {
      const g = new T.PlaneGeometry(2600, 1100, 150, 70);
      g.rotateX(-Math.PI / 2);
      const pos = g.attributes.position, col = [];
      const c1 = lin(0x4d5a52), c2 = lin(0x7c7468), c3 = lin(0xf2f4f8);
      for (let i = 0; i < pos.count; i++) {
        const x = pos.getX(i), z = pos.getZ(i) - 480; // z in [-1030, 70]
        const dz = -z - 260; // distance behind lawn edge
        let y = 0;
        if (dz > 0) {
          const f = Math.min(1, dz / 400);
          const n = vnoise(x / 190, z / 190, 3) * 0.55 + vnoise(x / 70, z / 70, 9) * 0.3 + vnoise(x / 28, z / 28, 5) * 0.15;
          const ridge = 1 - Math.abs(vnoise(x / 260 + 5, z / 260, 11) * 2 - 1);
          y = (n * 150 + ridge * ridge * 260) * f * f;
        }
        pos.setXYZ(i, x, y - 0.4, z);
        const t = Math.min(1, y / 240);
        const c = t < 0.5 ? c1.clone().lerp(c2, t * 2) : c2.clone().lerp(c3, Math.min(1, (t - 0.5) * 3.2));
        col.push(c.r, c.g, c.b);
      }
      g.setAttribute('color', new T.Float32BufferAttribute(col, 3));
      g.computeVertexNormals();
      const mt = new T.Mesh(g, new T.MeshStandardMaterial({ vertexColors: true, roughness: 1, flatShading: true }));
      scene.add(mt);
    }

    // neighbour towers (skyline)
    {
      const spots = [[60, -215, 30, 62, 26], [150, -190, 28, 50, 24], [-40, -255, 34, 78, 28], [-190, -235, 30, 58, 26], [250, -170, 30, 66, 26], [-120, -300, 36, 70, 30]];
      const roofM = new T.MeshStandardMaterial({ color: lin(0x6f737c), roughness: 0.9 });
      neighbourMat = [];
      spots.forEach(([x, z, w, h, d], i) => {
        const tx = skylineTextures(40 + i);
        const mk = (rx) => { const a = tx.alb.clone(), e = tx.em.clone(); a.needsUpdate = e.needsUpdate = true; a.repeat.set(rx / 24, h / 32); e.repeat.copy(a.repeat); return new T.MeshStandardMaterial({ map: a, emissiveMap: e, emissive: new T.Color(0xffffff), emissiveIntensity: 0.05, roughness: 0.75 }); };
        const mA = mk(w), mB = mk(d); neighbourMat.push(mA, mB);
        const b = new T.Mesh(new T.BoxGeometry(w, h, d), [mB, mB, roofM, roofM, mA, mA]);
        b.position.set(x, h / 2 - 3, z); b.castShadow = true; b.receiveShadow = true; scene.add(b);
      });
    }
    var neighbourMat;

    // ---------- the tower ----------
    const tower = new T.Group();
    scene.add(tower);
    let pickGroup = [];
    let apartmentInst = []; // instance index -> {floor, slot, id}
    let floorsCount = 16;
    let glass, glow, frames, mullions, slabsMesh, railFront, railSide;
    let selBand, selEdges, hoverIdx = -1, selectedIdx = -1;
    let roofLight, lampBulbs = [], lampMat, signMat;
    let overlay = true;
    let aptMap = {}; // id -> inst
    let keyMap = {}; // "floor:slot" -> inst
    let apartments = [];

    const mats = {
      plaster: null,
      // Gold accent — Dushanbe landmark color (Ayni Opera, Ismoili Somoni monument)
      stone: new T.MeshStandardMaterial({ color: lin(0xc9a227), roughness: 0.45, metalness: 0.15 }),
      // Upper-body concrete/stucco — warm cream/beige Dushanbe apartment style
      concrete: new T.MeshStandardMaterial({ color: lin(0xd8d2c4), roughness: 0.82 }),
      // Window frames: anthracite aluminum
      frame: new T.MeshStandardMaterial({ color: lin(0x232830), roughness: 0.3, metalness: 0.78 }),
      // Lobby / entrance glass: dark blue architectural
      glassWall: new T.MeshStandardMaterial({ color: lin(0x1a3558), roughness: 0.03, metalness: 0.98, transparent: true, opacity: 0.72 }),
      // Balcony railings: tinted blue glass
      railGlass: new T.MeshStandardMaterial({ color: lin(0x3a6a98), roughness: 0.04, metalness: 0.6, transparent: true, opacity: 0.28 }),
      // Roof membrane: dark charcoal
      roof: new T.MeshStandardMaterial({ color: lin(0x484440), roughness: 0.96 }),
    };

    function floorY(n) { return n === 0 ? 0 : GROUND_H + (n - 1) * FLOOR_H; }
    function floorTop(n) { return n === 0 ? GROUND_H : GROUND_H + n * FLOOR_H; }
    function floorMid(n) { return (floorY(n) + floorTop(n)) / 2; }

    function clearTower() {
      while (tower.children.length) {
        const o = tower.children.pop();
        o.traverse && o.traverse((c) => { if (c.geometry) c.geometry.dispose(); });
      }
    }

    function build(n) {
      clearTower();
      floorsCount = Math.max(1, n);
      const H = floorTop(floorsCount);
      const plaster = plasterTexture(rnd(3));
      mats.plaster = new T.MeshStandardMaterial({ map: plaster, roughness: 0.88 });
      const faceMat = (w) => {
        const t = plaster.clone(); t.needsUpdate = true; t.repeat.set(w / 4, floorsCount);
        return new T.MeshStandardMaterial({ map: t, roughness: 0.88 });
      };

      // ============================================================
      // DUSHANBE-STYLE BUILDING (Nekruz Residence look)
      // ============================================================

      // --- Main body (floors 2+) ---
      const bodyH = floorsCount * FLOOR_H;
      const body = new T.Mesh(new T.BoxGeometry(W, bodyH, D), [faceMat(D), faceMat(D), mats.roof, mats.roof, faceMat(W), faceMat(W)]);
      body.position.y = GROUND_H + bodyH / 2; body.castShadow = true; body.receiveShadow = true;
      tower.add(body);

      // --- Ground floor: granite-clad base (Dushanbe style: dark stone lower 2 floors) ---
      const graniteM = new T.MeshStandardMaterial({ color: lin(0x2e2b28), roughness: 0.75, metalness: 0.05 }); // dark granite
      const gf = new T.Mesh(new T.BoxGeometry(W + 0.4, GROUND_H, D + 0.4), graniteM);
      gf.position.y = GROUND_H / 2; gf.castShadow = true; gf.receiveShadow = true; tower.add(gf);

      // Lobby glass interior behind granite shell
      const core = new T.Mesh(new T.BoxGeometry(W - 1.0, GROUND_H - 0.6, D - 1.0), new T.MeshStandardMaterial({ color: lin(0x1e3050), roughness: 0.1, metalness: 0.9, transparent: true, opacity: 0.72, emissive: lin(0xffc27a), emissiveIntensity: 0.0 }));
      core.position.y = GROUND_H / 2 + 0.2; tower.add(core);
      window.__coreMat = core.material;

      // Granite horizontal rustication joints (3 rows on base)
      const rusticM = new T.MeshStandardMaterial({ color: lin(0x1e1c1a), roughness: 0.9 });
      for (let ri = 1; ri <= 3; ri++) {
        [[0, D / 2 + 0.22, 0, W + 0.9], [0, -D / 2 - 0.22, 0, W + 0.9], [W / 2 + 0.22, 0, Math.PI / 2, D + 0.9], [-W / 2 - 0.22, 0, Math.PI / 2, D + 0.9]].forEach(([x, z, ry, len]) => {
          const slot = new T.Mesh(new T.BoxGeometry(len, 0.08, 0.1), rusticM);
          slot.position.set(x, (GROUND_H / 4) * ri, z); slot.rotation.y = ry; tower.add(slot);
        });
      }

      // Vertical granite pilasters on ground floor (Dushanbe decorative piers)
      const pilasterM = new T.MeshStandardMaterial({ color: lin(0x3a3733), roughness: 0.7 });
      const pilasterPositions = [];
      for (let i = -3; i <= 3; i++) pilasterPositions.push([i * (W / 7), D / 2 + 0.22, 0]);
      for (let i = -1; i <= 1; i++) { pilasterPositions.push([W / 2 + 0.22, i * (D / 3), Math.PI / 2]); pilasterPositions.push([-W / 2 - 0.22, i * (D / 3), Math.PI / 2]); }
      pilasterPositions.forEach(([x, z, ry]) => {
        const pil = new T.Mesh(new T.BoxGeometry(0.5, GROUND_H, 0.4), pilasterM);
        pil.position.set(x, GROUND_H / 2, z); pil.rotation.y = ry; pil.castShadow = true; tower.add(pil);
      });

      // --- Base plinth (extra-wide stone step) ---
      const plinth = new T.Mesh(new T.BoxGeometry(W + 4.0, 0.7, D + 4.0), new T.MeshStandardMaterial({ color: lin(0x2a2826), roughness: 0.8 }));
      plinth.position.y = 0.35; plinth.receiveShadow = true; tower.add(plinth);
      const plinth2 = new T.Mesh(new T.BoxGeometry(W + 2.2, 0.45, D + 2.2), new T.MeshStandardMaterial({ color: lin(0x322f2c), roughness: 0.75 }));
      plinth2.position.y = 0.9; tower.add(plinth2);

      // --- Transition cornice at top of ground floor (stone crown molding) ---
      const corniceM = new T.MeshStandardMaterial({ color: lin(0xd0c8b8), roughness: 0.6 });
      [[0, D / 2 + 0.12, 0, W + 1.2], [0, -D / 2 - 0.12, 0, W + 1.2], [W / 2 + 0.12, 0, Math.PI / 2, D + 1.2], [-W / 2 - 0.12, 0, Math.PI / 2, D + 1.2]].forEach(([x, z, ry, len]) => {
        const cr = new T.Mesh(new T.BoxGeometry(len, 0.6, 0.55), corniceM);
        cr.position.set(x, GROUND_H + 0.3, z); cr.rotation.y = ry; cr.castShadow = true; tower.add(cr);
        const cr2 = new T.Mesh(new T.BoxGeometry(len + 0.2, 0.22, 0.38), corniceM);
        cr2.position.set(x, GROUND_H + 0.72, z); cr2.rotation.y = ry; tower.add(cr2);
      });

      // --- Floor-level cornice bands (every 3 floors — Dushanbe belt courses) ---
      {
        const bandM = new T.MeshStandardMaterial({ color: lin(0xc8c0b0), roughness: 0.65 });
        const beltFloors = [];
        for (let f = 3; f <= floorsCount; f += 3) beltFloors.push(f);
        beltFloors.forEach((f) => {
          const y = floorY(f);
          [[0, D / 2 + 0.1, 0, W + 0.5], [0, -D / 2 - 0.1, 0, W + 0.5], [W / 2 + 0.1, 0, Math.PI / 2, D + 0.5], [-W / 2 - 0.1, 0, Math.PI / 2, D + 0.5]].forEach(([x, z, ry, len]) => {
            const b = new T.Mesh(new T.BoxGeometry(len, 0.42, 0.44), bandM);
            b.position.set(x, y + 0.21, z); b.rotation.y = ry; b.castShadow = true; tower.add(b);
          });
        });
      }

      // --- Thin floor slab lines (every floor — standard Dushanbe precast appearance) ---
      {
        const slabM = new T.MeshStandardMaterial({ color: lin(0xb8b2a8), roughness: 0.85 });
        const o2 = new T.Object3D();
        const slabBands = new T.InstancedMesh(new T.BoxGeometry(1, 0.18, 0.28), slabM, floorsCount * 4);
        let k = 0;
        for (let f = 1; f <= floorsCount; f++) {
          const y = floorY(f) + 0.04;
          [[0, D / 2 + 0.08, 0, W + 0.1], [0, -D / 2 - 0.08, 0, W + 0.1], [W / 2 + 0.08, 0, Math.PI / 2, D + 0.1], [-W / 2 - 0.08, 0, Math.PI / 2, D + 0.1]].forEach(([x, z, ry, len]) => {
            o2.position.set(x, y, z); o2.rotation.set(0, ry, 0); o2.scale.set(len, 1, 1); o2.updateMatrix(); slabBands.setMatrixAt(k++, o2.matrix);
          });
        }
        slabBands.frustumCulled = false; slabBands.castShadow = true; tower.add(slabBands);
      }

      // --- Vertical fins / pilasters on upper body (classical vertical articulation) ---
      const finM = new T.MeshStandardMaterial({ color: lin(0xc9a227), roughness: 0.4, metalness: 0.2 }); // gold accent fins
      [[0, D / 2, 1.1, 0.42], [0, -D / 2, 1.1, 0.42], [W / 2, 0, 0.42, 1.1], [-W / 2, 0, 0.42, 1.1]].forEach(([x, z, fw, fd]) => {
        const fin = new T.Mesh(new T.BoxGeometry(fw, bodyH, fd), finM);
        fin.position.set(x + (x ? Math.sign(x) * 0.1 : 0), GROUND_H + bodyH / 2, z + (z ? Math.sign(z) * 0.1 : 0));
        fin.castShadow = true; tower.add(fin);
      });

      // Corner columns (massive, full-height — Dushanbe landmark style)
      [[-1, -1], [1, -1], [1, 1], [-1, 1]].forEach(([sx, sz]) => {
        const c = new T.Mesh(new T.BoxGeometry(2.0, H + 1.0, 2.0), mats.stone);
        c.position.set(sx * (W / 2 + 0.25), (H + 1.0) / 2, sz * (D / 2 + 0.25));
        c.castShadow = true; c.receiveShadow = true; tower.add(c);
        // Column capital at top
        const cap = new T.Mesh(new T.BoxGeometry(2.6, 0.5, 2.6), corniceM);
        cap.position.set(sx * (W / 2 + 0.25), H + 1.3, sz * (D / 2 + 0.25)); tower.add(cap);
      });

      // --- ENTRANCE: Dushanbe grand portal with arch ---
      const entW = 13, entH = GROUND_H + 1.0;
      // Entrance canopy slab
      const canopy = new T.Mesh(new T.BoxGeometry(entW + 0.6, 0.55, 5.8), mats.stone);
      canopy.position.set(0, GROUND_H + 0.2, D / 2 + 2.8); canopy.castShadow = true; tower.add(canopy);
      // Canopy soffit (underside — warm)
      const canSoffit = new T.Mesh(new T.BoxGeometry(entW - 0.2, 0.08, 5.6), new T.MeshStandardMaterial({ color: lin(0xd4b87a), roughness: 0.6, emissive: lin(0xffa040), emissiveIntensity: 0.0 }));
      canSoffit.position.set(0, GROUND_H - 0.04, D / 2 + 2.8); tower.add(canSoffit);
      window.__canSoffit = canSoffit.material;
      // Entrance columns (round — classical Tajik architecture)
      [-5.8, -2.9, 2.9, 5.8].forEach((x) => {
        const col = new T.Mesh(new T.CylinderGeometry(0.38, 0.44, GROUND_H + 0.3, 16), mats.stone);
        col.position.set(x, (GROUND_H + 0.3) / 2, D / 2 + 5.0); col.castShadow = true; tower.add(col);
        // Column base
        const base = new T.Mesh(new T.BoxGeometry(1.1, 0.4, 1.1), corniceM);
        base.position.set(x, 0.2, D / 2 + 5.0); tower.add(base);
        // Column capital
        const cap = new T.Mesh(new T.BoxGeometry(1.0, 0.45, 1.0), corniceM);
        cap.position.set(x, GROUND_H + 0.26, D / 2 + 5.0); tower.add(cap);
      });
      // Entrance arch frieze above canopy
      const frieze = new T.Mesh(new T.BoxGeometry(entW + 0.6, 1.4, 0.55), corniceM);
      frieze.position.set(0, GROUND_H + 1.0, D / 2 + 0.4); frieze.castShadow = true; tower.add(frieze);
      // Sign plate
      signMat = new T.MeshBasicMaterial({ map: signTexture('НЕКРУЗ'), toneMapped: false });
      const sign = new T.Mesh(new T.PlaneGeometry(10.4, 1.1), signMat);
      sign.position.set(0, GROUND_H + 1.0, D / 2 + 0.72); tower.add(sign);
      // Entrance steps
      [[3.2, 0.28], [2.0, 0.18], [1.0, 0.1]].forEach(([ext, h], i) => {
        const step = new T.Mesh(new T.BoxGeometry(entW + ext, h, ext), new T.MeshStandardMaterial({ color: lin(0x2e2b28), roughness: 0.7 }));
        step.position.set(0, h / 2 + i * 0.1, D / 2 + 5.3 + (3.2 - ext) / 2 + ext / 2); step.receiveShadow = true; tower.add(step);
      });
      // Entrance door glass
      const door = new T.Mesh(new T.BoxGeometry(5.5, GROUND_H - 0.5, 0.12), mats.glassWall);
      door.position.set(0, (GROUND_H - 0.5) / 2 + 0.25, D / 2 + 0.1); tower.add(door);

      // --- ATTIC / PENTHOUSE floor (top 2 floors set back — Dushanbe tower crown) ---
      if (floorsCount >= 6) {
        const attH = FLOOR_H * 2;
        const attSetback = 1.5;
        const att = new T.Mesh(new T.BoxGeometry(W - attSetback * 2, attH, D - attSetback * 2), faceMat(W - attSetback * 2));
        att.position.y = H - attH / 2; att.castShadow = true; tower.add(att);
        // Attic base cornice (crown molding)
        [[0, (D - attSetback * 2) / 2 + 0.1, 0, W - attSetback * 2 + 0.5], [0, -(D - attSetback * 2) / 2 - 0.1, 0, W - attSetback * 2 + 0.5], [(W - attSetback * 2) / 2 + 0.1, 0, Math.PI / 2, D - attSetback * 2 + 0.5], [-(W - attSetback * 2) / 2 - 0.1, 0, Math.PI / 2, D - attSetback * 2 + 0.5]].forEach(([x, z, ry, len]) => {
          const cr = new T.Mesh(new T.BoxGeometry(len, 0.55, 0.65), corniceM);
          cr.position.set(x, H - attH - 0.08, z); cr.rotation.y = ry; cr.castShadow = true; tower.add(cr);
        });
      }

      // --- ROOF: Parapet with decorative crown ---
      const roofY = H + 0.5;
      // Main parapet walls
      [[0, D / 2 + 0.1, 0, W + 1.0], [0, -D / 2 - 0.1, 0, W + 1.0], [W / 2 + 0.1, 0, Math.PI / 2, D + 1.0], [-W / 2 - 0.1, 0, Math.PI / 2, D + 1.0]].forEach(([x, z, ry, len]) => {
        const p = new T.Mesh(new T.BoxGeometry(len, 1.4, 0.4), mats.concrete);
        p.position.set(x, H + 0.7, z); p.rotation.y = ry; p.castShadow = true; tower.add(p);
        // Parapet cap
        const cap = new T.Mesh(new T.BoxGeometry(len + 0.1, 0.22, 0.6), corniceM);
        cap.position.set(x, H + 1.52, z); cap.rotation.y = ry; tower.add(cap);
      });
      // Parapet corner posts
      [[-1, -1], [1, -1], [1, 1], [-1, 1]].forEach(([sx, sz]) => {
        const post = new T.Mesh(new T.BoxGeometry(0.6, 1.8, 0.6), mats.stone);
        post.position.set(sx * (W / 2 + 0.1), H + 0.9, sz * (D / 2 + 0.1)); tower.add(post);
      });
      // Decorative pediment / triangular feature on front
      const pedBase = new T.Mesh(new T.BoxGeometry(16, 0.3, 0.5), mats.stone);
      pedBase.position.set(0, roofY + 0.5, D / 2 + 0.1); tower.add(pedBase);
      // Penthouse + utilities
      const pent = new T.Mesh(new T.BoxGeometry(10, 3.2, 7), mats.stone);
      pent.position.set(-5, roofY + 1.6, -3); pent.castShadow = true; tower.add(pent);
      const tank = new T.Mesh(new T.CylinderGeometry(1.9, 1.9, 2.2, 16), mats.frame);
      tank.position.set(7.5, roofY + 1.1, 3); tank.castShadow = true; tower.add(tank);
      const ant = new T.Mesh(new T.CylinderGeometry(0.08, 0.14, 11, 8), mats.frame);
      ant.position.set(-5, roofY + 3.2 + 5.5, -3); ant.castShadow = true; tower.add(ant);
      roofLight = new T.Mesh(new T.SphereGeometry(0.3, 12, 8), new T.MeshBasicMaterial({ color: 0xff3b30, toneMapped: false }));
      roofLight.position.set(-5, roofY + 3.2 + 11, -3); tower.add(roofLight);

      // apartments: windows, frames, balconies
      apartmentInst = [];
      aptMap = {}; keyMap = {};
      const count = floorsCount * 8;
      glass = new T.InstancedMesh(new T.BoxGeometry(1, 1, 0.06), new T.MeshStandardMaterial({ color: lin(0x3a6ea8), roughness: 0.03, metalness: 0.95, transparent: true, opacity: 0.55, envMapIntensity: 2.0 }), count);
      glow = new T.InstancedMesh(new T.PlaneGeometry(1, 1), new T.MeshBasicMaterial({ color: 0xffffff, toneMapped: true }), count);
      frames = new T.InstancedMesh(new T.BoxGeometry(1, 1, 0.3), mats.frame, count);
      mullions = new T.InstancedMesh(new T.BoxGeometry(0.1, 1, 0.12), mats.frame, count * 2);
      slabsMesh = new T.InstancedMesh(new T.BoxGeometry(1, 0.22, 1), mats.concrete, count);
      railFront = new T.InstancedMesh(new T.BoxGeometry(1, 1.05, 0.04), mats.railGlass, count);
      railSide = new T.InstancedMesh(new T.BoxGeometry(0.04, 1.05, 1), mats.railGlass, count * 2);
      const handrail = new T.InstancedMesh(new T.BoxGeometry(1, 0.05, 0.07), mats.frame, count);
      [glass, glow, frames, mullions, slabsMesh, railFront, railSide, handrail].forEach((m) => { m.frustumCulled = false; });
      frames.castShadow = true; slabsMesh.castShadow = true; slabsMesh.receiveShadow = true;
      const o = new T.Object3D();
      const baseCol = new T.Color();
      let mi = 0, ri = 0, idx = 0;
      for (let f = 1; f <= floorsCount; f++) {
        for (let s = 0; s < 8; s++) {
          const [cx, cz, ry, bw] = SLOTS[s];
          const nx = Math.sin(ry), nz = Math.cos(ry);
          const tx = Math.cos(ry), tz = -Math.sin(ry); // tangent along face
          const y = floorY(f) + FLOOR_H / 2 + 0.05;
          const ww = bw * 0.7, wh = 2.1;
          const place = (mesh, i, lx, ly, lz, sx, sy, sz) => {
            o.position.set(cx + tx * lx + nx * lz, y + ly, cz + tz * lx + nz * lz);
            o.rotation.set(0, ry, 0); o.scale.set(sx, sy, sz); o.updateMatrix(); mesh.setMatrixAt(i, o.matrix);
          };
          place(frames, idx, 0, 0.1, 0.12, ww + 0.34, wh + 0.34, 1);
          place(glow, idx, 0, 0.1, 0.285, ww, wh, 1);
          place(glass, idx, 0, 0.1, 0.3, ww, wh, 1);
          place(mullions, mi++, -ww / 6, 0.1, 0.31, 1, wh, 1);
          place(mullions, mi++, ww / 6, 0.1, 0.31, 1, wh, 1);
          // balcony (not on top floor corners variety: every floor)
          const bwid = ww * 0.82, bdep = 1.55;
          place(slabsMesh, idx, 0, -FLOOR_H / 2 + 0.1 - 0.05 + 0.0, 0.1 + bdep / 2, bwid, 1, bdep);
          place(railFront, idx, 0, -FLOOR_H / 2 + 0.1 + 0.11 + 0.52, 0.1 + bdep, bwid, 1, 1);
          place(railSide, ri++, -bwid / 2, -FLOOR_H / 2 + 0.1 + 0.11 + 0.52, 0.1 + bdep / 2, 1, 1, bdep);
          place(railSide, ri++, bwid / 2, -FLOOR_H / 2 + 0.1 + 0.11 + 0.52, 0.1 + bdep / 2, 1, 1, bdep);
          place(handrail, idx, 0, -FLOOR_H / 2 + 0.1 + 0.11 + 1.05, 0.1 + bdep, bwid, 1, 1);
          baseCol.set(0xffffff); glass.setColorAt(idx, baseCol); glow.setColorAt(idx, baseCol);
          apartmentInst.push({ floor: f, slot: s, id: null, status: 'free', rand: rand() });
          keyMap[f + ':' + s] = idx;
          idx++;
        }
      }
      [glass, glow, frames, mullions, slabsMesh, railFront, railSide, handrail].forEach((m) => { m.instanceMatrix.needsUpdate = true; tower.add(m); });
      glass.instanceColor.needsUpdate = true; glow.instanceColor.needsUpdate = true;
      tower.userData.handrail = handrail;

      // selection band + glowing ring
      selBand = new T.Mesh(new T.BoxGeometry(W + 2.2, FLOOR_H, D + 2.2), new T.MeshBasicMaterial({ color: 0xf2c46a, transparent: true, opacity: 0.1, depthWrite: false, toneMapped: false }));
      selBand.visible = false; tower.add(selBand);
      selEdges = new T.Group();
      {
        const gm2 = new T.MeshBasicMaterial({ color: 0xffd27a, toneMapped: false });
        [-1, 1].forEach((sy) => {
          [[0, D / 2 + 1.1, W + 2.4, 0.16], [0, -D / 2 - 1.1, W + 2.4, 0.16], [W / 2 + 1.1, 0, 0.16, D + 2.2], [-W / 2 - 1.1, 0, 0.16, D + 2.2]].forEach(([x, z, w, d]) => {
            const b = new T.Mesh(new T.BoxGeometry(w, 0.16, d), gm2); b.position.set(x, sy * (FLOOR_H / 2 - 0.08), z); selEdges.add(b);
          });
        });
      }
      selEdges.visible = false; tower.add(selEdges);

      // pick proxy for floors (invisible body)
      pickBody = new T.Mesh(new T.BoxGeometry(W, H, D), new T.MeshBasicMaterial({ visible: false }));
      pickBody.position.y = H / 2; tower.add(pickBody);

      applyApartments();
      buildGroundStuff(H);
    }
    var pickBody;

    // lamps, trees, cars
    const extras = new T.Group(); scene.add(extras);
    let cars = [];
    function buildGroundStuff() {
      while (extras.children.length) extras.remove(extras.children[0]);
      cars = []; lampBulbs = [];
      // lamps
      lampMat = new T.MeshBasicMaterial({ color: 0xffd9a0, toneMapped: false });
      const poleM = mats.frame;
      for (let i = -9; i <= 9; i++) {
        const x = i * 22, z = 34.2;
        const pole = new T.Mesh(new T.CylinderGeometry(0.1, 0.14, 7, 8), poleM); pole.position.set(x, 3.5, z); pole.castShadow = true; extras.add(pole);
        const arm = new T.Mesh(new T.BoxGeometry(0.1, 0.1, 2.4), poleM); arm.position.set(x, 6.9, z + 1.1); extras.add(arm);
        const bulb = new T.Mesh(new T.SphereGeometry(0.34, 10, 8), lampMat.clone()); bulb.position.set(x, 6.78, z + 2.2); extras.add(bulb); lampBulbs.push(bulb);
      }
      // trees
      const trunkG = new T.CylinderGeometry(0.2, 0.3, 3.4, 7);
      const crownG = new T.IcosahedronGeometry(2.1, 1);
      const spots = [];
      for (let i = -9; i <= 9; i++) { if (Math.abs(i * 11) > 22) spots.push([i * 11 + 4, 26.2]); }
      for (let i = 0; i < 22; i++) spots.push([-W / 2 - 8 - rand() * 36, -D / 2 + rand() * (D + 30)]);
      for (let i = 0; i < 22; i++) spots.push([W / 2 + 8 + rand() * 36, -D / 2 + rand() * (D + 30)]);
      for (let i = 0; i < 26; i++) spots.push([-70 + rand() * 140, -D / 2 - 6 - rand() * 60]);
      for (let i = 0; i < 5; i++) { spots.push([-W / 2 - 3.5, D / 2 + 4 + i * 2.3]); spots.push([W / 2 + 3.5, D / 2 + 4 + i * 2.3]); }
      const trunks = new T.InstancedMesh(trunkG, new T.MeshStandardMaterial({ color: lin(0x5a4333), roughness: 1 }), spots.length);
      const crowns = new T.InstancedMesh(crownG, new T.MeshStandardMaterial({ color: 0xffffff, roughness: 0.95, flatShading: true }), spots.length * 2);
      const m = new T.Matrix4(), col = new T.Color();
      spots.forEach(([x, z], i) => {
        const s = 0.8 + rand() * 0.7;
        m.makeScale(s, s, s); m.setPosition(x, 1.7 * s, z); trunks.setMatrixAt(i, m);
        for (let k = 0; k < 2; k++) {
          const cs = s * (k ? 0.75 : 1.05);
          m.makeScale(cs, cs * 0.95, cs); m.setPosition(x + (k ? 0.5 : 0), (3.6 + k * 1.5) * s, z + (k ? -0.3 : 0));
          crowns.setMatrixAt(i * 2 + k, m);
          col.setHSL(0.26 + rand() * 0.07, 0.28 + rand() * 0.14, 0.16 + rand() * 0.09); col.convertSRGBToLinear();
          crowns.setColorAt(i * 2 + k, col);
        }
      });
      trunks.castShadow = crowns.castShadow = true; crowns.receiveShadow = true;
      trunks.frustumCulled = crowns.frustumCulled = false;
      extras.add(trunks); extras.add(crowns);
      // cars
      const colors = [0xd8d8d8, 0x23324a, 0xa23a32, 0x2b2f35, 0xe8e2d2, 0x3a5f4c];
      for (let i = 0; i < 7; i++) {
        const car = new T.Group();
        const c = colors[i % colors.length];
        const body = new T.Mesh(new T.BoxGeometry(4.3, 0.8, 1.8), new T.MeshStandardMaterial({ color: lin(c), roughness: 0.35, metalness: 0.6 }));
        body.position.y = 0.75; body.castShadow = true; car.add(body);
        const cab = new T.Mesh(new T.BoxGeometry(2.2, 0.7, 1.6), new T.MeshStandardMaterial({ color: lin(0x1a2230), roughness: 0.1, metalness: 0.9 }));
        cab.position.set(-0.2, 1.4, 0); cab.castShadow = true; car.add(cab);
        const wm = new T.MeshStandardMaterial({ color: 0x111111, roughness: 0.9 });
        [[-1.4, 0.9], [1.4, 0.9], [-1.4, -0.9], [1.4, -0.9]].forEach(([x, z]) => {
          const w = new T.Mesh(new T.CylinderGeometry(0.38, 0.38, 0.3, 12), wm); w.rotation.x = Math.PI / 2; w.position.set(x, 0.38, z); car.add(w);
        });
        const hl = new T.MeshBasicMaterial({ color: 0xfff1c4, toneMapped: false });
        const tl = new T.MeshBasicMaterial({ color: 0xff2a2a, toneMapped: false });
        [0.6, -0.6].forEach((z) => {
          const a = new T.Mesh(new T.BoxGeometry(0.1, 0.18, 0.4), hl); a.position.set(2.16, 0.8, z); car.add(a);
          const b = new T.Mesh(new T.BoxGeometry(0.1, 0.18, 0.4), tl); b.position.set(-2.16, 0.8, z); car.add(b);
        });
        const dir = i % 2 ? 1 : -1;
        car.position.set(-400 + rand() * 800, 0.03, 40 + (dir > 0 ? -2.6 : 2.6));
        car.rotation.y = dir > 0 ? 0 : Math.PI;
        car.userData = { dir, v: 7 + rand() * 7 };
        extras.add(car); cars.push(car);
      }
    }

    // ---------- apartments data ----------
    function colorFor(a) {
      if (!overlay) {
        const c = new T.Color();
        if (a.rand < 0.42) c.setHex(0x1a2030);
        else c.setHSL(0.09 + a.rand * 0.04, 0.8, 0.55 + (a.rand - 0.42) * 0.3);
        return c;
      }
      return new T.Color(STATUS_COLOR[a.status] || STATUS_COLOR.free);
    }
    function applyApartments() {
      const c = new T.Color();
      apartmentInst.forEach((a, i) => {
        const col = colorFor(a); col.convertSRGBToLinear();
        glow.setColorAt(i, col);
        c.copy(overlay && a.status !== 'sold' ? col : new T.Color(0xffffff)).lerp(new T.Color(1, 1, 1), 0.78);
        glass.setColorAt(i, c);
      });
      glow.instanceColor.needsUpdate = true; glass.instanceColor.needsUpdate = true;
    }

    function normStatus(s) {
      s = String(s == null ? 'free' : s).toLowerCase().replace(/[^a-z]/g, '');
      if (s === '0') return 'free';
      return STATUS_COLOR[s] ? s : 'free';
    }

    function setData(list) {
      // list: [{id, floor, planSlot, status}]
      const maxFloor = list.reduce((m, a) => Math.max(m, a.floor || 0), 0) || floorsCount;
      if (!glass || maxFloor !== floorsCount) build(maxFloor);
      apartmentInst.forEach((a) => { a.id = null; a.status = 'sold'; a.data = null; });
      aptMap = {};
      const byFloor = {};
      list.forEach((a) => { (byFloor[a.floor] = byFloor[a.floor] || []).push(a); });
      Object.keys(byFloor).forEach((f) => {
        byFloor[f].sort((x, y) => (x.number || 0) - (y.number || 0));
        byFloor[f].forEach((a, k) => {
          const slot = a.planSlot != null && a.planSlot >= 0 ? a.planSlot : k;
          const i = keyMap[f + ':' + slot];
          if (i == null) return;
          const inst = apartmentInst[i];
          inst.id = a.id; inst.status = normStatus(a.status); inst.data = a;
          aptMap[a.id] = i;
        });
      });
      applyApartments();
    }

    // ---------- presets ----------
    let cur = null, tgt = null, presetName = 'dusk', pmremTimer = 0, envTex = null;
    const pm = new T.PMREMGenerator(renderer);
    function snapshot(p) {
      return { top: lin(p.top), horizon: lin(p.horizon), bottom: lin(p.bottom), fog: lin(p.fog), sunColor: lin(p.sunColor), hemiSky: lin(p.hemiSky), hemiGround: lin(p.hemiGround),
        sunPos: new T.Vector3(...p.sunPos), n: { sunI: p.sunI, hemiI: p.hemiI, glow: p.glow, lamp: p.lamp, stars: p.stars, cloud: p.cloud, exposure: p.exposure, env: p.env } };
    }
    function clonePreset(s) {
      return { top: s.top.clone(), horizon: s.horizon.clone(), bottom: s.bottom.clone(), fog: s.fog.clone(), sunColor: s.sunColor.clone(), hemiSky: s.hemiSky.clone(), hemiGround: s.hemiGround.clone(), sunPos: s.sunPos.clone(), n: Object.assign({}, s.n) };
    }
    function applyCur() {
      const u = skyMat.uniforms;
      u.top.value.copy(cur.top); u.horizon.value.copy(cur.horizon); u.bottom.value.copy(cur.bottom);
      u.sunColor.value.copy(cur.sunColor); u.sunDir.value.copy(cur.sunPos).normalize();
      scene.fog.color.copy(cur.fog);
      hemi.color.copy(cur.hemiSky); hemi.groundColor.copy(cur.hemiGround); hemi.intensity = cur.n.hemiI;
      sun.color.copy(cur.sunColor); sun.intensity = cur.n.sunI;
      sun.position.copy(cur.sunPos).normalize().multiplyScalar(260);
      starMat.opacity = cur.n.stars;
      clouds.forEach((c) => { c.material.opacity = cur.n.cloud * 0.55; });
      renderer.toneMappingExposure = cur.n.exposure;
      if (glow) { const g = cur.n.glow; glow.material.color.setRGB(g, g, g); }
      lampBulbs.forEach((b) => { b.material.color.setRGB(1, 0.85, 0.6).multiplyScalar(Math.max(0.05, cur.n.lamp)); b.visible = cur.n.lamp > 0.05; });
      if (neighbourMat) neighbourMat.forEach((m) => { m.emissiveIntensity = 0.04 + cur.n.lamp * 0.85; });
      if (window.__coreMat) window.__coreMat.emissiveIntensity = cur.n.lamp * 0.55;
      if (window.__canSoffit) window.__canSoffit.emissiveIntensity = cur.n.lamp * 0.7;
      sky.material.uniforms.sunColor.value.copy(cur.sunColor).multiplyScalar(presetName === 'night' ? 0.55 : 1);
    }
    function rebuildEnv() {
      try {
        const es = new T.Scene(); es.add(new T.Mesh(sky.geometry, skyMat));
        const rt = pm.fromScene(es, 0.02);
        if (envTex) envTex.dispose();
        envTex = rt.texture; scene.environment = envTex;
        scene.traverse((o) => { if (o.material && o.material.isMeshStandardMaterial) o.material.envMapIntensity = Math.max(0.25, cur.n.env * 0.9); });
        if (glass) glass.material.envMapIntensity = 1.6 * cur.n.env;
      } catch (e) { /* env map is a visual extra */ }
    }
    function setTime(name, instant) {
      if (!PRESETS[name]) return;
      presetName = name; tgt = snapshot(PRESETS[name]);
      if (instant || !cur) { cur = clonePreset(tgt); applyCur(); rebuildEnv(); }
      else { clearTimeout(pmremTimer); pmremTimer = setTimeout(() => { rebuildEnv(); }, 1400); }
    }
    function stepPreset(dt) {
      if (!cur || !tgt) return;
      const k = 1 - Math.exp(-dt * 2.6);
      ['top', 'horizon', 'bottom', 'fog', 'sunColor', 'hemiSky', 'hemiGround'].forEach((n) => cur[n].lerp(tgt[n], k));
      cur.sunPos.lerp(tgt.sunPos, k);
      Object.keys(cur.n).forEach((n) => { cur.n[n] += (tgt.n[n] - cur.n[n]) * k; });
      applyCur();
    }

    // ---------- camera / controls ----------
    const view = { az: 0.62, pol: 1.32, dist: 118, ty: 24, tx: 0, tz: 0 };
    const goal = Object.assign({}, view);
    // No slow turntable for people who asked for less motion.
    const reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    let autoRotate = opts.autoRotate !== false && !reduceMotion, lastInput = 0;
    function placeCamera() {
      const sp = Math.sin(view.pol), cp = Math.cos(view.pol);
      camera.position.set(view.tx + view.dist * sp * Math.sin(view.az), view.ty + view.dist * cp, view.tz + view.dist * sp * Math.cos(view.az));
      camera.lookAt(view.tx, view.ty, view.tz);
    }
    function defaultGoal() {
      const H = floorTop(floorsCount);
      goal.ty = H * 0.5; goal.dist = Math.max(110, H * 2.35) * (container.clientWidth < container.clientHeight ? 1.45 : 1);
      goal.pol = 1.36; goal.tx = 0; goal.tz = 0;
    }
    function resetView() { defaultGoal(); selectFloor(null, true); }
    function focusFloor(n) {
      goal.ty = floorMid(n); goal.dist = 58 * (container.clientWidth < container.clientHeight ? 1.35 : 1); goal.pol = 1.42;
    }

    const ray = new T.Raycaster(); const mouse = new T.Vector2();
    let ptr = null, pointers = new Map(), pinchD = 0, moved = 0, downPos = null;
    const el = renderer.domElement;
    el.addEventListener('pointerdown', (e) => {
      el.setPointerCapture(e.pointerId); pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
      downPos = { x: e.clientX, y: e.clientY }; moved = 0; lastInput = performance.now();
      if (pointers.size === 2) { const [a, b] = [...pointers.values()]; pinchD = Math.hypot(a.x - b.x, a.y - b.y); }
    });
    el.addEventListener('pointermove', (e) => {
      lastInput = performance.now();
      const p = pointers.get(e.pointerId);
      if (p) {
        const dx = e.clientX - p.x, dy = e.clientY - p.y;
        p.x = e.clientX; p.y = e.clientY; moved += Math.abs(dx) + Math.abs(dy);
        if (pointers.size === 1) {
          goal.az -= dx * 0.0062; goal.pol = Math.min(1.62, Math.max(0.55, goal.pol - dy * 0.0042));
        } else if (pointers.size === 2) {
          const [a, b] = [...pointers.values()]; const d = Math.hypot(a.x - b.x, a.y - b.y);
          goal.dist = Math.min(220, Math.max(24, goal.dist * (pinchD / (d || 1)))); pinchD = d;
        }
        el.style.cursor = 'grabbing';
      } else hoverAt(e);
    });
    const up = (e) => {
      const had = pointers.has(e.pointerId); pointers.delete(e.pointerId); el.style.cursor = hoverIdx >= 0 ? 'pointer' : 'grab';
      if (had && pointers.size === 0 && moved < 7 && downPos) clickAt(e);
    };
    el.addEventListener('pointerup', up); el.addEventListener('pointercancel', (e) => { pointers.delete(e.pointerId); });
    el.addEventListener('pointerleave', () => { setHover(-1); });
    el.addEventListener('wheel', (e) => {
      e.preventDefault(); lastInput = performance.now();
      goal.dist = Math.min(220, Math.max(24, goal.dist * Math.exp(e.deltaY * 0.0011)));
    }, { passive: false });
    el.style.cursor = 'grab';

    function pick(e) {
      const r = el.getBoundingClientRect();
      mouse.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
      ray.setFromCamera(mouse, camera);
      const hits = ray.intersectObjects([glass, pickBody], false);
      if (!hits.length) return null;
      const h = hits[0];
      if (h.object === glass) return { apt: h.instanceId, point: h.point };
      return { floor: floorAt(h.point.y), point: h.point };
    }
    function floorAt(y) {
      if (y < GROUND_H) return 0;
      return Math.min(floorsCount, 1 + Math.floor((y - GROUND_H) / FLOOR_H));
    }
    function setHover(i) {
      if (i === hoverIdx) return;
      hoverIdx = i; el.style.cursor = i >= 0 ? 'pointer' : 'grab';
      if (i < 0) handlers.hover(null);
    }
    let lastHoverEv = null, hoverQueued = false;
    function hoverAt(e) {
      lastHoverEv = e;
      if (hoverQueued) return; hoverQueued = true;
      requestAnimationFrame(() => {
        hoverQueued = false; if (!lastHoverEv || pointers.size) return;
        const r = pick(lastHoverEv);
        if (r && r.apt != null && apartmentInst[r.apt].id != null) {
          setHover(r.apt);
          const rect = el.getBoundingClientRect();
          handlers.hover(apartmentInst[r.apt].data, lastHoverEv.clientX - rect.left, lastHoverEv.clientY - rect.top);
        } else if (r && r.floor != null) { setHover(-1); el.style.cursor = 'pointer'; }
        else setHover(-1);
      });
    }
    function clickAt(e) {
      const r = pick(e);
      if (!r) { handlers.floor(null); selectFloor(null); return; }
      if (r.apt != null) {
        const a = apartmentInst[r.apt];
        selectFloor(a.floor, false, true);
        if (a.id != null) { selectedIdx = r.apt; handlers.select(a.data); }
      } else if (r.floor != null) {
        selectFloor(r.floor, false, true);
      }
    }
    function selectFloor(n, silent, fromClick) {
      if (n == null) {
        selBand.visible = selEdges.visible = false; selectedIdx = -1; defaultGoalIfNone(); if (!silent) handlers.floor(null); return;
      }
      selBand.visible = selEdges.visible = true;
      const h = n === 0 ? GROUND_H : FLOOR_H;
      selBand.scale.set(1, h / FLOOR_H, 1); selEdges.scale.set(1, h / FLOOR_H, 1);
      selBand.position.y = selEdges.position.y = floorMid(n);
      focusFloor(n);
      if (selectedIdx >= 0 && apartmentInst[selectedIdx].floor !== n) selectedIdx = -1;
      if (!silent) handlers.floor(n);
    }
    function defaultGoalIfNone() { defaultGoal(); }
    function selectApartment(id) {
      const i = aptMap[id];
      if (i == null) { selectedIdx = -1; return; }
      selectedIdx = i; selectFloor(apartmentInst[i].floor, true);
    }
    function clearSelection() { selectedIdx = -1; selBand.visible = selEdges.visible = false; defaultGoal(); }

    // ---------- loop ----------
    // The loop runs only while the stage is on screen and the tab is visible; otherwise no frame is even scheduled.
    let running = true, last = performance.now(), visible = true, raf = 0;
    const tmpC = new T.Color();
    const resume = () => { if (running && !raf && visible && !document.hidden) { last = performance.now(); raf = requestAnimationFrame(frame); } };
    document.addEventListener('visibilitychange', resume);
    function frame(now) {
      raf = 0;
      if (!running || !visible || document.hidden) return;
      raf = requestAnimationFrame(frame);
      const dt = Math.min(0.1, (now - last) / 1000); last = now;
      if (autoRotate && now - lastInput > 4500 && pointers.size === 0 && selBand && !selBand.visible) goal.az += dt * 0.045;
      const k = 1 - Math.exp(-dt * 6);
      view.az += (goal.az - view.az) * k; view.pol += (goal.pol - view.pol) * k; view.dist += (goal.dist - view.dist) * k;
      view.ty += (goal.ty - view.ty) * k; view.tx += (goal.tx - view.tx) * k; view.tz += (goal.tz - view.tz) * k;
      placeCamera();
      stepPreset(dt);
      const t = now / 1000;
      // hover / selected glow
      if (glow && glow.instanceColor) {
        if (!animIdx) animIdx = new Set();
        const touch = (i, boost, base) => {
          if (i < 0 || i >= apartmentInst.length) return;
          const c = colorFor(apartmentInst[i]).convertSRGBToLinear();
          tmpC.set(1, 0.95, 0.8);
          c.lerp(tmpC, boost); glow.setColorAt(i, c); animIdx.add(i);
        };
        let dirty = false;
        for (const i of [...animIdx]) { if (i !== hoverIdx && i !== selectedIdx) { const c = colorFor(apartmentInst[i]).convertSRGBToLinear(); glow.setColorAt(i, c); animIdx.delete(i); dirty = true; } }
        if (hoverIdx >= 0) { touch(hoverIdx, 0.55 + Math.sin(t * 6) * 0.1); dirty = true; }
        if (selectedIdx >= 0) { touch(selectedIdx, 0.62 + Math.sin(t * 4) * 0.3); dirty = true; }
        if (dirty) glow.instanceColor.needsUpdate = true;
      }
      if (selBand && selBand.visible) { selBand.material.opacity = 0.1 + Math.sin(t * 3) * 0.04; }
      if (roofLight) roofLight.visible = Math.sin(t * 3.2) > -0.2;
      cars.forEach((c) => {
        c.position.x += c.userData.dir * c.userData.v * dt;
        if (c.position.x > 450) c.position.x = -450; if (c.position.x < -450) c.position.x = 450;
      });
      clouds.forEach((c) => { c.position.x += c.userData.v * dt; if (c.position.x > 900) c.position.x = -900; });
      renderer.render(scene, camera);
    }
    var animIdx;

    function resize() {
      const w = container.clientWidth || 800, h = container.clientHeight || 500;
      renderer.setSize(w, h, false);
      renderer.domElement.style.width = '100%'; renderer.domElement.style.height = '100%';
      camera.aspect = w / h; camera.fov = w < h ? 52 : 38; camera.filmOffset = w / h > 1.5 ? -10 : 0; camera.updateProjectionMatrix();
    }
    const ro = new ResizeObserver(() => { resize(); if (!selBand || !selBand.visible) defaultGoal(); });
    ro.observe(container);
    const io = new IntersectionObserver((en) => { visible = en[en.length - 1].isIntersecting; resume(); }, { threshold: 0.01 });
    io.observe(container);

    build(opts.floors || 16);
    setTime(opts.time || 'dusk', true);
    resize(); defaultGoal(); Object.assign(view, goal); view.dist *= 1.25; view.az = goal.az - 0.8;
    placeCamera();
    resume();

    return {
      setData, selectFloor: (n) => selectFloor(n, true), selectApartment, clearSelection, resetView, setTime,
      setOverlay(v) { overlay = !!v; applyApartments(); },
      setAutoRotate(v) { autoRotate = !!v; },
      zoom(f) { goal.dist = Math.min(220, Math.max(24, goal.dist * f)); },
      rotate(a) { goal.az += a; lastInput = performance.now(); },
      on(name, fn) { handlers[name] = fn; },
      get preset() { return presetName; },
      get floors() { return floorsCount; },
      get renderer() { return renderer; },
      destroy() { running = false; cancelAnimationFrame(raf); document.removeEventListener('visibilitychange', resume); ro.disconnect(); io.disconnect(); renderer.dispose(); if (el.parentNode) el.parentNode.removeChild(el); },
    };
  }

  S.House3D = { create, STATUS_COLOR };
})();
