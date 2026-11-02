"""Curated recorded flamethrower candidates. No synthesis, pitch variants or changes to old entries."""
import hashlib
import json
import math
from pathlib import Path
import re
import shutil
import subprocess

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DIST = ROOT / 'dist'
WORK = ROOT / '.local/research/flamethrower'
DATE = '2026-10-29'
RATE = 48000
TARGET = -26.0

# One source recording per candidate; different excerpts are not separate candidates.
# source, stable slug, role, title, start seconds, source duration, description
CHOICES = [
    ('gdc-3666b134061d', 'digital-rain-gas', 'loop', 'Digital Rain Lab · Deep Gas Burner', 2, 20,
     'Профессиональная стереозапись газовой горелки из Driving Forces. Низкий непрерывный гул и поток; основа для тяжёлого корабельного огнемёта.'),
    ('fs-490166', 'samster-heavy-flame', 'loop', 'SamsterBirdies · Heavy Flamethrower', 1.6, 9.9,
     'Готовый авторский дизайн: запись горелки и ванны, с добавленным автором тоном около 120 Hz. Плотное ровное тело; басовый тон стоит проверить на утомляемость.'),
    ('fs-395039', 'aslak-long-burn', 'loop', 'AslakHostaker · Long Flame Stream', 2.7, 9.5,
     'Непрерывное ядро одного длинного огнемётного выброса. Второй дубль и short/medium-версии не считаются отдельными вариантами.'),
    ('oga-fire-loop', 'qubodup-fire-loop', 'loop', 'qubodup · Seamless Fire', 0, 5.019,
     'Готовый игровой Fire Loop с равномерным горением. Короткая петля: на длительном огне оцените заметность её повторения. Нужна атрибуция CC BY 3.0.'),
    ('fs-257055', 'jagadamba-petrol', 'loop', 'Jagadamba · Petrol Flame Jet', 9, 11.5,
     'Реальная бензиновая горелка с низким гулом и пульсацией потока. Более механический оттенок; нужна атрибуция CC BY 4.0.'),
    ('fs-244230', 'syntax-weed-torch', 'loop', 'SyntaxSeed · Large Weed Torch', 1, 12,
     'Большая газовая горелка для сорняков: устойчивое горение. Автор записал её на Android снаружи; проверьте фон в тихом игровом миксе.'),
    ('fs-197800', 'rombart-chalumeau', 'loop', 'rombart · Propane Roar', 23.5, 5.5,
     'Горелка, записанная Zoom H4n: выбран участок мощного ровного горения, без переходов и регулировки клапана.'),
    ('fs-24221', 'starrock-flutter', 'loop', 'StarRock · Fluttering Gas Flame', 3, 10,
     'Газовое горение с мягкой флуктуацией и шипением. Выделена середина между включением и выключением; нужна атрибуция CC BY 3.0.'),
    ('fs-396890', 'pugaeme-aerosol', 'loop', 'pugaeme · Aerosol Flame Stream', 4.7, 5.6,
     'Готовая запись потока пламени: зажигалка и аэрозоль, Rode NT1-A. Более зернистая фактура; без первой короткой вспышки. Нужна атрибуция CC BY 3.0.'),
    ('fs-453408', 'kyles-detailed-jet', 'loop', 'kyles · Detailed Blowtorch', 9, 12,
     'Продолжительный газовый поток с деталями факела. Более яркое шипение, чем у басовых кандидатов: полезен для сравнения читаемости на телефоне.'),
    ('fs-494085', 'sidequesting-designed', 'loop', 'sidequesting · Designed Flame Roar', 6.5, 8,
     'Авторская комбинация public-domain шумов для огнемёта. Один участок из трёх запусков; остальные запуски не добавлены как новые кандидаты.'),
    ('fs-513582', 'rar-small-torch', 'loop', 'rar2k · Compact Torch Hiss', 1.1, 2.6,
     'Малая горелка, один из двух исходных дублей. Самая короткая и сравнительно яркая петля; не основной кандидат для мягкого тяжёлого огнемёта. CC BY 3.0.'),
    ('fs-569332', 'pegasus-workshop', 'loop', 'PegasusCZ · Workshop Gas Burner', 6, 9.5,
     'Небольшая рабочая газовая горелка: более спокойный технический поток. Начальная механика зажигания вырезана; оцените фоновый шум.'),
    ('fs-572395', 'guialgarve-asphalt', 'loop', 'Guialgarve · Industrial Flame', 2.5, 9,
     'Реальный огнемёт для плавления асфальта. Низкая индустриальная фактура с естественным изменением интенсивности.'),
    ('fs-593755', 'mighael-gas-torch', 'loop', 'Mighael · Steady Gas Torch', .6, 4,
     'Устойчивое начало газового факела до его регулировки. Короткая петля с более лёгким корпусом; CC BY 3.0.'),
    ('fs-867029', 'raphavpires-layered', 'loop', 'raphavpires · Layered Fire Loop', .2, 10.8,
     'Готовый многослойный фоли-дизайн из шипения, дыхания и пластиковых пакетов, Audio-Technica AT2040. Более шуршащая фантастическая альтернатива реальному газу.'),
    ('gdc-b522db294908', 'soundmorph-firestorm', 'start', 'SoundMorph · Firestorm Ignition', .2, 2.95,
     'Готовый Firestorm Flamer из Steampunk Weapons. Выброс и затухание, подходит для запуска тяжёлой способности; не выдаётся за непрерывную петлю.'),
    ('gdc-bc738403a219', 'soundrangers-torch', 'start', 'Soundrangers · Torch Whoosh', 0, 1.1,
     'Профессиональный короткий огненный whoosh. Компактный запуск перед основным непрерывным потоком.'),
    ('fs-457209', 'redvulpus-flame-bottle', 'start', 'RedVulpus · Hollow Flame Jet', .65, 1.45,
     'Записанный Zoom H4n выброс пламени из стеклянного сосуда. Полый низкий резонанс; годится для воспламенения, не для основного Loop.'),
    ('fs-459884', 'lilmati-flame-start', 'start', 'LilMati · Incendiary Start', 0, 2.2,
     'Готовый игровой эффект огнемёта с атакой и коротким спадом. Рассматривается только как запуск.'),
    ('fs-447941', 'breviceps-cooldown', 'end', 'Breviceps · Flamethrower Cooldown', .9, .99,
     'Хвост готового Blast + Flamethrower cooldown: начало взрыва исключено, оставлен короткий спад. Единственный отдельный кандидат End, без искусственного увеличения количества.'),
]

SHORTLIST = {
    'digital-rain-gas': (1, 'Профессиональная длительная запись, выраженный низ и небольшая доля верхнего диапазона. Первым проверить в боевом миксе.'),
    'samster-heavy-flame': (2, 'Готовый плотный огнемётный дизайн с ровным потоком. Авторский тон 120 Hz может утомлять при большом числе одновременных источников.'),
    'aslak-long-burn': (3, 'Длинный прямой огнемётный поток без стартовых щелчков. Хорошая основа для разделения Start/Loop/End.'),
    'qubodup-fire-loop': (4, 'Уже предназначен для игрового зацикливания, исходник скачан в lossless FLAC. CC BY 3.0 требует кредита.'),
    'jagadamba-petrol': (5, 'Низкий бензиновый факел с иной механической фактурой. Полезное сравнение с ровным газовым потоком; CC BY 4.0.'),
}


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def run(args, payload=None): return subprocess.run(args, input=payload, capture_output=True, check=True)


def measure(data, channels):
    response = run(['ffmpeg', '-v', 'info', '-f', 'f32le', '-ar', str(RATE), '-ac', str(channels), '-i', 'pipe:0',
                    '-af', 'loudnorm=I=-26:TP=-8:LRA=11:print_format=json', '-f', 'null', '-'], data.astype('<f4').tobytes())
    result = json.loads(re.search(r'\{\s*"input_i".*?\}', response.stderr.decode(), re.S).group())
    return dict(lufs=float(result['input_i']), truePeakDb=float(result['input_tp']), loudnessRange=float(result['input_lra']))


def main():
    import sys
    sys.path.insert(0, str(ROOT))
    from audio_identity import ImportGate
    gate = ImportGate(ROOT)
    catalog_path = DIST / 'catalog.js'
    catalog = json.loads(catalog_path.read_text(encoding='utf8').split('=', 1)[1].strip().rstrip(';'))
    before = {sound['id']: sound for sound in catalog['sounds']}
    selections = ROOT / 'data/selections.json'
    selection_hash = sha(selections) if selections.exists() else None
    records = []
    for key, slug, role, title, start, length, description in CHOICES:
        info = json.loads((WORK / (key + '.json')).read_text(encoding='utf8'))
        source_path = WORK / info['local']
        assert sha(source_path) == info['sha256']
        sid = 'ft-' + slug
        if not gate.allow(sid, source_path, info):
            continue
        object_id = 'flamethrower-' + role
        folder = DIST / 'audio/flamethrower' / object_id / sid
        licenses = DIST / 'licenses' / sid
        (folder / 'originals').mkdir(parents=True, exist_ok=True)
        (folder / 'processed').mkdir(exist_ok=True); licenses.mkdir(parents=True, exist_ok=True)
        original = folder / 'originals' / ('source' + source_path.suffix)
        shutil.copyfile(source_path, original)
        source = dict(name=info.get('title', '').rsplit(' by ', 1)[0] + ' · Freesound', author=info.get('author'),
                      url=info.get('url'), downloadUrl=info.get('downloadUrl'))
        if key.startswith('gdc-'):
            library = info['path'].split('/')[-2]
            source.update(name=library + f' · GDC {info["year"]}', author=library.split(' - ')[0],
                          url='https://sonniss.com/gameaudiogdc/', downloadUrl=info['url'], archiveMember=info['path'],
                          archiveCRC32=f'{info["crc"]:08x}')
            original_name = info['path'].split('/')[-1]
            license_id, license_name, license_url, license_page = ('SONNISS-GDC-2.0', 'Sonniss GDC 2.0',
                 'https://sonniss.com/gdc-bundle-license/', 'sonniss-license.html')
            conditions = 'Коммерческие игры и обработка разрешены без атрибуции. Нельзя распространять записи как SFX-библиотеку/семплы, в том числе обработанные; запрещено обучение AI/ML. Только локальный отбор для проекта.'
            source_page = WORK / 'sonniss.html'
            original_note = 'Неизменённый профессиональный WAV из бесплатной выборки Sonniss; ZIP CRC32 проверена при извлечении.'
        else:
            own_license = info['licenses'][-1]
            assert '/zero/1.0/' in own_license or re.search(r'/licenses/by/(3\.0|4\.0)/', own_license), own_license
            version = '3.0' if '/3.0/' in own_license else '4.0'
            cc0 = '/zero/1.0/' in own_license
            license_id = 'CC0' if cc0 else 'CC-BY-' + version
            license_name = 'CC0 1.0' if cc0 else 'CC BY ' + version
            license_url = 'https://creativecommons.org/publicdomain/zero/1.0/' if cc0 else 'https://creativecommons.org/licenses/by/' + version + '/'
            license_page = 'cc0.html' if cc0 else 'cc-by' + version[0] + '.html'
            conditions = 'Атрибуция не обязательна (CC0 1.0).' if cc0 else 'Нужны автор, ссылка на источник, CC BY ' + version + ', ссылка на лицензию и указание обработки. Готовый кредит в attribution.txt; дополнительных ограничений не накладывать.'
            source_page = WORK / (key + '.html')
            if key.startswith('oga-'):
                source['name'] = title + ' · OpenGameArt'
                original_name = requests_unquote_name(info['downloadUrl'])
                original_note = 'Неизменённый lossless-исходник, открытая загрузка OpenGameArt.'
            else:
                original_name = info['downloadUrl'].split('/')[-1]
                source['downloadKind'] = 'public-hq-preview'
                original_note = 'Общедоступное HQ MP3-превью Freesound сохранено без изменений. Это не исходный WAV/FLAC автора; полный исходник требует входа пользователя. Декодирование в PCM не повышает качество MP3.'
        source['recordingTitle'] = info.get('title', original_name)
        probe = json.loads(run(['ffprobe', '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(original)]).stdout)
        channels = min(2, int(probe['streams'][0]['channels']))
        filters = f'atrim=start={start}:duration={length},asetpts=PTS-STARTPTS,highpass=f=35:p=2,lowpass=f=6500:p=2'
        raw = run(['ffmpeg', '-v', 'error', '-i', str(original), '-ac', str(channels), '-ar', str(RATE), '-af', filters, '-f', 'f32le', 'pipe:1']).stdout
        data = np.frombuffer(raw, dtype='<f4').reshape(-1, channels).copy()
        assert len(data) >= round((length - .025) * RATE), (sid, len(data), length)
        crossfade = 0.12 if role == 'loop' else 0
        if crossfade:
            count = round(crossfade * RATE)
            angle = np.linspace(0, math.pi / 2, count)[:, None]
            seam = data[-count:] * np.cos(angle) + data[:count] * np.sin(angle)
            data = np.concatenate((data[count:-count], seam)).astype(np.float32)
        else:
            fade_in = min(round(RATE * .006), len(data) // 3)
            fade_out = min(round(RATE * .06), len(data) // 3)
            data[:fade_in] *= np.linspace(0, 1, fade_in)[:, None]
            data[-fade_out:] *= np.linspace(1, 0, fade_out)[:, None]
        before_metrics = measure(data, channels)
        gain = min(TARGET - before_metrics['lufs'], -8 - before_metrics['truePeakDb'])
        assert math.isfinite(gain)
        data *= 10 ** (gain / 20)
        preview = folder / 'processed/audition.wav'
        run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(RATE), '-ac', str(channels), '-i', 'pipe:0', '-c:a', 'pcm_s24le', str(preview)], data.astype('<f4').tobytes())
        after = measure(data, channels)
        # A transient may hit the headroom ceiling first: retain dynamics rather than limiting it.
        assert TARGET - (1 if role == 'loop' else 3) <= after['lufs'] <= TARGET + .2, (sid, after)
        assert after['truePeakDb'] <= -7.95
        duration = len(data) / RATE
        preparation = dict(sourceStart=start, sourceDuration=length, duration=duration, filters=filters, crossfadeSeconds=crossfade,
                           crossfadeCurve='equal-power overlap; no fade-to-silence for loops', gainDb=round(gain, 4),
                           before=before_metrics, after=after, channels=channels, sampleRate=RATE, format='PCM24 WAV', synthesis=False,
                           seamStep=max(abs(data[-1] - data[0]).tolist()) if role == 'loop' else None)
        (folder / 'processed/preparation.json').write_text(json.dumps(preparation, indent=2), encoding='utf8')
        shutil.copyfile(source_page, licenses / 'source-page.html.txt')
        shutil.copyfile(WORK / license_page, licenses / 'license-page.html.txt')
        (licenses / 'license.txt').write_text(f'{license_name}\n{license_url}\nПроверено: {DATE}\n{conditions}\nПолный снимок условий: license-page.html.txt\n', encoding='utf8')
        credit = f'{source["recordingTitle"]} — {source["author"]}\n{source["url"]}\n{license_name}: {license_url}\nИзменения: выделен фрагмент, HP35/LP6500, PCM24/48kHz, постоянное усиление; ' + ('стык equal-power 120 ms.' if crossfade else 'плавные края 6/60 ms.')
        (licenses / 'attribution.txt').write_text(credit + '\n' + conditions, encoding='utf8')
        record = dict(id=sid, objectId=object_id, title=title, description=description,
                      tags=[role.capitalize(), 'Flamethrower', 'поток пламени' if role == 'loop' else 'воспламенение' if role == 'start' else 'затухание'],
                      file=preview.relative_to(DIST).as_posix(), duration=round(duration, 6), playbackKind=role,
                      originalFile=original.relative_to(DIST).as_posix(), originalFilename=original_name, downloadedAt=DATE,
                      sha256=sha(preview), originalSha256=sha(original), source=source,
                      license=dict(id=license_id, name=license_name, url=license_url, commercialUse=True, attribution=conditions,
                                   verifiedAt=DATE, evidenceFile=f'licenses/{sid}/license.txt', attributionFile=f'licenses/{sid}/attribution.txt'),
                      preparation=preparation,
                      processing=f'Фрагмент {start:.3f}–{start + length:.3f} с; HP35/LP6500, PCM24/48 kHz, {channels} канал(а). ' +
                        (f'Петля: equal-power перекрытие 120 ms, без тишины на стыке. ' if crossfade else 'Края 6/60 ms. ') +
                        f'Одно постоянное усиление {gain:+.2f} dB, {after["lufs"]:.2f} LUFS, true peak {after["truePeakDb"]:.2f} dBTP. Без компрессии, изменения высоты, новых слоёв или синтеза. ' + original_note)
        if source.get('downloadKind'):
            record['originalLabel'] = 'Источник · HQ MP3 ↓'
            record['description'] += ' Публичное HQ MP3; полный исходник — на Freesound после входа.'
        if slug in SHORTLIST:
            rank, reason = SHORTLIST[slug]; record['shortlist'] = dict(rank=rank, reason=reason)
        (licenses / 'source.txt').write_text(json.dumps(dict(record=record, originalNote=original_note), ensure_ascii=False, indent=2), encoding='utf8')
        records.append(record)
        print(sid, role, round(duration, 3), after, flush=True)
    assert len(records) <= 21 and len({r['originalSha256'] for r in records}) == len(records)
    assert not ({r['originalSha256'] for r in records} & {r.get('originalSha256', r['sha256']) for r in before.values() if not r['id'].startswith('ft-')})
    if not any(c['id'] == 'flamethrower' for c in catalog['categories']):
        catalog['categories'].append(dict(id='flamethrower', name='Flamethrower'))
    for role, name, description in [('loop', 'Loop · Непрерывное пламя', '16 разных записей. Для проверки стыка включите повтор в нижней панели. Пятёрка — предварительный отбор, не ваше избранное.'),
                                    ('start', 'Start · Воспламенение', '4 самостоятельных запуска. Каждый — отдельная исходная запись.'),
                                    ('end', 'End · Затухание', '1 отдельный хвост остановки. Не добирали количество дублями или синтезом.')]:
        if not any(o['id'] == 'flamethrower-' + role for o in catalog['objects']):
            catalog['objects'].append(dict(id='flamethrower-' + role, categoryId='flamethrower', name=name, description=description))
    new_by_id = {r['id']: r for r in records}
    catalog['sounds'] = [new_by_id.pop(r['id'], r) for r in catalog['sounds']] + list(new_by_id.values())
    assert all(next(r for r in catalog['sounds'] if r['id'] == sid) == value for sid, value in before.items() if not sid.startswith('ft-'))
    catalog['updatedAt'] = DATE
    import_result = gate.store.import_records(records, catalog['categories'], catalog['objects'])
    assert (sha(selections) if selections.exists() else None) == selection_hash
    (WORK / 'import-result.json').write_text(json.dumps(dict(total=21, loop=16, start=4, end=1, selectionsUnchanged=True, oldRecordsUnchanged=True), indent=2))
    print(json.dumps(import_result, ensure_ascii=False), flush=True)


def requests_unquote_name(url):
    from urllib.parse import unquote
    return unquote(url.split('/')[-1])


if __name__ == '__main__': main()
