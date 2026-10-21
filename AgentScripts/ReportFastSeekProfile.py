from pathlib import Path
import json, statistics, csv, hashlib

root = Path(__file__).resolve().parents[1]
art = root / 'AgentArtifacts'
def load(i, v, r=1): return json.loads((art/f'fast-seek-profile-{i}-{v}-{r}.json').read_text(encoding='utf-8-sig'))
def avg(rows, key): return statistics.mean(r[key] for r in rows)
def dist(values):
    s=sorted(values);n=len(s)
    import math
    return [statistics.mean(s),min(s),max(s),s[math.ceil(.95*n)-1],s[math.ceil(.99*n)-1]]
def f(x, n=2): return f'{x:,.{n}f}'.replace(',',' ')
def table(headers, rows):
    return '\n'.join(['| '+' | '.join(headers)+' |','|'+'|'.join(['---']*len(headers))+'|']+['| '+' | '.join(str(c) for c in row)+' |' for row in rows])+'\n'
primary=[[load(0,'profile',2),load(0,'profile',3)]]+[[load(i,'profile',1),load(i,'profile',2)] for i in range(1,4)]
base=[[load(i,'off',1),load(i,'off',2)] for i in range(4)]
control=[load(i,'profile',10) for i in range(4)]
native=[load(i,'alloc',2 if i==0 else 1) for i in range(4)]
names=[r[0]['wave'] for r in base]
areas={}
for i,rows in enumerate(primary):
    areas[i]={a['name']:{k:statistics.mean(r['categories'][j][k] for r in rows) for k in ['selfMs','inclusiveMs','calls','maximumMs']}
              for j,a in enumerate(rows[0]['categories'])}
groups={
'Wave / SubWave (включая Formation, Entry, Post, Morph/Reorder)': ['InitializeCue','Positions','WaveEvaluate','PostPipeline','PostCommand','Entrance','CheckpointPath','Formation'],
'Attack / Sequence / Cooldown / Coordinator / emission': ['Coordinator','SessionTick','AttackSequence','AttackGroup','AttackSlot','QueueVolley','EmitDirection','EmitterGeometry'],
'Projectile motion / curves / lifetime / cleanup': ['ProjectileStep','BurstBounds','Homing','Burst','SpeedCurve','SizeCurve','Movement','Lifetime','Cleanup','BurstSpawn','Spawn'],
'Child Attack Pattern': ['Child','ChildTriggerUpdate','ChildVolley','ChildEmit'],
'Laser / Telegraph': ['Laser','LaserGeometry'],
'Snapshots / cache': ['SnapshotCapture','SnapshotManaged','SnapshotNative','SnapshotVisuals','SnapshotRestore','CacheTrim'],
'Presentation / визуальные Unity API': ['Facing','Publish','PublishVisuals','CaptureRender','Visibility'],
'Transport / input / прочее в шаге': ['PlayerUpdate','FixedStep'],
}
md=['# Wave Preview Player — профилирование Fast Seek\n',
'Дата: 21.10.2026. Диагностика; оптимизации игровой логики не выполнялись.\n',
'''Главное ограничение — стоимость фиксированного шага при получении конфигурации и пересчёте Formation. Основной источник этой стоимости — миллионы повторных `GetComponent<T>()` для отсутствующих компонентов. `cachedComponent ??= GetComponent<T>()` не кеширует отрицательный результат. В обычном прогоне ожидание между обновлениями составляет около 1–2%; увеличение лимита шагов само по себе причину не устранит.

В открытом окне дополнительно измерена заметная стоимость IMGUI. Снимки дают небольшой вклад во время, но их оценка памяти занижена. Снаряды, GC и частота Editor Update не являются главным ограничением в этих четырёх конфигурациях.

## Условия и воспроизводимость

- Unity 6000.3.2f1, CLI 1.0.0-beta.12, существующий Editor проекта `C:\\My project`; Play Mode выключен.
- AMD Ryzen 7 5800X, 8 ядер / 16 потоков. Данные процесса и оборудования: [environment](fast-seek-profile-environment.json).
- Четыре исходных prefab: `1_1_extrime`, `1_4_extrime`, `2_2_extrime`, `2_3_extrime`.
- Seed 53, неподвижный игрок `(0, -3, 0)`, горизонт Preview 65 с, Seek 0→60 с.
- Fixed Step = 1/30 с; каждый прогон — ровно 1800 шагов. Бюджет 24 мс, Max Steps Per Update 2048, обычный лимит снарядов 2048.
- Snapshot Interval 2 с, оценочный лимит кеша 128 МиБ, максимум 32 записи.
- AutoTick 16 мс. Прогоны выполнялись последовательно, через `EditorApplication.update`; внутри шага нет `Task.Yield` и искусственных ожиданий.
- Перед основной серией — полный прогрев 0→60 для каждой волны. Затем по два прогона с выключенными метками и по два с включёнными, чередованием. Для `1_1` используются profile-2/3: первый пилот не сохранил вложенные статистики из-за особенностей `JsonUtility` для динамического скрипта.
- В основных прогонах окно Player закрыто, чтобы измерить тот же двигатель расчёта, что использован в прежнем benchmark. Отдельно выполнен прогон с реальным открытым окном. Расходы UI не смешаны с чистой симуляцией.
- Префабы не сохранялись и не изменялись. Для каждого запуска проверены неизменность байтов prefab и восстановление исходного числа ECS World после Dispose.

Серия A — повторяемые baseline/profile после установки основных меток. Серия B — изоляции и близкие контрольные profile-10 после добавления диагностических счётчиков. Между сериями происходил reload; менялись прогрев, состояние памяти Editor и данные Profiler. Поэтому абсолютные времена A и B не используются как доказательство эффекта отдельной оптимизации. Особенно заметен разброс `2_3`: около 42 с в A и около 37 с в B при одинаковом числе вычислений. Исторические 30,66/40,15/41,95/35,67 с служат только контекстом.

## Как измерено

`WavePreviewProfiler` выключен по умолчанию и полностью исключён из Player build через `UNITY_EDITOR`. Его вложенные scopes используют `Stopwatch.GetTimestamp`, предвыделенный стек и счётчики. В таблицах `self` — время без вложенных scopes, поэтому категории можно складывать. `inclusive` используется только там, где это явно указано.

Шаг измеряется вокруг тела одного fixed-step, включая callback сохранения снимка. `Player.Update` включает первоначальную подготовку, шаги и финальную публикацию. Время вне вызовов — разница wall time и суммы длительностей вызовов; это работа прочего Editor и ожидание, а не доказанное бездействие CPU.

Настоящее время CPU основного потока отдельно получено Windows `GetThreadTimes` (user + kernel) на границах каждого вызова Player.Update. Оно включает подготовку и финальную публикацию; деление на 1800 даёт CPU на шаг с этими накладными расходами. Отдельный CPU clock для каждого шага не снимался: это изменило бы горячий цикл. `Stopwatch` шага — main-thread elapsed/service time, включающий вытеснение потока и возможные ожидания внутри Unity API. CPU всего процесса также сохранён в JSON, но не приписывается симуляции: туда входят фоновые потоки Editor.

Процентили шага рассчитаны по объединённым 3600 шагам двух профилированных повторов. Min=0 шагов на Update допустим: первый вызов может полностью израсходовать бюджет на инициализацию. Бюджет кооперативный: неделимый шаг/снимок может превысить 24 мс.

## Основные времена: серия A
''']
rows=[]
for i in range(4):
    wall=avg(base[i],'wallSeconds'); active=avg(base[i],'activeMilliseconds'); wait=avg(base[i],'waitingMilliseconds')
    rows.append([names[i],f(wall),f(60/wall),f(active/1000),f(wait/1000),f(100*wait/(1000*wall)),f(1800/wall),f(avg(base[i],'threadCpuMilliseconds')/1800,3)])
md.append(table(['Волна','Wall, с','Ускорение ×','Внутри Update, с','Вне Update, с','Вне, %','Шагов/с','CPU main/шаг, мс'],rows))
md.append('\nПовторы без меток и накладные расходы лёгких измерений:\n')
md.append(table(['Волна','Off #1 / #2, с','Profile #1 / #2, с','Средний overhead, %'],[[names[i],'/'.join(f(r['wallSeconds']) for r in base[i]),'/'.join(f(r['wallSeconds']) for r in primary[i]),f((avg(primary[i],'wallSeconds')/avg(base[i],'wallSeconds')-1)*100)] for i in range(4)]))
md.append('\nРаспределение стоимости фиксированного шага, мс:\n')
md.append(table(['Волна','Mean','Min','Max','P95','P99'],[[names[i]]+[f(x,3) for x in dist([x for r in primary[i] for x in r['rawStepMs']])] for i in range(4)]))
md.append('\nОбработка за один Editor Update (off, объединённые повторы):\n')
md.append(table(['Волна','Вызовов (среднее)','Шагов mean / min / max','Вызов mean / max, мс','Промежуток mean / P95 / max, мс'],[[names[i],f(statistics.mean(len(r['batches']) for r in base[i]),1),
    ' / '.join(f(x,2) for x in [statistics.mean(b['steps'] for r in base[i] for b in r['batches']),min(b['steps'] for r in base[i] for b in r['batches']),max(b['steps'] for r in base[i] for b in r['batches'])]),
    ' / '.join(f(x,2) for x in [statistics.mean(b['milliseconds'] for r in base[i] for b in r['batches']),max(b['milliseconds'] for r in base[i] for b in r['batches'])]),
    ' / '.join(f(x,3) for x in [statistics.mean(b['gapMilliseconds'] for r in base[i] for b in r['batches'][1:]),dist([b['gapMilliseconds'] for r in base[i] for b in r['batches'][1:]])[3],max(b['gapMilliseconds'] for r in base[i] for b in r['batches'][1:])])] for i in range(4)]))
md.append('''
Число шагов 1,7–2,2 за вызов объясняется ценой шага около 16–24 мс и бюджетом 24 мс. Ограничение 2048 шагов не достигается. Паузы между вызовами малы; повторная отправка Editor tick уже работает. Большой Max одного вызова связан с атомарной подготовкой/шагом/снимком, а не с жёстким соблюдением бюджета.

## Стоимость подсистем

В каждой ячейке: **общее self-время, мс / среднее на 1800 шагов, мс / % времени Player.Update**. Подготовка и финальная публикация включены в соответствующие категории. Сумма процентов близка к 100%; небольшая разница — измерительная обвязка.
''')
rows=[]
for group, keys in groups.items():
    cells=[]
    for i in range(4):
        value=sum(areas[i][k]['selfMs'] for k in keys)
        denominator=areas[i]['PlayerUpdate']['inclusiveMs']
        cells.append(f'{f(value,1)} / {f(value/1800,4)} / {f(value/denominator*100,2)}%')
    rows.append([group]+cells)
md.append(table(['Категория']+names,rows))
md.append('''
Подробные маркеры, включая отдельные Homing, BurstAtPoint, Speed Curve, Size Curve, Movement, Lifetime, Cleanup, emission и snapshot, сохранены в [CSV всех категорий](fast-seek-profile-categories.csv) и в JSON каждого прогона. Стоимость Unity API относится к вызывающему участку: дорогие GetComponent уже входят в Wave/Formation; строка визуальных API не является суммой всех Unity API.

Angular Turn, Lateral, Orbit и DNA выполняются в общем `ProjectileMovementSystem.OnUpdate`. Замерено время всего системного прохода; отдельные ветви внутри Burst-совместимого цикла по каждому снаряду не размечались, чтобы не добавлять маркер на каждый объект. Аналогично Post включает Morph/Reorder и обработчики команд: семейство измерено, отдельные типы команд не изолированы. Это ограничение детализации, а не нулевая стоимость этих операций.

Во всех четырёх прогонах `ChildTriggerUpdate`, `ChildVolley`, `ChildEmit` и `LaserGeometry` имеют **0 вызовов**. Вызвано по 1800 пустых обновлений Child/Laser — это стоимость готовности подсистем, а не нагрузочный тест нескольких поколений/Telegraph. Производительность этих ветвей на насыщенных ими волнах не установлена. Функциональные fixtures с Child/Laser проверены отдельно.

## Пять самых дорогих участков

Здесь используются **self**, чтобы не считать вложенную стоимость дважды. Ячейка — мс и % Player.Update; сортировка по суммарной стоимости четырёх волн.
''')
locations={
'Formation':('DirectedEnemySubWave.CreateSimulationFormationPositions','Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveSimulation.cs'),
'PostCommand':('DirectedEnemySubWave.EvaluateSimulationCommandFrame','Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveSimulation.cs'),
'WaveEvaluate':('DirectedEnemySubWave.EvaluateSimulationPreviewInternal','Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveSimulation.cs'),
'Entrance':('DirectedEnemySubWave.EvaluateSimulationEntrancePosition','Assets/Scripts/Gameplay/Waves/DirectedEnemySubWaveSimulation.cs'),
'SnapshotManaged':('WaveConductorShootingPreview.Snapshot constructor','Assets/Editor/WaveConductorPreviewSnapshots.cs')}
for key in sorted(locations,key=lambda k:sum(areas[i][k]['selfMs'] for i in range(4)),reverse=True):
    name,path=locations[key]; source=(root/path).read_text(encoding='utf-8-sig').splitlines();needle='internal Snapshot(' if key=='SnapshotManaged' else name.split('.')[-1]+'('
    line=next(j+1 for j,s in enumerate(source) if needle in s and ('private' in s or 'internal Snapshot' in s))
    md.append(f'\n### `{name}`\n\n[Исходник](../{path}#L{line}).\n')
    md.append(table(['Волна','Self, мс','%'],[[names[i],f(areas[i][key]['selfMs'],1),f(areas[i][key]['selfMs']/areas[i]['PlayerUpdate']['inclusiveMs']*100,2)] for i in range(4)]))
md.append('''
`SnapshotManaged` — внешний конструктор снимка за вычетом размеченных native-copy и визуальных данных. Он включает reflection-capture управляемого графа и создание ECS storage World, происходящее в инициализаторе поля дочернего snapshot. Поэтому это не «чистое время одного managed-copy». Native CopyEntitiesFrom указан отдельно в CSV.

## Подтверждённая причина: отрицательный результат поиска не кешируется

В `DirectedEnemySubWaveBehaviourConfiguration.cs`:

```csharp
cachedFormationBehaviour ??= GetComponent<DirectedWaveFormationBehaviour>();
```

Если компонента нет, поле остаётся null и следующая попытка повторяет Unity API. Та же схема используется для Phase Entry, Post и Completion. Приватные свойства FormationConfig/PhaseEntryConfig/PostBehaviourConfig обращаются к резолверам; `UsesLegacyConfigurationCompatibility` сначала вычисляет `UsesSeparateBehaviourComponents`, проверяя четыре компонента, и лишь затем версию конфигурации. Поэтому чтение очередного числового свойства может снова запускать несколько поисков.

Цепочка горячего вызова:

`CreateSimulationFormationPositions → GetSimulationFormationPosition → GetFormationPosition → CalculateFormationPosition → свойства конфигурации → UsesLegacyConfigurationCompatibility / Resolve… → GetComponent`.

`CalculateFormationPosition` собирает структуру из многих отдельных свойств для каждого слота. Formation пересобирается на каждом требующем движения шаге, хотя исходная геометрия формирования обычно постоянна. Post/Entry дополнительно читают те же настройки.

Точное число попыток поиска отсутствующего компонента за 1800 шагов (контроль серии B):
''')
md.append(table(['Волна','Formation','Phase Entry','Post','Completion','Всего'],[[names[i]]+[f(control[i]['counters'][k],0) for k in ['FormationLookupMiss','PhaseLookupMiss','PostLookupMiss','CompletionLookupMiss']]+[f(sum(control[i]['counters'][k] for k in ['FormationLookupMiss','PhaseLookupMiss','PostLookupMiss','CompletionLookupMiss']),0)] for i in range(4)]))
md.append('''
Счётчики Resolve и LookupMiss совпадают для этих четырёх типов. Обращений к `GetLegacyCompatibilityFormation/PhaseEntry/PostBehaviour` — 0: дорогостоящая проверка компонентов выполняется даже тогда, когда в итоге выбираются современные встроенные настройки, а не legacy-перенос. Поиск AttackBehaviour этим счётчиком не охвачен; приведённое число не выдаётся за все вызовы GetComponent во всём Editor.

Во втором, более тяжёлом проходе использован встроенный Unity Profiler без Deep Profiling. Он показывает `GetComponentNullErrorMessage` и `GetComponentNullErrorWrapper`. Это внутренний путь обработки отсутствующего компонента, **не миллионы сообщений в Console**. Время маркера вложено в Formation/Post/Entry и не складывается с ними повторно:
''')
md.append(table(['Волна','GetComponentNullErrorMessage, с','% активного времени native-прохода','GetComponentNullErrorWrapper, мс'],[[names[i],f(native[i]['nativeAllocations']['unityMarkerMs'].get('GetComponentNullErrorMessage',0)/1000),f(native[i]['nativeAllocations']['unityMarkerMs'].get('GetComponentNullErrorMessage',0)/native[i]['activeMilliseconds']*100),f(native[i]['nativeAllocations']['unityMarkerMs'].get('GetComponentNullErrorWrapper',0),1)] for i in range(4)]))
md.append('''
Нативные marker totals в этой таблице собраны по всему main-thread Editor frame, а не только внутри Player.Update. Доля относительно активного времени — сравнительный ориентир; полное приписывание этой суммы FastSeek было бы верхней оценкой. Scoped allocation и собственные счётчики независимо локализуют проблему в Wave/Formation. Ни замена всей системы движения, ни переход на Jobs не нужны для устранения повторных поисков.

## GC и память

В этой версии Mono `GC.GetAllocatedBytesForCurrentThread()` всегда возвращал 0. Соответствующие нули в ранних JSON (`allocatedMainThreadBytes`, `rawStepBytes`, bytes в scopes) **не являются измерением отсутствия allocations** и не используются для выводов.

Для точных байтов выполнены отдельные native-проходы: `GC.Alloc` metadata суммируются только внутри собственного маркера `FastSeek.FixedStep`; дополнительно выделены Player.Update и внешняя работа. Все 1800 шагов каждого прохода захвачены, пропусков кадров raw-профайлера нет. Начальная подготовка до первого fixed-step не входит в «за 1800 шагов», но включена в соседнюю колонку Player.Update.

Native-проход включает полную запись CPU/GC-событий и чтение raw frame data. Эта операция существенно дороже лёгких scopes; её wall time не служит оценкой скорости Fast Seek. У диагностического reader было убрано повторное сканирование тех же samples; поэтому эти wall time не сравниваются даже между native-проходами. Алгоритм суммирования bytes и границы fixed-step одинаковы. Пилот `0-alloc-1` исключён: lookup отрицательных custom marker IDs тогда ошибочно их отфильтровывал; корректный `0-alloc-2` захватил все шаги.
''')
md.append(table(['Волна','Fixed-step total, GiB','Mean, MiB/шаг','Min / P95 / P99 / Max, MiB','Player.Update total, GiB'],[[names[i],f(native[i]['nativeAllocations']['stepBytes']/2**30,3),f(native[i]['nativeAllocations']['stepBytes']/1800/2**20,3),
    ' / '.join(f(v/2**20,3) for v in [min(native[i]['nativeAllocations']['bytesPerStep']),dist(native[i]['nativeAllocations']['bytesPerStep'])[3],dist(native[i]['nativeAllocations']['bytesPerStep'])[4],max(native[i]['nativeAllocations']['bytesPerStep'])]),f(native[i]['nativeAllocations']['playerBytes']/2**30,3)] for i in range(4)]))
md.append('\nБольшие выделения приходятся на те же категории. Ниже self-allocations внутри размеченных вызовов, MiB; это cumulative allocation, не живой размер heap:\n')
md.append(table(['Категория']+names,[[k]+[f(native[i]['nativeAllocations']['selfBytes'].get(k,0)/2**20,2) for i in range(4)] for k in ['Formation','WaveEvaluate','PostCommand','Entrance','SnapshotManaged','QueueVolley','BurstBounds']]))
md.append('\nСборки мусора и GC time в лёгких profile-проходах серии A, без тяжёлого raw reader:\n')
md.append(table(['Волна','GC count #1 / #2','GC.Collect среднее, мс','% активного времени'],[[names[i],'/'.join(str(r['gc0']) for r in primary[i]),f(avg(primary[i],'gcMarkerMilliseconds'),1),f(avg(primary[i],'gcMarkerMilliseconds')/avg(primary[i],'activeMilliseconds')*100,2)] for i in range(4)]))
md.append('''
Значения CollectionCount(0/1/2) в этом Mono совпадают; их нельзя складывать как три независимых поколения. `GC.Collect` — измеренный доступный marker, а не доказательство учёта абсолютно каждой фоновой/инкрементальной фазы GC. Отдельный доступный `GarbageCollectIncremental` marker в полученных raw-данных не найден. Последний незавершённый Editor frame может не попасть в лёгкий recorder.

Большой cumulative allocation подтверждён. Однако измеренное время сборок — малая доля wall time; основная стоимость находится в создании объектов/обработке Unity API до сборки, а не в самих паузах GC. Полностью отделить CPU malloc/stacktrace внутри native GetComponent от остальных внутренних операций Unity по этим маркерам нельзя.

Память Unity в off-прогонах A (MiB, абсолютный tracked peak — максимум периодических samples, не точный мгновенный пик RSS):
''')
md.append(table(['Волна','Unity tracked peak','Прирост к началу','Оценка кеша','Снимков'],[[names[i],f(avg(base[i],'unityMemoryPeak')/2**20,1),f(statistics.mean(r['unityMemoryPeak']-r['unityMemoryBefore'] for r in base[i])/2**20,1),f(avg(base[i],'snapshotEstimateBytes')/2**20,1),base[i][0]['snapshots']] for i in range(4)]))
md.append('''
`Process.WorkingSet64` внутри Unity Mono возвращал 0 и не использован. В поздних прогонах добавлен Windows `GetProcessMemoryInfo`. RSS всего Editor включает ассеты, Mono heap, компилятор и буферы встроенного Profiler; это не память одного Preview. Во время native-проходов RSS заметно выше обычного и не сопоставляется с лимитом кеша. Для off-серии A точного RSS peak нет; доступна периодическая метрика Unity выше и внешний OS sample в environment.json.

## Снимки: время, восстановление и фактический расход
''')
md.append(table(['Волна','Capture total incl., мс','Средний capture, мс','Native copy total, мс','Visual capture total, мс','Restore 58с, мс (off #1 / #2)'],[[names[i],f(areas[i]['SnapshotCapture']['inclusiveMs'],1),f(areas[i]['SnapshotCapture']['inclusiveMs']/areas[i]['SnapshotCapture']['calls'],2),f(areas[i]['SnapshotNative']['inclusiveMs'],1),f(areas[i]['SnapshotVisuals']['inclusiveMs'],2),'/'.join(f(r['restoreMs'],2) for r in base[i])] for i in range(4)]))
memory=load(1,'memory')['snapshotMemory']; first=memory[0];last=memory[-2];released=memory[-1]
md.append('''
Во всех обычных проверках готовый snapshot 58 с восстановлен на кадр 1740 с 0 дополнительных fixed-step. В таблице — непосредственно вызов Update восстановления, без ожидания очередного Editor callback; это отличается от старых асинхронных замеров 25–39 мс.

Отдельный опыт: на состоянии `1_4_extrime`, 60 с, с одним исходным снимком созданы восемь дополнительных snapshot одного и того же состояния. Принудительный GC — только до опыта и после удаления; не в обычных Seek. Unity memory delta и оценка в MiB:
''')
md.append(table(['Доп. копий','Unity delta','Mono used delta','Оценка','Capture, мс'],[[x['copies'],f((x['unityBytes']-first['unityBytes'])/2**20,3),f((x['monoUsedBytes']-first['monoUsedBytes'])/2**20,3),f(x['estimatedBytes']/2**20,3),f(x['captureMs'],2)] for x in memory]))
peak=(last['unityBytes']-first['unityBytes'])/2**20; remaining=(released['unityBytes']-first['unityBytes'])/2**20;est=last['estimatedBytes']/2**20
md.append(f'''
`-1` — все восемь копий удалены и вызван GC. Пиковый прирост {f(peak)} MiB против оценки {f(est)} MiB ({f(peak/est,2)}×). Освобождаемая часть прироста — около {f(peak-remaining)} MiB ({f((peak-remaining)/est,2)}× оценки). Оставшиеся {f(remaining)} MiB могут включать прогретые кеши/allocator; это не доказательство утечки. Все Worlds после полного Dispose возвращены к исходному числу.

Причина неточности оценки: native-snapshot добавляет фиксированные 1 MiB + число chunks ×16 KiB + entity arrays, но отдельный storage World имеет собственные метаданные и аллокаторы. Managed estimate `80 + Values.Length*32` также не является heap census. Восьми копиям одного кадра доступны общие ссылки на исходные объекты; опыт не моделирует все удерживаемые объекты разных кадров. Event arrays кеша и живой симулятор тоже не входят в этот marginal опыт. Следовательно, установленный лимит 128 MiB — лимит приближённой оценки, а не строгий лимит реального RAM.

Самые дорогие отдельные capture могут вызывать заметные пики шага. Первый capture после принудительного GC в memory-опыте не выдаётся за типичную стоимость: для типичной стоимости используются обычные профилированные прогоны.

## Изолированные сравнения: серия B

`profile-10` — контроль с нормальным лимитом 2048, снимками и отключённой промежуточной визуализацией. `noSnapshots` оставляет только необходимый baseline 0 и не создаёт новых. `minimal` выставляет диагностический лимит создания снарядов 0; расчёт атак/Formation остаётся, но результаты и потенциальные ожидания дочерних атак отличаются. Ни одна настройка исходных волн не редактируется.

Для каждой изоляции выполнен один прогон на каждой волне; это локализация, а не статистически доказанное ускорение. Контроли B не объединены с A. Таблица показывает наблюдаемую разницу; небольшие значения в пределах разброса не считаются эффектом отключения.
''')
md.append(table(['Волна','Полный контроль, с','Без новых snapshots, с','0 снарядов, с','Финальных снарядов в полном','Финальных в minimal','Оценка кеша: полный / noSnapshots, MiB'],[[names[i],f(control[i]['wallSeconds']),f(load(i,'noSnapshots')['wallSeconds']),f(load(i,'minimal')['wallSeconds']),control[i]['finalProjectiles'],load(i,'minimal')['finalProjectiles'],f(control[i]['snapshotEstimateBytes']/2**20)+' / '+f(load(i,'noSnapshots')['snapshotEstimateBytes']/2**20)] for i in range(4)]))
md.append('''
Уменьшение числа снарядов до нуля не даёт крупного выигрыша. Это согласуется с очень малым измеренным временем ECS-систем. Отключение снимков не устраняет дорогие миллионы обращений к конфигурации. Разницу полного wall time нельзя целиком приписывать capture: отдельный scoped capture даёт более надёжную оценку его вклада.

`visuals` включает публикацию позиций/поворотов врагов на каждом шаге через существующий deferPresentation; GPU-отрисовку SceneView и per-step CaptureRenderState снарядов этот режим не включает. Два прогона `1_4`:
''')
md.append(table(['Режим','Wall, с','PublishVisuals, мс','Facing, мс'],[['Обычный контроль B',f(control[1]['wallSeconds']),f(next(a['inclusiveMs'] for a in control[1]['categories'] if a['name']=='PublishVisuals'),2),f(next(a['inclusiveMs'] for a in control[1]['categories'] if a['name']=='Facing'),2)]]+[[f'Позиции/повороты каждый шаг #{r}',f(load(1,'visuals',r)['wallSeconds']),f(next(a['inclusiveMs'] for a in load(1,'visuals',r)['categories'] if a['name']=='PublishVisuals'),2),f(next(a['inclusiveMs'] for a in load(1,'visuals',r)['categories'] if a['name']=='Facing'),2)] for r in (1,2)]))
gui=json.loads((art/'fast-seek-profile-1-gui.json').read_text()); ga={x['name']:x for x in gui['categories']}
md.append(f'''
### Реальное открытое окно Player

Отдельный `1_4` с настоящим окном, Timeline, Advanced Settings и диагностикой: wall **{f(gui['wallMilliseconds']/1000)} с**, Player.Update **{f(gui['activeMilliseconds']/1000)} с**, вне него **{f(gui['outsidePlayerMilliseconds']/1000)} с**. Выполнено 1800 шагов. Это один прогон с другой UI-нагрузкой, не baseline той же серии.

Измерено `{ga['WindowGUI']['calls']}` вызовов OnGUI: **{f(ga['WindowGUI']['inclusiveMs']/1000,3)} с**, из них Timeline **{f(ga['Timeline']['inclusiveMs']/1000,3)} с**. Scene GUI — `{ga['SceneGUI']['calls']}` вызова, **{f(ga['SceneGUI']['inclusiveMs'],3)} мс**. GPU/полноценная непрерывная отрисовка открытого prefab-stage этой проверкой не измерена. Оставшаяся часть времени вне Player.Update не атрибутирована отдельным методам Editor; туда входят ожидание и другие работы.

Узкое место конфигурации остаётся главным, но UI — подтверждённый дополнительный расход. На каждой Layout/Repaint окно строит timeline/events и выполняет IMGUI. `DrawTimeline` перебирает всю историю событий и создаёт подписи/GUIContent. Во время Seek таймлайн продолжает обновляться, хотя промежуточный мир скрыт.

## Другие проверенные источники лишней работы

| Участок | Что подтверждено | Значимость сейчас |
|---|---|---|
| Formation | Полная сборка начальных позиций и структуры settings из многих свойств на движущихся шагах; повторный обход всех слотов | Главный кандидат после исправления поиска компонентов |
| Phase/Post | Повторное чтение флагов, post-start, durations и текущих команд; getter запускает те же поиски компонентов | Большой измеренный вклад, нельзя считать его только математикой кривых |
| Маршруты | Строятся массивы runtime checkpoints, повторно суммируются duration; сам размеченный EvaluateSimulationCheckpointPath мал | Чистая математика Bezier/CatmullRom/Arc не доказана как узкое место |
| Presets | BuildSlot/Sequence разрешают profile при создании; QueueVolley получает shooting geometry; ResolveProjectilePrefab вызывается снова при создании requests | Повторные обращения есть, но attack/emission суммарно малы; массового дорогого per-frame preset-copy не подтверждено |
| Копирование | EnemyShootingSettings и spawn request передаются значениями, managed snapshot создаёт object[]/box values и reflection-walk | Основной подтверждённый большой copy/alloc участок — snapshot graph |
| Временные коллекции | BoundBurstRequests создаёт EntityQuery и временный entity array каждый шаг; Cleanup — два native array и NativeList; burst requests копируются даже при пустой очереди | Native/GC overhead подтверждён, но намного меньше конфигурации. Для BurstBounds GC.Alloc измерено 72 000 байт за 1800 шагов |
| Полные обходы снарядов | Movement/Speed/Scale/Homing/Lifetime и Cleanup проходят соответствующие queries; Child удаляет dead lineages, если они есть | Уже отдельные специализированные проходы; вред от их объединения/разбиения не доказан. Число снарядов до 2048 не объясняет основную задержку |
| Объекты Unity | Нет GameObject на каждый обычный ECS-снаряд; enemy visual hierarchy создаётся на подготовке cue. Для Laser нужен живой источник Transform | Инициализация и необходимая поддержка источника отдельно измерены; нельзя просто выключить все Transforms |
| Скрытая визуальная работа | Snapshot.CaptureVisuals читает Facing; RequiresLiveSource синхронизирует anchor даже при deferPresentation; SetVisible вызывается каждый Update | Эти операции существуют и нужны для restore/Laser, но в данных волнах дешевы. `CaptureRenderState` в нормальном Seek вызван один раз в конце |
| LINQ | В горячем пути Player/Conductor/Session/ProjectilePreviewWorld нет выявленного LINQ на каждый снаряд. LINQ есть в диагностическом reader/report, выполняется вне измеряемого body | Не подтверждён как runtime-причина |
| Snapshot cache | Линейный поиск/Trim на максимум 32 элемента и копирование event history при capture | Сам Trim мал; работа reflection/storage важнее |
| Unity synchronization | Системы обновляются вручную, structural ECS операции и CopyEntitiesFrom могут синхронизировать jobs | Отдельное worker/synchronization CPU-разделение не снято; дорогостоящие GetComponent измерены напрямую, поэтому проблема ожидания jobs не предполагается без данных |

## Рекомендации на следующий этап

Оптимизации ниже **не реализованы**. Оценки эффекта основаны на доле измеренного участка, а не на обещании конкретного ускорения.

| Приоритет / изменение | Где | Ожидаемый эффект и уверенность | Детерминированность / риск | Сложность |
|---|---|---|---|---|
| 1. Кешировать отсутствие behaviour, либо один раз собрать явно разрешённые ссылки/конфигурации | ResolveFormationBehaviour, ResolvePhaseEntryBehaviour, ResolvePostBehaviour, ResolveCompletionBehaviour; InvalidateBehaviourComponentCache и Set/Clear | Убирает миллионы подтверждённых Unity API вызовов и основной источник allocations. Высокая уверенность; выигрыш всей симуляции ещё не замерен | Низкий при корректной invalidation; обязательны Add/Remove, Undo/Redo, Prefab Stage и уничтоженная Unity-ссылка | Небольшая–средняя |
| 2. Не проверять отдельные компоненты ради legacy, если версия уже исключает legacy; не читать FormationConfig заново для каждого поля | UsesLegacyConfigurationCompatibility, CalculateFormationPosition | Снижает подтверждённое многократное разрешение конфигураций. Высокая уверенность в устранении работы, независимый прирост после пункта 1 неизвестен | Низкий, если сохранены приоритеты и отсутствие скрытых side effects от getter | Небольшая |
| 3. Кешировать исходную геометрию Formation и неизменные route/duration по revision + parent matrix | CreateSimulationFormationPositions, GetSimulationPathCheckpoints, GetSimulationPreviewPostStartTime; runtime evaluation context | Убирает повторную сборку констант. Уверенность средняя–высокая; текущая доля включает пункт 1, её нельзя второй раз обещать как отдельный выигрыш | Средний: не кешировать живые результаты Post/loop/parallel; инвалидировать настройки/координатное пространство/Undo | Средняя |
| 4. Во время Seek строить облегчённый progress UI; кешировать событийные полосы/подписи, не перестраивать весь Timeline на каждый GUI event | WavePreviewPlayerWindow.OnGUI / DrawContents / DrawTimeline / DrawEvents | Измерено 5,02 с UI в одном открытом прогоне. Средняя–высокая уверенность, но эта доля относится к открытому окну | Низкий; только представление, final publish должен сохранить правильное состояние | Небольшая–средняя |
| 5. Уточнить memory accounting storage World и managed graph; затем рассмотреть компактный capture/delta только после проверки restore | EnemyProjectilePreviewSnapshots.Snapshot, WavePreviewStateGraph, WavePreviewSnapshotCache | Сначала корректный лимит памяти; потенциально меньше capture allocations/пиков. Время snapshots — ограниченная доля; большая кратность не обещается | Для accounting низкий; для нового memento/delta высокий: aliasing, Random, pending children, Entity remap | Accounting средняя; новый формат большая |
| 6. Переиспользовать burst query, пропускать доказанно пустые временные копии, аккуратно пакетировать cleanup | EnemyProjectilePreviewWorld.BoundBurstRequests / Step | Убирает подтверждённые мелкие alloc/Unity API расходы. Высокая уверенность локально, низкая значимость до исправления конфигураций | Низкий–средний: сохранить порядок структурных изменений и safety buffers | Небольшая |
| 7. Повторить профиль после 1–3 перед изменениями ECS/Jobs/Burst или per-projectile math | ProjectileMovementSystem и связанные системы | Сейчас их доля слишком мала, чтобы оправдать переписывание | Средний–высокий при смене порядка операций/RNG/float math | Повторный замер небольшой; миграция не рекомендована |

Не следует первым шагом увеличивать Max Steps Per Update, менять fixed-step, снижать 2048 или реже сохранять snapshots ради скрытия проблемы. Это меняет компромиссы или точность, но не исправляет дорогой доступ к конфигурации.

## Что изменено и проверено

Добавлены только Editor-only измерения, счётчики и диагностические переключатели. По умолчанию Current=null, SkipNewSnapshots=false, IntermediateVisuals=false, ProjectileLimit=-1. Runtime сборка не содержит helper. Обычная геометрия, RNG, расписание, Child/Coordinator и формат snapshot не менялись.

- После изменений выполнен Pipeline recompile; `completed`, `failed=false`, без ошибок компиляции.
- `dotnet build "Assembly-CSharp.csproj" --no-restore --verbosity:quiet --nologo`: 0 ошибок, 118 существующих предупреждений (injected/неинициализированные поля и др.).
- `dotnet build "Assembly-CSharp-Editor.csproj" --no-restore --verbosity:quiet --nologo`: 0 ошибок, 0 предупреждений в этом запуске.
- Unity Test Runner обнаружил 0 EditMode tests. Вместо заявления о пустом успешном наборе запущен существующий динамический regression fixture.
- `VerifyWavePreviewFastSeek.Run`: 1020 проверок off; повтор с активным recorder — 1018 проверок, 4717 записанных шагов. Оба прогона passed; число проверок зависит от транспортных циклов. Проверяются полный managed/native state fast vs обычная симуляция, target history, rollback/retarget, транспорт, Sequence/Coordinator, Child/Laser, Random/Sector и режимы Max/8×/16×. Это два запуска существующего fixture, а не новый тест, зеркалящий реализацию.
- Во всех benchmark-прогонах 1800 шагов; для обычных off/profile/control совпали конечные числа снарядов, omitted, количество и оценка снимков. Эти счётчики не заменяют полный побитовый battle-runtime comparator.
- Все временные Worlds освобождены; по финальному read-only API — 0 Worlds, compiling=false, updating=false, все диагностические переключатели выключены.
- Индекс codebase-memory обновлён после изменений: 52 206 nodes / 173 669 edges. Существующие parser gaps в сторонних плагинах не изменялись.
- Профилировочные данные не подтверждают производительность Laser/Child под нагрузкой, детальную стоимость каждой ветви movement, GPU и всех вариантов открытого prefab-stage. Эти пункты явно не выдаются за проверенные.

### Сообщения Unity Profiler

Во время тяжёлого native-профилирования Unity Search Importer выдал два сообщения о несовпадающих BeginSample/EndSample для **SearchImporter.IndexingRequest** (21.10.2026, 07:41:43 UTC). В scopes этих сообщений — EditorApplication.delayCall / UnityEditor.Search.SearchInit, не FastSeek. Они сохранены в [санитизированном Console-логе](fast-seek-profile-console-errors-sanitized.json). Ошибок C# нет. Все 1800 FastSeek-маркеров в каждом allocation-проходе захвачены; пропусков raw frames нет.

Console не очищалась: эти два сообщения остаются видимыми. Поэтому «Console совершенно без новых записей» не заявляется. [Финальная проверка после сборок](fast-seek-profile-console-final.json) с cursor 31 вернула 0 новых ошибок; Editor ready, compiling=false, domain reload завершён. Устранение внутренней ошибки SearchImporter не входит в изменение симуляции.

## Артефакты и повторный запуск

- [Инструмент измерений](../Assets/Scripts/Gameplay/Waves/WavePreviewProfiler.cs), [runner](../AgentScripts/ProfileWaveFastSeek.cs), [генератор отчёта](../AgentScripts/ReportFastSeekProfile.py).
- JSON `fast-seek-profile-<waveIndex>-<variant>-<repeat>.json`: durations каждого шага/Update, gaps, исходные category totals, CPU, GC и memory. Индексы: 0=1_1, 1=1_4, 2=2_2, 3=2_3.
- `*-cli.json` — ответы Pipeline. Поля вложенных статистик для пилотных `0-profile-1`/warmup-0 не сериализовались; они не использованы в таблицах. `0-alloc-1` исключён по указанной причине.
- [Только изменения этого профилирования относительно начального грязного worktree](fast-seek-profiling-only.patch). Исходные байты 15 затронутых файлов сохранены отдельно; чужой diff не откатывался и не форматировался.
- [Build runtime](fast-seek-profile-build-runtime.log), [build editor](fast-seek-profile-build-editor.log), [fixture off](fast-seek-profile-validation-base.json), [fixture on](fast-seek-profile-validation-instrumented.json).

Повтор из корня проекта (в открытом Editor, вне Play Mode):

```powershell
unity command run_script --file AgentScripts/ProfileWaveFastSeek.cs --entry ProfileWaveFastSeek.Run --args '[1,"profile",20]' --timeout_ms 240000 --timeout 250 --format json
```

Варианты: `off`, `profile`, `noSnapshots`, `minimal`, `visuals`, `alloc`, `memory`. `alloc` включает тяжёлый native Profiler только для байтов, его wall time не сравнивают с baseline. `Gui` — отдельный entry с аргументом `[1]`. Runner возвращает исходный Seed и выключает свои флаги/Profiler в finally; исходные ассеты не сохраняет. Использовать PowerShell 7: PowerShell 5 иначе передаёт кавычки JSON в CLI и может дать INVALID_COMMAND_ARGS. Инструмент установки меток одноразовый, повторно не применяется и теперь защищает исходный source-baseline от перезаписи.
''')
(art/'WavePreviewFastSeekProfiling.md').write_text('\n'.join(md),encoding='utf-8')
with (art/'fast-seek-profile-categories.csv').open('w',newline='',encoding='utf-8-sig') as fp:
    writer=csv.writer(fp);writer.writerow(['wave','category','calls','self_ms','inclusive_ms','self_ms_per_step','self_percent_player_update'])
    for i in range(4):
        for k,s in areas[i].items(): writer.writerow([names[i],k,s['calls'],s['selfMs'],s['inclusiveMs'],s['selfMs']/1800,s['selfMs']/areas[i]['PlayerUpdate']['inclusiveMs']*100])
compat=[]
for i in range(4):
    runs=base[i]+primary[i]+[control[i]]
    keys=['steps','finalProjectiles','omitted','snapshots','snapshotEstimateBytes']
    equal=all(all(r[k]==runs[0][k] for k in keys) for r in runs)
    assert equal and all(r['worldCleanup'] and r['prefabUnchanged'] for r in runs)
    compat.append(dict(wave=names[i],counters_equal=equal,values={k:runs[0][k] for k in keys}))
(art/'fast-seek-profile-compatibility.json').write_text(json.dumps(compat,indent=2),encoding='utf-8')
print('Report, category CSV and compatibility checks written.')
