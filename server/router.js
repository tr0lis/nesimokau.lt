import { getConfig } from './config.js';
import { db } from './supabase.js';
import { HttpError, applyBaseHeaders, assertSameSiteRequest, sendJson } from './http.js';
import { authRoutes } from './routes/auth.js';
import { dataRoutes } from './routes/data.js';

const routes = { ...authRoutes, ...dataRoutes };

// Reports only variable names and failure categories, never values.
async function health(res) {
  const result = { config: 'ok', database: 'unknown' };
  const missing = ['SUPABASE_URL', 'SUPABASE_SERVICE_ROLE_KEY', 'SESSION_SECRET', 'TOTP_ENC_KEY', 'ALLOWED_ORIGINS']
    .filter((name) => !process.env[name]);
  if (missing.length) result.missing = missing;
  try {
    getConfig();
  } catch (error) {
    result.config = 'invalid';
    result.reason = String(error.message).replace(/^Missing environment variable: /, 'missing ');
    return sendJson(res, 503, result);
  }
  try {
    await db.list('players', { perPage: 1, fields: 'username' });
    result.database = 'ok';
  } catch (error) {
    result.database = error.status === 401 || error.status === 403 || /permission denied/i.test(error.message)
      ? 'permission_denied (run database/supabase-schema.sql and use the service_role/sb_secret key)'
      : /relation|does not exist|schema cache/i.test(error.message) ? 'tables_missing (run database/supabase-schema.sql)' : `error_${error.status ?? 'network'}`;
    return sendJson(res, 503, result);
  }
  return sendJson(res, 200, result);
}

export async function handle(req, res) {
  try {
    applyBaseHeaders(req, res);

    if (req.method === 'OPTIONS') {
      res.statusCode = 204;
      return res.end();
    }

    const path = new URL(req.url, 'http://x').pathname.replace(/^\/api/, '').replace(/\/+$/, '') || '/';
    if (req.method === 'GET' && path === '/health') return await health(res);
    const route = routes[`${req.method} ${path}`];
    if (!route) throw new HttpError(404, 'Nerasta.');

    assertSameSiteRequest(req);
    await route(req, res);
  } catch (error) {
    if (error instanceof HttpError) {
      if (error.extra?.retryAfter) res.setHeader('Retry-After', String(error.extra.retryAfter));
      return sendJson(res, error.status, { error: error.message });
    }
    console.error('API error:', error);
    sendJson(res, 500, { error: 'Serverio klaida. Pabandyk vėliau.' });
  }
}
