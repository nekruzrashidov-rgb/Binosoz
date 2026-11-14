// QR-код адреса сайта в SVG: frontend/img/qr-binosoz.svg (футер «Открыть на телефоне») и docs/qr-binosoz.svg.
// Локально, без внешних сервисов: cd frontend/tools/qr && npm ci && npm run build
// Цвета — тёмные модули на белом (QR читается только с хорошим контрастом; тёмная тема сайта это не меняет).
import QRCode from 'qrcode';
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const URL_SITE = 'https://binosoz.vercel.app';
const here = dirname(fileURLToPath(import.meta.url));
const svg = await QRCode.toString(URL_SITE, { type: 'svg', errorCorrectionLevel: 'M', margin: 2, color: { dark: '#1a1510', light: '#ffffff' } });
const titled = svg.replace('<svg ', '<svg role="img" aria-label="QR-код: ' + URL_SITE + '" ');
for (const out of [join(here, '../../img/qr-binosoz.svg'), join(here, '../../../docs/qr-binosoz.svg')]) {
  writeFileSync(out, titled + '\n');
  console.log('записан', out);
}
