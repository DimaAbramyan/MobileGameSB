# Wave Editor: функции, поддержка, взаимодействия и ограничения

Срез кода: 24.10.2026. [Руководство](WaveEditorCompleteGuide.md) · [справочник и реестр файлов](WaveEditorScriptsReference.md) · [рецепты](WaveEditorPracticalExamples.md).

«Да» ниже означает наличие пути исполнения в текущем коде, **не результат нового Play/Android теста**. «Расчёт» — учитывается в вычисляемом движении/состоянии; «частично» требует прочитать ограничение. Fast Seek использует тот же Preview, поэтому не расширяет его поддержку. Наличие Scene guide не означает боевой симуляции урона.

## Реестр возможностей и поддержка

| Возможность | Play Mode | Wave Preview Player | Fast Seek / snapshots | Scene View | Основной код / существенная оговорка |
|---|---|---|---|---|---|
| Порядок Waves в LevelConfig | Да | Одна выбранная Wave | Внутри выбранной Wave | Нет отдельной схемы кампании | [LevelConfig](../../Assets/Scripts/Gameplay/Levels/LevelConfig.cs), [WaveManager](../../Assets/Scripts/Gameplay/Waves/WaveManager.cs). Player не проигрыватель всей кампании. |
| Schedule подволнов, startDelay | Да | Да, Directed cues | Да | Старый schedule preview | [Wave](../../Assets/Scripts/Gameplay/Waves/Wave.cs), [conductor](../../Assets/Editor/WaveConductorShootingPreview.cs). |
| Zenject Prepare/preload | Да | Свой preview runtime | Не состояние настоящего DI-контейнера | Нет | [Wave.Prepare](../../Assets/Scripts/Gameplay/Waves/Wave.cs), [factory](../../Assets/Scripts/Gameplay/Waves/DirectedWaveEnemyFactory.cs). |
| Component-based Formation/Entry/Post/Completion | Да | Читает тот же config | Да, invalidation при изменении | Да | [blocks](../../Assets/Scripts/Gameplay/Waves/DirectedWaveSubWaveBehaviourBlocks.cs), [configuration](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveBehaviourConfiguration.cs). |
| Legacy embedded Directed config | Compatibility path | Да, через config resolver | Да | Legacy Inspector | [configuration](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveBehaviourConfiguration.cs). Добавление пустого компонента не равно миграции. |
| Procedural Formation: 11 Layout enum | Да | Да | Расчёт/состояние | Точки и силуэты | [solver](../../Assets/Scripts/Gameplay/Waves/DirectedWaveFormationSolver.cs). |
| Custom Matrix → Points | Результат как точки | Да | Да | Рисование/применение матрицы | [Subwave editor](../../Assets/Editor/DirectedEnemySubWaveEditor.cs). Не отдельный Runtime enum Matrix. |
| CustomPoints / TransformPoints | Да | Да | Да | Handles / Transform | [solver](../../Assets/Scripts/Gameplay/Waves/DirectedWaveFormationSolver.cs). Slot ID сохраняет смысл после уничтожения соседей. |
| Freeze/Bake и генераторы фигур | Сохранённый результат | Да | Да после config invalidation | Да | [editor](../../Assets/Editor/DirectedEnemySubWaveEditor.cs). Bake — изменение авторских данных. |
| Per-Enemy Offset, Middle/Symmetric | Да | Да | Да | Да | [DirectedWaveFormationOffset](../../Assets/Scripts/Gameplay/Waves/DirectedWaveModels.cs). |
| Spawn Order | Да | Да | Да | Нумерация/перестановка точек | [resolver](../../Assets/Scripts/Gameplay/Waves/DirectedWaveSpawnOrderResolver.cs). Отдельно от Attack Order. |
| Local prefab/tint overrides | Да | По выбранному prefab/визуалам | Да | Подробный слот | [override](../../Assets/Scripts/Gameplay/Waves/DirectedWaveEnemyOverride.cs). Не произвольный override любых полей Enemy. |
| Coordinate spaces World/SubWave/SpawnPoint | Да | Да, с mapping preview parent | Да | Да | [simulation](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveSimulation.cs). |
| Checkpoint entry | Да | Расчёт позиций | Да | Маршруты/ручки | [path](../../Assets/Scripts/Gameplay/Waves/DirectedWavePathEvaluator.cs). |
| Bezier ручки Auto/Free/Aligned/Mirrored | Да | Да | Да | Числа и handles | [curve model](../../Assets/Scripts/Gameplay/Waves/DirectedWaveModels.cs), [curve editor](../../Assets/Editor/DirectedEnemySubWaveCurveEditor.cs). |
| CatmullRom tension/parameterization | Да | Да | Да | Да | [path evaluator](../../Assets/Scripts/Gameplay/Waves/DirectedWavePathEvaluator.cs). Старый точный путь Uniform+tension=0 сохранён. |
| Arc bulge/longArc | Да | Да | Да | Ручка кривизны | [EvaluateArc](../../Assets/Scripts/Gameplay/Waves/DirectedWavePathEvaluator.cs). Совпавшие точки/0 bulge — отрезок. |
| Individual entry points | Да | Да | Да | Генераторы и handles | [configs](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveBehaviourConfigs.cs). |
| Entrance loop/teleport/settle | Да | Расчёт той же временной схемы | Да | Да | [loop evaluator](../../Assets/Scripts/Gameplay/Waves/DirectedWaveEntranceLoopEvaluator.cs). Не новый spawn. |
| Rotate enemy along entry | Да | Movement/facing preview | Да | Поворот визуала | [attack motion](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveAttackMotion.cs). Отличается от Facing/Layout Rotation. |
| Post Wait/Patrol/Local/Wobble/Orbit/Rotation/Morph | Да | Общий evaluator | Да | Маршрут/форма/команда | [handlers](../../Assets/Scripts/Gameplay/Waves/DirectedWavePostCommandHandlers.cs), [simulation](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveSimulation.cs). |
| Formation Reorder | Да | Да | Runtime evaluation state | Порядок/цель | [reorder](../../Assets/Scripts/Gameplay/Waves/DirectedWaveFormationReorder.cs). |
| Post Parallel/Loop/Infinite | Да | Да в конечном горизонте | Да | Command preview | [timeline cache](../../Assets/Scripts/Gameplay/Waves/DirectedWaveTimelineRuntimeCache.cs). Infinite не обещает завершение. |
| Fire Aimed/Stationary/Default/ForwardWhenPlayerAhead/None | Да | Выстрелы по preview target | Да | Лучи | [attack models](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackModels.cs). |
| Aim On Start/Burst/Continuous | Да | Да, собственные captures | Да | Статический guide не вся история | [aim state](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackModels.cs). |
| MoveToPlayer / FlyThroughDive | Да | **Нет player-dependent dive motion** | Не появляется от seek | Статические сведения/route preview | [runtime controller](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackController.cs), [session BuildSlot](../../Assets/Editor/WaveShootingPreviewSession.cs). |
| Min Dive Duration / Max Dive Speed | MoveToPlayer approach | Не боевое dive движение | Нет отдельной симуляции | Числовые настройки | [DiveMotionProgress](../../Assets/Scripts/Gameplay/Waves/DirectedWaveDiveMotionProgress.cs). Оба 0 сохраняют прежнее поведение. |
| Entrance attacks At/Across/Continuous | Да | План route events | Да | Checkpoint rules | [entrance runtime](../../Assets/Scripts/Gameplay/Waves/DirectedWaveEntranceAttackController.cs), [preview coordinator](../../Assets/Editor/WaveShootingPreviewCoordinator.cs). |
| Single / Multiple enemy patterns | Да | Да | Отдельные состояния | Потоки/лучи | [burst](../../Assets/Scripts/Gameplay/Enemies/EnemyBurstAttackSettings.cs). Multiple не Wave Sequence. |
| Shot/Burst/Cooldown | Да | Да, fixed-step | Да | Наглядный залп / timing UI | [schedule](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackPatternSchedule.cs). |
| Attack Sequence / Pause / RepeatBlock | Да | Общий SequencePlayer | Да | Статический guide только first reachable attack | [sequence](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackSequence.cs), [preview sequence](../../Assets/Editor/WaveShootingPreviewSequence.cs). |
| Sequence Wait For Child Attacks | Да | Да для моделируемых child lifecycles | Да | Нет отдельной шкалы всех детей | [completion](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackCompletionTracker.cs). Timeout/unlimited задаются явно. |
| Attack Order 12 режимов | Да | Тот же план, свои clocks | Да | Нумерация/батчи | [order](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackOrder.cs), [drawer](../../Assets/Editor/DirectedWaveAttackOrderSettingsDrawer.cs). |
| Coordinator Timed/Cycles/All/First/Overlap/Restart/Resume | Да | Общий CoordinatorPlayer | Да | Панель внизу Player / Timeline | [player](../../Assets/Scripts/Gameplay/Waves/WaveAttackCoordinatorPlayer.cs). Нет моделирования смерти участников. |
| Previous/Next Coordinator Event | Editor-only | Да | Через seek/same conductor search | Timeline маркеры | [navigation](../../Assets/Editor/WavePreviewPlayerCoordinatorNavigation.cs). Только изменение active set. |
| Emission Origin независимые X/Y | Да | Да | Capture/random samples | Точки и лучи | [origin](../../Assets/Scripts/Gameplay/Enemies/EnemyEmissionOriginSettings.cs). В Child скрыт/переопределён. |
| Point/Radial/Circle/Sector | Да | Да | Да | Полная геометрия с лимитами обзора | [layout](../../Assets/Scripts/Gameplay/Enemies/EnemyEmissionLayoutSettings.cs). |
| Sector Sequential/PingPong | Да | Да | Незавершённые очереди/order state | Да | [order state](../../Assets/Scripts/Gameplay/Enemies/EnemySectorLaunchOrderState.cs). Toggle только по завершённому сектору. |
| Fan/Stack/Random Spread | Да | Общий builder/geometry | Да | Лучи каждого элемента в бюджете | [builder](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackProjectileBuilder.cs). |
| Layout Rotation 3 режима | Да | Общие EvaluateAngle/attack clocks | Да | From/To/coverage автоматически | [rotation](../../Assets/Scripts/Gameplay/Enemies/EnemyRotationSettings.cs), [guide](../../Assets/Editor/EnemyShootingPreview.Rotation.cs). |
| Enemy Facing independent/follow pattern | Да | Preview визуала | Да | Да | [Facing](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackFacing.cs). Не physical Rigidbody simulation. |
| Straight/Homing/BurstAtPoint ECS | Да | Те же системы в отдельном World | ECS state snapshots | Траектория/снаряды | [ECS systems](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSystems.cs). Collision с игроком не воспроизводится как бой. |
| AngularTurn/Lateral/Orbit/DNA | Да | ECS movement | Да | Статическая и анимированная форма | [components/DNA](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsComponents.cs), [systems](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSystems.cs). |
| Speed/size curves и targets | Да | ECS update | Да | По текущей симуляции | [shooting](../../Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.cs). |
| Destroy Outside Screen, dynamic radius | Да | Общий boundary system | Entered/Previous state | Через исчезновение/рамку | [screen system](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileScreenBoundarySystem.cs). Lifetime отдельно. |
| Normal/Fragile/цвет | Да | Визуал/данные | Да | Цвет снаряда | [characteristics](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsComponents.cs). Уничтожение способностями игрока не симулируется. |
| Composite Circle/Polygon/SegmentedRing | Да | Те же ECS figure systems | Да | Компоненты + общий guide | [Composite](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyCompositeProjectileSettings.cs). ≤256 компонентов. |
| Composite collisions / damage | Да | Production contact с synthetic circle .05; без HP | Да для состояния parts, не подтверждает полученный урон | Компонентные hit areas | [registry](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileCollisionRegistry.cs), [systems](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyCompositeProjectileSystems.cs). |
| Child AfterTime/AfterDistance/OnSpawn/Expired | Да | Общий runner | Да | Child guide / реальные дети | [child](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackSystem.cs). |
| Child HitPlayer/Intercepted | Да, при фактическом событии | Нет настоящего player hit/interception | Только если такое событие специально подано в моделируемый мир | Не гарантирован момент боя | [resolution](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSystems.cs), [runner](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackSystem.cs). |
| Child BurstAtPoint | Да | Есть соответствующая ECS lifecycle | Да | Через моделируемые дочерние эмиссии | [ECS/child](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackSystem.cs). |
| Child Screen Boundary flags/delay | Да | Общий intersection + runner | Предыдущая позиция/queues/state | Child/bounds guides | [boundary](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackBoundary.cs), [trigger](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackBoundaryTrigger.cs). |
| Laser/Telegraph/fade/followOrigin | Да | Общий laser update + preview draw | Managed/ECS state | Луч/ширина/Telegraph | [laser](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyLaserRuntime.cs), [Scene preview](../../Assets/Editor/EnemyLaserScenePreview.cs). |
| Laser contact/periodic damage | Да | Без настоящего урона | Не проверка урона | Только геометрия | [laser runtime](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyLaserRuntime.cs). |
| Numeric random scopes/seed | Да | С seed offset / snapshots | Да в покрытом scope | Детерминированный sample | [numeric](../../Assets/Scripts/Gameplay/Enemies/NumericRandomization.cs). Legacy unseeded RNG — отдельная оговорка. |
| Independent Projectile/Attack Presets | Да, materialize | Да | Configuration revision | Library preview | [preset resolver](../../Assets/Scripts/Gameplay/Enemies/EnemyBurstAttackSettings.Presets.cs). |
| Numeric scrubbing / Undo / Prefab Overrides | Editor-only | Не симуляция | Изменение config инвалидирует cache | Числовые поля | [drawer](../../Assets/Editor/RandomizableNumberDrawer.cs). |
| Show Camera Bounds / All Silhouettes | Editor-only | Текущий published state | Не отдельная симуляция | Постоянно, SessionState | [visualization](../../Assets/Editor/WaveSceneVisualization.cs). |
| Actual camera safe area / HUD bounds | Да | Отдельный Editor camera adapter | Camera revision | Рамка | [viewport](../../Assets/Scripts/Gameplay/Levels/BattleCameraViewport.cs), [display](../../Assets/Editor/WavePreviewDisplay.cs). |
| Danger Warning | Да | Учитывает длительность | Задержка, без runtime warning visuals | Собственный warning editor | [warning](../../Assets/Scripts/Gameplay/Waves/WaveDangerWarningController.cs). |
| Wave difficulty / buff / metal | Да | Не полная симуляция Level+игрока | Не тест экономики/боевых исходов | Inspector конфигов | [modifier](../../Assets/Scripts/Gameplay/Waves/WaveEnemyDifficultyModifier.cs), [drop plan](../../Assets/Scripts/Gameplay/Waves/WaveMetalDropPlan.cs). |
| Wave completion / clear explosion / popup | Да | Нет смертей для реального completion | Не предсказывается автоматически | Нет боевого результата | [WaveManager](../../Assets/Scripts/Gameplay/Waves/WaveManager.cs). |
| BossFight / Boss phases / Circle-Arc-Spiral assets | Да, отдельный encounter | Нет в Directed Player | Нет | Собственный статический Inspector Attack Preview | [BossFight](../../Assets/Scripts/Gameplay/Bosses/BossFight.cs), [Boss asset](../../Assets/Scripts/Gameplay/Bosses/BossRadialAttackPattern.cs). Pooled MonoBehaviour projectiles, не общий ECS Child/Composite. |
| TrajectoryEnemySubWave / legacy SO movement | Да | Не поддержан полноценным conductor Player | Unsupported cue | Существующий legacy workflow | [trajectory](../../Assets/Scripts/Gameplay/Waves/TrajectoryEnemySubWave.cs), [movement](../../Assets/Scripts/StrategyMovement/Scripts/MovementSequencePlayer.cs). |

## Карта Inspector: какой выбор раскрывает поля

| Раздел | Постоянные элементы | Условные элементы | Источник |
|---|---|---|---|
| Wave | Schedule, Preview, Camera Bounds, All Silhouettes | Legacy copy при legacy списке; назначение подволны/задержка в cue | [WaveEditor](../../Assets/Editor/WaveEditor.cs) |
| Subwave | Composition + Formation | Entry/Post/Attack/Completion по наличию/включению component; старые embedded поля при compatibility path | [Subwave editor](../../Assets/Editor/DirectedEnemySubWaveEditor.cs) |
| Formation | Layout/space/center/spawn | Grid matrix, Arc radius/sweep, shape count/radius/flattening, Free points, Transform root по Layout; freeze меняет управление точками | [Subwave editor](../../Assets/Editor/DirectedEnemySubWaveEditor.cs) |
| Entry | Mode, coordinate space | Checkpoints либо IndividualPoints; loop start/teleport/delay при LoopEntrancePath; handles по Motion/Handle Mode | [Curve editor](../../Assets/Editor/DirectedEnemySubWaveCurveEditor.cs) |
| Post | Массив enabled-команд, reorder, add/remove | Payload конкретного type; Parallel/Loop вложенные массивы; Infinite скрывает ненужное конечное время | [Subwave editor](../../Assets/Editor/DirectedEnemySubWaveEditor.cs) |
| AttackBehaviour | Mode, selection | Patterns либо Sequence; preset overrides при назначенном preset; fire-specific Aim/Stationary; dive-specific параметры | [Attack editor](../../Assets/Editor/DirectedWaveAttackBehaviourEditor.cs) |
| Sequence | Mode/start/reentry/wait | LoopCount кроме Once; Seed в Random; Pause duration; Attack completion/child wait; RepeatBlock steps | [Sequence editor](../../Assets/Editor/DirectedWaveAttackSequenceEditor.cs) |
| Order | enabled/mode | Manual slots в Manual; соответствующие interval/batch/source/reverse/loop | [Order drawer](../../Assets/Editor/DirectedWaveAttackOrderSettingsDrawer.cs) |
| Shooting Behaviour | Preset, Origin, Layout, Spread, Stack, Fan, Layout Rotation, timing | Origin скрыт в Child; Random spread angles; Stack count/direction; Fan count/min/max; Rotation payload при ON | [Shooting drawer](../../Assets/Editor/EnemyShootingSettingsDrawer.cs) |
| Origin ось | Source, Offset | Player Capture; World Reference; Boundary сторона; Randomize On при random offset | [Origin drawer](../../Assets/Editor/EnemyEmissionOriginSettingsDrawer.cs) |
| Layout | Type | Point offset; Radial directions/start; Circle emitters/radius/center; Sector angles/count/launch. Sequential delays/order; PingPong initial/reset | [Layout drawer](../../Assets/Editor/EnemyEmissionLayoutSettingsDrawer.cs) |
| Projectile | Type, preset/prefab, characteristic/color overrides, initial speed | Laser имеет свой payload; Composite — свой shape drawer; overrides values только ON; Random speed ranges/seeded toggle; curve targets при Orbit/Rotation | [Shooting drawer](../../Assets/Editor/EnemyShootingSettingsDrawer.cs) |
| Movement | Pattern | Angular speed; lateral speed; Orbit center/radius/radial; Fixed center vector; DNA weave вместо обычных orbit radius fields | [Shooting drawer](../../Assets/Editor/EnemyShootingSettingsDrawer.cs) |
| Screen cleanup | Destroy Outside Screen | Offset Mode при ON; Boundary Offset только Custom | [screen drawer](../../Assets/Editor/EnemyProjectileScreenSettingsDrawer.cs) |
| Composite | Shape, radius mode, start, rotation, components | sides/perSide либо segments/gap либо count; radius только Custom; spacing только Auto; angular в Continuous/PingPong; min/max только PingPong | [composite drawer](../../Assets/Editor/EnemyCompositeProjectileSettingsDrawer.cs) |
| Child | Enable | TriggerValue для time/distance; EventType для OnEvent; boundary flags/offset/once/time только boundary; Aim/Stationary по fire mode; вложенный pattern | [Child drawer](../../Assets/Editor/EnemyChildAttackSettingsDrawer.cs) |
| Laser | Geometry, duration, damage, Enable Telegraph | Custom length либо edge offset; width/length curves если nonconstant; interval только Periodic; Telegraph payload и override geometry только при ON | [Laser drawer](../../Assets/Editor/EnemyLaserSettingsDrawer.cs) |
| Random numeric | Значение+кубик | Min/Max вместо fixed, scope popup только для разрешённых scopes; X/Y отдельно | [Random drawer](../../Assets/Editor/RandomizableNumberDrawer.cs) |
| Preset | Asset + Linked/Copy | Marked local override paths, Apply/Revert/Unlink/Save при применимом источнике | [Preset Inspector](../../Assets/Editor/EnemyPresetInspector.cs) |
| Preview Player | Transport, timeline, duration, target | Progress при seeking; Coordinator навигация только при enabled; advanced cache/budget/diagnostics; Coordinator editor внизу | [window](../../Assets/Editor/WavePreviewPlayerWindow.cs) |

Точные имена полей, инициализаторы, скрытые legacy поля и enum каждого файла собраны в [Scripts Reference](WaveEditorScriptsReference.md). Поэтому эта таблица не пытается представить все сериализованные поля как одновременно видимые в одном Inspector.

## Взаимодействия и приоритеты

| Комбинация | Поддержка / правило | Что проверить | Источник |
|---|---|---|---|
| Layout + Rotation | Rotation применяется к layout basis; Point offset/Circle позиции и направления используют свои преобразования | From/To полного Fan, а не только ось | [layout](../../Assets/Scripts/Gameplay/Enemies/EnemyEmissionLayoutSettings.cs), [geometry](../../Assets/Editor/EnemyShootingPreviewGeometry.cs) |
| Rotation + Randomization | Случайные angle/speed получают samples в разрешённом scope | PerAttack и PerShot дают разный ритм; seed offset 0 для authored seeds | [Rotation.Numeric](../../Assets/Scripts/Gameplay/Enemies/EnemyRotationSettings.Numeric.cs) |
| Multiple + Facing | Потоки независимы; FollowPattern выбирает устойчивый PatternId | Не ожидать физического поворота всех стволов от одного follow | [Burst](../../Assets/Scripts/Gameplay/Enemies/EnemyBurstAttackSettings.cs), [Facing](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackFacing.cs) |
| Composite + Fan/Radial/Sector | Каждый элемент верхней эмиссии создаёт целую фигуру | Цена сущностей умножается на 1+ComponentCount | [builder](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackProjectileBuilder.cs), [composite spawn](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSpawner.Composite.cs) |
| Composite AlignToMovement + Homing | Center поворачивает траекторию; фигура ориентируется по её движению | Начальный rotation и angular mode независимы от layout | [composite systems](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyCompositeProjectileSystems.cs) |
| Composite + size curve | Масштабируется вся фигура и component envelope | Auto radius исходно из фактических размеров, не только sprite width | [composite systems](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyCompositeProjectileSystems.cs) |
| Composite + Child | Child принадлежит центру; потомки могут снова быть Composite | Нет child на отдельных компонентах; HitPlayer центра недоступен | [composite spawn](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSpawner.Composite.cs) |
| Composite + BurstAtPoint component | **Не поддерживается**, runtime exception | Выбрать Straight/Homing и Child центра | [composite spawn](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSpawner.Composite.cs) |
| Child + Boundary flags | Только выбранные линии; unselected не обрезают дальнейший путь | Появление снаружи не равно crossing | [intersection](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackBoundary.cs) |
| Child boundary + DestroyOutside | ScreenHold защищает ожидаемую границу/дочернюю работу | Lifetime всё ещё может закончиться раньше | [screen](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileScreenBoundarySystem.cs), [child](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackSystem.cs) |
| Child boundary + delay + capture | CaptureOnStart берёт точку crossing; FollowParent текущую позу при эмиссии | Родитель продолжает движение; уничтожение обрабатывается отдельно | [trigger](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackBoundaryTrigger.cs) |
| Child + Sequential Sector + Repeat | Repeat interval после окончания всех launches | Жизнь родителя и общий descendant budget | [runner](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyChildAttackSystem.cs) |
| Sequence + Coordinator | Coordinator управляет permission/cycle budgets Sequence-участника | Resume/restart, drain, infinite child wait | [permissions](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackBehaviourPermissions.cs), [sequence runtime](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackSequenceRuntime.cs) |
| Sequence + Attack Order | Порядок врагов внутри Attack шага | Не использовать один slot одновременно в двух steps | [controller order](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackControllerOrder.cs) |
| Post movement + Shooting | Общая текущая позиция врага, отдельные clocks | Атака может временно detach enemy от timeline | [attack motion](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveAttackMotion.cs) |
| Entry rotation + Facing | Физическая/маршрутная ориентация и visual-facing разные источники | На конкретном prefab проверить target, который вращается | [facing](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackFacing.cs), [attack motion](../../Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveAttackMotion.cs) |
| Origin Player + Aim mode | Capture координаты источника не равен фиксации направления прицеливания | Circle имеет отдельные направления от каждого emitter | [origin](../../Assets/Scripts/Gameplay/Enemies/EnemyEmissionOriginSettings.cs), [aim](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackModels.cs) |
| Sequential Sector + Origin/random samples | Снимок одного залпа переживает delayed launches | AimContinuous может обновлять направление, не обязан пересэмплировать origin | [executor](../../Assets/Scripts/Gameplay/Enemies/SimpleEnemyAttackExecutor.cs) |
| Sector PingPong + interruption | Только completed sector продвигает порядок | Reset On Attack Start / source identity | [sector registry](../../Assets/Scripts/Gameplay/Enemies/EnemySectorLaunchOrderState.cs) |
| Base speed override + Random initial speed | Initial Random выбирает свой диапазон | Fixed base speed в этом режиме не источник результата | [shooting](../../Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.cs) |
| Local speed + shared wave speed | Явный local projectile override сохраняется | Не спутать local geometry-only path с legacy full override | [resolver](../../Assets/Scripts/Gameplay/Enemies/EnemyAttackSettingsResolver.cs), [local](../../Assets/Scripts/Gameplay/Waves/DirectedWaveEnemyOverride.cs) |
| Projectile Preset + Attack Pattern Preset | Раздельные списки Projectile/Layout/Timing; Child сохраняется в Projectile | Linked materialization и local paths | [preset fields](../../Assets/Scripts/Gameplay/Enemies/EnemyPresetFields.cs) |
| Presets + Randomization | Ranges/scopes сериализуются рядом с fixed values и копируются | Не сохранять runtime sample; проверить Linked/Copy | [preset fields](../../Assets/Scripts/Gameplay/Enemies/EnemyPresetFields.cs), [numeric](../../Assets/Scripts/Gameplay/Enemies/NumericRandomization.cs) |
| Preview + Fast Seek + target history | Replay fixed frames из snapshot + recorded inputs | Изменение прошлого инвалидирует будущее | [player](../../Assets/Editor/WavePreviewPlayer.cs), [history](../../Assets/Editor/WavePreviewTargetHistory.cs) |
| Runtime Level multiplier + standalone Preview | Preview знает Wave, а не полный боевой LevelContext | Совпадение fireRate не обещать только по одинаковой Wave ссылке | [conductor](../../Assets/Editor/WaveConductorShootingPreview.cs), [modifier](../../Assets/Scripts/Gameplay/Waves/WaveEnemyDifficultyModifier.cs) |

## Ограничения и неочевидные особенности

1. **Snapshot не универсальное сохранение боя.** Memento привязан к своему conductor/ECS World; не переносится в Runtime-сцену или другой экземпляр симуляции. [StateGraph](../../Assets/Editor/WavePreviewStateGraph.cs), [ConductorSnapshots](../../Assets/Editor/WaveConductorPreviewSnapshots.cs).
2. **Preview Duration — горизонт.** Кнопка Base Route не знает момент смерти последнего врага; loops и Coordinator могут работать дольше маршрута. [BaseRouteDuration](../../Assets/Editor/WavePreviewPlayer.cs).
3. **Боевые исходы не моделируются.** У Composite есть частный геометрический контакт частей с synthetic target .05, а ordinary bullets не проходят player collision в Preview; это не симуляция HP.  Поведение FirstCompleted/AllCompleted по циклам можно исследовать, но onEnemyDeath, награды и очистку после победы нельзя считать испытанными этим Player. [Window help](../../Assets/Editor/WavePreviewPlayerWindow.cs).
4. **Player-dependent dive не двигает врага как в бою.** Это прямо выдаваемое Warning, а не предположение об одинаковости. [BuildSlot](../../Assets/Editor/WaveShootingPreviewSession.cs).
5. **Ограничения визуализации существуют.** 8192 live preview projectiles, 128 directions, 512 volley/step budget; rotation coverage — до 32 sampled rays и до 24 промежуточных поз. Omitted не равно неисправной матрице Formation. [session](../../Assets/Editor/WaveShootingPreviewSession.cs), [rotation guides](../../Assets/Editor/EnemyShootingPreview.Rotation.cs).
6. **Child ограничен 8 поколениями/2048 descendants/512 per step/4096 tracked parents/8192 live.** Repeat 1…32. Composite отнимает бюджет несколькими entities на фигуру. [Child settings](../../Assets/Scripts/Gameplay/Enemies/EnemyChildAttackSettings.cs).
7. **Coordinator randomSeed зарезервирован.** Случайный порядок Coordinator не реализован только наличием этого поля. [settings](../../Assets/Scripts/Gameplay/Waves/WaveAttackCoordinator.cs).
8. **loopCount имеет разный контракт.** Sequence loopCount=0 — infinity кроме Once; Coordinator infinity задаётся loopSequence=true, а loopCount работает при false; Child RepeatCount — общее число запусков. [sequence](../../Assets/Scripts/Gameplay/Waves/DirectedWaveAttackSequence.cs), [coordinator](../../Assets/Scripts/Gameplay/Waves/WaveAttackCoordinatorPlayer.cs), [child](../../Assets/Scripts/Gameplay/Enemies/EnemyChildAttackSettings.cs).
9. **Legacy поля не обязательно активны.** DamageMultiplier Level/Wave returns 1; LegacyAttack — сохранение enum ID; старый DrawMobileBoundsControls не имеет вызывающего места в текущем Subwave editor. [modifier](../../Assets/Scripts/Gameplay/Waves/WaveEnemyDifficultyModifier.cs), [editor](../../Assets/Editor/DirectedEnemySubWaveEditor.cs).
10. **Правки конфигурации не равны scrubbing времени.** Изменение seed/config invalidates state; обычное перемещение playhead выбирает snapshot/replay. [player](../../Assets/Editor/WavePreviewPlayer.cs).
11. **Источник границ важен.** Runtime использует реальную gameplay camera, Editor адаптер — Fighting scene/дисплей. Library preset preview имеет собственную тестовую сессию и не является доказательством совпадения всего Level/HUD. [camera](../../Assets/Editor/WavePreviewGameplayCamera.cs), [library preview](../../Assets/Editor/EnemyPresetPreviewSession.cs).
12. **Точность навигации Coordinator — fixed frame.** Time и StateTime различаются на один StepInterval; внутренние смены без изменения active set не видны как отдельный event. [event](../../Assets/Editor/WavePreviewCoordinatorEvent.cs).
13. **Неподдерживаемые комбинации Composite** отвергаются явно: BurstAtPoint component, HitPlayer child центра, вложенные фигуры внутри components. [validation](../../Assets/Scripts/Gameplay/EnemyProjectiles/EnemyProjectileEcsSpawner.Composite.cs).
14. **Не все числа рандомизируются одинаково.** Атрибут задаёт разрешённые scopes. Legacy InitialSpeed Random без seeded toggle не становится автоматически новым детерминированным stream. [numeric](../../Assets/Scripts/Gameplay/Enemies/NumericRandomization.cs), [shooting](../../Assets/Scripts/Gameplay/Enemies/EnemyShootingSettings.cs).
15. **Кривые/длины аппроксимируются.** Arc length uses 24 samples; Scene coverage тоже sampled. Это численная модель, не математически точная непрерывная проверка каждого пикселя. [path evaluator](../../Assets/Scripts/Gameplay/Waves/DirectedWavePathEvaluator.cs).

## Скрытые возможности, которые уже есть

| Возможность | Где искать | Практический смысл |
|---|---|---|
| Независимые координаты Origin X/Y | Shooting Behaviour → Emission Origin | Стрельба над игроком из-за верхней границы без перемещения врага |
| Capture mode и Randomize On — разные настройки | Origin ось | Зафиксировать player X, но пересэмплировать offset на очереди |
| Circle per-emitter aim | Circle + Aimed/WorldPoint | Каждый эмиттер целится от собственной точки, а не копирует центральный вектор |
| Per-direction/per-projectile geometry randomization | Кубик поля с соответствующим scopes | Неровные веера/радиусы при сохранении детерминированности |
| Composite Initial Rotation Random | Composite Rotation → Initial Rotation | Поворачивать целую фигуру случайно для каждого элемента верхнего залпа |
| Repeat Child после полного delayed Sector | Child Repeat Interval | Каскады без наложения незавершённого сектора на следующий repeat |
| Выбранные boundary линии за пределами viewport | Child boundary flags | Игнорировать Top и запустить ребёнка позже при пересечении Right |
| ScreenHold отдельно от LifetimeHold | Child + DestroyOutside | Сохранить полёт к выбранной стороне без бессрочной жизни |
| Partial linked preset overrides | Preset local fields | Обновлять общий layout, сохраняя особую скорость/цвет конкретного слота |
| PatternId для Facing | Multiple → Facing FollowPattern | Перестановка потоков не должна менять выбранный визуальный источник |
| Entrance checkpoint count scope | Across Checkpoints | Равномерно распределить общие залпы по группе вместо N на каждого |
| FormationReorder target center / batches | Post → Reorder | Менять роли и переводить строй в новую область без respawn |
| Background Parallel | Post → Parallel | Добавить Wobble поверх дальнейшего движения |
| Automatic recording при drag target | Wave Preview Player | Менять цель без сброса текущего мира и точно повторять её историю |
| Изолированный future Coordinator search | Next Event | Искать передачу управления, не двигая видимый текущий кадр |
| Seed offset 0 / Repeat / New / Runtime Seed | NumericRandomPreviewControls | Разделить повтор authored seed и новую генерацию Preview |
| Legacy compatibility snapshots | Component configuration resolver | Читать старые prefab без автоматической перезаписи YAML |
| Legacy Circle menu / фактический PatrolMovement | SOStrategyMovement asset | Несмотря на имя, горизонтальный DOMoveX Yoyo, не окружность |
| Stencil/inverted warning geometry | Danger Warning | Подсветить опасную область вокруг безопасного отверстия |

Все перечисленные элементы имеют отдельные исходники в [реестре скриптов](WaveEditorScriptsReference.md); описания назначения и условий находятся в [полном руководстве](WaveEditorCompleteGuide.md).

## Проверка полноты и границы исследования

Исследование начинается с graph File inventory для Waves, Enemies, EnemyProjectiles, Editor, затем дополняется Level/camera/UI viewport, legacy encounters/strategy movement, DI factory, боевыми boundary-потребителями и BossFight encounter с его отдельными редакторами/пулом. Покрытие MCP best-effort сообщило coverage_unavailable; актуальные исходники прочитаны напрямую. Граф использован для нахождения классов и связи TryCopyResolvedAttackSettingsTo с editor/runtime consumers; результаты resolver не принимались за доказательство без проверки текста.

Реестр в ScriptsReference содержит отдельный раздел на каждый включённый файл, типы, авторские/открытые поля, enum, исходные defaults, операции и основные статические связи. Для partial-класса разбор разделён по файлам. Legacy и вспомогательные редакторы включены, а несвязанные редакторы кораблей/оружия, Analytics/Localization/сохранения и сторонние plugin internals исключены. AgentScripts проверки не являются возможностями Wave Editor и в основной реестр не включены.

Это документирование кода, а не поиск/исправление дефектов: здесь не утверждается отсутствие runtime ошибок, полная визуальная эквивалентность каждого сложного паттерна или успешная проверка Android. Идентичность содержимого исследованных C# файлов контролируется при подготовке документации; ограничения и непроверенные динамические сценарии оставлены явными.


## Итог реестров

Реестр A: **224 файла** с самостоятельными разделами и прямыми ссылками — в [Scripts Reference](WaveEditorScriptsReference.md#реестр-скриптов). Реестр B: таблица поддержки и карта Inspector выше связывают обнаруженные функции с их владельцами; полный набор **108 enum** и **1852 field declarations** (включая runtime storage, не только UI) приведён в [справочнике](WaveEditorScriptsReference.md#справочник-enum). Реестры C/D: ограничения и скрытые возможности выше. **14 практических сценариев** — в [Practical Examples](WaveEditorPracticalExamples.md).

При финальной проверке сверены 7106 локальных ссылок и 224 source fingerprints, без ошибок. Динамические проверки/Play/сборки не выполнялись и не подменяются этими проверками документации.
