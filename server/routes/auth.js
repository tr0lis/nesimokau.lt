import crypto from 'node:crypto';
import QRCode from 'qrcode';
import { db, q } from '../supabase.js';
import {
  hashPassword, verifyPassword, burnPasswordCheck, encrypt, decrypt, signTicket, verifyTicket,
  generateTotpSecret, verifyTotp, buildOtpAuthUri, generateBackupCodes, hashBackupCode, normalizeBackupCode, safeEqual,
} from '../crypto.js';
import { HttpError, clientIp, rateLimit, audit, sendJson, readJson } from '../http.js';
import { createSession, getSession, requirePlayer, destroySession, revokeAllSessions } from '../sessions.js';

const CLASS_GROUPS = ['1-2', '3-4', '5-6', '7-8', '9-10', '11-12'];
const USERNAME = /^[A-Za-z0-9]{3,24}$/;
const AVATAR = /^[a-z0-9_-]{1,32}$/i;
const LOCK_THRESHOLD = 5;
const LOCK_MS = 15 * 60 * 1000;

const publicUser = (p) => ({
  id: p.uid,
  username: p.username,
  avatarId: p.avatar_id || 'a1',
  classGroup: p.class_group || '5-6',
  totpEnabled: Boolean(p.totp_enabled),
});

function str(value, max = 256) {
  return typeof value === 'string' ? value.slice(0, max) : '';
}

function validatePassword(password, username) {
  if (password.length < 8) return 'Slaptažodis turi būti bent 8 simbolių.';
  if (password.length > 128) return 'Slaptažodis per ilgas.';
  if (!/\p{Lu}/u.test(password) || !/\d/.test(password) || !/[^\p{L}\d]/u.test(password)) {
    return 'Slaptažodis per silpnas. Reikia: 8+ simbolių, didžiosios raidės, skaičiaus ir simbolio.';
  }
  if (username && password.toLowerCase().includes(username.toLowerCase())) return 'Slaptažodyje negali būti slapyvardžio.';
  return null;
}

async function reauthenticate(player, password) {
  await rateLimit(`reauth:${player.uid}`, 10, 900);
  const result = await verifyPassword(str(password, 128), player.password_hash);
  if (!result.ok) throw new HttpError(403, 'Neteisingas slaptažodis.');
}

/** Verifies a TOTP code (replay-protected) or a one-time backup code, persisting the consumed state. */
async function verifySecondFactor(player, rawCode) {
  const code = str(rawCode, 32).trim();
  if (!player.totp_secret_enc) return false;

  if (/^\d{6}$/.test(code)) {
    const step = verifyTotp(decrypt(player.totp_secret_enc), code, Number(player.totp_last_step || 0));
    if (step === null) return false;
    await db.update('players', player.id, { totp_last_step: step });
    return true;
  }

  const normalized = normalizeBackupCode(code);
  if (normalized.length !== 10) return false;

  const hash = hashBackupCode(normalized);
  const codes = Array.isArray(player.backup_codes) ? player.backup_codes : [];
  const match = codes.find((c) => safeEqual(c, hash));
  if (!match) return false;

  await db.update('players', player.id, { backup_codes: codes.filter((c) => c !== match) });
  return true;
}

function qrFor(uri) {
  const qr = QRCode.create(uri, { errorCorrectionLevel: 'H' });
  const size = qr.modules.size;
  const rows = [];
  for (let y = 0; y < size; y++) {
    let row = '';
    for (let x = 0; x < size; x++) row += qr.modules.data[y * size + x] ? '1' : '0';
    rows.push(row);
  }
  return { size, rows };
}

export const authRoutes = {
  async 'POST /auth/register'(req, res) {
    const body = await readJson(req);
    const username = str(body.username, 40).trim();
    const password = str(body.password, 200);
    const avatarId = AVATAR.test(str(body.avatarId, 40)) ? body.avatarId : 'a1';
    const classGroup = CLASS_GROUPS.includes(body.classGroup) ? body.classGroup : '5-6';

    await rateLimit(`register:${clientIp(req)}`, 6, 3600);

    if (!USERNAME.test(username)) throw new HttpError(400, 'Slapyvardį turi sudaryti 3–24 raidės arba skaičiai (be tarpų ir simbolių).');
    const weak = validatePassword(password, username);
    if (weak) throw new HttpError(400, weak);

    if (await db.first('players', `username_lower = ${q(username.toLowerCase())}`)) {
      throw new HttpError(409, 'Toks slapyvardis jau užimtas.');
    }

    const uid = crypto.randomUUID();
    try {
      await db.create('players', {
        uid,
        username,
        username_lower: username.toLowerCase(),
        password_hash: await hashPassword(password),
        avatar_id: avatarId,
        class_group: classGroup,
        totp_enabled: false,
        backup_codes: [],
        failed_logins: 0,
        locked_until_ms: 0,
        last_login_ms: Date.now(),
      });
    } catch (error) {
      if (error.status === 400) throw new HttpError(409, 'Toks slapyvardis jau užimtas.');
      throw error;
    }

    await createSession(req, res, uid);
    await audit(req, uid, 'register');
    sendJson(res, 201, { user: { id: uid, username, avatarId, classGroup, totpEnabled: false } });
  },

  async 'POST /auth/login'(req, res) {
    const body = await readJson(req);
    const username = str(body.username, 40).trim();
    const password = str(body.password, 200);

    await rateLimit(`login-ip:${clientIp(req)}`, 30, 600);

    const generic = new HttpError(401, 'Neteisingas slapyvardis arba slaptažodis.');
    if (!USERNAME.test(username) || !password) {
      await burnPasswordCheck(password);
      throw generic;
    }

    const player = await db.first('players', `username_lower = ${q(username.toLowerCase())}`);
    if (!player) {
      await burnPasswordCheck(password);
      throw generic;
    }

    if (Number(player.locked_until_ms) > Date.now()) {
      await audit(req, player.uid, 'login_locked');
      throw new HttpError(429, 'Paskyra laikinai užblokuota dėl per daug bandymų. Pabandyk vėliau.');
    }

    const result = await verifyPassword(password, player.password_hash);
    if (!result.ok) {
      const failed = Number(player.failed_logins || 0) + 1;
      await db.update('players', player.id, {
        failed_logins: failed,
        locked_until_ms: failed >= LOCK_THRESHOLD ? Date.now() + LOCK_MS : 0,
      });
      await audit(req, player.uid, 'login_failed', `n=${failed}`);
      throw generic;
    }

    if (result.needsRehash) {
      await db.update('players', player.id, { password_hash: await hashPassword(password) });
    }

    if (player.totp_enabled) {
      await audit(req, player.uid, 'login_password_ok_mfa_required');
      return sendJson(res, 200, { mfaRequired: true, ticket: signTicket({ uid: player.uid }, 5 * 60 * 1000) });
    }

    await db.update('players', player.id, { failed_logins: 0, locked_until_ms: 0, last_login_ms: Date.now() });
    await createSession(req, res, player.uid);
    await audit(req, player.uid, 'login');
    sendJson(res, 200, { user: publicUser(player) });
  },

  async 'POST /auth/mfa'(req, res) {
    const body = await readJson(req);
    const ticket = verifyTicket(body.ticket);
    if (!ticket) throw new HttpError(401, 'Prisijungimo sesija pasibaigė. Prisijunk iš naujo.');

    await rateLimit(`mfa-ticket:${ticket.jti}`, 5, 300);
    await rateLimit(`mfa-user:${ticket.uid}`, 15, 900);

    const player = await db.first('players', `uid = ${q(ticket.uid)}`);
    if (!player || !player.totp_enabled) throw new HttpError(401, 'Prisijungti nepavyko.');

    if (!(await verifySecondFactor(player, body.code))) {
      await audit(req, player.uid, 'mfa_failed');
      throw new HttpError(401, 'Neteisingas kodas.');
    }

    await db.update('players', player.id, { failed_logins: 0, locked_until_ms: 0, last_login_ms: Date.now() });
    await createSession(req, res, player.uid);
    await audit(req, player.uid, 'login_mfa');
    sendJson(res, 200, { user: publicUser(player) });
  },

  async 'GET /auth/me'(req, res) {
    const session = await getSession(req, res);
    const player = session ? await db.first('players', `uid = ${q(session.uid)}`) : null;
    sendJson(res, 200, player ? { authenticated: true, user: publicUser(player) } : { authenticated: false });
  },

  async 'POST /auth/logout'(req, res) {
    await destroySession(req, res);
    sendJson(res, 200, { ok: true });
  },

  async 'GET /auth/username-available'(req, res) {
    await rateLimit(`uname:${clientIp(req)}`, 60, 600);
    const username = str(new URL(req.url, 'http://x').searchParams.get('u'), 40).trim();
    if (!USERNAME.test(username)) return sendJson(res, 200, { available: false, valid: false });
    const existing = await db.first('players', `username_lower = ${q(username.toLowerCase())}`);
    sendJson(res, 200, { available: !existing, valid: true });
  },

  async 'POST /auth/avatar'(req, res) {
    const { player } = await requirePlayer(req, res);
    const body = await readJson(req);
    const avatarId = str(body.avatarId, 40);
    if (!AVATAR.test(avatarId)) throw new HttpError(400, 'Neteisingas avataras.');
    await db.update('players', player.id, { avatar_id: avatarId });
    sendJson(res, 200, { ok: true });
  },

  async 'POST /auth/2fa/setup'(req, res) {
    const { player } = await requirePlayer(req, res);
    const body = await readJson(req);
    if (player.totp_enabled) throw new HttpError(400, '2FA jau įjungta.');
    await reauthenticate(player, body.password);

    const secret = generateTotpSecret();
    await db.update('players', player.id, { totp_pending_enc: encrypt(secret) });

    const uri = buildOtpAuthUri(secret, player.username);
    sendJson(res, 200, { secret, otpauthUri: uri, qr: qrFor(uri) });
  },

  async 'POST /auth/2fa/enable'(req, res) {
    const { player } = await requirePlayer(req, res);
    const body = await readJson(req);
    if (player.totp_enabled) throw new HttpError(400, '2FA jau įjungta.');
    if (!player.totp_pending_enc) throw new HttpError(400, 'Pirmiausia sugeneruok QR kodą.');

    await rateLimit(`2fa-enable:${player.uid}`, 10, 900);
    const secret = decrypt(player.totp_pending_enc);
    const step = verifyTotp(secret, str(body.code, 12).trim());
    if (step === null) throw new HttpError(400, 'Neteisingas kodas. Patikrink programėlę ir bandyk dar kartą.');

    const backupCodes = generateBackupCodes();
    await db.update('players', player.id, {
      totp_secret_enc: encrypt(secret),
      totp_pending_enc: '',
      totp_enabled: true,
      totp_last_step: step,
      backup_codes: backupCodes.map(hashBackupCode),
    });
    await audit(req, player.uid, '2fa_enabled');
    sendJson(res, 200, { ok: true, backupCodes });
  },

  async 'POST /auth/2fa/disable'(req, res) {
    const { player } = await requirePlayer(req, res);
    const body = await readJson(req);
    if (!player.totp_enabled) throw new HttpError(400, '2FA neįjungta.');
    await reauthenticate(player, body.password);
    if (!(await verifySecondFactor(player, body.code))) throw new HttpError(403, 'Neteisingas kodas.');

    await db.update('players', player.id, { totp_enabled: false, totp_secret_enc: '', totp_pending_enc: '', totp_last_step: 0, backup_codes: [] });
    await audit(req, player.uid, '2fa_disabled');
    sendJson(res, 200, { ok: true });
  },

  async 'POST /auth/2fa/regenerate'(req, res) {
    const { player } = await requirePlayer(req, res);
    const body = await readJson(req);
    if (!player.totp_enabled) throw new HttpError(400, '2FA neįjungta.');
    await reauthenticate(player, body.password);
    if (!(await verifySecondFactor(player, body.code))) throw new HttpError(403, 'Neteisingas kodas.');

    const backupCodes = generateBackupCodes();
    await db.update('players', player.id, { backup_codes: backupCodes.map(hashBackupCode) });
    await audit(req, player.uid, '2fa_codes_regenerated');
    sendJson(res, 200, { backupCodes });
  },

  async 'POST /auth/password'(req, res) {
    const { player, session } = await requirePlayer(req, res);
    const body = await readJson(req);
    await reauthenticate(player, body.currentPassword);
    if (player.totp_enabled && !(await verifySecondFactor(player, body.code))) throw new HttpError(403, 'Neteisingas 2FA kodas.');

    const newPassword = str(body.newPassword, 200);
    const weak = validatePassword(newPassword, player.username);
    if (weak) throw new HttpError(400, weak);

    await db.update('players', player.id, { password_hash: await hashPassword(newPassword) });
    await revokeAllSessions(player.uid, session.id);
    await audit(req, player.uid, 'password_changed');
    sendJson(res, 200, { ok: true });
  },
};
