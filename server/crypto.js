import crypto from 'node:crypto';
import { promisify } from 'node:util';
import { getConfig } from './config.js';

const scrypt = promisify(crypto.scrypt);
const pbkdf2 = promisify(crypto.pbkdf2);

const SCRYPT = { N: 32768, r: 8, p: 1, keylen: 64, maxmem: 96 * 1024 * 1024 };

export const sha256 = (value) => crypto.createHash('sha256').update(value).digest('hex');
export const hmac = (value, label = '') =>
  crypto.createHmac('sha256', getConfig().sessionSecret).update(`${label}:${value}`).digest('hex');
export const randomToken = (bytes = 32) => crypto.randomBytes(bytes).toString('base64url');

export function safeEqual(a, b) {
  const x = Buffer.from(String(a));
  const y = Buffer.from(String(b));
  return x.length === y.length && crypto.timingSafeEqual(x, y);
}

// Passwords are peppered with a server-side secret before scrypt, so a leaked database alone cannot be cracked offline.
const pepper = (password) => hmac(password, 'pw');

export async function hashPassword(password) {
  const salt = crypto.randomBytes(16);
  const key = await scrypt(pepper(password), salt, SCRYPT.keylen, SCRYPT);
  return `scrypt$${SCRYPT.N}$${SCRYPT.r}$${SCRYPT.p}$${salt.toString('base64')}$${key.toString('base64')}`;
}

export async function verifyPassword(password, stored) {
  if (typeof stored !== 'string') return { ok: false, needsRehash: false };
  const parts = stored.split('$');

  if (parts[0] === 'scrypt' && parts.length === 6) {
    const [, N, r, p, salt, hash] = parts;
    const expected = Buffer.from(hash, 'base64');
    const actual = await scrypt(pepper(password), Buffer.from(salt, 'base64'), expected.length, {
      N: Number(N), r: Number(r), p: Number(p), maxmem: SCRYPT.maxmem,
    });
    const ok = crypto.timingSafeEqual(actual, expected);
    return { ok, needsRehash: ok && Number(N) < SCRYPT.N };
  }

  // Legacy hashes imported from the old Supabase version of the site: upgraded on first successful login.
  if (parts[0] === 'pbkdf2-sha256' && parts.length === 4) {
    const iterations = Number(parts[1]);
    if (!Number.isInteger(iterations) || iterations < 1000 || iterations > 2_000_000) return { ok: false, needsRehash: false };
    const expected = Buffer.from(parts[3], 'base64');
    const actual = await pbkdf2(password, Buffer.from(parts[2], 'base64'), iterations, expected.length, 'sha256');
    const ok = actual.length === expected.length && crypto.timingSafeEqual(actual, expected);
    return { ok, needsRehash: ok };
  }

  return { ok: false, needsRehash: false };
}

// Burns the same CPU time when the user does not exist, so response timing does not reveal valid usernames.
let dummyHash;
export async function burnPasswordCheck(password) {
  dummyHash ??= await hashPassword('dummy-password-for-timing');
  await verifyPassword(password, dummyHash);
}

export function encrypt(plain) {
  const iv = crypto.randomBytes(12);
  const cipher = crypto.createCipheriv('aes-256-gcm', getConfig().encKey, iv);
  const data = Buffer.concat([cipher.update(plain, 'utf8'), cipher.final()]);
  return Buffer.concat([iv, cipher.getAuthTag(), data]).toString('base64url');
}

export function decrypt(payload) {
  const raw = Buffer.from(payload, 'base64url');
  const decipher = crypto.createDecipheriv('aes-256-gcm', getConfig().encKey, raw.subarray(0, 12));
  decipher.setAuthTag(raw.subarray(12, 28));
  return Buffer.concat([decipher.update(raw.subarray(28)), decipher.final()]).toString('utf8');
}

export function signTicket(payload, ttlMs) {
  const body = Buffer.from(JSON.stringify({ ...payload, exp: Date.now() + ttlMs, jti: randomToken(12) })).toString('base64url');
  return `${body}.${hmac(body, 'ticket')}`;
}

export function verifyTicket(ticket) {
  if (typeof ticket !== 'string' || ticket.length > 1024) return null;
  const [body, sig] = ticket.split('.');
  if (!body || !sig || !safeEqual(sig, hmac(body, 'ticket'))) return null;
  try {
    const payload = JSON.parse(Buffer.from(body, 'base64url').toString('utf8'));
    return payload.exp > Date.now() ? payload : null;
  } catch {
    return null;
  }
}

// ---------- TOTP (RFC 6238, SHA-1, 6 digits, 30 s) ----------
const B32 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';

export function base32Encode(buffer) {
  let bits = 0;
  let value = 0;
  let out = '';
  for (const byte of buffer) {
    value = (value << 8) | byte;
    bits += 8;
    while (bits >= 5) {
      out += B32[(value >>> (bits - 5)) & 31];
      bits -= 5;
    }
  }
  if (bits > 0) out += B32[(value << (5 - bits)) & 31];
  return out;
}

function base32Decode(text) {
  let bits = 0;
  let value = 0;
  const out = [];
  for (const char of text.replace(/=+$/, '').toUpperCase()) {
    const index = B32.indexOf(char);
    if (index < 0) continue;
    value = (value << 5) | index;
    bits += 5;
    if (bits >= 8) {
      out.push((value >>> (bits - 8)) & 255);
      bits -= 8;
    }
  }
  return Buffer.from(out);
}

export const generateTotpSecret = () => base32Encode(crypto.randomBytes(20));

function hotp(secretBase32, counter) {
  const buf = Buffer.alloc(8);
  buf.writeBigUInt64BE(BigInt(counter));
  const digest = crypto.createHmac('sha1', base32Decode(secretBase32)).update(buf).digest();
  const offset = digest[digest.length - 1] & 15;
  const code = ((digest[offset] & 0x7f) << 24) | (digest[offset + 1] << 16) | (digest[offset + 2] << 8) | digest[offset + 3];
  return String(code % 1_000_000).padStart(6, '0');
}

// Returns the matched time step (so callers can reject replays) or null.
export function verifyTotp(secretBase32, code, afterStep = 0) {
  if (!/^\d{6}$/.test(code)) return null;
  const now = Math.floor(Date.now() / 30_000);
  for (const step of [now, now - 1, now + 1]) {
    if (step > afterStep && safeEqual(hotp(secretBase32, step), code)) return step;
  }
  return null;
}

export function buildOtpAuthUri(secret, account) {
  const issuer = getConfig().issuer;
  const label = encodeURIComponent(`${issuer}:${account}`);
  return `otpauth://totp/${label}?secret=${secret}&issuer=${encodeURIComponent(issuer)}&algorithm=SHA1&digits=6&period=30`;
}

// ---------- Backup codes ----------
const CODE_ALPHABET = 'ABCDEFGHJKMNPQRSTUVWXYZ23456789';

export function generateBackupCodes(count = 8) {
  return Array.from({ length: count }, () => {
    const raw = Array.from(crypto.randomBytes(10), (b) => CODE_ALPHABET[b % CODE_ALPHABET.length]).join('');
    return `${raw.slice(0, 5)}-${raw.slice(5)}`;
  });
}

export const normalizeBackupCode = (code) => String(code).toUpperCase().replace(/[^A-Z0-9]/g, '');
export const hashBackupCode = (code) => hmac(normalizeBackupCode(code), 'backup');
