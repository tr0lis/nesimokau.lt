-- nesimokau.lt full Supabase bootstrap schema
-- Run in Supabase SQL Editor

create extension if not exists pgcrypto;

-- =========================
-- Accounts
-- =========================
create table if not exists public.player_accounts (
  id uuid primary key default gen_random_uuid(),
  username text not null unique check (username ~ '^[A-Za-z0-9]{3,24}$'),
  password_hash text not null,
  avatar_id text not null default 'a1',
  class_group text not null default '5-6' check (class_group in ('1-2','3-4','5-6','7-8','9-10','11-12')),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create index if not exists idx_player_accounts_username
  on public.player_accounts (username);

create index if not exists idx_player_accounts_updated_at
  on public.player_accounts (updated_at desc);

-- =========================
-- Leaderboard
-- =========================
create table if not exists public.leaderboard (
  player_id text primary key,
  nickname text not null,
  avatar text not null,
  country_flag text not null,
  score int not null default 0,
  updated_at timestamptz not null default now()
);

create index if not exists idx_leaderboard_score
  on public.leaderboard (score desc, updated_at asc);

create index if not exists idx_leaderboard_nickname
  on public.leaderboard (nickname);

-- =========================
-- Activity timeline / analytics
-- =========================
create table if not exists public.user_activity_log (
  id uuid primary key default gen_random_uuid(),
  created_at timestamptz not null default now(),
  user_id text,
  username text,
  event_type text not null,
  subject text,
  game_id text,
  game_title text,
  class_group text,
  score int,
  accuracy int,
  correct_answers int,
  incorrect_answers int,
  xp_earned int,
  coins_earned int,
  coins_spent int,
  hint_cost int,
  is_correct boolean,
  question_id text,
  mistake_type text,
  payload jsonb not null default '{}'::jsonb
);

create index if not exists idx_user_activity_created_at
  on public.user_activity_log (created_at desc);

create index if not exists idx_user_activity_user_time
  on public.user_activity_log (user_id, created_at desc);

create index if not exists idx_user_activity_event_type
  on public.user_activity_log (event_type, created_at desc);

create index if not exists idx_user_activity_subject
  on public.user_activity_log (subject, created_at desc);

create index if not exists idx_user_activity_game
  on public.user_activity_log (game_id, created_at desc);

create index if not exists idx_user_activity_payload_gin
  on public.user_activity_log using gin (payload);

-- =========================
-- Updated_at trigger helper
-- =========================
create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

drop trigger if exists trg_player_accounts_set_updated_at on public.player_accounts;
create trigger trg_player_accounts_set_updated_at
before update on public.player_accounts
for each row
execute procedure public.set_updated_at();

drop trigger if exists trg_leaderboard_set_updated_at on public.leaderboard;
create trigger trg_leaderboard_set_updated_at
before update on public.leaderboard
for each row
execute procedure public.set_updated_at();

-- =========================
-- RLS
-- =========================
alter table public.player_accounts enable row level security;
alter table public.leaderboard enable row level security;
alter table public.user_activity_log enable row level security;

-- Drop old policies if rerun

drop policy if exists "anon read player_accounts" on public.player_accounts;
drop policy if exists "anon insert player_accounts" on public.player_accounts;
drop policy if exists "anon update player_accounts" on public.player_accounts;

drop policy if exists "public read leaderboard" on public.leaderboard;
drop policy if exists "public insert leaderboard" on public.leaderboard;
drop policy if exists "public update leaderboard" on public.leaderboard;

drop policy if exists "anon read user_activity_log" on public.user_activity_log;
drop policy if exists "anon insert user_activity_log" on public.user_activity_log;

-- NOTE: These anon policies are for current WASM/testing architecture.
-- For production security, move auth/updates to server-side API and tighten policies.

create policy "anon read player_accounts"
on public.player_accounts
for select
to anon
using (true);

create policy "anon insert player_accounts"
on public.player_accounts
for insert
to anon
with check (true);

create policy "anon update player_accounts"
on public.player_accounts
for update
to anon
using (true)
with check (true);

create policy "public read leaderboard"
on public.leaderboard
for select
to anon
using (true);

create policy "public insert leaderboard"
on public.leaderboard
for insert
to anon
with check (true);

create policy "public update leaderboard"
on public.leaderboard
for update
to anon
using (true)
with check (true);

create policy "anon read user_activity_log"
on public.user_activity_log
for select
to anon
using (true);

create policy "anon insert user_activity_log"
on public.user_activity_log
for insert
to anon
with check (true);

-- =========================
-- Convenience views
-- =========================
create or replace view public.v_user_timeline as
select
  created_at,
  user_id,
  username,
  event_type,
  subject,
  game_id,
  game_title,
  class_group,
  score,
  accuracy,
  correct_answers,
  incorrect_answers,
  xp_earned,
  coins_earned,
  coins_spent,
  hint_cost,
  is_correct,
  question_id,
  mistake_type,
  payload
from public.user_activity_log
order by created_at desc;

create or replace view public.v_user_error_summary as
select
  coalesce(username, user_id, 'unknown') as user_key,
  subject,
  game_id,
  count(*) filter (where incorrect_answers is not null and incorrect_answers > 0) as sessions_with_errors,
  sum(coalesce(incorrect_answers, 0)) as total_incorrect,
  sum(coalesce(correct_answers, 0)) as total_correct,
  round(avg(nullif(accuracy, 0))::numeric, 2) as avg_accuracy,
  min(created_at) as first_seen,
  max(created_at) as last_seen
from public.user_activity_log
where event_type in ('game_result_saved', 'diagnostic_saved')
group by 1,2,3;

create or replace view public.v_daily_activity as
select
  date_trunc('day', created_at) as day,
  count(*) as events,
  count(distinct coalesce(user_id, username)) as active_users,
  sum(coalesce(xp_earned, 0)) as total_xp,
  sum(coalesce(coins_earned, 0)) as total_coins_earned,
  sum(coalesce(coins_spent, 0)) as total_coins_spent
from public.user_activity_log
group by 1
order by day desc;

-- =========================
-- Example useful queries
-- =========================
-- 1) Full timeline for one user:
-- select * from public.v_user_timeline where username = 'tr0lis' order by created_at desc;

-- 2) Most frequent mistakes by subject:
-- select subject, count(*) as wrong_events
-- from public.user_activity_log
-- where event_type = 'answer_submitted' and is_correct = false
-- group by subject
-- order by wrong_events desc;

-- 3) Latest 50 diagnostic events:
-- select * from public.user_activity_log
-- where event_type = 'diagnostic_saved'
-- order by created_at desc
-- limit 50;
