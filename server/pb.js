import { getConfig } from './config.js';

let adminToken = null;
let adminTokenExp = 0;

function jwtExpMs(token) {
  try {
    const payload = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString('utf8'));
    return payload.exp * 1000;
  } catch {
    return Date.now() + 10 * 60 * 1000;
  }
}

function rawFetch(path, options = {}) {
  const cfg = getConfig();
  return fetch(`${cfg.pbUrl}${path}`, { ...options, signal: AbortSignal.timeout(8000) });
}

async function getAdminToken(force = false) {
  if (!force && adminToken && Date.now() < adminTokenExp - 60_000) return adminToken;

  const cfg = getConfig();
  const response = await rawFetch('/api/collections/_superusers/auth-with-password', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ identity: cfg.pbEmail, password: cfg.pbPassword }),
  });

  if (!response.ok) throw new Error(`PocketBase admin auth failed (${response.status})`);

  const data = await response.json();
  adminToken = data.token;
  adminTokenExp = jwtExpMs(adminToken);
  return adminToken;
}

async function request(path, { method = 'GET', body } = {}, retried = false) {
  const token = await getAdminToken(retried);
  const response = await rawFetch(path, {
    method,
    headers: {
      authorization: token,
      ...(body === undefined ? {} : { 'content-type': 'application/json' }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (response.status === 401 && !retried) return request(path, { method, body }, true);
  if (response.status === 204) return null;

  const text = await response.text();
  const data = text ? JSON.parse(text) : null;

  if (!response.ok) {
    const error = new Error(data?.message ?? `PocketBase error ${response.status}`);
    error.status = response.status;
    error.data = data?.data;
    throw error;
  }

  return data;
}

// PocketBase filter strings use single quotes; escape backslashes and quotes in user data.
export function q(value) {
  return `'${String(value).replace(/\\/g, '\\\\').replace(/'/g, "\\'")}'`;
}

export const pb = {
  async list(collection, { filter, sort, perPage = 50, page = 1, fields } = {}) {
    const params = new URLSearchParams({ perPage: String(perPage), page: String(page), skipTotal: '1' });
    if (filter) params.set('filter', filter);
    if (sort) params.set('sort', sort);
    if (fields) params.set('fields', fields);
    const data = await request(`/api/collections/${collection}/records?${params}`);
    return data.items;
  },

  async first(collection, filter, options = {}) {
    const items = await pb.list(collection, { ...options, filter, perPage: 1 });
    return items[0] ?? null;
  },

  create: (collection, body) => request(`/api/collections/${collection}/records`, { method: 'POST', body }),
  update: (collection, id, body) => request(`/api/collections/${collection}/records/${id}`, { method: 'PATCH', body }),
  remove: (collection, id) => request(`/api/collections/${collection}/records/${id}`, { method: 'DELETE' }),
};
