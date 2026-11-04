import { pb, q } from '../pb.js';
import { HttpError, rateLimit, sendJson, readJson } from '../http.js';
import { requirePlayer } from '../sessions.js';

const CLASS_GROUPS = ['1-2', '3-4', '5-6', '7-8', '9-10', '11-12'];

const int = (value, min, max) => {
  const n = Math.trunc(Number(value));
  return Number.isFinite(n) ? Math.min(max, Math.max(min, n)) : 0;
};
const text = (value, max) => (typeof value === 'string' ? value.slice(0, max) : '');
const list = (value, max, itemMax = 80) =>
  Array.isArray(value) ? value.filter((x) => typeof x === 'string').slice(0, max).map((x) => x.slice(0, itemMax)) : [];

function countMap(value) {
  const out = {};
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    for (const [k, v] of Object.entries(value).slice(0, 2000)) out[k.slice(0, 80)] = int(v, 0, 100000);
  }
  return out;
}

function solvedMap(value) {
  const out = {};
  if (value && typeof value === 'object' && !Array.isArray(value)) {
    for (const [k, v] of Object.entries(value).slice(0, 500)) {
      out[k.slice(0, 80)] = Array.isArray(v) ? v.slice(0, 5000).map((n) => int(n, 0, 1_000_000)) : [];
    }
  }
  return out;
}

const objects = (value, max) =>
  Array.isArray(value) ? value.filter((x) => x && typeof x === 'object' && !Array.isArray(x)).slice(0, max) : [];

function avatarDataUrl(value) {
  if (typeof value !== 'string' || !value) return '';
  return /^data:image\/(png|jpeg|webp);base64,[A-Za-z0-9+/=]+$/.test(value) && value.length <= 400_000 ? value : '';
}

function computeScore(recent, bestScore) {
  const sum = recent.reduce((acc, r) => acc + int(r.score ?? r.Score, 0, 1_000_000), 0);
  return Math.max(sum, bestScore);
}

function toClient(row) {
  return {
    user_id: row.uid,
    updated_at: row.updated ? String(row.updated).replace(' ', 'T') : null,
    name: row.name,
    avatar_id: row.avatar_id,
    avatar_background: row.avatar_background,
    class_group: row.class_group,
    theme: row.theme,
    level: row.level,
    xp: row.xp,
    coins: row.coins,
    streak: row.streak,
    best_score: row.best_score,
    games_completed: row.games_completed,
    unlocked_avatar_ids: row.unlocked_avatar_ids ?? [],
    unlocked_achievement_ids: row.unlocked_achievement_ids ?? [],
    bookmarked_game_ids: row.bookmarked_game_ids ?? [],
    practice_mistake_counts: row.practice_mistake_counts ?? {},
    solved_question_ids_by_game: row.solved_question_ids_by_game ?? {},
    hint_purchase_date_utc: row.hint_purchase_date_utc,
    hint_purchases_today: row.hint_purchases_today,
    custom_avatar_data_url: row.custom_avatar_data_url,
    diagnostic_attempts: row.diagnostic_attempts ?? [],
    recent_results: row.recent_results ?? [],
  };
}

export const dataRoutes = {
  async 'GET /progress'(req, res) {
    const { player } = await requirePlayer(req, res);
    const row = await pb.first('user_progress', `uid = ${q(player.uid)}`);
    sendJson(res, 200, row ? toClient(row) : null);
  },

  async 'PUT /progress'(req, res) {
    const { player } = await requirePlayer(req, res);
    await rateLimit(`progress:${player.uid}`, 120, 60);
    const b = await readJson(req);

    const recent = objects(b.recent_results, 100);
    const bestScore = int(b.best_score, 0, 10_000_000);

    const data = {
      uid: player.uid,
      name: text(b.name, 40) || player.username,
      avatar_id: /^[a-z0-9_-]{1,32}$/i.test(text(b.avatar_id, 40)) ? b.avatar_id : player.avatar_id,
      avatar_background: /^[a-z0-9_-]{1,32}$/i.test(text(b.avatar_background, 40)) ? b.avatar_background : 'bg-violet',
      class_group: CLASS_GROUPS.includes(b.class_group) ? b.class_group : player.class_group,
      theme: ['light', 'dark'].includes(b.theme) ? b.theme : text(b.theme, 20),
      level: int(b.level, 1, 1000),
      xp: int(b.xp, 0, 100_000_000),
      coins: int(b.coins, 0, 100_000_000),
      streak: int(b.streak, 0, 100_000),
      best_score: bestScore,
      games_completed: int(b.games_completed, 0, 10_000_000),
      unlocked_avatar_ids: list(b.unlocked_avatar_ids, 500),
      unlocked_achievement_ids: list(b.unlocked_achievement_ids, 500),
      bookmarked_game_ids: list(b.bookmarked_game_ids, 500),
      practice_mistake_counts: countMap(b.practice_mistake_counts),
      solved_question_ids_by_game: solvedMap(b.solved_question_ids_by_game),
      hint_purchase_date_utc: text(b.hint_purchase_date_utc, 40),
      hint_purchases_today: int(b.hint_purchases_today, 0, 1000),
      custom_avatar_data_url: avatarDataUrl(b.custom_avatar_data_url),
      diagnostic_attempts: objects(b.diagnostic_attempts, 200),
      recent_results: recent,
      score: computeScore(recent, bestScore),
    };

    const existing = await pb.first('user_progress', `uid = ${q(player.uid)}`);
    if (existing) await pb.update('user_progress', existing.id, data);
    else await pb.create('user_progress', data);

    if (data.avatar_id !== player.avatar_id || data.class_group !== player.class_group) {
      await pb.update('players', player.id, { avatar_id: data.avatar_id, class_group: data.class_group });
    }

    sendJson(res, 200, { ok: true });
  },

  async 'GET /leaderboard'(req, res) {
    await requirePlayer(req, res);
    const rows = await pb.list('user_progress', {
      sort: '-score,updated',
      perPage: 200,
      fields: 'uid,name,score,avatar_id,avatar_background',
    });
    sendJson(res, 200, rows.map((r) => ({
      player_id: `user-${r.uid}`,
      nickname: r.name,
      score: r.score ?? 0,
      avatar_id: r.avatar_id,
      avatar_background: r.avatar_background || 'bg-violet',
    })));
  },

  async 'POST /leaderboard/reset'(req, res) {
    const { player } = await requirePlayer(req, res);
    const row = await pb.first('user_progress', `uid = ${q(player.uid)}`);
    if (row) await pb.update('user_progress', row.id, { score: 0 });
    sendJson(res, 200, { ok: true });
  },

  async 'POST /activity'(req, res) {
    const { player } = await requirePlayer(req, res);
    await rateLimit(`activity:${player.uid}`, 240, 60);
    const b = await readJson(req);
    const eventType = text(b.event_type, 60);
    if (!eventType) throw new HttpError(400, 'Trūksta įvykio tipo.');

    let payload = {};
    try {
      const json = JSON.stringify(b.payload ?? {});
      if (json.length <= 4000) payload = JSON.parse(json);
    } catch { /* ignore */ }

    await pb.create('activity_log', {
      uid: player.uid,
      username: player.username,
      event_type: eventType,
      subject: text(b.subject, 80),
      game_id: text(b.game_id, 80),
      game_title: text(b.game_title, 120),
      class_group: text(b.class_group, 10),
      score: int(b.score, 0, 1_000_000),
      accuracy: int(b.accuracy, 0, 100),
      correct_answers: int(b.correct_answers, 0, 100000),
      incorrect_answers: int(b.incorrect_answers, 0, 100000),
      xp_earned: int(b.xp_earned, 0, 1_000_000),
      coins_earned: int(b.coins_earned, 0, 1_000_000),
      coins_spent: int(b.coins_spent, 0, 1_000_000),
      hint_cost: int(b.hint_cost, 0, 100000),
      is_correct: typeof b.is_correct === 'boolean' ? b.is_correct : null,
      question_id: text(b.question_id, 80),
      mistake_type: text(b.mistake_type, 80),
      payload,
    });
    sendJson(res, 200, { ok: true });
  },
};
