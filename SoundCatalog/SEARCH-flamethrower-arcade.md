# Flamethrower · Stylized / Arcade

Дата: 29 октября 2026. Добавлен **21 самостоятельный готовый игровой SFX: 13 Loop, 7 Start, 1 End** в существующую категорию Flamethrower, отдельный объект `flamethrower-arcade` / `Stylized / Arcade`.

[Открыть новую группу](http://127.0.0.1:8765/?object=flamethrower-arcade). Для непрерывного звучания включите «Повтор» в нижнем плеере. Фильтр «Перспективные · топ-5» показывает предварительную пятёрку, не меняя пользовательские звёздочки.

## Характер и отбор

Готовые игровые дизайны из 17 библиотек 13 студий: Sound Spark, SoundMorph, Digital Rain Lab, CB Sound Design, PMSFX, David Dumais Audio, Bluezone, InspectorJ, Fox Audio, Doex Studio, Rogue Waves, Chris Logsdon и Gamemaster Audio. BlueZone / Bluezone Corporation считаются одной студией. Общий дистрибьютор — Sonniss GDC, а не 17 независимых сайтов. Дополнительно исследованы OpenGameArt и Freesound.

Энергетические дуги, силовые поля, колеблющееся фантастическое горение, магический воздух, аниме-луч и огненные заклинания. Это альтернативы для стилизованного огнемёта, а не утверждение, что каждый источник изначально продавался как flamethrower. Короткие вспышки и затухания оставлены Start/End. Три дубля одного anime beam не добавлены как три звука; RPM-варианты одного двигателя также не размножались.

Исключены яркие шумовые Handheld Generator, Tripod Black Smoke и Anime Charge Down; длинные одиночные whoosh не превращались в искусственные «непрерывные потоки». Петля двигателя Little Robot Sound Factory с OpenGameArt исследована, но не включена: всего 0,493 с, недостаточно выразительная длительность для этого отбора. Freesound отвечает HTTP 403 на доступные загрузчику страницы; защищённые загрузки не обходились. GitHub Harvester не включён из-за неоднозначного лицензирования аудио относительно общего LICENSE. AI-генераторы и самостоятельно синтезированные эффекты не использованы.

Технические спектральные и динамические измерения помогают убрать яркие и обрывочные кандидаты, но **не подтверждают субъективную приятность**. Прослушивание на слух и сравнение в боевом миксе не выполнены. Пятёрка — предварительный отбор для вашего прослушивания.

## Подготовка аудио

Все 21 оригинала — неизменённые WAV, извлечённые из официальных публичных бесплатных архивов Sonniss. Проверены CRC32 ZIP и SHA-256. Исходники и обработанные версии лежат отдельно; MP3-превью или перекодированные в WAV MP3 здесь не использовались.

Превью: PCM24 WAV / 48 kHz, исходное число каналов до stereo, HP55 / LP6500; одно постоянное усиление к −26 LUFS с потолком −8 dBTP. Громкость измерена на повторённом до ≥4 секунд фрагменте. Лимитер, компрессия, изменение высоты и новые слои не применялись. Петли имеют equal-power перекрытие 120 мс без провала в тишину; Start/End — края 6/60 мс. Точный фрагмент, обработка и метрики доступны в каждой карточке и `processed/preparation.json`.

Для одного динамичного Start (Electrostatic Ignition) потолок пиков ограничивает уровень до −29,01 LUFS: сохранена динамика вместо принудительного лимитирования. Остальные около −26 LUFS. Это не готовый финальный игровой микс; на телефоне нужно проверить бас, резонансы и сочетание с остальными орудиями.

## Лицензии и происхождение

[Официальные бесплатные выборки Sonniss](https://sonniss.com/gameaudiogdc/), [Sonniss GDC License 2.0](https://sonniss.com/gdc-bundle-license/), актуальная страница проверена и сохранена локально. Разрешены коммерческие игры и обработка без обязательного авторства. Запрещено распространять записи как SFX-библиотеку/семплы, включая обработанные версии; также запрещено обучение AI/ML. **Этот каталог предназначен для локального отбора внутри проекта; его нельзя публиковать как скачиваемую аудиобиблиотеку.**

Каждый файл содержит точное имя и участника архива, URL архива, студию, CRC32, SHA-256 и сохранённые страницы источника/лицензии: `dist/licenses/fta-*/`. Полные оригиналы — `dist/audio/flamethrower/flamethrower-arcade/<id>/originals/`, версии для сравнения — `processed/`.

## Добавленные звуки

| ID | Роль | Название | Библиотека / официальный архив |
|---|---|---|---|
| `fta-deep-bass-arc` | Loop | Sound Spark · Deep Bass Energy Arc | [Sound Spark LLC - Electric Arcs and Energy · GDC 2020](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202020%20-%20Game%20Audio%20Bundle%20Part12of14.zip) |
| `fta-burning-warble` | Loop | SoundMorph · Warbling Energy Burn | [SoundMorph - TENSION · GDC 2016](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202016-%20Game%20Audio%20Bundle%20Part%206of6.zip) |
| `fta-lethal-force-field` | Loop | Digital Rain Lab · Lethal Force Field | [Digital Rain Lab - Lethal Energies · GDC 2016](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202016-%20Game%20Audio%20Bundle%20Part%202of6.zip) |
| `fta-cyberdeck-stream` | Loop | CB Sound Design · Cyberdeck Energy Stream | [CB Sound Design - Cyberdeck - Hologram & Transmissions Sound Effects · GDC 2023](https://downloads.sonniss.com/Sonniss.com-GDC2023-GameAudioBundle2of14.zip) |
| `fta-hybrid-plasma` | Loop | PMSFX · Hybrid Electromagnetic Flow | [PMSFX - Obscure Frequencies · GDC 2019](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202019%20-%20Game%20Audio%20Bundle%20Part%203of8.zip) |
| `fta-force-field` | Loop | SoundMorph · Energy Force Field | [SoundMorph - ENERGY · GDC 2017](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202017%20-%20Game%20Audio%20Bundle%20Part%208of9.zip) |
| `fta-barrier-core` | Loop | SoundMorph · Barrier Core Stream | [SoundMorph - ENERGY · GDC 2017](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202017%20-%20Game%20Audio%20Bundle%20Part%208of9.zip) |
| `fta-oscillating-burn` | Loop | SoundMorph · Low Oscillating Burn | [SoundMorph - TENSION · GDC 2016](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202016-%20Game%20Audio%20Bundle%20Part%206of6.zip) |
| `fta-magic-electricity` | Loop | David Dumais · Magic Electricity | [David Dumais Audio - Magic Sound FX Pack 1 · GDC 2020](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202020%20-%20Game%20Audio%20Bundle%20Part3of14.zip) |
| `fta-arcane-air-stream` | Loop | David Dumais · Arcane Air Stream | [David Dumais Audio - Magic Sound FX Pack 2 · GDC 2023](https://downloads.sonniss.com/Sonniss.com-GDC2023-GameAudioBundle2of14.zip) |
| `fta-alien-energy` | Loop | Bluezone · Alien Energy Texture | [BlueZone - Alien Spacecraft Sound Effects · GDC 2019](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202019%20-%20Game%20Audio%20Bundle%20Part%201of8.zip) |
| `fta-abstract-reactor` | Loop | InspectorJ · Abstract Reactor Flow | [InspectorJ - Abstract Spaceships · GDC 2019](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202019%20-%20Game%20Audio%20Bundle%20Part%202of8.zip) |
| `fta-podracer-flame` | Loop | Fox Audio · Podracer Flame Drive | [Fox Audio Post-Production - Space Racer 2 – Turbines & Engines · GDC 2018](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202018%20-%20Game%20Audio%20Bundle%20Part%203of8.zip) |
| `fta-anime-beam` | Start | Doex Studio · Anime Beam Ignition | [Doex Studio - 90s Anime SFX Pack · GDC 2024](https://downloads.sonniss.com/Sonniss.com-GDC2024-GameAudioBundle1of9.zip) |
| `fta-dragon-flare` | Start | Sound Spark · Dragon Fire Flare | [Sound Spark LLC – Magic Spells, Buffs and Attacks · GDC 2019](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202019%20-%20Game%20Audio%20Bundle%20Part%205of8.zip) |
| `fta-dark-energy` | Start | Rogue Waves · Dark Energy Ignition | [Rogue Waves - Druid Magic · GDC 2023](https://downloads.sonniss.com/Sonniss.com-GDC2023-GameAudioBundle8of14.zip) |
| `fta-energy-breath` | Start | Chris Logsdon · Energy Breath Attack | [Chris Logsdon - Sci-Fi SFX Pack · GDC 2019](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202019%20-%20Game%20Audio%20Bundle%20Part%202of8.zip) |
| `fta-warble-powerup` | Start | Sound Spark · Warbling Powerup | [Sound Spark LLC - Electric Arcs and Energy · GDC 2020](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202020%20-%20Game%20Audio%20Bundle%20Part12of14.zip) |
| `fta-electrostatic-ignition` | Start | Bluezone · Electrostatic Ignition | [Bluezone Corporation - Electrostatic Field · GDC 2018](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202018%20-%20Game%20Audio%20Bundle%20Part%202of8.zip) |
| `fta-portal-flame` | Start | Gamemaster Audio · Portal Flame Surge | [Gamemaster Audio - Magic and Spell Sounds · GDC 2018](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202018%20-%20Game%20Audio%20Bundle%20Part%203of8.zip) |
| `fta-energy-shutdown` | End | Chris Logsdon · Energy Shutdown | [Chris Logsdon - Sci-Fi SFX Pack · GDC 2019](https://downloads.sonniss.com/Sonniss.com%20-%20GDC%202019%20-%20Game%20Audio%20Bundle%20Part%202of8.zip) |

## Проверки и сохранность

- В каталоге 82 записи: все прежние 61 запись, категории и объекты сохранены; добавлена только новая группа и 21 запись. Совпадение старых метаданных проверено программно.
- Файл `data/selections.json` побайтово совпадает с исходным SHA-256 `080809b976389911f32bd6045ec90a18b604ca2653cb2332ff49b068573d3197`. Избранное и заметки не менялись.
- `node validate.mjs`: 82 sounds, 7 objects, 2 categories; все локальные ссылки, лицензии и контрольные суммы корректны. Проверяет также старые категории.
- `node --check dist/app.js` и `node tools/test_automatic_fire.mjs`: успешно; интерфейс, плеер и автоматическая стрельба не переписывались.
- `python -X utf8 tools/verify_flamethrower.py --arcade`: все 42 WAV (превью + оригиналы) декодируются FFmpeg; SHA-256 совпадают; HTTP Range 206 возвращает правильные байты и audio MIME. Нет одинаковых хешей источников внутри новой группы и совпадений с прежними источниками.
- Проверены 78 пар Loop на корреляцию PCM с поиском сдвига: максимум 0,3041, признаков повторного использования одного семпла не найдено. Это техническая проверка, не гарантия отсутствия сходства на слух.
- Браузер Zen: **42 / 42 файлов** (21 превью и 21 оригинал) реально воспроизведены до события ended, без ошибок. Проверка была без звука в динамиках и на скорости 4×; это проверка воспроизведения, а не прослушивание тембра. Новая группа открыта для обычного прослушивания.

Рецепт импорта: `python -X utf8 tools/import_flamethrower_arcade.py` при наличии исследовательских оригиналов в `.local/research/flamethrower-arcade`. Он не скачивает заново старые подборки и не пишет отметки. Технические отчёты: `import-result.json`, `verification.json`, `browser-playback.json` в той же исследовательской папке. Для обычного прослушивания нужны только браузер и сервер из `Open Catalog.cmd`.

Unity, FMOD, волны, игровые параметры и сохранения игры не изменялись. Для открытия нужной вкладки и проверки отображения использован Computer Use. После завершения сеанс сброшен, найденный помощник остановлен. Проверка процессов показала автоматический перезапуск `codex-computer-use-swift.exe` самим `ChatGPT.exe` (новый PID 6820). ChatGPT и браузер не закрывались; бесконечный цикл завершения не создавался. Гарантировать отсутствие жёлтой рамки по одному факту остановки процесса нельзя.
