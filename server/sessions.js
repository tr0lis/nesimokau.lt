import { getConfig } from './config.js';
import { db, q } from './supabase.js';
import { sha256, randomToken } from './crypto.js';
import { HttpError, ipHash, parseCookies, setSessionCookie, clearSessionCookie } from './http.js';

const DAY = 24 * 60 * 60 * 1000;

export async function createSession(req, res, uid) {
  const cfg = getConfig();
  const token = randomToken(32);
  const now = Date.now();

  await db.create('sessions', {
    token_hash: sha256(token),
    uid,
    created_ms: now,
    last_seen_ms: now,
    expires_ms: now + cfg.sessionDays * DAY,
    ip_hash: ipHash(req),
    user_agent: String(req.headers['user-agent'] ?? '').slice(0, 200),
  });

  setSessionCookie(res, token, cfg.sessionDays * 24 * 60 * 60);
}

export async function getSession(req, res) {
  const cfg = getConfig();
  const token = parseCookies(req)[cfg.cookieName];
  if (!token || token.length > 128) return null;

  const row = await db.first('sessions', `token_hash = ${q(sha256(token))}`);
  if (!row) return null;

  const now = Date.now();
  if (row.expires_ms <= now || row.created_ms + cfg.sessionMaxDays * DAY <= now) {
    try {
      await db.remove('sessions', row.id);
    } catch (error) {
      console.error('Failed to remove expired session:', error instanceof Error ? error.message : 'Unknown error');
    }
    return null;
  }

  // Sliding expiration, written at most once an hour.
  if (res && now - row.last_seen_ms > 60 * 60 * 1000) {
    const expires = Math.min(now + cfg.sessionDays * DAY, row.created_ms + cfg.sessionMaxDays * DAY);
    try {
      await db.update('sessions', row.id, { last_seen_ms: now, expires_ms: expires });
      setSessionCookie(res, token, Math.floor((expires - now) / 1000));
    } catch (error) {
      console.error('Failed to refresh session:', error instanceof Error ? error.message : 'Unknown error');
    }
  }

  return row;
}

export async function requirePlayer(req, res) {
  const session = await getSession(req, res);
  if (!session) throw new HttpError(401, 'Reikia prisijungti.');

  const player = await db.first('players', `uid = ${q(session.uid)}`);
  if (!player) throw new HttpError(401, 'Reikia prisijungti.');

  return { session, player };
}

export async function destroySession(req, res) {
  const session = await getSession(req, null);
  if (session) await db.remove('sessions', session.id);
  clearSessionCookie(res);
}

export async function revokeAllSessions(uid, exceptId = null) {
  const rows = await db.list('sessions', { filter: `uid = ${q(uid)}`, perPage: 200 });
  await Promise.all(rows.filter((r) => r.id !== exceptId).map((r) => db.remove('sessions', r.id)));
}
