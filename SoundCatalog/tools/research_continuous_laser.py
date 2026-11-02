"""Public game-SFX research for continuous beams; never writes the active catalog."""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import unquote, urljoin

import requests
import research_flamethrower as core

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / '.local/research/continuous-laser'
WORK.mkdir(parents=True, exist_ok=True)
core.WORK = WORK

PAGES = {
    'fs-unfa-beam': 'https://freesound.org/people/unfa/sounds/584191/',
    'fs-thimblerig-field': 'https://freesound.org/people/Thimblerig/sounds/467727/',
    '99-interspace': 'https://99sounds.org/interspace/',
    '99-scifi': 'https://99sounds.org/sci-fi-sounds/',
    '99-textures': 'https://99sounds.org/cinematic-textures/',
    '99-license': 'https://99sounds.org/license/',
    'oga-jc': 'https://opengameart.org/content/jc-sounds-sci-fi-pack-vol-1',
    'oga-drone': 'https://opengameart.org/content/sci-fi-drone-loop',
    'sonniss': 'https://sonniss.com/gameaudiogdc/',
    'sonniss-license': 'https://sonniss.com/gdc-bundle-license/',
    'cc-by3': 'https://creativecommons.org/licenses/by/3.0/',
    'cc-by4': 'https://creativecommons.org/licenses/by/4.0/',
}

GDC_NAMES = [
    'CYBERDECK_Hologram_designed_19.wav',
    'Bluezone_BC0285_afterlife_ambient_synth_texture_005.wav',
    'Bluezone_BC0235_ambience_sfx_013.wav',
    'Bluezone_BC0234_ambience_sfx_004.wav',
    'Space_Ambiences_03.wav',
    'Magic_Bonus_ElectricityLoop33.wav',
    'Magic_Ambience_Electricity01.wav',
    'Electric_Arc_Deep_Bass_Seamless_Loop_02.wav',
    'Laser_Beam_004.wav',
    'background_room_interior_hum_loop_01.wav',
    'PM_FSSF_UI_HOLOGRAM_AMB_LOOP.wav',
    'PM_FSSF_AMBIENCE_SOUNDSCAPE_LOOP_1.wav',
    'Sci-Fi&Futuristic - Ultraviolet Rays.wav',
    'Abnormal&Experimental - Multiverse Interaction.wav',
    'PM_DSSP_Source_Airy_Intake_Hum_Scifi_Futuristic_Constant_Engine_27.wav',
    'DSGNDron_Sci-Fi Drone Light Space Exploration_SYSO_SYSO018.wav',
    'DEFECT_Electro_Magnetic_29.wav',
    'Dream_Drone_09.wav',
    'Sci-Fi_Drone_11.wav',
    'DSGNDron_Harmonic Electric Drone_JSE_VEMF.wav',
    'ELECBuzz_Electric Buzz Loop 23_JSE_VEMF_Stereo.wav',
    'Bluezone_BC0233_ambience_energy_drone_020.wav',
    'Bluezone_BC0233_synth_texture_001.wav',
    'space_station_04_loop.wav',
    'SBsd_Insectoid Humming 002.wav',
    'SBsd_Dark Resonance 001.wav',
    'Granular Abstract Ambient Element (12).wav',
    'Granular Abstract Ambient Atmosphere (24).wav',
    'Lost Transmissions - Noise Loops_16.wav',
]


def pages():
    def fetch(item):
        name, url = item
        response = requests.get(url, timeout=25)
        result = dict(name=name, url=url, status=response.status_code)
        if response.ok:
            (WORK / (name + '.html')).write_text(response.text, encoding='utf8')
            page = core.Page(response.text)
            result['links'] = list(dict.fromkeys(urljoin(url, u) for u in page.links
                if re.search(r'gumroad|download|mediafire|sendowl|\.zip|\.rar|\.7z|\.wav|license', u, re.I)))
        return result
    result = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        for item in pool.map(fetch, PAGES.items()):
            result.append(item)
            print(json.dumps(item, ensure_ascii=False), flush=True)
    (WORK / 'page-results.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')


def gdc_jobs():
    paths = list((ROOT / '.local/research/machine-gun-v3').glob('gdc*.json'))
    paths += list((ROOT / '.local/research/flamethrower-arcade').glob('index-*.json'))
    jobs = []
    for path in paths:
        index = json.loads(path.read_text(encoding='utf8'))
        for entry in index.get('files', []):
            if entry['path'].split('/')[-1] not in GDC_NAMES:
                continue
            year = index.get('year') or int(re.search(r'20\d\d', path.name).group())
            jobs.append(dict(year=year, url=index['url'], **entry))
    (WORK / 'gdc-candidates.json').write_text(json.dumps(jobs, ensure_ascii=False, indent=2), encoding='utf8')
    return jobs


def gdc():
    jobs = gdc_jobs()
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        futures = {pool.submit(core.fetch_gdc, entry): entry for entry in jobs}
        for future in concurrent.futures.as_completed(futures):
            try:
                print(json.dumps(future.result(), ensure_ascii=False), flush=True)
            except Exception as exc:
                print(json.dumps(dict(error=str(exc), source=futures[future]['path'])), flush=True)


def oga():
    for slug, author in [('seamless-energy-emission-loop', 'zeroisnotnull'),
                         ('sci-fi-drone-loop', 'jdagenet')]:
        url = 'https://opengameart.org/content/' + slug
        response = core.get(url)
        (WORK / ('oga-' + slug + '.html')).write_text(response.text, encoding='utf8')
        page = core.Page(response.text)
        links = [urljoin(url, u) for extension in ('.flac', '.wav', '.ogg')
                 for u in page.links if u.lower().endswith(extension) and '/files/' in u]
        if not links:
            raise ValueError('No public audio: ' + url)
        download = links[0]
        filename = 'oga-' + slug + Path(download).suffix
        payload = core.get(download).content
        (WORK / filename).write_bytes(payload)
        result = dict(id='oga-' + slug, author=author, url=url, title=page.title,
                      local=filename, downloadUrl=download,
                      licenses=[u for u in page.links if 'creativecommons.org/' in u],
                      sha256=hashlib.sha256(payload).hexdigest(), text='\n'.join(page.words))
        (WORK / ('oga-' + slug + '.json')).write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
        print(json.dumps({k: result[k] for k in ('id', 'licenses', 'downloadUrl')}), flush=True)


def kenney():
    url = 'https://kenney.nl/assets/sci-fi-sounds'
    response = core.get(url)
    (WORK / 'kenney.html').write_text(response.text, encoding='utf8')
    page = core.Page(response.text)
    links = [urljoin(url, u) for u in page.links if u.lower().endswith('.zip')]
    if not links:
        raise ValueError('No public ZIP')
    payload = core.get(links[0]).content
    (WORK / 'kenney.zip').write_bytes(payload)
    with core.zipfile.ZipFile(WORK / 'kenney.zip') as archive:
        for entry in archive.infolist():
            if not entry.filename.lower().endswith(('.wav', '.ogg', 'license.txt')):
                continue
            target = WORK / 'kenney' / entry.filename
            if not target.resolve().is_relative_to((WORK / 'kenney').resolve()):
                raise ValueError('Unsafe ZIP entry')
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(archive.read(entry))
    (WORK / 'kenney-source.json').write_text(json.dumps(dict(url=url, downloadUrl=links[0],
        sha256=hashlib.sha256(payload).hexdigest()), indent=2), encoding='utf8')
    print('Downloaded Kenney Sci-fi Sounds', len(payload), flush=True)


if __name__ == '__main__':
    core.baseline()
    if sys.argv[1] == 'pages': pages()
    elif sys.argv[1] == 'gdc': gdc()
    elif sys.argv[1] == 'oga': oga()
    elif sys.argv[1] == 'kenney': kenney()
    elif sys.argv[1] == 'analyze': core.analyze()
    elif sys.argv[1] == 'manifest':
        jobs = gdc_jobs()
        print(json.dumps(dict(candidates=len(jobs), originalBytes=sum(j['size'] for j in jobs)), indent=2))
