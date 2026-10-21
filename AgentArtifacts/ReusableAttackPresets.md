# Независимые пресеты снарядов и атак

В `AttackBehaviour → Shooting Behaviour` появился **Attack Pattern Preset**, а в **Projectile Settings** — отдельный **Projectile Preset**. Эти ссылки работают независимо. Существующий пресет всего `DirectedWaveAttackBehaviour` сохранён.

## Использование

1. Настройте снаряд и нажмите **Save As New** в Projectile Settings. Сохраняются тип/префаб, цвет/характеристика, скорость и её рандомизация, движение, кривые, размер, Lifetime, Laser и полное дерево Child Attack Pattern.
2. Отдельно сохраните Attack Pattern: Fire/Aim, Emission Origin/Layout, Fan/Stack/Spread, вращение, порядок Sector, залпы, Burst и перезарядку.
3. Выберите любую пару пресетов. Смена паттерна сохраняет снаряд; смена снаряда сохраняет геометрию и расписание атаки.
4. **Linked** наследует изменения Asset. **Override Parameter…** включает локальный параметр: он отмечен `●`, **Reset** возвращает наследование. Остальные унаследованные значения доступны для просмотра и защищены от случайного редактирования.
5. **Copy** создаёт независимую конфигурацию. Ссылка остаётся для явных Apply/Revert. При переключении Linked → Copy сохраняются текущие эффективные значения, включая локальные переопределения.

**Apply Preset / Revert** повторно применяют выбранную категорию и сбрасывают её локальные переопределения. **Update Preset** записывает эффективную конфигурацию обратно в выбранный Asset и обновляет другие Linked-атаки. **Unlink** сохраняет текущие значения и убирает ссылку.

Attack Pattern Asset может иметь **Optional Projectile Preset**. Он применяется исключительно кнопкой **Apply linked Projectile Preset** и не заменяет снаряд при обычном применении паттерна.

Если у снаряда нет собственного префаба, используется прежний префаб соответствующего типа из исполнителя врага. При сохранении через Inspector этот префаб по возможности включается в Projectile Preset. Для независимого снаряда скорость больше не зависит от старого значения скорости Stack; старые конфигурации без новых пресетов сохраняют прежнее поведение.

## Библиотека и Preview

Меню: **Tools → Waves → Attack and Projectile Preset Library**.

Библиотека содержит поиск по имени/описанию/тегам, фильтр категории, Rename, Duplicate, метаданные и редактирование Asset. Кнопка **Library** в конкретной атаке также задаёт цель для **Apply to current attack**.

Для Preview выберите вторую половину пары: Projectile для паттерна или Attack Pattern для снаряда. При необходимости укажите Fallback Projectile Prefab. Нажмите **Play**, затем **Focus** для просмотра в Scene View. **Repeat Same Seed** перезапускает генерацию с тем же Seed, **New Seed** меняет Seed; после перезапуска нажмите Play. **Stop / Clear**, закрытие окна, смена Play Mode и domain reload освобождают временные объекты и ECS World.

Preview использует существующие расписания, геометрию и симуляцию снарядов/Child/Laser. В отдельной библиотеке источник расположен в `(0, 2)`, цель — в `(0, -3)`; реальное движение врагов и групп смотрите через Preview Wave. Если игровой камеры нет, отдельный Preview использует ортографическое поле 9:16. Запуск Preview защищён существующими ограничениями количества снарядов.

Примеры: `Assets/Configs/AttackPresets/Examples/`:

- `OrbitBoundary_Projectile` — Orbit с дочерней атакой по границе экрана и задержкой.
- `BoundaryChild_Projectile` — отдельный снаряд дочерней атаки.
- `Fan_AttackPattern` — Fan из трёх направлений.
- `SequentialSector_AttackPattern` — последовательный Sector с Burst и PingPong.

Примеры не назначены существующим волнам. Assets создаются также через **Create → Game → Waves → Projectile Preset / Attack Pattern Preset**.

## Вложенность и приоритеты

Внутри Child Attack Pattern доступны те же две ссылки. Дочерняя конфигурация копируется глубоко; ссылки на Assets остаются ссылками. Локальная замена самого дерева Child выполняется как единый override `Child Attack`; внутри дерева можно независимо связать/настроить вложенные паттерны. Унаследованные кривые и остальные обычные вложенные настройки допускают отдельные overrides.

Циклические зависимости пресетов запрещены. Существующие ограничения глубины и бюджета Child сохранены. Runtime-состояние расписаний, рандомизации и Sector не хранится в Assets.

Пресеты разрешаются перед существующими Local Overrides и общими runtime-переопределениями скорости. Существующий порядок приоритетов Local Overrides сохранён. Поддержаны Single, Multiple, Sequence, атаки входного маршрута и WaveAttackCoordinator.

## Реализация

- `Assets/Scripts/Gameplay/Enemies/EnemyProjectilePreset.cs`, `EnemyAttackPatternPreset.cs` — Assets поверх существующих моделей.
- `EnemyPresetFields.cs`, `EnemyBurstAttackSettings.Presets.cs`, `EnemyShootingSettings.Presets.cs` — границы категорий, копирование, ссылки, разрешение и кэширование.
- `EnemyPresetDependencies.cs` — проверка циклов.
- `Assets/Editor/EnemyPresetInspector.cs`, `EnemyReusablePresetEditor.cs` — общий Inspector и overrides.
- `EnemyPresetLibraryWindow.cs`, `EnemyPresetPreviewSession.cs` — библиотека и Preview.

## Проверка

- Unity recompile завершён, domain reload пройден; Console: 0 текущих ошибок, новых ошибок после проверок нет.
- `Assembly-CSharp.csproj` и `Assembly-CSharp-Editor.csproj` собраны с `--no-restore --verbosity:quiet --nologo`, ошибок нет. Служебные файлы NuGet восстановлены с явного разрешения пользователя. В чистой сборке игрового кода — 118 предупреждений существующих полей/зависимостей.
- 55 проверок ядра: независимость категорий, Linked/Copy/Unlink, отдельные overrides, глубокое копирование, Laser, дочерние ссылки и циклы, Multiple, runtime-приоритеты и порядок Sector.
- 542 проверки Editor: Undo/Redo, Prefab Overrides, Inspector, Seed, очистка Preview, обычный ECS-исполнитель и Preview Wave/Sequence.
- Повторная загрузка сохранённых Assets после domain reload: 6 проверок, ссылки и значения сохранены.
- Совместимость: 7 волн `1_1…1_4_extrime` и `2_1…2_3_extrime`, 29 подволн, 259 слотов; файлы волн не изменились.
- Регрессии: Laser — 449 проверок; Sector PingPong — 585; Child Boundary — 3496; Attack Sequence — 32; WaveAttackCoordinator — 50.
- Библиотека и оба редактора Assets открыты через Unity API; изображения Preview и библиотеки просмотрены. Артефакты: `projectile-presets-library.png`, `projectile-presets-projectile-preview.png`, `projectile-presets-sector-preview.png`.
- Unity Test Runner не обнаружил EditMode-тестов; проверки выше выполнены скриптами через Pipeline. Полное закрытие/открытие Editor, полный бой в Play Mode и сборка на устройство не проверялись.

Рабочее дерево изначально содержало многочисленные изменения. Они не откатывались. Отчёт по исходникам этой задачи: `projectile-presets-task.diff`; остальные изменения, включая редакторы Bosses, в него не включены.
