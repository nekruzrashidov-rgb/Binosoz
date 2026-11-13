#!/usr/bin/env node
// Refresh token Gmail для отправки писем с кодами входа (Email__Gmail__RefreshToken). Владелец запускает один раз на своём ПК.
//   node backend/scripts/gmail-token.mjs                         — спросит Client ID и Client Secret (секрет без эха)
//   node backend/scripts/gmail-token.mjs <client_id> <secret>    — или аргументами
// Node 18+, без зависимостей. Поднимает сервер на 127.0.0.1:<случайный порт>, печатает ссылку согласия Google
// (scope gmail.send, offline, PKCE S256, state), принимает код и меняет его на токены. Печатает только refresh token;
// в файлы ничего не пишет. Подробно — docs/DEPLOY.md, «Почта через Gmail API».
import http from 'node:http';
import crypto from 'node:crypto';
import readline from 'node:readline';

const SCOPE = 'https://www.googleapis.com/auth/gmail.send';
const AUTH_URL = 'https://accounts.google.com/o/oauth2/v2/auth';
const TOKEN_URL = 'https://oauth2.googleapis.com/token';

const ask = (question, hidden) => new Promise((resolve) => {
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout, terminal: true });
  if (hidden) {
    // Секрет не отображается при вводе.
    rl._writeToOutput = (s) => { if (s.includes(question)) rl.output.write(s); };
  }
  rl.question(question, (answer) => { rl.close(); if (hidden) process.stdout.write('\n'); resolve(answer.trim()); });
});

const b64url = (buf) => buf.toString('base64').replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');

async function main() {
  const clientId = process.argv[2] || await ask('Client ID: ');
  const clientSecret = process.argv[3] || await ask('Client Secret (не отображается): ', true);
  if (!clientId || !clientSecret) {
    console.error('Нужны Client ID и Client Secret (Google Cloud → Credentials → OAuth client, тип Desktop app).');
    process.exit(1);
  }

  const verifier = b64url(crypto.randomBytes(48));
  const challenge = b64url(crypto.createHash('sha256').update(verifier).digest());
  const state = b64url(crypto.randomBytes(24));

  const server = http.createServer();
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const redirectUri = `http://127.0.0.1:${server.address().port}`;

  const url = new URL(AUTH_URL);
  url.search = new URLSearchParams({
    client_id: clientId,
    redirect_uri: redirectUri,
    response_type: 'code',
    scope: SCOPE,
    access_type: 'offline',
    prompt: 'consent',
    code_challenge: challenge,
    code_challenge_method: 'S256',
    state,
  }).toString();

  console.log('\nОткройте ссылку в браузере и войдите как binosoz27@gmail.com:\n');
  console.log(url.toString());
  console.log('\nЕсли Google пишет «приложение не проверено» — «Дополнительно» → «Перейти». Жду ответа…\n');

  const code = await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('Нет ответа от Google за 10 минут — запустите скрипт заново.')), 10 * 60 * 1000);
    server.on('request', (req, res) => {
      const params = new URL(req.url, redirectUri).searchParams;
      if (!params.has('code') && !params.has('error')) { res.writeHead(404).end(); return; }
      const fail = (text) => { res.writeHead(400, { 'Content-Type': 'text/plain; charset=utf-8' }).end(text); };
      if (params.get('state') !== state) { fail('Неверный state — запрос не от этого скрипта.'); return; }
      if (params.get('error')) { fail('Google: ' + params.get('error')); clearTimeout(timer); reject(new Error('Google отказал: ' + params.get('error'))); return; }
      res.writeHead(200, { 'Content-Type': 'text/plain; charset=utf-8' }).end('Готово. Вернитесь в терминал — это окно можно закрыть.');
      clearTimeout(timer);
      resolve(params.get('code'));
    });
  }).finally(() => server.close());

  const response = await fetch(TOKEN_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ code, client_id: clientId, client_secret: clientSecret, redirect_uri: redirectUri, grant_type: 'authorization_code', code_verifier: verifier }),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok || !data.refresh_token) {
    // Токены и секреты не печатаются: только код ошибки Google.
    console.error('Google не выдал refresh token: ' + (data.error || ('HTTP ' + response.status)) +
      (data.refresh_token ? '' : '. Проверьте, что в ссылке был prompt=consent и что доступ приложения не отозван.'));
    process.exit(1);
  }

  console.log('Refresh token:\n');
  console.log(data.refresh_token);
  console.log('\nВставьте его в Render → Environment как Email__Gmail__RefreshToken. Никуда больше не сохраняйте.');
}

main().catch((e) => { console.error(e.message); process.exit(1); });
