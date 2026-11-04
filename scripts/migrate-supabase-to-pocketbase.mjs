// Imports accounts + progress from the old Supabase project into PocketBase.
// Usage (PowerShell):
//   $env:SUPABASE_URL="https://xxxx.supabase.co"; $env:SUPABASE_KEY="<service_role or old publishable key>"
//   $env:POCKETBASE_URL="https://pb.tavodomenas.lt"; $env:PB_ADMIN_EMAIL="..."; $env:PB_ADMIN_PASSWORD="..."
//   node scripts/migrate-supabase-to-pocketbase.mjs
// Old PBKDF2 password hashes are kept; they are upgraded to scrypt when each user next logs in.

const need = (n) => {
  const v = process.env[n];
  if (!v) { console.error(`Trūksta aplinkos kintamojo ${n}`); process.exit(1); }
  return v.replace(/\/+$/, '');
};

const SB = need('SUPABASE_URL');
const SB_KEY = need('SUPABASE_KEY');
const PB = need('POCKETBASE_URL');

async function sb(path) {
  const rows = [];
  for (let from = 0; ; from += 1000) {
    const r = await fetch(`${SB}/rest/v1/${path}`, {
      headers: { apikey: SB_KEY, authorization: `Bearer ${SB_KEY}`, range: `${from}-${from + 999}` },
    });
    if (!r.ok) throw new Error(`Supabase ${path}: ${r.status} ${await r.text()}`);
    const page = await r.json();
    rows.push(...page);
    if (page.length < 1000) break;
  }
  return rows;
}

const auth = await fetch(`${PB}/api/collections/_superusers/auth-with-password`, {
  method: 'POST',
  headers: { 'content-type': 'application/json' },
  body: JSON.stringify({ identity: need('PB_ADMIN_EMAIL'), password: process.env.PB_ADMIN_PASSWORD }),
});
if (!auth.ok) throw new Error(`PocketBase prisijungimas nepavyko: ${auth.status}`);
const { token } = await auth.json();

async function pb(path, method, body) {
  const r = await fetch(`${PB}${path}`, {
    method,
    headers: { authorization: token, 'content-type': 'application/json' },
    body: JSON.stringify(body),
  });
  return { ok: r.ok, status: r.status, text: r.ok ? '' : await r.text() };
}

const accounts = await sb('player_accounts?select=*');
const progress = await sb('user_progress?select=*');
console.log(`Rasta paskyrų: ${accounts.length}, pažangos įrašų: ${progress.length}`);

let a = 0, p = 0, skipped = 0;
for (const acc of accounts) {
  const res = await pb('/api/collections/players/records', 'POST', {
    uid: acc.id,
    username: acc.username,
    username_lower: String(acc.username).toLowerCase(),
    password_hash: acc.password_hash,
    avatar_id: acc.avatar_id,
    class_group: acc.class_group,
    totp_enabled: false,
    backup_codes: [],
    failed_logins: 0,
    locked_until_ms: 0,
    last_login_ms: 0,
  });
  if (res.ok) a++; else { skipped++; console.warn(`Praleista ${acc.username}: ${res.status} ${res.text.slice(0, 120)}`); }
}

const known = new Set(accounts.map((x) => x.id));
for (const row of progress) {
  if (!known.has(row.user_id)) continue;
  const { user_id, created_at, created_at_lt, updated_at, updated_at_lt, ...rest } = row;
  const res = await pb('/api/collections/user_progress/records', 'POST', {
    ...rest,
    uid: user_id,
    score: Number(row.best_score) || 0,
  });
  if (res.ok) p++; else console.warn(`Pažanga ${user_id}: ${res.status} ${res.text.slice(0, 120)}`);
}

console.log(`Baigta. Paskyros: ${a}, pažanga: ${p}, praleista: ${skipped}`);
