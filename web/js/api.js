/* API layer: talks to the real Sitora API (when an address is set) or to the in-browser demo backend.
   All responses are normalised here so views never depend on exact field names.
   Contract of the real API: docs/API_CONTRACT.md. The demo backend (mock.js) speaks an older dialect,
   so mappers accept both shapes and requests carry the field names both sides understand. */
(function () {
  const S = (window.S = window.S || {});

  // ---------- tiny helpers ----------
  const store = {
    get(k) { try { return window.localStorage.getItem(k); } catch (e) { return mem[k] || null; } },
    set(k, v) { try { window.localStorage.setItem(k, v); } catch (e) { mem[k] = v; } },
    del(k) { try { window.localStorage.removeItem(k); } catch (e) { delete mem[k]; } },
  };
  const mem = {};
  S.store = store;

  const cfg = window.SITORA_CONFIG || {};
  function apiBase() {
    const saved = store.get('sitora.api');
    let b = saved !== null ? saved : cfg.API_BASE || '';
    b = String(b || '').trim().replace(/\/+$/, '');
    return b;
  }
  S.apiBase = apiBase;
  S.isDemo = () => !apiBase();
  S.setApiBase = (v) => { store.set('sitora.api', String(v || '').trim()); };
  const real = () => !S.isDemo();

  class ApiError extends Error {
    constructor(status, message, errors, title) { super(message); this.status = status; this.errors = errors || null; this.title = title || ''; }
  }
  S.ApiError = ApiError;

  // ---------- auth state ----------
  let auth = null;
  try { auth = JSON.parse(store.get('sitora.auth') || 'null'); } catch (e) { auth = null; }
  const saveAuth = () => { if (auth) store.set('sitora.auth', JSON.stringify(auth)); else store.del('sitora.auth'); };

  const pick = (o, keys, d) => { if (!o) return d; for (const k of keys) { if (o[k] !== undefined && o[k] !== null) return o[k]; } return d; };
  const arr = (r) => (Array.isArray(r) ? r : (r && (r.items || r.data || r.results || r.value || r.rows)) || []);
  const num = (v, d) => { const n = Number(v); return Number.isFinite(n) ? n : d === undefined ? 0 : d; };
  const day = (v) => String(v || '').slice(0, 10);
  S.pick = pick; S.arr = arr;

  const STATUS = ['free', 'booked', 'sold', 'onrequest'];
  function normStatus(s) { if (typeof s === 'number') return STATUS[s] || 'free'; s = String(s || 'free').toLowerCase().replace(/[^a-z]/g, ''); return STATUS.includes(s) ? s : s === 'available' ? 'free' : s === 'reserved' ? 'booked' : 'free'; }
  const STAGE_NAMES = ['Lead', 'Showing', 'Booking', 'Contract', 'Payment', 'Completed', 'Cancelled'];
  function normStage(s) { if (typeof s === 'number') return STAGE_NAMES[s] || 'Lead'; const f = STAGE_NAMES.find((x) => x.toLowerCase() === String(s || '').toLowerCase()); return f || String(s || 'Lead'); }
  // Main role picks the interface: PlatformOwner > Owner > ObjectAdmin > Manager > Accountant > Content > Customer.
  const ROLE_ORDER = ['PlatformOwner', 'Owner', 'ObjectAdmin', 'Manager', 'Accountant', 'Content', 'Customer'];
  function primaryRole(role, roles) { if (typeof role === 'string' && ROLE_ORDER.includes(role)) return role; return ROLE_ORDER.find((r) => roles.includes(r)) || roles[0] || ''; }
  function roleList(u) {
    const r = pick(u, ['roles', 'role', 'userRoles'], []);
    return (Array.isArray(r) ? r : [r]).filter(Boolean).map((x) => (typeof x === 'string' ? x : x.name || x.role || ''));
  }
  // Views know three lead states (New, InProgress, Closed); the API closes a lead as Converted (became a customer) or Rejected.
  const leadStatus = (s) => (s === 'Converted' || s === 'Rejected' ? 'Closed' : String(s || 'New'));
  const parseJson = (v) => { if (typeof v !== 'string') return v; try { return JSON.parse(v); } catch (e) { return v; } };

  const M = (S.map = {
    building(b) {
      return { id: b.id, name: pick(b, ['name', 'title'], '') || pick(b, ['nameRu'], ''), nameRu: pick(b, ['nameRu'], ''), nameTg: pick(b, ['nameTg'], ''), description: pick(b, ['description'], '') || pick(b, ['descriptionRu'], ''), descriptionRu: pick(b, ['descriptionRu'], ''), descriptionTg: pick(b, ['descriptionTg'], ''), address: pick(b, ['address'], ''), deliveryDate: pick(b, ['deliveryDate', 'completionDate', 'deadline'], ''), floorsCount: num(pick(b, ['floorsCount', 'floors', 'floorCount'], 0)), modelUrl: pick(b, ['modelUrl'], null), total: num(pick(b, ['totalApartments', 'apartmentsCount', 'total'], 0)), free: num(pick(b, ['freeApartments', 'freeCount', 'free'], 0)) };
    },
    floor(f) { return { id: f.id, number: num(pick(f, ['number', 'floorNumber', 'level'], 0)), commercial: !!pick(f, ['isCommercial', 'commercial'], false), total: num(pick(f, ['totalApartments', 'apartmentsCount', 'total'], 0)), free: num(pick(f, ['freeApartments', 'freeCount', 'free'], 0)) }; },
    apt(a, floorNo) {
      const area = num(pick(a, ['area', 'totalArea', 'squareMeters'], 0)), pps = num(pick(a, ['pricePerM2', 'pricePerSqm', 'pricePerSquareMeter'], 0));
      return { id: a.id, floorId: pick(a, ['floorId'], null), buildingId: pick(a, ['buildingId'], null), floor: floorNo !== undefined ? floorNo : num(pick(a, ['floorNumber', 'floor'], 0)), number: pick(a, ['number', 'apartmentNumber'], ''), rooms: num(pick(a, ['rooms', 'roomsCount'], 0)), area, pricePerSqm: pps, price: num(pick(a, ['price', 'totalPrice'], area * pps)), status: normStatus(pick(a, ['status'], 'free')), planSlot: pick(a, ['planSlot'], null) === null ? null : num(a.planSlot), model3dId: pick(a, ['model3dId'], null) };
    },
    lead(l) { return { id: l.id, name: pick(l, ['name', 'fullName', 'customerName'], ''), phone: pick(l, ['phone', 'phoneNumber'], ''), comment: pick(l, ['comment', 'message'], ''), status: leadStatus(pick(l, ['status'], 'New')), apiStatus: String(pick(l, ['status'], 'New')), managerId: pick(l, ['managerId'], null), assignee: pick(l, ['assignee', 'assignedTo', 'managerName', 'responsibleName'], ''), createdAt: pick(l, ['createdAt', 'created'], ''), apartmentId: pick(l, ['apartmentId'], null) }; },
    customer(c) { return { id: c.id, fullName: pick(c, ['fullName', 'name'], ''), phone: pick(c, ['phone'], ''), email: pick(c, ['email'], ''), passport: pick(c, ['passportData', 'passport', 'passportNumber'], ''), createdAt: pick(c, ['createdAt'], ''), dealsCount: pick(c, ['dealsCount'], null), deals: arr(c.deals).map(M.deal) }; },
    deal(d) {
      const apt = d.apartment || {}, cu = d.customer || {};
      const price = num(pick(d, ['price', 'totalPrice', 'amount'], 0)), disc = num(pick(d, ['discount'], 0));
      // The deal itself has no paid/remaining in the real API; they come from the schedule (see api.deal).
      const paid = pick(d, ['paid', 'paidAmount', 'totalPaid'], null);
      const remaining = pick(d, ['remaining', 'remainingAmount', 'balance'], null);
      return { id: d.id, stage: normStage(pick(d, ['stage', 'status'], 'Lead')), stageReason: pick(d, ['stageReason', 'cancelReason'], ''), price, discount: disc, discountReason: pick(d, ['discountReason'], ''), currency: String(pick(d, ['currency', 'contractCurrency'], 'TJS')).toUpperCase(), apartmentId: pick(d, ['apartmentId'], apt.id), apartmentNumber: pick(d, ['apartmentNumber'], apt.number), floor: pick(d, ['floor', 'floorNumber'], apt.floorNumber !== undefined ? apt.floorNumber : apt.floor), rooms: pick(d, ['rooms'], apt.rooms), area: pick(d, ['area'], apt.area), buildingId: pick(d, ['buildingId'], apt.buildingId || null), buildingName: pick(d, ['buildingName'], ''), customerId: pick(d, ['customerId'], cu.id), customerName: pick(d, ['customerName'], cu.fullName || cu.name || ''), customerPhone: pick(d, ['customerPhone'], cu.phone || ''), managerName: pick(d, ['managerName', 'manager'], ''), createdAt: pick(d, ['createdAt'], ''), paid: paid === null ? null : num(paid), remaining: remaining === null ? (paid === null ? null : price - disc - num(paid)) : num(remaining), hasPlan: pick(d, ['hasPlan'], true) };
    },
    schedule(s) {
      let monthly = 0;
      const items = arr(pick(s, ['items', 'schedule', 'rows'], [])).map((i, k) => {
        const kindRaw = String(pick(i, ['kind', 'type'], k === 0 ? 'Down' : 'Monthly'));
        const down = /^down/i.test(kindRaw);
        if (!down) monthly++;
        const raw = String(pick(i, ['status'], 'Pending'));
        const status = raw === 'PartiallyPaid' ? 'Partial' : raw;
        return { id: pick(i, ['id'], k), number: num(pick(i, ['number', 'order', 'index'], down ? 0 : monthly)), kind: down ? 'Down' : 'Monthly', dueDate: day(pick(i, ['dueDate', 'date'], '')), amountDue: num(pick(i, ['amountDue', 'amount', 'due'], 0)), amountPaid: num(pick(i, ['amountPaid', 'paid'], 0)), status: i.isOverdue && status !== 'Paid' ? 'Overdue' : status, overdue: !!i.isOverdue, eqDue: pick(i, ['amountDueEquivalent'], null), eqPaid: pick(i, ['amountPaidEquivalent'], null) };
      });
      const eq = s.equivalent || null;
      const total = num(pick(s, ['total', 'totalDue', 'price'], items.reduce((t, i) => t + i.amountDue, 0))), paid = num(pick(s, ['paid', 'totalPaid'], items.reduce((t, i) => t + i.amountPaid, 0)));
      return { dealId: pick(s, ['dealId'], null), currency: String(pick(s, ['currency'], 'TJS')), total, paid, remaining: num(pick(s, ['remaining', 'balance'], total - paid)), overdue: num(pick(s, ['overdue'], 0)), down: pick(s, ['downPaymentPct', 'downPaymentPercent', 'downPayment'], null), months: pick(s, ['months'], monthly), day: pick(s, ['paymentDay', 'day'], null), startDate: day(pick(s, ['startDate'], '')), items, payments: arr(s.payments).map(M.payment), eq: eq ? { currency: eq.currency, rate: eq.rate, date: eq.rateDate, estimate: eq.isEstimate !== false, note: eq.note || '', total: pick(eq, ['total', 'totalDue'], null), paid: pick(eq, ['paid', 'totalPaid'], null), remaining: pick(eq, ['remaining'], null) } : null };
    },
    payment(p) {
      const reversal = !!(pick(p, ['isReversal', 'reversal'], false) || p.reversalOfId);
      // Reversals may come with a minus sign; views add "−" themselves.
      const abs = (v) => (reversal ? Math.abs(num(v)) : num(v));
      return { id: pick(p, ['id', 'paymentId'], null), dealId: p.dealId, date: day(pick(p, ['paidAt', 'date', 'createdAt'], '')), amount: abs(pick(p, ['amount'], 0)), currency: String(pick(p, ['currency'], 'TJS')), amountDeal: abs(pick(p, ['amountInDealCurrency', 'amountDeal', 'amountInContractCurrency'], pick(p, ['amount'], 0))), amountTjs: abs(pick(p, ['amountTjs', 'amountInTjs'], 0)), rate: pick(p, ['rateUsed', 'rate'], null), method: pick(p, ['method'], ''), reversal, reversedId: pick(p, ['reversalOfId', 'reversedId', 'reversedPaymentId'], null), isReversed: !!p.isReversed, comment: pick(p, ['comment', 'note'], ''), createdBy: pick(p, ['createdBy', 'author'], ''), customerName: pick(p, ['customerName'], ''), apartmentNumber: pick(p, ['apartmentNumber'], '') };
    },
    message(m) {
      const fromCustomer = pick(m, ['fromCustomer'], null);
      const me = auth && auth.user ? String(auth.user.id) : null;
      return { id: m.id, text: pick(m, ['text', 'body', 'message'], ''), sender: fromCustomer === null ? String(pick(m, ['sender', 'senderRole', 'from'], '')).toLowerCase() : fromCustomer ? 'customer' : 'staff', senderName: pick(m, ['senderName', 'authorName', 'author'], ''), createdAt: pick(m, ['createdAt', 'sentAt'], ''), mine: m.senderId && me ? String(m.senderId) === me : pick(m, ['mine', 'isMine'], undefined), read: !!(m.readAt || pick(m, ['read', 'isRead'], false)) };
    },
    doc(d) { return { id: d.id, name: pick(d, ['fileName', 'name', 'title'], ''), type: pick(d, ['type', 'contentType'], ''), contentType: pick(d, ['contentType'], ''), size: num(pick(d, ['sizeBytes', 'size'], 0)), signedAt: pick(d, ['signedAt'], ''), createdAt: pick(d, ['createdAt', 'uploadedAt'], ''), uploadedBy: pick(d, ['uploadedBy', 'author'], '') }; },
    stage(s) { return { id: s.id, title: pick(s, ['name', 'title'], ''), percent: num(pick(s, ['percent', 'progress'], 0)), order: num(pick(s, ['sortOrder', 'order'], 0)), description: pick(s, ['description'], ''), date: day(pick(s, ['updatedAt', 'date'], '')), photos: arr(s.photos).map((p) => ({ id: p.id, url: p.url, caption: p.caption || '' })) }; },
    user(u) {
      const active = pick(u, ['isActive'], null);
      const status = active === null ? String(pick(u, ['status'], 'Active')) : active ? 'Active' : 'Blocked';
      const roles = roleList(u);
      return { id: u.id, email: pick(u, ['email'], ''), phone: pick(u, ['phone', 'phoneNumber'], ''), name: pick(u, ['fullName', 'name', 'userName'], ''), roles, role: primaryRole(u.role, roles), organizationId: pick(u, ['organizationId'], null), organizationName: pick(u, ['organizationName'], ''), buildingIds: arr(pick(u, ['buildingIds'], [])), impersonation: u.impersonation || null, status, blocked: active === false || /block|lock/i.test(status) || !!u.isBlocked };
    },
    invitation(i) { return { id: i.id, email: pick(i, ['email'], ''), name: pick(i, ['fullName', 'name'], ''), role: pick(i, ['role'], ''), buildingIds: arr(pick(i, ['buildingIds'], [])), expiresAt: pick(i, ['expiresAt'], ''), createdAt: pick(i, ['createdAt'], '') }; },
    organization(o) { return { id: o.id, name: pick(o, ['name'], ''), slug: pick(o, ['slug'], ''), active: pick(o, ['isActive'], true) !== false, buildings: num(pick(o, ['buildingsCount'], 0)), users: num(pick(o, ['usersCount'], 0)), createdAt: pick(o, ['createdAt'], '') }; },
    booking(b) { return { id: b.id, apartmentId: b.apartmentId, apartmentNumber: pick(b, ['apartmentNumber'], ''), name: pick(b, ['name', 'customerName', 'fullName'], ''), phone: pick(b, ['phone', 'customerPhone'], ''), status: String(pick(b, ['status'], 'Active')), createdAt: pick(b, ['createdAt'], ''), expiresAt: pick(b, ['expiresAt'], '') }; },
    audit(a) { return { id: a.id, at: pick(a, ['at', 'createdAt', 'timestamp'], ''), user: pick(a, ['userEmail', 'user', 'userName', 'actor'], '') || (S.t ? S.t('audit.system', null, 'система') : 'система'), action: pick(a, ['action'], ''), entity: pick(a, ['entity', 'entityType'], ''), entityId: pick(a, ['entityId'], ''), before: parseJson(pick(a, ['before', 'oldValues'], null)), after: parseJson(pick(a, ['after', 'newValues'], null)), viaPlatform: !!a.impersonatedById }; },
  });

  // ---------- transport ----------
  let refreshing = null;
  function langHeader() { return S.lang || 'ru'; }
  function qs(q) { const p = Object.keys(q || {}).filter((k) => q[k] !== undefined && q[k] !== null && q[k] !== ''); return p.length ? '?' + p.map((k) => encodeURIComponent(k) + '=' + encodeURIComponent(q[k])).join('&') : ''; }
  function origin() { try { return new URL(apiBase(), location.href).origin; } catch (e) { return ''; } }
  function fileHref(u) {
    if (!u) return u; if (/^(https?:|data:|blob:)/.test(u)) return u;
    // Signed links come relative to the server root: /api/v1/files?token=...
    if (u.startsWith('/api/') || u.startsWith('/hubs/')) return origin() + u;
    return apiBase() + (u.startsWith('/') ? '' : '/') + u;
  }
  S.fileHref = fileHref;

  async function doFetch(method, path, o) {
    const base = apiBase(); const headers = { 'Accept-Language': langHeader() };
    if (o.auth !== false && auth && auth.accessToken) headers.Authorization = 'Bearer ' + auth.accessToken;
    let body;
    if (o.form) body = o.form; else if (o.body !== undefined) { headers['Content-Type'] = 'application/json'; body = JSON.stringify(o.body); }
    const url = (o.absolute ? origin() : base) + path + qs(o.query);
    try { return await fetch(url, { method, headers, body }); } catch (e) { throw new ApiError(0, S.t ? S.t('err.network') : 'Нет связи с сервером'); }
  }

  // Form field names that differ between the views and the API (validation errors come back with API names).
  const FIELD_ALIASES = { downpaymentpct: 'downPaymentPercent', paidat: 'date', passportdata: 'passport', ratetotjs: 'rate', priceperm2: 'pricePerSqm', sortorder: 'order', isactive: 'blocked' };
  function renameErrors(errs, extra) {
    if (!errs) return null; const out = {};
    Object.keys(errs).forEach((k) => { const low = k.toLowerCase().replace(/^\$\./, ''); const to = (extra && extra[low]) || FIELD_ALIASES[low] || k; out[to] = [].concat(out[to] || [], errs[k]); });
    return out;
  }

  async function readError(res) {
    let data = null; try { data = await res.json(); } catch (e) { /* no body */ }
    let errs = null, msg = (data && (data.detail || data.message)) || '';
    if (data && Array.isArray(data.errors)) {
      // 422 from the Excel import: [{ row, message }]
      const lines = data.errors.map((x) => (x && x.row ? (S.t ? S.t('err.row', { n: x.row }, 'Строка ' + x.row) : 'Строка ' + x.row) + ': ' + x.message : x && x.message ? x.message : String(x)));
      msg = lines.join('\n') || msg;
    } else if (data && data.errors && typeof data.errors === 'object') {
      errs = renameErrors(data.errors);
      if (!msg) msg = Object.values(data.errors).flat().join(' ');
    }
    if (res.status === 429) { const ra = res.headers.get('Retry-After'); msg = msg || (S.t ? S.t('err.429') : 'Слишком много запросов') + (ra ? ' (' + ra + ' c)' : ''); }
    // Titles of generic ProblemDetails are English ("Unauthorized", "One or more validation errors occurred.") — not shown to people.
    if (!msg) msg = S.t ? S.t('err.' + (res.status >= 500 ? '500' : res.status), {}, S.t('err.unknown')) : 'Ошибка ' + res.status;
    return new ApiError(res.status, msg, errs, data && data.title);
  }

  async function demoCall(method, path, o) {
    const hdr = auth && auth.accessToken ? auth.accessToken : null;
    try {
      return await S.mock.handle(method, path, { query: o.query, body: o.body, token: hdr, lang: langHeader(), file: o.file });
    } catch (e) {
      if (e.status) throw new ApiError(e.status, e.detail || e.message, e.errors, e.title);
      throw e;
    }
  }

  async function refresh() {
    if (!auth || !auth.refreshToken) return false;
    if (refreshing) return refreshing;
    refreshing = (async () => {
      try {
        const data = S.isDemo() ? await demoCall('POST', '/auth/refresh', { body: { refreshToken: auth.refreshToken } }) : await (async () => { const r = await doFetch('POST', '/auth/refresh', { auth: false, body: { refreshToken: auth.refreshToken } }); if (!r.ok) throw await readError(r); return r.json(); })();
        setAuth(data, auth.user); return true;
      } catch (e) { clearAuth(); return false; } finally { refreshing = null; }
    })();
    return refreshing;
  }

  async function request(method, path, o) {
    o = o || {};
    let tried = false;
    for (;;) {
      if (S.isDemo()) {
        try { return await demoCall(method, path, o); } catch (e) { if (e.status === 401 && o.auth !== false && !tried && auth && (await refresh())) { tried = true; continue; } throw e; }
      }
      const res = await doFetch(method, path, o);
      // The "as owner" token has no refresh token: when it expires, the platform owner's own session comes back.
      if (res.status === 401 && o.auth !== false && !tried && auth && auth.platform) { endImpersonation(); tried = true; S.toast && S.toast(S.t('imp.expired'), 'warn'); continue; }
      if (res.status === 401 && o.auth !== false && !tried && auth && (await refresh())) { tried = true; continue; }
      if (!res.ok) {
        const er = await readError(res);
        if (res.status === 401 && o.auth !== false && auth) { clearAuth(); er.message = S.t ? S.t('err.401') : er.message; }
        throw er;
      }
      if (res.headers.get('X-Impersonated-By') && auth && auth.user && !auth.user.impersonation) { auth.user.impersonation = { impersonatedBy: res.headers.get('X-Impersonated-By') }; saveAuth(); S.bus.emit('auth', auth.user); }
      if (o.raw) return res;
      if (o.blob) return res.blob();
      if (res.status === 204) return {};
      const ct = res.headers.get('content-type') || '';
      return ct.includes('json') ? res.json() : {};
    }
  }

  // Lists in the real API are paged (max 100 per page); the demo returns plain arrays.
  async function all(path, query, maxPages) {
    if (S.isDemo()) return arr(await request('GET', path, { query }));
    const out = [];
    for (let page = 1; page <= (maxPages || 20); page++) {
      const r = await request('GET', path, { query: Object.assign({}, query, { page, pageSize: 100 }) });
      const items = arr(r); out.push(...items);
      if (Array.isArray(r) || !items.length || out.length >= num(r.totalCount, 0)) break;
    }
    return out;
  }

  // Re-throws an API error with view field names (for requests whose body names differ from the form).
  const withFields = (promise, extra) => promise.catch((e) => { if (e && e.errors) e.errors = renameErrors(e.errors, extra); throw e; });

  function setAuth(data, prevUser) {
    const user = data.user ? M.user(data.user) : prevUser || null;
    auth = { accessToken: pick(data, ['accessToken', 'token', 'access_token'], null), refreshToken: pick(data, ['refreshToken', 'refresh_token'], null), user };
    saveAuth(); S.bus.emit('auth', auth.user);
  }
  function clearAuth() { auth = null; saveAuth(); S.bus.emit('auth', null); }
  // Back to the platform owner's own session (kept aside while working "as owner").
  function endImpersonation(accessToken) {
    if (!auth || !auth.platform) return;
    const p = auth.platform;
    auth = { accessToken: accessToken || p.accessToken, refreshToken: p.refreshToken, user: p.user };
    saveAuth(); S.bus.emit('auth', auth.user);
  }
  // Login result: tokens, or a second-factor request (only the platform owner gets one).
  async function signedIn(d) {
    if (d && d.challengeToken) return { challenge: { token: d.challengeToken, methods: arr(d.methods), message: d.message || '' } };
    setAuth(d);
    if (!auth.user || !auth.user.roles.length) await api.loadMe();
    return { user: auth.user };
  }

  // Apartment details for a deal (floor, rooms, area, building) — the real deal only carries the apartment id.
  const aptCache = new Map();
  async function withApartment(d) {
    if (!real() || !d.apartmentId || (d.floor !== undefined && d.floor !== null && d.buildingId)) return d;
    try {
      if (!aptCache.has(d.apartmentId)) aptCache.set(d.apartmentId, request('GET', '/apartments/' + d.apartmentId).then((a) => M.apt(a)));
      const a = await aptCache.get(d.apartmentId);
      return Object.assign(d, { floor: a.floor, rooms: a.rooms, area: a.area, buildingId: a.buildingId });
    } catch (e) { aptCache.delete(d.apartmentId); return d; }
  }
  async function withSchedule(d) {
    if (!real()) return d;
    try { const s = await api.schedule(d.id); return Object.assign(d, { paid: s.paid, remaining: s.remaining, hasPlan: true }); } catch (e) { return Object.assign(d, { hasPlan: false, paid: d.paid === null ? 0 : d.paid, remaining: d.remaining === null ? d.price - d.discount : d.remaining }); }
  }

  // Positions inside an organization; leaders (Owner, ObjectAdmin) can do everything the positions can.
  const STAFF_ROLES = ['Owner', 'ObjectAdmin', 'Manager', 'Accountant', 'Content'];
  const LEADERS = ['Owner', 'ObjectAdmin'];
  const role = () => (auth && auth.user ? auth.user.role : '');
  const cfgSlug = () => String(cfg.ORG_SLUG || '').trim();
  const api = (S.api = {
    request,
    get user() { return auth && auth.user; },
    get isAuthed() { return !!(auth && auth.accessToken); },
    isStaff() { return !!(auth && auth.user && auth.user.roles.some((r) => STAFF_ROLES.includes(r))); },
    isCustomer() { return role() === 'Customer'; },
    // The platform owner's own section (not while working "as owner" of an organization).
    isPlatform() { return role() === 'PlatformOwner'; },
    isLeader() { return LEADERS.includes(role()); },
    isOwner() { return role() === 'Owner'; },
    get impersonating() { return !!(auth && (auth.platform || (auth.user && auth.user.impersonation))); },
    has(...roles) { return !!(auth && auth.user && (api.isLeader() || auth.user.roles.some((r) => roles.includes(r)))); },
    // Everyone signs in with a code from e-mail; the platform owner then confirms with a password or an authenticator code.
    emailSend(email) { return request('POST', '/auth/email/send', { auth: false, body: { email } }); },
    async emailVerify(email, code) { return signedIn(await request('POST', '/auth/email/verify', { auth: false, body: { email, code } })); },
    async secondFactor(token, password, totpCode) { return signedIn(await request('POST', '/auth/second-factor', { auth: false, body: { challengeToken: token, password: password || null, totpCode: totpCode || null } })); },
    async acceptInvitation(token) { return signedIn(await request('POST', '/auth/invitations/accept', { auth: false, body: { token } })); },
    async loadMe() { const u = M.user(await request('GET', '/auth/me')); auth.user = u; saveAuth(); S.bus.emit('auth', u); return u; },
    async logout() {
      if (auth && auth.platform) auth = Object.assign({}, auth.platform);
      try { if (auth && auth.refreshToken) await request('POST', '/auth/logout', { body: { refreshToken: auth.refreshToken } }); } catch (e) { /* ignore */ }
      clearAuth();
    },

    // platform owner
    async organizations() { return arr(await request('GET', '/platform/organizations')).map(M.organization); },
    createOrganization(b) { return withFields(request('POST', '/platform/organizations', { body: { name: b.name, slug: b.slug, ownerEmail: b.ownerEmail, ownerFullName: b.ownerFullName } })).then(M.organization); },
    async impersonate(organizationId) {
      const d = await request('POST', '/platform/impersonate/' + organizationId);
      const platform = auth.platform || { accessToken: auth.accessToken, refreshToken: auth.refreshToken, user: auth.user };
      auth = { accessToken: d.accessToken, refreshToken: null, user: M.user(d.user), platform };
      saveAuth(); S.bus.emit('auth', auth.user); return auth.user;
    },
    async stopImpersonation() {
      let token = null;
      try { token = (await request('POST', '/platform/impersonate/stop')).accessToken; } catch (e) { /* expired: the saved session is enough */ }
      endImpersonation(token); return auth && auth.user;
    },
    totpSetup() { return request('GET', '/platform/totp'); },
    totpEnable(code) { return request('POST', '/platform/totp/enable', { body: { code } }); },
    clearAuth,
    async restore() { if (!auth || !auth.accessToken) return null; try { return await api.loadMe(); } catch (e) { if (auth && e.status !== 0) clearAuth(); return null; } },

    // catalog
    // The public site shows one developer's buildings (ORG_SLUG in config.js); a staff token is filtered by the server itself.
    async buildings() { return (await all('/buildings', real() && !api.isStaff() && cfgSlug() ? { organization: cfgSlug() } : undefined)).map(M.building); },
    async building(id) { return M.building(await request('GET', '/buildings/' + id)); },
    async floors(id) { return (await all('/buildings/' + id + '/floors')).map(M.floor).sort((a, b) => a.number - b.number); },
    async floorApartments(f) { return (await all('/floors/' + f.id + '/apartments')).map((a) => M.apt(a, f.number)); },
    async allApartments(floors) { const parts = await Promise.all(floors.filter((f) => !f.commercial || f.total > 0).map((f) => api.floorApartments(f))); return parts.flat(); },
    async apartment(id) { return M.apt(await request('GET', '/apartments/' + id)); },
    async saveBuilding(id, b) {
      if (S.isDemo()) return request('PUT', '/buildings/' + id, { body: b });
      // PUT replaces the whole building: fields the form does not show are taken from the current version.
      const cur = await request('GET', '/buildings/' + id);
      const body = { nameRu: pick(b, ['nameRu'], cur.nameRu), nameTg: pick(b, ['nameTg'], cur.nameTg), address: pick(b, ['address'], cur.address), floorsCount: num(pick(b, ['floorsCount'], cur.floorsCount)), descriptionRu: pick(b, ['descriptionRu'], cur.descriptionRu) || null, descriptionTg: pick(b, ['descriptionTg'], cur.descriptionTg) || null, modelUrl: pick(b, ['modelUrl'], cur.modelUrl) || null };
      return request('PUT', '/buildings/' + id, { body }).then(M.building);
    },
    createBuilding(b) { return withFields(request('POST', '/buildings', { body: { nameRu: b.nameRu, nameTg: b.nameTg, address: b.address, floorsCount: b.floorsCount, descriptionRu: null, modelUrl: null } })).then(M.building); },
    createApartment(b) { return withFields(request('POST', '/apartments', { body: Object.assign({}, b, { pricePerM2: pick(b, ['pricePerM2', 'pricePerSqm'], 0) }) })).then((a) => M.apt(a)); },
    updateApartment(id, b) { return withFields(request('PUT', '/apartments/' + id, { body: Object.assign({}, b, { pricePerM2: pick(b, ['pricePerM2', 'pricePerSqm'], 0) }) })).then((a) => M.apt(a)); },
    onRequest(id, on) { return request('PATCH', '/apartments/' + id + '/on-request', { body: { onRequest: on } }); },
    async importApartments(buildingId, file) {
      const f = new FormData(); f.append('file', file);
      const r = await request('POST', '/apartments/import', { query: { buildingId }, form: f, file });
      if (r && r.message) return r;
      return Object.assign({}, r, { message: S.t ? S.t('cat.importedN', { c: num(r && r.created), u: num(r && r.updated) }, 'Создано: ' + num(r && r.created) + ', обновлено: ' + num(r && r.updated)) : '' });
    },

    // leads & bookings
    // The building tells the server which developer the lead is for.
    createLead(b) { return request('POST', '/leads', { auth: false, body: { name: b.name, phone: b.phone, comment: b.comment || null, apartmentId: b.apartmentId || null, buildingId: b.buildingId || S.siteBuildingId || null, source: 'Website' } }); },
    book(aptId, b) { return request('POST', '/apartments/' + aptId + '/bookings', { body: { name: b.name || null, phone: b.phone || null, customerId: b.customerId || null, comment: b.comment || null } }); },
    async leads() { return (await all('/leads')).map(M.lead); },
    updateLead(id, b) {
      if (S.isDemo()) return request('PATCH', '/leads/' + id, { body: b });
      // 'Closed' in the views is a rejection: a lead becomes Converted only by creating a customer from it.
      const status = b.status === 'Closed' ? 'Rejected' : b.status || null;
      const managerId = b.managerId || (b.assignee !== undefined && auth && auth.user ? auth.user.id : null);
      return request('PATCH', '/leads/' + id, { body: { status, comment: b.comment, managerId } }).then(M.lead);
    },
    extendBooking(id) { return request('POST', '/bookings/' + id + '/extend'); },
    cancelBooking(id, reason) { return request('POST', '/bookings/' + id + '/cancel', { body: { reason } }); },

    // customers & deals
    async customers(search) { return (await all('/customers', { search: search || undefined })).map(M.customer); },
    async customer(id) {
      const c = M.customer(await request('GET', '/customers/' + id));
      if (real() && !c.deals.length) c.deals = (await all('/deals', { customerId: id })).map(M.deal);
      return c;
    },
    createCustomer(b) { return withFields(request('POST', '/customers', { body: { fullName: b.fullName, phone: b.phone, email: b.email || null, passportData: b.passport || b.passportData || null, passport: b.passport, leadId: b.leadId || null, buildingId: b.buildingId || null } })).then(M.customer); },
    async deals(stage) { return (await all('/deals', { stage: stage && stage !== 'all' ? stage : undefined })).map(M.deal); },
    async myDeals() {
      const list = arr(await request('GET', '/me/deals')).map(M.deal);
      // Active deals first: a cancelled one should not open the cabinet.
      list.sort((a, b) => (a.stage === 'Cancelled') - (b.stage === 'Cancelled'));
      return Promise.all(list.map((d) => withApartment(d).then(withSchedule)));
    },
    /* hint — what the caller already knows about the deal (a report row). The deal card is for managers;
       an accountant gets 403 and sees the deal assembled from the schedule and the hint. */
    async deal(id, hint) {
      try {
        const d = M.deal(await request('GET', '/deals/' + id));
        if (!real()) return d;
        // Customer cards (phone, passport) are for sales only: an accountant would get 403 here.
        const canSeeCustomer = d.customerId && api.has('Manager');
        await Promise.all([withApartment(d), withSchedule(d), canSeeCustomer ? request('GET', '/customers/' + d.customerId).then((c) => { d.customerPhone = c.phone || ''; }).catch(() => {}) : null]);
        return d;
      } catch (e) {
        if (!real() || e.status !== 403) throw e;
        const s = await api.schedule(id);
        return Object.assign(M.deal({ id, stage: 'Payment', price: s.total, currency: s.currency, apartmentNumber: hint && hint.apartmentNumber, customerName: hint && hint.customerName, customerPhone: hint && hint.phone }), { paid: s.paid, remaining: s.remaining, readOnly: true });
      }
    },
    createDeal(b) { return withFields(request('POST', '/deals', { body: { customerId: b.customerId || null, apartmentId: b.apartmentId || null, currency: b.currency || 'TJS', discount: b.discount || null, discountReason: b.discountReason || null, stage: b.stage || null } })).then(M.deal); },
    setStage(id, stage, reason) { return request('PATCH', '/deals/' + id + '/stage', { body: { stage, reason: reason || null } }); },
    async summary() {
      const r = await request('GET', '/crm/summary');
      if (S.isDemo()) return r;
      // Sales are kept per currency; the dashboard shows one figure in somoni (USD at the current rate).
      const sold = arr(r.soldAmount);
      const rate = sold.some((m) => m.currency === 'USD') ? await api.rate().then((x) => x.rate).catch(() => 0) : 0;
      const salesTotalTjs = Math.round(sold.reduce((s, m) => s + (m.currency === 'USD' ? num(m.amount) * rate : num(m.amount)), 0));
      return Object.assign({}, r, { openLeads: num(r.newLeads) + num(r.leadsInProgress), expiringBookings: r.bookingsExpiringIn24Hours, salesTotalTjs, salesNote: sold.some((m) => m.currency === 'USD') && !rate ? 'usdWithoutRate' : null });
    },
    async pipeline() {
      const r = await request('GET', '/crm/pipeline');
      return arr(r.columns || r.stages || r).map((c) => ({
        stage: normStage(pick(c, ['stage', 'name'], '')),
        count: pick(c, ['count'], null),
        items: arr(c.items || c.cards || c.deals).map((it) => {
          const kind = String(it.kind || '').toLowerCase();
          const client = pick(it, ['clientName', 'customerName', 'name'], '');
          const aptNo = pick(it, ['apartmentNumber'], null);
          return Object.assign({}, it, {
            kind: kind || undefined,
            title: it.title || (aptNo && kind !== 'lead' ? '№ ' + aptNo + ' · ' + client : client),
            subtitle: it.subtitle || it.phone || '',
            price: pick(it, ['price', 'amount'], null),
            dealId: kind === 'deal' ? it.id : it.dealId,
          });
        }),
      }));
    },

    // schedule & money
    createPlan(id, b) { return withFields(request('POST', '/deals/' + id + '/plan', { body: { downPaymentPct: pick(b, ['downPaymentPct', 'downPaymentPercent'], 0), downPaymentPercent: pick(b, ['downPaymentPct', 'downPaymentPercent'], 0), months: b.months, paymentDay: b.paymentDay, startDate: b.startDate || S.today() } })).then(M.schedule); },
    async schedule(id) { return M.schedule(await request('GET', '/deals/' + id + '/schedule')); },
    async payments(id) {
      if (real()) return (await api.schedule(id)).payments;
      const r = await api.reportPayments(S.addDays(-365), S.today()); return r.rows.filter((p) => String(p.dealId) === String(id));
    },
    addPayment(id, b) { return withFields(request('POST', '/deals/' + id + '/payments', { body: { amount: b.amount, currency: b.currency, paidAt: b.date || b.paidAt, date: b.date || b.paidAt, method: b.method || 'Cash', comment: b.comment || null } })); },
    reversePayment(id, reason) { return request('POST', '/payments/' + id + '/reverse', { body: { reason } }); },
    // Each developer has its own manual rate: staff get theirs from the token, the public site names its organization.
    async rate() { const r = await request('GET', '/rates/current', { auth: api.isStaff(), query: real() ? { currency: 'USD', organization: api.isStaff() ? undefined : cfgSlug() || undefined } : undefined }); return { rate: num(pick(r, ['rateToTjs', 'rate', 'value', 'usdRate'], 0)), date: day(pick(r, ['date', 'rateDate'], '')), source: pick(r, ['source'], '') }; },
    setRate(rate, date) { return withFields(request('PUT', '/rates', { body: { date, currency: 'USD', rateToTjs: rate, rate } })); },
    async reportPayments(from, to) {
      const r = await request('GET', '/reports/payments', { query: { from, to } });
      const t = pick(r, ['totalsByCurrency', 'totals'], {});
      const totals = Array.isArray(t) ? t.reduce((o, m) => { o[m.currency] = num(m.amount); return o; }, {}) : t;
      return { from: r.from, to: r.to, rows: arr(r.rows || r.payments || r.items).map(M.payment), totalTjs: pick(r, ['totalTjs', 'total'], null), totals };
    },
    async reportDebts(asOf) { const r = await request('GET', '/reports/debts', { query: { asOf } }); return { asOf: r.asOf, rows: arr(r.rows || r.items || r.debts).map((x) => ({ dealId: x.dealId, customerName: pick(x, ['customerName'], ''), phone: pick(x, ['phone', 'customerPhone'], ''), apartmentNumber: pick(x, ['apartmentNumber'], ''), currency: pick(x, ['currency'], 'TJS'), overdue: num(pick(x, ['overdue', 'overdueAmount', 'amount'], 0)), since: day(pick(x, ['oldestOverdueDate', 'since', 'overdueSince'], '')), days: num(pick(x, ['daysOverdue', 'days', 'overdueDays'], 0)), remaining: num(pick(x, ['remaining'], 0)) })) }; },

    // documents, files, chat, construction
    async documents(id) { return arr(await request('GET', '/deals/' + id + '/documents')).map(M.doc); },
    uploadDocument(id, file, type) { const f = new FormData(); f.append('file', file); f.append('type', type || 'Other'); return request('POST', '/deals/' + id + '/documents', { form: f, file }); },
    docLink(id) { return request('GET', '/documents/' + id + '/download'); },
    async messages(id) { return arr(await request('GET', '/deals/' + id + '/messages', { query: real() ? { page: 1, pageSize: 100 } : undefined })).map(M.message); },
    sendMessage(id, text) { return request('POST', '/deals/' + id + '/messages', { body: { text } }); },
    async stages(buildingId) { const r = await request('GET', '/buildings/' + buildingId + '/stages'); return arr(Array.isArray(r) ? r : r.stages).map(M.stage).sort((a, b) => a.order - b.order); },
    async createStage(buildingId, b) {
      const order = b.order !== undefined ? b.order : real() ? (await api.stages(buildingId)).length + 1 : undefined;
      return withFields(request('POST', '/buildings/' + buildingId + '/stages', { body: { name: b.title, title: b.title, percent: b.percent, sortOrder: order, description: b.description } }), { name: 'title' });
    },
    updateStage(id, b) { return withFields(request('PUT', '/stages/' + id, { body: { name: b.title, title: b.title, percent: b.percent, sortOrder: b.order || 1, description: b.description } }), { name: 'title' }); },
    uploadPhoto(stageId, file) { const f = new FormData(); f.append('file', file); return request('POST', '/stages/' + stageId + '/photos', { form: f, file: { name: file.name, size: file.size, dataUrl: file.dataUrl } }); },

    // admin
    async users() { return (await all('/users')).map(M.user).filter((u) => !u.roles.length || u.roles.some((r) => STAFF_ROLES.includes(r))); },
    // Staff are invited by e-mail and sign in with a code; no passwords are set here.
    async invitations() { return arr(await request('GET', '/invitations')).map(M.invitation); },
    invite(b) { return withFields(request('POST', '/invitations', { body: { email: b.email, fullName: b.fullName, role: b.role, buildingIds: b.buildingIds && b.buildingIds.length ? b.buildingIds : null } })).then(M.invitation); },
    revokeInvitation(id) { return request('DELETE', '/invitations/' + id); },
    setUserBuildings(id, buildingIds) { return request('PUT', '/users/' + id + '/buildings', { body: { buildingIds } }).then(M.user); },
    setUserBlocked(id, blocked) { return request('PATCH', '/users/' + id + '/status', { body: { isActive: !blocked, blocked } }); },
    async audit(entity) { return (await all('/audit', { entity: entity || undefined }, 1)).map(M.audit); },
    async health() { return S.isDemo() ? request('GET', '/health/ready', { auth: false }) : request('GET', '/health/ready', { auth: false, absolute: true }); },

    /* Opens a PDF in a tab or downloads Excel: real API -> file from the server; demo -> in-page preview */
    async openDoc(path, query, title) {
      if (S.isDemo()) { const r = await request('GET', path, { query }); S.bus.emit('demoDoc', r.demoDoc || { title: title || 'Документ', lines: [] }); return; }
      // The tab is opened right away: a window.open after an await is blocked as a pop-up.
      const isPdf = /\.pdf$/i.test(path); const tab = isPdf ? window.open('', '_blank') : null;
      try {
        const res = await request('GET', path, { query, raw: true });
        const blob = await res.blob(); const url = URL.createObjectURL(blob);
        if (tab) tab.location.href = url;
        else {
          const cd = res.headers.get('Content-Disposition') || ''; const m = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(cd);
          const a = document.createElement('a'); a.href = url; a.download = m ? decodeURIComponent(m[1] || m[2]) : (title || 'file') + (/\.xlsx$/i.test(path) ? '.xlsx' : '');
          document.body.appendChild(a); a.click(); a.remove();
        }
        setTimeout(() => URL.revokeObjectURL(url), 60000);
      } catch (e) { if (tab) tab.close(); throw e; }
    },
    async openSigned(id, title) {
      if (S.isDemo()) { const r = await api.docLink(id); S.bus.emit('demoDoc', r.demoDoc || { title: title || 'Документ', lines: [] }); return; }
      const tab = window.open('', '_blank');
      try { const r = await api.docLink(id); const href = fileHref(r.url || r.link || r.downloadUrl); if (tab) tab.location.href = href; else location.href = href; } catch (e) { if (tab) tab.close(); throw e; }
    },
  });
  S.fileSrc = (u) => (S.isDemo() ? u : fileHref(u));
  S.hubUrl = () => origin() + '/hubs/chat';
  S.accessToken = () => (auth && auth.accessToken) || '';
})();
