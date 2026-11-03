# LestingCraft: New Era

Воксельная песочница на **Unity 6** для **Windows x64** и **Android ARM64**.
Свой мир, крафт, выживание и кооператив. Исходный код доступен для просмотра по
ограничительной source-available лицензии; это не OSI open-source.

- [Сайт и загрузки](https://lestingcraft.kroterallitir.workers.dev)
- [Последний релиз](https://github.com/Timofei2506/lestingcraft-releases/releases/latest)
- [План графической итерации](PLAN.md)
- [Графика и интерфейс](Docs/Графика-и-интерфейс.md)
- [Визуальная ревизия 08](Docs/Визуальная-ревизия-08.md)

## Платформы

| Платформа | Файл | Как запустить |
|---|---|---|
| Windows x64 | `build-StandaloneWindows64.zip` | распаковать целиком и открыть `LestingCraft.exe` |
| Android ARM64 | `LestingCraft-Android-arm64.apk` | установить APK; при несовпадении подписи может потребоваться удалить старую версию |

Браузерная версия отложена. Сборки выполняются в GitHub Actions; игроку не нужны Unity Editor/Hub.
Ручной workflow создаёт артефакты, но не публикует GitHub Release. Инструкция: [Сборка в облаке](Docs/Сборка-в-облаке.md).

## Что уже есть в коде

- Блочный мир: генерация, стриминг чанков, greedy meshing, сохранение изменений.
- Добыча/установка блоков, инвентарь, хотбар, крафт 2×2 и верстак 3×3, книга рецептов.
- Печь, сундуки, инструменты, прочность, экипировка.
- Выживание: витальные показатели, погода, температура, локальные сохранения.
- Кооператив до 8 игроков: хост открывает мир, игроки подключаются по адресу, сервер подтверждает изменения блоков.
- Аккаунт, кошелёк, скины и друзья на Supabase — необязательны для одиночной игры.
- PBR-материалы, URP, процедурное небо, геометрическая трава, вода, погода, день/ночь.
- Животные, жители, структуры и боссы — прототипы; баланс, анимации и полировка продолжаются.
- Космические полёты и планеты — экспериментальные механики, не отдельный завершённый релиз.

## Графика: итерация 08 «лесная экспедиция»

Текущая ветка улучшает не «количество эффектов», а читаемость леса:

- единая шкала качества **Mobile / Balanced / High / Ultra / Cinematic**;
- корректная миграция старых настроек и отдельные лимиты для Android;
- ограниченный sky-bounce для листвы, чтобы кроны не проваливались в чёрный;
- более плотная и высокая трава на старших пресетах, без коллизий и без GameObject на стебель;
- спокойнее bloom/contrast/vignette, без хроматической аберрации;
- компактные tooltip, поиск/фильтр рецептов, safe-area fit для мобильных вырезов;
- сегментные кольца боссов вместо сплошных квадратных плит.

Это **не** hardware ray tracing и не фотограмметрия. Карты материалов — производные PBR.
CI-кадры на программном rasterizer подтверждают работоспособность и композицию, но не FPS на целевом железе.
Подробности и ограничения: [Визуальная ревизия 08](Docs/Визуальная-ревизия-08.md).

## Структура репозитория

```text
Assets/Scripts/        # Runtime, World, Gameplay, Presentation, Networking, Story
Assets/Resources/      # шейдеры, иконки, модели, UI; Generated создаётся при сборке
Assets/Editor/         # BuildScript и воспроизводимая генерация presentation assets
Assets/Tests/          # EditMode-тесты
ArtSource/             # исходники арта, включая архивные итерации
Docs/                  # архитектура, графика, выживание, космос, хостинг
Tools/                 # генераторы, headless core checks, hygiene/art/content gates
Server/                # co-op backend
Supabase/              # схема и функции аккаунтов/экономики
Launcher/              # десктопный лаунчер и проверки обновления
Website/               # официальный сайт
.github/workflows/     # CI, сборки, showcase, visual review, server/website deploy
```

## Сборка и проверка

Секреты `UNITY_USERNAME` / `UNITY_PASSWORD` хранятся только в GitHub Actions secrets.

1. **Actions → Build Windows and Android → Run workflow** — тестовые Windows ZIP и Android APK.
2. **Actions → Visual review (forest + UI) → Run workflow** — EditMode, Linux-плеер и 7 реальных кадров.
3. **Actions → Showcase Screenshots** — расширенный ERA-V9 showcase.

Локальные быстрые проверки без Unity:

```bash
python Tools/audit_hygiene.py
python Tools/check_art.py
python Tools/check_content.py
python Tools/check_nature_render.py
```

Headless-проверки ядра компилируются `mcs` в CI (`.github/workflows/core.yml`).
Полная компиляция Unity, шейдеры и runtime-поведение проверяются только в Unity/плеере.

## Кооператив и хостинг

Сервер: `https://lestingcraft-coop.kroterallitir.workers.dev` (`/health` — API, не игра).
Документация: [кооператив, Cloudflare, Node, Docker и протокол](Docs/Мультиплеер-и-хостинг.md).
Standalone backend не требует Cloudflare/R2/Neon.

## Правовой момент

Проект использует только собственные текстуры, модели, звуки и код.
Ассеты и данные Minecraft/Mojang не используются и не будут использоваться — только жанровые механики.
Правообладатель — `EHOT_EHOTbI4.music`. Полный текст: [LICENSE](LICENSE).
Сторонние материалы сохраняют собственные лицензии.
