# Continuous Laser — подборка для сравнения

29 октября 2026. **Добавлено 13 разных Loop-кандидатов. Start — 0, End — 0.** До целевых 20–30 подборка не дополнена дублями, короткими выстрелами и неподходящими записями ради количества. Это предварительный отбор для прослушивания, а не гарантия комфорта при многоминутном повторении.

[Открыть Continuous Laser](http://127.0.0.1:8765/?category=continuous-laser). В карточках доступны локальные оригиналы, подготовленные петли и **«Повтор петли · 60 с»**. Бесконечный повтор включается в нижнем плеере. Корзина, Removed, восстановление, заметки и избранное используют прежние механизмы.

## Состав

| Направление | Количество |
|---|---:|
| Soft Energy Beam | 4 |
| Electric Beam | 2 |
| Plasma Beam | 1 |
| Arcade Laser | 1 |
| Ambient Energy | 5 |

| Кандидат | Направление | Петля, с |
|---|---|---:|
| JC Sounds · Soft Energy Loop | Soft Energy Beam | 3,88 |
| JC Sounds · Futuristic Electric Flow | Electric Beam | 15,88 |
| JC Sounds · Muffled Energy Field | Ambient Energy | 6,88 |
| Gamemaster · Quiet Sci-Fi Hum | Ambient Energy | 14,09 |
| PMSFX · Hologram Energy Loop | Soft Energy Beam | 7,58 |
| PMSFX · Airy Futuristic Flow | Soft Energy Beam | 7,88 |
| CB Sound Design · Hologram Stream | Arcade Laser | 7,88 |
| CB Sound Design · Exoplanet Field | Ambient Energy | 7,88 |
| Glitchedtones · Granular Energy Flow | Ambient Energy | 7,88 |
| zeroisnotnull · Seamless Energy Emission | Soft Energy Beam | 5,12 |
| jdagenet · Sci-Fi Energy Drone | Ambient Energy | 7,88 |
| Kenney · Digital Energy Texture | Electric Beam | 4,88 |
| Kenney · Circular Sci-Fi Flow | Plasma Beam | 4,88 |

Пять предварительных рекомендаций отмечены через shortlist, отдельно от избранного: **JC Soft Energy, JC Futuristic Electric Flow, PMSFX Airy Flow, Seamless Energy Emission, PMSFX Hologram**. Основание — тип исходника и устойчивость тела петли; субъективной оценки тембра это не заменяет.

Ambient Energy — спокойные альтернативы из смежных игровых библиотек, менее узнаваемые как оружие. Exoplanet Field и jdagenet имеют тональный оттенок; Granular Flow — более заметную модуляцию; Circular Flow — пульсацию. Ограничения указаны в карточках. Сравните их с Soft Energy вариантами в реальном миксе.

## Источники и права

- **3 записи JC Sounds**: [Sci-Fi Pack Vol 1](https://opengameart.org/content/jc-sounds-sci-fi-pack-vol-1), CC BY 4.0. Обязательны автор, источник, лицензия и указание обработки.
- **6 записей Sonniss GDC** из Gamemaster Audio, PMSFX, CB Sound Design, Glitchedtones. [GDC License 2.0](https://sonniss.com/gdc-bundle-license/) разрешает коммерческие игры и обработку без обязательной атрибуции. Запрещены отдельная раздача/продажа SFX-библиотеки и обучение AI/ML. Каталог остаётся локальным. Сохранены URL архива, archiveMember, CRC, SHA-256 и студия каждой записи.
- **Seamless Energy Emission — zeroisnotnull**: [OpenGameArt](https://opengameart.org/content/seamless-energy-emission-loop), CC0. Готовая петля из Laser Asteroids.
- **Sci-Fi Drone Loop — jdagenet**: [OpenGameArt](https://opengameart.org/content/sci-fi-drone-loop), CC BY 3.0. Требуются автор, источник, лицензия и изменения.
- **2 записи Kenney**: [Sci-fi Sounds 1.0](https://kenney.nl/assets/sci-fi-sounds), CC0. Использованы неизменённые файлы ранее скачанного официального ZIP: в активном каталоге этих звуков не было. Лицензия внутри ZIP и страница источника сохранены. По одному представителю Computer Noise и Circular Engine; номерные вариации не добавлялись.

Итого: CC0 — 3, CC BY 4.0 — 3, CC BY 3.0 — 1, Sonniss — 6. Полное происхождение и готовые кредиты находятся в `dist/licenses/cl-*/`. Для выпуска игры перенесите необходимые кредиты в титры. Не публикуйте каталог с Sonniss как самостоятельную библиотеку.

## Подготовка и ограничения оценки

Оригиналы WAV/OGG сохранены побайтно отдельно от превью. Для части длинных атмосфер выбран устойчивый фрагмент без стартового нарастания и финального затухания. Начало и длительность указаны в preparation.json и карточке.

Метод сравнения: PCM24/48 kHz, исходные mono/stereo; HP55/LP6500; equal-power перекрытие 120 мс без вставки тишины; постоянное усиление к **−28 LUFS** на одинаковых 60-секундных повторениях, запас до **−10 dBTP**. Нет изменения высоты/темпа, компрессии и новых синтетических слоёв. Демонстрации повторения не считаются отдельными кандидатами.

Проверен технический скачок на стыке относительно обычных изменений отсчётов внутри записи. Это не доказывает незаметность изменения тембра или отсутствие утомления. Браузерная проверка выполняется без звука. **Оценка на слух, на Android-динамике и в боевом миксе не выполнена.**

## Отбор и дубликаты

До копирования используется ImportGate.allow, при фиксации под общей блокировкой — CatalogStore.import_records. Проверяются актуальные active и Removed: ID/URL, конкретный член ZIP, SHA-256 и консервативное сходство PCM. В начале и конце поиска Removed пуст; индекс не очищался.

Повторно не добавлены уже имеющиеся в Flamethrower Deep Bass Arc, Magic Electricity Loop33, Anime Beam 004. Исследовательские копии могут оставаться, но в новой категории их нет. Резкие Electrostatic/Distant Wave, Noise Loops, неустойчивые нарастания и тяжёлые басовые дроны отсеяны. Близкие JC Muffled v2, Near Engine и серии однотипных Kenney-вариаций не использованы для количества.

Исследованы также Freesound и 99Sounds. Часть страниц/загрузок возвращала 403/406 или требовала вход; доступ не обходился. Найденные unfa Weapons Beam Loop и Thimblerig Forcefield Ambience не выдаются за скачанные. Artninja Death Ray исключён из-за указанного NonCommercial-компонента. Платные файлы и AI-генераторы не использованы.

## Проверки

- 39 файлов: 13 оригиналов, 13 превью, 13 повторов — декодирование FFmpeg, SHA-256, HTTP Range 206.
- Все 82 прежние записи, категории и объекты сохранены. SHA-256 избранного/заметок совпал с baseline.
- `node validate.mjs`: **95 sounds, 8 objects, 3 categories**; локальные ссылки, лицензии, хеши и демонстрации корректны.
- `node --check dist/app.js`, `test_archive_ui.mjs`, `test_automatic_fire.mjs` — успешно.
- `tools/test_removed.py`: **13 тестов прошли**, включая перенос/возврат, восстановление после сбоя, блокировку переименованного/перекодированного дубликата и сохранение отметок.
- Браузерная проверка: **39/39 файлов воспроизведены до конца без ошибок**, включая оригиналы и 60-секундные повторы. Выполнена без звука с ускорением ×4; отчёт — `.local/research/continuous-laser/browser-playback.json`. Обычная страница категории открыта и визуально проверена: показано 13 из 95, актуальное описание, прежние категории сохранены.

Исходники, страницы, измерения, baseline и отчёты: `.local/research/continuous-laser/`. Загрузчик — `tools/research_continuous_laser.py`, импортёр — `tools/import_continuous_laser.py`. Повторный импорт пропускает готовые ID и заново проверяет Removed. Unity/FMOD, игровые сцены, волны и сохранения не изменялись.

Сеанс Computer Use завершён; использованный основной процесс остановлен. Swift-помощник автоматически появился снова с новым PID со стороны ChatGPT; приложение ChatGPT не закрывалось. Сервер и браузер каталога оставлены работающими. Переиндексация codebase-memory запрошена, но index_repository и контрольный index_status вернули `Transport closed`; это не влияет на проверки каталога.
