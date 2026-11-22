# LestingCraft: New Era — Официальный сайт и пресс-кит

Продакшн: **https://lestingcraft.kroterallitir.workers.dev**

Официальный веб-сайт проекта **LestingCraft: New Era** (`v0.7.1`), включающий главную витрину с прямой загрузкой сборок (`Windows x64` и `Android ARM64`), мониторинг кооперативных миров, двуязычный пресс-кит для СМИ и издателей (`/press/`), инструкцию по сетевой игре (`/play-with-friends/`), политику конфиденциальности (`/privacy/`) и условия лицензии (`/license/`).

Автор: **Timofei (`EHOT_EHOTbI4.music`)**.

## Структура

- `public/index.html` — главная страница: ключевые механики `v0.7.1`, прямая загрузка релизов с проверкой SHA-256, геймплейный тизер, список активных кооперативных миров, галерея внутриигровых кадров Unity 6 URP, дорожная карта к `1.0`, форма обратной связи и FAQ.
- `public/press/index.html` — двуязычный пресс-кит (`RU / EN`) для журналистов и издателей: Fact Sheet, Pitch Deck, прямые ссылки на билд, скриншоты высокого разрешения и готовый пресс-релиз.
- `public/play-with-friends/index.html` — пошаговое руководство по запуску кооперативного сервера до 8 игроков.
- `public/privacy/index.html` и `public/license/index.html` — юридическая информация и политика работы с данными.
- `public/style.css` — адаптивная вёрстка, поддержка `prefers-reduced-motion` и доступных фокус-состояний.
- `public/app.js` — клиентская логика без inline-скриптов (строгий CSP `script-src 'self'`): мобильное меню, лайтбокс галереи, статус кооператива, загрузка проверенных релизов и форма уведомлений.
- `src/` — серверные обработчики Cloudflare Worker (`/api/status`, `/api/worlds`, `/api/releases`, `/api/waitlist`, `/api/hit`, `/api/launcher/latest`).
- `test/` — модульные тесты Node (`*.test.mjs`) и сквозные браузерные тесты `Playwright` (`browser.spec.js`).

## Локальный запуск и тесты

```bash
cd Website
npm ci
npm test             # Node: API, лаунчер, честность формулировок, лист уведомлений
npx playwright install --with-deps chromium
npm run test:browser # Playwright: рендер всех страниц, отсутствие горизонтального скролла на 390px, лайтбокс, релизы, форма
```

## Автоматический деплой

При пуше изменений `Website/**` в ветку `main` запускается `.github/workflows/website.yml`: после успешного прохождения `npm test` и `Playwright` выполняется `wrangler deploy` в Cloudflare Workers и отправка уведомления `IndexNow`.
