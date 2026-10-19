# LestingCraft: New Era — сайт

Продакшн: **https://lestingcraft.kroterallitir.workers.dev**

Одностраничный сайт проекта плюс две служебные страницы (`/press/`, `/privacy/`) и 404. Игра находится в раннем прототипе. Публичного релиза нет. Сайт собирает лист ожидания для закрытого тестового доступа; загрузок игры на сайте нет.

Автор: EHOT_EHOTbI4.music.

## Структура

- `public/index.html` — главная: hero, что уже работает, кадры, прогресс (Готово / В работе / В планах), форма листа ожидания, FAQ.
- `public/style.css` — вёрстка, адаптивность, `prefers-reduced-motion`, фокус-состояния.
- `public/app.js` — без inline-скриптов (CSP `script-src 'self'`): мобильное меню, reveal, lightbox кадров, статус сервера, отправка формы, фоновая вокселька на canvas.
- `public/press/index.html` — пресс-кит: факты, описание, кадры для скачивания, контакт через лист ожидания.
- `public/privacy/index.html` — что хранится и как удалить адрес.
- `public/404.html` — страница для неизвестных адресов (`not_found_handling = "404-page"`).
- `public/_redirects` — 301 со старых маршрутов предыдущей версии сайта.
- `public/_headers` — заголовки безопасности для статики.
- `public/media/*.webp` — кадры из игры и лаунчера (сжатые копии PNG из `marketing/media`).
- `public/fonts/` — Manrope и Unbounded, self-hosted, с лицензиями OFL.
- `src/worker.mjs` — маршрутизация: `/api/waitlist`, `/api/status`, `/api/releases`, `/api/launcher/latest`, остальное — статика.
- `src/waitlist.mjs` — приём адреса, валидация, honeypot, KV.
- `src/api.mjs`, `src/launcher.mjs` — статус сервера, релизы, лаунчер-фид.
- `server.mjs` — локальный preview-сервер на `0.0.0.0`; ведёт себя как продакшн (`/press` → `/press/`, неизвестные адреса → 404).

Статика обслуживается Workers Static Assets. В Worker идут только `/api/*`.

## Лист ожидания

`POST /api/waitlist`, JSON: `email`, `consent` (строго `true`), `note` (необязательно, до 500 символов), `website` (honeypot, для людей пустое поле).

- Адрес хранится в KV-namespace `WAITLIST` по ключу `email:<адрес в нижнем регистре>`. Значение: адрес, заметка, язык браузера, дата.
- Повторная запись отвечает так же, как первая, и не перезаписывает данные: форма не показывает, кто уже в списке.
- Без привязки `WAITLIST` эндпоинт отвечает **503**, а форма честно пишет, что лист откроется позже. Ничего не сохраняется.
- Чтобы включить хранение: `npx wrangler kv namespace create WAITLIST`, затем раскомментировать блок `[[kv_namespaces]]` в `wrangler.toml` и вписать id.
- Тело запроса до 2 КБ, только `application/json`.

Удаление адреса выполняется вручную по запросу: в заметке пишется «Удаление», запись находится в KV и удаляется.

## Запуск и проверки

```bash
cd Website
npm ci
npm start            # http://localhost:3900 (в облачном preview порт проксируется)
npm test             # Node: вебсайт API, лаунчер, лист ожидания, честность текстов
npx playwright install --with-deps chromium
npm run test:browser # Playwright: страницы, мобильное меню, статус, lightbox, форма
```

`npm test` запускает `test/*.test.mjs`. `copy.test.mjs` проверяет, что запрещённые утверждения (нереализованные функции, «бесплатно», «топ» и т. п.) встречаются только в блоках «В планах». `browser.spec.js` поднимает `server.mjs` на порту 3901 и мокает сетевые ответы API.

## Публикация

Push в `main` запускает `.github/workflows/website.yml`: сначала тесты, потом `npm run deploy` (wrangler). Для деплоя нужны GitHub Secrets `CLOUDFLARE_API_TOKEN` и `CLOUDFLARE_ACCOUNT_ID`. Значения в репозиторий не попадают.

Свой домен не подключён, используется workers.dev. Сервис-биндинг `COOP` указывает на `lestingcraft-coop`, статус сервера берётся оттуда.

## Что не делаем на сайте

Скачивание игры (до публичного релиза), аккаунты, платежи, рассылки, cookies, сторонняя аналитика, отзывы и счётчики, которых нет в проекте. Шрифты и скрипты грузятся только с домена сайта.

## Лицензия и файлы

Код сайта и тексты — в репозитории. Шрифты Manrope и Unbounded распространяются по лицензии SIL OFL (тексты лицензий в `public/fonts/`). Кадры — снимки Windows-прототипа и лаунчера.
