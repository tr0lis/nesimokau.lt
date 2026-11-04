import { getConfig } from './config.js';

const tables = {
  players: { name: 'player_accounts', fields: { uid: 'id' } },
  sessions: { name: 'auth_sessions', fields: {} },
  user_progress: { name: 'user_progress', fields: { uid: 'user_id', updated: 'updated_at' }, id: 'user_id' },
  activity_log: { name: 'user_activity_log', fields: { uid: 'user_id' } },
  rate_limits: { name: 'rate_limits', fields: {} },
  auth_events: { name: 'auth_events', fields: {} },
  leaderboard: { name: 'leaderboard', fields: {} },
};

function mapping(collection) {
  const result = tables[collection];
  if (!result) throw new Error(`Unknown Supabase table mapping: ${collection}`);
  return result;
}

function mapIn(collection, row) {
  const { fields } = mapping(collection);
  const result = {};
  for (const [key, value] of Object.entries(row ?? {})) {
    if (key === 'username_lower' && collection === 'players') continue;
    result[fields[key] ?? key] = value;
  }
  return result;
}

function mapOut(collection, row) {
  if (!row) return null;
  const table = mapping(collection);
  const reverse = Object.fromEntries(Object.entries(table.fields).map(([from, to]) => [to, from]));
  const result = {};
  for (const [key, value] of Object.entries(row ?? {})) result[reverse[key] ?? key] = value;
  if (collection === 'players') {
    result.uid = row.id;
    result.username_lower = String(row.username ?? '').toLowerCase();
  }
  if (collection === 'user_progress') result.id = row.user_id;
  return result;
}

function decodeQuoted(value) {
  return value.replace(/\\(['\\])/g, '$1');
}

function decodeFilter(filter) {
  if (!filter) return [];
  const match = filter.match(/^([a-z_]+) = '((?:\\.|[^'])*)'$/);
  if (!match) throw new Error('Unsupported database filter.');
  return [[match[1], decodeQuoted(match[2])]];
}

function databaseField(collection, field) {
  return mapping(collection).fields[field] ?? field;
}

function supabaseAuthHeaders(key) {
  const headers = { apikey: key };
  if (!key.startsWith('sb_secret_')) headers.authorization = `Bearer ${key}`;
  return headers;
}

async function request(path, { method = 'GET', body, headers = {} } = {}) {
  const cfg = getConfig();
  const response = await fetch(`${cfg.supabaseUrl}/rest/v1/${path}`, {
    method,
    headers: {
      ...supabaseAuthHeaders(cfg.supabaseServiceRoleKey),
      ...(body === undefined ? {} : { 'content-type': 'application/json' }),
      ...headers,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(8000),
  });

  if (response.status === 204) return null;
  const text = await response.text();
  let data;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  if (!response.ok) {
    const error = new Error(data?.message ?? data?.hint ?? `Supabase error ${response.status}`);
    error.status = response.status;
    error.data = data;
    throw error;
  }
  return data;
}

export function q(value) {
  return `'${String(value).replace(/\\/g, '\\\\').replace(/'/g, "\\'")}'`;
}

export const db = {
  async list(collection, { filter, sort, perPage = 50, page = 1, fields } = {}) {
    const table = mapping(collection);
    const params = new URLSearchParams({ select: fields ? fields.split(',').map((field) => databaseField(collection, field.trim())).join(',') : '*' });
    for (const [field, value] of decodeFilter(filter)) params.set(databaseField(collection, field), `eq.${value}`);
    if (sort) {
      params.set('order', sort.split(',').map((part) => {
        const descending = part.startsWith('-');
        const field = databaseField(collection, part.replace(/^-/, ''));
        return `${field}.${descending ? 'desc' : 'asc'}`;
      }).join(','));
    }

    const offset = Math.max(0, (page - 1) * perPage);
    const rows = await request(`${table.name}?${params}`, {
      headers: { range: `${offset}-${offset + perPage - 1}`, 'range-unit': 'items' },
    });
    return (rows ?? []).map((row) => mapOut(collection, row));
  },

  async first(collection, filter, options = {}) {
    return (await db.list(collection, { ...options, filter, perPage: 1 }))[0] ?? null;
  },

  async create(collection, body) {
    const table = mapping(collection);
    const row = await request(table.name, {
      method: 'POST',
      body: mapIn(collection, body),
      headers: { Prefer: 'return=representation' },
    });
    return mapOut(collection, row?.[0]);
  },

  async update(collection, id, body) {
    const table = mapping(collection);
    const params = new URLSearchParams({ [table.id ?? 'id']: `eq.${id}`, select: '*' });
    const rows = await request(`${table.name}?${params}`, {
      method: 'PATCH',
      body: mapIn(collection, body),
      headers: { Prefer: 'return=representation' },
    });
    return mapOut(collection, rows?.[0]);
  },

  async remove(collection, id) {
    const table = mapping(collection);
    const params = new URLSearchParams({ [table.id ?? 'id']: `eq.${id}` });
    return request(`${table.name}?${params}`, { method: 'DELETE' });
  },

  async rpc(name, args) {
    const cfg = getConfig();
    const response = await fetch(`${cfg.supabaseUrl}/rest/v1/rpc/${name}`, {
      method: 'POST',
      headers: {
        ...supabaseAuthHeaders(cfg.supabaseServiceRoleKey),
        'content-type': 'application/json',
      },
      body: JSON.stringify(args),
      signal: AbortSignal.timeout(8000),
    });
    const text = await response.text();
    const data = text ? JSON.parse(text) : null;
    if (!response.ok) {
      const error = new Error(data?.message ?? `Supabase RPC error ${response.status}`);
      error.status = response.status;
      error.data = data;
      throw error;
    }
    return data;
  },
};
