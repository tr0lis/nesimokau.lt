/// <reference path="../pb_data/types.d.ts" />
// Creates all collections used by the nesimokau.lt API.
// All API rules are left null => only superusers (the API server) can access the data.

migrate((app) => {
  const text = (name, extra = {}) => ({ type: 'text', name, max: 500, ...extra });
  const num = (name) => ({ type: 'number', name, onlyInt: true });
  const bool = (name) => ({ type: 'bool', name });
  const json = (name, maxSize = 2000000) => ({ type: 'json', name, maxSize });
  const stamps = [
    { type: 'autodate', name: 'created', onCreate: true, onUpdate: false },
    { type: 'autodate', name: 'updated', onCreate: true, onUpdate: true },
  ];

  const make = (name, fields, indexes = []) => {
    const c = new Collection({ type: 'base', name, fields: [...fields, ...stamps], indexes });
    app.save(c);
  };

  make('players', [
    text('uid', { required: true, max: 64 }),
    text('username', { required: true, max: 40 }),
    text('username_lower', { required: true, max: 40 }),
    text('password_hash', { required: true, max: 400 }),
    text('avatar_id', { max: 40 }),
    text('class_group', { max: 10 }),
    text('totp_secret_enc', { max: 400 }),
    text('totp_pending_enc', { max: 400 }),
    bool('totp_enabled'),
    num('totp_last_step'),
    json('backup_codes', 20000),
    num('failed_logins'),
    num('locked_until_ms'),
    num('last_login_ms'),
  ], [
    'CREATE UNIQUE INDEX idx_players_uid ON players (uid)',
    'CREATE UNIQUE INDEX idx_players_username_lower ON players (username_lower)',
  ]);

  make('sessions', [
    text('token_hash', { required: true, max: 128 }),
    text('uid', { required: true, max: 64 }),
    num('created_ms'), num('last_seen_ms'), num('expires_ms'),
    text('ip_hash', { max: 64 }),
    text('user_agent', { max: 200 }),
  ], [
    'CREATE UNIQUE INDEX idx_sessions_token ON sessions (token_hash)',
    'CREATE INDEX idx_sessions_uid ON sessions (uid)',
  ]);

  make('user_progress', [
    text('uid', { required: true, max: 64 }),
    text('name', { max: 60 }),
    text('avatar_id', { max: 40 }),
    text('avatar_background', { max: 40 }),
    text('class_group', { max: 10 }),
    text('theme', { max: 20 }),
    num('level'), num('xp'), num('coins'), num('streak'),
    num('best_score'), num('games_completed'), num('score'),
    json('unlocked_avatar_ids'), json('unlocked_achievement_ids'), json('bookmarked_game_ids'),
    json('practice_mistake_counts'), json('solved_question_ids_by_game'),
    text('hint_purchase_date_utc', { max: 40 }),
    num('hint_purchases_today'),
    text('custom_avatar_data_url', { max: 400000 }),
    json('diagnostic_attempts'), json('recent_results'),
  ], [
    'CREATE UNIQUE INDEX idx_progress_uid ON user_progress (uid)',
    'CREATE INDEX idx_progress_score ON user_progress (score)',
  ]);

  make('activity_log', [
    text('uid', { max: 64 }), text('username', { max: 40 }),
    text('event_type', { required: true, max: 60 }),
    text('subject', { max: 80 }), text('game_id', { max: 80 }), text('game_title', { max: 120 }),
    text('class_group', { max: 10 }),
    num('score'), num('accuracy'), num('correct_answers'), num('incorrect_answers'),
    num('xp_earned'), num('coins_earned'), num('coins_spent'), num('hint_cost'),
    bool('is_correct'),
    text('question_id', { max: 80 }), text('mistake_type', { max: 80 }),
    json('payload', 8000),
  ], ['CREATE INDEX idx_activity_uid ON activity_log (uid)']);

  make('rate_limits', [
    text('key', { required: true, max: 128 }),
    num('count'), num('reset_ms'),
  ], ['CREATE UNIQUE INDEX idx_rate_key ON rate_limits (key)']);

  make('auth_events', [
    text('uid', { max: 64 }), text('type', { max: 60 }),
    text('ip_hash', { max: 64 }), text('detail', { max: 200 }),
    num('at_ms'),
  ]);
}, (app) => {
  for (const name of ['auth_events', 'rate_limits', 'activity_log', 'user_progress', 'sessions', 'players']) {
    try { app.delete(app.findCollectionByNameOrId(name)); } catch (_) { /* ignore */ }
  }
});
