# Enemy Laser

В `AttackBehaviour → Projectile Settings → Projectile Type` выбрать **Laser**.
Те же поля доступны в Single/Multiple Patterns, шагах Sequence, Presets, Local Overrides и Child Attack Pattern.
Существующие волны не перенастраиваются. Отдельный prefab лазера не требуется.

## Настройка

- **Geometry**: To Screen Edge (по умолчанию) или Custom; длина, Length Offset, ширина и Laser Offset.
  Laser Offset — дополнительное мировое смещение X/Y от рассчитанной точки Emission Layout.
  Автоматическая длина использует игровой viewport камеры из Emission Origin. Источник снаружи экрана,
  направленный внутрь, даёт луч до дальней границы; при направлении от экрана автоматическая длина равна нулю.
- **Size Behaviour**: Constant Width/Length включены по умолчанию. Выключение открывает соответствующую
  кривую-множитель по нормализованному времени. Constant Length фиксирует базовую длину в момент активации.
  При переменной длине To Screen Edge пересчитывается с учётом Follow Origin и текущего viewport.
- **Duration**: Activation Delay → Telegraph Duration (если включён) → Laser Duration → Fade Out Duration.
  Activation Delay — невидимая задержка перед предупреждением. Fade In находится внутри активного периода;
  Fade Out — дополнительный визуальный хвост без урона.
- **Follow Origin**: следует только позиция. Enemy/Player оси следуют соответствующим источникам,
  World Absolute остаётся фиксированным, World Boundary учитывает изменение viewport. Случайные смещения
  и смещение эмиттера фиксируются при выпуске. Направление уже выпущенного луча не поворачивается.
- **Survive Source Destruction**: включено по умолчанию. При потере источника луч сохраняет последнюю
  позицию. Выключение отменяет луч, включая предупреждение и его незавершённые дочерние атаки.
  Остановка расписания не удаляет уже выпущенные лучи.
- **Damage**: заданный урон × существующий множитель урона врага. On Contact — одно попадание при входе
  в область; повторное возможно после выхода и нового входа. Periodic — первое попадание сразу, затем
  не чаще Damage Interval. Оба режима используют существующие щиты, защитные способности и неуязвимость.
  Старые обычные попадания сохраняют правило одной единицы урона.
- **Telegraph**: длительность, цвет, прозрачность и затухания. Без Override Telegraph Geometry используется
  тот же объект геометрии, что у основного луча. Override открывает отдельную геометрию и кривые предупреждения.
  Предупреждение не наносит урон. Направление при переходе в активное состояние сохраняется.
- **Цвет и уничтожение**: используются существующие Projectile Colour и Normal/Fragile. Fragile сохраняет
  розовый цвет активного луча; цвет Telegraph задаётся отдельно. Перехватчики учитывают геометрию луча и
  существующий контракт уничтожения. Очистка вокруг повреждённого игрока также проверяет геометрию луча.

## Preview и Scene View

Preview Wave использует существующее расписание залпов, Burst, Sector, Attack Sequence и координатор.
Для одиночного executor Layout Preview Time показывает один залп с его жизненным циклом и дочерними атаками.
Сохраняются Fan/Stack/Spread, поворот выпуска и несколько одновременных лучей.

В Scene View жёлтые ручки редактируют длину/Length Offset и ширину. Направление редактируется через
существующие углы Emission Layout, а для Point + Stationary Angle — через угол Stationary.
В Aimed направление определяется существующими Fire/Aim Mode. Самостоятельного вращения лазера нет.
Изменения используют SerializedProperty, Undo/Redo и Prefab Overrides.

## Child Attack Pattern

Используется общий генератор, бюджет потомков и ограничения глубины. Поддерживаются On Spawn, After Time,
Hit Player, Expired, Intercepted. Попадание не уничтожает луч. After Distance и Screen Boundary относятся
к перемещению **начала** луча при Follow Origin; конец неподвижного луча не считается летящим снарядом.
Burst At Point относится к соответствующему прежнему типу снаряда. Destroy Parent ждёт завершения
запланированных дочерних залпов; жизненный цикл обычных визуальных эффектов не блокирует Sequence.

## Реализация

- `EnemyLaserSettings.cs`: сериализованные настройки и общий объект геометрии.
- `EnemyAttackProjectileBuilder` / `EnemyProjectileEcsSpawner`: существующая генерация, расширенная типом Laser.
- `EnemyLaserRuntime.cs`: жизненный цикл и общий прозрачный URP Unlit quad; без GameObject/Collider2D на луч.
  Runtime использует Entities Graphics, Preview — тот же quad с переиспользуемым MaterialPropertyBlock.
- `EnemyLaserGeometryUtility` / `EnemyLaserOrigin`: viewport, геометрия и привязка источника.
- `EnemyProjectileCollisionRegistry` / `ParentShip`: существующий канал урона с явным разрешением заданного Laser Damage.
- `EnemyLaserSettingsDrawer` / `EnemyLaserScenePreview`: Inspector и Scene View.

## Проверки

Проверки выполняются через Pipeline в изолированных ECS worlds/Preview Scenes, без сохранения игровых assets:
`VerifyEnemyLaser`, `VerifyEnemyLaserEditor`, `CaptureEnemyLaser`; результаты находятся в `AgentArtifacts/laser-*.json`.
Изображения `laser-active.png` и `laser-telegraph.png` подтверждают реальную отрисовку общего URP quad.
Совместимость ограничена семью согласованными extreme-префабами: 29 подволнами и 259 слотами.
Полный бой в Play Mode и производительность на Android-устройстве не проверялись.
