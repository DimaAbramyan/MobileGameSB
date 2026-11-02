"""Import selected, already downloaded recordings. No synthesis and no network calls.

Requires numpy, ffmpeg and the provenance cache in .local/research/machine-gun-v3.
Original bytes are copied unchanged. Re-running keeps stable IDs and user selections.
"""
from pathlib import Path
import hashlib
import html
import json
import math
import re
import shutil
import subprocess
import zlib
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DIST = ROOT / 'dist'
RESEARCH = ROOT / '.local/research/machine-gun-v3'
DATE = '2026-10-26'
RATE = 48000
OBJECT = 'machine-gun-v3'

# Each row is a different source effect, not a pitch variant or a different shot rate.
# key, stable slug, title, start (seconds in source), length, tags, context
CHOICES = [
 ('fbf4b22895af', 'soundmorph-assault', 'SoundMorph · Future Assault', .170, .28, ['гибридный','штурмовая винтовка'], 'Готовый дизайн Assault Rifle из Future Weapons 3; сокращён пространственный хвост.'),
 ('0146e57a53fa', 'shapeforms-rpu1', 'Shapeforms · RPU1', 0, .22, ['технологичный','автоматический'], 'Короткий RPU1 из Cyberpunk Arsenal. Наиболее прямо соответствует автоматическому оружию.'),
 ('3d5281bde2fd', 'rescopic-pulse', 'Rescopic · Pulse YR', .035, .23, ['энергетический','импульсный'], 'Готовый Pulse из Sci-Fi Energy Weapons; короткое ядро без длинного остаточного хвоста.'),
 ('f043dc73aab6', 'gamemaster-suppressed', 'Gamemaster · Suppressed Compact', 0, .16, ['баллистический','глушитель','компактный'], 'Смежная категория: приглушённый пистолет. Контрольный вариант с небольшой длительностью наложения.'),
 ('cc9d8a29518f', 'bluezone-scifi-gun', 'Bluezone · Sci-Fi Gun', 0, .24, ['гибридный','оружейный импульс'], 'Отдельный выстрел из Sci Fi Weapon. Низкая доля верхних частот в исходной записи.'),
 ('815f570d4f53', 'bluezone-annihilation', 'Bluezone · Annihilation Mech', 0, .28, ['мех','тяжёлый','sci-fi'], 'Оружейный выстрел меха из Annihilation. Альтернатива компактным винтовкам; оцените плотность при 15/с.'),
 ('894a42ca59d7', 'bluezone-invader', 'Bluezone · Invader', 0, .30, ['инопланетный','энергетический'], 'Выстрел из Invader — Alien Creature. Взято короткое ядро самостоятельного эффекта.'),
 ('72ce1d8580d2', 'bluezone-soundwave', 'Bluezone · Soundwave Robot', 0, .23, ['робот','гибридный'], 'Роботизированное оружие из Soundwave. Укорочено до следующего выраженного события в хвосте.'),
 ('7f445cee8470', 'dumais-heavy12', 'David Dumais · Heavy Weapon 12', .050, .26, ['тяжёлый','sci-fi'], 'Готовый тяжёлый одиночный выстрел; взята первая атака без последующих событий хвоста.'),
 ('534f7f9a3cca', 'shapeforms-scatter', 'Shapeforms · Cyberpunk Scatter', 0, .28, ['киберпанк','широкий импульс'], 'Первый отчётливый импульс Sci-Fi Shotgun Burst. Смежная категория для более широкого характера оружия.'),
 ('ff25bd7ab70b', 'dramatic-saiga', 'Dramatic Cat · Saiga Core', .115, .23, ['баллистический','плотный корпус'], 'Первый отдельно записанный Single Shot Core из Saiga-12; не длинная очередь.'),
 ('ffe3a965ab6c', 'dramatic-svd', 'Dramatic Cat · SVD Core', 0, .24, ['баллистический','винтовка'], 'Одиночное ядро SVD Dragunov. Альтернатива автоматам с более тяжёлой основой.'),
 ('5d4fa27f2227', 'bluezone-cannon', 'Bluezone · Sci-Fi Cannon', 0, .26, ['энергетический','тяжёлый'], 'Выстрел из Sci Fi Weapon Cannon; укорочен для частого повторения.'),
 ('f6ea43a76760', 'bluezone-steam-cannon', 'Bluezone · Steam Cannon', 0, .28, ['пневматический','тяжёлый'], 'Смежный Steampunk Cannon. Сохранён первый низкий импульс, длинная текстура удалена только из превью.'),
 ('20c38df7e226', 'bluezone-steam-gun', 'Bluezone · Steam Gun', 0, .19, ['пневматический','механический'], 'Компактное ядро Steampunk Gun до следующего заметного события в исходном хвосте.'),
 ('708d586f4564', 'gamemaster-blaster', 'Gamemaster · Blaster Boom', .005, .25, ['бластер','гибридный'], 'Оружейный blaster из Sci-Fi Sounds and Sci-Fi Weapons; сохранён начальный шумовой корпус.'),
 ('49950b9b139d', 'gamemaster-suppressed-rifle', 'Gamemaster · Suppressed Rifle', 0, .24, ['баллистический','глушитель','винтовка'], 'Приглушённая винтовка как альтернатива резкому открытому выстрелу. Хвост сокращён.'),
 ('7e7312aaa63b', 'pole-ak47-open', 'Pole Position · AK47 Open Air', .830, .22, ['баллистический','AK47','открытая площадка'], 'Первый изолированный одиночный выстрел AK47: микрофон 2 м сверху-сзади. Длинная запись сохранена отдельно.'),
 ('18f779237a44', 'pole-mp5-forest', 'Pole Position · MP5 Off Axis', .365, .20, ['баллистический','MP5','вне оси'], 'Первый изолированный выстрел MP5: 10 м позади, вне оси, лесная дорога. Более отдалённая перспектива.'),
 ('71d7a2372650', 'soundmorph-steampunk', 'SoundMorph · Steampunk Shot', .080, .28, ['механический','гибридный'], 'Отдельный Steampunk Shotgun 2. Сохранён первый компактный импульс; оттенок механики отличается от Future Assault.'),
 ('fs-331240', 'synthy95-plasma', 'synthy95 · Plasma Rifle', 0, .24, ['плазма','шумовой импульс','CC0'], 'Первый выстрел из авторской записи Plasma Rifle Gun Shots — Various; весь набор считается одним кандидатом.'),
 ('fs-397681', 'alipikminer-plasma', 'Alipikminer · Plasma Rifle', .980, .28, ['плазма','энергетический','CC0'], 'Отдельный импульс Plasma rifle после начальной паузы; без следующего события записи.'),
 ('fs-427396', 'erokia-plasma-p226', 'Erokia · Plasma P226', 0, .24, ['гибридный','плазма','CC BY'], 'Авторский дизайн на основе Sig Sauer P226, переработанный автором в plasma/lazer pistol. Не простой генераторный писк.'),
 ('fs-557514', 'newlocknew-rifle', 'newlocknew · Sci-Fi Rifle', .080, .30, ['sci-fi','обработанный выстрел','CC BY'], 'Короткое ядро готовой многослойной обработки Sci-fi rifle weapon shot_2-1; длинный хвост оставлен в исходнике.'),
]

SHORTLIST = {
 'soundmorph-assault': (1, 'Готовый дизайн штурмовой винтовки; короткое ядро и небольшой вклад верхнего диапазона. Проверить накопление корпуса при 15/с.'),
 'shapeforms-rpu1': (2, 'Исходный эффект автоматического оружия и компактная форма. Прямой кандидат для проверки длительного огня.'),
 'rescopic-pulse': (3, 'Энергетическая альтернатива из профильной библиотеки; короткий импульс без необходимости добавлять слои.'),
 'gamemaster-suppressed': (4, 'Самое короткое ядро в подборке; полезный ориентир для плотного микса и небольшого перекрытия.'),
 'bluezone-scifi-gun': (5, 'Самостоятельный sci-fi выстрел с небольшой долей верхних частот; проверить выразительность в общем миксе.'),
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(args, data=None):
    return subprocess.run(args, input=data, capture_output=True, check=True)


def plain_page(path):
    text = path.read_text(encoding='utf8')
    text = re.sub(r'<(script|style)\b[^>]*>.*?</\1>', '', text, flags=re.S | re.I)
    text = re.sub(r'</(?:p|h[1-6]|li|div|section)>|<br\s*/?>', '\n', text, flags=re.I)
    text = re.sub(r'<[^>]+>', '', text)
    return re.sub(r'\n[ \t]*\n+', '\n\n', html.unescape(text)).strip()


def measure(data):
    result = run(['ffmpeg', '-v', 'info', '-f', 'f32le', '-ar', str(RATE), '-ac', '1', '-i', 'pipe:0',
                  '-af', 'loudnorm=I=-26:TP=-8:LRA=11:print_format=json', '-f', 'null', '-'], data.astype('<f4').tobytes())
    block = re.search(r'\{\s*"input_i".*?\}', result.stderr.decode(), re.S)
    values = json.loads(block.group())
    return {'lufs': float(values['input_i']), 'truePeakDb': float(values['input_tp'])}


def train(data):
    result = np.zeros(4 * RATE, dtype=np.float32)
    for shot in range(40):
        offset = round(shot * RATE / 10)
        count = min(len(data), len(result) - offset)
        result[offset:offset + count] += data[:count]
    return result


def main():
    import sys
    sys.path.insert(0, str(ROOT))
    from audio_identity import ImportGate
    gate = ImportGate(ROOT)
    catalog_path = DIST / 'catalog.js'
    catalog = json.loads(catalog_path.read_text(encoding='utf8').split('=', 1)[1].strip().rstrip(';'))
    old = {sound['id']: sound for sound in catalog['sounds'] if sound['objectId'] != OBJECT}
    selections_path = ROOT / 'data/selections.json'
    selection_hash = sha(selections_path) if selections_path.exists() else None
    downloaded = {item['key']: item for item in json.loads((RESEARCH / 'downloaded-gdc.json').read_text())}
    records = []
    for key, slug, title, start, length, tags, description in CHOICES:
        sid = f'mg-v3-{slug}'
        candidate_info = (json.loads((RESEARCH / f'{key}.json').read_text()) if key.startswith('fs-') else downloaded[key])
        if not gate.allow(sid, RESEARCH / candidate_info['local'], candidate_info):
            continue
        folder = DIST / f'audio/weapons/{OBJECT}/{sid}'
        license_folder = DIST / 'licenses' / sid
        (folder / 'originals').mkdir(parents=True, exist_ok=True)
        (folder / 'processed').mkdir(exist_ok=True)
        license_folder.mkdir(parents=True, exist_ok=True)
        is_fs = key.startswith('fs-')
        if is_fs:
            info = json.loads((RESEARCH / f'{key}.json').read_text())
            source = RESEARCH / info['local']
            original_name = info['local']
            author = info['author']
            source_info = {'name': info['title'].removeprefix('Freesound - ').split(' by ')[0] + ' · Freesound',
                           'author': author, 'url': info['url'], 'downloadUrl': info['previews'][0],
                           'downloadKind': 'public-hq-preview'}
            cc0 = any('zero/1.0' in url for url in info['licenses'])
            license_id = 'CC0' if cc0 else 'CC-BY-4.0'
            license_url = 'https://creativecommons.org/publicdomain/zero/1.0/' if cc0 else 'https://creativecommons.org/licenses/by/4.0/'
            conditions = 'Атрибуция не требуется (CC0 1.0).' if cc0 else f'Требуется указать {author}, источник, CC BY 4.0 и изменения. Готовый текст — в файле атрибуции; дополнительные ограничения на лицензированный материал не накладывать.'
            evidence = RESEARCH / ('cc0.html' if cc0 else 'cc-by4.html')
            shutil.copyfile(RESEARCH / f'{key}.html', license_folder / 'source-page.html.txt')
            original_note = 'Скачано общедоступное HQ MP3-превью Freesound без перекодирования; это не оригинальный WAV автора. Для исходного WAV сайт требует вход в аккаунт.'
        else:
            info = downloaded[key]
            source = RESEARCH / info['local']
            assert sha(source) == info['sha256']
            assert zlib.crc32(source.read_bytes()) & 0xffffffff == info['crc']
            library = info['path'].split('/')[-2]
            original_name = info['path'].split('/')[-1]
            author = library.split(' - ')[0]
            source_info = {'name': f'{library} · GDC {info["year"]}', 'author': author,
                           'url': 'https://sonniss.com/gameaudiogdc/', 'downloadUrl': info['url'],
                           'archiveMember': info['path'], 'archiveCRC32': f'{info["crc"]:08x}'}
            license_id = 'SONNISS-GDC-2.0'
            license_url = 'https://sonniss.com/gdc-bundle-license/'
            conditions = 'Авторство не обязательно. Sonniss GDC 2.0: коммерческие игры и обработка разрешены; нельзя распространять звуки как библиотеку/набор семплов, в том числе обработанные; запрещено обучение AI/ML. Этот каталог предназначен для локального отбора в вашем проекте.'
            evidence = RESEARCH / 'sonniss-license.html'
            shutil.copyfile(RESEARCH / 'sonniss.html', license_folder / 'source-page.html.txt')
            original_note = f'Неизменённый WAV из бесплатной выборки Sonniss GDC {info["year"]}; ZIP CRC32 проверена при извлечении и повторно при импорте.'
        original = folder / 'originals' / ('source.mp3' if is_fs else 'source.wav')
        shutil.copyfile(source, original)
        preview = folder / 'processed/audition.wav'
        filters = f'highpass=f=70:p=2,lowpass=f=6000:p=2,afade=t=in:d=0.003,afade=t=out:st={length - .035:.5f}:d=0.035'
        # Reset timestamps before fades; otherwise a late source excerpt can be silent.
        excerpt_filters = f'atrim=start={start}:duration={length},asetpts=PTS-STARTPTS,{filters}'
        raw = run(['ffmpeg', '-v', 'error', '-i', str(original),
                   '-ac', '1', '-ar', str(RATE), '-af', excerpt_filters, '-f', 'f32le', 'pipe:1']).stdout
        data = np.frombuffer(raw, dtype='<f4').copy()
        assert len(data) == round(length * RATE), (sid, len(data))
        before = measure(train(data))
        gain_db = min(-26 - before['lufs'], -8 - 20 * math.log10(max(float(abs(data).max()), 1e-9)))
        data *= 10 ** (gain_db / 20)
        run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(RATE), '-ac', '1', '-i', 'pipe:0',
             '-c:a', 'pcm_s24le', str(preview)], data.astype('<f4').tobytes())
        after = measure(train(data))
        assert abs(after['lufs'] + 26) < .20, (sid, after)
        assert float(abs(data).max()) < 10 ** (-7.9 / 20)
        recipe = {'sourceStart': start, 'sourceEnd': round(start + length, 6), 'duration': length,
                  'filters': filters, 'gainDb': round(gain_db, 4), 'reference': '40 shots, 10/s, 4 seconds, before live voice-cap gain',
                  'referenceMetrics': after, 'previewPeakDbFS': round(20 * math.log10(float(abs(data).max())), 3),
                  'format': 'PCM24 mono 48000 Hz', 'synthesis': False}
        (folder / 'processed/preparation.json').write_text(json.dumps(recipe, indent=2), encoding='utf8')
        credit = (f'{source_info["name"]} — {author}\n{source_info["url"]}\n'
                  f'{license_id}: {license_url}\nИзменения: выделен одиночный импульс, mono, фильтры 70–6000 Hz, плавные края, выравнивание громкости.\n')
        (license_folder / 'attribution.txt').write_text(credit + conditions, encoding='utf8')
        shutil.copyfile(evidence, license_folder / 'license-page.html.txt')
        (license_folder / 'license.txt').write_text(f'{license_url}\nПроверено: {DATE}\n{conditions}\n\n' + plain_page(evidence), encoding='utf8')
        processing = (f'Превью: {start:.3f}–{start + length:.3f} с исходника; mono PCM24/48 кГц, фильтры 70–6000 Hz, края 3/35 мс. '
                      f'Громкость сравнения: −26 LUFS на эталонной очереди 10/с (4 с), одно постоянное усиление {gain_db:+.2f} dB; '
                      f'пик одиночного файла {recipe["previewPeakDbFS"]:.1f} dBFS. Живой режим дополнительно делит уровень на лимит голосов для запаса при наложении. '
                      f'{original_note} Новые слои, синтез, сдвиг высоты и компрессия не применялись.')
        record = {'id': sid, 'objectId': OBJECT, 'title': title, 'description': description,
                  'tags': tags, 'file': preview.relative_to(DIST).as_posix(), 'duration': length,
                  'originalFile': original.relative_to(DIST).as_posix(), 'originalFilename': original_name,
                  'downloadedAt': DATE, 'sha256': sha(preview), 'originalSha256': sha(original),
                  'processing': processing, 'preparation': recipe, 'source': source_info,
                  'license': {'id': license_id, 'name': {'CC0': 'CC0 1.0', 'CC-BY-4.0': 'CC BY 4.0', 'SONNISS-GDC-2.0': 'Sonniss GDC 2.0'}[license_id],
                              'url': license_url, 'commercialUse': True, 'attribution': conditions, 'verifiedAt': DATE,
                              'evidenceFile': f'licenses/{sid}/license.txt', 'attributionFile': f'licenses/{sid}/attribution.txt'}}
        if is_fs:
            record['originalLabel'] = 'Превью источника · HQ MP3 ↓'
            record['description'] += ' Скачан публичный HQ MP3; оригинальный WAV доступен на сайте после входа.'
        if slug in SHORTLIST:
            rank, reason = SHORTLIST[slug]
            record['shortlist'] = {'rank': rank, 'reason': reason}
        (license_folder / 'source.txt').write_text(json.dumps({'record': record, 'originalNote': original_note}, ensure_ascii=False, indent=2), encoding='utf8')
        records.append(record)
        print(sid, after, flush=True)
    assert len({r['originalSha256'] for r in records}) == len(records) <= 24
    assert not ({r['originalSha256'] for r in records} & {r.get('originalSha256') for r in old.values()})
    catalog['sounds'] = list(old.values()) + records
    if not any(item['id'] == OBJECT for item in catalog['objects']):
        catalog['objects'].append({'id': OBJECT, 'categoryId': 'weapons', 'name': 'Пулемёт · новая подборка',
                                  'description': '24 готовые записи · 5 предварительных кандидатов · живой тест 1–30 выстр./с. Оцените комфорт длительного повторения самостоятельно.'})
    catalog['updatedAt'] = DATE
    import_result = gate.store.import_records(records, catalog['categories'], catalog['objects'])
    assert (sha(selections_path) if selections_path.exists() else None) == selection_hash
    print(json.dumps(import_result, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
