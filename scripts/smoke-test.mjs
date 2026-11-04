// Smoke test: node scripts/smoke-test.mjs (needs Supabase env vars + `npm run dev:api`)
import crypto from 'node:crypto';

const base = process.env.API ?? 'http://localhost:3011';
const origin = 'http://localhost:5232';
let cookie = '';

async function call(method, path, body) {
  const res = await fetch(base + path, {
    method,
    headers: { origin, 'x-requested-with': 'nesimokau', 'content-type': 'application/json', ...(cookie ? { cookie } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const set = res.headers.get('set-cookie');
  if (set) cookie = set.split(';')[0];
  return { status: res.status, data: await res.json().catch(() => null) };
}

function totp(secret) {
  const A = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = '';
  for (const c of secret) bits += A.indexOf(c).toString(2).padStart(5, '0');
  const key = Buffer.from(bits.match(/.{8}/g).map((b) => parseInt(b, 2)));
  const buf = Buffer.alloc(8);
  buf.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30000)));
  const h = crypto.createHmac('sha1', key).update(buf).digest();
  const o = h[19] & 15;
  return String(((h.readUInt32BE(o) & 0x7fffffff) % 1000000)).padStart(6, '0');
}

const name = 'Test' + Math.floor(Math.random() * 1e6);
const pass = 'Slaptazodis1!x';
const log = (label, r) => console.log(label.padEnd(22), r.status, JSON.stringify(r.data).slice(0, 110));

log('register', await call('POST', '/api/auth/register', { username: name, password: pass, avatarId: 'a1', classGroup: '5-6' }));
log('me', await call('GET', '/api/auth/me'));
log('progress put', await call('PUT', '/api/progress', { name, level: 3, xp: 40, coins: 10, best_score: 50, recent_results: [{ score: 70 }] }));
log('progress get', await call('GET', '/api/progress'));
log('leaderboard', await call('GET', '/api/leaderboard'));
log('activity', await call('POST', '/api/activity', { event_type: 'test', score: 5 }));
const setup = await call('POST', '/api/auth/2fa/setup', { password: pass });
log('2fa setup', { status: setup.status, data: { size: setup.data?.qr?.size } });
log('2fa enable', await call('POST', '/api/auth/2fa/enable', { code: totp(setup.data.secret) }));
await call('POST', '/api/auth/logout');
log('me after logout', await call('GET', '/api/auth/me'));
const login = await call('POST', '/api/auth/login', { username: name, password: pass });
log('login (mfa)', login);
log('mfa bad', await call('POST', '/api/auth/mfa', { ticket: login.data.ticket, code: '000000' }));
console.log('(waiting for next TOTP step to avoid replay rejection)');
await new Promise((r) => setTimeout(r, 31000));
log('mfa good', await call('POST', '/api/auth/mfa', { ticket: login.data.ticket, code: totp(setup.data.secret) }));
log('bad origin', await fetch(base + '/api/auth/logout', { method: 'POST', headers: { origin: 'https://evil.com', 'x-requested-with': 'nesimokau' } }).then(async (r) => ({ status: r.status, data: await r.json() })));
