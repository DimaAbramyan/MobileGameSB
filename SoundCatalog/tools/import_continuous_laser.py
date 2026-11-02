"""Import verified, independently selected continuous-beam loops through the existing admission gate."""
import json
import math
from pathlib import Path
import shutil
import sys
from urllib.parse import unquote, urlparse

import numpy as np
from import_flamethrower import ROOT, DIST, RATE, run, measure, sha

sys.path.insert(0, str(ROOT))
from audio_identity import ImportGate

WORK = ROOT / '.local/research/continuous-laser'
DATE = '2026-10-29'
CATEGORY = 'continuous-laser'
OBJECT = 'continuous-laser-loop'
SOURCE = 'https://opengameart.org/content/jc-sounds-sci-fi-pack-vol-1'
DOWNLOAD = 'https://opengameart.org/sites/default/files/scifi_pack_vol_1.7z'
CHOICES = [
    ('jc-soft-energy', 'Loop_SciFi Pack Vol 1_Energy BuildUp LOOP_var 1.wav', 'JC Sounds · Soft Energy Loop', 'Soft Energy Beam',
     'Готовая четырёхсекундная Energy BuildUp LOOP из игрового Sci-Fi Pack Vol 1. Самостоятельное устойчивое тело, без стартового нарастания и остановки. Комфорт долгого звучания нужно оценить на слух.'),
    ('jc-electric-hum', 'Loop_SciFi Pack Vol 1_Futuristic Machine Hum.wav', 'JC Sounds · Futuristic Electric Flow', 'Electric Beam',
     'Готовая 16-секундная игровая петля Futuristic Machine Hum. Ровный sci-fi гул как вариант непрерывного луча, не запись реальной сварки или двигателя. Подходит для сравнения более спокойного энергетического тела.'),
    ('jc-muffled-energy', 'Loop_SciFi Pack Vol 1_Ship Interior Muffled_v1.wav', 'JC Sounds · Muffled Energy Field', 'Ambient Energy',
     'Приглушённая игровая петля Ship Interior. Фоновая альтернатива для ненавязчивого луча; выраженность оружия ниже, чем у специального Beam Loop. Вторая близкая вариация и Near Engine не добавлены для увеличения количества.'),
]


def extra_choices():
    # Only one representative per related family; numbered takes are not separate candidates.
    choices = [
        ('gdc-0eb7128d5a93', 'gamemaster-room-hum', 'Gamemaster · Quiet Sci-Fi Hum', 'Ambient Energy', 0, 14.206,
         'Готовая игровая фоновая петля. Спокойная альтернатива телу луча; узнаваемость оружия слабее, чем у специального Beam. Низкочастотный характер нужно проверить на динамике телефона.'),
        ('gdc-10a5379f2666', 'pmsfx-hologram', 'PMSFX · Hologram Energy Loop', 'Soft Energy Beam', 0, 7.7,
         'Готовый Hologram Ambient Loop из Foundation Series SCI-FI. Более приглушённая энергетическая фактура; низкая середина и гул могут маскироваться другими эффектами.'),
        ('gdc-17dd044c7a48', 'pmsfx-airy-flow', 'PMSFX · Airy Futuristic Flow', 'Soft Energy Beam', 16.5, 8,
         'Устойчивый фрагмент дизайнерского футуристического воздушного гула. Источник из смежной категории sci-fi engine, не запись настоящего двигателя. Подходит для сравнения мягкого непрерывного потока.'),
        ('gdc-bfe78ddab950', 'cyberdeck-hologram', 'CB Sound Design · Hologram Stream', 'Arcade Laser', 2, 8,
         'Голографическая фактура Cyberdeck с внутренней модуляцией. Вырезано устойчивое тело до затухания. Это иной исходник, чем Holonoise Energy 15 в Flamethrower; наличие тонального оттенка оцените на повторе.'),
        ('gdc-f8f28a4c6111', 'exoplanets-soft-field', 'CB Sound Design · Exoplanet Field', 'Ambient Energy', 82, 8,
         'Ровный фрагмент Sci-Fi Drone 11 из Exoplanets. Фоновый энергетический вариант, не готовая оружейная атака. Есть тональная составляющая: не использовать, если она конфликтует с музыкой игры.'),
        ('gdc-695818aff891', 'dreamstates-granular-flow', 'Glitchedtones · Granular Energy Flow', 'Ambient Energy', 26, 8,
         'Гранулярная игровая атмосферная фактура без отдельных выстрелов. Умеренная модуляция уровня заметнее, чем у самых ровных вариантов; тихая альтернатива для сравнения, не главный кандидат.'),
    ]
    for key, slug, title, style, start, length, description in choices:
        m = json.loads((WORK / (key + '.json')).read_text(encoding='utf8'))
        member = m['path']
        parts = member.split('/')
        pack = parts[-2]
        author = pack.split(' - ')[0]
        info = dict(url='https://sonniss.com/gameaudiogdc/', downloadUrl=m['url'], archiveMember=member,
                    author=author, title=parts[-1])
        yield dict(slug=slug, filename=parts[-1], title=title, style=style, description=description,
                   path=WORK / m['local'], digest=m['sha256'], start=start, length=length, info=info,
                   author=author, sourceName=pack + ' · Sonniss GDC ' + str(m['year']), crc=m['crc'],
                   licenseId='Sonniss-GDC-2.0', licenseName='Sonniss GDC License 2.0',
                   licenseUrl='https://sonniss.com/gdc-bundle-license/',
                   conditions='Коммерческие игры разрешены, атрибуция не обязательна. Нельзя распространять отдельные SFX/библиотеку семплов или применять материал для обучения AI/ML. Каталог предназначен для локального выбора в проекте.',
                   evidence=['sonniss.html', 'sonniss-license.html'])
    for key, slug, title, author, style, license_id, license_url, description in [
        ('oga-seamless-energy-emission-loop', 'energy-emission', 'zeroisnotnull · Seamless Energy Emission', 'zeroisnotnull', 'Soft Energy Beam', 'CC0-1.0', 'https://creativecommons.org/publicdomain/zero/1.0/',
         'Готовая бесшовная петля энергетического щита из игры Laser Asteroids. Не одиночный выстрел: плавно модулированное тело для непрерывного луча. Есть выраженный низкий тон; оцените в миксе.'),
        ('oga-sci-fi-drone-loop', 'jdagenet-drone', 'jdagenet · Sci-Fi Energy Drone', 'jdagenet', 'Ambient Energy', 'CC-BY-3.0', 'https://creativecommons.org/licenses/by/3.0/',
         'Готовая обработанная авторская sci-fi петля для энергетического гула. В исходнике автор использовал синус и эффекты; это не сгенерированный нами звук. Тональный фоновый вариант, а не музыкальная мелодия; пригодность для долгой стрельбы оцените на слух.'),
    ]:
        m = json.loads((WORK / (key + '.json')).read_text(encoding='utf8'))
        assert any(('zero/1.0' in u if license_id == 'CC0-1.0' else '/by/3.0' in u) for u in m['licenses'])
        info = dict(url=m['url'], downloadUrl=m['downloadUrl'], author=author, title=m['title'])
        yield dict(slug=slug, filename=Path(unquote(urlparse(m['downloadUrl']).path)).name, title=title, style=style, description=description,
                   path=WORK / m['local'], digest=m['sha256'], start=0, length=None, info=info,
                   author=author, sourceName='OpenGameArt · ' + m['title'].split('|')[0].strip(),
                   licenseId=license_id, licenseName=license_id, licenseUrl=license_url,
                   conditions=('CC0: коммерческое использование, атрибуция не обязательна.' if license_id=='CC0-1.0'
                               else 'CC BY 3.0: указать jdagenet, источник, лицензию и обработку. Готовый кредит в attribution.txt.'),
                   evidence=['oga-' + key.removeprefix('oga-') + '.html'] + ([] if license_id=='CC0-1.0' else ['cc-by3.html']))
    meta = json.loads((WORK / 'kenney-metadata.json').read_text(encoding='utf8'))
    for filename, slug, title, style, description in [
        ('computerNoise_000.ogg', 'kenney-computer-flow', 'Kenney · Digital Energy Texture', 'Electric Beam',
         'Пятисекундная игровая цифровая фактура Kenney. Один представитель семейства Computer Noise; соседние номерные варианты не добавлены. Более электронный вариант, его повторяемость и резонансы требуют прослушивания.'),
        ('engineCircular_002.ogg', 'kenney-circular-flow', 'Kenney · Circular Sci-Fi Flow', 'Plasma Beam',
         'Один представитель дизайнерских Circular Engine из Sci-fi Sounds. Это игровая стилизация из смежной категории, не настоящий мотор; пульсацию стоит сравнить с более ровными петлями. Другие варианты семейства исключены.'),
    ]:
        m=next(r for r in meta if Path(r['member']).name == filename)
        info=dict(url='https://kenney.nl/assets/sci-fi-sounds',
                  downloadUrl='https://kenney.nl/media/pages/assets/sci-fi-sounds/6b296f9ecf-1677589334/kenney_sci-fi-sounds.zip',
                  archiveMember=m['member'], author='Kenney', title=filename)
        yield dict(slug=slug, filename=filename, title=title, style=style, description=description,
                   path=WORK / m['file'], digest=m['sha256'], start=0, length=None, info=info,
                   author='Kenney', sourceName='Kenney · Sci-fi Sounds 1.0', licenseId='CC0-1.0', licenseName='CC0 1.0',
                   licenseUrl='https://creativecommons.org/publicdomain/zero/1.0/',
                   conditions='CC0: коммерческое использование разрешено, атрибуция не обязательна. Использован ранее скачанный неизменённый оригинал пакета; лицензия находится внутри ZIP.',
                   evidence=[])


def main():
    gate = ImportGate(ROOT)
    metadata = json.loads((WORK / 'oga-jc-audio-metadata.json').read_text(encoding='utf8'))
    records, results = [], []
    candidates=[]
    for rank, (slug, filename, title, style, description) in enumerate(CHOICES, 1):
        item=next(m for m in metadata if Path(m['file']).name == filename)
        info=dict(url=SOURCE, downloadUrl=DOWNLOAD, archiveMember=item['member'].replace('\\','/'), author='JC Sounds',title=filename)
        candidates.append(dict(slug=slug,filename=filename,title=title,style=style,description=description,
            path=WORK/item['file'],digest=item['sha256'],start=0,length=None,info=info,author='JC Sounds',
            sourceName='JC Sounds · Sci-Fi Pack Vol 1 · OpenGameArt',crc=item['crc32'],licenseId='CC-BY-4.0',
            licenseName='CC BY 4.0',licenseUrl='https://creativecommons.org/licenses/by/4.0/',
            conditions='CC BY 4.0: указать JC Sounds, источник, лицензию и обработку. Готовый кредит в attribution.txt.',
            evidence=['oga-jc.html','cc-by4.html']))
    candidates.extend(extra_choices())
    for choice in candidates:
        slug, filename, title, style, description = (choice[k] for k in ('slug','filename','title','style','description'))
        source_path, info = choice['path'], choice['info']
        sid = 'cl-' + slug
        assert sha(source_path) == choice['digest']
        if not gate.allow(sid, source_path, info):
            continue
        details=json.loads(run(['ffprobe','-v','error','-show_format','-show_streams','-of','json',str(source_path)]).stdout)
        item=dict(channels=int(details['streams'][0]['channels']),duration=float(details['format']['duration']))
        print('Preparing', sid, flush=True)
        folder = DIST / 'audio' / CATEGORY / OBJECT / sid
        evidence = DIST / 'licenses' / sid
        for directory in (folder / 'originals', folder / 'processed', evidence):
            directory.mkdir(parents=True, exist_ok=True)
        original = folder / ('originals/source' + source_path.suffix)
        shutil.copyfile(source_path, original)
        channels = min(2, item['channels'])
        filters = 'highpass=f=55:p=2,lowpass=f=6500:p=2'
        segment=['-ss',str(choice['start'])]
        if choice['length'] is not None: segment += ['-t',str(choice['length'])]
        raw = run(['ffmpeg', '-v', 'error', '-i', str(original), *segment, '-ac', str(channels), '-ar', str(RATE),
                   '-af', filters, '-f', 'f32le', 'pipe:1']).stdout
        data = np.frombuffer(raw, dtype='<f4').reshape(-1, channels).copy()
        count = round(.12 * RATE)
        angle = np.linspace(0, math.pi / 2, count)[:, None]
        data = np.concatenate((data[count:-count], data[-count:] * np.cos(angle) + data[:count] * np.sin(angle))).astype(np.float32)
        # Integrated loudness over the same 60-second loop train for every candidate.
        repeats = math.ceil(RATE * 60 / len(data))
        train = np.tile(data, (repeats, 1))[:RATE * 60]
        before = measure(train, channels)
        gain = min(-28 - before['lufs'], -10 - before['truePeakDb'])
        data *= 10 ** (gain / 20)
        after = measure(train * 10 ** (gain / 20), channels)
        assert math.isfinite(gain) and after['truePeakDb'] <= -9.9
        preview = folder / 'processed/audition.wav'
        run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(RATE), '-ac', str(channels),
             '-i', 'pipe:0', '-c:a', 'pcm_s24le', str(preview)], data.astype('<f4').tobytes())
        # A local multi-cycle artifact is for seam verification, not an extra candidate.
        demo = folder / 'processed/loop-check-60s.wav'
        run(['ffmpeg', '-v', 'error', '-y', '-stream_loop', '-1', '-i', str(preview), '-t', '60',
             '-c:a', 'pcm_s24le', str(demo)])
        preparation = dict(sourceStart=choice['start'], sourceDuration=item['duration'], selectedDuration=len(data)/RATE+.12, duration=len(data)/RATE, filters=filters,
                           crossfadeSeconds=.12, crossfadeCurve='equal-power overlap; no silence at seam',
                           gainDb=round(gain, 4), before=before, after=after,
                           reference='60-second repeated loop; constant gain; no limiter',
                           seamStep=float(np.max(np.abs(data[-1]-data[0]))), synthesis=False,
                           sampleRate=RATE, format='PCM24 WAV', channels=channels,
                           loopCheck=dict(file=demo.relative_to(DIST).as_posix(), sha256=sha(demo), duration=60))
        (folder / 'processed/preparation.json').write_text(json.dumps(preparation, indent=2), encoding='utf8')
        conditions=choice['conditions']
        processing=(f"Оригинал {source_path.suffix.upper().lstrip('.')} сохранён без изменений. "
                    f"Фрагмент с {choice['start']:g} s, {len(data)/RATE:.3f} s после перекрытия. "
                    f"Превью PCM24/48 kHz: HP55/LP6500; equal-power кроссфейд 120 ms; "
                    f"постоянное усиление {gain:+.2f} dB. {after['lufs']:.2f} LUFS на 60-секундном повторе; "
                    f"{after['truePeakDb']:.2f} dBTP. Без изменения высоты/темпа, компрессии и новых слоёв. "
                    "Техническое декодирование и измерения не подтверждают комфорт на слух; используйте повтор 60 с.")
        source=dict(name=choice['sourceName'],author=choice['author'],**{k:v for k,v in info.items() if k!='author' and k!='title'},recordingTitle=filename)
        if choice.get('crc') is not None: source['archiveCRC32']=choice['crc']
        license=dict(id=choice['licenseId'],name=choice['licenseName'],url=choice['licenseUrl'],
                     commercialUse=True,attribution=conditions,verifiedAt=DATE,
                     evidenceFile=f'licenses/{sid}/license.txt',attributionFile=f'licenses/{sid}/attribution.txt')
        (evidence/'license.txt').write_text(choice['licenseName']+'\n'+choice['licenseUrl']+'\n'+info['url']+'\n'+conditions,encoding='utf8')
        for name in choice['evidence']: shutil.copyfile(WORK/name,evidence/name)
        if choice['author']=='Kenney':
            import zipfile
            with zipfile.ZipFile(ROOT/'.local/research/machine-gun/kenney.zip') as z:
                (evidence/'original-license.txt').write_bytes(z.read('License.txt'))
            shutil.copyfile(ROOT/'.local/research/machine-gun/kenney.html',evidence/'kenney-original-source.html')
        (evidence/'attribution.txt').write_text(f"{filename} — {choice['author']}\n{info['url']}\n{choice['licenseName']}: {choice['licenseUrl']}\n{conditions}\nИзменения: {processing}\n",encoding='utf8')
        record = dict(id=sid, objectId=OBJECT, title=title, description=description, tags=['Loop', style, 'Continuous Laser'],
                      playbackKind='loop', file=preview.relative_to(DIST).as_posix(), sha256=sha(preview),
                      originalFile=original.relative_to(DIST).as_posix(), originalSha256=sha(original),
                      originalFilename=filename, downloadedAt=DATE, duration=round(len(data)/RATE,6),
                      source=source, license=license, processing=processing, preparation=preparation)
        ranks={'jc-soft-energy':1,'jc-electric-hum':2,'pmsfx-airy-flow':3,'energy-emission':4,'pmsfx-hologram':5}
        if slug in ranks: record['shortlist']=dict(rank=ranks[slug],reason='Предварительный кандидат: устойчивое тело без отдельных атак. Тембр и комфорт долгого звучания требуют оценки в игровом миксе.')
        (evidence / 'source.txt').write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf8')
        records.append(record)
        results.append(dict(id=sid, style=style, duration=record['duration'], metrics=after))
    result = gate.store.import_records(records,
        [dict(id=CATEGORY, name='Continuous Laser')],
        [dict(id=OBJECT, categoryId=CATEGORY, name='Loop · Continuous Laser',
              description='Петли непрерывной энергии из готовых игровых библиотек. Стиль указан в тегах; в карточке есть оригинал и повтор 60 с. Пятёрка предварительных кандидатов отмечена отдельно от избранного.')])
    (WORK / 'import-extra-result.json').write_text(json.dumps(dict(admission=result, sounds=results), ensure_ascii=False, indent=2), encoding='utf8')
    print(json.dumps(result, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
