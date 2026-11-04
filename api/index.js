import { handle } from '../server/router.js';

export default function apiHandler(req, res) {
  const url = new URL(req.url, 'http://localhost');
  const route = url.searchParams.get('__path');

  if (route !== null) {
    if (!/^[a-zA-Z0-9_-]+(?:\/[a-zA-Z0-9_-]+)*$/.test(route)) {
      res.statusCode = 400;
      return res.end();
    }

    url.searchParams.delete('__path');
    req.url = `/api/${route}${url.search}`;
  }

  return handle(req, res);
}
