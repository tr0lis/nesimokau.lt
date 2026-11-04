import { HttpError, applyBaseHeaders, assertSameSiteRequest, sendJson } from './http.js';
import { authRoutes } from './routes/auth.js';
import { dataRoutes } from './routes/data.js';

const routes = { ...authRoutes, ...dataRoutes };

export async function handle(req, res) {
  try {
    applyBaseHeaders(req, res);

    if (req.method === 'OPTIONS') {
      res.statusCode = 204;
      return res.end();
    }

    const path = new URL(req.url, 'http://x').pathname.replace(/^\/api/, '').replace(/\/+$/, '') || '/';
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
