"""Add ready-made game SFX; preserve all earlier catalog entries and player selections."""
import json
import math
import shutil
from collections import Counter

import numpy as np

from import_flamethrower import ROOT, DIST, DATE, RATE, TARGET, run, measure, sha

WORK = ROOT / '.local/research/flamethrower-arcade'
OBJECT = 'flamethrower-arcade'

# One source per candidate. A burst is not relabelled as a sustained loop.
# Source ID, stable slug, role, title, excerpt start/duration, description.
CHOICES = [
    ('gdc-da6a2984145a', 'deep-bass-arc', 'loop', 'Sound Spark · Deep Bass Energy Arc', 0, 10,
     'Готовая Seamless Loop из Electric Arcs and Energy. Басовая энергетическая дуга: кандидат для плотного плазменного потока; проверить читаемость на динамике телефона.'),
    ('gdc-faf181bc401b', 'burning-warble', 'loop', 'SoundMorph · Warbling Energy Burn', 1.5, 11,
     'Авторский warbley burning tone из TENSION. Сохранено устойчивое тело до длинного затухания: необычная низкая фактура энергетического горения.'),
    ('gdc-9f8f6d7c7605', 'lethal-force-field', 'loop', 'Digital Rain Lab · Lethal Force Field', .3, 7.1,
     'Готовая петля из Lethal Energies. Пульсирующее силовое поле как альтернатива реалистичному шипению огня; ритмичность стоит сравнить с темпом оружия.'),
    ('gdc-ef9d8de5b95e', 'cyberdeck-stream', 'loop', 'CB Sound Design · Cyberdeck Energy Stream', 0, 10,
     'Готовая игровая энергетическая текстура CYBERDECK. Ровное тело с внутренними изменениями, без записи настоящей горелки.'),
    ('gdc-03bfa4bd4d75', 'hybrid-plasma', 'loop', 'PMSFX · Hybrid Electromagnetic Flow', 0, 10.5,
     'Готовый Hybrid Electro Magnetic LOOP из Obscure Frequencies. Более зернистый энергетический поток для сравнения с мягкими вариантами.'),
    ('gdc-4ebf0d257c92', 'force-field', 'loop', 'SoundMorph · Energy Force Field', 0, 14,
     'Длительная авторская force field loop из ENERGY. Тяжёлый фантастический фон потока; не обычный газовый факел.'),
    ('gdc-3da868e0c96a', 'barrier-core', 'loop', 'SoundMorph · Barrier Core Stream', 7, 15,
     'Выделен idle-участок готового defense barrier activation idle & stop. Атака и остановка исключены; плотное ядро энергетического щита можно примерить к огнемёту.'),
    ('gdc-a4bde4f9a072', 'oscillating-burn', 'loop', 'SoundMorph · Low Oscillating Burn', 2, 8,
     'Авторская low oscillating distortion из TENSION. Низкая колеблющаяся фактура для стилизованного огненного оружия; проверить резонанс при долгом повторении.'),
    ('gdc-0c36f424e8b5', 'magic-electricity', 'loop', 'David Dumais · Magic Electricity', 0, 9.121,
     'Готовая ElectricityLoop из Magic Sound FX Pack 1. Более яркий магический энергетический слой; верх мягко ограничен в превью.'),
    ('gdc-7b62aff6b74c', 'arcane-air-stream', 'loop', 'David Dumais · Arcane Air Stream', .5, 11.5,
     'Дизайн воздуха из Magic Sound FX Pack 2, без конечного спада. Неровная фантастическая струя; более воздушная альтернатива электрическим потокам.'),
    ('gdc-39939d4dd4c7', 'alien-energy', 'loop', 'Bluezone · Alien Energy Texture', 0, 5.965,
     'Готовая alien synthetic texture из Alien Spacecraft. Изменяющееся энергетическое тело, не ровная горелка. Проверить заметность внутренней пульсации.'),
    ('gdc-3b9ffe051929', 'abstract-reactor', 'loop', 'InspectorJ · Abstract Reactor Flow', 5, 14,
     'Стабильный участок Abstract Spaceships, а не запись бытового двигателя. Более спокойный фантастический реакторный слой; может потребовать отдельного Start.'),
    ('gdc-ebd44071f73a', 'podracer-flame', 'loop', 'Fox Audio · Podracer Flame Drive', 3, 16,
     'Готовый игровой двигатель из Space Racer 2. Более механический sci-fi выброс пламени, один RPM-вариант без добора похожих дублей.'),
    ('gdc-3603b5ff9e1d', 'anime-beam', 'start', 'Doex Studio · Anime Beam Ignition', 0, 2.3,
     'Первый отдельный выброс из Laser Beam 004, 90s Anime SFX Pack. Выразительная атака фантастического луча; три исходных дубля не считаются тремя кандидатами. Это Start, не Loop.'),
    ('gdc-5cde8181992f', 'dragon-flare', 'start', 'Sound Spark · Dragon Fire Flare', 0, 2.9,
     'Готовая Fire Spell Dragon Trap: стилизованное огненное заклинание с атакой и спадом. Короткий запуск энергетического огнемёта.'),
    ('gdc-da4bf0e21b94', 'dark-energy', 'start', 'Rogue Waves · Dark Energy Ignition', 0, 1.979,
     'Готовая Dark Energy Attack из Druid Magic. Короткий выразительный энергетический запуск с несколькими акцентами, без искусственного превращения в петлю.'),
    ('gdc-6561232ec80e', 'energy-breath', 'start', 'Chris Logsdon · Energy Breath Attack', 0, 4.5,
     'Готовая Breath Attack из Sci-Fi SFX Pack. Растянутый энергетический выдох и затухание, пригодный для начала потока или короткой активации.'),
    ('gdc-10bdfbb65c88', 'warble-powerup', 'start', 'Sound Spark · Warbling Powerup', .15, 2.2,
     'Готовый Electric Energy Ambient Warble Powerup. Нарастание энергетического ядра перед непрерывным пламенем; не выдаётся за стабильный Loop.'),
    ('gdc-5e3cf0339faa', 'electrostatic-ignition', 'start', 'Bluezone · Electrostatic Ignition', .3, 5.8,
     'Готовая synth texture из Electrostatic Field. Выраженный подъём и длинный спад: альтернативный запуск фантастического потока.'),
    ('gdc-524e0f5145e6', 'portal-flame', 'start', 'Gamemaster Audio · Portal Flame Surge', .2, 6.5,
     'Готовый dark portal wind из Magic and Spell Sounds. Магический воздушно-энергетический выброс; более длинный Start для заряжаемого оружия.'),
    ('gdc-c585e1a39e19', 'energy-shutdown', 'end', 'Chris Logsdon · Energy Shutdown', 0, 2.049,
     'Готовый Energy Hum Down из Sci-Fi SFX Pack. Короткое затухание энергии для прекращения подачи пламени.'),
]

SHORTLIST = {
    'deep-bass-arc': (1, 'Готовая басовая seamless-петля энергетической дуги. Первая проба для мощного плазменного огнемёта; сравнить на динамике телефона.'),
    'burning-warble': (2, 'Самостоятельный игровой burning-дизайн с низкой колеблющейся фактурой. Проверить комфорт резонансов в длительном огне.'),
    'lethal-force-field': (3, 'Готовый выразительный силовой Loop с мягким спектром. Проверить, не конфликтует ли его пульсация с музыкой.'),
    'cyberdeck-stream': (4, 'Ровное энергетическое тело с внутренним движением. Потенциально меньше утомляет, чем короткие повторяющиеся вспышки.'),
    'hybrid-plasma': (5, 'Более зернистый и читаемый электромагнитный Loop. Для сравнения характера с мягкой басовой пятёркой.'),
}


def metrics(data, channels):
    # Loudness of a comparable >=4-second signal, also valid for short one-shots.
    repeated = np.tile(data, (max(1, math.ceil(4 * RATE / len(data))), 1))
    return measure(repeated, channels)


def main():
    import sys
    sys.path.insert(0, str(ROOT))
    from audio_identity import ImportGate
    gate = ImportGate(ROOT)
    path = DIST / 'catalog.js'
    catalog = json.loads(path.read_text(encoding='utf8').split('=', 1)[1].strip().rstrip(';'))
    old = json.loads(path.read_text(encoding='utf8').split('=', 1)[1].strip().rstrip(';'))
    selections = ROOT / 'data/selections.json'
    selection_hash = sha(selections) if selections.exists() else None
    records = []
    for key, slug, role, title, start, length, description in CHOICES:
        info = json.loads((WORK / (key + '.json')).read_text(encoding='utf8'))
        source_path = WORK / info['local']
        assert sha(source_path) == info['sha256']
        sid = 'fta-' + slug
        if not gate.allow(sid, source_path, info):
            continue
        folder = DIST / 'audio/flamethrower' / OBJECT / sid
        evidence = DIST / 'licenses' / sid
        for directory in (folder / 'originals', folder / 'processed', evidence):
            directory.mkdir(parents=True, exist_ok=True)
        original = folder / 'originals/source.wav'
        shutil.copyfile(source_path, original)
        library = info['path'].split('/')[-2]
        author = library.replace(' – ', ' - ').split(' - ')[0]
        source = dict(name=library + f' · GDC {info["year"]}', author=author,
                      url='https://sonniss.com/gameaudiogdc/', downloadUrl=info['url'],
                      archiveMember=info['path'], archiveCRC32=f'{info["crc"]:08x}',
                      recordingTitle=info['path'].split('/')[-1])
        conditions = ('Коммерческие игры и обработка разрешены без атрибуции. Нельзя распространять записи как '
                      'SFX-библиотеку или семплы, включая обработанные версии; запрещено обучение AI/ML. '
                      'Локальный каталог только для отбора внутри проекта.')
        license_url = 'https://sonniss.com/gdc-bundle-license/'
        probe = json.loads(run(['ffprobe', '-v', 'error', '-show_streams', '-of', 'json', str(original)]).stdout)
        channels = min(2, int(probe['streams'][0]['channels']))
        filters = f'atrim=start={start}:duration={length},asetpts=PTS-STARTPTS,highpass=f=55:p=2,lowpass=f=6500:p=2'
        raw = run(['ffmpeg', '-v', 'error', '-i', str(original), '-ac', str(channels), '-ar', str(RATE),
                   '-af', filters, '-f', 'f32le', 'pipe:1']).stdout
        data = np.frombuffer(raw, dtype='<f4').reshape(-1, channels).copy()
        assert len(data) >= round((length - .025) * RATE), (sid, len(data), length)
        crossfade = .12 if role == 'loop' else 0
        if crossfade:
            count = round(crossfade * RATE)
            angle = np.linspace(0, math.pi / 2, count)[:, None]
            data = np.concatenate((data[count:-count], data[-count:] * np.cos(angle) + data[:count] * np.sin(angle))).astype(np.float32)
        else:
            fade_in, fade_out = round(.006 * RATE), round(.06 * RATE)
            data[:fade_in] *= np.linspace(0, 1, fade_in)[:, None]
            data[-fade_out:] *= np.linspace(1, 0, fade_out)[:, None]
        before = metrics(data, channels)
        gain = min(TARGET - before['lufs'], -8 - before['truePeakDb'])
        assert math.isfinite(gain)
        data *= 10 ** (gain / 20)
        after = metrics(data, channels)
        assert after['truePeakDb'] <= -7.95
        preview = folder / 'processed/audition.wav'
        run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(RATE), '-ac', str(channels),
             '-i', 'pipe:0', '-c:a', 'pcm_s24le', str(preview)], data.astype('<f4').tobytes())
        preparation = dict(sourceStart=start, sourceDuration=length, duration=len(data) / RATE,
                           filters=filters, crossfadeSeconds=crossfade, gainDb=round(gain, 4),
                           loudnessMethod='integrated LUFS on repeated >=4s excerpt; constant gain, no limiter',
                           before=before, after=after, channels=channels, sampleRate=RATE, format='PCM24 WAV',
                           synthesis=False, seamStep=float(np.max(np.abs(data[-1] - data[0]))) if crossfade else None)
        (folder / 'processed/preparation.json').write_text(json.dumps(preparation, indent=2), encoding='utf8')
        for filename, source_file in [('source-page.html.txt', 'sonniss.html'), ('license-page.html.txt', 'sonniss-license.html')]:
            shutil.copyfile(WORK / source_file, evidence / filename)
        (evidence / 'license.txt').write_text(f'Sonniss GDC 2.0\n{license_url}\nПроверено: {DATE}\n{conditions}\n', encoding='utf8')
        processing = (f'Фрагмент {start:.3f}–{start + length:.3f} с; HP55/LP6500, PCM24/48 kHz. ' +
                      ('Петля: equal-power перекрытие 120 ms без тишины на стыке. ' if crossfade else 'Края 6/60 ms. ') +
                      f'Постоянное усиление {gain:+.2f} dB: {after["lufs"]:.2f} LUFS на повторённом ≥4 с фрагменте, '
                      f'{after["truePeakDb"]:.2f} dBTP. Без новых слоёв, синтеза, изменения высоты и компрессии. '
                      'Оригинальный WAV сохранён без изменений, ZIP CRC32 проверена при скачивании.')
        credit = f'{source["recordingTitle"]} — {author}\n{source["url"]}\nSonniss GDC 2.0: {license_url}\n{conditions}\n{processing}\n'
        (evidence / 'attribution.txt').write_text(credit, encoding='utf8')
        record = dict(id=sid, objectId=OBJECT, title=title, description=description,
                      tags=[role.capitalize(), 'Stylized / Arcade', 'Sci-fi', 'Flamethrower'],
                      file=preview.relative_to(DIST).as_posix(), originalFile=original.relative_to(DIST).as_posix(),
                      originalFilename=source['recordingTitle'], downloadedAt=DATE, duration=round(len(data) / RATE, 6),
                      playbackKind=role, sha256=sha(preview), originalSha256=sha(original), source=source,
                      license=dict(id='SONNISS-GDC-2.0', name='Sonniss GDC 2.0', url=license_url, commercialUse=True,
                                   attribution=conditions, verifiedAt=DATE, evidenceFile=f'licenses/{sid}/license.txt',
                                   attributionFile=f'licenses/{sid}/attribution.txt'),
                      preparation=preparation, processing=processing)
        if slug in SHORTLIST:
            rank, reason = SHORTLIST[slug]
            record['shortlist'] = dict(rank=rank, reason=reason)
        (evidence / 'source.txt').write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf8')
        records.append(record)
        print(sid, role, round(record['duration'], 3), after, flush=True)
    assert len(records) == len({r['originalSha256'] for r in records}) <= 21
    assert not ({r['originalSha256'] for r in records} & {r.get('originalSha256', r['sha256']) for r in old['sounds']})
    if not any(o['id'] == OBJECT for o in catalog['objects']):
        catalog['objects'].append(dict(id=OBJECT, categoryId='flamethrower', name='Stylized / Arcade',
            description='21 готовый игровой SFX: 13 Loop · 7 Start · 1 End. Энергетические потоки, магия и sci-fi. Пятёрка — предварительный отбор; оцените комфорт на слух.'))
    incoming = {r['id']: r for r in records}
    catalog['sounds'] = [incoming.pop(r['id'], r) for r in catalog['sounds']] + list(incoming.values())
    assert all(next(r for r in catalog['sounds'] if r['id'] == s['id']) == s for s in old['sounds'])
    assert catalog['categories'] == old['categories']
    assert catalog['objects'][:len(old['objects'])] == old['objects']
    catalog['updatedAt'] = DATE
    import_result = gate.store.import_records(records, catalog['categories'], catalog['objects'])
    assert (sha(selections) if selections.exists() else None) == selection_hash
    result = dict(total=len(records), roles=dict(Counter(r['playbackKind'] for r in records)),
                  oldRecordsUnchanged=len(old['sounds']), selectionsUnchanged=True)
    (WORK / 'import-result.json').write_text(json.dumps(result, indent=2), encoding='utf8')
    print(json.dumps(import_result, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
