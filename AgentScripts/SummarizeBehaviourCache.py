from pathlib import Path
import json, statistics, gzip, re, hashlib, csv
root=Path(__file__).resolve().parents[1]
out=root/'AgentArtifacts'
def read(name): return json.loads((out/name).read_text(encoding='utf-8-sig'))
def med(data,key): return statistics.median(d[key] for d in data)
def area(data,name,kind='inclusiveMs'): return statistics.median(next(c[kind] for c in d['categories'] if c['name']==name) for d in data)
def normalized_state(path):
    value=gzip.decompress(path.read_bytes()).decode('utf-8-sig')
    return re.sub(r'\|(numericPreviewRevision|attackConfigurationRevision|presetConfigurationRevision)=-?\d+',r'|\1=revision',value)
def table(head,rows): return '\n'.join(['| '+' | '.join(head)+' |','| '+' | '.join(['---']*len(head))+' |']+['| '+' | '.join(map(str,row))+' |' for row in rows])
def span(data,key): return f'{min(d[key] for d in data):.3f}–{max(d[key] for d in data):.3f}'
summary=[]; comparisons=[]
for i in range(4):
    before=[read(f'behaviour-cache-before-{i}-profile-{r}.json') for r in (1,2,3)]
    after=[read(f'behaviour-cache-after-{i}-profile-{r}.json') for r in (2,3,4)]
    native_before=read(f'fast-seek-profile-{i}-alloc-{2 if i==0 else 1}.json')['nativeAllocations']
    native_after=read(f'behaviour-cache-after-{i}-alloc-5.json')['nativeAllocations']
    baseline=normalized_state(out/f'behaviour-cache-before-{i}-profile-3-state.txt.gz')
    comparison={'wave':before[0]['wave'],'beforeHash':hashlib.sha256(baseline.encode()).hexdigest(),'comparisons':[]}
    for r in (2,3,4):
        state=normalized_state(out/f'behaviour-cache-after-{i}-profile-{r}-state.txt.gz')
        comparison['comparisons'].append({'repeat':r,'identical':state==baseline,'afterHash':hashlib.sha256(state.encode()).hexdigest()})
        assert state==baseline, f'Logical state changed: {i}/{r}'
    comparisons.append(comparison)
    for d in before+after:
        assert d['steps']==1800 and d['seed']==53 and d['prefabUnchanged'] and d['worldCleanup']
        assert d['restoreSteps']==0 and d['restoredFrame']==1740
    invariant_keys=('finalProjectiles','snapshots','omitted','snapshotEstimateBytes')
    for key in invariant_keys: assert len({d[key] for d in before+after})==1,(i,key)
    lookup_keys=('FormationLookupMiss','PhaseLookupMiss','PostLookupMiss','CompletionLookupMiss')
    row={'wave':before[0]['wave'],'beforeSeconds':med(before,'wallSeconds'),'afterSeconds':med(after,'wallSeconds'),
         'speedup':med(before,'wallSeconds')/med(after,'wallSeconds'),'beforeRange':span(before,'wallSeconds'),'afterRange':span(after,'wallSeconds'),
         'beforeStepMs':statistics.median(d['stepMs']['mean'] for d in before),'afterStepMs':statistics.median(d['stepMs']['mean'] for d in after),
         'beforeQueries':sum(before[0]['counters'][k] for k in lookup_keys),'afterQueries':sum(after[0]['counters'][k] for k in lookup_keys),
         'beforeGcStepBytes':native_before['stepBytes'],'afterGcStepBytes':native_after['stepBytes'],
         'beforeGcCollections':med(before,'gc0'),'afterGcCollections':med(after,'gc0'),
         'snapshotBytes':after[0]['snapshotEstimateBytes'],'snapshots':after[0]['snapshots'],'projectiles':after[0]['finalProjectiles'],'omitted':after[0]['omitted'],
         'beforeUnityPeak':med(before,'unityMemoryPeak'),'afterUnityPeak':med(after,'unityMemoryPeak'),
         'beforeWorkingSetPeak':med(before,'workingSetPeak'),'afterWorkingSetPeak':med(after,'workingSetPeak'),
         'beforeManagedLive':med(before,'managedMemoryLive'),'afterManagedLive':med(after,'managedMemoryLive'),
         'beforeUnityGrowth':statistics.median(d['unityMemoryPeak']-d['unityMemoryBefore'] for d in before),
         'afterUnityGrowth':statistics.median(d['unityMemoryPeak']-d['unityMemoryBefore'] for d in after),
         'beforeCpuMs':med(before,'threadCpuMilliseconds'),'afterCpuMs':med(after,'threadCpuMilliseconds'),
         'areas':{name:{'before':area(before,name),'after':area(after,name)} for name in ('Formation','Entrance','PostPipeline','PostCommand','WaveEvaluate','SnapshotManaged')}}
    assert row['afterQueries']==0
    assert native_before['capturedSteps']==native_after['capturedSteps']==1800
    assert native_before['missingFrames']==native_after['missingFrames']==0
    summary.append(row)
(out/'behaviour-cache-state-comparison.json').write_text(json.dumps(comparisons,indent=2),encoding='utf-8')
(out/'behaviour-cache-summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
flat=[{k:v for k,v in row.items() if k!='areas'} for row in summary]
with (out/'behaviour-cache-summary.csv').open('w',newline='',encoding='utf-8-sig') as file:
    writer=csv.DictWriter(file,fieldnames=list(flat[0]));writer.writeheader();writer.writerows(flat)
cache=read('behaviour-cache-checks.json'); prefab=read('behaviour-cache-prefab-checks.json')
regression=read('behaviour-cache-regression-after.json')['data']['result']['result']
snapshots=read('behaviour-cache-snapshot-checks.json')['data']['result']['result']
stress=read('behaviour-cache-snapshot-stress.json')['data']['result']['result']
lines=['# Исправление кеша Behaviour-компонентов волн','',
'Дата: 21.10.2026. Unity 6000.3.2f1, Windows, существующий Editor и Unity Pipeline. База исходников: commit 19361ef. Измерения относятся к Editor Fast Seek; численного замера реального боя/Android не проводилось.','',
'## Результат','',
'Общая конфигурация Runtime и Preview теперь запоминает найденный компонент и его отсутствие. В четырёх измеренных прогонах устранены 11–18 миллионов повторных запросов компонентов за 1800 шагов. Порядок источников конфигурации и полное логическое состояние волн сохранились.','',
'## Изменения','',
'- DirectedEnemySubWaveBehaviourConfiguration.cs: единый ResolveBehaviourComponent<T>, отдельные transient resolved-флаги для Formation, Phase Entry, Post, Completion и связанного Attack. TryGetComponent выполняется один раз после инвалидации. Настоящий null означает кешированное отсутствие; Unity fake-null требует повторного поиска. Кеш и флаги помечены NonSerialized. Явные сериализованные ссылки по-прежнему имеют первый приоритет.','- UsesLegacyConfigurationCompatibility сначала проверяет structuredConfigurationVersion. Современная версия не проверяет наличие четырёх компонентов ради заведомо отключённого legacy-режима. Getter конфигурации разрешает соответствующий компонент один раз. Legacy-поля и их значения не изменены.','- Existing Set/Clear и InvalidateBehaviourComponentCache расширены флагами; Set(null)/Clear сохраняют прежний fallback к компоненту на том же GameObject. Инвалидация доступна Editor-сборке. Awake и PrepareForActivation сбрасывают временный кеш при инициализации.','- Четыре компонента из DirectedWaveSubWaveBehaviourBlocks.cs получили ExecuteAlways и Reset/OnEnable/OnDestroy для уведомления владельца. Они меняют только transient-кеш, без сериализованных привязок. Reset покрывает добавление на неактивный объект в Editor.','- DirectedWaveBehaviourComponentCacheEditor.cs: события структурных изменений, относящихся к конфигурации, Undo/Redo, prefab instance update и импорт prefab. Transform-изменения Preview не сбрасывают кеш. Обход загруженных волн выполняется только по соответствующим Editor-событиям, а не каждый кадр.','- DirectedWaveAttackBehaviour.RegisterWithWave: сброс ранее закешированного отсутствия перед регистрацией вновь добавленного Attack-компонента. Настройки и расписание атак не менялись.','- Расчёты Formation/траекторий, Fixed Step, ECS, RNG, формат снимков, настройки волн и UI не изменялись. AgentScripts расширены только измерениями и проверками.','',
'## Условия и повторения','',
'Seed 53; Player Target (0, -3, 0); 0 → 60 секунд; 1800 фиксированных шагов; Snapshot Interval 2 секунды; Projectile Limit 2048. Auto-tick 16 мс. По три профильных прогона до и после; после исправления отдельный прогрев каждой волны. Для 1_1 дополнительно есть четвёртый исходный прогон, использованный для проверки отпечатка, но не для медианы ниже. Сборки и прочие CPU-нагрузки во время бенчмарков не запускались.','',
'Инструменты: существующие WavePreviewProfiler и ProfileWaveFastSeek; RunBehaviourCacheBenchmarks.ps1. Время ниже включает профильные маркеры, без deep profiling и без записи native GC.Alloc. Хеширование/сжатие состояния выполняется после измерения времени и памяти.','',
'## Fast Seek: медиана и разброс','',
table(['Волна','До, с [min–max]','После, с [min–max]','Ускорение','Шаг до → после, мс'],[[r['wave'],f"{r['beforeSeconds']:.3f} [{r['beforeRange']}]",f"{r['afterSeconds']:.3f} [{r['afterRange']}]",f"{r['speedup']:.2f}×",f"{r['beforeStepMs']:.3f} → {r['afterStepMs']:.3f}"] for r in summary]),'',
'Полная серия после исправления заново прошла в Editor после его перезапуска из-за OOM при первом domain reload. Поэтому это измеренное ускорение в указанных сессиях; условия фоновой памяти/прогрева процессов не абсолютно идентичны. Устранение поисков отдельно подтверждено счётчиками и native GC.Alloc.','',
'## Фактические запросы компонентов','',
table(['Волна','Formation до → после','Phase Entry до → после','Post до → после','Completion до → после','Всего до → после'],[[r['wave']]+[f"{read(f'behaviour-cache-before-{i}-profile-3.json')['counters'][k]:,} → 0" for k in ('FormationLookupMiss','PhaseLookupMiss','PostLookupMiss','CompletionLookupMiss')]+[f"{r['beforeQueries']:,} → {r['afterQueries']}"] for i,r in enumerate(summary)]),'',
'Эти счётчики охватывают четыре resolver-а внутри измеренных 1800 шагов, а не все GetComponent во всём Editor. Кеш уже прогрет инициализацией симуляции до включения записи: поэтому после исправления в измеренном интервале 0 запросов. При холодном кеше проверка 4000 чтений отсутствующих компонентов дала ровно 4 запроса, по одному на тип. После реальной инвалидации каждый тип может быть запрошен заново. Счётчик LookupMiss теперь увеличивается при выполнении фактического TryGetComponent. Native GetComponentNullErrorMessage из других участков Editor остаётся на уровне 0.007–0.65 мс за allocation-прогон; нулевое число всех GetComponent проекта не заявляется.','',
'## Formation / Phase Entry / Post','',
table(['Волна','Formation до → после, мс','Entrance до → после, мс','Post Pipeline до → после, мс','Post Command до → после, мс'],[[r['wave']]+[f"{r['areas'][a]['before']:.2f} → {r['areas'][a]['after']:.2f}" for a in ('Formation','Entrance','PostPipeline','PostCommand')] for r in summary]),'',
'Медианы inclusive-времени за 1800 шагов. Formation — существующий маркер CreateSimulationFormationPositions; Entrance — EvaluateSimulationEntrancePosition, включая проверку HasPhaseEntry и fallback к Formation. Post Pipeline включает вложенные вызовы Formation и другие команды. Эти колонки вложены друг в друга: их нельзя складывать. Формулы и результаты этих методов не менялись.','',
'## GC allocations и память','',
'Точные managed allocations взяты отдельным прогоном native Unity Profiler, GC.Alloc под FastSeek.FixedStep: 1800/1800 шагов, без потерянных кадров, deep profiling выключен. До — сохранённые allocation-прогоны исходной реализации fast-seek-profile-0-alloc-2 и 1/2/3-alloc-1; после — behaviour-cache-after-*-alloc-5. Значения GC.GetAllocatedBytesForCurrentThread в этом Unity/Mono равны 0 и для заключения о расходе памяти не используются.','',
table(['Волна','GC.Alloc до → после, MiB','Снижение','GC collections до → после','Снимки, MiB (до = после)'],[[r['wave'],f"{r['beforeGcStepBytes']/2**20:.2f} → {r['afterGcStepBytes']/2**20:.2f}",f"{100*(1-r['afterGcStepBytes']/r['beforeGcStepBytes']):.2f}%",f"{r['beforeGcCollections']:g} → {r['afterGcCollections']:g}",f"{r['snapshotBytes']/2**20:.2f}"] for r in summary]),'',
'GC collections — медиана счётчика GC.CollectionCount(0) в обычных профильных прогонах. Байты GC.Alloc — суммарно выделенная память, а не одновременно удерживаемая. Большая часть оставшихся выделений приходится на существующее сохранение managed-снимков.','',
table(['Волна','Unity peak, MiB до → после','Рост Unity tracked за прогон, MiB до → после','RSS peak, MiB до → после','Managed live, MiB до → после'],[[r['wave']]+[f"{r[a]/2**20:.1f} → {r[b]/2**20:.1f}" for a,b in [('beforeUnityPeak','afterUnityPeak'),('beforeUnityGrowth','afterUnityGrowth'),('beforeWorkingSetPeak','afterWorkingSetPeak'),('beforeManagedLive','afterManagedLive')]] for r in summary]),'',
'Абсолютные Unity/RSS/Mono показатели включают историю Editor, Profiler-буферы и загруженные диагностические сборки; серии разделены перезапуском Editor. Снижение/рост этих абсолютных значений нельзя приписывать только кешу. Snapshot estimate совпал побайтно, миры ECS после Dispose: 0 во всех прогонах. Уменьшение transient allocations подтверждено отдельной scoped-записью выше; постоянный объём памяти Preview существенно не уменьшался.','',
'## Совместимость и проверки','',
f"- VerifyBehaviourComponentCache: {cache['checks']} проверок. Отрицательный/положительный кеш, все четыре компонента одновременно, обновление Configuration, fake-null, Set(null), Add/Remove на активных/неактивных объектах, Undo/Redo, приоритеты legacy/separate/embedded, modern version short-circuit, связанный Attack resolver.",
f"- Prefab/Variant/Prefab Stage: {prefab['checks']} проверок; наследование и удаление override, обновление уже созданного instance с отрицательным кешем. Persistent round-trip проверялся на Formation, Prefab Stage add/remove — на Phase Entry; массовое сохранение всех prefab-комбинаций Post/Completion не выполнялось.",
'- Реальный domain reload вызван EditorUtility.RequestScriptReload: положительный/отрицательный кеш и добавление после reload прошли. Предварительный Pipeline recompile вернул up_to_date; проверка reload выполнена отдельно и действительно перезагрузила сборки.',
f"- VerifyWavePreviewFastSeek: {regression['checks']} проверок; полный managed/native state, 60 секунд, переназначение seek, отмена, транспорт, max speed, последовательности/координатор, дочерние паттерны и очистка.",
f"- Snapshot Player: {snapshots['checks']} проверка; обратная/повторная перемотка 20 → 5 → 20, sequence/children/laser/coordinator, Unity Random state. Snapshot Stress: {stress['checks']} проверки; лимиты снарядов и снимков, повторные восстановления/очистка.",
'- Полные состояния реальных четырёх волн на 60-й секунде: все три after-прогона точно совпали с before. Сравнивается полный текст состояния из существующего FullState, включая managed/native данные, позиции, RNG и события. Нормализованы только три Editor cache-revision токена numericPreviewRevision/attackConfigurationRevision/presetConfigurationRevision, которые меняются при SetSeed, восстановлении Inspector и reload и не определяют игровое состояние. Доказательство: behaviour-cache-state-comparison.json; сжатые исходные состояния сохранены рядом.',
'- Во всех прогонах восстановление снимка на 58 сек: frame 1740, без досимуляции (restoreSteps=0); одинаковые projectile count, omission count, snapshot count/estimated bytes.','',
table(['Волна','Снаряды в 60 с','Пропущенные по лимиту','Снимки','Snapshot estimate, bytes'],[[r['wave'],r['projectiles'],r['omitted'],r['snapshots'],r['snapshotBytes']] for r in summary]),'',
'- Git diff: существующие .prefab/.unity/.asset/.meta и ProjectSettings/Packages не изменены. В Assets изменены только четыре существующих .cs и добавлен один Editor .cs с созданным Unity .meta. Уникальные временные prefab-fixtures удалены через AssetDatabase; пользовательские ассеты не удалялись. Изначальные чужие локальные изменения сохранены.','- Unity C# recompilation: успешно. Обе обязательные dotnet build --no-restore --verbosity:quiet --nologo завершились с 0 ошибками; финальные инкрементальные сборки — 0 предупреждений. Первая полная Runtime-сборка показала 118 прежних предупреждений о полях. Из-за отсутствующих project.assets.json выполнен ранее разрешённый dotnet restore.','- Unity Test Runner list_tests: найдено 0 NUnit-тестов; вместо этого выполнены существующие Pipeline regression fixtures, перечисленные выше.','',
'## Сбои проверки и ограничения','',
'- Первый domain reload после длительной исходной серии завершился нативным Unity System out of memory в MonoManager::BeginReloadAssembly / BackupAndDeflateManagedReferences. Пользователь перезапустил Editor; дальнейшие компиляции и отдельный reload прошли. Точную причину OOM эта задача не устанавливает.','- Первое открытие/закрытие тестового Prefab Stage в одном Editor-кадре вызвало одну ошибку Unity PrefabStage.DelayedFraming. В fixture добавлено ожидание завершения открытия; повторная проверка прошла, новых Console errors после cursor 19 нет. Историческую запись не удаляли из пользовательской Console. Ошибок компиляции нет.','- В реальном бою на устройстве и в Android player профилирование не выполнялось. Исправленный resolver общий для Runtime и Preview; численные ускорения выше относятся только к Editor Fast Seek.','- Счётчик peakProjectiles читается runner-ом после Editor update, а не после каждого fixed step. При ускорении выросло число шагов в update: поэтому выборочный пик в 1_1 составил 710–715 вместо 720, в одном after-прогоне 2_3 — 1153 вместо 1155. Это метрика с разной частотой выборки, а не доказательство изменения генерации. Конечное полное состояние, finalProjectiles и omission count совпали.','- Произвольный AddComponent на уже инициализированном неактивном объекте в Runtime обнаруживается при его активации/PrepareForActivation; код, который читает конфигурацию такого объекта до активации, должен пользоваться существующими Set/Clear либо явно вызвать InvalidateBehaviourComponentCache после структурного изменения. На активных объектах callback действует сразу. Опрос состава компонентов каждый кадр не добавлялся.','- Следующее заметное узкое место: SnapshotManaged (примерно 0.68–1.66 с за прогон), затем оставшиеся вычисления Formation и движение снарядов. Их оптимизация не выполнялась в рамках этой задачи.','',
'## Артефакты и повторение','',
'- behaviour-cache-summary.json / .csv — сводные измерения.','- behaviour-cache-before-*-profile-*.json и behaviour-cache-after-*-profile-*.json — исходные профильные результаты; after-*-alloc-5.json — native allocations.','- behaviour-cache-*-state.txt.gz и behaviour-cache-state-comparison.json — состояния и сравнение.','- behaviour-cache-checks.json, behaviour-cache-prefab-checks.json, behaviour-cache-reload-checks.json, behaviour-cache-regression-after.json, behaviour-cache-snapshot-checks.json, behaviour-cache-snapshot-stress.json — проверки.','- AgentScripts/VerifyBehaviourComponentCache.cs и VerifyBehaviourCachePrefabs.cs — lifecycle/приоритеты/prefab/reload.','- Для benchmark после domain reload сначала загрузить fixture: unity command run_script --file AgentScripts/VerifyWavePreviewFastSeek.cs --entry VerifyWavePreviewFastSeek.Run. Затем pwsh -NoProfile -File AgentScripts/RunBehaviourCacheBenchmarks.ps1 -Phase after.','- AgentScripts/SummarizeBehaviourCache.py повторно проверяет сохранённые состояния и формирует этот отчёт.','']
(out/'BehaviourComponentCacheFix.md').write_text('\n'.join(lines),encoding='utf-8')
print('Report written:',out/'BehaviourComponentCacheFix.md')
print(table(['Wave','Before','After','Speedup'],[[r['wave'],f"{r['beforeSeconds']:.3f}",f"{r['afterSeconds']:.3f}",f"{r['speedup']:.2f}x"] for r in summary]))
