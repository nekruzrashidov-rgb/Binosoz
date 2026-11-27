/* Sitora 3D: the residential complex on three.js r128 — its blocks built from the data (outline, setbacks, place on the
   site), the yard, the parking levels and the lift shafts. Instanced windows are the pick targets: every window belongs to
   its apartment (the same split of the floor as the 2D plan, S.geom.floor in geometriya-blok-script.js). */
(function () {
  const S = (window.S = window.S || {});
  const T = window.THREE;

  // Floor height of the residential floors (the podium and the parking levels have their own, see floorY).
  const FLOOR_H = 3.3, D = 26;

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
    // Dushanbe night: deep navy sky, windows glow warmly, street lamps orange; bright enough that the house reads on a phone
    night: {
      top: 0x050c1a, horizon: 0x1e2c56, bottom: 0x0b1512, fog: 0x0c1830,
      sunColor: 0x7d9ad8, sunI: 0.5, sunPos: [60, 70, 40], hemiSky: 0x2a3c78, hemiGround: 0x0c120e, hemiI: 0.5,
      glow: 1.8, lamp: 2.4, stars: 1, cloud: 0.08, exposure: 1.45, env: 0.8,
    },
  };

  const lin = (hex) => new T.Color(hex).convertSRGBToLinear();

  // Dushanbe: the sun is computed for this point unless the building has its own coordinates. Local time is UTC+5 all year.
  const DUSHANBE = { lat: 38.56, lon: 68.78 }, TZ = 5;
  // Position of the sun (radians): elevation above the horizon and azimuth from the north, clockwise (east = π/2).
  // The usual low-precision formula (about 0.01°): enough for light and shadows.
  function sunPosition(date, lat, lon) {
    const rad = Math.PI / 180;
    const d = (date.getTime() - Date.UTC(2000, 0, 1, 12)) / 86400000;
    const g = (357.529 + 0.98560028 * d) * rad;
    const q = 280.459 + 0.98564736 * d;
    const L = (q + 1.915 * Math.sin(g) + 0.02 * Math.sin(2 * g)) * rad;
    const e = (23.439 - 0.00000036 * d) * rad;
    const ra = Math.atan2(Math.cos(e) * Math.sin(L), Math.cos(L));
    const dec = Math.asin(Math.sin(e) * Math.sin(L));
    const gmst = ((18.697374558 + 24.06570982441908 * d) % 24 + 24) % 24;
    const H = (gmst * 15 + lon) * rad - ra;
    const la = lat * rad;
    const el = Math.asin(Math.sin(la) * Math.sin(dec) + Math.cos(la) * Math.cos(dec) * Math.cos(H));
    const az = Math.atan2(-Math.sin(H), Math.tan(dec) * Math.cos(la) - Math.sin(la) * Math.cos(H));
    return { el, az: (az + Math.PI * 2) % (Math.PI * 2) };
  }
  // Today's date in Dushanbe and the moment of «hours» (decimal, local time) on it.
  function dushanbeDate(hours, base) {
    const now = base || new Date();
    const local = new Date(now.getTime() + TZ * 3600000);
    return new Date(Date.UTC(local.getUTCFullYear(), local.getUTCMonth(), local.getUTCDate()) + (hours - TZ) * 3600000);
  }
  function dushanbeHours(base) {
    const local = new Date((base || new Date()).getTime() + TZ * 3600000);
    return local.getUTCHours() + local.getUTCMinutes() / 60;
  }
  // Metres between two points (haversine) — the «straight line» distances of the labels.
  function distanceM(lat1, lon1, lat2, lon2) {
    const r = Math.PI / 180, R = 6371008.8;
    const a = Math.sin((lat2 - lat1) * r / 2) ** 2 + Math.cos(lat1 * r) * Math.cos(lat2 * r) * Math.sin((lon2 - lon1) * r / 2) ** 2;
    return 2 * R * Math.asin(Math.sqrt(a));
  }
  const easeInOut = (x) => (x < 0.5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2);

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
      g.fillStyle = '#7d828c'; g.fillRect(0, 0, w, h);
      // Windows a little darker than the wall: far away a high-contrast grid turned into grey-white noise.
      cells.forEach((c, i) => { const x = (i % 16) * 16, y = Math.floor(i / 16) * 16; g.fillStyle = '#4b5261'; g.fillRect(x + 3, y + 4, 10, 8); });
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
      g.font = '600 112px Manrope, Arial, sans-serif';
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
    const handlers = { select: opts.onSelectApartment || (() => {}), floor: opts.onSelectFloor || (() => {}), hover: opts.onHover || (() => {}), mode: () => {}, room: () => {}, view: () => {}, fps: () => {} };
    const rand = rnd(7);

    const renderer = new T.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
    // Phones and tablets draw at most 1.5 device pixels per CSS pixel: sharp enough, and much lighter for a budget GPU.
    const smallScreen = (window.matchMedia && window.matchMedia('(max-width: 1024px), (pointer: coarse)').matches);
    const maxRatio = smallScreen ? 1.5 : 2;
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, maxRatio));
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
    // One shadow map; a phone gets a smaller one.
    sun.shadow.mapSize.set(smallScreen ? 1024 : 2048, smallScreen ? 1024 : 2048);
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
      dash.frustumCulled = false; scene.add(dash); road.userData.dash = dash;
    }
    const paveT = paverTexture(rand); paveT.repeat.set(60, 3);
    const sidewalk = new T.Mesh(new T.BoxGeometry(900, 0.14, 8), new T.MeshStandardMaterial({ map: paveT, roughness: 0.9 }));
    sidewalk.position.set(0, 0.07, 30); sidewalk.receiveShadow = true; scene.add(sidewalk);
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
      terrainMesh = mt;
    }

    // neighbour towers (skyline): a generic backdrop, hidden when the real surroundings from OpenStreetMap are loaded
    const fakeNeighbours = new T.Group(); scene.add(fakeNeighbours);
    {
      const spots = [[60, -215, 30, 62, 26], [150, -190, 28, 50, 24], [-40, -255, 34, 78, 28], [-190, -235, 30, 58, 26], [250, -170, 30, 66, 26], [-120, -300, 36, 70, 30]];
      const roofM = new T.MeshStandardMaterial({ color: lin(0x6f737c), roughness: 0.9 });
      neighbourMat = [];
      spots.forEach(([x, z, w, h, d], i) => {
        const tx = skylineTextures(40 + i);
        // The texture is 16 × 32 windows: one every 3 m across and one floor every 3 m up (denser read as noise far away).
        const mk = (rx) => { const a = tx.alb.clone(), e = tx.em.clone(); a.needsUpdate = e.needsUpdate = true; a.repeat.set(rx / 48, h / 96); e.repeat.copy(a.repeat); return new T.MeshStandardMaterial({ map: a, emissiveMap: e, emissive: new T.Color(0xffffff), emissiveIntensity: 0.05, roughness: 0.75 }); };
        const mA = mk(w), mB = mk(d); neighbourMat.push(mA, mB);
        const b = new T.Mesh(new T.BoxGeometry(w, h, d), [mB, mB, roofM, roofM, mA, mA]);
        b.position.set(x, h / 2 - 3, z); b.castShadow = true; b.receiveShadow = true; fakeNeighbours.add(b);
      });
    }
    var neighbourMat, terrainMesh;

    // ---------- the complex: blocks on one site ----------
    // A model { blocks: [{ id, code, name, footprint, position, setbacks, floors, elevators, amenities }], site } from the API
    // (Footprint, SitePosition, Setbacks of every block, SitePlan of the organization); without it — one plain block of the
    // old size (a building without an outline). Plan axes: x to the right, y down the plan → world x and z; the complex is
    // centred at the origin. Floors: −2, −1 underground (parking), 1–2 the podium (taller), then the residential floors.
    const tower = new T.Group();
    scene.add(tower);
    const under = new T.Group(); under.visible = false; scene.add(under); // parking levels, shown in the parking mode
    let apartmentInst = []; // apartment index -> { block, floor, slot, id, status, data, windows: [instance], kind }
    let winApt = [];        // window instance -> apartment index (−1 — a window of no apartment)
    let spotApt = [];       // parking spot instance -> apartment index
    let floorsCount = 16;
    let glass, glow, frames, spots;
    let selBand, hoverIdx = -1, selectedIdx = -1;
    let roofLight, lampBulbs = [], lampMat, signMat, terraceLamps = null;
    // opts.overlay === false: windows lit like a lived-in house, without status colours (renders for the site's pictures).
    let overlay = opts.overlay !== false;
    let aptMap = {}; // id -> apartment index
    let keyMap = {}; // "block:floor:slot" -> apartment index
    let blocks = [], site = null, model = null, complexR = 60, siteCenter = { x: 0, y: 0 };
    let shafts = [], cabins = [], dims = [], yardZones = [], parkingOn = false, xray = false;
    const bodyMats = [];
    let pickMeshes = [], selBlock = -1;

    const mats = {
      stone: new T.MeshStandardMaterial({ color: lin(0x9a6a44), roughness: 0.42, metalness: 0.45 }),
      concrete: new T.MeshStandardMaterial({ color: lin(0xd8d2c4), roughness: 0.82 }),
      frame: new T.MeshStandardMaterial({ color: lin(0x232830), roughness: 0.3, metalness: 0.78 }),
      glassWall: new T.MeshStandardMaterial({ color: lin(0x1a3558), roughness: 0.03, metalness: 0.98, transparent: true, opacity: 0.72 }),
      railGlass: new T.MeshStandardMaterial({ color: lin(0x3a6a98), roughness: 0.04, metalness: 0.6, transparent: true, opacity: 0.28, depthWrite: false }),
      roof: new T.MeshStandardMaterial({ color: lin(0x484440), roughness: 0.96 }),
      granite: new T.MeshStandardMaterial({ color: lin(0x2e2b28), roughness: 0.75, metalness: 0.05 }),
      slab: new T.MeshStandardMaterial({ color: lin(0xc8c0b0), roughness: 0.65 }),
      crown: new T.MeshStandardMaterial({ color: lin(0xb88a5e), roughness: 0.45, metalness: 0.35 }),
      shaft: new T.MeshStandardMaterial({ color: lin(0x7a8696), roughness: 0.5, metalness: 0.3, transparent: true, opacity: 0.55, depthWrite: false }),
      cabin: new T.MeshStandardMaterial({ color: lin(0xf2c46a), roughness: 0.4, emissive: lin(0xf2c46a), emissiveIntensity: 0.4 }),
      parkSlab: new T.MeshStandardMaterial({ color: lin(0x6b6f76), roughness: 0.9, transparent: true, opacity: 0.85 }),
      planter: new T.MeshStandardMaterial({ color: lin(0x5f8a4a), roughness: 1 }),
    };
    // Each block its own accent of the cladding, one style for the complex.
    const ACCENT = [0xece6d8, 0xe9dcc8, 0xe3e3dc, 0xe8e0d0];

    // Heights: the podium (floors 1–2) is taller; underground levels 3,2 m each.
    const PODIUM_H = 4.5;
    function floorY(n) { return n < 0 ? n * 3.2 : n <= 2 ? (n - 1) * PODIUM_H : 2 * PODIUM_H + (n - 3) * FLOOR_H; }
    function floorTop(n) { return n < 0 ? (n + 1) * 3.2 : floorY(n) + (n <= 2 ? PODIUM_H : FLOOR_H); }
    function floorMid(n) { return (floorY(n) + floorTop(n)) / 2; }

    function clearTower() {
      [tower, under].forEach((g) => { while (g.children.length) { const o = g.children.pop(); o.traverse && o.traverse((c) => { if (c.geometry) c.geometry.dispose(); }); } });
      bodyMats.length = 0; shafts = []; cabins = []; dims = []; yardZones = [];
      removeLabels('blocks'); removeLabels('dim'); removeLabels('yard');
    }

    // Plan → world.
    const W2 = (p) => ({ x: p.x - siteCenter.x, z: p.y - siteCenter.y });
    // A ring on the plan (placed on the site) as a THREE.Shape in world x, −z (extruded up after rotateX(−π/2)).
    function shapeOf(ring) { return new T.Shape(ring.map((p) => { const w = W2(p); return new T.Vector2(w.x, -w.z); })); }
    function extrude(ring, y0, h, holeRing) {
      const sh = shapeOf(ring);
      if (holeRing) sh.holes.push(new T.Path(holeRing.slice().reverse().map((p) => { const w = W2(p); return new T.Vector2(w.x, -w.z); })));
      const g = new T.ExtrudeGeometry(sh, { depth: h, bevelEnabled: false, steps: 1, curveSegments: 1 });
      g.rotateX(-Math.PI / 2); g.translate(0, y0, 0);
      return g;
    }
    // The outline grown outwards by d metres (an inward offset with a negative depth).
    const grow = (rawPlaced, d) => S.geom.offsetInward(rawPlaced.map((p) => S.geom.pt([p.x, p.y])), -d).inner;
    const placedRaw = (b, raw) => S.geom.place(raw.map(S.geom.pt), b.position);
    const placedExp = (b, raw) => S.geom.place(S.geom.expand(raw), b.position);

    // A plain block of the old size, for a building without an outline.
    function plainModel(n) {
      return { blocks: [{ id: null, code: null, name: '', footprint: [[0, 0], [38, 0], [38, 26], [0, 26]], position: { x: -19, y: -13, rotationDeg: 0 }, setbacks: [], floors: Math.max(1, n), elevators: null, amenities: [] }], site: null, plain: true };
    }

    function build(m) {
      clearTower(); pickMeshes = [];
      model = m && m.blocks && m.blocks.length ? m : plainModel(+m || 16);
      site = model.site || null;
      // The site's centre: the middle of all blocks (and of the site outline when there is one).
      const all = model.blocks.flatMap((b) => placedExp(b, b.footprint));
      const bb = S.geom.bounds(all);
      siteCenter = { x: (bb.x0 + bb.x1) / 2, y: (bb.y0 + bb.y1) / 2 };
      complexR = Math.max(30, Math.hypot(bb.x1 - bb.x0, bb.y1 - bb.y0) / 2);
      floorsCount = Math.max(...model.blocks.map((b) => b.floors));
      const plaster = plasterTexture(rnd(3));
      const placements = []; // windows: { x, y, z, ry, w, h, apt, kind }
      apartmentInst = []; aptMap = {}; keyMap = {};
      const aptOf = (bi, f, slot, kind) => {
        const key = bi + ':' + f + ':' + slot;
        if (keyMap[key] == null) { keyMap[key] = apartmentInst.length; apartmentInst.push({ block: bi, floor: f, slot, id: null, status: 'free', data: null, windows: [], kind: kind || 'residential', rand: rand() }); }
        return keyMap[key];
      };
      blocks = model.blocks.map((b, bi) => {
        const g = new T.Group(); g.userData.block = bi; tower.add(g);
        const tex = plaster.clone(); tex.needsUpdate = true; tex.repeat.set(1 / 4, 1 / FLOOR_H);
        const bodyM = new T.MeshStandardMaterial({ map: tex, color: lin(ACCENT[bi % ACCENT.length]), roughness: 0.88, transparent: true, opacity: 1 });
        bodyMats.push(bodyM);
        const fpOf = (n) => S.geom.footprintOf({ footprint: b.footprint, setbacks: b.setbacks }, n);
        const depthOf = (n) => { let d = 7; (b.setbacks || []).forEach((s) => { if (n >= s.fromFloor && s.planDepth) d = s.planDepth; }); return d; };
        const H = floorTop(b.floors);
        // Runs of floors with the same outline: one extruded body each (the podium of floors 1–2 in granite).
        let runStart = 1;
        for (let n = 1; n <= b.floors + 1; n++) {
          const same = n <= b.floors && fpOf(n) === fpOf(runStart) && !(runStart <= 2 && n === 3);
          if (same) continue;
          const ring = placedExp(b, fpOf(runStart)), y0 = floorY(runStart), y1 = floorTop(n - 1);
          const mesh = new T.Mesh(extrude(ring, y0, y1 - y0), runStart <= 2 && b.floors > 3 && !model.plain ? mats.granite : bodyM);
          mesh.castShadow = mesh.receiveShadow = true; mesh.userData.pick = { block: bi }; g.add(mesh); pickMeshes.push(mesh);
          runStart = n;
        }
        // Slab edges of every floor (merged: one draw call per block); the roof of a setback — a terrace with a glass rail.
        const slabs = [];
        for (let n = 2; n <= b.floors; n++) slabs.push(extrude(grow(placedRaw(b, fpOf(n)), 0.12), floorY(n) - 0.09, 0.18));
        const top = fpOf(b.floors), base = b.footprint, setback = top !== base;
        // The crown: a cornice over the top floor and a parapet.
        const topRing = placedRaw(b, top);
        slabs.push(extrude(grow(topRing, 0.6), H, 0.45));
        slabs.push(extrude(grow(topRing, 0.15), H + 0.45, 1.0, S.geom.offsetInward(topRing.map((p) => S.geom.pt([p.x, p.y])), 0.25).inner));
        if (setback) {
          // The terrace: the roof of the floor below the setback (paving), the glass rail on its edge, planters.
          const below = b.floors - 1, ty = floorTop(below);
          slabs.push(extrude(grow(placedRaw(b, base), 0.12), ty - 0.05, 0.12));
          const rail = new T.Mesh(extrude(grow(placedRaw(b, base), 0.02), ty + 0.07, 1.1, S.geom.offsetInward(placedRaw(b, base).map((p) => S.geom.pt([p.x, p.y])), 0.06).inner), mats.railGlass);
          rail.renderOrder = 2; g.add(rail);
        }
        const slabMesh = new T.Mesh(mergeGeometries(slabs), mats.slab); slabMesh.castShadow = true; g.add(slabMesh);

        // Windows of every floor along the facade of each unit (the same split as the 2D plan, S.geom.floor).
        for (let n = 1; n <= b.floors; n++) {
          const kind = n <= 2 && b.floors > 3 && !model.plain ? 'commercial' : n === b.floors && setback ? 'penthouse' : 'residential';
          const units = (b.unitsOf && b.unitsOf(n)) || null;
          const shares = units && units.length ? units.map((u) => u.area || 1) : (kind === 'commercial' ? [1, 1, 1, 1, 1, 1] : kind === 'penthouse' ? [1, 1, 1, 1] : [1, 1, 1, 1, 1, 1, 1, 1]);
          const plan = S.geom.floor(fpOf(n), shares, { depth: depthOf(n), lifts: b.elevators || 0 });
          plan.units.forEach((u, k) => {
            const ai = aptOf(bi, n, units && units[k] && units[k].planSlot != null ? units[k].planSlot : k, kind);
            u.facade.forEach((s) => {
              const a = S.geom.place([s.a, s.b], b.position), p0 = W2(a[0]), p1 = W2(a[1]);
              const len = Math.hypot(p1.x - p0.x, p1.z - p0.z);
              if (len < (s.arc ? 0.9 : 1.6)) return;
              const step = kind === 'commercial' ? 4.2 : kind === 'penthouse' ? 2.6 : 3.4;
              const m = Math.max(1, Math.floor(len / step)), ww = Math.min(kind === 'residential' ? 1.8 : 3.4, len / m - 0.5);
              if (ww < 0.6) return;
              const dx = (p1.x - p0.x) / len, dz = (p1.z - p0.z) / len, ry = Math.atan2(dz, -dx);
              for (let i = 0; i < m; i++) {
                const t0 = (i + 0.5) / m;
                placements.push({ x: p0.x + (p1.x - p0.x) * t0, z: p0.z + (p1.z - p0.z) * t0, y: floorY(n), ry, w: ww, h: kind === 'residential' ? 2.0 : kind === 'penthouse' ? 2.9 : 3.4, fh: n <= 2 ? PODIUM_H : FLOOR_H, apt: ai, kind });
              }
            });
          });
          if (n === 3 || (b.floors <= 3 && n === 1)) b.core = plan.core;
        }
        // The lift core: 3 shafts from the parking (−2) to the roof and the stairs; seen in the cut-away and the parking mode.
        const core = b.core;
        if (core && (b.elevators || 0) > 0 && !model.plain) {
          const y0 = floorY(-2), y1 = H + 1.4;
          core.lifts.slice(0, b.elevators).forEach((r, k) => {
            const pr = S.geom.place(r, b.position).map(W2);
            const cx = pr.reduce((s, p) => s + p.x, 0) / pr.length, cz = pr.reduce((s, p) => s + p.z, 0) / pr.length;
            const sw = Math.hypot(pr[1].x - pr[0].x, pr[1].z - pr[0].z), sd = Math.hypot(pr[2].x - pr[1].x, pr[2].z - pr[1].z), ang = Math.atan2(-(pr[1].z - pr[0].z), pr[1].x - pr[0].x);
            const shaft = new T.Mesh(new T.BoxGeometry(sw, y1 - y0, sd), mats.shaft); shaft.position.set(cx, (y0 + y1) / 2, cz); shaft.rotation.y = ang;
            shaft.renderOrder = 3; shaft.visible = false; g.add(shaft); shafts.push({ block: b.code, mesh: shaft });
            const cab = new T.Mesh(new T.BoxGeometry(sw * 0.8, 2.4, sd * 0.8), mats.cabin); cab.position.set(cx, floorY(1) + 1.2, cz); cab.rotation.y = ang;
            cab.visible = false; cab.userData = { y0: floorY(-2) + 1.2, y1: H - 1.2, phase: k * 2.1 + bi }; g.add(cab); cabins.push(cab);
          });
        }
        // Parking levels −1/−2 under the block: plates and spots (pick targets), shown in the parking mode.
        if (b.parkingLevels && b.parkingLevels.length) {
          b.parkingLevels.forEach((lv) => under.add(new T.Mesh(extrude(placedExp(b, b.footprint), floorY(lv) - 0.25, 0.25), mats.parkSlab)));
        }
        // Entrance: a canopy over the street-facing (largest z) edge's middle, on the podium.
        if (!model.plain) {
          const ring = placedRaw(b, base).map(W2);
          let best = null;
          ring.forEach((p, i) => { const q = ring[(i + 1) % ring.length], len = Math.hypot(q.x - p.x, q.z - p.z), mz = (p.z + q.z) / 2; if (len > 6 && (!best || mz > best.mz)) best = { p, q, mz, len }; });
          if (best) {
            const mx = (best.p.x + best.q.x) / 2, ry = Math.atan2(best.q.z - best.p.z, -(best.q.x - best.p.x));
            const canopy = new T.Mesh(new T.BoxGeometry(Math.min(10, best.len * 0.5), 0.35, 3.2), mats.stone);
            canopy.position.set(mx, PODIUM_H - 0.4, best.mz); canopy.rotation.y = ry; canopy.translateZ(1.6); canopy.castShadow = true; g.add(canopy);
          }
        }
        const bbB = S.geom.bounds(placedExp(b, base)), ctr = W2({ x: (bbB.x0 + bbB.x1) / 2, y: (bbB.y0 + bbB.y1) / 2 });
        return { code: b.code, name: b.name, id: b.id, floors: b.floors, H, group: g, center: ctr, ring: placedExp(b, base), raw: b, setback, windows: 0 };
      });

      // Windows: one instanced set for the whole complex (glass is the pick target, glow is the light behind it).
      const count = Math.max(1, placements.length);
      glass = new T.InstancedMesh(new T.BoxGeometry(1, 1, 0.06), new T.MeshStandardMaterial({ color: lin(0x3a6ea8), roughness: 0.03, metalness: 0.95, transparent: true, opacity: 0.55, envMapIntensity: 2.0 }), count);
      glow = new T.InstancedMesh(new T.PlaneGeometry(1, 1), new T.MeshBasicMaterial({ color: 0xffffff, toneMapped: true }), count);
      frames = new T.InstancedMesh(new T.BoxGeometry(1, 1, 0.24), mats.frame, count);
      [glass, glow, frames].forEach((m) => { m.frustumCulled = false; });
      frames.castShadow = true;
      const o = new T.Object3D(), white = new T.Color(0xffffff);
      winApt = [];
      placements.forEach((p, i) => {
        const place = (mesh, lz, sx, sy) => { o.position.set(p.x, p.y + p.fh / 2 + 0.1, p.z); o.rotation.set(0, p.ry, 0); o.translateZ(lz); o.scale.set(sx, sy, 1); o.updateMatrix(); mesh.setMatrixAt(i, o.matrix); };
        place(frames, 0.04, p.w + 0.26, p.h + 0.26);
        place(glow, 0.17, p.w, p.h);
        place(glass, 0.19, p.w, p.h);
        glass.setColorAt(i, white); glow.setColorAt(i, white);
        winApt.push(p.apt); if (p.apt >= 0) { const a = apartmentInst[p.apt]; a.windows.push(i); if (!a.pos) a.pos = { x: p.x, y: p.y + p.fh / 2, z: p.z, ry: p.ry }; }
        blocks[apartmentInst[p.apt].block].windows++;
      });
      [glass, glow, frames].forEach((m) => { m.instanceMatrix.needsUpdate = true; tower.add(m); });
      glass.instanceColor.needsUpdate = true; glow.instanceColor.needsUpdate = true;

      // Parking spots: one instanced set; each spot an apartment (kind parking) of its block and level.
      const spotPl = [];
      model.blocks.forEach((b, bi) => (b.parkingLevels || []).forEach((lv) => {
        const n = (b.unitsOf && b.unitsOf(lv) || []).length;
        if (!n) return;
        const core = blocks[bi].raw.core;
        S.geom.parking(b.footprint, n, { avoid: core ? core.poly : null }).forEach((s, k) => {
          const units = b.unitsOf(lv);
          const ai = aptOf(bi, lv, units[k] && units[k].planSlot != null ? units[k].planSlot : k, 'parking');
          const c = W2(S.geom.place([S.geom.pt([s.x, s.y])], b.position)[0]);
          spotPl.push({ x: c.x, z: c.z, y: floorY(lv), w: s.w, d: s.h, rot: (b.position.rotationDeg || 0) * Math.PI / 180, apt: ai });
          if (!apartmentInst[ai].pos) apartmentInst[ai].pos = { x: c.x, y: floorY(lv) + 1.5, z: c.z, ry: 0, spot: true };
        });
      }));
      spotApt = [];
      if (spotPl.length) {
        spots = new T.InstancedMesh(new T.BoxGeometry(1, 0.08, 1), new T.MeshBasicMaterial({ color: 0xffffff, toneMapped: false }), spotPl.length);
        spotPl.forEach((s, i) => { o.position.set(s.x, s.y + 0.05, s.z); o.rotation.set(0, -s.rot, 0); o.scale.set(s.w - 0.25, 1, s.d - 0.25); o.updateMatrix(); spots.setMatrixAt(i, o.matrix); spots.setColorAt(i, white); spotApt.push(s.apt); });
        spots.frustumCulled = false; spots.instanceMatrix.needsUpdate = true; under.add(spots);
      } else spots = null;

      // The selection band of a floor (built for the chosen block and floor when it is picked).
      selBand = new T.Mesh(new T.BufferGeometry(), new T.MeshBasicMaterial({ color: 0xf2c46a, transparent: true, opacity: 0.16, depthWrite: false, toneMapped: false }));
      selBand.visible = false; tower.add(selBand);
      // Pick bodies: the blocks' own meshes carry userData.pick.
      buildSite();
      applyApartments();
      buildGroundStuff();
      fitLimits();
    }

    // Labels over the blocks («Блок A»), the yard's zones and the distance between neighbouring blocks (on «Сверху»).
    function blockLabels() {
      removeLabels('blocks'); removeLabels('dim');
      if (model.plain) return;
      blocks.forEach((b) => {
        const el = document.createElement('div'); el.className = 'h3d-tag h3d-tag--block'; el.dataset.block = b.code; el.textContent = b.name || ('Блок ' + b.code);
        addLabel(el, [b.center.x, b.H + 6, b.center.z], { group: 'blocks', prio: 8 });
      });
      dims.forEach((d) => {
        const el = document.createElement('div'); el.className = 'h3d-tag h3d-tag--dim';
        el.textContent = '≈ ' + Math.round(d.d) + ' ' + S.t('h3d.m') + ' · ' + S.t('h3d.byConcept');
        d.label = addLabel(el, [d.mid.x, 2, d.mid.z], { group: 'dim', prio: 7 });
        d.label.on = currentView === 'top';
      });
    }

    // The common site: the yard's zones from SitePlan — the playground and the football pitch only when their amenity is
    // in the data (honest data); paths and lawn; the parking ramp. The dimension lines between the blocks.
    function buildSite() {
      const amen = new Set(model.blocks.flatMap((b) => b.amenities || []));
      const zones = (site && site.zones) || [];
      const flat = (ring, y, mat) => { const g = new T.ShapeGeometry(shapeOf(ring)); g.rotateX(-Math.PI / 2); g.translate(0, y, 0); const m = new T.Mesh(g, mat); m.receiveShadow = true; tower.add(m); return m; };
      if (site && site.boundary && site.boundary.length >= 3) flat(site.boundary.map(S.geom.pt), 0.05, new T.MeshStandardMaterial({ map: (() => { const t = paverTexture(rnd(5)); t.repeat.set(30, 30); return t; })(), roughness: 0.9 }));
      zones.forEach((z) => {
        const ring = (z.polygon || []).map(S.geom.pt);
        if (ring.length < 3) return;
        if (z.kind === 'lawn') flat(ring, 0.09, new T.MeshStandardMaterial({ color: lin(0x6f9a52), roughness: 1 }));
        else if (z.kind === 'path') flat(ring, 0.1, new T.MeshStandardMaterial({ color: lin(0xcfc6b6), roughness: 0.9 }));
        else if (z.kind === 'parking_ramp' && amen.has('underground_parking')) {
          const m = flat(ring, 0.12, new T.MeshStandardMaterial({ color: lin(0x3c3f46), roughness: 0.9 })); yardZones.push({ kind: z.kind, mesh: m });
        } else if (z.kind === 'football_field' && amen.has('football_field')) { yardZones.push({ kind: z.kind, mesh: footballField(ring) }); }
        else if (z.kind === 'playground' && amen.has('playground')) { yardZones.push({ kind: z.kind, mesh: playground(ring) }); }
      });
      removeLabels('yard');
      yardZones.filter((z) => z.kind !== 'parking_ramp').forEach((z) => {
        const el = document.createElement('div'); el.className = 'h3d-tag h3d-tag--yard'; el.textContent = S.t('amen.' + z.kind);
        const c = z.mesh.userData.center; const l = addLabel(el, [c.x, 1, c.z], { group: 'yard', prio: 6 }); l.on = yardView() && !parkingOn;
      });
      // Distances between neighbouring blocks (by their order), from the data — a line and a label on «Сверху».
      dims = [];
      for (let i = 0; i + 1 < blocks.length; i++) {
        const c = S.geom.closest(blocks[i].ring, blocks[i + 1].ring);
        const a = W2(c.a), b = W2(c.b);
        const line = new T.Line(new T.BufferGeometry().setFromPoints([new T.Vector3(a.x, 0.6, a.z), new T.Vector3(b.x, 0.6, b.z)]), new T.LineBasicMaterial({ color: 0xf2c46a, toneMapped: false }));
        line.visible = false; tower.add(line);
        dims.push({ a: blocks[i].code, b: blocks[i + 1].code, d: c.d, mid: { x: (a.x + b.x) / 2, z: (a.z + b.z) / 2 }, line });
      }
      blockLabels();
    }
    function zoneFrame(ring) {
      const w = ring.map(W2), xs = w.map((p) => p.x), zs = w.map((p) => p.z);
      const x0 = Math.min(...xs), x1 = Math.max(...xs), z0 = Math.min(...zs), z1 = Math.max(...zs);
      return { x0, x1, z0, z1, cx: (x0 + x1) / 2, cz: (z0 + z1) / 2, w: x1 - x0, d: z1 - z0 };
    }
    // A mini-football pitch: artificial turf with the marking (canvas), goals at both ends, a mesh fence around.
    function footballField(ring) {
      const f = zoneFrame(ring), g = new T.Group(); tower.add(g);
      const long = f.w >= f.d;
      const tex = canvasTex(512, 256, (c, w, h) => {
        for (let i = 0; i < 8; i++) { c.fillStyle = i % 2 ? '#3f8a3a' : '#469a40'; c.fillRect(i * w / 8, 0, w / 8, h); }
        c.strokeStyle = '#f4f4ee'; c.lineWidth = 4; c.strokeRect(8, 8, w - 16, h - 16);
        c.beginPath(); c.moveTo(w / 2, 8); c.lineTo(w / 2, h - 8); c.stroke();
        c.beginPath(); c.arc(w / 2, h / 2, h * 0.18, 0, Math.PI * 2); c.stroke();
        c.strokeRect(8, h * 0.3, w * 0.09, h * 0.4); c.strokeRect(w - 8 - w * 0.09, h * 0.3, w * 0.09, h * 0.4);
      });
      const turf = new T.Mesh(new T.PlaneGeometry(long ? f.w : f.d, long ? f.d : f.w), new T.MeshStandardMaterial({ map: tex, roughness: 0.95 }));
      turf.rotation.x = -Math.PI / 2; if (!long) turf.rotation.z = Math.PI / 2; turf.position.set(f.cx, 0.14, f.cz); turf.receiveShadow = true; g.add(turf);
      const white = new T.MeshStandardMaterial({ color: lin(0xf4f4ee), roughness: 0.5 });
      const L = long ? f.w : f.d, Wd = long ? f.d : f.w;
      [-1, 1].forEach((s) => {
        const goal = new T.Group();
        [-1.5, 1.5].forEach((x) => { const post = new T.Mesh(new T.CylinderGeometry(0.06, 0.06, 2, 8), white); post.position.set(x, 1, 0); goal.add(post); });
        const bar = new T.Mesh(new T.CylinderGeometry(0.06, 0.06, 3, 8), white); bar.rotation.z = Math.PI / 2; bar.position.y = 2; goal.add(bar);
        const net = new T.Mesh(new T.BoxGeometry(3, 2, 1), mats.railGlass); net.position.set(0, 1, -s * 0.5); goal.add(net);
        goal.position.set(f.cx + (long ? s * (L / 2 - 0.6) : 0), 0.14, f.cz + (long ? 0 : s * (L / 2 - 0.6))); goal.rotation.y = long ? s * Math.PI / 2 : (s > 0 ? Math.PI : 0); g.add(goal);
      });
      const fence = new T.Mesh(new T.BoxGeometry(f.w + 1, 3, f.d + 1), new T.MeshStandardMaterial({ color: lin(0x9aa4ae), roughness: 0.6, transparent: true, opacity: 0.16, depthWrite: false, side: T.DoubleSide }));
      fence.position.set(f.cx, 1.6, f.cz); fence.renderOrder = 2; g.add(fence);
      void Wd;
      g.userData.center = { x: f.cx, z: f.cz };
      return g;
    }
    // A children's playground: a soft surface, a slide, swings and a sandbox (simple shapes).
    function playground(ring) {
      const f = zoneFrame(ring), g = new T.Group(); tower.add(g);
      const soft = new T.Mesh(new T.PlaneGeometry(f.w, f.d), new T.MeshStandardMaterial({ color: lin(0xc8643c), roughness: 1 }));
      soft.rotation.x = -Math.PI / 2; soft.position.set(f.cx, 0.13, f.cz); soft.receiveShadow = true; g.add(soft);
      const col = (c) => new T.MeshStandardMaterial({ color: lin(c), roughness: 0.6 });
      const tower1 = new T.Mesh(new T.BoxGeometry(2, 2.2, 2), col(0x3f7fbf)); tower1.position.set(f.cx - f.w / 4, 1.1, f.cz); tower1.castShadow = true; g.add(tower1);
      const slide = new T.Mesh(new T.BoxGeometry(0.9, 0.1, 3.6), col(0xf2c03c)); slide.position.set(f.cx - f.w / 4, 1.1, f.cz + 2.2); slide.rotation.x = -0.55; g.add(slide);
      const swingM = col(0x3c3c44);
      [[-0.9, 0], [0.9, 0]].forEach(([x]) => { const p = new T.Mesh(new T.CylinderGeometry(0.07, 0.07, 2.4, 8), swingM); p.position.set(f.cx + f.w / 4 + x, 1.2, f.cz - 1.5); g.add(p); });
      const beam = new T.Mesh(new T.CylinderGeometry(0.06, 0.06, 2, 8), swingM); beam.rotation.z = Math.PI / 2; beam.position.set(f.cx + f.w / 4, 2.4, f.cz - 1.5); g.add(beam);
      const sand = new T.Mesh(new T.BoxGeometry(3, 0.3, 3), col(0xe2cf9a)); sand.position.set(f.cx + f.w / 4, 0.15, f.cz + 2); g.add(sand);
      g.userData.center = { x: f.cx, z: f.cz };
      return g;
    }

    // Lamps, trees and cars around the complex.
    const extras = new T.Group(); scene.add(extras);
    let cars = [];
    const streetLamps = []; // the drawn street's lamps: hidden with the real roads
    function buildGroundStuff() {
      while (extras.children.length) extras.remove(extras.children[0]);
      cars = []; lampBulbs = []; streetLamps.length = 0;
      const zFront = complexR * 0.95 + 18; // the street in front of the complex
      road.position.z = zFront + 10; sidewalk.position.z = zFront + 2; if (road.userData.dash) road.userData.dash.position.z = zFront + 10 - 40;
      plaza.visible = !!model.plain;
      lampMat = new T.MeshBasicMaterial({ color: 0xffd9a0, toneMapped: false });
      const poleM = mats.frame;
      for (let i = -9; i <= 9; i++) {
        const x = i * 22, z = zFront + 4.2;
        const pole = new T.Mesh(new T.CylinderGeometry(0.1, 0.14, 7, 8), poleM); pole.position.set(x, 3.5, z); pole.castShadow = true; extras.add(pole); streetLamps.push(pole);
        const bulb = new T.Mesh(new T.SphereGeometry(0.34, 10, 8), lampMat.clone()); bulb.position.set(x, 6.78, z + 1.2); extras.add(bulb); lampBulbs.push(bulb); streetLamps.push(bulb);
      }
      // Trees: on the lawn and along the street, never on a block, the pitch or the playground (instanced).
      const blocked = blocks.map((b) => grow(b.ring.map((p) => p), 4)).map((r) => r.map(W2));
      const zonesW = yardZones.map((z) => z.mesh.userData.center ? zoneFrame(((site.zones || []).find((q) => q.kind === z.kind) || { polygon: [] }).polygon.map(S.geom.pt)) : null).filter(Boolean);
      const free = (x, z) => !blocked.some((r) => S.geom.contains(r.map((p) => ({ x: p.x, y: p.z })), { x, y: z })) && !zonesW.some((f) => x > f.x0 - 2 && x < f.x1 + 2 && z > f.z0 - 2 && z < f.z1 + 2);
      const spotsT = [];
      for (let i = -9; i <= 9; i++) { const x = i * 11 + 4; if (Math.abs(x) > 8) spotsT.push([x, zFront - 3.8]); }
      const lawns = ((site && site.zones) || []).filter((z) => z.kind === 'lawn').map((z) => zoneFrame(z.polygon.map(S.geom.pt)));
      lawns.forEach((f) => { for (let i = 0; i < 40; i++) { const x = f.x0 + rand() * f.w, z = f.z0 + rand() * f.d; if (free(x, z)) spotsT.push([x, z]); } });
      for (let i = 0; i < 46; i++) { const a = rand() * Math.PI * 2, r = complexR + 14 + rand() * 40, x = Math.cos(a) * r, z = Math.sin(a) * r; if (z < zFront - 6 && free(x, z)) spotsT.push([x, z]); }
      const trunkG = new T.CylinderGeometry(0.2, 0.3, 3.4, 7), crownG = new T.IcosahedronGeometry(2.1, 1);
      const trunks = new T.InstancedMesh(trunkG, new T.MeshStandardMaterial({ color: lin(0x5a4333), roughness: 1 }), spotsT.length);
      const crowns = new T.InstancedMesh(crownG, new T.MeshStandardMaterial({ color: 0xffffff, roughness: 0.95, flatShading: true }), spotsT.length * 2);
      const m = new T.Matrix4(), col = new T.Color();
      spotsT.forEach(([x, z], i) => {
        const s = 0.8 + rand() * 0.7;
        m.makeScale(s, s, s); m.setPosition(x, 1.7 * s, z); trunks.setMatrixAt(i, m);
        for (let k = 0; k < 2; k++) {
          const cs = s * (k ? 0.75 : 1.05);
          m.makeScale(cs, cs * 0.95, cs); m.setPosition(x + (k ? 0.5 : 0), (3.6 + k * 1.5) * s, z + (k ? -0.3 : 0));
          crowns.setMatrixAt(i * 2 + k, m);
          col.setHSL(0.26 + rand() * 0.07, 0.28 + rand() * 0.14, 0.16 + rand() * 0.09); col.convertSRGBToLinear(); crowns.setColorAt(i * 2 + k, col);
        }
      });
      trunks.castShadow = crowns.castShadow = true; crowns.receiveShadow = true; trunks.frustumCulled = crowns.frustumCulled = false;
      extras.add(trunks); extras.add(crowns);
      // Warm lights of the penthouse terraces at night (one instanced set of small lamps along the terrace rails).
      const tl = [];
      blocks.forEach((b) => { if (!b.setback) return; const ring = b.ring.map(W2), y = floorTop(b.floors - 1) + 0.5; ring.forEach((p, i) => { const q = ring[(i + 1) % ring.length], len = Math.hypot(q.x - p.x, q.z - p.z); for (let s = 2; s < len; s += 6) tl.push([p.x + (q.x - p.x) * s / len, y, p.z + (q.z - p.z) * s / len]); }); });
      terraceLamps = null;
      if (tl.length) {
        terraceLamps = new T.InstancedMesh(new T.SphereGeometry(0.16, 6, 4), new T.MeshBasicMaterial({ color: 0xffc27a, toneMapped: false }), tl.length);
        tl.forEach(([x, y, z], i) => { m.makeTranslation(x, y, z); terraceLamps.setMatrixAt(i, m); });
        terraceLamps.frustumCulled = false; extras.add(terraceLamps);
      }
      const colors = [0xd8d8d8, 0x23324a, 0xa23a32, 0x2b2f35, 0xe8e2d2, 0x3a5f4c];
      for (let i = 0; i < 7; i++) {
        const car = new T.Group();
        const body = new T.Mesh(new T.BoxGeometry(4.3, 0.8, 1.8), new T.MeshStandardMaterial({ color: lin(colors[i % colors.length]), roughness: 0.35, metalness: 0.6 }));
        body.position.y = 0.75; body.castShadow = true; car.add(body);
        const cab = new T.Mesh(new T.BoxGeometry(2.2, 0.7, 1.6), new T.MeshStandardMaterial({ color: lin(0x1a2230), roughness: 0.1, metalness: 0.9 }));
        cab.position.set(-0.2, 1.4, 0); car.add(cab);
        const dir = i % 2 ? 1 : -1;
        car.position.set(-400 + rand() * 800, 0.03, zFront + 10 + (dir > 0 ? -2.6 : 2.6));
        car.rotation.y = dir > 0 ? 0 : Math.PI;
        car.userData = { dir, v: 7 + rand() * 7 };
        extras.add(car); cars.push(car);
      }
    }

    // The parking mode: the blocks see-through, the ground hidden, the two parking levels with their spots and statuses.
    // The cut-away (xray) shows the lift shafts and the moving cabins.
    function setXray(on) {
      xray = !!on;
      bodyMats.forEach((m) => { m.opacity = xray ? 0.22 : 1; m.depthWrite = !xray; });
      [mats.granite, mats.slab].forEach((m) => { m.transparent = xray; m.opacity = xray ? 0.25 : 1; m.depthWrite = !xray; m.needsUpdate = true; });
      // See-through: the windows would hide the shafts and the parking — they go with the walls.
      [glass, glow, frames].forEach((m) => { if (m) m.visible = !xray; });
      shafts.forEach((s) => { s.mesh.visible = xray; });
      cabins.forEach((c) => { c.visible = xray; });
    }
    function setParking(on) {
      parkingOn = !!on;
      under.visible = parkingOn;
      // The ground over the parking is taken away while it is shown (the levels are under it).
      lawn.visible = !parkingOn; if (terrainMesh) terrainMesh.visible = !parkingOn; [road, sidewalk].forEach((m) => { m.visible = !parkingOn && !contextOn; });
      tower.children.forEach((c) => { if (c.isMesh && c.geometry && c.geometry.type === 'ShapeGeometry') c.visible = !parkingOn; });
      // The yard and the trees stand on the parking's roof: hidden while it is shown.
      yardZones.forEach((z) => { z.mesh.visible = !parkingOn; });
      extras.visible = !parkingOn;
      labels.forEach((l) => { if (l.group === 'yard') l.on = !parkingOn && yardView(); });
      setXray(parkingOn);
      if (parkingOn) flyTo({ pol: 0.8, ty: -5, tx: 0, tz: 0, dist: complexR * 2.3 * (portrait() ? 1.4 : 1) }, 1);
    }

    // ---------- apartments data ----------
    // Without the status overlay: lived-in windows, some lit and some dark — random but the same every time (seed 7).
    // An apartment that does not pass the catalog filters is dimmed (a.dim).
    function colorFor(a) {
      let c;
      if (!a) return new T.Color(0x1a2030);
      if (!overlay) {
        c = new T.Color();
        if (a.rand < 0.42) c.setHex(0x1a2030);
        else c.setHSL(0.09 + a.rand * 0.04, 0.8, 0.55 + (a.rand - 0.42) * 0.3);
      } else c = new T.Color(STATUS_COLOR[a.status] || STATUS_COLOR.free);
      if (a.dim) c.multiplyScalar(0.12);
      return c;
    }
    const tmpW = new T.Color(1, 1, 1);
    function paintApt(i, col) {
      const a = apartmentInst[i];
      const c = new T.Color();
      c.copy(overlay && a.status !== 'sold' && !a.dim ? col : tmpW).lerp(tmpW, a.dim ? 0.3 : 0.78);
      if (a.dim) c.multiplyScalar(0.45);
      a.windows.forEach((w) => { glow.setColorAt(w, col); glass.setColorAt(w, c); });
    }
    function applyApartments() {
      apartmentInst.forEach((a, i) => { const col = colorFor(a); col.convertSRGBToLinear(); paintApt(i, col); });
      // A window of no apartment (no data for that floor) stays dark.
      winApt.forEach((ai, w) => { if (ai < 0) { const c = new T.Color(0x1a2030); glow.setColorAt(w, c); } });
      glow.instanceColor.needsUpdate = true; glass.instanceColor.needsUpdate = true;
      if (spots) {
        const sc = new T.Color();
        spotApt.forEach((ai, k) => { const a = apartmentInst[ai]; sc.set(STATUS_COLOR[a && a.status] || STATUS_COLOR.free); if (a && a.dim) sc.multiplyScalar(0.2); sc.convertSRGBToLinear(); spots.setColorAt(k, sc); });
        spots.instanceColor.needsUpdate = true;
      }
    }

    function normStatus(s) {
      s = String(s == null ? 'free' : s).toLowerCase().replace(/[^a-z]/g, '');
      if (s === '0') return 'free';
      return STATUS_COLOR[s] ? s : 'free';
    }

    // list: [{ id, block (letter or building id; empty — the one building), floor, planSlot, number, status, kind, area }].
    // complex: { blocks: [{ id, code, name, footprint, position, setbacks, floors, elevators, amenities }], site } — the
    // complex's blocks from the API; without it (a building without an outline) — one plain block.
    let lastSig = '', lastComplex = null;
    function setData(list, complex) {
      if (complex !== undefined) lastComplex = complex;
      const cx = lastComplex && lastComplex.blocks && lastComplex.blocks.length ? lastComplex : null;
      const keyOf = (a) => (cx ? String(a.block == null ? '' : a.block) : '');
      const byBlock = {};
      list.forEach((a) => { (byBlock[keyOf(a)] = byBlock[keyOf(a)] || []).push(a); });
      const unitsFn = (arr) => (n) => (arr || []).filter((a) => a.floor === n).sort((x, y) => (x.planSlot != null ? x.planSlot : x.number || 0) - (y.planSlot != null ? y.planSlot : y.number || 0));
      let m;
      if (cx) {
        m = { site: cx.site || null, blocks: cx.blocks.map((b) => {
          const arr = byBlock[String(b.code)] || byBlock[String(b.id)] || [];
          return Object.assign({}, b, { unitsOf: unitsFn(arr), parkingLevels: [...new Set(arr.filter((a) => a.floor < 0).map((a) => a.floor))].sort((x, y) => x - y) });
        }) };
      } else {
        const arr = list;
        const n = arr.reduce((mx, a) => Math.max(mx, a.floor || 0), 0) || floorsCount;
        m = plainModel(n); m.blocks[0].unitsOf = unitsFn(arr); m.blocks[0].parkingLevels = [];
      }
      // Rebuilt only when the shape of the data changes (blocks, floors, units per floor); a new status just repaints.
      const sig = JSON.stringify(m.blocks.map((b) => [b.code, b.floors, JSON.stringify(b.footprint), JSON.stringify(b.position), JSON.stringify(b.setbacks),
        b.parkingLevels, Array.from({ length: b.floors + 3 }, (_, k) => k - 2).map((f) => b.unitsOf(f).map((u) => (u.planSlot || 0) + ':' + Math.round((u.area || 0) * 10)).join(','))]));
      if (!glass || sig !== lastSig) { lastSig = sig; build(m); }
      apartmentInst.forEach((a) => { a.id = null; a.status = 'sold'; a.data = null; });
      aptMap = {};
      m.blocks.forEach((b, bi) => {
        const arr = cx ? (byBlock[String(b.code)] || byBlock[String(b.id)] || []) : list;
        arr.forEach((a) => {
          const sorted = b.unitsOf(a.floor);
          const slot = a.planSlot != null && a.planSlot >= 0 ? a.planSlot : sorted.indexOf(a);
          const i = keyMap[bi + ':' + a.floor + ':' + slot];
          if (i == null) return;
          const inst = apartmentInst[i];
          inst.id = a.id; inst.status = normStatus(a.status); inst.data = a;
          aptMap[a.id] = i;
        });
      });
      applyFilter();
    }
    // Catalog filters in the model: fn(apartment) === false — its windows are dimmed. null — every window as usual.
    let filterFn = null;
    function applyFilter() {
      apartmentInst.forEach((a) => { a.dim = !!(filterFn && (a.id == null || !filterFn(a.data))); });
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
      return { top: s.top.clone(), horizon: s.horizon.clone(), bottom: s.bottom.clone(), fog: s.fog.clone(), sunColor: s.sunColor.clone(), hemiSky: s.hemiSky.clone(), hemiGround: s.hemiGround.clone(), sunPos: s.sunPos.clone(), skyDir: s.skyDir ? s.skyDir.clone() : null, n: Object.assign({}, s.n) };
    }
    const COLS = ['top', 'horizon', 'bottom', 'fog', 'sunColor', 'hemiSky', 'hemiGround'];
    function mixPreset(a, b, k) {
      const o = clonePreset(a);
      COLS.forEach((n) => o[n].lerp(b[n], k));
      o.sunPos.lerp(b.sunPos, k);
      Object.keys(o.n).forEach((n) => { o.n[n] += (b.n[n] - o.n[n]) * k; });
      return o;
    }
    // ---------- time of day by the real sun of Dushanbe ----------
    // The light at «hours» (local time, today): the sun's elevation picks between the day, dusk and night looks
    // (≥ 12° day; 2…12° day ↔ golden hour; −6…2° golden hour ↔ night — civil twilight; lower — night). While the sun is up
    // the light comes from its real direction (x — east, z — south; the facade with the entrance looks south: the model's
    // orientation is conventional); at night from the moon side of the night look.
    const geo = { lat: DUSHANBE.lat, lon: DUSHANBE.lon };
    let clockH = null, lastSun = { el: 0, az: 0 };
    const SNAP = {};
    function sunDirOf(p) { return new T.Vector3(Math.cos(p.el) * Math.sin(p.az), Math.sin(p.el), -Math.cos(p.el) * Math.cos(p.az)); }
    function lightAt(hours) {
      if (!SNAP.day) Object.keys(PRESETS).forEach((k) => { SNAP[k] = snapshot(PRESETS[k]); });
      const p = sunPosition(dushanbeDate(hours), geo.lat, geo.lon); lastSun = p;
      const el = p.el * 180 / Math.PI;
      let s;
      if (el >= 12) s = clonePreset(SNAP.day);
      else if (el >= 2) s = mixPreset(SNAP.dusk, SNAP.day, (el - 2) / 10);
      else if (el >= -6) s = mixPreset(SNAP.night, SNAP.dusk, (el + 6) / 8);
      else s = clonePreset(SNAP.night);
      const dir = sunDirOf(p);
      const moon = SNAP.night.sunPos.clone().normalize();
      const low = sunDirOf({ el: 2 * Math.PI / 180, az: p.az });
      const lightDir = el >= 2 ? dir : el <= -6 ? moon : moon.clone().lerp(low, (el + 6) / 8).normalize();
      s.sunPos.copy(lightDir).multiplyScalar(150);
      s.skyDir = dir;
      return s;
    }
    function setClock(hours, smooth) {
      clockH = Math.max(0, Math.min(24, hours));
      const s = lightAt(clockH);
      presetName = s.n.stars > 0.6 ? 'night' : s.n.glow > 0.5 ? 'dusk' : 'day';
      tgt = s;
      if (!smooth || !cur) { cur = clonePreset(s); applyCur(); }
      envLater(smooth ? 1200 : 500);
    }
    // The reflections (PMREM of the sky) are rebuilt a moment after the light changes. Building them takes seconds of a still
    // scene on a weak GPU and on software WebGL, and with them every frame there is many times heavier — so they are only for a
    // GPU that keeps ≥ 20 fps at full quality (measured; opts.reflections === 'always' — the still renders). The rebuild waits
    // for a calm moment: not during a camera flight and not right after a touch — the camera the person asked for comes first.
    function envLater(delay) {
      clearTimeout(pmremTimer);
      const attempt = () => {
        const always = opts.reflections === 'always';
        if (!always && stats.quality < 2) return;
        if (fly || performance.now() - lastInput < QUALITY_CALM_MS || (!always && !stats.fps)) { pmremTimer = setTimeout(attempt, 700); return; }
        if (!always && stats.fps < 20) return;
        rebuildEnv();
      };
      pmremTimer = setTimeout(attempt, delay);
    }
    function dropEnv() { clearTimeout(pmremTimer); scene.environment = null; if (envTex) { envTex.dispose(); envTex = null; } }
    // The hour (local) when the sun is at «deg» above the horizon: in the afternoon (sunset side) or the morning.
    function hourAtElevation(deg, afternoon) {
      let lo = afternoon ? 12.5 : 3, hi = afternoon ? 22 : 12.5;
      for (let i = 0; i < 30; i++) {
        const mid = (lo + hi) / 2, el = sunPosition(dushanbeDate(mid), geo.lat, geo.lon).el * 180 / Math.PI;
        if ((el > deg) === afternoon) lo = mid; else hi = mid;
      }
      return (lo + hi) / 2;
    }
    function applyCur() {
      const u = skyMat.uniforms;
      u.top.value.copy(cur.top); u.horizon.value.copy(cur.horizon); u.bottom.value.copy(cur.bottom);
      u.sunColor.value.copy(cur.sunColor); u.sunDir.value.copy(cur.skyDir || cur.sunPos).normalize();
      scene.fog.color.copy(cur.fog);
      hemi.color.copy(cur.hemiSky); hemi.groundColor.copy(cur.hemiGround); hemi.intensity = cur.n.hemiI;
      sun.color.copy(cur.sunColor); sun.intensity = cur.n.sunI;
      sun.position.copy(cur.sunPos).normalize().multiplyScalar(260);
      starMat.opacity = cur.n.stars;
      clouds.forEach((c) => { c.material.opacity = cur.n.cloud * 0.55; });
      renderer.toneMappingExposure = cur.n.exposure;
      if (glow) { const g = cur.n.glow; glow.material.color.setRGB(g, g, g); }
      lampBulbs.forEach((b) => { b.material.color.setRGB(1, 0.85, 0.6).multiplyScalar(Math.max(0.05, cur.n.lamp)); b.visible = cur.n.lamp > 0.05 && !b.userData.off; });
      if (neighbourMat) neighbourMat.forEach((m) => { m.emissiveIntensity = 0.04 + cur.n.lamp * 0.85; });
      // The penthouses' terraces: warm lights in the evening and at night.
      if (terraceLamps) { terraceLamps.visible = cur.n.lamp > 0.2; terraceLamps.material.color.setRGB(1, 0.76, 0.48).multiplyScalar(Math.min(1.6, 0.4 + cur.n.lamp)); }
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
      presetName = name; clockH = null; tgt = snapshot(PRESETS[name]);
      if (instant || !cur) { cur = clonePreset(tgt); applyCur(); envLater(opts.reflections === 'always' ? 0 : 300); }
      else envLater(1400);
    }
    function stepPreset(dt) {
      if (!cur || !tgt) return;
      const k = 1 - Math.exp(-dt * 2.6);
      COLS.forEach((n) => cur[n].lerp(tgt[n], k));
      cur.sunPos.lerp(tgt.sunPos, k);
      if (tgt.skyDir) { if (!cur.skyDir) cur.skyDir = cur.sunPos.clone().normalize(); cur.skyDir.lerp(tgt.skyDir, k); } else cur.skyDir = null;
      Object.keys(cur.n).forEach((n) => { cur.n[n] += (tgt.n[n] - cur.n[n]) * k; });
      applyCur();
    }

    // ---------- camera / controls ----------
    const view = { az: 0.62, pol: 1.32, dist: 118, ty: 24, tx: 0, tz: 0 };
    const goal = Object.assign({}, view);
    // No slow turntable and no long flights for people who asked for less motion.
    const reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    let autoRotate = opts.autoRotate !== false && !reduceMotion, lastInput = 0;
    // 'house' — orbit around the building; 'inside' — the chosen apartment cut open; 'walk' — first person inside it.
    let mode = 'house';
    // In the full-screen mode a chosen apartment brings the camera to its windows (the catalog frame shows the floor).
    let focusApt = !!opts.focusApartment, centered = !!opts.centered;
    // The orbit never goes under the ground; zoom and pan have limits (wider with the surroundings loaded).
    const LIM = { polMin: 0.12, polMax: 1.5, distMin: 20, distMax: 260, pan: 120 };
    const fitLimits = () => { if (!contextOn) { LIM.distMax = Math.max(260, complexR * 6); LIM.pan = Math.max(120, complexR * 2); } };
    const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
    let fly = null;
    const vel = { az: 0, pol: 0 };
    const portrait = () => container.clientWidth < container.clientHeight;
    const houseH = () => Math.max(...blocks.map((b) => b.H), floorTop(1));
    function placeCamera() {
      const sp = Math.sin(view.pol), cp = Math.cos(view.pol);
      camera.position.set(view.tx + view.dist * sp * Math.sin(view.az), view.ty + view.dist * cp, view.tz + view.dist * sp * Math.cos(view.az));
      camera.lookAt(view.tx, view.ty, view.tz);
    }
    function nearAngle(a, ref) { while (a - ref > Math.PI) a -= Math.PI * 2; while (a - ref < -Math.PI) a += Math.PI * 2; return a; }
    function clampGoal(g) {
      g.pol = clamp(g.pol, LIM.polMin, LIM.polMax); g.dist = clamp(g.dist, LIM.distMin, LIM.distMax);
      const r = Math.hypot(g.tx, g.tz); if (r > LIM.pan) { g.tx *= LIM.pan / r; g.tz *= LIM.pan / r; }
    }
    // A smooth flight (ease in-out, about a second). Any touch of the user stops it where the camera is and goes on from
    // there — the input is never lost.
    function flyTo(to, dur) {
      const target = Object.assign({}, goal, to);
      target.az = nearAngle(target.az, view.az);
      clampGoal(target);
      vel.az = vel.pol = 0;
      Object.assign(goal, target);
      if (reduceMotion || dur === 0) { Object.assign(view, target); fly = null; return; }
      fly = { from: Object.assign({}, view), to: target, t0: performance.now(), dur: (dur || 1) * 1000 };
    }
    function stopFly() { if (fly) { fly = null; Object.assign(goal, view); } }
    function defaultGoalValues() {
      const H = houseH();
      const g = { az: goal.az, ty: H * 0.5, dist: Math.max(110, H * 2.35, complexR * 2.7) * (portrait() ? 1.45 : 1), pol: 1.36, tx: 0, tz: 0 };
      // opts.view: a fixed camera { az, pol, distK, tyK, tx } (frontend/tools/renders): distK and tyK scale the default distance and height.
      const v = opts.view;
      if (v) {
        if (v.az != null) g.az = v.az; if (v.pol != null) g.pol = v.pol;
        if (v.distK != null) g.dist *= v.distK; if (v.tyK != null) g.ty *= v.tyK;
        if (v.tx != null) g.tx = v.tx;
      }
      return g;
    }
    function defaultGoal() { if (mode === 'house') Object.assign(goal, defaultGoalValues()); }
    // Camera views of the full-screen mode: the whole house, the yard (the back), from the street (low, in front of the
    // entrance), from above, and the surroundings (far out).
    const VIEWS = ['overview', 'yard', 'street', 'top', 'area'];
    function viewGoal(name) {
      const H = houseH(), k = portrait() ? 1.45 : 1;
      switch (name) {
        case 'yard': return model && !model.plain ? { az: 0.15, pol: 0.95, dist: Math.max(90, complexR * 1.9) * k, ty: 8, tx: 0, tz: complexR * 0.15 } : { az: Math.PI + 0.5, pol: 1.2, dist: Math.max(120, H * 2.6) * k, ty: H * 0.5, tx: 0, tz: 0 };
        case 'street': return { az: 0.14, pol: 1.47, dist: (55 + H * 0.5 + complexR * 0.8) * k, ty: Math.min(H * 0.38, 26), tx: 0, tz: 0 };
        case 'top': return { az: 0.001, pol: LIM.polMin, dist: Math.max(150, H * 2.5, complexR * 3.9) * k, ty: 0, tx: 0, tz: 0 };
        case 'area': return { az: 0.85, pol: 0.95, dist: (contextOn ? 430 : 300) * (portrait() ? 1.1 : 1), ty: 0, tx: 0, tz: 0 };
        default: { const g = defaultGoalValues(); return Object.assign(g, { az: 0.62, pol: 1.3, ty: H * 0.6, dist: g.dist * 1.22 }); }
      }
    }
    let currentView = 'overview';
    const yardView = () => currentView === 'yard' || currentView === 'top';
    function setView(name, dur) {
      if (mode !== 'house') return;
      currentView = VIEWS.includes(name) ? name : 'overview';
      autoRotate = false; lastInput = performance.now();
      // The distances between the blocks are drawn on «Сверху» only.
      dims.forEach((d) => { d.line.visible = currentView === 'top'; if (d.label) d.label.on = currentView === 'top'; });
      labels.forEach((l) => { if (l.group === 'yard') l.on = !parkingOn && yardView(); });
      flyTo(viewGoal(currentView), dur == null ? 1.0 : dur);
      handlers.view(currentView);
    }
    function resetView() { defaultGoal(); selectFloor(null, true); }
    function focusFloor(n, bi) {
      const b = blocks[bi != null && bi >= 0 ? bi : 0];
      goal.ty = floorMid(n); goal.dist = 58 * (portrait() ? 1.35 : 1) + (blocks.length > 1 ? 30 : 0); goal.pol = n < 0 ? 1.1 : 1.42;
      if (b && blocks.length > 1) { goal.tx = b.center.x; goal.tz = b.center.z; }
    }
    // The world point of an apartment's window (its first window) and the direction its facade faces.
    function aptWorld(i) {
      const a = apartmentInst[i], p = a.pos || { x: 0, z: 0, ry: 0 };
      return { x: p.x, y: floorMid(a.floor), z: p.z, ry: p.ry, nx: Math.sin(p.ry), nz: Math.cos(p.ry) };
    }
    // The camera to a block (or the whole complex with null): its centre, a distance for its height.
    let focusCode = null;
    function focusBlock(code) {
      if (mode !== 'house') return;
      autoRotate = false;
      const bi = blocks.findIndex((b) => b.code === code);
      focusCode = bi < 0 ? null : code;
      lastInput = performance.now();
      if (bi < 0) { flyTo(viewGoal('overview'), 1); handlers.view(currentView = 'overview'); labels.forEach((l) => { if (l.group === 'yard') l.on = !parkingOn && yardView(); }); return; }
      const b = blocks[bi];
      flyTo({ tx: b.center.x, tz: b.center.z, ty: b.H * 0.5, dist: Math.max(70, b.H * 2.2) * (portrait() ? 1.4 : 1), pol: 1.3 }, 1.1);
    }
    function flyToApartment(i) {
      const p = aptWorld(i);
      const spot = apartmentInst[i].pos && apartmentInst[i].pos.spot;
      flyTo(spot ? { tx: p.x, tz: p.z, ty: p.y, pol: 0.9, dist: 30 * (portrait() ? 1.4 : 1) } : { tx: p.x - p.nx * 6, tz: p.z - p.nz * 6, ty: p.y, az: p.ry + 0.18, pol: 1.4, dist: 46 * (portrait() ? 1.4 : 1) }, 1.0);
    }

    // ---------- labels over the scene (HTML) ----------
    // The page makes the elements (texts in its language); the engine places them every frame and hides those that would
    // cover a label of a higher priority.
    const labelLayer = document.createElement('div');
    labelLayer.className = 'h3d-labels';
    container.appendChild(labelLayer);
    const labels = [], labelInset = { top: 0 };
    let labelAvoid = null;
    const pv = new T.Vector3();
    function addLabel(el, pos, o) {
      const l = Object.assign({ el, pos: new T.Vector3(pos[0], pos[1], pos[2]), group: 'misc', prio: 0, on: true, w: 0, h: 0, shown: false }, o || {});
      el.classList.add('h3d-label'); el.style.visibility = 'hidden';
      labelLayer.appendChild(el); labels.push(l);
      return l;
    }
    function removeLabels(group) {
      for (let i = labels.length - 1; i >= 0; i--) if (!group || labels[i].group === group) { labels[i].el.remove(); labels.splice(i, 1); }
    }
    function layoutLabels() {
      if (!labels.length) return;
      const w = container.clientWidth, h = container.clientHeight, placed = [];
      const topInset = labelInset.top || 0;
      const avoid = labelAvoid ? labelAvoid() : [];
      const hit = (r, list) => list.some((p) => r.x0 < p.x1 && r.x1 > p.x0 && r.y0 < p.y1 && r.y1 > p.y0);
      labels.slice().sort((a, b) => b.prio - a.prio).forEach((l) => {
        let show = l.on;
        let x = 0, y = 0;
        if (show) {
          pv.copy(l.pos).project(camera);
          x = (pv.x + 1) / 2 * w; y = (1 - pv.y) / 2 * h;
          show = pv.z < 1 && x > -40 && x < w + 40 && y > -40 && y < h + 40;
        }
        if (show) {
          if (!l.w) { l.w = l.el.offsetWidth || 90; l.h = l.el.offsetHeight || 26; }
          const base = { x0: x - l.w / 2 - 3, y0: y - l.h - 8, x1: x + l.w / 2 + 3, y1: y - 4 };
          // Over its point first; covering a control or a more important label — up, down, left, right; nowhere — hidden.
          const tries = l.free ? [[0, 0]] : [[0, 0], [0, -(l.h + 6)], [0, l.h + 12], [-(l.w / 2 + 12), 0], [l.w / 2 + 12, 0]];
          let r = null;
          for (const [dx, dy] of tries) {
            const c = { x0: base.x0 + dx, y0: base.y0 + dy, x1: base.x1 + dx, y1: base.y1 + dy };
            if (c.x0 < 0 || c.x1 > w || c.y0 < topInset || c.y1 > h || hit(c, avoid) || (!l.free && hit(c, placed))) continue;
            r = c; break;
          }
          if (!r) show = false;
          else { placed.push(r); l.el.style.transform = 'translate3d(' + Math.round(r.x0 + 3) + 'px,' + Math.round(r.y0) + 'px,0)'; }
        }
        if (show !== l.shown) { l.shown = show; l.el.style.visibility = show ? 'visible' : 'hidden'; l.el.setAttribute('aria-hidden', String(!show)); }
      });
    }

    // ---------- the real surroundings (OpenStreetMap, a static file made by frontend/tools/context/build.mjs) ----------
    // Shown only for a building with coordinates: neighbour buildings as glass boxes on their real outlines and heights,
    // roads, parks; the places nearby come back to the page with their distance in a straight line from the building.
    let contextOn = false;
    const contextGroup = new T.Group(); scene.add(contextGroup);
    const procedural = [road, sidewalk, road.userData.dash, fakeNeighbours];
    function project(lat, lon, o) {
      return [(lon - o.lon) * Math.cos(o.lat * Math.PI / 180) * 111320, -(lat - o.lat) * 110540];
    }
    function mergeGeometries(list) {
      // Indexed parts are unrolled first: their vertex count grows.
      const flat = list.map((g) => { const gg = g.index ? g.toNonIndexed() : g; if (gg !== g) g.dispose(); if (!gg.attributes.normal) gg.computeVertexNormals(); return gg; });
      let n = 0; flat.forEach((g) => { n += g.attributes.position.count; });
      const pos = new Float32Array(n * 3), nor = new Float32Array(n * 3);
      let o = 0;
      flat.forEach((g) => {
        pos.set(g.attributes.position.array, o * 3); nor.set(g.attributes.normal.array, o * 3); o += g.attributes.position.count;
        g.dispose();
      });
      const m = new T.BufferGeometry();
      m.setAttribute('position', new T.BufferAttribute(pos.subarray(0, o * 3), 3));
      m.setAttribute('normal', new T.BufferAttribute(nor.subarray(0, o * 3), 3));
      return m;
    }
    function clearContext() {
      contextGroup.children.slice().forEach((c) => { contextGroup.remove(c); c.geometry && c.geometry.dispose(); c.material && c.material.dispose(); });
      contextOn = false; procedural.forEach((m) => { if (m) m.visible = true; });
      streetLamps.forEach((m) => { m.userData.off = false; m.visible = true; });
      cars.forEach((c) => { c.userData.path = null; });
      LIM.distMax = Math.max(260, complexR * 6); LIM.pan = Math.max(120, complexR * 2);
      lawn.scale.set(1, 1, 1); lawn.position.set(0, 0, -130); cars.forEach((c) => { c.visible = true; });
    }
    function setContext(data, origin) {
      clearContext();
      if (!data || !origin || origin.lat == null || origin.lon == null) return [];
      const keepOut = (pts) => pts.some(([x, z]) => Math.hypot(x, z) < complexR + 6);
      const blds = [];
      (data.buildings || []).forEach((b) => {
        const pts = (b.pts || []).map(([la, lo]) => project(la, lo, origin));
        if (pts.length < 3 || keepOut(pts)) return;
        const shape = new T.Shape(pts.map(([x, z]) => new T.Vector2(x, -z)));
        const g = new T.ExtrudeGeometry(shape, { depth: Math.max(3, b.h || 12), bevelEnabled: false, steps: 1 });
        g.rotateX(-Math.PI / 2);
        blds.push(g);
      });
      if (blds.length) {
        const g = mergeGeometries(blds);
        const glassBox = new T.Mesh(g, new T.MeshStandardMaterial({ color: lin(0xcfdff0), roughness: 0.25, metalness: 0.1, transparent: true, opacity: 0.32, depthWrite: false }));
        glassBox.renderOrder = 2; contextGroup.add(glassBox);
        const edges = new T.LineSegments(new T.EdgesGeometry(g, 25), new T.LineBasicMaterial({ color: lin(0xeaf2fb), transparent: true, opacity: 0.55 }));
        edges.renderOrder = 3; edges.userData.edges = true; contextGroup.add(edges);
      }
      const ribbons = [];
      const paths = [];
      (data.roads || []).forEach((r) => {
        const pts = (r.pts || []).map(([la, lo]) => project(la, lo, origin));
        const hw = (r.w || 6) / 2;
        const pos = [];
        for (let i = 0; i < pts.length - 1; i++) {
          const [x1, z1] = pts[i], [x2, z2] = pts[i + 1];
          const dx = x2 - x1, dz = z2 - z1, L = Math.hypot(dx, dz) || 1, px = -dz / L * hw, pz = dx / L * hw;
          pos.push(x1 + px, 0.06, z1 + pz, x2 + px, 0.06, z2 + pz, x2 - px, 0.06, z2 - pz, x1 + px, 0.06, z1 + pz, x2 - px, 0.06, z2 - pz, x1 - px, 0.06, z1 - pz);
        }
        if (!pos.length) return;
        const g = new T.BufferGeometry(); g.setAttribute('position', new T.Float32BufferAttribute(pos, 3)); g.computeVertexNormals();
        ribbons.push(g);
        if ((r.w || 0) >= 7 && pts.length > 1) paths.push({ pts, w: r.w });
      });
      if (ribbons.length) {
        const rm = new T.Mesh(mergeGeometries(ribbons), new T.MeshStandardMaterial({ color: lin(0x3c3f46), roughness: 0.95, side: T.DoubleSide }));
        rm.receiveShadow = true; contextGroup.add(rm);
      }
      const greens = [];
      (data.parks || []).forEach((p) => {
        const pts = (p.pts || []).map(([la, lo]) => project(la, lo, origin));
        if (pts.length < 3) return;
        const g = new T.ShapeGeometry(new T.Shape(pts.map(([x, z]) => new T.Vector2(x, -z))));
        g.rotateX(-Math.PI / 2); g.translate(0, 0.04, 0); greens.push(g);
      });
      if (greens.length) {
        const gm = new T.Mesh(mergeGeometries(greens), new T.MeshStandardMaterial({ color: lin(0x5f8a4a), roughness: 1, side: T.DoubleSide }));
        gm.receiveShadow = true; contextGroup.add(gm);
      }
      // Real roads instead of the drawn street; cars drive along the widest ones.
      procedural.forEach((m) => { if (m) m.visible = false; });
      streetLamps.forEach((m) => { m.userData.off = true; m.visible = false; });
      paths.sort((a, b) => b.pts.length - a.pts.length);
      cars.forEach((c, i) => { const p = paths[i % Math.max(1, paths.length)]; c.userData.path = p ? { pts: p.pts, s: rand() * 300, len: p.pts.reduce((s, q, k) => s + (k ? Math.hypot(q[0] - p.pts[k - 1][0], q[1] - p.pts[k - 1][1]) : 0), 0) } : null; c.visible = !!p; });
      lawn.scale.set(2.2, 2.6, 1); lawn.position.set(0, 0, 0);
      contextOn = true; LIM.distMax = Math.max(520, complexR * 6); LIM.pan = 420;
      return (data.pois || []).map((p) => {
        const [x, z] = project(p.lat, p.lon, origin);
        return { cat: p.cat, name: p.name, x, z, dist: Math.round(distanceM(origin.lat, origin.lon, p.lat, p.lon)) };
      });
    }
    function driveCars(dt) {
      cars.forEach((c) => {
        const p = c.userData.path;
        if (!p) {
          c.position.x += c.userData.dir * c.userData.v * dt;
          if (c.position.x > 450) c.position.x = -450; if (c.position.x < -450) c.position.x = 450;
          return;
        }
        p.s = (p.s + c.userData.v * dt) % Math.max(1, p.len);
        let s = p.s;
        for (let k = 1; k < p.pts.length; k++) {
          const [x1, z1] = p.pts[k - 1], [x2, z2] = p.pts[k], L = Math.hypot(x2 - x1, z2 - z1);
          if (s <= L || k === p.pts.length - 1) {
            const f = L ? Math.min(1, s / L) : 0;
            const ox = -(z2 - z1) / (L || 1) * 1.6, oz = (x2 - x1) / (L || 1) * 1.6;
            c.position.set(x1 + (x2 - x1) * f + ox, 0.06, z1 + (z2 - z1) * f + oz);
            c.rotation.y = Math.atan2(-(z2 - z1), x2 - x1);
            break;
          }
          s -= L;
        }
      });
    }

    // ---------- the apartment inside and the walk ----------
    let inside = null, ghost = null;
    const aptLamp = new T.PointLight(0xffe2b8, 0, 12, 1.2); scene.add(aptLamp);
    const walkSt = { x: 0, z: 0, y: 0, yaw: 0, pitch: -0.05, target: null, keys: {} };
    function makeGhost(floor, bi) {
      const g = new T.Group(), b = blocks[bi] || blocks[0];
      const y = Math.max(0.1, floorY(floor)), ring = placedExp(b.raw, S.geom.footprintOf({ footprint: b.raw.footprint, setbacks: b.raw.setbacks }, floor));
      const below = new T.Mesh(extrude(ring, 0, y), new T.MeshStandardMaterial({ color: lin(0xe9e1d3), roughness: 0.9, transparent: true, opacity: 0.35, depthWrite: false }));
      g.add(below);
      const plate = new T.Mesh(extrude(grow(placedRaw(b.raw, b.raw.footprint), 0.6), y - 0.12, 0.24), new T.MeshStandardMaterial({ color: lin(0x8f887c), roughness: 0.9, transparent: true, opacity: 0.55, depthWrite: false }));
      plate.receiveShadow = true; g.add(plate);
      const outline = new T.LineSegments(new T.EdgesGeometry(extrude(b.ring, 0, b.H), 25), new T.LineBasicMaterial({ color: lin(0xf2c46a), transparent: true, opacity: 0.5 }));
      g.add(outline);
      return g;
    }
    function disposeTree(o) {
      o.traverse((c) => {
        if (c.geometry) c.geometry.dispose();
        const ms = c.material ? (Array.isArray(c.material) ? c.material : [c.material]) : [];
        ms.forEach((m) => { ['map', 'emissiveMap', 'normalMap', 'roughnessMap'].forEach((k) => { if (m[k]) m[k].dispose(); }); m.dispose(); });
      });
    }
    // layout: S.plans.layout(type, area). Needs modeli-3d-kvartira-script.js (S.House3D.interior), loaded by the page.
    function enterInside(id, layout, o) {
      const i = aptMap[id];
      if (i == null || !S.House3D.interior || !layout) return false;
      leaveInside(true);
      const a = apartmentInst[i], p = aptWorld(i);
      const built = S.House3D.interior.build(T, layout, { lin, furniture: !o || o.furniture !== false });
      const grp = built.group;
      grp.position.set(p.x - p.nx * 0.35, floorY(a.floor) + 0.13, p.z - p.nz * 0.35);
      grp.rotation.y = p.ry + Math.PI;
      scene.add(grp); grp.updateMatrixWorld(true);
      const v = new T.Vector3();
      const toWorld = (x, y) => { v.set(x, 0, y); built.inner.localToWorld(v); return [v.x, v.z]; };
      const walls = built.walls.map(([x1, y1, x2, y2]) => toWorld(x1, y1).concat(toWorld(x2, y2)));
      const rooms = built.rooms.map((r) => { const [x, z] = toWorld(r.cx, r.cy); return Object.assign({}, r, { x, z }); });
      const [cx, cz] = toWorld(layout.w / 2, layout.d / 2 - 0.6);
      // Warm ceiling lights of the apartment: brighter in the evening and at night (the light of the day comes from the sun).
      // (One light made once: adding and removing lights would recompile every material.)
      const lamp = aptLamp; lamp.distance = Math.max(layout.w, layout.d) * 1.6;
      v.set(layout.w / 2, 2.45, layout.d / 2); built.inner.localToWorld(v); lamp.position.copy(v);
      inside = { i, id, built, grp, walls, rooms, lamp, y: grp.position.y, center: { x: cx, z: cz }, ry: p.ry, size: Math.max(layout.w, layout.d) };
      tower.visible = false; selBand.visible = false;
      ghost = makeGhost(a.floor, a.block); scene.add(ghost);
      mode = 'inside'; autoRotate = false; LIM.distMin = 5; LIM.polMax = 1.45;
      flyTo({ tx: cx, tz: cz, ty: inside.y + 1.0, az: p.ry, pol: 0.58, dist: (inside.size * 1.5 + 5) * (portrait() ? 1.7 : 1) }, 1.1);
      handlers.mode(mode);
      return true;
    }
    function leaveInside(silent) {
      if (!inside) return;
      if (mode === 'walk') leaveWalk(true);
      scene.remove(inside.grp); disposeTree(inside.grp); aptLamp.intensity = 0;
      if (ghost) { scene.remove(ghost); disposeTree(ghost); ghost = null; }
      const i = inside.i;
      inside = null; tower.visible = true; LIM.distMin = 20; LIM.polMax = 1.5;
      mode = 'house';
      if (!silent) { selectedIdx = i; selectFloor(apartmentInst[i].floor, true, false, apartmentInst[i].block); flyToApartment(i); handlers.mode(mode); }
    }
    function roomFocus(k) {
      if (!inside || !inside.rooms[k]) return;
      const r = inside.rooms[k];
      if (mode === 'walk') { walkSt.target = { x: r.x, z: r.z }; return; }
      flyTo({ tx: r.x, tz: r.z, ty: inside.y + 1, dist: Math.max(7, inside.size * 0.9), pol: 0.9 }, 0.9);
      handlers.room(k);
    }
    function startWalk(k) {
      if (!inside) return;
      const r = inside.rooms[k != null ? k : inside.rooms.findIndex((x) => x.key === 'living' || x.key === 'bedroom')] || inside.rooms[0];
      walkSt.x = r.x; walkSt.z = r.z; walkSt.y = inside.y + 1.6; walkSt.pitch = -0.05; walkSt.target = null;
      walkSt.yaw = Math.atan2(Math.sin(inside.ry), Math.cos(inside.ry));
      fly = null; mode = 'walk'; inside.built.setCeiling(true); if (ghost) ghost.visible = false;
      camera.fov = 70; camera.filmOffset = 0; camera.near = 0.05; camera.updateProjectionMatrix();
      handlers.mode(mode);
    }
    function leaveWalk(silent) {
      if (mode !== 'walk') return;
      mode = 'inside'; walkSt.keys = {}; walkSt.target = null;
      if (inside) inside.built.setCeiling(false);
      if (ghost) ghost.visible = true;
      camera.near = 0.5; resize();
      if (!silent) { Object.assign(view, goal); handlers.mode(mode); }
    }
    // Circle (r 0.28 m) against the wall segments: pushed out of every wall it touches — doors are gaps, so the only way
    // between rooms is through them.
    function collide(x, z) {
      const R = 0.28;
      for (let it = 0; it < 3; it++) {
        for (const [x1, z1, x2, z2] of inside.walls) {
          const dx = x2 - x1, dz = z2 - z1, L2 = dx * dx + dz * dz || 1;
          const t = clamp(((x - x1) * dx + (z - z1) * dz) / L2, 0, 1);
          const px = x1 + dx * t, pz = z1 + dz * t, ex = x - px, ez = z - pz, d = Math.hypot(ex, ez);
          if (d < R) { const k = (R - d) / (d || 1); x += ex * k; z += ez * k; }
        }
      }
      return [x, z];
    }
    function stepWalk(dt) {
      const K = walkSt.keys, fwd = [Math.sin(walkSt.yaw), Math.cos(walkSt.yaw)], right = [-Math.cos(walkSt.yaw), Math.sin(walkSt.yaw)];
      let mx = 0, mz = 0;
      if (K.KeyW || K.ArrowUp) { mx += fwd[0]; mz += fwd[1]; }
      if (K.KeyS || K.ArrowDown) { mx -= fwd[0]; mz -= fwd[1]; }
      if (K.KeyD) { mx += right[0]; mz += right[1]; }
      if (K.KeyA) { mx -= right[0]; mz -= right[1]; }
      if (K.ArrowLeft) walkSt.yaw += dt * 1.8;
      if (K.ArrowRight) walkSt.yaw -= dt * 1.8;
      if (mx || mz) walkSt.target = null;
      else if (walkSt.target) {
        const ex = walkSt.target.x - walkSt.x, ez = walkSt.target.z - walkSt.z, d = Math.hypot(ex, ez);
        if (d < 0.08) walkSt.target = null; else { mx = ex / d * Math.min(1, d * 2); mz = ez / d * Math.min(1, d * 2); }
      }
      const L = Math.hypot(mx, mz);
      if (L) {
        const sp = 1.5 * dt / Math.max(1, L);
        const bx = walkSt.x, bz = walkSt.z;
        [walkSt.x, walkSt.z] = collide(walkSt.x + mx * sp, walkSt.z + mz * sp);
        if (walkSt.target && Math.hypot(walkSt.x - bx, walkSt.z - bz) < sp * 0.15) walkSt.target = null; // stuck at a wall
      }
      camera.position.set(walkSt.x, walkSt.y, walkSt.z);
      const cp = Math.cos(walkSt.pitch);
      camera.lookAt(walkSt.x + Math.sin(walkSt.yaw) * cp, walkSt.y + Math.sin(walkSt.pitch), walkSt.z + Math.cos(walkSt.yaw) * cp);
    }
    const onKey = (e) => {
      if (mode !== 'walk') return;
      const t = e.target;
      if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.isContentEditable)) return;
      if (!['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.code)) return;
      walkSt.keys[e.code] = e.type === 'keydown';
      e.preventDefault();
    };
    window.addEventListener('keydown', onKey); window.addEventListener('keyup', onKey);

    // ---------- pointer: orbit with inertia, pinch and pan; look around in the walk ----------
    const ray = new T.Raycaster(); const mouse = new T.Vector2();
    let pointers = new Map(), pinchD = 0, moved = 0, downPos = null, panning = false, lastMove = 0;
    const el = renderer.domElement;
    el.setAttribute('tabindex', '-1');
    function centroid() { const ps = [...pointers.values()]; return { x: ps.reduce((s, p) => s + p.x, 0) / ps.length, y: ps.reduce((s, p) => s + p.y, 0) / ps.length }; }
    let lastC = null;
    function pan(dx, dy) {
      const k = view.dist * 0.0016;
      const rx = Math.cos(view.az), rz = -Math.sin(view.az), fx = -Math.sin(view.az), fz = -Math.cos(view.az);
      goal.tx -= (rx * dx - fx * dy) * k; goal.tz -= (rz * dx - fz * dy) * k;
      const r = Math.hypot(goal.tx - (inside ? inside.center.x : 0), goal.tz - (inside ? inside.center.z : 0)), lim = inside ? 12 : LIM.pan;
      if (r > lim) { const cx = inside ? inside.center.x : 0, cz = inside ? inside.center.z : 0; goal.tx = cx + (goal.tx - cx) * lim / r; goal.tz = cz + (goal.tz - cz) * lim / r; }
    }
    el.addEventListener('pointerdown', (e) => {
      el.setPointerCapture(e.pointerId); pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
      downPos = { x: e.clientX, y: e.clientY }; moved = 0; lastInput = performance.now();
      stopFly(); vel.az = vel.pol = 0; autoRotate = false;
      panning = e.button === 2 || e.shiftKey;
      if (pointers.size === 2) { const [a, b] = [...pointers.values()]; pinchD = Math.hypot(a.x - b.x, a.y - b.y); lastC = centroid(); }
    });
    el.addEventListener('contextmenu', (e) => e.preventDefault());
    el.addEventListener('pointermove', (e) => {
      const p = pointers.get(e.pointerId);
      if (!p) { hoverAt(e); return; }
      lastInput = performance.now();
      const dx = e.clientX - p.x, dy = e.clientY - p.y;
      p.x = e.clientX; p.y = e.clientY; moved += Math.abs(dx) + Math.abs(dy);
      if (mode === 'walk') {
        if (pointers.size === 1) { walkSt.yaw += dx * 0.0042; walkSt.pitch = clamp(walkSt.pitch + dy * 0.0035, -1.1, 1.1); }
        return;
      }
      if (pointers.size === 1) {
        if (panning) { pan(dx, dy); return; }
        const now = performance.now(), dtm = Math.max(8, now - lastMove); lastMove = now;
        goal.az -= dx * 0.0062; goal.pol = clamp(goal.pol - dy * 0.0042, LIM.polMin, LIM.polMax);
        vel.az = -dx * 0.0062 / dtm * 1000; vel.pol = -dy * 0.0042 / dtm * 1000;
      } else if (pointers.size === 2) {
        const [a, b] = [...pointers.values()]; const d = Math.hypot(a.x - b.x, a.y - b.y);
        goal.dist = clamp(goal.dist * (pinchD / (d || 1)), LIM.distMin, LIM.distMax); pinchD = d;
        const c = centroid(); if (lastC) pan(c.x - lastC.x, c.y - lastC.y); lastC = c;
      }
      el.style.cursor = 'grabbing';
    });
    const up = (e) => {
      const had = pointers.has(e.pointerId); pointers.delete(e.pointerId); el.style.cursor = hoverIdx >= 0 ? 'pointer' : 'grab';
      if (pointers.size < 2) lastC = null;
      if (performance.now() - lastMove > 90) vel.az = vel.pol = 0;
      if (had && pointers.size === 0 && moved < 7 && downPos) { vel.az = vel.pol = 0; clickAt(e); }
    };
    el.addEventListener('pointerup', up); el.addEventListener('pointercancel', (e) => { pointers.delete(e.pointerId); vel.az = vel.pol = 0; });
    el.addEventListener('pointerleave', () => { setHover(-1); });
    el.addEventListener('wheel', (e) => {
      if (mode === 'walk') return;
      e.preventDefault(); lastInput = performance.now(); stopFly();
      goal.dist = clamp(goal.dist * Math.exp(e.deltaY * 0.0011), LIM.distMin, LIM.distMax);
    }, { passive: false });
    el.style.cursor = 'grab';

    function setRay(e) {
      const r = el.getBoundingClientRect();
      mouse.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
      ray.setFromCamera(mouse, camera);
    }
    function pick(e) {
      setRay(e);
      const targets = parkingOn && spots ? [spots] : [glass].concat(pickMeshes);
      const hits = ray.intersectObjects(targets, false);
      if (!hits.length) return null;
      const h = hits[0];
      if (h.object === glass) return winApt[h.instanceId] >= 0 ? { apt: winApt[h.instanceId], point: h.point } : null;
      if (h.object === spots) return { apt: spotApt[h.instanceId], point: h.point };
      const bi = h.object.userData.pick ? h.object.userData.pick.block : 0;
      return { floor: floorAt(h.point.y, bi), block: bi, point: h.point };
    }
    function floorAt(y, bi) {
      const b = blocks[bi] || blocks[0];
      if (y < 0) return y < -3.2 ? -2 : -1;
      for (let n = 1; n <= b.floors; n++) if (y < floorTop(n)) return n;
      return b.floors;
    }
    function setHover(i) {
      if (i === hoverIdx) return;
      hoverIdx = i; el.style.cursor = i >= 0 ? 'pointer' : 'grab';
      if (i < 0) handlers.hover(null);
    }
    let lastHoverEv = null, hoverQueued = false;
    function hoverAt(e) {
      if (mode !== 'house') return;
      lastHoverEv = e;
      if (hoverQueued) return; hoverQueued = true;
      requestAnimationFrame(() => {
        hoverQueued = false; if (!lastHoverEv || pointers.size || mode !== 'house') return;
        const r = pick(lastHoverEv);
        if (r && r.apt != null && apartmentInst[r.apt] && apartmentInst[r.apt].id != null) {
          setHover(r.apt);
          const rect = el.getBoundingClientRect();
          handlers.hover(apartmentInst[r.apt].data, lastHoverEv.clientX - rect.left, lastHoverEv.clientY - rect.top);
        } else if (r && r.floor != null) { setHover(-1); el.style.cursor = 'pointer'; }
        else setHover(-1);
      });
    }
    function clickAt(e) {
      if (mode !== 'house') {
        if (!inside) return;
        setRay(e);
        const hit = ray.intersectObjects(inside.built.floors, false)[0];
        if (!hit) return;
        if (mode === 'walk') walkSt.target = { x: hit.point.x, z: hit.point.z };
        else roomFocus(hit.object.userData.room);
        return;
      }
      const r = pick(e);
      if (!r) { handlers.floor(null); selectFloor(null); return; }
      if (r.apt != null) {
        const a = apartmentInst[r.apt];
        if (focusApt && a.id != null) { selectApartment(a.id, true); handlers.select(a.data); return; }
        selectFloor(a.floor, false, true, a.block);
        if (a.id != null) { selectedIdx = r.apt; handlers.select(a.data); }
      } else if (r.floor != null) {
        selectFloor(r.floor, false, true, r.block);
      }
    }
    // The chosen floor of the chosen block: a glowing band around that floor's outline (the setback's on its floors).
    function selectFloor(n, silent, fromClick, bi) {
      if (n == null) {
        selBand.visible = false; selectedIdx = -1; selBlock = -1; defaultGoal(); if (!silent) handlers.floor(null); return;
      }
      if (typeof bi === 'string') bi = blocks.findIndex((b) => b.code === bi);
      selBlock = bi != null && bi >= 0 ? bi : selBlock >= 0 ? selBlock : 0;
      const b = blocks[selBlock], fp = S.geom.footprintOf({ footprint: b.raw.footprint, setbacks: b.raw.setbacks }, Math.max(1, n));
      selBand.geometry.dispose();
      selBand.geometry = extrude(grow(placedRaw(b.raw, fp), 0.9), floorY(n), floorTop(n) - floorY(n));
      selBand.visible = n >= 1 || parkingOn;
      if (!focusApt) focusFloor(n, selBlock);
      if (selectedIdx >= 0 && (apartmentInst[selectedIdx].floor !== n || apartmentInst[selectedIdx].block !== selBlock)) selectedIdx = -1;
      if (!silent) handlers.floor(n, b.code);
    }
    // flyThere: true — fly to its windows; false — only mark it (fresh data, the camera stays where the person put it);
    // not given — fly in the full-screen mode (focusApt), stay in the catalog frame.
    function selectApartment(id, flyThere) {
      const i = aptMap[id];
      if (i == null) { selectedIdx = -1; return; }
      selectedIdx = i; selectFloor(apartmentInst[i].floor, true, false, apartmentInst[i].block);
      if (flyThere === true || (focusApt && flyThere !== false)) { autoRotate = false; flyToApartment(i); }
    }
    function clearSelection() { selectedIdx = -1; selBand.visible = false; if (!focusApt) defaultGoal(); }

    // ---------- speed: FPS and the automatic quality ----------
    // Below 30 frames a second for three seconds in a row the quality steps down (never back up in this visit):
    // 2 — everything; 1 — no shadows, at most 1.25 device pixels; 0 — 1 device pixel, no clouds, no outlines of the
    // neighbour buildings. A step rebuilds the shaders of every material — on a weak phone (and on software WebGL) the
    // scene stands still for seconds. So a step never comes during a camera flight or right after a touch (it waits for a
    // calm moment), and below 12 frames a second it goes straight to 0 — one rebuild, not two.
    const stats = { fps: 0, quality: 2, frames: 0, t0: performance.now(), slow: 0, since: performance.now() };
    const QUALITY_CALM_MS = 2500;
    function setQuality(q) {
      stats.quality = q;
      if (q <= 1) { sun.castShadow = false; renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.25)); dropEnv(); }
      if (q <= 0) { renderer.setPixelRatio(1); clouds.forEach((c) => { c.visible = false; }); contextGroup.children.forEach((c) => { if (c.userData.edges) c.visible = false; }); }
      resize();
    }
    function countFrame(now) {
      stats.frames++;
      if (now - stats.t0 < 1000) return;
      stats.fps = Math.round(stats.frames * 1000 / (now - stats.t0)); stats.frames = 0; stats.t0 = now;
      handlers.fps(stats.fps, stats.quality);
      // Not in the first 4 s after the loop (re)starts: the shaders compile and the shadows build then.
      if (opts.autoQuality === false || now - stats.since < 6000) return;
      stats.slow = stats.fps < 30 ? stats.slow + 1 : 0;
      if (stats.slow >= 3 && stats.quality > 0 && !fly && now - lastInput > QUALITY_CALM_MS) { stats.slow = 0; setQuality(stats.fps < 12 ? 0 : stats.quality - 1); }
    }

    // ---------- loop ----------
    // The loop runs only while the stage is on screen and the tab is visible; otherwise no frame is even scheduled.
    let running = true, last = performance.now(), visible = true, raf = 0;
    const tmpC = new T.Color();
    const resume = () => { if (running && !raf && visible && !document.hidden) { last = performance.now(); stats.t0 = stats.since = last; stats.frames = 0; raf = requestAnimationFrame(frame); } };
    document.addEventListener('visibilitychange', resume);
    let playing = null;
    function frame(now) {
      raf = 0;
      if (!running || !visible || document.hidden) return;
      raf = requestAnimationFrame(frame);
      const dt = Math.min(0.1, (now - last) / 1000); last = now;
      if (playing) {
        // The day in about 20 s: 05:00 → 22:00.
        let h = clockH + dt * (17 / playing.seconds);
        if (h >= 22) { h = 22; const cb = playing.done; playing = null; if (cb) cb(); }
        setClock(h, false);
        if (playing && playing.tick) playing.tick(h);
      }
      if (mode === 'walk') stepWalk(dt);
      else {
        if (autoRotate && now - lastInput > 4500 && pointers.size === 0 && selBand && !selBand.visible && mode === 'house' && !fly) goal.az += dt * 0.045;
        if (fly) {
          const f = Math.min(1, (now - fly.t0) / fly.dur), e = easeInOut(f);
          Object.keys(view).forEach((k) => { view[k] = fly.from[k] + (fly.to[k] - fly.from[k]) * e; });
          if (f >= 1) fly = null;
        } else {
          if (pointers.size === 0 && (Math.abs(vel.az) > 0.002 || Math.abs(vel.pol) > 0.002)) {
            goal.az += vel.az * dt; goal.pol = clamp(goal.pol + vel.pol * dt, LIM.polMin, LIM.polMax);
            const dec = Math.exp(-dt * 4); vel.az *= dec; vel.pol *= dec;
          }
          const k = 1 - Math.exp(-dt * 6);
          Object.keys(view).forEach((n) => { view[n] += (goal[n] - view[n]) * k; });
        }
        placeCamera();
      }
      stepPreset(dt);
      if (inside && cur) inside.lamp.intensity = 0.35 + Math.min(1.6, cur.n.glow * 0.9);
      const t = now / 1000;
      // hover / selected glow
      if (glow && glow.instanceColor && mode === 'house') {
        if (!animIdx) animIdx = new Set();
        const touch = (i, boost) => {
          if (i < 0 || i >= apartmentInst.length) return;
          const c = colorFor(apartmentInst[i]).convertSRGBToLinear();
          tmpC.set(1, 0.95, 0.8);
          c.lerp(tmpC, boost); apartmentInst[i].windows.forEach((w) => glow.setColorAt(w, c)); animIdx.add(i);
        };
        let dirty = false;
        for (const i of [...animIdx]) { if (i !== hoverIdx && i !== selectedIdx) { const c = colorFor(apartmentInst[i]).convertSRGBToLinear(); apartmentInst[i].windows.forEach((w) => glow.setColorAt(w, c)); animIdx.delete(i); dirty = true; } }
        if (hoverIdx >= 0) { touch(hoverIdx, 0.55 + Math.sin(t * 6) * 0.1); dirty = true; }
        if (selectedIdx >= 0) { touch(selectedIdx, 0.62 + Math.sin(t * 4) * 0.3); dirty = true; }
        if (dirty) glow.instanceColor.needsUpdate = true;
      }
      if (selBand && selBand.visible) { selBand.material.opacity = 0.1 + Math.sin(t * 3) * 0.04; }
      if (roofLight) roofLight.visible = Math.sin(t * 3.2) > -0.2;
      // Lift cabins ride between −2 and the top floor (not at the lowest quality).
      if (xray && stats.quality > 0) cabins.forEach((c) => { const u = c.userData; c.position.y = u.y0 + (u.y1 - u.y0) * (0.5 + 0.5 * Math.sin(t * 0.22 + u.phase)); });
      driveCars(dt);
      clouds.forEach((c) => { c.position.x += c.userData.v * dt; if (c.position.x > 900) c.position.x = -900; });
      renderer.render(scene, camera);
      layoutLabels();
      countFrame(now);
      // The picture is on screen (a dozen frames: the very first ones can still be black while the GPU compiles the
      // shaders and builds the shadows) — the page can take away its placeholder render.
      if (!drawnOnce && ++framesDrawn >= 12) { drawnOnce = true; container.classList.add('is-ready'); if (opts.onReady) opts.onReady(); }
    }
    var animIdx, drawnOnce = false, framesDrawn = 0;

    function resize() {
      const w = container.clientWidth || 800, h = container.clientHeight || 500;
      renderer.setSize(w, h, false);
      renderer.domElement.style.width = '100%'; renderer.domElement.style.height = '100%';
      camera.aspect = w / h;
      if (mode !== 'walk') { camera.fov = w < h ? 52 : 38; camera.filmOffset = w / h > 1.5 && !centered ? -10 : 0; }
      camera.updateProjectionMatrix();
      labels.forEach((l) => { l.w = 0; });
    }
    const ro = new ResizeObserver(() => { resize(); if (mode === 'house' && !fly && lastInput === 0 && (!selBand || !selBand.visible)) defaultGoal(); });
    ro.observe(container);
    const io = new IntersectionObserver((en) => { visible = en[en.length - 1].isIntersecting; resume(); }, { threshold: 0.01 });
    io.observe(container);

    build(+opts.floors || 16);
    setTime(opts.time || 'dusk', true);
    resize(); defaultGoal(); Object.assign(view, goal); view.dist *= 1.25; view.az = goal.az - 0.8;
    if (opts.view) Object.assign(view, goal);
    placeCamera();
    resume();

    const api = {
      setData, selectFloor: (n, block) => selectFloor(n, true, false, block), selectApartment, clearSelection, resetView, setTime,
      // The complex: a block (letter) or the whole complex (null); the parking mode; the cut-away with the lift shafts.
      focusBlock, setParking, setXray: (v) => { setXray(v); }, get parking() { return parkingOn; },
      get blocks() { return blocks.map((b) => ({ code: b.code, name: b.name, floors: b.floors, H: b.H })); },
      setOverlay(v) { overlay = !!v; applyApartments(); },
      get overlay() { return overlay; },
      setAutoRotate(v) { autoRotate = !!v; },
      setFilter(fn) { filterFn = fn || null; applyFilter(); },
      setFocusMode(v) { focusApt = !!v; centered = !!v; resize(); },
      zoom(f) { stopFly(); goal.dist = clamp(goal.dist * f, LIM.distMin, LIM.distMax); },
      rotate(a) { stopFly(); goal.az += a; lastInput = performance.now(); },
      on(name, fn) { handlers[name] = fn; },
      // Full-screen mode
      views: VIEWS, setView, get currentView() { return currentView; },
      setClock(h, smooth) { playing = null; setClock(h, smooth); },
      get clock() { return clockH; },
      // Play the day from «from» to 22:00 in «seconds»; tick(h) on every frame; done() at the end.
      play(from, seconds, tick, done) { setClock(from, false); playing = { seconds: seconds || 20, tick, done }; },
      stop() { playing = null; },
      get playing() { return !!playing; },
      nowHours: () => dushanbeHours(),
      hourAtElevation,
      sun() { return { el: lastSun.el * 180 / Math.PI, az: lastSun.az * 180 / Math.PI }; },
      setGeo(lat, lon) { if (lat != null && lon != null) { geo.lat = +lat; geo.lon = +lon; } },
      addLabel, removeLabels,
      // Labels never go under the page's top bar (px from the top of the scene).
      setLabelInset(top) { labelInset.top = top || 0; },
      // Boxes (px in the scene) labels must not cover — the page's panels and buttons: fn() → [{ x0, y0, x1, y1 }].
      setLabelAvoid(fn) { labelAvoid = fn || null; },
      // The page changed a label's text: measure again.
      labelsChanged() { labels.forEach((l) => { l.w = 0; l.h = 0; }); },
      // The list's row under the pointer lights its apartment's windows (null — none).
      hoverApartment(id) { const i = id == null || aptMap[id] == null ? -1 : aptMap[id]; if (i === hoverIdx) return; hoverIdx = i; resume(); },
      // World points for the page's labels.
      anchors() { return { top: [0, houseH() + 4, 0], H: houseH(), blocks: blocks.map((b) => ({ code: b.code, top: [b.center.x, b.H + 6, b.center.z] })) }; },
      roomAnchors() { return inside ? inside.rooms.map((r) => [r.x, inside.y + 1.3, r.z]) : []; },
      setContext, clearContext, get hasContext() { return contextOn; },
      enterInside, leaveInside: () => leaveInside(false), roomFocus, startWalk, leaveWalk: () => leaveWalk(false),
      setFurniture(v) { if (inside) inside.built.setFurniture(!!v); },
      get mode() { return mode; },
      get insideRooms() { return inside ? inside.rooms : []; },
      get preset() { return presetName; },
      get floors() { return floorsCount; },
      get renderer() { return renderer; },
      get stats() { return { fps: stats.fps, quality: stats.quality }; },
      // For the tests (window.__house3d) and the page: camera, mode, light, the complex.
      // A window's world point and its apartment (the tests click real windows): instance → { id, block, x, y, z }.
      windowPoints(n) {
        const out = [], mtx = new T.Matrix4(), v = new T.Vector3();
        const ids = winApt.map((a, w) => [a, w]).filter(([a]) => a >= 0 && apartmentInst[a].id != null);
        for (let k = 0; k < Math.min(n || 10, ids.length); k++) {
          const [a, w] = ids[Math.floor((k + 0.5) * ids.length / Math.min(n || 10, ids.length))];
          glass.getMatrixAt(w, mtx); v.setFromMatrixPosition(mtx);
          const p = v.clone().project(camera);
          out.push({ id: apartmentInst[a].id, block: blocks[apartmentInst[a].block].code, x: (p.x + 1) / 2 * container.clientWidth, y: (1 - p.y) / 2 * container.clientHeight, visible: p.z < 1 && Math.abs(p.x) < 1 && Math.abs(p.y) < 1 });
        }
        return out;
      },
      // The screen point (px in the scene) of an apartment's first window, or null.
      windowPoint(id) {
        const i = aptMap[id]; if (i == null || !apartmentInst[i].windows.length) return null;
        const mtx = new T.Matrix4(), v = new T.Vector3(); glass.getMatrixAt(apartmentInst[i].windows[0], mtx); v.setFromMatrixPosition(mtx).project(camera);
        return { x: (v.x + 1) / 2 * container.clientWidth, y: (1 - v.y) / 2 * container.clientHeight, visible: v.z < 1 && Math.abs(v.x) < 1 && Math.abs(v.y) < 1, block: blocks[apartmentInst[i].block].code };
      },
      lookAtApartment(id) { const i = aptMap[id]; if (i != null) { stopFly(); const p = aptWorld(i); Object.assign(goal, { tx: p.x, tz: p.z, ty: p.y, az: Math.atan2(p.nx, p.nz), pol: 1.45, dist: 9 }); Object.assign(view, goal); placeCamera(); camera.updateMatrixWorld(); } },
      state() {
        return { mode, view: Object.assign({}, view), currentView, clock: clockH, sun: this.sun(), sunIntensity: sun.intensity, exposure: renderer.toneMappingExposure,
          selected: selectedIdx >= 0 ? apartmentInst[selectedIdx].id : null, fps: stats.fps, quality: stats.quality, camera: camera.position.toArray(), context: contextOn,
          walk: mode === 'walk' ? { x: walkSt.x, z: walkSt.z, yaw: walkSt.yaw } : null, labels: labels.filter((l) => l.shown).length,
          // The complex from the scene's own data (for the tests): blocks, the gaps between them, lifts, the yard, parking.
          blocks: blocks.map((b) => ({ code: b.code, floors: b.floors, windows: b.windows, center: [b.center.x, b.center.z], setback: b.setback })),
          gaps: dims.map((d) => ({ a: d.a, b: d.b, d: Math.round(d.d * 10) / 10 })),
          elevators: blocks.map((b) => ({ block: b.code, shafts: shafts.filter((s) => s.block === b.code).length })),
          amenities: yardZones.map((z) => z.kind),
          parking: { on: parkingOn, levels: [...new Set(apartmentInst.filter((a) => a.kind === 'parking').map((a) => a.floor))].sort((x, y) => x - y), spots: spotApt.length },
          xray, selectedBlock: selBlock >= 0 && blocks[selBlock] ? blocks[selBlock].code : null, focusBlock: focusCode, hover: hoverIdx >= 0 ? apartmentInst[hoverIdx].id : null,
          drawCalls: renderer.info.render.calls, triangles: renderer.info.render.triangles };
      },
      destroy() {
        running = false; cancelAnimationFrame(raf);
        document.removeEventListener('visibilitychange', resume);
        window.removeEventListener('keydown', onKey); window.removeEventListener('keyup', onKey);
        ro.disconnect(); io.disconnect(); clearTimeout(pmremTimer);
        leaveInside(true); removeLabels(); labelLayer.remove(); scene.remove(under);
        disposeTree(scene);
        if (envTex) envTex.dispose(); pm.dispose();
        renderer.dispose(); if (renderer.forceContextLoss) renderer.forceContextLoss();
        if (el.parentNode) el.parentNode.removeChild(el);
      },
    };
    return api;
  }

  S.House3D = Object.assign(S.House3D || {}, { create, STATUS_COLOR, sunPosition, distanceM });
})();
