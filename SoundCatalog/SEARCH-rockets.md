# Rocket Launcher — запуски и взрывы

29 октября 2026. Добавлены две отдельные категории, **20 Rocket Launch + 20 Rocket Explosion**.

- [Rocket Launch](http://127.0.0.1:8765/?category=rocket-launch): готовые ракетные запуски, короткие sci-fi разгоны, воздушные и энергетические импульсы.
- [Rocket Explosion](http://127.0.0.1:8765/?category=rocket-explosion): компактные игровые детонации, магические/плазменные удары и отдельные ретро-аркадные варианты.

Основной плеер, удержание огня, избранное, заметки, корзина и восстановление работают через существующие компоненты. Для сравнения повторения начните с 1–2 событий/с и одинакового лимита голосов. В каждой категории отмечены пять предварительных кандидатов, отдельно от пользовательского избранного.

## Источники и лицензии

- **30 SFX из бесплатных выборок Sonniss GDC**: SoundMorph, Gamemaster Audio, Bluezone, David Dumais Audio, PMSFX, CB Sound Design, Rescopic, Airborne Sound, Soundbankster, Soundrangers, Async Audio и другие. [GDC License 2.0](https://sonniss.com/gdc-bundle-license/) разрешает коммерческие игры и обработку без обязательной атрибуции, но запрещает самостоятельное распространение SFX-библиотеки/семплов и обучение AI/ML. Каталог предназначен для локального отбора в проекте. Для каждого файла сохранены конкретный ZIP URL, archiveMember, CRC, автор и SHA-256.
- **4 SFX CC BY 3.0**: Michel Baradari — [4 projectile launches](https://opengameart.org/content/4-projectile-launches) и [2 High Quality Explosions](https://opengameart.org/content/2-high-quality-explosions); dklon — [Rocket Launch Pack](https://opengameart.org/content/rocket-launch-pack). Требуются автор, источник, лицензия и указание обработки.
- **6 SFX CC0**: mikeask — [Missile sound](https://opengameart.org/content/missile-sound); qubodup — [Synthesized explosion](https://opengameart.org/content/synthesized-explosion); Joth — [Chunky Explosion](https://opengameart.org/content/chunky-explosion); rubberduck — [50 CC0 Sci-Fi SFX](https://opengameart.org/content/50-cc0-sci-fi-sfx); Kenney — [Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds). Коммерческое использование разрешено без обязательной атрибуции.

Три файла Kenney/rubberduck взяты из ранее скачанных официальных пакетов: в активном каталоге их ещё не было. Остальные выбранные исходники скачаны при этом поиске. MP3 Joth сохранён в исходном формате; превью переведено в PCM, без дополнительного lossy-перекодирования. Оригиналы WAV/FLAC/OGG также сохранены побайтно.

Полные источники, сохранённые доказательства лицензий и готовые кредиты: `dist/licenses/rl-*/` и `dist/licenses/re-*/`. Перед коммерческим выпуском перенесите CC BY-кредиты в титры игры. Не публикуйте каталог с Sonniss как самостоятельную библиотеку.

## Отбор и обработка

Основная часть — готовые игровые дизайны. Некоторые Launch являются дизайнерскими пролётами/разгонами из смежных категорий, а некоторые Explosion — магическими ударами: это альтернативы для стилизованного оружия, а не записи настоящих боевых ракет. Характер каждого указан в карточке.

Исключены длинная непрерывная тяга, наиболее яркий Laser Rocket, тяжёлый искажённый Explosion Rumble, однотипные варианты одного семейства и идентичный SIGNUM-файл из второго архива. Из четырёх dklon оставлен один кандидат; большой и малый варианты взрыва Michel не выданы за две независимые записи. Не использованы платные файлы, обходы входа/защиты или самостоятельная генерация звуков.

Превью: запуск до **1,5 с**, взрыв до **2 с**; удалено тихое начальное/конечное заполнение, сохранён основной импульс. Края смягчены на 2/60 мс. HP65 для Launch / HP80 для Explosion, LP6000; PCM24/48 kHz, исходные mono/stereo. Полная исходная длительность всегда доступна в оригинале.

Громкость сопоставлена по одному эталону: восемь событий с интервалом 1 с в десятисекундном сигнале. Постоянное усиление к примерно **−27 LUFS**, с ограничением измеренного true peak эталона до **−10 dBTP**. Итоговый диапазон −27,24…−26,55 LUFS; тихий вариант с предельным пиком не усиливался сверх запаса. Нет компрессии, изменения высоты/темпа или новых слоёв.

**Оценка комфорта на слух и в реальном боевом миксе не выполнена.** Назначение исходников, спектр, огибающая, пики и техническое воспроизведение помогают отбору, но не доказывают отсутствие утомления. В частности, яркие электрические и ретро-варианты стоит сравнить с мягкими воздушными и короткими sci-fi ударами на динамике телефона.

## Проверки

- ImportGate перед копированием и CatalogStore.import_records при фиксации: все 40 приняты; проверки active/Removed по ID, URL/члену архива, SHA-256 и консервативному сходству PCM.
- 80 аудиофайлов: FFmpeg декодирование, SHA-256 и HTTP Range 206 прошли.
- Все 95 прежних записей, категории, объекты и файл избранного/заметок сохранены без изменений.
- На изолированных копиях реальных новых ассетов проверены перенос в Removed, сохранение preparation.json и оригинала, повторное открытие архива, блокировка повторного импорта под другим ID и восстановление. Пользовательские треки для тестирования не удалялись.
- `node validate.mjs`: 135 sounds, 10 objects, 5 categories. Проверки основного плеера, корзины и автоматической стрельбы прошли; `test_removed.py` — 13 тестов успешно.
- Браузерная проверка: **80/80 файлов воспроизведены до конца без ошибок** (40 Launch + 40 Explosion, включая оригиналы), без вывода звука, с ускорением ×4. Отчёты — `browser-launch.json` и `browser-explosion.json` в исследовательской папке. Обычная страница Rocket Explosion открыта для прослушивания; обе новые категории отображают по 20 записей, общий плеер и корзину.

Исследования и отчёты: `.local/research/rockets/`; инструменты — `tools/research_rockets.py`, `tools/import_rockets.py`, `tools/verify_rockets.py`. Unity/FMOD, сцены, волны и игровые сохранения не изменялись.

## Все добавленные записи

| Категория | Кандидат | Превью, с | Лицензия |
|---|---|---:|---|
| rocket-launch | SoundMorph · Steampunk Rocket | 1.500 | Sonniss GDC 2.0 |
| rocket-launch | David Dumais · Sci-Fi Blast Off | 1.500 | Sonniss GDC 2.0 |
| rocket-launch | Gamemaster · Fluid Projectile | 0.880 | Sonniss GDC 2.0 |
| rocket-launch | CB Sound Design · Spell Propulsion | 0.985 | Sonniss GDC 2.0 |
| rocket-launch | CB Sound Design · Epic Acceleration | 1.485 | Sonniss GDC 2.0 |
| rocket-launch | Chupapsound · Low Sci-Fi Pass | 1.500 | Sonniss GDC 2.0 |
| rocket-launch | Rescopic · Pure Sci-Fi Acceleration | 1.185 | Sonniss GDC 2.0 |
| rocket-launch | SmartSoundFX · Smooth Air Launch | 0.685 | Sonniss GDC 2.0 |
| rocket-launch | PMSFX · Futuristic Flyout | 1.500 | Sonniss GDC 2.0 |
| rocket-launch | Soundbankster · Space Vehicle Boost | 1.500 | Sonniss GDC 2.0 |
| rocket-launch | Sound Spark · Fast Propulsion | 0.705 | Sonniss GDC 2.0 |
| rocket-launch | Airborne Sound · Soft Forceful Launch | 0.365 | Sonniss GDC 2.0 |
| rocket-launch | Airborne Sound · Arcade Whir | 0.285 | Sonniss GDC 2.0 |
| rocket-launch | L.A. Sounds · Power Launch | 1.500 | Sonniss GDC 2.0 |
| rocket-launch | Soundrangers · Morphing Launch | 1.500 | Sonniss GDC 2.0 |
| rocket-explosion | c2caudio · Warped Detonation | 2.000 | Sonniss GDC 2.0 |
| rocket-explosion | Gamemaster · Compact Game Blast | 2.000 | Sonniss GDC 2.0 |
| rocket-explosion | Gamemaster · Electric Surge Blast | 1.480 | Sonniss GDC 2.0 |
| rocket-explosion | Bluezone · Combat Drone Detonation | 2.000 | Sonniss GDC 2.0 |
| rocket-explosion | David Dumais · Fluid Detonation | 1.960 | Sonniss GDC 2.0 |
| rocket-explosion | Rogue Waves · Arcade Explosion | 1.320 | Sonniss GDC 2.0 |
| rocket-explosion | Bluezone · Steampunk Flare Blast | 1.520 | Sonniss GDC 2.0 |
| rocket-explosion | Async Audio · Sci-Fi Blast | 0.805 | Sonniss GDC 2.0 |
| rocket-explosion | Bluezone · Mothership Explosion | 1.360 | Sonniss GDC 2.0 |
| rocket-explosion | David Dumais · Short Magic Blast | 0.880 | Sonniss GDC 2.0 |
| rocket-explosion | David Dumais · Contemporary Arcade Blast | 1.300 | Sonniss GDC 2.0 |
| rocket-explosion | ShapingWaves · Pixel Detonation | 0.736 | Sonniss GDC 2.0 |
| rocket-explosion | SoundMorph · Future Launcher Impact | 1.200 | Sonniss GDC 2.0 |
| rocket-explosion | Blik Blinks · Composite Sci-Fi Impact | 2.000 | Sonniss GDC 2.0 |
| rocket-explosion | David Dumais · Soft Fluid Impact | 1.800 | Sonniss GDC 2.0 |
| rocket-launch | Michel Baradari · Fire Projectile | 0.563 | CC BY 3.0 |
| rocket-launch | Michel Baradari · Rocket Launch | 1.500 | CC BY 3.0 |
| rocket-launch | dklon · Compact Rocket | 1.500 | CC BY 3.0 |
| rocket-launch | mikeask · Missile Launch | 1.500 | CC0 1.0 |
| rocket-explosion | Michel Baradari · Compact Explosion | 1.300 | CC BY 3.0 |
| rocket-explosion | qubodup · Designed Explosion | 1.020 | CC0 1.0 |
| rocket-explosion | Joth · Chunky Explosion | 2.000 | CC0 1.0 |
| rocket-launch | rubberduck · Sci-Fi Rocket | 1.500 | CC0 1.0 |
| rocket-explosion | rubberduck · Sci-Fi Explosion | 0.825 | CC0 1.0 |
| rocket-explosion | Kenney · Compact Crunch Explosion | 1.180 | CC0 1.0 |
