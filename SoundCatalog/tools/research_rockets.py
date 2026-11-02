"""Download public licensed rocket candidates; never mutate the active library."""
import concurrent.futures
import hashlib
import json
import re
import sys
from pathlib import Path
from urllib.parse import urljoin

import research_flamethrower as core

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / '.local/research/rockets'
WORK.mkdir(parents=True, exist_ok=True)
core.WORK = WORK

NAMES = [
    'Steampunk Weapons - Rocket Launcher - Shot - 04.wav', 'Laser Rocket 7.wav',
    'Vehicle5_BlastOff1.wav', 'water_blast_projectile_spell_03.wav',
    'W_a_P_Spell_Whoosh_19.wav', 'W_a_P_Epic_Whoosh_8.wav',
    'WHOOSH PASS SF LOW.wav', 'WHSH_Pure SciFi-Whoosh Fast 03_RSCPC_DW.wav',
    'WHOOSH AIR Steady Smooth_02.wav',
    'PM_DSSP_Passby_Designed_Whoosh_Futuristic_Scifi_Airy_Synth_Spaceship_Fast_12.wav',
    'SF_sdb_WHOOSH_15_space_vehicle.wav', 'Whoosh_Fast_02.wav',
    'Whoosh,Sound Design,Airy,Full,Forceful,Soft.wav',
    'Whoosh,Sound Design,Cartoon,Whir,2.wav',
    'Power Whoosh Large 05.wav', 'morph_whoosh_13.wav',
    'Explosion Warped.wav', 'explosion_large_07.wav',
    'electric_surge_blast_04.wav', 'Bluezone_BC0288_combat_drone_weapon_explosion_004.wav',
    'MagicPack2_WaterExplosions_Explosion2-004.wav',
    'GAMEVideo_Big Explosion_Rogue Waves_8-Bit Legend_13.wav',
    'EXPLDsgn_Explosion Rumble Distorted_07_SFRMS_SCIWPNS.wav',
    'Bluezone_BC0296_steampunk_weapon_flare_shot_explosion_003.wav',
    'Big Blast 4.wav', 'Bluezone_BC0244_explosion_009_01.wav',
    'Magic_Explosion_Short19.wav', '8BitRetroSFXPack1_Contemporary_Explosion03.wav',
    'SW001_8-Bit-Games-017_Explosion.wav',
    'Future Weapons 3 - Grenade Launcher 2 - Hit 2.wav',
    'Signum_source_composite_impact_2.wav',
    'Magic_Spells_Impact_Creation20.wav', 'Magic_Spells_Impact_Water_Low23.wav',
]
SLUGS = ['rocket-launch-pack', '4-projectile-launches', 'missile-sound', 'rocket-launch',
         '2-high-quality-explosions', 'chunky-explosion', 'dull-explosion',
         '9-explosion-sounds', 'synthesized-explosion']


def gdc():
    indexes = list((ROOT / '.local/research/machine-gun-v3').glob('gdc*.json'))
    indexes += list((ROOT / '.local/research/flamethrower-arcade').glob('index-*.json'))
    jobs = []
    for path in indexes:
        index = json.loads(path.read_text(encoding='utf8'))
        for item in index.get('files', []):
            if item['path'].split('/')[-1] in NAMES and not any(j['path']==item['path'] for j in jobs):
                year = index.get('year') or int(re.search(r'20\d\d', path.name).group())
                jobs.append(dict(year=year, url=index['url'], **item))
    (WORK/'gdc-candidates.json').write_text(json.dumps(jobs, ensure_ascii=False, indent=2), encoding='utf8')
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        futures = {pool.submit(core.fetch_gdc,j): j for j in jobs}
        for future in concurrent.futures.as_completed(futures):
            try: print(json.dumps(future.result()), flush=True)
            except Exception as error: print(json.dumps(dict(error=str(error),path=futures[future]['path'])),flush=True)


def oga(slug):
    saved=WORK/('oga-'+slug+'.json')
    if saved.exists():
        return dict(id='oga-'+slug,cached=True)
    url='https://opengameart.org/content/'+slug
    page=core.get(url)
    (WORK/('oga-'+slug+'.html')).write_text(page.text,encoding='utf8')
    parsed=core.Page(page.text)
    licenses=list(dict.fromkeys(u for u in parsed.links if 'creativecommons.org/' in u))
    links=[urljoin(url,u) for ext in ('.7z','.zip','.flac','.wav','.ogg','.mp3') for u in parsed.links
           if '/files/' in u and u.lower().endswith(ext)]
    if not links: return dict(slug=slug,error='No public audio')
    payload=core.get(links[0]).content
    filename='oga-'+slug+Path(links[0]).suffix
    (WORK/filename).write_bytes(payload)
    result=dict(id='oga-'+slug,url=url,downloadUrl=links[0],title=parsed.title,local=filename,
                sha256=hashlib.sha256(payload).hexdigest(),licenses=licenses,text='\n'.join(parsed.words))
    (WORK/('oga-'+slug+'.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    return {k:result[k] for k in ('id','title','licenses','downloadUrl')}


if __name__=='__main__':
    core.baseline()
    if sys.argv[1]=='gdc':gdc()
    elif sys.argv[1]=='oga':
        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
            futures={pool.submit(oga,s):s for s in SLUGS}
            for future in concurrent.futures.as_completed(futures):
                try:print(json.dumps(future.result()),flush=True)
                except Exception as error:print(json.dumps(dict(slug=futures[future],error=str(error))),flush=True)
    elif sys.argv[1]=='analyze':
        raise SystemExit('Use audio-analysis.json: archives must be extracted before audio analysis.')
