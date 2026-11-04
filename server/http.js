import { getConfig, isOriginAllowed } from './config.js';
import { db } from './supabase.js';
import { hmac } from './crypto.js';

const MAX_BODY_BYTES = 3 * 1024 * 1024;

export class HttpError extends Error {
  constructor(status, message, extra = {}) {
    super(message);
    this.status = status;
    this.extra = extra;
  }
}

export function clientIp(req) {
  const forwarded = req.headers['x-vercel-forwarded-for'] ?? req.headers['x-real-ip'] ?? req.headers['x-forwarded-for'];
  const first = String(Array.isArray(forwarded) ? forwarded[0] : (forwarded ?? '')).split(',')[0].trim();
  return first || req.socket?.remoteAddress || 'unknown';
}

export const ipHash = (req) => hmac(clientIp(req), 'ip').slice(0, 32);

export function applyBaseHeaders(req, res) {
  res.setHeader('Cache-Control', 'no-store, max-age=0');
  res.setHeader('X-Content-Type-Options', 'nosniff');
  res.setHeader('Referrer-Policy', 'no-referrer');
  res.setHeader('Content-Security-Policy', "default-src 'none'; frame-ancestors 'none'");

  const origin = req.headers.origin;
  if (origin && isOriginAllowed(origin)) {
    res.setHeader('Access-Control-Allow-Origin', origin);
    res.setHeader('Access-Control-Allow-Credentials', 'true');
    res.setHeader('Access-Control-Allow-Headers', 'content-type, x-requested-with');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
    res.setHeader('Access-Control-Max-Age', '600');
    res.setHeader('Vary', 'Origin');
  }
}

// CSRF defence: state-changing calls need an allowed Origin plus a custom header (which forces a CORS preflight).
export function assertSameSiteRequest(req) {
  if (req.method === 'GET' || req.method === 'HEAD') return;
  if (!isOriginAllowed(req.headers.origin)) throw new HttpError(403, 'Užklausa atmesta.');
  if (req.headers['x-requested-with'] !== 'nesimokau') throw new HttpError(403, 'Užklausa atmesta.');
}

export async function readJson(req) {
  if (req.body !== undefined && req.body !== null && typeof req.body === 'object' && !Buffer.isBuffer(req.body)) return req.body;

  const chunks = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > MAX_BODY_BYTES) throw new HttpError(413, 'Per didelė užklausa.');
    chunks.push(chunk);
  }

  if (size === 0) return {};
  try {
    return JSON.parse(Buffer.concat(chunks).toString('utf8'));
  } catch {
    throw new HttpError(400, 'Neteisingas užklausos formatas.');
  }
}

export function sendJson(res, status, body) {
  res.statusCode = status;
  res.setHeader('Content-Type', 'application/json; charset=utf-8');
  res.end(JSON.stringify(body));
}

export function parseCookies(req) {
  const out = {};
  for (const part of String(req.headers.cookie ?? '').split(';')) {
    const index = part.indexOf('=');
    if (index > 0) out[part.slice(0, index).trim()] = decodeURIComponent(part.slice(index + 1).trim());
  }
  return out;
}

export function setSessionCookie(res, token, maxAgeSeconds) {
  const cfg = getConfig();
  const flags = ['Path=/', 'HttpOnly', 'SameSite=Strict', `Max-Age=${maxAgeSeconds}`];
  if (cfg.isProd) flags.push('Secure');
  res.setHeader('Set-Cookie', `${cfg.cookieName}=${token}; ${flags.join('; ')}`);
}

export const clearSessionCookie = (res) => setSessionCookie(res, '', 0);

/**
 * Persistent fixed-window rate limiter backed by Supabase (serverless functions have no shared memory).
 * Throws HTTP 429 when the limit is exceeded.
 */
export async function rateLimit(key, max, windowSeconds) {
  const hashed = hmac(key, 'rl');
  const now = Date.now();
  const result = await db.rpc('consume_rate_limit', {
    p_key: hashed,
    p_now_ms: now,
    p_window_ms: windowSeconds * 1000,
  });
  const row = Array.isArray(result) ? result[0] : result;
  const count = Number(row?.hit_count);
  if (count > max) {
    const retry = Math.max(1, Math.ceil((Number(row.reset_at_ms) - now) / 1000));
    throw new HttpError(429, `Per daug bandymų. Pabandyk po ${Math.ceil(retry / 60)} min.`, { retryAfter: retry });
  }
}

export async function audit(req, uid, type, detail = '') {
  try {
    await db.create('auth_events', { uid: uid ?? '', type, ip_hash: ipHash(req), detail: String(detail).slice(0, 200), at_ms: Date.now() });
  } catch (error) {
    console.error('Failed to write auth audit event:', error instanceof Error ? error.message : 'Unknown error');
  }
}
