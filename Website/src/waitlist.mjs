// Waitlist for early test access. Stores only what the form sends.
// Without a WAITLIST KV binding the endpoint answers 503 instead of pretending to save.
const EMAIL = /^[^\s@<>()[\],;:"]+@[^\s@<>()[\],;:"]+\.[^\s@<>()[\],;:"]{2,}$/;
const MAX_BODY = 2048;
const MAX_EMAIL = 254;
const MAX_NOTE = 500;
export const WAITLIST_HEADERS = {
  'Cache-Control': 'no-store',
  'X-Content-Type-Options': 'nosniff',
};

const json = (status, body) => Response.json(body, { status, headers: WAITLIST_HEADERS });

export function normalizeEmail(value) {
  return String(value ?? '').trim().toLowerCase();
}

export function cleanNote(value) {
  // Drop control characters (keep newlines out too: it is a single-line field), trim, cap length.
  return String(value ?? '').replace(/[\u0000-\u001f\u007f]+/g, ' ').replace(/\s+/g, ' ').trim().slice(0, MAX_NOTE);
}

export function validate(input) {
  if (!input || typeof input !== 'object' || Array.isArray(input)) return { error: 'Некорректные данные.' };
  const email = normalizeEmail(input.email);
  if (!email || email.length > MAX_EMAIL || !EMAIL.test(email)) return { error: 'Проверьте адрес почты: он выглядит неполным.' };
  if (input.consent !== true) return { error: 'Нужно согласие на хранение адреса, чтобы записать вас.' };
  return { email, note: cleanNote(input.note), locale: String(input.locale ?? '').slice(0, 16), honeypot: String(input.website ?? '') };
}

export async function handleWaitlist(request, env = {}) {
  if (request.method !== 'POST') return json(405, { ok: false, message: 'Метод не поддерживается.' });
  const type = request.headers.get('Content-Type') || '';
  if (!type.toLowerCase().startsWith('application/json')) return json(415, { ok: false, message: 'Ожидается JSON.' });
  const length = Number(request.headers.get('Content-Length') || 0);
  if (length > MAX_BODY) return json(413, { ok: false, message: 'Слишком большой запрос.' });

  let raw;
  try { raw = await request.text(); } catch { return json(400, { ok: false, message: 'Не удалось прочитать запрос.' }); }
  if (raw.length > MAX_BODY) return json(413, { ok: false, message: 'Слишком большой запрос.' });
  let input;
  try { input = JSON.parse(raw); } catch { return json(400, { ok: false, message: 'Некорректные данные.' }); }

  const checked = validate(input);
  if (checked.error) return json(400, { ok: false, message: checked.error });

  // Honeypot: bots fill the hidden "website" field. Answer as if saved, store nothing.
  if (checked.honeypot) return json(200, { ok: true, message: 'Готово! Напишем, когда откроем тестовый доступ.' });

  if (!env.WAITLIST) return json(503, { ok: false, message: 'Лист ожидания скоро откроется. Попробуйте позже.' });

  const key = `email:${checked.email}`;
  const existing = await env.WAITLIST.get(key);
  // Same answer for new and known addresses, so the form cannot be used to check who is subscribed.
  if (!existing) {
    await env.WAITLIST.put(key, JSON.stringify({
      email: checked.email,
      note: checked.note,
      locale: checked.locale,
      createdAt: new Date().toISOString(),
    }));
  }
  return json(200, { ok: true, message: 'Готово! Напишем, когда откроем тестовый доступ.' });
}
