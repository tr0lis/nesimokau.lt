const isProd = process.env.NODE_ENV === 'production' || Boolean(process.env.VERCEL);

function required(name) {
  const value = process.env[name];
  if (!value) throw new Error(`Missing environment variable: ${name}`);
  return value;
}

let cached;

export function getConfig() {
  if (cached) return cached;

  const sessionSecret = required('SESSION_SECRET');
  if (sessionSecret.length < 32) throw new Error('SESSION_SECRET must be at least 32 characters');

  const encKey = Buffer.from(required('TOTP_ENC_KEY'), 'base64');
  if (encKey.length !== 32) throw new Error('TOTP_ENC_KEY must be 32 random bytes encoded as base64');
  const supabaseUrl = required('SUPABASE_URL').replace(/\/+$/, '');
  if (isProd && new URL(supabaseUrl).protocol !== 'https:') throw new Error('SUPABASE_URL must use HTTPS in production');

  cached = {
    isProd,
    supabaseUrl,
    supabaseServiceRoleKey: required('SUPABASE_SERVICE_ROLE_KEY'),
    sessionSecret,
    encKey,
    allowedOrigins: (process.env.ALLOWED_ORIGINS ?? '')
      .split(',')
      .map((x) => x.trim().replace(/\/+$/, ''))
      .filter(Boolean),
    cookieName: isProd ? '__Host-nsm_session' : 'nsm_session',
    sessionDays: 7,
    sessionMaxDays: 30,
    issuer: process.env.TOTP_ISSUER ?? 'nesimokau.lt',
  };

  return cached;
}

export function isOriginAllowed(origin) {
  if (!origin) return false;
  const cfg = getConfig();
  const clean = origin.replace(/\/+$/, '');
  if (cfg.allowedOrigins.includes(clean)) return true;
  if (!cfg.isProd && /^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/.test(clean)) return true;
  return false;
}
