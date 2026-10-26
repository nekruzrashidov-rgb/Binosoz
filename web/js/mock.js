/* Demo backend: simulates the Sitora REST API (see RESULT.md) in the browser.
   Used when no API address is configured, so the whole site can be tried without a server. */
(function () {
  const S = (window.S = window.S || {});

  const DAY = 86400000;
  const iso = (d) => new Date(d).toISOString();
  const dateOnly = (d) => new Date(d).toISOString().slice(0, 10);
  function rng(seed) { let s = seed >>> 0; return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296); }

  const STAGES = ['Lead', 'Showing', 'Booking', 'Contract', 'Payment', 'Completed', 'Cancelled'];
  const STAFF = [
    { id: 'u1', email: 'admin@sitora.tj', password: 'Admin123!', name: 'Фирдавс Назаров', roles: ['Admin'] },
    { id: 'u2', email: 'manager@sitora.tj', password: 'Manager123!', name: 'Мадина Исмоилова', roles: ['Manager'] },
    { id: 'u3', email: 'accountant@sitora.tj', password: 'Account123!', name: 'Зафар Саидов', roles: ['Accountant'] },
    { id: 'u4', email: 'content@sitora.tj', password: 'Content123!', name: 'Шахноза Рахимова', roles: ['Content'] },
  ];

  function makeDb() {
    const r = rng(11);
    const db = { seq: 1000, audit: [], rate: [{ date: dateOnly(Date.now() - 3 * DAY), value: 10.92 }, { date: dateOnly(Date.now()), value: 10.95 }] };
    db.users = STAFF.map((u) => Object.assign({ status: 'Active', failed: 0 }, u));
    db.building = {
      id: 1,
      nameRu: 'ЖК «Ситора Резиденс»', nameTg: 'ЖК «Ситора Резиденс»',
      descriptionRu: 'Жилой комплекс комфорт-класса в центре Душанбе: 16 жилых этажей, подземный паркинг, закрытый двор и коммерческий этаж.',
      descriptionTg: '',
      address: 'Душанбе, пр. Рудаки, 124', deliveryDate: '2028-06-30',
    };
    db.floors = []; db.apts = [];
    let aid = 1;
    for (let f = 0; f <= 16; f++) {
      db.floors.push({ id: f + 1, buildingId: 1, number: f, isCommercial: f === 0 });
      if (f === 0) continue;
      const rooms = [1, 2, 2, 3, 3, 2, 2, 1];
      for (let s = 0; s < 8; s++) {
        const rm = rooms[s];
        const area = rm === 1 ? 38 + Math.round(r() * 8) : rm === 2 ? 58 + Math.round(r() * 14) : 84 + Math.round(r() * 14);
        const pps = 9600 + f * 110 + (s === 3 || s === 4 ? 180 : 0);
        const x = r();
        const status = f <= 3 ? (x < 0.6 ? 'Sold' : x < 0.75 ? 'Booked' : 'Free') : f <= 9 ? (x < 0.35 ? 'Sold' : x < 0.5 ? 'Booked' : x < 0.97 ? 'Free' : 'OnRequest') : (x < 0.12 ? 'Sold' : x < 0.24 ? 'Booked' : x < 0.93 ? 'Free' : 'OnRequest');
        db.apts.push({ id: aid++, floorId: f + 1, floorNumber: f, buildingId: 1, number: f * 100 + s + 1, rooms: rm, area, pricePerSqm: pps, price: Math.round((area * pps) / 100) * 100, status, planSlot: s, model3dId: 'apt-' + f + '-' + s });
      }
    }
    const names = ['Фарҳод Раҳимов', 'Мадина Холиқова', 'Умед Ҷураев', 'Зарина Назарова', 'Сухроб Каримов', 'Дилором Шарипова', 'Бахтиёр Азизов', 'Гулнора Мирзоева', 'Парвиз Одинаев', 'Саодат Юсупова'];
    db.customers = names.map((n, i) => ({ id: i + 1, fullName: n, phone: '+99290000000' + (i + 1 > 9 ? '' : '') + (i + 1 > 9 ? '10' : i + 1), email: i % 3 === 0 ? 'client' + (i + 1) + '@mail.tj' : '', passport: 'A' + (1200000 + i * 1311), createdAt: iso(Date.now() - (30 + i * 9) * DAY) }));
    db.customers[9].phone = '+992900000010';
    db.leads = [
      ['Сарвар Алиев', '+992935551001', 'Интересует 2-комнатная до 700 тыс.', 'New'], ['Нигина Сафарова', '+992988881202', 'Хочу посмотреть квартиру на выходных', 'New'],
      ['Руслан Кодиров', '+992907771303', 'Рассрочка на 5 лет возможна?', 'InProgress'], ['Мехрангез Ато', '+992917771404', 'Нужна 3-комнатная на высоком этаже', 'InProgress'],
      ['Далер Худойбердиев', '+992927771505', 'Сравниваю с другим ЖК', 'Closed'], ['Зулфия Раупова', '+992937771606', '', 'New'],
    ].map((l, i) => ({ id: i + 1, name: l[0], phone: l[1], comment: l[2], status: l[3], assignee: i % 2 ? 'Мадина Исмоилова' : '', createdAt: iso(Date.now() - (i * 7 + 2) * 3600000 * 5), apartmentId: null }));
    db.bookings = [];
    db.deals = []; db.payments = []; db.docs = []; db.messages = [];
    const sold = db.apts.filter((a) => a.status === 'Sold').slice(0, 5);
    const stageSeq = ['Payment', 'Payment', 'Contract', 'Payment', 'Showing'];
    sold.forEach((a, i) => {
      const cur = i === 1 ? 'USD' : 'TJS';
      const rate = db.rate[1].value;
      const price = cur === 'USD' ? Math.round(a.price / rate) : a.price;
      const created = Date.now() - (140 - i * 25) * DAY;
      const deal = { id: i + 1, apartmentId: a.id, customerId: i + 1, stage: stageSeq[i], price, discount: i === 2 ? Math.round(price * 0.02) : 0, discountReason: i === 2 ? 'Акция «Старт продаж»' : '', currency: cur, managerId: 'u2', managerName: 'Мадина Исмоилова', createdAt: iso(created), plan: null, cancelReason: '' };
      db.deals.push(deal);
      const net = price - deal.discount;
      deal.plan = { downPct: [30, 25, 40, 20, 30][i], months: [60, 48, 36, 72, 60][i], day: [5, 10, 15, 1, 20][i], startDate: dateOnly(created) };
      const down = Math.round((net * deal.plan.downPct) / 100);
      db.payments.push({ id: db.seq++, dealId: deal.id, date: dateOnly(created), amount: down, currency: cur, amountDeal: down, amountTjs: cur === 'TJS' ? down : Math.round(down * rate), rate: cur === 'USD' ? rate : null, reversal: false, reversedId: null, comment: 'Первоначальный взнос', createdBy: 'Зафар Саидов' });
      const monthly = Math.round((net - down) / deal.plan.months);
      for (let k = 1; k <= Math.min(3, [4, 3, 1, 5, 0][i]); k++) db.payments.push({ id: db.seq++, dealId: deal.id, date: dateOnly(created + k * 30 * DAY), amount: monthly, currency: cur, amountDeal: monthly, amountTjs: cur === 'TJS' ? monthly : Math.round(monthly * rate), rate: cur === 'USD' ? rate : null, reversal: false, reversedId: null, comment: 'Ежемесячный платёж', createdBy: 'Зафар Саидов' });
      db.docs.push({ id: db.seq++, dealId: deal.id, name: 'Договор купли-продажи.pdf', type: 'application/pdf', size: 184320, createdAt: iso(created + DAY), uploadedBy: 'Мадина Исмоилова' });
      db.docs.push({ id: db.seq++, dealId: deal.id, name: 'Акт приёма-передачи (проект).pdf', type: 'application/pdf', size: 96256, createdAt: iso(created + 5 * DAY), uploadedBy: 'Мадина Исмоилова' });
      db.messages.push({ id: db.seq++, dealId: deal.id, text: 'Здравствуйте! Подскажите, когда будет готов график платежей?', sender: 'customer', senderName: db.customers[i].fullName, createdAt: iso(created + 2 * DAY), read: true });
      db.messages.push({ id: db.seq++, dealId: deal.id, text: 'Добрый день! График уже в кабинете, в разделе «График платежей».', sender: 'staff', senderName: 'Мадина Исмоилова', createdAt: iso(created + 2 * DAY + 3600000), read: true });
    });
    const booked = db.apts.filter((a) => a.status === 'Booked');
    booked.forEach((a, i) => {
      const left = i % 5 === 0 ? 3 : 8 + ((i * 7) % 40);
      db.bookings.push({ id: i + 1, apartmentId: a.id, customerId: null, status: 'Active', name: ['Азиз Холов', 'Шабнам Умарова', 'Ориф Насруллоев', 'Зебо Валиева', 'Мансур Хошимов'][i % 5], phone: '+99293' + (7000000 + i * 311), createdAt: iso(Date.now() - (48 - left) * 3600000), expiresAt: iso(Date.now() + left * 3600000) });
    });
    db.stages = [
      { id: 1, buildingId: 1, title: 'Котлован и фундамент', percent: 100, order: 1, description: 'Монолитная плита, гидроизоляция.', date: '2026-01-20' },
      { id: 2, buildingId: 1, title: 'Подземный паркинг', percent: 100, order: 2, description: 'Стены и перекрытие готовы.', date: '2026-04-12' },
      { id: 3, buildingId: 1, title: 'Каркас здания', percent: 72, order: 3, description: 'Возведено 12 этажей из 16.', date: '2026-09-30' },
      { id: 4, buildingId: 1, title: 'Фасады и остекление', percent: 18, order: 4, description: 'Навесной фасад, витражи.', date: '2026-10-15' },
      { id: 5, buildingId: 1, title: 'Инженерные сети', percent: 8, order: 5, description: 'Лифты, вентиляция, электрика.', date: '2026-10-15' },
      { id: 6, buildingId: 1, title: 'Благоустройство двора', percent: 0, order: 6, description: 'Озеленение, детская площадка.', date: '' },
    ];
    db.photos = [];
    audit(db, 'u2', 'Deal.Create', 'Deal', 1, null, { stage: 'Lead' });
    return db;
  }

  function audit(db, who, action, entity, id, before, after) {
    db.audit.unshift({ id: db.seq++, at: iso(Date.now()), user: who, action, entity, entityId: id, before, after });
    if (db.audit.length > 300) db.audit.pop();
  }

  let db = makeDb();
  const rateOn = (date) => { const l = db.rate.filter((x) => x.date <= date).sort((a, b) => (a.date < b.date ? 1 : -1)); return l.length ? l[0].value : null; };
  const curRate = () => rateOn(dateOnly(Date.now()));

  function err(status, title, detail, errors) { const e = new Error(detail || title); e.status = status; e.title = title; e.detail = detail || title; e.errors = errors; throw e; }

  // ----- helpers for schedule -----
  function schedule(deal) {
    if (!deal.plan) return null;
    const p = deal.plan, net = deal.price - deal.discount;
    const down = Math.round((net * p.downPct) / 100);
    const monthly = Math.round((net - down) / p.months);
    const items = [{ id: deal.id * 1000, number: 0, kind: 'Down', dueDate: p.startDate, amountDue: down }];
    const start = new Date(p.startDate + 'T00:00:00Z');
    let sum = down;
    for (let k = 1; k <= p.months; k++) {
      const d = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth() + k, p.day));
      const amt = k === p.months ? net - sum : monthly;
      sum += amt;
      items.push({ id: deal.id * 1000 + k, number: k, kind: 'Monthly', dueDate: dateOnly(d), amountDue: amt });
    }
    const paid = db.payments.filter((x) => x.dealId === deal.id).reduce((s, x) => s + (x.reversal ? -x.amountDeal : x.amountDeal), 0);
    let rest = paid; const today = dateOnly(Date.now());
    items.forEach((it) => {
      const take = Math.max(0, Math.min(it.amountDue, rest)); rest -= take;
      it.amountPaid = take;
      it.status = take >= it.amountDue ? 'Paid' : it.dueDate < today ? 'Overdue' : take > 0 ? 'Partial' : 'Pending';
    });
    const rate = curRate();
    const eqCur = deal.currency === 'TJS' ? 'USD' : 'TJS';
    const conv = (v) => (rate ? Math.round(deal.currency === 'TJS' ? v / rate : v * rate) : null);
    items.forEach((it) => { it.amountDueEquivalent = conv(it.amountDue); it.amountPaidEquivalent = conv(it.amountPaid); });
    return {
      dealId: deal.id, currency: deal.currency, price: net, downPaymentPercent: p.downPct, months: p.months, paymentDay: p.day, items,
      totalDue: net, totalPaid: paid, remaining: net - paid,
      equivalent: rate ? { currency: eqCur, rate, rateDate: dateOnly(Date.now()), isEstimate: true, note: 'Оценочно по текущему курсу', totalDue: conv(net), totalPaid: conv(paid) } : null,
    };
  }

  const aptById = (id) => db.apts.find((a) => a.id === +id);
  const dealView = (d) => {
    const a = aptById(d.apartmentId), c = db.customers.find((x) => x.id === d.customerId);
    const paid = db.payments.filter((x) => x.dealId === d.id).reduce((s, x) => s + (x.reversal ? -x.amountDeal : x.amountDeal), 0);
    return { id: d.id, stage: d.stage, price: d.price, discount: d.discount, discountReason: d.discountReason, currency: d.currency, apartmentId: d.apartmentId, apartmentNumber: a.number, floor: a.floorNumber, rooms: a.rooms, area: a.area, buildingId: 1, buildingName: db.building.nameRu, customerId: d.customerId, customerName: c ? c.fullName : '', customerPhone: c ? c.phone : '', managerName: d.managerName, createdAt: d.createdAt, paid, remaining: d.price - d.discount - paid, hasPlan: !!d.plan };
  };
  const setAptStatus = (a, s, who, why) => { if (a.status !== s) { audit(db, who, 'Apartment.Status', 'Apartment', a.id, { status: a.status }, { status: s, reason: why }); a.status = s; } };

  function validPhone(p) { return /^\+992\d{9}$/.test(String(p || '').replace(/[\s()-]/g, '')); }
  const normPhone = (p) => String(p || '').replace(/[\s()-]/g, '');

  // ----- auth -----
  function tokenFor(u) { return 'demo.' + u.id + '.' + (Date.now() + 15 * 60000); }
  function userFromToken(h) {
    if (!h) return null;
    const m = /^demo\.([^.]+)\.(\d+)$/.exec(h.replace(/^Bearer\s+/i, ''));
    if (!m) return null;
    if (+m[2] < Date.now()) return { expired: true };
    return db.users.find((u) => u.id === m[1]) || null;
  }
  function authResp(u) { return { accessToken: tokenFor(u), refreshToken: 'demo-r.' + u.id, expiresIn: 900, user: userView(u) }; }
  const userView = (u) => ({ id: u.id, email: u.email || '', phone: u.phone || '', name: u.name, fullName: u.name, roles: u.roles, role: u.roles[0], status: u.status });

  const R = [];
  const route = (m, re, fn, roles) => R.push({ m, re, fn, roles });

  function guard(ctx, roles) {
    if (!roles) return;
    if (!ctx.user || ctx.user.expired) err(401, 'Unauthorized', 'Требуется вход в систему');
    if (roles === 'any') return;
    const ok = ctx.user.roles.includes('Admin') && !roles.includes('Customer-only') ? true : ctx.user.roles.some((r) => roles.includes(r));
    if (!ok) err(403, 'Forbidden', 'Недостаточно прав для этого действия');
  }

  const staffDeal = ['Manager', 'Accountant', 'Admin'];
  function dealAccess(ctx, id) {
    const d = db.deals.find((x) => x.id === +id);
    if (!d) err(404, 'Not Found', 'Сделка не найдена');
    if (ctx.user.roles.includes('Customer')) { if (d.customerId !== ctx.user.customerId) err(404, 'Not Found', 'Сделка не найдена'); } else if (!ctx.user.roles.some((r) => staffDeal.includes(r))) err(403, 'Forbidden', 'Недостаточно прав');
    return d;
  }

  // auth
  route('POST', /^\/auth\/login$/, (c) => {
    const u = db.users.find((x) => x.email && x.email.toLowerCase() === String(c.body.email || '').toLowerCase());
    if (u && u.locked && u.locked > Date.now()) err(429, 'Too Many Requests', 'Слишком много попыток входа. Попробуйте через 15 минут.');
    if (!u || u.password !== c.body.password || u.status === 'Blocked') {
      if (u) { u.failed = (u.failed || 0) + 1; if (u.failed >= 5) { u.locked = Date.now() + 15 * 60000; u.failed = 0; } }
      err(401, 'Unauthorized', 'Неверная почта или пароль');
    }
    u.failed = 0; return authResp(u);
  });
  route('POST', /^\/auth\/refresh$/, (c) => {
    const m = /^demo-r\.(.+)$/.exec(c.body.refreshToken || ''); const u = m && db.users.find((x) => x.id === m[1]);
    if (!u) err(401, 'Unauthorized', 'Сессия истекла, войдите снова'); return authResp(u);
  });
  route('POST', /^\/auth\/logout$/, () => ({}));
  route('POST', /^\/auth\/sms\/send$/, (c) => {
    if (!validPhone(c.body.phone)) err(422, 'Validation', 'Введите номер в формате +992XXXXXXXXX', { phone: ['Введите номер в формате +992XXXXXXXXX'] });
    return { sent: true, message: 'Если номер есть в базе, код отправлен по SMS', devHint: 'Демо-код: 123456' };
  });
  route('POST', /^\/auth\/sms\/verify$/, (c) => {
    const ph = normPhone(c.body.phone); const cust = db.customers.find((x) => x.phone === ph);
    if (!cust || String(c.body.code) !== '123456') err(401, 'Unauthorized', 'Неверный или просроченный код');
    let u = db.users.find((x) => x.customerId === cust.id);
    if (!u) { u = { id: 'c' + cust.id, phone: cust.phone, name: cust.fullName, roles: ['Customer'], status: 'Active', customerId: cust.id }; db.users.push(u); }
    return authResp(u);
  });
  route('GET', /^\/auth\/me$/, (c) => userView(c.user), 'any');

  // catalog
  const bView = (lang) => {
    const b = db.building, apts = db.apts;
    return { id: b.id, name: lang === 'tg' ? b.nameTg : b.nameRu, nameRu: b.nameRu, nameTg: b.nameTg, description: lang === 'tg' && b.descriptionTg ? b.descriptionTg : b.descriptionRu, descriptionRu: b.descriptionRu, descriptionTg: b.descriptionTg, address: b.address, deliveryDate: b.deliveryDate, floorsCount: db.floors.length, totalApartments: apts.length, freeApartments: apts.filter((a) => a.status === 'Free').length };
  };
  route('GET', /^\/buildings$/, (c) => [bView(c.lang)]);
  route('GET', /^\/buildings\/(\d+)$/, (c) => bView(c.lang));
  route('GET', /^\/buildings\/(\d+)\/floors$/, () => db.floors.map((f) => ({ id: f.id, number: f.number, isCommercial: f.isCommercial, totalApartments: db.apts.filter((a) => a.floorId === f.id).length, freeApartments: db.apts.filter((a) => a.floorId === f.id && a.status === 'Free').length })));
  route('GET', /^\/floors\/(\d+)\/apartments$/, (c) => db.apts.filter((a) => a.floorId === +c.m[1]).map((a) => ({ ...a })));
  route('GET', /^\/apartments\/(\d+)$/, (c) => { const a = aptById(c.m[1]); if (!a) err(404, 'Not Found', 'Квартира не найдена'); return { ...a }; });
  const content = ['Content', 'Admin'];
  route('POST', /^\/buildings$/, (c) => { Object.assign(db.building, c.body); return bView(c.lang); }, content);
  route('PUT', /^\/buildings\/(\d+)$/, (c) => {
    const b = c.body; if (b.nameRu !== undefined) db.building.nameRu = b.nameRu; if (b.nameTg !== undefined) db.building.nameTg = b.nameTg;
    if (b.descriptionRu !== undefined) db.building.descriptionRu = b.descriptionRu; if (b.descriptionTg !== undefined) db.building.descriptionTg = b.descriptionTg;
    if (b.address !== undefined) db.building.address = b.address; audit(db, c.user.name, 'Building.Update', 'Building', 1, null, b); return bView(c.lang);
  }, content);
  route('POST', /^\/apartments$/, (c) => {
    const b = c.body; const f = db.floors.find((x) => x.id === +b.floorId); if (!f) err(422, 'Validation', 'Этаж не найден');
    if (db.apts.some((a) => a.floorId === f.id && a.planSlot === +b.planSlot)) err(409, 'Conflict', 'Эта позиция на плане этажа уже занята');
    if (!(b.planSlot >= 0 && b.planSlot <= 7)) err(422, 'Validation', 'Позиция на плане — от 0 до 7');
    const area = +b.area, pps = +b.pricePerSqm;
    const a = { id: db.seq++, floorId: f.id, floorNumber: f.number, buildingId: 1, number: +b.number, rooms: +b.rooms, area, pricePerSqm: pps, price: Math.round((area * pps) / 100) * 100, status: 'Free', planSlot: +b.planSlot, model3dId: b.model3dId || null };
    db.apts.push(a); audit(db, c.user.name, 'Apartment.Create', 'Apartment', a.id, null, a); return a;
  }, content);
  route('PUT', /^\/apartments\/(\d+)$/, (c) => {
    const a = aptById(c.m[1]); if (!a) err(404, 'Not Found', 'Квартира не найдена'); const b = c.body;
    ['number', 'rooms', 'area', 'pricePerSqm', 'planSlot'].forEach((k) => { if (b[k] !== undefined && b[k] !== '') a[k] = +b[k]; });
    if (b.model3dId !== undefined) a.model3dId = b.model3dId; a.price = Math.round((a.area * a.pricePerSqm) / 100) * 100; audit(db, c.user.name, 'Apartment.Update', 'Apartment', a.id, null, b); return a;
  }, content);
  route('PATCH', /^\/apartments\/(\d+)\/on-request$/, (c) => {
    const a = aptById(c.m[1]); if (!a) err(404, 'Not Found', 'Квартира не найдена');
    if (a.status === 'Booked' || a.status === 'Sold') err(409, 'Conflict', 'Нельзя менять статус забронированной или проданной квартиры');
    setAptStatus(a, c.body.onRequest === false ? 'Free' : 'OnRequest', c.user.name, ''); return { ...a };
  }, content);
  route('POST', /^\/apartments\/import$/, () => ({ imported: 0, message: 'В демо-режиме импорт не выполняется' }), content);

  // leads and bookings
  route('POST', /^\/leads$/, (c) => {
    const b = c.body; const nm = String(b.name || b.fullName || '').trim(); const errors = {};
    if (nm.length < 2) errors.name = ['Укажите имя'];
    if (!validPhone(b.phone)) errors.phone = ['Номер в формате +992XXXXXXXXX'];
    if (Object.keys(errors).length) err(422, 'Validation', 'Проверьте поля формы', errors);
    const l = { id: db.seq++, name: nm, phone: normPhone(b.phone), comment: b.comment || b.message || '', status: 'New', assignee: '', createdAt: iso(Date.now()), apartmentId: b.apartmentId || null };
    db.leads.unshift(l); audit(db, 'guest', 'Lead.Create', 'Lead', l.id, null, { phone: l.phone }); return l;
  });
  route('GET', /^\/leads$/, () => db.leads.map((l) => ({ ...l })), ['Manager', 'Admin']);
  route('PATCH', /^\/leads\/(\d+)$/, (c) => {
    const l = db.leads.find((x) => x.id === +c.m[1]); if (!l) err(404, 'Not Found', 'Заявка не найдена');
    ['status', 'comment'].forEach((k) => { if (c.body[k] !== undefined) l[k] = c.body[k]; }); if (c.body.assignee !== undefined) l.assignee = c.body.assignee || c.user.name;
    audit(db, c.user.name, 'Lead.Update', 'Lead', l.id, null, c.body); return l;
  }, ['Manager', 'Admin']);
  route('POST', /^\/apartments\/(\d+)\/bookings$/, (c) => {
    const a = aptById(c.m[1]); if (!a) err(404, 'Not Found', 'Квартира не найдена');
    const b = c.body; const nm = String(b.name || b.fullName || '').trim(); const errors = {};
    let cust = null;
    if (c.user && c.user.roles && (c.user.roles.includes('Manager') || c.user.roles.includes('Admin')) && b.customerId) cust = db.customers.find((x) => x.id === +b.customerId);
    if (!cust) { if (nm.length < 2) errors.name = ['Укажите имя']; if (!validPhone(b.phone)) errors.phone = ['Номер в формате +992XXXXXXXXX']; }
    if (Object.keys(errors).length) err(422, 'Validation', 'Проверьте поля формы', errors);
    if (a.status !== 'Free') err(409, 'Conflict', 'Квартира уже недоступна для брони. Выберите другую.');
    const ph = cust ? cust.phone : normPhone(b.phone);
    if (db.bookings.some((x) => x.status === 'Active' && x.phone === ph)) err(409, 'Conflict', 'На этот номер уже оформлена активная бронь');
    const bk = { id: db.seq++, apartmentId: a.id, customerId: cust ? cust.id : null, status: 'Active', name: cust ? cust.fullName : nm, phone: ph, createdAt: iso(Date.now()), expiresAt: iso(Date.now() + 48 * 3600000) };
    db.bookings.push(bk); setAptStatus(a, 'Booked', cust ? c.user.name : 'guest', 'booking');
    if (!cust) db.leads.unshift({ id: db.seq++, name: nm, phone: ph, comment: 'Бронь квартиры № ' + a.number, status: 'New', assignee: '', createdAt: iso(Date.now()), apartmentId: a.id });
    audit(db, cust ? c.user.name : 'guest', 'Booking.Create', 'Booking', bk.id, null, { apartment: a.number }); return bk;
  });
  route('POST', /^\/bookings\/(\d+)\/extend$/, (c) => {
    const bk = db.bookings.find((x) => x.id === +c.m[1]); if (!bk || bk.status !== 'Active') err(404, 'Not Found', 'Активная бронь не найдена');
    bk.expiresAt = iso(Math.max(Date.now(), new Date(bk.expiresAt).getTime()) + 48 * 3600000); audit(db, c.user.name, 'Booking.Extend', 'Booking', bk.id, null, null); return bk;
  }, ['Manager', 'Admin']);
  route('POST', /^\/bookings\/(\d+)\/cancel$/, (c) => {
    const bk = db.bookings.find((x) => x.id === +c.m[1]); if (!bk || bk.status !== 'Active') err(404, 'Not Found', 'Активная бронь не найдена');
    if (!String(c.body.reason || '').trim()) err(422, 'Validation', 'Укажите причину отмены', { reason: ['Укажите причину'] });
    bk.status = 'Cancelled'; setAptStatus(aptById(bk.apartmentId), 'Free', c.user.name, c.body.reason); return bk;
  }, ['Manager', 'Admin']);

  // customers & deals
  route('GET', /^\/customers$/, (c) => {
    const q = String(c.q.search || c.q.q || '').toLowerCase();
    return db.customers.filter((x) => !q || x.fullName.toLowerCase().includes(q) || x.phone.includes(q)).map((x) => ({ id: x.id, fullName: x.fullName, phone: x.phone, email: x.email, createdAt: x.createdAt, dealsCount: db.deals.filter((d) => d.customerId === x.id).length }));
  }, ['Manager', 'Admin']);
  route('GET', /^\/customers\/(\d+)$/, (c) => { const x = db.customers.find((y) => y.id === +c.m[1]); if (!x) err(404, 'Not Found', 'Клиент не найден'); return { ...x, deals: db.deals.filter((d) => d.customerId === x.id).map(dealView) }; }, ['Manager', 'Admin']);
  route('POST', /^\/customers$/, (c) => {
    const b = c.body; const errors = {};
    if (String(b.fullName || '').trim().length < 3) errors.fullName = ['Укажите ФИО'];
    if (!validPhone(b.phone)) errors.phone = ['Номер в формате +992XXXXXXXXX'];
    if (Object.keys(errors).length) err(422, 'Validation', 'Проверьте поля формы', errors);
    if (db.customers.some((x) => x.phone === normPhone(b.phone))) err(409, 'Conflict', 'Клиент с таким номером уже есть');
    const x = { id: db.seq++, fullName: b.fullName.trim(), phone: normPhone(b.phone), email: b.email || '', passport: b.passport || '', createdAt: iso(Date.now()) };
    db.customers.push(x);
    if (b.leadId) { const l = db.leads.find((y) => y.id === +b.leadId); if (l) l.status = 'Closed'; }
    audit(db, c.user.name, 'Customer.Create', 'Customer', x.id, null, { name: x.fullName }); return x;
  }, ['Manager', 'Admin']);
  route('GET', /^\/deals$/, (c) => db.deals.filter((d) => !c.q.stage || d.stage === c.q.stage).map(dealView).reverse(), ['Manager', 'Accountant', 'Admin']);
  route('GET', /^\/me\/deals$/, (c) => { if (!c.user.roles.includes('Customer')) err(403, 'Forbidden', 'Раздел только для покупателей'); return db.deals.filter((d) => d.customerId === c.user.customerId).map(dealView); }, 'any');
  route('GET', /^\/deals\/(\d+)$/, (c) => dealView(dealAccess(c, c.m[1])), 'any');
  route('POST', /^\/deals$/, (c) => {
    const b = c.body; const a = aptById(b.apartmentId); const cu = db.customers.find((x) => x.id === +b.customerId);
    if (!a) err(422, 'Validation', 'Выберите квартиру'); if (!cu) err(422, 'Validation', 'Выберите клиента');
    if (db.deals.some((d) => d.apartmentId === a.id && d.stage !== 'Cancelled')) err(409, 'Conflict', 'По этой квартире уже есть сделка');
    if (a.status === 'Sold' || a.status === 'OnRequest') err(409, 'Conflict', 'Квартира недоступна для сделки');
    const cur = b.currency === 'USD' ? 'USD' : 'TJS'; const rate = curRate();
    if (cur === 'USD' && !rate) err(422, 'Validation', 'Для сделки в долларах нужен курс. Введите курс в разделе «Курс».');
    const price = cur === 'USD' ? Math.round(a.price / rate) : a.price; const disc = +b.discount || 0;
    if (disc < 0 || disc >= price) err(422, 'Validation', 'Скидка должна быть от 0 и меньше цены', { discount: ['Скидка от 0 и меньше цены'] });
    if (disc > 0 && !String(b.discountReason || '').trim()) err(422, 'Validation', 'При скидке укажите причину', { discountReason: ['Укажите причину'] });
    const d = { id: db.deals.length + 1, apartmentId: a.id, customerId: cu.id, stage: 'Booking', price, discount: disc, discountReason: b.discountReason || '', currency: cur, managerId: c.user.id, managerName: c.user.name, createdAt: iso(Date.now()), plan: null, cancelReason: '' };
    db.deals.push(d);
    const bk = db.bookings.find((x) => x.apartmentId === a.id && x.status === 'Active'); if (bk) bk.status = 'Converted';
    setAptStatus(a, 'Booked', c.user.name, 'deal'); audit(db, c.user.name, 'Deal.Create', 'Deal', d.id, null, { price }); return dealView(d);
  }, ['Manager', 'Admin']);
  route('PATCH', /^\/deals\/(\d+)\/stage$/, (c) => {
    const d = db.deals.find((x) => x.id === +c.m[1]); if (!d) err(404, 'Not Found', 'Сделка не найдена');
    const to = c.body.stage; if (!STAGES.includes(to)) err(422, 'Validation', 'Неизвестный этап');
    if (d.stage === 'Completed' || d.stage === 'Cancelled') err(409, 'Conflict', 'Сделка завершена, этап менять нельзя');
    const back = STAGES.indexOf(to) < STAGES.indexOf(d.stage) || to === 'Cancelled';
    if (back && !String(c.body.reason || '').trim()) err(422, 'Validation', 'Для возврата или отмены нужна причина', { reason: ['Укажите причину'] });
    const a = aptById(d.apartmentId); const from = d.stage; d.stage = to;
    if (to === 'Cancelled') { d.cancelReason = c.body.reason; setAptStatus(a, 'Free', c.user.name, c.body.reason); }
    else if (STAGES.indexOf(to) >= STAGES.indexOf('Contract')) setAptStatus(a, 'Sold', c.user.name, ''); else if (STAGES.indexOf(to) >= STAGES.indexOf('Booking')) setAptStatus(a, 'Booked', c.user.name, ''); else setAptStatus(a, 'Free', c.user.name, '');
    audit(db, c.user.name, 'Deal.Stage', 'Deal', d.id, { stage: from }, { stage: to, reason: c.body.reason || '' }); return dealView(d);
  }, ['Manager', 'Admin']);
  route('GET', /^\/crm\/summary$/, () => {
    const dealsByStage = {}; STAGES.forEach((s) => (dealsByStage[s] = db.deals.filter((d) => d.stage === s).length));
    const apartmentsByStatus = {}; ['Free', 'Booked', 'Sold', 'OnRequest'].forEach((s) => (apartmentsByStatus[s] = db.apts.filter((a) => a.status === s).length));
    const act = db.bookings.filter((b) => b.status === 'Active');
    const sold = db.deals.filter((d) => d.stage !== 'Cancelled').reduce((s, d) => s + (d.currency === 'TJS' ? d.price - d.discount : (d.price - d.discount) * curRate()), 0);
    return { openLeads: db.leads.filter((l) => l.status !== 'Closed').length, newLeads: db.leads.filter((l) => l.status === 'New').length, activeBookings: act.length, expiringBookings: act.filter((b) => new Date(b.expiresAt).getTime() - Date.now() < 24 * 3600000).length, dealsByStage, apartmentsByStatus, salesTotalTjs: Math.round(sold) };
  }, ['Manager', 'Admin']);
  route('GET', /^\/crm\/pipeline$/, () => {
    const cols = STAGES.filter((s) => s !== 'Cancelled').map((s) => {
      let items = db.deals.filter((d) => d.stage === s).map((d) => { const v = dealView(d); return { kind: 'deal', id: v.id, title: '№ ' + v.apartmentNumber + ' · ' + v.customerName, price: v.price - v.discount, currency: v.currency, subtitle: v.rooms + '-комн., ' + v.area + ' м²' }; });
      if (s === 'Lead') items = db.leads.filter((l) => l.status !== 'Closed').map((l) => ({ kind: 'lead', id: l.id, title: l.name, subtitle: l.phone, comment: l.comment })).concat(items);
      if (s === 'Booking') items = db.bookings.filter((b) => b.status === 'Active').map((b) => ({ kind: 'booking', id: b.id, title: '№ ' + aptById(b.apartmentId).number + ' · ' + b.name, subtitle: b.phone, expiresAt: b.expiresAt })).concat(items);
      return { stage: s, count: items.length, items };
    });
    return { columns: cols };
  }, ['Manager', 'Admin']);

  // schedule & payments
  route('POST', /^\/deals\/(\d+)\/plan$/, (c) => {
    const d = db.deals.find((x) => x.id === +c.m[1]); if (!d) err(404, 'Not Found', 'Сделка не найдена');
    if (db.payments.some((p) => p.dealId === d.id)) err(409, 'Conflict', 'Есть платежи, график уже нельзя изменить');
    const b = c.body; const errors = {};
    if (!(b.downPaymentPercent >= 0 && b.downPaymentPercent <= 90)) errors.downPaymentPercent = ['Взнос от 0 до 90%'];
    if (!(b.months >= 1 && b.months <= 360)) errors.months = ['Срок от 1 до 360 месяцев'];
    if (!(b.paymentDay >= 1 && b.paymentDay <= 28)) errors.paymentDay = ['День платежа от 1 до 28'];
    if (Object.keys(errors).length) err(422, 'Validation', 'Проверьте поля графика', errors);
    d.plan = { downPct: +b.downPaymentPercent, months: +b.months, day: +b.paymentDay, startDate: dateOnly(Date.now()) };
    audit(db, c.user.name, 'Plan.Create', 'Deal', d.id, null, d.plan); return schedule(d);
  }, ['Manager', 'Admin']);
  route('GET', /^\/deals\/(\d+)\/schedule$/, (c) => { const d = dealAccess(c, c.m[1]); const s = schedule(d); if (!s) err(404, 'Not Found', 'График ещё не составлен'); return s; }, 'any');
  route('POST', /^\/deals\/(\d+)\/payments$/, (c) => {
    const d = db.deals.find((x) => x.id === +c.m[1]); if (!d) err(404, 'Not Found', 'Сделка не найдена');
    const s = schedule(d); if (!s) err(409, 'Conflict', 'Сначала составьте график платежей');
    const b = c.body; const cur = b.currency === 'USD' ? 'USD' : 'TJS'; const amount = +b.amount; const date = b.date || dateOnly(Date.now());
    if (!(amount > 0)) err(422, 'Validation', 'Введите сумму больше нуля', { amount: ['Введите сумму больше нуля'] });
    if (date > dateOnly(Date.now())) err(422, 'Validation', 'Дата платежа не может быть в будущем', { date: ['Дата не в будущем'] });
    const rate = rateOn(date); if (cur !== d.currency && !rate) err(422, 'Validation', 'Нет курса на эту дату');
    const amtDeal = cur === d.currency ? amount : (d.currency === 'TJS' ? Math.round(amount * rate) : Math.round(amount / rate));
    if (amtDeal > s.remaining) err(409, 'Conflict', 'Платёж больше остатка по договору');
    const p = { id: db.seq++, dealId: d.id, date, amount, currency: cur, amountDeal: amtDeal, amountTjs: cur === 'TJS' ? amount : Math.round(amount * rate), rate: cur !== d.currency || cur === 'USD' ? rate : null, reversal: false, reversedId: null, comment: b.comment || '', createdBy: c.user.name };
    db.payments.push(p); audit(db, c.user.name, 'Payment.Create', 'Payment', p.id, null, { amount, cur }); return p;
  }, ['Accountant', 'Admin']);
  route('POST', /^\/payments\/(\d+)\/reverse$/, (c) => {
    const p = db.payments.find((x) => x.id === +c.m[1]); if (!p) err(404, 'Not Found', 'Платёж не найден');
    if (p.reversal) err(409, 'Conflict', 'Сторно нельзя сторнировать'); if (db.payments.some((x) => x.reversedId === p.id)) err(409, 'Conflict', 'Этот платёж уже сторнирован');
    if (!String(c.body.reason || '').trim()) err(422, 'Validation', 'Укажите причину сторно', { reason: ['Укажите причину'] });
    const r = { ...p, id: db.seq++, date: dateOnly(Date.now()), reversal: true, reversedId: p.id, comment: 'Сторно: ' + c.body.reason, createdBy: c.user.name };
    db.payments.push(r); audit(db, c.user.name, 'Payment.Reverse', 'Payment', p.id, null, { reason: c.body.reason }); return r;
  }, ['Accountant', 'Admin']);
  route('GET', /^\/rates\/current$/, () => { const v = curRate(); if (!v) err(404, 'Not Found', 'Курс не задан'); return { rate: v, value: v, date: dateOnly(Date.now()), currency: 'USD' }; });
  route('PUT', /^\/rates$/, (c) => {
    const v = +c.body.rate; const date = c.body.date || dateOnly(Date.now());
    if (!(v > 0)) err(422, 'Validation', 'Введите курс больше нуля', { rate: ['Курс больше нуля'] });
    if (date > dateOnly(Date.now() + DAY)) err(422, 'Validation', 'Курс можно ввести не дальше чем на завтра');
    db.rate = db.rate.filter((x) => x.date !== date); db.rate.push({ date, value: v }); audit(db, c.user.name, 'Rate.Set', 'Rate', date, null, { v }); return { rate: v, date };
  }, ['Accountant', 'Admin']);
  route('GET', /^\/reports\/payments$/, (c) => {
    const from = c.q.from || dateOnly(Date.now()).slice(0, 8) + '01', to = c.q.to || dateOnly(Date.now());
    const rows = db.payments.filter((p) => p.date >= from && p.date <= to).map((p) => { const d = db.deals.find((x) => x.id === p.dealId); const a = aptById(d.apartmentId); const cu = db.customers.find((x) => x.id === d.customerId); return { ...p, apartmentNumber: a.number, customerName: cu.fullName }; });
    const byCur = {}; rows.forEach((p) => { byCur[p.currency] = (byCur[p.currency] || 0) + (p.reversal ? -p.amount : p.amount); });
    return { from, to, rows, totalsByCurrency: byCur, totalTjs: rows.reduce((s, p) => s + (p.reversal ? -p.amountTjs : p.amountTjs), 0) };
  }, ['Accountant', 'Admin']);
  route('GET', /^\/reports\/debts$/, (c) => {
    const asOf = c.q.asOf || dateOnly(Date.now()); const rows = [];
    db.deals.filter((d) => d.stage !== 'Cancelled' && d.plan).forEach((d) => {
      const s = schedule(d); const od = s.items.filter((i) => i.dueDate <= asOf && i.amountPaid < i.amountDue);
      if (!od.length) return; const sum = od.reduce((t, i) => t + i.amountDue - i.amountPaid, 0); const since = od[0].dueDate;
      const a = aptById(d.apartmentId), cu = db.customers.find((x) => x.id === d.customerId);
      rows.push({ dealId: d.id, customerName: cu.fullName, phone: cu.phone, apartmentNumber: a.number, currency: d.currency, overdue: sum, since, days: Math.max(0, Math.round((new Date(asOf) - new Date(since)) / DAY)), remaining: s.remaining });
    });
    return { asOf, rows };
  }, ['Accountant', 'Admin']);
  route('GET', /^\/reports\/(payments|debts)\.xlsx$/, (c) => ({ demoDoc: { title: 'Отчёт Excel (демо)', lines: ['В демо-режиме файл .xlsx не создаётся.', 'При подключении к API здесь скачается настоящий Excel.'] } }), ['Accountant', 'Admin']);

  // documents / files
  route('GET', /^\/deals\/(\d+)\/documents$/, (c) => { const d = dealAccess(c, c.m[1]); return db.docs.filter((x) => x.dealId === d.id).map((x) => ({ ...x })); }, 'any');
  route('POST', /^\/deals\/(\d+)\/documents$/, (c) => {
    const d = db.deals.find((x) => x.id === +c.m[1]); if (!d) err(404, 'Not Found', 'Сделка не найдена'); const f = c.file;
    if (!f) err(422, 'Validation', 'Выберите файл'); if (f.size > 20 * 1024 * 1024) err(422, 'Validation', 'Файл больше 20 МБ');
    const x = { id: db.seq++, dealId: d.id, name: f.name, type: f.type || 'application/octet-stream', size: f.size, createdAt: iso(Date.now()), uploadedBy: c.user.name };
    db.docs.push(x); audit(db, c.user.name, 'Document.Upload', 'Document', x.id, null, { name: x.name }); return x;
  }, ['Manager', 'Accountant', 'Admin']);
  route('GET', /^\/documents\/(\d+)\/download$/, (c) => {
    const x = db.docs.find((y) => y.id === +c.m[1]); if (!x) err(404, 'Not Found', 'Документ не найден'); dealAccess(c, x.dealId);
    return { url: '/files?token=demo-doc-' + x.id, expiresAt: iso(Date.now() + 15 * 60000), demoDoc: { title: x.name, lines: ['Демонстрационный документ', '', 'В боевом режиме по этой ссылке скачивается настоящий файл.', 'Ссылка действует 15 минут и подписана сервером.'] } };
  }, 'any');
  route('GET', /^\/deals\/(\d+)\/schedule\.pdf$/, (c) => {
    const d = dealAccess(c, c.m[1]); const s = schedule(d); if (!s) err(404, 'Not Found', 'График ещё не составлен'); const v = dealView(d);
    const lines = ['График платежей', 'Квартира № ' + v.apartmentNumber + ', этаж ' + v.floor, 'Покупатель: ' + v.customerName, 'Валюта договора: ' + s.currency, ''].concat(s.items.slice(0, 18).map((i) => i.dueDate + '   ' + i.amountDue + '   ' + i.status)).concat(['...', 'Итого: ' + s.totalDue + '  Оплачено: ' + s.totalPaid]);
    return { demoDoc: { title: 'График платежей', lines } };
  }, 'any');
  route('GET', /^\/deals\/(\d+)\/statement\.pdf$/, (c) => {
    const d = dealAccess(c, c.m[1]); const s = schedule(d); if (!s) err(404, 'Not Found', 'График ещё не составлен'); const v = dealView(d);
    const lines = ['Акт сверки на ' + (c.q.date || dateOnly(Date.now())), 'Сделка № ' + d.id + ', квартира № ' + v.apartmentNumber, 'Покупатель: ' + v.customerName, '', 'Начислено: ' + s.totalDue + ' ' + s.currency, 'Оплачено: ' + s.totalPaid + ' ' + s.currency, 'Остаток: ' + s.remaining + ' ' + s.currency, ''].concat(db.payments.filter((p) => p.dealId === d.id).map((p) => p.date + '   ' + (p.reversal ? '-' : '') + p.amount + ' ' + p.currency));
    return { demoDoc: { title: 'Акт сверки', lines } };
  }, 'any');
  route('GET', /^\/files$/, () => ({ demoDoc: { title: 'Файл', lines: ['Демо-файл'] } }));

  // construction
  route('GET', /^\/buildings\/(\d+)\/stages$/, (c) => {
    if (c.user && c.user.roles.includes('Customer') && !db.deals.some((d) => d.customerId === c.user.customerId && d.stage !== 'Cancelled')) err(403, 'Forbidden', 'Нет доступа к ходу стройки');
    return db.stages.map((s) => ({ ...s, photos: db.photos.filter((p) => p.stageId === s.id).map((p) => ({ id: p.id, url: p.url })) }));
  }, 'any');
  route('POST', /^\/buildings\/(\d+)\/stages$/, (c) => { const s = { id: db.seq++, buildingId: 1, title: c.body.title, percent: +c.body.percent || 0, order: db.stages.length + 1, description: c.body.description || '', date: dateOnly(Date.now()) }; if (!s.title) err(422, 'Validation', 'Укажите название этапа'); db.stages.push(s); audit(db, c.user.name, 'Stage.Create', 'Stage', s.id, null, s); return s; }, content);
  route('PUT', /^\/stages\/(\d+)$/, (c) => { const s = db.stages.find((x) => x.id === +c.m[1]); if (!s) err(404, 'Not Found', 'Этап не найден'); if (!(c.body.percent >= 0 && c.body.percent <= 100)) err(422, 'Validation', 'Процент от 0 до 100'); ['title', 'description'].forEach((k) => { if (c.body[k] !== undefined) s[k] = c.body[k]; }); s.percent = +c.body.percent; s.date = dateOnly(Date.now()); audit(db, c.user.name, 'Stage.Update', 'Stage', s.id, null, { percent: s.percent }); return s; }, content);
  route('POST', /^\/stages\/(\d+)\/photos$/, (c) => { const f = c.file; if (!f) err(422, 'Validation', 'Выберите фото'); const p = { id: db.seq++, stageId: +c.m[1], url: f.dataUrl || '' }; db.photos.push(p); return { id: p.id, url: p.url }; }, content);

  // chat
  route('GET', /^\/deals\/(\d+)\/messages$/, (c) => { const d = dealAccess(c, c.m[1]); const mine = c.user.roles.includes('Customer') ? 'customer' : 'staff'; const l = db.messages.filter((m) => m.dealId === d.id); l.forEach((m) => { if (m.sender !== mine) m.read = true; }); return l.slice().reverse().map((m) => ({ ...m, mine: m.sender === mine })); }, 'any');
  route('POST', /^\/deals\/(\d+)\/messages$/, (c) => {
    const d = dealAccess(c, c.m[1]); const text = String(c.body.text || '').trim(); if (!text) err(422, 'Validation', 'Введите сообщение');
    const sender = c.user.roles.includes('Customer') ? 'customer' : 'staff';
    const m = { id: db.seq++, dealId: d.id, text, sender, senderName: c.user.name, createdAt: iso(Date.now()), read: false }; db.messages.push(m);
    if (sender === 'customer') setTimeout(() => { db.messages.push({ id: db.seq++, dealId: d.id, text: 'Приняли, ответим в ближайшее время.', sender: 'staff', senderName: d.managerName, createdAt: iso(Date.now()), read: false }); S.bus && S.bus.emit('chat', d.id); }, 1800);
    return { ...m, mine: true };
  }, 'any');

  // admin
  route('GET', /^\/audit$/, (c) => { const e = c.q.entity; return db.audit.filter((a) => !e || a.entity === e).slice(0, 100); }, ['Admin']);
  route('GET', /^\/users$/, () => db.users.filter((u) => !u.roles.includes('Customer')).map(userView), ['Admin']);
  route('POST', /^\/users$/, (c) => {
    const b = c.body; const errors = {};
    if (!/^\S+@\S+\.\S+$/.test(b.email || '')) errors.email = ['Укажите почту'];
    if (!/^(?=.*\d)(?=.*[a-z])(?=.*[A-Z]).{8,}$/.test(b.password || '')) errors.password = ['Не короче 8 символов, цифра, строчная и заглавная буква'];
    if (Object.keys(errors).length) err(422, 'Validation', 'Проверьте поля', errors);
    if (db.users.some((u) => u.email === b.email)) err(409, 'Conflict', 'Пользователь с такой почтой уже есть');
    const u = { id: 'u' + db.seq++, email: b.email, password: b.password, name: b.name || b.fullName || b.email, roles: [b.role || 'Manager'], status: 'Active' }; db.users.push(u); audit(db, c.user.name, 'User.Create', 'User', u.id, null, { email: u.email }); return userView(u);
  }, ['Admin']);
  route('PATCH', /^\/users\/([^/]+)\/status$/, (c) => { const u = db.users.find((x) => x.id === c.m[1]); if (!u) err(404, 'Not Found', 'Пользователь не найден'); u.status = c.body.blocked || c.body.status === 'Blocked' ? 'Blocked' : 'Active'; u.locked = 0; audit(db, c.user.name, 'User.Status', 'User', u.id, null, { status: u.status }); return userView(u); }, ['Admin']);
  route('GET', /^\/health(\/ready|\/live)?$/, () => ({ status: 'Healthy', checks: { database: 'Healthy', storage: 'Healthy', hangfire: 'Healthy' } }));

  S.mock = {
    reset() { db = makeDb(); },
    demoUsers: STAFF.map((u) => ({ email: u.email, password: u.password, role: u.roles[0] })),
    demoCustomerPhone: '+992900000001',
    async handle(method, path, o) {
      o = o || {};
      await new Promise((r) => setTimeout(r, 90 + Math.random() * 160));
      const user = userFromToken(o.token);
      const ctx = { user, q: o.query || {}, body: o.body || {}, lang: o.lang || 'ru', file: o.file };
      for (const r of R) {
        if (r.m !== method) continue; const m = r.re.exec(path); if (!m) continue;
        ctx.m = m; guard(ctx, r.roles); const out = r.fn(ctx);
        return JSON.parse(JSON.stringify(out === undefined ? {} : out));
      }
      err(404, 'Not Found', 'Адрес не найден: ' + path);
    },
  };
})();
