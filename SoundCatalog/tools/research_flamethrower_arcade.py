"""Public human-designed game SFX for Stylized / Arcade. Does not modify the existing catalog."""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import re
import sys
import html
import zipfile

import research_flamethrower as core

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / '.local/research/flamethrower-arcade'
WORK.mkdir(parents=True, exist_ok=True)
core.WORK = WORK

GDC = [
    'Energy - defense barrier activation idle&stop.wav',
    'Energy - force_field_5_loop.wav',
    'Energy - handheld generators 6.wav',
    'Energy - electric_fuse-08.wav',
    'dark_portal_wind_effect_04.wav',
    'CYBERDECK_Holonoise_Energy_15.wav',
    'Laser_Beam_004.wav',
    'MagicPack2_Air_Soundscapes_Broken002.wav',
    'MAGEvil_Dark Spell Dark Energy Attack_RogueWaves_DruidMagic.wav',
    'Bluezone_BC0292_alien_tripod_pneumatic_chemical_weapon_black_smoke_001.wav',
    'DSGN VORTEX IN.wav',
    'DSGNMisc_Charge Down Classic 1_RogueWaves_AnimeStudio.wav',
    'PodRacer_Medium_RPM03.wav',
    'Bluezone_BC0235_synth_texture_002.wav',
    'WHSH_Whoosh Synth Noise 28_RogueWaves_AnimeStudio.wav',
    'Electric_Arc_Deep_Bass_Seamless_Loop_02.wav',
    'Magic_Bonus_ElectricityLoop33.wav',
    'CK_Force_Field_Loop-32.wav',
    'Hybrid_Electro_Magnetic_LOOP_5.wav',
    'Tension - warbley burning tone 1.wav',
    'Tension - low oscillating distortion 3.wav',
    'Lost Transmissions - Noise Loops_16.wav',
    'Fire_Spell_Dragon_Trap_03.wav',
    'AbstractSpaceshipA07_Flat.wav',
    'Sci Fi Movement Long 01.wav',
    'Misc_BreathAttack.wav',
    'Amb_EnergyHumDown.wav',
    'Bluezone_BC0242_alien_synthetic_texture_010.wav',
    'Electric_Energy_Ambient_Warble_Powerup_03.wav',
]


def gdc():
    jobs = []
    paths = list((ROOT / '.local/research/machine-gun-v3').glob('gdc*.json')) + list(WORK.glob('index-*.json'))
    for path in paths:
        index = json.loads(path.read_text(encoding='utf8'))
        for entry in index.get('files', []):
            if entry['path'].split('/')[-1] in GDC:
                year = index.get('year') or int(re.search(r'20\d\d', path.name).group())
                jobs.append(dict(year=year, url=index['url'], **entry))
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        futures = {pool.submit(core.fetch_gdc, entry): entry for entry in jobs}
        for future in concurrent.futures.as_completed(futures):
            try: print(json.dumps(future.result()), flush=True)
            except Exception as exc: print(json.dumps({'error': str(exc), 'source': futures[future]['path']}), flush=True)


def batch():
    urls = []
    for page in WORK.glob('search-*.html'):
        for href in core.Page(page.read_text(encoding='utf8')).links:
            if re.fullmatch(r'/people/[^/]+/sounds/\d+/', href.split('?')[0]):
                url = 'https://freesound.org' + href.split('?')[0]
                if url not in urls: urls.append(url)
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        futures = {pool.submit(core.fetch_fs, url): url for url in urls}
        for future in concurrent.futures.as_completed(futures):
            try:
                info = future.result()
                print(json.dumps({k: info[k] for k in ('id', 'title', 'licenses', 'local') if k in info}), flush=True)
            except Exception as exc: print(json.dumps({'source': futures[future], 'error': str(exc)}), flush=True)


def scan_archives():
    page = (ROOT / '.local/research/machine-gun-v3/sonniss.html').read_text(encoding='utf8')
    urls = list(dict.fromkeys(html.unescape(u) for u in re.findall(r'https://downloads.sonniss.com/[^\s"<>]+\.zip', page)))
    # Some older archive URLs sit in a /2020/ directory: derive year from the decoded filename.
    from urllib.parse import unquote
    urls = [u for u in urls if re.search(r'GDC\s*(2019|2020|2016)', unquote(u).split('/')[-1])]
    def scan(url):
        name = 'index-' + hashlib.sha256(url.encode()).hexdigest()[:12] + '.json'
        cached = WORK / name
        if cached.exists(): return json.loads(cached.read_text(encoding='utf8'))
        remote = core.RemoteZIP(url)
        with zipfile.ZipFile(remote) as archive:
            files = [dict(path=x.filename, size=x.file_size, compressed=x.compress_size, crc=x.CRC) for x in archive.infolist() if not x.is_dir()]
        year = int(re.search(r'GDC\s*(20\d\d)', unquote(url).split('/')[-1]).group(1))
        result = dict(url=url, year=year, files=files, bytesDownloaded=remote.downloaded)
        cached.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
        return result
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        futures = {pool.submit(scan, url): url for url in urls}
        for future in concurrent.futures.as_completed(futures):
            try:
                item = future.result()
                found = [f for f in item['files'] if re.search(r'(flame|firestorm|plasma|beam|force.?field|reactor|thruster|energy.*loop|magic.*loop)', f['path'], re.I)]
                print(json.dumps(dict(year=item['year'], archive=item['url'].split('/')[-1], matches=found)), flush=True)
            except Exception as exc: print(json.dumps(dict(error=str(exc), url=futures[future])), flush=True)


def harvester():
    api = 'https://api.github.com/repos/gdquest-demos/godot-2d-space-game/git/trees/master?recursive=1'
    tree = core.get(api).json()
    (WORK / 'harvester-tree.json').write_text(json.dumps(tree, indent=2), encoding='utf8')
    for entry in tree['tree']:
        if re.search(r'license|credit|author', entry['path'], re.I):
            print('LICENSE', entry['path'], flush=True)
            if entry['type'] == 'blob' and not entry['path'].lower().endswith(('.png', '.import')):
                url = f'https://raw.githubusercontent.com/gdquest-demos/godot-2d-space-game/{tree["sha"]}/{entry["path"]}'
                content = core.get(url).text
                (WORK / ('harvester-' + entry['path'].replace('/', '-') + '.txt')).write_text(content, encoding='utf8')
                print(content[:4000], flush=True)
        if re.search(r'\.(ogg|flac|wav)$', entry['path'], re.I): print('AUDIO', entry['path'], flush=True)


def little_robot():
    url = 'https://opengameart.org/content/sci-fi-sound-effects-library'
    response = core.get(url)
    (WORK / 'oga-little-robot.html').write_text(response.text, encoding='utf8')
    page = core.Page(response.text)
    download = next(u for u in page.links if '/files/' in u and u.lower().endswith('.zip'))
    from urllib.parse import urljoin
    download = urljoin(url, download)
    archive_path = WORK / 'oga-little-robot.zip'
    if not archive_path.exists(): archive_path.write_bytes(core.get(download).content)
    with zipfile.ZipFile(archive_path) as archive:
        names = archive.namelist()
        (WORK / 'oga-little-robot-manifest.json').write_text(json.dumps(dict(url=url, downloadUrl=download, files=names), indent=2), encoding='utf8')
        for name in names:
            if re.search(r'(engine|warp|license|readme)', name, re.I): print(name, flush=True)
        member = 'Sci-Fi Sound Library/Wav/SpaceShip_Engine_Large_Loop_00.wav'
        payload = archive.read(member)
        local = 'oga-lrsf-large-engine.wav'
        (WORK / local).write_bytes(payload)
        info = dict(id='oga-lrsf-large-engine', local=local, title='SpaceShip Engine Large Loop',
                    author='Little Robot Sound Factory', url=url, downloadUrl=download, archiveMember=member,
                    licenses=['https://creativecommons.org/licenses/by/3.0/'], sha256=hashlib.sha256(payload).hexdigest(),
                    text='Ready-made stylized sci-fi spaceship engine loop. Author page licenses this library CC BY 3.0.')
        (WORK / 'oga-lrsf-large-engine.json').write_text(json.dumps(info, indent=2), encoding='utf8')


if __name__ == '__main__':
    core.baseline()
    if sys.argv[1] == 'search':
        for query in sys.argv[2:]: print(json.dumps(core.discover(query)), flush=True)
    elif sys.argv[1] == 'batch': batch()
    elif sys.argv[1] == 'gdc': gdc()
    elif sys.argv[1] == 'scan': scan_archives()
    elif sys.argv[1] == 'harvester': harvester()
    elif sys.argv[1] == 'oga': little_robot()
    elif sys.argv[1] == 'fs':
        for url in sys.argv[2:]:
            try: print(json.dumps(core.fetch_fs(url)), flush=True)
            except Exception as exc: print(json.dumps({'url': url, 'error': str(exc)}), flush=True)
    elif sys.argv[1] == 'analyze': core.analyze()
    elif sys.argv[1] == 'licenses':
        for name, url in {'cc0': 'https://creativecommons.org/publicdomain/zero/1.0/',
                          'cc-by3': 'https://creativecommons.org/licenses/by/3.0/',
                          'cc-by4': 'https://creativecommons.org/licenses/by/4.0/',
                          'sonniss-license': 'https://sonniss.com/gdc-bundle-license/',
                          'sonniss': 'https://sonniss.com/gameaudiogdc/'}.items():
            (WORK / (name + '.html')).write_text(core.get(url).text, encoding='utf8')
            print(name, flush=True)
