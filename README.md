# LestingCraft: New Era

**First-Person Open-World Voxel Survival & Space Exploration Sandbox on Unity 6 (URP)**  
*Воксельная песочница нового поколения: от каменного топора и живых поселений Лестингии — до собственного космодрома, 3D-кабины шаттла, 26 воксельных боссов и полётов к 16 мирам Солнечной системы в одиночку или в кооперативе до 8 игроков.*

<p align="center">
  <img src="https://github.com/Timofei2506/lestingcraft-new-era/releases/download/v0.7.1/gameplay.gif" alt="LestingCraft: New Era — Unity 6 URP Gameplay" width="800">
</p>

<p align="center">
  <a href="https://lestingcraft.kroterallitir.workers.dev/#download"><b>Скачать сборку v0.7.1 (Windows &amp; Android)</b></a> ·
  <a href="https://lestingcraft.kroterallitir.workers.dev/press/"><b>Press &amp; Publisher Kit (RU / EN)</b></a> ·
  <a href="https://drive.google.com/file/d/160lqiAUg5538eTwFhv-TALU2-XVMvDdh/view?usp=sharing"><b>Pitch Deck (PDF)</b></a> ·
  <a href="https://github.com/Timofei2506/lestingcraft-new-era/releases/latest"><b>GitHub Releases</b></a>
</p>

---

## English Summary (For Publishers & Technical Reviewers)

**LestingCraft: New Era** (`v0.7.1`, Unity `6000.0.32f1` URP) is an original first-person voxel survival, engineering, and spaceflight sandbox for **PC (Windows x64)** and **Android ARM64**, playable solo or in **online co-op up to 8 players**.

### Key Playable Pillars in Build `v0.7.1`
- **Greedy-Meshed Streaming Voxel Worlds**: Multi-biome terrain generation (forests, cherry groves, dunes, caves, villages, keeps), authored multi-storey architecture with verified capsule-clear topology, and local + server delta persistence.
- **Deep Survival, Crafting & Power Grids**: 156 3D voxel items/blocks, 2×2 inventory & 3×3 workbench crafting with searchable recipe book, smelting, body-part health, hunger, thirst, stamina, thermal insulation, off-world O₂ suit life support, electrical generation/storage (`kW` / `kWh`), rocket fuel synthesis, and radiation dosimetry in the Cherenkov anomaly zone.
- **Living Ecosystem & 26 Boss Encounters**: 34 wildlife & mutated species with custom PBR fur/plumage/chitin array coats, village settler professions & trading, and 26 multi-part voxel boss sculpts across planetary and expedition arenas.
- **Walkable Cosmodrome, 3D Shuttle Flight Deck & 16 Worlds**: Deploy a full launch pad complex with elevator tower, board a walkable shuttle interior with interactive 3D flight controls & live telemetry, dock with orbital stations featuring zero-gravity (`0g`) EVA and astronaut trading, and travel across 16 Solar System destinations (Moon, Mars, Venus, Mercury, Saturn, Titan, etc.).
- **Unity 6 URP Visual Stack**: Custom procedural volumetric sky & silver-lined cloud shaders, refractive crystal glass, array-backed PBR surface materials, SSAO, ACES tonemapping, and 5 quality presets (`Mobile` to `Cinematic`).
- **One-Click Co-op & Cloud Services**: Host any world directly from the pause menu (`Esc → Create Server`) over HTTPS (`443`) with server-authoritative block deltas; optional email-free account system (`Supabase`) with recovery codes, 200 achievements, `@nickname` friends list, and escrow skin marketplace.

---

## Быстрый старт (RU)

| Платформа | Пакет релиза | Запуск |
|---|---|---|
| **Windows x64** | `build-StandaloneWindows64.zip` | Распаковать архив целиком в отдельную папку и запустить `LestingCraft.exe` |
| **Android ARM64** | `LestingCraft-Android-arm64.apk` | Установить APK на устройство с Android 8.0+ |

Игроку не требуются Unity Hub, .NET или обязательная регистрация: одиночные экспедиции и прямое подключение к кооперативным серверам работают сразу после запуска.

---

## Что реализовано в текущей сборке (`v0.7.1`)

- **Мир и архитектура:** процедурные биомы Лестингии, пещеры, рудные жилы, деревни поселенцев, двухэтажные крепости, аномальное измерение Черенковского затона и планеты Солнечной системы; оптимизированный greedy-мешинг чанков и небесное освещение (`VoxelSkylight`).
- **Выживание, ремёсла и инженерия:** сетки крафта 2×2 и 3×3, интерактивная книга рецептов, плавильная печь, износ и утепление экипировки, температура тела, жажда, кислород скафандра, промышленная энергосеть и цепочка синтеза ракетного топлива.
- **Фауна и боссы:** 34 вида наземных, пернатых и аномальных существ с индивидуальными PBR-текстурами, мирные жители с профессиями и торговлей, а также **26 воксельных боссов** с расчётом затенения граней (AO) и эмиссией.
- **Космонавтика:** полноразмерный стартовый комплекс с подъёмным лифтом, многоуровневый интерьер шаттла, интерактивная 3D-приборная панель с живым навигатором, орбитальные станции с режимом невесомости (`0g`) и посадка на 16 небесных тел.
- **Графика и интерфейс (Unity 6 URP):** атмосферное небо с объёмными облаками, преломляющее стекло, PBR-массивы поверхностей, нативный масштабируемый интерфейс uGUI (`Unbounded` + `Manrope`), сюжетный бортовой журнал (3 главы) и 5 пресетов графики.
- **Кооператив и экосистема:** совместная игра до 8 человек с созданием сервера из меню паузы и приглашением в один клик, адресная книга серверов, система из 200 достижений, друзья по `@нику` и торговая площадка косметики с эскроу.

---

## Архитектура репозитория

```text
Assets/
  Art/PBR/              # PBR-массивы поверхностей и текстуры фауны
  Editor/               # Точки облачной сборки и генерации ресурсов (BuildScript)
  Resources/            # Шейдеры URP, воксельные каталоги (.bytes), шрифты и иконки (384×384)
  Scripts/
    Gameplay/           # Выживание, крафт, энергетика, фауна (34), боссы (26), орбитальная механика
    Networking/         # Сетевой клиент комнат и адресная книга серверов
    Presentation/       # Шейдерные контроллеры атмосферы, риги моделей и масштабируемый uGUI
    Runtime/            # Игровой цикл, контроллер от первого лица, шаттл, станции и смоук-пробы
    Story/              # Сюжетные главы, акторы и скульптура Стража
    World/              # Генерация биомов, планет, структур, greedy-мешинг и свет
  Tests/EditMode/       # Автоматические NUnit EditMode-тесты ядра, графики, космоса и UI
Server/                 # Кооперативный сервер (Cloudflare Workers / Durable Objects и standalone Node)
Supabase/               # SQL-схемы профиля, достижений, друзей и эскроу-маркетплейса
Launcher/               # Опциональный десктопный лаунчер с проверкой SHA-256 и безопасным откатом
Website/                # Официальный сайт, пресс-кит и API релизов (Workers Static Assets + Playwright)
Tools/                  # Генераторы ассетов и кросс-проверки C# / JS / Python без запуска редактора
Docs/                   # Проектная и техническая документация по подсистемам
.github/workflows/      # Полный конвейер CI/CD: EditMode-тесты, сборки Windows/Android, витрина кадров и деплой
```

---

## Автоматическая проверка качества (CI)

Репозиторий поддерживает многоуровневую верификацию каждого изменения:
1. **Детерминированные проверки без редактора (`Tools/check_*.py` + Mono C#):** сверяют целостность каталога из 156 моделей и прозрачных иконок, 30+ PBR-покрытий фауны, проходимость дверей и лестниц в архитектуре (паритет `C#` ↔ `JS`), орбитальную навигацию, топливную цепочку и скульпты всех 26 боссов.
2. **Тесты сайта (`Website/`):** модульные проверки API/честности текстов (`npm test`) и браузерные сквозные тесты на Chromium (`Playwright`) для десктопной и мобильной вёрстки.
3. **Облачные тесты и сборки Unity 6 (`.github/workflows/`):** прогон NUnit `EditMode`-тестов, автоматический захват эталонных внутриигровых сцен (`showcase.yml`, `mob-boss-shots.yml`) и сборка релизов `Windows x64` + `Android ARM64`.

---

## Интеллектуальная собственность и лицензия

- **100% Original IP:** весь исходный код, игровые системы, шейдеры, текстуры и воксельные модели разработаны специально для **LestingCraft: New Era** (автор и правообладатель: **Timofei · `EHOT_EHOTbI4.music`**). Никакие материалы сторонних коммерческих игр не используются.
- Сторонние шрифты и библиотеки сопровождаются открытыми лицензиями в каталоге [`Docs/ThirdParty`](Docs/ThirdParty).
- Код репозитория открыт для публичного просмотра и технического ревью на условиях ограничительной source-available лицензии — подробности в файле [`LICENSE`](LICENSE).

**Контакты для прессы и издателей:** `ehotehotich.music@gmail.com` · [Пресс-кит на сайте](https://lestingcraft.kroterallitir.workers.dev/press/)
