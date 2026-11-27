# LestingCraft: New Era

**First-Person Open-World Voxel Survival & Space Exploration Sandbox on Unity 6 (URP)**  
*Воксельная песочница нового поколения: от каменного топора и живых поселений Лестингии — до собственного космодрома, 3D-кабины шаттла, 24 воксельных боссов и полётов к 24 мирам: Солнечная система плюс шесть измерений Фронтира 2.0 — в одиночку или в кооперативе до 8 игроков.*

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

**LestingCraft: New Era** (`v0.7.1`, Unity `6000.3.29f1` URP) is an original first-person voxel survival, engineering, and spaceflight sandbox for **PC (Windows x64)** and **Android ARM64**, playable solo or in **online co-op up to 8 players**.

### Key Playable Pillars in Build `v0.7.1`
- **Greedy-Meshed Streaming Voxel Worlds**: Multi-biome terrain generation (forests, cherry groves, dunes, caves, villages, keeps), authored multi-storey architecture with verified capsule-clear topology, and local + server delta persistence.
- **Deep Survival, Crafting & Power Grids**: 171 3D voxel items/blocks, 2×2 inventory & 3×3 workbench crafting with searchable recipe book, smelting, body-part health, hunger, thirst, stamina, thermal insulation, off-world O₂ suit life support, electrical generation/storage (`kW` / `kWh`), rocket fuel synthesis, and radiation dosimetry in the Cherenkov anomaly zone.
- **Living Ecosystem & 24 Boss Encounters**: 48 wildlife & mutated species with custom PBR fur/plumage/chitin array coats, village settler professions & trading, and 26 multi-part voxel boss sculpts across planetary and expedition arenas.
- **Walkable Cosmodrome, 3D Shuttle Flight Deck & 24 Realms**: Deploy a full launch pad complex with elevator tower, board a walkable shuttle interior with interactive 3D flight controls & live telemetry, dock with orbital stations featuring zero-gravity (`0g`) EVA and astronaut trading, and travel across **24 realms** — the Solar System ladder plus the six Deep Frontier worlds of 2.0 (Crystalia, Emberfall, Aquaria, Stormreach, Verdant, Nihility), each with its own terrain sampler, gravity, atmosphere, boss, fauna and PBR palette.
- **Unity 6 URP Visual Stack**: Custom procedural volumetric sky & silver-lined cloud shaders, refractive crystal glass, array-backed PBR surface materials, SSAO, ACES tonemapping, and **7 quality presets** (`Mobile` → `Balanced` → `High` → `Ultra` → `Cinematic` → `Frontier` → `Extreme`) driving a per-preset view-distance and detail-LOD band, chunk-mesh pooling and nearest-first streaming.
- **One-Click Co-op & Cloud Services**: Host any world directly from the pause menu (`Esc → Create Server`) over HTTPS (`443`) with server-authoritative block deltas; optional email-free account system (`Supabase`) with recovery codes, 200 achievements, `@nickname` friends list, and escrow skin marketplace.

---

## Фронтир 2.0 — шесть новых измерений (RU)

Апдейт 2.0 добавляет шесть полноценных измерений (id `18..23`), каждое со своим сэмплером
рельефа, гравитацией, атмосферой, набором поверхности, фауной, боссом и стеком PBR-материалов.
Старые `0..17` не перенумерованы — сейвы, протокол и авторские постройки совместимы.

| id | Измерение | Гравитация | Что внутри |
|---|---|---|---|
| 18 | **Кристалия** | 0.28 g | призматические шпили, жеодовые впадины, полярный лёд, парящие осколки; Страж — Призматический Суверен |
| 19 | **Пепельный Водопад** | 0.64 g | небесные базальтовые острова над морем пепла, магмовые берега; Страж — Пепельный Титан |
| 20 | **Аквария** | 0.88 g | архипелаги, соборы ламинарии, приливные руины, коралловые отмели; Страж — Бездонная Пасть |
| 21 | **Грозовой Предел** | 0.74 g | плато, решётчатые колонны, грозовое стекло и штормовая сталь; Страж — Громовой Архистратиг |
| 22 | **Вердант** | 0.96 g | мицелиевые леса, гигантские шляпки, споровые болота; Страж — Мицелиевая Королева |
| 23 | **Нихилиум** | 0.10 g | перевёрнутые фрагменты, стеклянные уступы, осколки пустоты; Страж — Пожиратель Ничто |

- **Переход:** `/frontier 18..23` в творческом режиме (или ритуал измерения). Возврат — арка
  возврата в каждом секторе 192×192, площадка прибытия у спавна или команда `/overworld`.
- **Гарантии маршрута:** площадка прибытия выравнивается по самой низкой точке рельефа, арка
  всегда с проёмом (столбы и перекладина прорезают любой рельеф), так что игрок не может
  застрять в скале или оказаться замурованным.
- **Новые материалы:** 12 наборов PBR (`prism-crystal`, `aurora-ice`, `soot-dune`,
  `ember-magma-shore`, `kelp-weave`, `reef-shelf`, `tidal-ruin`, `charge-glass`, `storm-iron`,
  `mycelium-cap`, `spore-loam`, `void-shard`) — генерируются `Tools/build_frontier_materials.py`.
- **Блоки:** id `115..133` (в свободной полосе каталога, не пересекаются с id предметов/брони).

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
- **Фауна и боссы:** 48 видов наземных, пернатых и аномальных существ с индивидуальными PBR-текстурами, мирные жители с профессиями и торговлей, а также **24 воксельных босса** (включая шесть новых Стражей Фронтира) с расчётом затенения граней (AO) и эмиссией.
- **Небо, свечение и звук:** по одному эффекту неба на мир (полярные занавеси Кристалии, разряды Грозового Предела, споры Верданта, швы Нихилиума), живое эмиссивное свечение семи фронтирных поверхностей (мицелий, водоросли, призма, лёд, пустота, грозовое стекло, магма) и отдельная звуковая сцена для каждого фронтирного мира — включая гром, привязанный к той же вспышке, что видят игрок. Молния Грозового Предела теперь видна и на воде — блик и всплеск пены идут по тому же окну удара, что и сама вспышка. Все шесть презентационных таблиц (небо, звук, свечение, частицы, осадки, голоса Стражей) сверяются между собой сводной проверкой `realm-matrix`, чтобы новый мир не мог выйти «наполовину старым». Фауна Фронтира тоже обзавелась голосами: призматическая рысь звенит, геодовый левиафан гудит суб-басом, пустотный призрак шепчет. У шести Стражей Фронтира теперь и свои голоса — стеклянный звон Владыки Призм, печная низовка Пепельного Титана, раскат Громового Архонта, расстройка Хранителя Пустоты. Под ногами тоже слышно материал: кристалл и грозовое стекло звенят, лёд хрустит, водоросли пружинят, сажа глушит шаг. Осадки и выглядят по-разному: сажа кувыркается над Пепельным Водопадом, муссон хлещет по Акварии, споровая морось висит в Верданте. Осадки теперь доходят только туда, где им есть где падать: в вакууме Кристалии и Нихилиума, в закрытых мирах и в Грозовом Пределе дождя нет ни в звуке, ни в достижениях. У земли тоже свой слой: пепел, споры, соль и пыль пустоты висят в воздухе по таблице `MoteField` и реагируют на погоду, ветер и свечение поверхности.
- **Космонавтика:** полноразмерный стартовый комплекс с подъёмным лифтом, многоуровневый интерьер шаттла, интерактивная 3D-приборная панель с живым навигатором, орбитальные станции с режимом невесомости (`0g`) и посадка на 16 небесных тел.
- **Графика и интерфейс (Unity 6 URP):** атмосферное небо с объёмными облаками, преломляющее стекло, PBR-массивы поверхностей, нативный масштабируемый интерфейс uGUI (`Unbounded` + `Manrope`), сюжетный бортовой журнал (3 главы) и 5 пресетов графики. У каждого из 24 миров свой эффект неба (`SkyEffects`), у фронтирных поверхностей — собственное эмиссивное свечение (`SurfaceGlows`), а у шести миров Фронтира — свой процедурный эмбиент и гром по окну молнии (`RealmSoundscape`).
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
