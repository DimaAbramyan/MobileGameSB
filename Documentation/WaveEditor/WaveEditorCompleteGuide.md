# Wave Editor: полное руководство по текущей реализации

Срез исходников: **24 октября 2026**, Unity 6000.3.2f1. Документ описывает рабочее дерево, включая незакоммиченные изменения. Это исследование кода, а не протокол испытаний на устройстве. Play Mode, сборки и массовые тесты в рамках исследования не запускались; игровые исходники и assets не редактировались.

Навигация: [справочник каждого файла](WaveEditorScriptsReference.md) · [матрица, ограничения и реестры](WaveEditorFeatureMatrix.md) · [практические рецепты](WaveEditorPracticalExamples.md).

В ссылках на исходники путь относительно корня проекта. Значения по умолчанию в справочнике — инициализаторы C#, а не обещание, что существующий prefab имеет такое же значение. Наличие поля в модели не означает, что Custom Inspector показывает его в каждом режиме. Исторические поля миграции отдельно отмечены в справочнике.

## Оглавление

1. [Архитектура и единицы](#архитектура-и-единицы).
2. [Level и Wave](#level-и-wave).
3. [Состав Subwave](#состав-subwave).
4. [Formation и Spawn](#formation-и-spawn).
5. [Phase Entry и кривые](#phase-entry-и-кривые).
6. [Post Behaviour](#post-behaviour).
7. [Attack Behaviour](#attack-behaviour).
8. [Attack Sequence](#attack-sequence).
9. [Attack Order](#attack-order).
10. [WaveAttackCoordinator](#waveattackcoordinator).
11. [Emission Origin и Layout](#emission-origin-и-layout).
12. [Layout Rotation и Facing](#layout-rotation-и-facing).
13. [Projectile Settings](#projectile-settings).
14. [Composite Projectile](#composite-projectile).
15. [Child Attack Pattern](#child-attack-pattern).
16. [Laser и Telegraph](#laser-и-telegraph).
17. [Рандомизация и ввод чисел](#рандомизация-и-ввод-чисел).
18. [Пресеты и приоритеты](#пресеты-и-приоритеты).
19. [Wave Preview Player](#wave-preview-player).
20. [Scene View и границы](#scene-view-и-границы).
21. [Дополнительные возможности](#дополнительные-возможности).
22. [Проверка авторской волны](#проверка-авторской-волны).

## Архитектура и единицы

Рабочая цепочка: `LevelConfig.waves` → `WaveManager` → `Wave`/`WaveSubWaveCue` → `InfoAboutSubWave` → `DirectedEnemySubWave` → компоненты Formation/Phase Entry/Attack/Post/Completion. Контроллеры атак вызывают `IWaveAttackExecutor`; `SimpleEnemyAttackExecutor` разворачивает геометрию, а `EnemyProjectileEcsSpawner` создаёт ECS-сущности. `EnemyAttackProjectileBuilder.AppendEmitter` — общий расширитель Fan/Stack/Spread для обычных и дочерних атак. [Wave](../../Assets/Scripts/Gameplay/Waves/Wave.cs), [контракты](../../Assets/Scripts/Gameplay/Enemies/IWaveAttackExecutor.cs), [builder](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackProjectileBuilder.cs).

Editor не является единственной сценой исполнения: `WavePreviewPlayer` управляет временем, а `WaveConductorShootingPreview` и `WaveShootingPreviewSession` выполняют расписания и воспроизводят состояние. Для движения формации вызывается `DirectedEnemySubWaveSimulation`; для снарядов используется отдельный ECS World. Существуют также старый встроенный Preview подволны, статические shooting guides и Preview библиотеки пресетов. Это разные режимы наблюдения с разными пределами, а не синонимы Play Mode. [Preview player](../../Assets/Editor/WavePreviewPlayer.cs), [ECS Preview](../../Assets/Editor/EnemyProjectilePreviewWorld.cs).

Позиции и радиусы обычно в мировых единицах, углы — в градусах. Для `EnemyStationaryFireSettings` положительный угол поворачивает от up влево: 0° вверх, 90° влево, 180° вниз. У `EnemyRotationSettings` направление задаётся отдельным enum; clockwise даёт отрицательный Z-угол. У Composite собственная формула вращения: нельзя переносить знак угла между всеми подсистемами без проверки. `duration`, интервалы и cooldown атак используют игровое время; эффекты завершения Wave отдельно используют реальное время без паузы. [Stationary](../../Assets/Scripts/Gameplay/Enemies/EnemyStationaryFireSettings.cs), [Rotation](../../Assets/Scripts/Gameplay/Enemies/EnemyRotationSettings.cs), [Composite](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyCompositeProjectileSettings.cs), [WaveManager](../../Assets/Scripts/Gameplay/Waves/WaveManager.cs).

Различайте пять порядков: список Wave в Level; startDelay подволны; Spawn Order слотов; Attack Order врагов; порядок шагов Attack Sequence. Coordinator разрешает/запрещает работу атак выбранных подволнов и не создаёт снаряды самостоятельно. Ни один из этих порядков не следует автоматически считать заменой другого.

## Level и Wave

### Level Configuration

`LevelConfig` — ScriptableObject с id, displayName, difficulty, requiredLevel, bonusLevel, наградами gold/core, metalPickupPrefab и покомпонентным планом metal drops для волн, множителями hull/shield/fireRate, музыкой, параллаксом и массивом `waves`. `LevelDefinitionConfig`/`LevelCatalog` группируют определения и доступные уровни; `LevelLoader` выбирает конфигурацию и загружает общую Fighting-сцену. Это не редактор отдельных траекторий. [LevelConfig](../../Assets/Scripts/Gameplay/Levels/LevelConfig.cs), [LevelConfigEditor](../../Assets/Editor/LevelConfigEditor.cs), [LevelLoader](../../Assets/Scripts/Gameplay/Levels/LevelLoader.cs).

`LevelConfigEditor` дополнительно строит сведения о составе волн/подволнов. Это анализ ссылок и количества врагов в Inspector; он не подтверждает, сколько врагов переживёт бой. Множитель урона `enemyDamageMultiplier` сохранён как legacy, но актуальные API возвращают 1: изменение скрытого поля не является рабочей настройкой сложности.

### Wave conductor

В Inspector `Wave` основной источник — `scheduledSubWaves`: каждая запись содержит prefab подволны и `startDelay ≥ 0`. Задержка относится к началу данной Wave с учётом общего danger warning, а не к смерти предыдущей подволны. Несколько записей с одинаковой задержкой могут активироваться вместе. Старый `SubWavesToCreate` служит fallback; кнопка **Copy Legacy Subwaves To Schedule** переносит ссылки явно. [Wave.GetEffectiveSchedule/Prepare/Begin](../../Assets/Scripts/Gameplay/Waves/Wave.cs), [WaveEditor](../../Assets/Editor/WaveEditor.cs).

`Prepare` заранее создаёт неактивные экземпляры через Zenject, подписывает завершение, строит статические планы, регистрирует участников Coordinator. `Begin` запускает предупреждение и активации. Пустой или полностью некорректный состав не создаёт бесконечное ожидание: код сообщает проблему и завершает Wave. Очистка всех зарегистрированных подволнов передаёт управление `WaveManager`. Не путайте подготовку неактивных объектов с их фактическим появлением.

В разделе Wave доступны открытие **Wave Preview Player**, старый **Preview Wave Schedule**, **Stop Preview**, позиция/спрайт виртуального игрока, **Show Attack Patterns**, а также постоянные **Show Camera Bounds: ON/OFF** и **Show All Enemy Silhouettes: ON/OFF**. Последние — состояние Editor Session, не сериализованные настройки волны. [WaveEditor](../../Assets/Editor/WaveEditor.cs), [WaveSceneVisualization](../../Assets/Editor/WaveSceneVisualization.cs).

## Состав Subwave

`DirectedEnemySubWave` — координатор. В компонентной конфигурации Inspector показывает строки **Formation (required)**, **Phase Entry**, **Attack Pattern**, **Post Behaviour**, **Completion**, с Add/Select/Remove; Formation нельзя удалить кнопкой как необязательную часть. Добавление пустого компонента к старой подволне не означает автоматическое копирование старых значений. Предусмотрен отдельный мигратор. [компоненты](../../Assets/Scripts/Gameplay/Waves/DirectedWaveSubWaveBehaviourBlocks.cs), [конфигурация/совместимость](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveBehaviourConfiguration.cs), [редактор](../../Assets/Editor/DirectedEnemySubWaveEditor.cs).

Конфиги `DirectedWaveFormationConfiguration`, `DirectedWavePhaseEntryConfiguration`, `DirectedWavePostBehaviourConfiguration`, `DirectedWaveCompletionConfiguration` хранят авторские значения. Partial-части `DirectedEnemySubWave` читают их через общий слой разрешения компонентов и legacy snapshots. Старые сериализованные маршруты не следует вручную переносить в новые поля по похожему имени. [модели конфигурации](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveBehaviourConfigs.cs).

Completion сейчас фактически означает «спавн закончен и живых созданных врагов больше нет». Наличие `enabled`/`completeWhenAllEnemiesAreDefeated` в модели не означает существования набора альтернативных условий победы. Это прямо зафиксировано комментарием модели и `TryComplete` основной подволны. [TryComplete](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWave.cs).

## Formation и Spawn

### Spawn

Поля: `enemyPrefab`, `enemyCount` (1), `spawnInterval` (0.2 s), `spawnOrderMode`, `spawnOrderAngle`, `spawnOrderStartAngle` (90°), `parentEnemiesToSubWave` (true). Если слот имеет собственный prefab, используется он; пустой общий prefab допустим только при корректно заполненных нужных слотах. Реальное число определяется `GetEffectiveEnemyCount`, а для свободных/Transform-точек зависит от соответствующего списка. [основная подволна](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWave.cs), [factory](../../Assets/Scripts/Gameplay/Waves/DirectedWaveEnemyFactory.cs).

Spawn Order: `Manual` — исходный порядок слотов; `DirectionAngle` — проекция на направление; `CenterToOutside`/`OutsideToCenter` — расстояние от центра; `Clockwise`/`CounterClockwise` — угловой обход с начальным углом. Сортировка выдаёт порядок появления, сохраняя идентичность formation slot. **Rebuild Points From Spawn Order** запекает вычисленный порядок в точки, то есть уже меняет авторские данные, а не только подсветку. [resolver](../../Assets/Scripts/Gameplay/Waves/DirectedWaveSpawnOrderResolver.cs), [Inspector](../../Assets/Editor/DirectedEnemySubWaveEditor.cs).

### Геометрия

| Layout | Назначение и параметры |
|---|---|
| `HorizontalLine`, `VerticalLine` | Линия относительно formationCenter; шаг задаётся соответствующей компонентой spacing. |
| `Grid` | columns/rows, spacing, редактируемая матрица заполнения; **Build Full Matrix**, **Apply Matrix To Points**. |
| `VShape` | Две ветви с шагом spacing; подходит для фронта лидера и ведомых. |
| `Arc` | arcRadius, arcDegrees; дуга размещения врагов, не тип движения Arc. |
| `Circle` | shapePointCount, shapeRadius, shapeFlattening; распределение по окружности/эллипсу формации. |
| `Triangle`, `Square`, `Diamond` | Раскладка по контуру фигуры; число точек и радиус/flattening определяют плотность и пропорции. |
| `CustomPoints` | Массив Vector3, индивидуальные prefab overrides, перестановка, генераторы из стандартных фигур. |
| `TransformPoints` | formationPointsRoot и дочерние точки; позиции/повороты/локальные overrides редактируются как Transform. |

Общий решатель [DirectedWaveFormationSolver](../../Assets/Scripts/Gameplay/Waves/DirectedWaveFormationSolver.cs), формы и offset enum — [DirectedWaveModels](../../Assets/Scripts/Gameplay/Waves/DirectedWaveModels.cs). Матрица и генерация/bake — [DirectedEnemySubWaveEditor](../../Assets/Editor/DirectedEnemySubWaveEditor.cs). Matrix не является отдельным видом в enum: это инструмент построения точек.

`formationCoordinateSpace`: `World`, `LocalToSubWave`, `LocalToSpawnPoint`. `formationCenter` по умолчанию (0,2.5,0), spacing (0.75,0.75). `formationOffsetStep` добавляет смещение на слот после базовой геометрии. `formationOffsetOrigin`: FirstEnemy, LastEnemy, Middle. При Middle `Linear` даёт подписанные индексы -2,-1,0,1,2; `Symmetric` — 2,1,0,1,2. При чётном количестве центр находится между двумя слотами. Offset не заменяет шаг основной формации.

**Freeze / Bake Current Formation** фиксирует текущую форму в точках; **Unfreeze Formation** возвращает управление процедурными параметрами. Это операция редактирования с Undo, не режим заморозки времени в игре. Кнопки Match Enemy Count, Fill From Horizontal Line, Create/Match Child Points, Add Enemy Override Fields To Slots ускоряют создание и привязку точек. Подробный Inspector выбранного слота позволяет менять prefab, sprite tint и локальную атаку; старый rotation override вынесен как legacy. [редактор](../../Assets/Editor/DirectedEnemySubWaveEditor.cs), [override](../../Assets/Scripts/Gameplay/Waves/DirectedWaveEnemyOverride.cs).

Пример: Grid 6×4 → выключить центральные клетки → Apply Matrix To Points → назначить боковым слотам особые enemy prefab. Spawn Clockwise меняет порядок появления, но Attack Order остаётся отдельным выбором.

## Phase Entry и кривые

`entranceMode=Checkpoints`: общий `spawnPoint`, `pathCoordinateSpace`, массив pathCheckpoints. Точка хранит position, `durationToNext` (0.5), `speedToNext` (1), `motionToNext` (CatmullRom), `easeToNext` (Linear), curveSettings. Duration/Speed редактируются как метрики сегмента; не следует интерпретировать speed как независимую вторую траекторию. Editor пересчитывает метрики при изменении геометрии, учитывая длину. `DirectedWavePathEvaluator.GetParameterAtNormalizedDistance` приближает длину 24 отрезками. [path evaluator](../../Assets/Scripts/Gameplay/Waves/DirectedWavePathEvaluator.cs), [curve editor](../../Assets/Editor/DirectedEnemySubWaveCurveEditor.cs).

| Motion To Next | Настройки и смысл |
|---|---|
| `Linear` | Прямой отрезок. Ease задаёт прогресс по времени. |
| `Bezier` | Ручки относительно checkpoint: Out у начала, In у конца. `Auto` сохраняет старые автоматические касательные; `Free` независим; `Aligned` связывает направление, сохраняя длину противоположной ручки; `Mirrored` задаёт противоположный вектор той же длины. |
| `CatmullRom` | `tension` 0…1 масштабирует касательные; `Uniform`, `Centripetal`, `Chordal` выбирают узловую параметризацию. Uniform+tension=0 содержит отдельную ветку старой формулы. |
| `Arc` | Круговая дуга между checkpoint: `bulge` -1…1 — подписанная стрелка прогиба относительно половины хорды; положительное значение изгибает влево от направления сегмента. `longArc` выбирает длинную дугу. Нулевой прогиб/совпадение точек вырождаются в отрезок. |

Ручки Bezier, касательные и изгиб Arc доступны в Scene View и числовых полях. Изменение одной ручки может изменить соседний сегмент при Aligned/Mirrored — это связь ручек общего checkpoint, а не ошибка. Геометрия, easing и длительность — три разных уровня. [SetHandle](../../Assets/Scripts/Gameplay/Waves/DirectedWaveModels.cs), [CurveEditor](../../Assets/Editor/DirectedEnemySubWaveCurveEditor.cs).

`IndividualPoints` задаёт отдельный старт каждого слота и движение к formation: `individualPointMovementStartDelay` 0.1, duration 0.35, curve EaseInOut. Есть Match Formation и генераторы Circle/Triangle/Rectangle/Diamond со своими центром, радиусом, flattening и rotation. Это не отдельный checkpoint-маршрут на каждого врага. [конфиг](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveBehaviourConfigs.cs), [DrawIndividual...](../../Assets/Editor/DirectedEnemySubWaveEditor.cs).

`rotateEnemiesAlongEntrancePath` поворачивает up врага вдоль движения. Completion входа: `MoveToFormation` с settleDuration 0.35 и settleCurve; либо `LoopEntrancePath`, с entranceLoopStartCheckpointIndex, teleport-to-start и teleportDelay. Повтор входа не является повторным спавном. Для loop существуют отдельные расчёты первого прохода, возвратного сегмента и уведомлений о checkpoint. [EntranceLoopEvaluator](../../Assets/Scripts/Gameplay/Waves/DirectedWaveEntranceLoopEvaluator.cs), [основная подволна](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWave.cs).

Пример: вход сверху по Bezier, затем петля checkpoints 1…3; firing rule на checkpoint 2 создаёт залп на каждом достижении, не ожидая Post Behaviour.

## Post Behaviour

Панель Post Behaviour — редактируемый pipeline `postCommands` после входа/готовности формации. `postStartDelay=0.25`, фиксированное число проходов `postCommandPipelineFixedCount=1` либо `postCommandPipelineLoop`. Команда имеет enabled, completionMode, duration, holdDuration; типоспецифические поля показываются условно. Перестановка ↑/↓ меняет порядок. Preview отдельной команды служит для локальной настройки. [Models](../../Assets/Scripts/Gameplay/Waves/DirectedWaveModels.cs), [handlers](../../Assets/Scripts/Gameplay/Waves/DirectedWavePostCommandHandlers.cs), [timeline](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveTimelineRuntime.cs).

| Команда | Реальная функция / настройки |
|---|---|
| `Wait` | Пауза pipeline, сохранение позиций. |
| `LocalMovement` | Перенос центра текущего состава к targetOffset в выбранном coordinate space, duration/curve; имя Offset не отменяет преобразование целевого центра через ToWorld. |
| `Patrol` | Маршрут patrolPoints: offset, wait, duration/speed/motion/ease до следующей; patrolCoordinateSpace, patrolLoop; те же кривые, что у входа. |
| `Wobble` | Колебания X/Y: wobbleAmplitude (0.25,0.1), frequency 1.5, фаза по SpawnOrder либо Directional. Для Directional — угол и шаг; для SpawnOrder — phaseOffset 0.7. |
| `CircularMovement` | Собственная орбита врага: selfOrbitRadius (0.25,0.25), selfOrbitPhaseOffset 0.35, selfRotationDegreesPerSecond 90. |
| `FormationRotation` | Поворот всей формации вокруг центра. rotationDegrees/curve либо continuousFormationRotation и общая скорость 45°/s. Это движение позиций, не Layout Rotation выстрелов. |
| `FormationMorph` | Переход к morphTarget/списку формаций: layout, centerOffset, columns/rows, arc/shapeRadius, flattening/customPoints, durationToShape, holdDuration/ease; loop/returnDuration/returnCurve. |
| `FormationReorder` | Переназначение слотов: Mirror, Identity (в UI Default), Random. Скорость 5, интервал 0.1, shipsPerBatch 1, seed 12345; optional target center. |
| `Parallel` | Вложенные parallelCommands. Blocking ожидает совокупный блок, Background добавляет движение поверх дальнейшего pipeline. infiniteParallel — продолжение без конечного горизонта. |
| `Loop` | Вложенные loopCommands, loopCount либо infiniteLoop; не создаёт новых врагов. |
| `LegacyAttack` | Сохранён старый номер enum для сериализации. Не новый способ настраивать стрельбу: используйте Attack Behaviour. |

`Timed`, `CompleteRoute`, `Infinite` используются обработчиками, для которых существует естественная длительность; не каждая команда интерпретирует их как одинаковые три режима. Parallel/background складываются с текущими позициями по правилам evaluator; возврат на исходную точку не добавляется автоматически на каждом обороте loop. Infinite/Background могут не допускать перехода к следующей блокирующей команде — проверяйте естественную длительность. [Simulation](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveSimulation.cs), [runtime cache](../../Assets/Scripts/Gameplay/Waves/DirectedWaveTimelineRuntimeCache.cs).

## Attack Behaviour

### Панель и режимы

Компонент `DirectedWaveAttackBehaviour` имеет **Attack Behaviour Mode = Patterns / Sequence**. Внутри Patterns существуют общие attackSettings и список `DirectedWaveAttackPattern` для выбранных слотов. Внутри самого `EnemyBurstAttackSettings` другой переключатель **Attack Patterns = Single / Multiple**: это несколько потоков выстрелов одного executor. Не путайте группировку слотов Wave и потоки одного врага. [компонент](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackBehaviour.cs), [модель Single/Multiple](../../Assets/Scripts/Gameplay/Enemies/EnemyBurstAttackSettings.cs).

**Use Selected Enemy Slots** ограничивает участие. Индексы — formation slot, не текущий порядковый номер выжившего врага. В групповом режиме pattern.selectedEnemySlots распределяет слоты между профилями; разрешение ищет первый подходящий профиль, а Validate удаляет повторные назначения. Чтобы один враг выпускал несколько потоков одновременно, используйте Multiple внутри профиля, а не дублируйте один слот в группах. [TryCopyResolvedAttackSettingsTo](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackBehaviour.cs), [Pattern.Validate](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackPattern.cs).

Fire Mode: `Aimed` целится; `Stationary` использует Angle или фиксированную WorldPoint; `None` отключает выстрелы, позволяя движение; `ForwardWhenPlayerAhead` проверяет forwardFireHalfAngle (15°); `Default` делегирует штатное направление executor. Aim Mode: `AimOnStart` фиксирует прицел на атаку, `AimOnBurst` обновляет в начале очереди, `AimContinuous` — при каждом выстреле, включая отложенный Sector. Без Burst у AimOnBurst есть предупреждение: не следует ожидать новой фиксации для каждой пули. Circle/нестандартный Origin фиксируют прицел относительно фактических эмиттеров. [aim state](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackModels.cs), [Inspector](../../Assets/Editor/DirectedWaveAttackBehaviourEditor.cs).

Scheduling: optional **Delay Attack Start** (значение 1s), Attacks Per Enemy Per Cycle (1), Attacks Per Second (1), Wait For Previous Attack, Delay After Attack (1). Attack Settings Source: `EnemySettings` или `WaveOverride`. В первом случае можно заменить только Attack Cooldown флажком Override Enemy Attack Cooldown; во втором открывается Wave Attack Pattern. Не объединяйте частоту выбора врага контроллером и интервал пуль внутри Burst в одно число. [DirectedWaveAttackModels](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackModels.cs).

### Single, Multiple и Burst

Каждый поток содержит необязательную начальную задержку, attackShotCount (1), attackShotInterval (0.15), attackCooldown (1), Burst None/Enabled; при Enabled — burstShotCount (1), burstShotInterval (0.15). Расписание `EnemyAttackPatternSchedule` хранит оставшиеся внешние выстрелы и пули очереди, момент следующего выстрела и следующей атаки. Burst имеет собственные правила fireRate: нельзя предполагать, что один множитель просто одинаково делит все интервалы. [Schedule](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackPatternSchedule.cs), [Burst settings](../../Assets/Scripts/Gameplay/Enemies/EnemyBurstAttackSettings.cs).

Multiple — плоский список `EnemyAttackPatternSettings` с собственными устойчивыми PatternId; доступны Add Pattern, Duplicate, Remove и сворачивание. Потоки имеют отдельные состояния прицеливания/вращения/таймеров. Верхний Facing Mode выбирает визуальную ориентацию, а не поворачивает все потоки разом. [EnemyAttackPatternSettings](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackPatternSettings.cs), [Burst drawer](../../Assets/Editor/EnemyBurstAttackSettingsDrawer.cs).

### Пикирование

Movement Mode: `None`, `MoveToPlayer`, `FlyThroughDive`. MoveToPlayer использует min/maxDiveDepth (1.5/2.5), min/maxDiveSpeed (4/6), diveSpeedCurve, опциональную подготовку (distance 0.75, duration 0.2, curve), target mode FlyPastPlayer/StopAtPlayerRadius (playerStandoffRadius=2), ReturnSpeedMultiplier (1) и returnSpeedCurve. Minimum Dive Duration и Maximum Dive Speed по умолчанию 0 — отключены. Ограничитель удерживает текущий образец кривой до достижения позиции, поэтому превышение скорости растягивает время, а не телепортирует к цели. Применяется к соответствующему MoveToPlayer approach; не является глобальным ограничением движения формации. [DiveMotionProgress](../../Assets/Scripts/Gameplay/Waves/DirectedWaveDiveMotionProgress.cs), [AttackController](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackController.cs).

FlyThroughDive: approachSpeed=6, exitPadding=0.5, returnMode EntrancePath/ReverseDivePath/TeleportPosition, teleportPosition=(0,7), cooldown=1; отдельные подготовка и кривая. Dive Scheduling WaitForReturn либо StartNextWhileReturning. Во время атаки входной маршрут продолжает рассчитываться отдельно от временно захваченной видимой позиции. [AttackMotion](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveAttackMotion.cs).

### Атаки на входном маршруте

Entrance settings: Enable Entrance Attacks; правила **At Checkpoint** (index, фильтр слотов, shots per enemy), **Across Checkpoints** (start/end index, PerEnemy/TotalForGroup, count, Sequential/Random), и **Continuous Route Attack** с началом LoopRestart или конкретный Checkpoint. `allowAutonomousAttackDuringEntrance` разрешает штатную стрельбу executor, когда отдельные route-rules не ведут атаку. Это отдельные окна расписания; overlaps правил имеют отдельные captures. [EntranceAttackModels](../../Assets/Scripts/Gameplay/Waves/DirectedWaveEntranceAttackModels.cs), [EntranceAttackController](../../Assets/Scripts/Gameplay/Waves/DirectedWaveEntranceAttackController.cs).

## Attack Sequence

Sequence упорядочивает разные атаки, не заменяя внутренние Single/Multiple. Режимы `Once`, `Loop`, `PingPong`, `Random`; loopCount=1, 0 означает бесконечные проходы кроме Once; startStep=0; restartOnReentry=true; waitForPreviousAttack=true; seed=12345 для Random. PingPong строит прямой и обратный обход без двойного выполнения крайних шагов на стыке. Random перемешивает шаги, а на первом проходе учитывает startStep. [Cursor/Player](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackSequence.cs).

Типы шагов: **Attack**, **Pause**, **RepeatBlock**. Attack: pattern/preset, startDelay, repeatCount, waitForCompletion, delayAfterStep, attackSpeedMultiplier, optional Target Slots Override. Pause имеет pauseDuration. RepeatBlock содержит вложенные steps и repeatCount; максимальная глубина 8. В Inspector — ↑/↓, Duplicate, Remove, foldout, Add Sequence Step/Add Block Step. [Sequence editor](../../Assets/Editor/DirectedWaveAttackSequenceEditor.cs).

Wait For Completion учитывает завершение корневой атаки и отложенных запусков. Child Attack ожидается только при `waitForChildAttacks`; default timeout 10s, **Unlimited Child Wait** явно разрешает неограниченное ожидание. Без ожидания могут перекрываться атаки разных слотов, но два активных шага не захватывают один слот одновременно. Resume после прерывания не гарантирует продолжение с середины конкретного Burst: текущий прерванный шаг запускается заново по контракту reentry. [SequenceRuntime](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackSequenceRuntime.cs), [completion tracker](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackCompletionTracker.cs).

Пример: RepeatBlock(3× aimed attack) → radial attack → Pause(0.7) → rotating sector → Loop. Для разрешения следующего шага до завершения долгих детей оставьте waitForChildAttacks=false.

## Attack Order

Флажок enabled по умолчанию false сохраняет прежнее расписание. Режимы: Simultaneous, Sequential, Random, CenterToOutside, OutsideToCenter, LeftToRight, RightToLeft, Clockwise, CounterClockwise, PingPong, SymmetricalPairs, Manual. Параметры: attackIntervalBetweenEnemies=0.15, enemiesPerBatch=1, startSlot=-1 (первая запись порядка), reverseOrder=false, loopOrder=true, randomSeed=12345, orderSource=FormationSlotOrder, waitForPreviousEnemy=false, resetOrderEachCycle=true, manualSlots. [AttackOrder](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackOrder.cs).

Для обычного последовательного обхода FormationSlotOrder опирается на устойчивую нумерацию, SpatialOrder сортирует снимок позиций при построении цикла. Геометрические режимы Center/Outside/Clockwise/CounterClockwise используют позиции независимо от этого переключателя; Left/Right и SymmetricalPairs также принудительно пространственные. Это не непрерывная сортировка движущихся врагов каждый кадр. При resetOrderEachCycle план/seed строятся снова; выключение сохраняет состояние обхода. Manual может повторять заданные слоты; отсутствующие не исполняются. SymmetricalPairs ищет ближайшего партнёра к зеркальной позиции относительно вертикали центра; при отсутствии партнёра слот остаётся одиночным. Размер batch округляется к допустимым парам (минимум 2); Enemies Per Batch разрешает несколько одновременных участников. Погибший враг пропускается по своему slot ID, а не сдвигает смысл оставшихся индексов. [OrderPlan.BuildCycle/Compare](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackOrder.cs), [runtime adapter](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackControllerOrder.cs).

При включённом Attack Order `waitForPreviousEnemy` становится источником семантики ожидания вместо старого waitForPreviousAttack; интервал использует тот же controller, а не второй планировщик. В Sequence порядок применяется внутри каждого Attack шага. Scene guides нумеруют порядок отдельно от Spawn Order. [drawer/guide](../../Assets/Editor/DirectedWaveAttackOrderSettingsDrawer.cs).

## WaveAttackCoordinator

Необязательный компонент на Wave. Его настройки редактируются в нижней панели **Attack Coordinator** Wave Preview Player и собственном Inspector. Группа ссылается на `scheduleIndex` плюс optional subWavePrefab для проверки соответствия: одинаковый prefab в двух schedule slots — два разных участника. [Coordinator](../../Assets/Scripts/Gameplay/Waves/WaveAttackCoordinator.cs), [Editor](../../Assets/Editor/WaveAttackCoordinatorEditor.cs).

Общие настройки: enabled=true, startDelay, loopSequence=true, loopCount=1, globalCooldown, startGroupIndex=0, switchMode, reactivation. **Важно:** при loopSequence=true последовательность бесконечна; loopCount ограничивает проходы, когда loopSequence выключен. randomSeed сейчас **зарезервирован** и не включает случайный порядок групп. [ScheduleNext](../../Assets/Scripts/Gameplay/Waves/WaveAttackCoordinatorPlayer.cs).

Группа: groupName, controlledSubWaves, startDelay, waitUntilReady=true, completionMode, attackCyclesCount=1, duration=5, maxWaitDuration=30 (0 выключает timeout), delayAfterGroup, overlapDuration, onEnemyDeath. Таймаут включает ожидание spawn/entry. Pending означает ещё неготовую подволновую атаку, Gone — исчезнувшего участника; смерть отдельного врага сама по себе не равна исчезновению подволны. [RuntimeParticipant](../../Assets/Scripts/Gameplay/Waves/WaveAttackCoordinator.cs).

Completion: `Timed` — время активного окна; `AttackCycles` — заданное число завершённых циклов относительно activation baseline; `AllCompleted` — один цикл всех участвующих; `FirstCompleted` — один цикл первого. SkipParticipant пропускает полностью погибшего участника, CompleteGroup завершает группу при потере участника, который был жив при её активации. Пустая/уже исчезнувшая группа не должна трактоваться как новый выстрел.

Switch Mode: FinishCurrentAttack переводит уходящую группу в drain и запрещает новые атаки; Interrupt прерывает. Reactivation Restart/Resume задаёт поведение при возврате. Разрешения суммируются по владельцам и группам; активное перекрытие одного участника не должно сбрасывать его повторно. [Permissions](../../Assets/Scripts/Gameplay/Waves/WaveAttackPermissions.cs), [BehaviourPermissions](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackBehaviourPermissions.cs).

Overlap: для Timed следующая передача планируется относительно `StartedAt + duration + delayAfterGroup + globalCooldown - overlapDuration`; для прочих режимов позволяет concurrent drain с ограничением длительности. Это не универсальное «начать на N секунд раньше» для всех completion modes. Нет отдельного события навигации, если внутренний шаг поменялся, а набор активных групп остался прежним.

## Emission Origin и Layout

### Origin — где начинается геометрия

X и Y независимы: Source `Enemy`, `Player`, `World`; Offset. Для World ReferenceMode `Absolute` означает абсолютную координату Offset; `Boundary` — Left/Right или Bottom/Top, плюс отступ внутрь при положительном значении, наружу при отрицательном. Для Player доступен Capture Mode OnAttackStart/OnBurstStart/OnShot. Случайный offset имеет собственный Randomize On и не тождественен обновлению позиции игрока. По умолчанию Enemy+0 на обеих осях. [Origin](../../Assets/Scripts/Gameplay/Enemies/EnemyEmissionOriginSettings.cs), [drawer](../../Assets/Editor/EnemyEmissionOriginSettingsDrawer.cs).

Пример: X=Player, Y=World Boundary Top, Offset=-1 создаёт залп на одну мировую единицу над игровым viewport. Sequential Sector использует снимок Origin одного залпа, а не читает новые offsets для каждой задержанной пули. В Child Attack этот блок скрыт и переопределяется источником Parent+Spawn Offset.

### Layout — как расположены эмиттеры и направления

| Layout | Параметры | Результат |
|---|---|---|
| Point | positionOffset | Одна точка с локальным смещением от shooting point. |
| Radial | directionsCount=4, startAngle | Направления равномерно на 360° из одной точки. |
| Circle | emitterCount=4, radius=1, startAngle, centerOffset | Эмиттеры на окружности; radius мировой, масштаб корабля не делает сам круг эллипсом. |
| Sector | startAngle=0, endAngle=90, directionsCount=4, launchMode | Направления по положительному CCW sweep; оба края включены, при одном направлении — середина. 350→10 пересекает 0; разность ≥360 даёт полный оборот. |

В Aimed сектор центрируется относительно цели перед добавлением Layout Rotation/Fan. Для Circle прицеливание/WorldPoint рассчитываются от реальных точек эмиттеров. Позиционные offset и centerOffset принадлежат разным базисам; Enemy Facing не должен двигать круговой центр. Общие формулы: [GetPosition/GetAngle](../../Assets/Scripts/Gameplay/Enemies/EnemyEmissionLayoutSettings.cs), [Geometry preview](../../Assets/Editor/EnemyShootingPreviewGeometry.cs), [executor](../../Assets/Scripts/Gameplay/Enemies/SimpleEnemyAttackExecutor.cs).

Sequential Sector: Start Delay, Launch Interval, Wait Until Sector Ready, Launch Order Forward/Reverse/PingPong. PingPong Initial Direction и Reset Order On Attack Start видны только для PingPong. Направление меняется после завершённого сектора; отмена незавершённого сектора его не переключает. Состояние принадлежит источнику+паттерну, а не общему preset asset. Продолжительность запуска `startDelay + (count-1)*launchInterval`. Fan/Stack каждого направления выпускаются вместе. [SectorLaunchOrderState](../../Assets/Scripts/Gameplay/Enemies/EnemySectorLaunchOrderState.cs).

### Fan, Stack и Spread

В Shooting Behaviour **Fan Off/Fan**: fanProjectileCount=3, fanMinAngle=-45°, fanMaxAngle=45°. **Stack Off/Stack** использует legacy enum ShotGun и поля shotGunProjectileCount=3, shotGunDirection=(0,-1). **Spread Fixed/Random** использует shootingMode и randomMinAngle=-15°, randomMaxAngle=15°. Stack не является расстоянием между эмиттерами: это размножение элементов направления. Число обычных элементов залпа до ограничений: `EmissionCount × FanCount × StackCount`. Геометрия и случайный spread расширяются общим builder. [ShootingSettings](../../Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.cs), [AppendEmitter](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackProjectileBuilder.cs).

## Layout Rotation и Facing

Layout Rotation включается отдельно. Direction Clockwise/CounterClockwise; Mode Continuous/ByAngle/PingPongByAngle; rotationSpeedDegreesPerSecond=90, rotationProgressCurve Linear, rotationAngle=360, rotationFromAngle=0, resetRotationOnEnable=true. ByAngle duration = abs(angle)/speed; PingPong сперва идёт от 0 к From, затем между From и To. Continuous считает полные обороты и прогресс текущего оборота. `rotationProgressCurve` ограничивается в 0…1. [EnemyRotationSettings](../../Assets/Scripts/Gameplay/Enemies/EnemyRotationSettings.cs).

Facing Mode Default/Fixed сохраняют соответствующую исходную визуальную ориентацию; FollowPattern выбирает один PatternId, facingSpeed=360°/s (0 мгновенно), facingAngleOffset корректирует только спрайт; Rotation имеет собственные facingRotation. Это отдельный контур от вращения залпа. Pattern rotation масштабируется attack-speed там, где выполняется ScaleAttackTiming; физический поворот Rigidbody не следует автоматически считать таким паттерном. [Facing](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackFacing.cs), [AttackRotation](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackRotation.cs).

При Show Shooting Preview Scene View автоматически добавляет From/To, исходящие лучи полного залпа и угловой охват. ByAngle показывает конечный угол, PingPong обе границы, Continuous полный оборот. Обзор ограничивает покрывающие лучи/промежуточные позы ради читаемости, а не меняет Runtime. Старые Show Final Layout/Layout Preview Time не являются обязательным условием этой визуализации. [Rotation guides](../../Assets/Editor/EnemyShootingPreview.Rotation.cs).

## Projectile Settings

Projectile Type в авторском UI: Default, Homing, BurstAtPoint, Laser, Composite; отдельные CompositeCircle/Polygon/SegmentedRing сохранены как совместимые внутренние значения enum. Тип prefab для обычных вариантов выбирает executor, либо explicit projectilePrefab из Projectile Preset. В EnemyProjectileAuthoring: sprite/visualRenderer, characteristic (Fragile), baseDamage=1, radius=0.1, baseLifetime=1, baseSpeed=1, visualScale=(1,1), collisionScale=1, FlightMode Straight/Homing/BurstAtPoint и настройки соответствующего поведения. [тип](../../Assets/Scripts/Gameplay/Enemies/SimpleEnemyAttackExecutor.cs), [authoring](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileAuthoring.cs).

Normal/Fragile определяют допустимое уничтожение перехватчиком; Fragile использует фиксированный розовый `(1,.35,.65,1)`. **Overwrite Projectile Characteristic** переопределяет prefab. **Overwrite Base Colour** управляет цветом, но Fragile визуально фиксирован; цвет не меняет характеристику. Override Projectile Base Speed/Lifetime заменяют базовые значения только при включении соответствующих флагов. [Characteristics](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsComponents.cs), [spawner](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSpawner.cs).

Initial Speed: Default берёт приоритетный base speed; Random выбирает min/max (1…2) и имеет приоритет над фиксированным base speed; Consistent добавляет changePerVolley (-0.1) после целого залпа. Legacy Stack Initial Speed=5 сохраняется для старых конфигураций; новые независимые Projectile Presets отделяют его от геометрии стрельбы. Поле-информатор Base speed в Inspector показывает фактический источник. [SpeedSource](../../Assets/Editor/EnemyShootingSettingsDrawer.cs), [TryGetRandomSpeedRange/InitialSpeed](../../Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.cs).

Movement Pattern:

- Default — штатный Straight/Homing/BurstAtPoint prefab.
- AngularTurn — angular speed (90°/s), знак задаёт сторону.
- Lateral — постоянная добавочная локальная правая скорость (1); отрицательная меняет сторону.
- Orbit — центр SourcePosition/FixedWorldPoint/DNA-Like, radius (0 сохраняет стартовое расстояние), angularSpeed и radialSpeed. Центр SourcePosition — снимок позиции источника при создании, не вечная ссылка на корабль.
- DNA-Like — режим Orbit с общим ритмом пересечения веера; DnaWeaveSpeed=1, half-cycle зависит от angular speed и охвата Fan. Это не дополнительный Fire Mode. Согласованность пересечения зависит от режима случайной скорости: независимые PerProjectile скорости могут визуально расходиться.

[Movement settings](../../Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.cs), [ECS movement/DNA](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSystems.cs), [DNA evaluation](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsComponents.cs).

Speed Behaviour Customized: Speed Change Y, Speed By Distance (кривая по дистанции после достижения Y-порога). Curve Target Both/BaseSpeed/Radial показывается при Orbit или Layout Rotation, default Both. Вне реально существующей радиальной составляющей выбор Radial не создаёт орбиту сам по себе. Size Behaviour Customized: Size Change Y, Size Change Speed=1, Curve Linear, Final Size=1; это множитель исходных размеров спрайта и столкновения. Различайте временной прогресс изменения размера и координату старта эффекта. Speed-порог проверяет знак сохранённого custom-speed направления, size-порог — текущий velocity.y; при практически нулевой Y-компоненте проверка возвращает false. Поэтому чисто горизонтальная пуля не обязана включить Y-эффект только от совпадения координаты. У speed-кривой нет автоматического отсчёта от рождения, пока порог не достигнут. [ECS systems](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSystems.cs).

Destroy Outside Screen=true по умолчанию, Boundary Offset Mode Automatic/Custom. Automatic учитывает актуальный collision radius×scale; Custom — неотрицательный отступ. Для старых данных bool хранится инверсно как disabled=false. Сначала фиксируется вход в область, включая пересечение отрезком между предыдущей и новой позицией; появление снаружи не удаляется немедленно. После входа полный выход помечается Expired и проходит общий cleanup. Lifetime остаётся независимым ограничением. [screen settings](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileScreenSettings.cs), [screen system](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileScreenBoundarySystem.cs).

Homing поворачивает направление с turnSpeedDegreesPerSecond=180 и optional homingDuration. BurstAtPoint использует detonationPoint/radius, waveCount/interval, projectilesPerWave=18, angleStep=20°, startAngle и отдельный burst prefab. Не путайте этот FlightMode с обычным Burst расписания атак и с универсальным Child Attack.

## Composite Projectile

Projectile Type Composite раскрывает Shape: **Circle**, **RegularPolygon**, **SegmentedRing**. Фигура — невидимый центр с движением плюс отдельные ECS-компоненты, а не один круглый увеличенный hitbox. Circle: componentCount=12 (1…256). Polygon: sides=4 (3…64), projectilesPerSide=4; углы не дублируются, число равно sides×(perSide−1) с ограничением. SegmentedRing: segmentCount=3, projectilesPerSegment=4, gapAngle=20°. Gap меньше 360/segments; элементы распределяются с полушаговым отступом, чтобы при нулевом gap не удвоить края. [Spec](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyCompositeProjectileSettings.cs).

Radius Mode Automatic/Custom: Automatic выводит радиус из реального радиуса компонента, componentScale, spacingGap и геометрии; Custom показывает radius (1). StartAngle задаёт геометрию до Composite Rotation. Initial Rotation можно рандомизировать, отдельно от startAngle. Rotation modes None/Continuous/PingPong/AlignToMovement, angularSpeed=60, minAngle=-45, maxAngle=45. AlignToMovement ориентирует фигуру по движению центра, включая Homing. [Composite systems](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyCompositeProjectileSystems.cs), [drawer](../../Assets/Editor/EnemyCompositeProjectileSettingsDrawer.cs).

Components: componentScale=1, optional Override Hit Radius (0.1), damage=1, damageType=Default; prefab, цвет и характеристика берутся из общих Projectile Settings. Кривая размера масштабирует целую фигуру. Столкновения считают компоненты; центр не наносит контактный урон. Для Destroy Outside Screen учитывается envelope фигуры: удаление не по одному лишь центру до выхода остальных элементов. Entity cost = 1+число компонентов, лимит 256 сохраняет целые стороны/сегменты, не обрезает фигуру произвольно.

Ограничения подтверждены не только предупреждением UI, но и Runtime-проверками: component prefab с BurstAtPoint несовместим; Child HitPlayer на центре несовместим, потому что у него нет hitbox; компоненты не запускают собственные вложенные Composite/Child, дочерняя атака принадлежит центру. При этом Child Attack центра может создать следующую фигуру в рамках общих лимитов. [Spawner.Composite](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSpawner.Composite.cs), [Scene preview](../../Assets/Editor/EnemyShootingPreview.Composite.cs).

## Child Attack Pattern

В Projectile Settings включите Use Child Attack Pattern. Trigger Mode: AfterTime (triggerValue от рождения), AfterDistance (фактически пройденный путь), OnEvent: OnSpawn, HitPlayer, Intercepted, Expired, BurstAtPoint, OnScreenBoundaryReached. Pattern использует существующие Fire Mode (Default/Aimed/Stationary), Aim Mode, Single Pattern, Burst, Layout, Fan/Stack, Projectile Settings и следующих детей. Входные enemy/wave/dive настройки сюда не переносятся. [Child settings](../../Assets/Scripts/Gameplay/Enemies/EnemyChildAttackSettings.cs), [runner](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackSystem.cs).

Repeat Count=1 (runtime clamp 1…32) — общее число исполнений. Repeat Interval=1 отсчитывается после завершения всех запланированных выстрелов/Sequential Sector прошлого исполнения. Destroy Parent удерживает естественный lifetime, пока атака активна, затем завершает родителя; попадание/перехват остаются полноценными разрешениями. Для expiry-trigger есть специальный путь от конечной позы. Не распространяйте это исключение на произвольное уничтожение родителя во время boundary delay.

Spawn Offset=(0,0); OffsetSpace World/Local, где local X вправо, Y вперёд по текущему направлению родителя. OriginMode FollowParent использует текущую позу; CaptureOnStart фиксирует позу запуска (для boundary — точку пересечения). Вложенный Emission Origin не редактируется: всегда Parent+Offset. Это не фиксация каждого будущего дочернего снаряда в одной точке при FollowParent.

Screen boundary event показывает независимые Trigger Boundaries Left/Right/Top/Bottom, boundaryOffset, triggerOnce=true, timeOffset=0. Положительный offset внутрь. Проверяется отрезок предыдущая→текущая позиция, поэтому быстрый снаряд может пересечь линию между кадрами. Снаружи при создании события нет; достижение линии с любой стороны считается пересечением. Невыбранные стороны не ограничивают выбранные: снаряд может уйти вверх и позже достигнуть правой линии вне исходного прямоугольника. При нескольких пересечениях в одном шаге выбирается первая доля отрезка, без случайного дубля. [BoundaryIntersection](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackBoundary.cs).

TriggerOnce=false разрешает новые события после последующих пересечений, даже если прежнее исполнение ещё длится; для каждого свой capture/delay. DestroyParent ждёт суммарно запланированную работу. `EnemyChildAttackScreenHold` предотвращает преждевременную screen cleanup для ожидаемой выбранной стороны; это не отключает естественный lifetime. TimeOffset>0 оставляет родителя в движении; уничтожение отменяет недопустимую запланированную работу. [boundary runner](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackBoundaryTrigger.cs), [screen system](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileScreenBoundarySystem.cs).

Предохранители: MaximumDepth=8, MaximumDescendants=2048 на корень, MaximumPerStep=512, MaximumTrackedParents=4096, MaximumLiveProjectiles=8192. Эти лимиты — часть текущего поведения, не параметры дизайнера. Сильное ветвление может дать меньше детей, чем произведение всех чисел в Inspector. [Child constants](../../Assets/Scripts/Gameplay/Enemies/EnemyChildAttackSettings.cs).

## Laser и Telegraph

Laser использует общий attack schedule и раздельный runtime жизненный цикл: activationDelay → optional Telegraph → активный laserDuration → visual fade-out без урона. Геометрия: LengthMode ToScreenEdge/Custom, laserLength=10 либо lengthOffset, laserWidth=0.15, мировой laserOffset, constantWidth/Length и кривые по нормированному времени. [LaserSettings](../../Assets/Scripts/Gameplay/Enemies/EnemyLaserSettings.cs), [GeometryUtility](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyLaserGeometryUtility.cs).

laserDuration=2, activationDelay=0, fadeIn=0.1, fadeOut=0.2, followOrigin=false, surviveSourceDestruction=true. Направление выпущенного луча зафиксировано; followOrigin переносит начало, а не превращает его в новое непрерывное прицеливание. Damage=1, DamageMode OnContact/Periodic, damageInterval=0.5 только для Periodic. Telegraph включён по умолчанию, duration=1, красный цвет, opacity=0.3, fadeIn/out=0.1; Override Telegraph Geometry раскрывает отдельную геометрию. Иначе предупреждение использует геометрию луча. [runtime](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyLaserRuntime.cs), [origin](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyLaserOrigin.cs).

Fan/Radial/Circle/Sequential Sector создают соответствующие несколько лучей через тот же builder. Обычная base speed не является скоростью роста Laser: длина/ширина и время задаются Laser Settings. Preview рисует Telegraph/луч, но контактный урон по реальному игроку не воспроизводится. Частные маркеры Telegraph/Child Trigger не выдаются как Coordinator Event. [Laser Scene](../../Assets/Editor/EnemyLaserScenePreview.cs), [Preview world](../../Assets/Editor/EnemyProjectilePreviewWorld.cs).

## Рандомизация и ввод чисел

Поддерживаемые поля помечены `[Randomizable]`: справа кнопка кубика; Off показывает исходное число, On — Min/Max. Первое включение инициализирует диапазон текущим значением; дальнейшие переключения сохраняют фиксированное значение и диапазон. Для Vector2/3 X и Y независимы, Z остаётся обычным полем. Float равномерен между отсортированными границами; Int использует целый включительный диапазон без модульного смещения; Min=Max допустим. Неконечные значения и ограничения нормализуются соответствующим range/моделью. [range/types](../../Assets/Scripts/Gameplay/Enemies/NumericRandomization.cs), [drawer](../../Assets/Editor/RandomizableNumberDrawer.cs).

Общий scope enum: PerShot, PerAttack, PerDirection, PerProjectile. Флаговые наборы None/Volley/Direction/All ограничивают список, который реально показывает конкретное поле. Не все поля имеют все scopes. Для скорости отдельный EnemySpeedRandomizeOn с PerBurst; для Origin отдельный OnAttackStart/OnBurstStart/OnShot. Не переносите scope из одной системы в другую по созвучию.

Поддерживаются offsets Origin, layout counts/radius/angles/delays, Fan/Stack/Spread, интервалы и количества Shot/Burst, общий Attack scheduling, lifetime, скорости движения/Orbit/DNA, размеры, child trigger/repeat/offset, Layout Rotation и Initial Rotation Composite. Полный фактический перечень `[Randomizable]` и привязок companion range приведён в справочнике исходников. Число слайдеров вроде min/maxDiveDepth само по себе не означает поддержку общего кубика. [Numeric partials](../../Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.Numeric.cs), [Child numeric](../../Assets/Scripts/Gameplay/Enemies/EnemyChildAttackSettings.Numeric.cs).

Attack cooldown выбирается на цикл, а не каждый кадр. Снимок `EnemyRandomSpeedSample`/`EnemyNumericSample` принадлежит залпу/направлению/элементу, поэтому отложенный Sector не читает sample следующего выстрела. Для legacy InitialSpeed Random детерминированная схема включается отдельным seededInitialSpeedRandom; выключение сохраняет старую логику, что ограничивает обещание абсолютного совпадения всех старых случайных потоков с Preview. [Numeric state](../../Assets/Scripts/Gameplay/Enemies/NumericRandomization.cs), [NumericSample](../../Assets/Scripts/Gameplay/Enemies/EnemyNumericSample.cs).

Numeric Scrubbing: фиксированные Float/Int используют Unity `EditorGUI.FloatField/IntField` с привязанным label. Перетаскивание label — стандартное изменение числа, Min/Max имеют свои draggable labels. Это не slider с произвольным новым диапазоном. Undo/Prefab Override обеспечиваются SerializedProperty/BeginProperty и существующим editor apply; не делайте вывод о сохранении волны по одному изменённому визуальному sample. [drawer](../../Assets/Editor/RandomizableNumberDrawer.cs).

## Пресеты и приоритеты

Три различных уровня ресурсов:

1. `DirectedWaveAttackPreset` — Wave attack settings, movement/scheduling и optional shared base speed. Export Current/Resolved Settings As Preset, Use Preset As Local Settings в AttackBehaviour. [Wave preset](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackPreset.cs).
2. `EnemyAttackPatternPreset` — shooting layout, firing и timing через списки EnemyPresetFields.Layout/Timing; не подменяет всю Projectile Configuration. [pattern asset](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackPatternPreset.cs).
3. `EnemyProjectilePreset` — тип/prefab, color/characteristic, speed/lifetime, movement/size, Laser/Composite, screen cleanup и Child; не захватывает источник/раскладку верхней атаки. [projectile asset](../../Assets/Scripts/Gameplay/Enemies/EnemyProjectilePreset.cs), [границы полей](../../Assets/Scripts/Gameplay/Enemies/EnemyPresetFields.cs).

Use Mode Linked читает изменения asset с локальными override paths; Copy отделяет значения. Apply/Revert/Unlink, Save As, выбор локальных полей и библиотека позволяют осознанно выбрать, что меняется вместе. В библиотеке **Tools/Waves/Attack and Projectile Preset Library** есть категории/список, Create New, Rename, Duplicate, Ping Asset, Apply to current attack, Preview Play/Pause/Stop/Focus, Repeat Same Seed/New Seed. Реальная доступность Apply зависит от выбранного поддерживаемого target. [library](../../Assets/Editor/EnemyPresetLibraryWindow.cs), [inspector](../../Assets/Editor/EnemyPresetInspector.cs).

Порядок разрешения:

- выбранный DirectedWaveAttackPreset либо локальные Wave settings;
- overridePresetAttacksPerSecond и shared overrideBaseProjectileSpeed компонента/группы;
- EnemySettings/WaveOverride и возможный явный instance override executor;
- локальный slot override заменяет разрешённую shooting-конфигурацию, а legacy full override имеет отдельную совместимую ветку;
- Linked Attack Pattern materializes Timing/Layout, затем Linked Projectile — свою конфигурацию; marked local paths сохраняются;
- локальный projectile speed override выигрывает у shared wave speed; InitialSpeed Random выбирает собственный диапазон раньше фиксированной скорости;
- prefab defaults используются, когда соответствующего override нет.

Это схема чтения, а не разрешение безусловно перезаписывать все поля слоя сверху. Проверяйте информатор SpeedSource и `EnemyAttackSettingsResolver.CreateRuntimeCopy/ApplyRuntimeSpeedRandomization`. Для местных атак намеренно сохраняется cadence исходного расписания в современном пути, в отличие от миграционной full-settings ветки. Циклические ссылки пресетов отвергаются; глубина resolver ограничена. [resolver](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackSettingsResolver.cs), [materialization](../../Assets/Scripts/Gameplay/Enemies/EnemyBurstAttackSettings.Presets.cs), [dependencies](../../Assets/Scripts/Gameplay/Enemies/EnemyPresetDependencies.cs).

## Wave Preview Player

Открывается **Tools/Waves/Wave Preview Player** или кнопкой в Wave. Работает в Edit Mode, принимает Wave/prefab и Follow Wave Selection. Кнопки Play/Pause/Stop/Restart, ◀Step/Step▶, скорости 0.25×/0.5×/1×/2×/4×/8×/16× и Max Speed, Preview Duration и Use Base Route Duration; редактируемая цель игрока и Record Player Movement. Duration — горизонт просмотра, не предсказанное время убийства волны. Loop/боевые условия не имеют заранее известной конечной длительности. [window](../../Assets/Editor/WavePreviewPlayerWindow.cs), [player](../../Assets/Editor/WavePreviewPlayer.cs).

### Timeline и seek

Timeline задаёт целевое время, рисует маркеры изменений активных групп Coordinator, имеет диапазон просмотра. Player переводит время в фиксированные кадры **1/30s**. Fast Seek восстанавливает ближайший подходящий snapshot и досчитывает тем же conductor; это не аналитическая формула всех будущих выстрелов. Если подходящего snapshot нет, нужен расчёт с начального состояния. Промежуточное представление отделено от симуляции, чтобы не показывать частично перестроенный мир. [Session.StepInterval](../../Assets/Editor/WaveShootingPreviewSession.cs), [Player.Update](../../Assets/Editor/WavePreviewPlayer.cs), [presentation](../../Assets/Editor/WaveShootingPreviewPresentation.cs).

Advanced: Snapshot Interval default 2s, диапазон 0.25…30; Snapshot Memory Limit default 128MB, 16…512; Simulation Time Budget default 24ms, 0.1…100; Max Steps Per Update default 2048, 1…10000. Бюджет кооперативный: один fixed step/snapshot атомарен и может занять дольше. Cache держит максимум 32 entries, начальный кадр сохраняется при trim/clear; оценка памяти не равна точному RSS процесса. Fast Seek Diagnostics показывает Target Time, Simulated Duration, Seek Wall Time, Simulation Speed, Steps Processed. [cache](../../Assets/Editor/WavePreviewSnapshotCache.cs), [state graph](../../Assets/Editor/WavePreviewStateGraph.cs), [ECS snapshot](../../Assets/Editor/EnemyProjectilePreviewSnapshots.cs).

### История игрока и детерминизм

При перетаскивании цели после начала просмотра запись включается автоматически. Изменение текущего sample не сбрасывает мир к 0; будущие snapshots/events инвалидируются. Правка прошлого создаёт новую ветку истории и досчитывает состояние. История — кусочно-постоянные samples по кадрам, не плавная интерполяция между редкими точками. Record off сохраняет прошлое, замораживает будущую цель. Clear Player Movement History пересобирает с одной фиксированной целью. Seed/config/camera revisions делают прежние состояния неактуальными. [TargetHistory](../../Assets/Editor/WavePreviewTargetHistory.cs), [Player.TargetPosition/SetTargetPositionAtTime](../../Assets/Editor/WavePreviewPlayer.cs).

### Coordinator navigation

Previous Event/Next Event находят только изменение множества активных групп. Несколько изменений одного fixed step объединены. Перемотка останавливается после исполнения события на completed-frame; подпись Time показывает начало этого шага, StateTime — его завершение. Поэтому точность — шаг симуляции, а не произвольная субкадровая точка. При отсутствии/выключенном Coordinator кнопки неактивны. [event](../../Assets/Editor/WavePreviewCoordinatorEvent.cs).

Неизвестное событие ищется в отдельном экземпляре того же conductor, не двигая видимый мир; после нахождения выполняется обычный seek и пауза. Поиск ограничен 18000 шагами, 10s wall time и текущей Preview Duration; есть Cancel, отсутствие следующего события не вызывает бесконечный синхронный цикл. Обычные выстрелы/Child/Laser не становятся этими маркерами. [navigation](../../Assets/Editor/WavePreviewPlayerCoordinatorNavigation.cs).

### Что Preview не доказывает

Нет боевой симуляции здоровья/смертей врагов и реального урона по игроку. Исключение по геометрии контакта: Composite parts проверяются production collision-алгоритмом относительно synthetic player circle radius=0.05 и могут исчезать по отдельности; ordinary Preview bullets эту проверку пропускают (EnemyProjectilePreviewCollisionPolicy). Это не уменьшает настоящее здоровье игрока; зависимое от игрока attack dive movement не воспроизводится. Legacy cues без DirectedEnemySubWave отмечаются UnsupportedCueCount. Danger warning учитывается как задержка, но его runtime visuals здесь не воспроизводятся. Лимит живых снарядов — 8192, направлений на залп — 128, бюджет эмиссии — 512; omitted показывается, и тяжёлый узор может быть неполным. Силуэты всех врагов теперь не ограничены прежним sample=32, но это не снимает лимиты снарядов. [window HelpBox](../../Assets/Editor/WavePreviewPlayerWindow.cs), [session](../../Assets/Editor/WaveShootingPreviewSession.cs), [world](../../Assets/Editor/EnemyProjectilePreviewWorld.cs).

## Scene View и границы

**Show Camera Bounds** в Wave по умолчанию OFF. Постоянная рамка использует gameplay viewport Fighting Camera, включая Safe Area/HUD и Fill Available Gameplay Area. В Editor служебная нерендерящая preview-scene читает боевую камеру; при наличии загруженной боевой камеры предусмотрен live path; в Play — Camera.main. Размеры offline layout приходят из Game View/ScreenSimulation, а не из случайного окна Screen. Перемещение курсора между Scene/Inspector не является основанием менять геометрию. [camera provider](../../Assets/Editor/WavePreviewGameplayCamera.cs), [display adapter](../../Assets/Editor/WavePreviewDisplay.cs), [viewport](../../Assets/Scripts/Gameplay/Levels/BattleCameraViewport.cs).

**Show All Enemy Silhouettes** OFF по умолчанию; ON рисует реальные sprite meshes с текущими transform/scale/rotation всей Wave. При активном Preview берёт его текущий published frame, иначе формацию; уже видимые renderers повторно не рисуются. Для самой отрисовки силуэтов новые GameObject не создаются, но это не означает отсутствия объектов в служебной camera preview-scene или в анимированном Preview. [silhouette](../../Assets/Editor/WaveEnemySilhouette.cs), [scene overlay](../../Assets/Editor/WaveSceneVisualization.cs).

Подробный выбранный слот дополнительно показывает пути, метки, shooting rays, старт/конец Rotation, Child/Laser/Composite guides. Для сложного вращения обзор сэмплирует покрытие, выбранный подробный залп показывает больше деталей. Не интерпретируйте всё полупрозрачное покрытие как реальные одновременные collision shapes. [EnemyShootingPreview](../../Assets/Editor/EnemyShootingPreview.cs), [Composite preview](../../Assets/Editor/EnemyShootingPreview.Composite.cs).

В `DirectedEnemySubWaveEditor` остался метод старой панели Mobile Screen Bounds с 9:16/9:19.5, но прямого вызова этого метода в файле не обнаружено. Это сохранённый код, а не обещание доступной кнопки в актуальном Inspector. Общая Wave-рамка и preview display adapter — актуальный путь. Аналогично наличие Inspector enum legacyAttack не делает его новой командой дизайнера.

## Дополнительные возможности

- **WaveDangerWarningController** перед началом подволнов рисует предупреждения Circle/Ellipse/Parabola/Rectangle, несколько flashes, visible/hidden интервалы, alpha transitions/curve, global/per-shape color, inversion для замкнутых областей, stencil/playfield settings. Parabola открыта, поэтому inverted отключается; длина/curvature/x-range/power/segmentLengthScale задают её форму. `WaveDangerWarningVisualPreset` хранит вид, а Scene handles редактируют контуры. Это визуальное предупреждение, не автоматически наносящая урон зона. [controller/editor](../../Assets/Scripts/Gameplay/Waves/WaveDangerWarningController.cs), [editor](../../Assets/Editor/WaveDangerWarningControllerEditor.cs).
- **WaveEnemyDifficultyModifier** умножает hull/shield/fireRate на настройки Level и Wave при spawn. Урон в старом damageMultiplier больше не масштабируется; API возвращает 1. Это важно для Preview с нейтральной конфигурацией без конкретного Level context. [modifier](../../Assets/Scripts/Gameplay/Waves/WaveEnemyDifficultyModifier.cs).
- **WaveBuffDropController/SubWaveBuffDropController** распределяют buff drops по созданным врагам. WeightProfile и кривые зависят от playerHealthPercent, weaponLevel, teamAbilityRecoveryNeed. Это боевое состояние, не детерминированный список выстрелов Timeline. **WaveMetalDropPlan/WaveMetalDropSettings** планируют распределение металла по reward-eligible slots. [profile](../../Assets/Scripts/Gameplay/Waves/WaveBuffDropWeightProfile.cs), [metal](../../Assets/Scripts/Gameplay/Waves/WaveMetalDropPlan.cs).
- **WaveManager/WaveProgressPopup** переключают Waves, могут заранее Prepare совместимые encounters; очищающий expanding wave-clear effect и slow motion относятся к завершению живых врагов. Время до следующей таблички связано с восстановлением нормального хода времени. В Preview без смертей эти эффекты не возникают как настоящий бой. [WaveManager](../../Assets/Scripts/Gameplay/Waves/WaveManager.cs).
- **Enemy prefab** определяет здоровье, щит, коллизии, executor и визуальное Facing; `FourWayEnemy`, `CircleShip`, `ShootingShip`, `RamShip` имеют разные штатные реакции. Наличие локальной Wave атаки не превращает любой prefab в подходящий executor. [Enemy](../../Assets/Scripts/Gameplay/Enemies/Enemy.cs), [IWaveAttackExecutor](../../Assets/Scripts/Gameplay/Enemies/IWaveAttackExecutor.cs).
- **Legacy**: `InfoAboutSubWave` и `TrajectoryEnemySubWave` остаются совместимыми encounters. Вторая использует MovementSequencePlayer с SpawnAt/MoveLocal/MoveWorld/RotateBy/Repeat/Wait/DeactivateChildrenFor и fallback SO movement. Полноценный Wave Preview Player не подменяет эти сценарии Directed-путём. [legacy](../../Assets/Scripts/Gameplay/Waves/TrajectoryEnemySubWave.cs), [movement](../../Assets/Scripts/StrategyMovement/Scripts/MovementSequencePlayer.cs).
- **WavePreviewProfiler** содержит служебные области измерения и диагностические ограничения/переключатели представления. Это инструментарий, не дополнительные боевые настройки волны. [profiler](../../Assets/Scripts/Gameplay/Waves/WavePreviewProfiler.cs).

## Отдельный путь BossFight encounter

`WaveManager` принимает не только Wave conductor: `BossFight` реализует IWaveEncounter и может стоять в `LevelConfig.waves`. Поля Boss prefab, spawnPoint, spawnOffset, destroyEncounterOnDefeat задают создание босса через Zenject; Defeated завершает encounter, ошибочная ссылка сообщает причину и переводит менеджер дальше. Это самостоятельная существующая возможность уровня, а не поддержанная Directed-подволна. [BossFight](../../Assets/Scripts/Gameplay/Bosses/BossFight.cs).

`BossController` наследует Enemy и содержит attackOrigin и phases. Каждый `BossPhaseDefinition` задаёт имя, порог процента здоровья для следующей фазы, startDelay=0.5, delayBetweenAttacks=1, SelectionMode Sequential/Random и массив BossRadialAttackPattern. Последняя фаза не использует свой next threshold. Редактор предупреждает о нулевых и неубывающих порогах; большой разовый урон может перескочить несколько фаз. При входе новой фазы прежняя attack coroutine отменяется. [BossController](../../Assets/Scripts/Gameplay/Bosses/BossController.cs), [Inspector](../../Assets/Editor/Bosses/BossControllerEditor.cs).

`BossRadialAttackPattern` создаётся через **Boss/Attacks/Radial Attack**. Shape Circle/Arc/Spiral: projectileCount=12, arcDegrees=120, startAngleDegrees, spawnRadius=0.2; WorldAngle/TowardPlayer; volleyCount=1, delayBetweenVolleys=0.12, rotationPerVolleyDegrees=10 (вращение от volley применяется Spiral). Flight Straight/Curved/Homing: initialSpeed=4, acceleration, speedOverLifetime, angularVelocityDegrees, homingTurnSpeed=180. Damage=10, lifetime=6, projectileScale=(1,1), отдельный BossProjectile prefab. В этой системе углы считаются от +X через cos/sin; не переносите 180° Stationary-вниз из обычной атаки без преобразования. [asset](../../Assets/Scripts/Gameplay/Bosses/BossRadialAttackPattern.cs).

BossProjectile — pooled MonoBehaviour с FixedUpdate и Rigidbody2D/Transform movement, BossProjectilePool имеет initialSize=32. Это не ECS ordinary bullet и не наследует автоматически Child/Composite/screen cleanup настройки. Отдельный Inspector Attack Preview рисует первый/последний залп статически, без полного боя и Wave Fast Seek/Snapshots. Конфигурацию BossFight нельзя выдавать за поддержанную WavePreviewPlayer симуляцию. [projectile](../../Assets/Scripts/Gameplay/Bosses/BossProjectile.cs), [pool](../../Assets/Scripts/Gameplay/Bosses/BossProjectilePool.cs), [asset editor](../../Assets/Editor/Bosses/BossRadialAttackPatternEditor.cs).

## Обнаруженные особенности legacy authoring

В файле `CircularMovement.cs` объявлен **PatrolMovement**, а меню называется `SubWaveBehaviour/Circle`. Однако фактическое действие — `DOMoveX(radius,duration).SetRelative().SetLoops(times,Yoyo)`, горизонтальный маятник, не окружность. `isLooped` задаёт times=-1. В `StrategyMovement1.cs` класс **ForvardLoop** идёт вниз на forvardMove, затем callback телепортирует в start и бесконечно повторяет sequence. Это существенные различия имён и реализации, которые нельзя восстанавливать по названию asset. Они относятся к старому SOStrategyMovement pipeline, а не новым checkpoint curves/Post commands. [PatrolMovement](../../Assets/Scripts/StrategyMovement/Scripts/CircularMovement.cs), [ForvardLoop](../../Assets/Scripts/StrategyMovement/Scripts/StrategyMovement1.cs).

## Проверка авторской волны

1. Создайте GameObject/prefab с Wave и заполните scheduledSubWaves; проверьте, что каждый элемент имеет InfoAboutSubWave-совместимый компонент.
2. Для новой Directed-подволны добавьте Formation, назначьте prefab и форму. Сначала проверьте slot IDs и Spawn Order.
3. Добавьте Phase Entry только при необходимости, выберите coordinate space, путь, время/скорость, settle/loop. Проверьте ручки на переходах.
4. Добавьте AttackBehaviour, источник настроек, слоты, Fire/Aim и внутренний Pattern. Постройте простой Point, затем расширяйте Layout/Fan/Stack.
5. Настройте движение/жизнь снаряда, затем Rotation/Randomization. Не меняйте одновременно все уровни cadence.
6. Добавьте Child/Composite/Laser, оценив число сущностей и ограничители. Проверьте родительскую жизнь и границы.
7. Соберите Post pipeline и/или Attack Sequence. Infinite и Unlimited ожидания используйте только намеренно.
8. Добавьте Coordinator, если нужны окна активных подволнов. Укажите schedule slots и readiness/timeout/реактивацию; проверьте gap и overlap.
9. Включите Wave Bounds/силуэты и Shooting Preview; сравните from/to и фактические эмиттеры. Запустите Wave Preview Player, проверьте разные времена, обратный seek, повтор того же Seed и историю цели.
10. Прочитайте warning/omitted/unsupported и матрицу поддержки: Preview не заменяет проверку смертей/урона/пикирования в последующем обычном бою.
11. Сохраните prefab и нужные preset assets штатным Unity workflow, отдельно решив Linked или Copy. Не сохраняйте случайный preview sample как авторское фиксированное значение.

Подробные рецепты: [WaveEditorPracticalExamples](WaveEditorPracticalExamples.md). Полный реестр исследованных файлов, полей, enum и связей: [WaveEditorScriptsReference](WaveEditorScriptsReference.md). Неопределённости и неподдерживаемые сочетания: [WaveEditorFeatureMatrix](WaveEditorFeatureMatrix.md).
