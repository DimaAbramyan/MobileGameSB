"""Curated game SFX. Reuse the catalog admission/Removed pipeline; no synthesis."""
import json
import math
import shutil
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

import numpy as np
from import_flamethrower import ROOT, DIST, RATE, run, measure, sha

sys.path.insert(0,str(ROOT))
from audio_identity import ImportGate

WORK=ROOT/'.local/research/rockets'
DATE='2026-10-29'
# Source, role, stable slug, display name, character. One distinct design per row.
GDC=[
 ('gdc-31a6f8394a40','launch','steampunk-rocket','SoundMorph · Steampunk Rocket','Механический импульс и реактивный выброс; готовое оружие'),
 ('gdc-f3b626fee9f6','launch','vehicle-blastoff','David Dumais · Sci-Fi Blast Off','Короткий дизайнерский разгон sci-fi аппарата'),
 ('gdc-f54ca1ac5b58','launch','water-projectile','Gamemaster · Fluid Projectile','Мягкий фантастический водяной выброс; альтернатива сухому ракетному шипению'),
 ('gdc-7254826a6509','launch','spell-whoosh','CB Sound Design · Spell Propulsion','Короткий магический импульс движения'),
 ('gdc-3d42d93ecf59','launch','epic-whoosh','CB Sound Design · Epic Acceleration','Более плотный дизайнерский выброс воздуха'),
 ('gdc-3297a9d0ce20','launch','low-scifi-pass','Chupapsound · Low Sci-Fi Pass','Низкий фантастический пролёт; мягкая альтернатива запуску'),
 ('gdc-03ed35b47f34','launch','pure-scifi-fast','Rescopic · Pure Sci-Fi Acceleration','Быстрый sci-fi whoosh с коротким разгоном'),
 ('gdc-689d60885a86','launch','smooth-air','SmartSoundFX · Smooth Air Launch','Спокойный гладкий воздушный импульс; менее выраженный удар'),
 ('gdc-b80d4736e98d','launch','futuristic-passby','PMSFX · Futuristic Flyout','Дизайнерский воздушно-энергетический разгон космического аппарата'),
 ('gdc-d17b84e564bd','launch','space-vehicle','Soundbankster · Space Vehicle Boost','Фантастическая тяга из The Sci-Fi Book'),
 ('gdc-6966819fe546','launch','fast-whoosh','Sound Spark · Fast Propulsion','Короткий собранный выброс без длинного двигателя'),
 ('gdc-dff7db3a9198','launch','soft-forceful','Airborne Sound · Soft Forceful Launch','Полный, но мягкий воздушный импульс'),
 ('gdc-813637ad5e7c','launch','cartoon-whir','Airborne Sound · Arcade Whir','Очень короткий аркадный вращающийся выброс'),
 ('gdc-3c5955d7d0f6','launch','power-whoosh','L.A. Sounds · Power Launch','Плотная дизайнерская тяга из Transition Movement'),
 ('gdc-fd9b28f83c2d','launch','morph-whoosh','Soundrangers · Morphing Launch','Изменяющийся фантастический whoosh; более яркая фактура'),
 ('gdc-05578f0e6eb2','explosion','warped-blast','c2caudio · Warped Detonation','Искажённая по форме sci-fi детонация; длинный тихий хвост оставлен в оригинале'),
 ('gdc-9c7588fb004b','explosion','game-blast','Gamemaster · Compact Game Blast','Готовый игровой взрыв с основным ударом в начале'),
 ('gdc-57856bae96eb','explosion','electric-surge','Gamemaster · Electric Surge Blast','Электрическая энергетическая детонация; более яркий вариант'),
 ('gdc-929b7bc917ae','explosion','combat-drone','Bluezone · Combat Drone Detonation','Игровая sci-fi детонация из Combat Drone'),
 ('gdc-4d5b9a37ae1c','explosion','fluid-blast','David Dumais · Fluid Detonation','Плотный водяной магический blast вместо сухого порохового треска'),
 ('gdc-653975afea9e','explosion','arcade-legend','Rogue Waves · Arcade Explosion','Короткий ретро-аркадный взрыв из 8-Bit Legend'),
 ('gdc-ef14e505a1fe','explosion','steampunk-flare','Bluezone · Steampunk Flare Blast','Выразительная стилизованная вспышка с ударом'),
 ('gdc-c644baccd535','explosion','scifi-big-blast','Async Audio · Sci-Fi Blast','Короткое тело фантастического blast без долгого двигателя'),
 ('gdc-dccd49e63e80','explosion','mothership','Bluezone · Mothership Explosion','Энергетическая детонация из District 10'),
 ('gdc-fbee483c1237','explosion','magic-short','David Dumais · Short Magic Blast','Собранный магический взрыв с коротким затуханием'),
 ('gdc-235e70fb27e1','explosion','contemporary-retro','David Dumais · Contemporary Arcade Blast','Современный ретро-игровой взрыв'),
 ('gdc-c4940694aa2e','explosion','pixel-impact','ShapingWaves · Pixel Detonation','Короткая пиксельная детонация из игровой библиотеки'),
 ('gdc-695145e7d1ef','explosion','future-launcher-hit','SoundMorph · Future Launcher Impact','Готовый удар боеприпаса из Future Weapons 3'),
 ('gdc-c76b4f35eeec','explosion','signum-composite','Blik Blinks · Composite Sci-Fi Impact','Короткий составной sci-fi удар; копия из другого архива не добавлена'),
 ('gdc-8413cfb25732','explosion','low-water-impact','David Dumais · Soft Fluid Impact','Мягкий низкий магический удар; лёгкая альтернатива тяжёлому взрыву'),
]


def candidates():
    for key,role,slug,title,character in GDC:
        m=json.loads((WORK/(key+'.json')).read_text(encoding='utf8'))
        pack=m['path'].split('/')[-2]
        source=dict(name=pack+' · Sonniss GDC '+str(m['year']),author=pack.split(' - ')[0],
                    url='https://sonniss.com/gameaudiogdc/',downloadUrl=m['url'],
                    archiveMember=m['path'],archiveCRC32=m['crc'],recordingTitle=Path(m['path']).name)
        yield dict(role=role,slug=slug,title=title,character=character,path=WORK/m['local'],digest=m['sha256'],
                   source=source,licenseId='SONNISS-GDC-2.0',licenseName='Sonniss GDC 2.0',
                   licenseUrl='https://sonniss.com/gdc-bundle-license/',
                   conditions='Коммерческие игры и обработка разрешены, атрибуция не обязательна. Нельзя раздавать записи отдельно как SFX-библиотеку или использовать для обучения AI/ML.',
                   evidence=[ROOT/'.local/research/continuous-laser/sonniss.html',ROOT/'.local/research/continuous-laser/sonniss-license.html'])
    oga=[
      ('4-projectile-launches','launches/flaunch.wav','launch','michel-fire','Michel Baradari · Fire Projectile','Короткий выразительный запуск огненного снаряда','Michel Baradari','by/3.0'),
      ('4-projectile-launches','launches/rlaunch.wav','launch','michel-rocket','Michel Baradari · Rocket Launch','Готовый игровой запуск ракеты','Michel Baradari','by/3.0'),
      ('rocket-launch-pack','rocket_launch_4.wav','launch','dklon-rocket','dklon · Compact Rocket','Один представитель четырёх связанных запусков; выбран менее яркий по спектру','dklon','by/3.0'),
      ('missile-sound',None,'launch','mikeask-missile','mikeask · Missile Launch','Готовый авторский запуск ракеты','mikeask','publicdomain/zero/1.0'),
      ('2-high-quality-explosions','explosions/explodemini.wav','explosion','michel-mini','Michel Baradari · Compact Explosion','Компактный игровой взрыв; большой вариант не добавлен как ещё один кандидат','Michel Baradari','by/3.0'),
      ('synthesized-explosion',None,'explosion','qubodup-designed','qubodup · Designed Explosion','Готовый авторский эффект из OpenGameArt, не синтезированный нами','qubodup','publicdomain/zero/1.0'),
      ('chunky-explosion',None,'explosion','joth-chunky','Joth · Chunky Explosion','Плотный авторский игровой взрыв; оригинал опубликован в MP3','Joth','publicdomain/zero/1.0'),
    ]
    for package,member,role,slug,title,character,author,license_path in oga:
        key='oga-'+package;m=json.loads((WORK/(key+'.json')).read_text(encoding='utf8'))
        assert any(license_path+'/' in u or u.rstrip('/').endswith(license_path) for u in m['licenses'])
        file=WORK/'extracted'/key/member if member else WORK/m['local']
        source=dict(name=m['title'].split('|')[0].strip()+' · OpenGameArt',author=author,
                    url=m['url'],downloadUrl=m['downloadUrl'],recordingTitle=member or Path(unquote(urlparse(m['downloadUrl']).path)).name)
        if member:source['archiveMember']=member
        by='by/3.0'==license_path
        yield dict(role=role,slug=slug,title=title,character=character,path=file,digest=sha(file),source=source,
                   licenseId='CC-BY-3.0' if by else 'CC0',licenseName='CC BY 3.0' if by else 'CC0 1.0',
                   licenseUrl='https://creativecommons.org/'+('licenses/by/3.0/' if by else 'publicdomain/zero/1.0/'),
                   conditions=f'Указать {author}, источник, лицензию и изменения.' if by else 'CC0: коммерческое использование разрешено; атрибуция не обязательна.',
                   evidence=[WORK/(key+'.html')])
    for archive,member,role,slug,title,character,url,download in [
      ('rubberduck50.zip','rocket_01.ogg','launch','rubberduck-rocket','rubberduck · Sci-Fi Rocket','Готовый запуск из 50 CC0 Sci-Fi SFX','https://opengameart.org/content/50-cc0-sci-fi-sfx','https://opengameart.org/sites/default/files/sci-fi-sfx.zip'),
      ('rubberduck50.zip','explosion_01.ogg','explosion','rubberduck-blast','rubberduck · Sci-Fi Explosion','Один современный игровой blast; соседний дубль и резкий retro не добавлены','https://opengameart.org/content/50-cc0-sci-fi-sfx','https://opengameart.org/sites/default/files/sci-fi-sfx.zip'),
      ('kenney.zip','Audio/explosionCrunch_002.ogg','explosion','kenney-crunch','Kenney · Compact Crunch Explosion','Один представитель семейства Explosion Crunch из Sci-fi Sounds','https://kenney.nl/assets/sci-fi-sounds','https://kenney.nl/media/pages/assets/sci-fi-sounds/6b296f9ecf-1677589334/kenney_sci-fi-sounds.zip'),
    ]:
        file=WORK/'cached'/archive.removesuffix('.zip')/member
        author='Kenney' if archive=='kenney.zip' else 'rubberduck'
        yield dict(role=role,slug=slug,title=title,character=character,path=file,digest=sha(file),
                   source=dict(name=author+' · Sci-Fi Sounds',author=author,url=url,downloadUrl=download,archiveMember=member,recordingTitle=member),
                   licenseId='CC0',licenseName='CC0 1.0',licenseUrl='https://creativecommons.org/publicdomain/zero/1.0/',
                   conditions='CC0: коммерческое использование разрешено; атрибуция не обязательна. Неизменённый файл ранее скачанного официального пакета, впервые добавленный в активный каталог.',
                   evidence=[ROOT/'.local/research/machine-gun'/('kenney.html' if author=='Kenney' else 'rubberduck50.html'),ROOT/'.local/research/machine-gun/cc0-legal.html'])


def sequence(data):
    signal=np.zeros((10*RATE,data.shape[1]),dtype=np.float32)
    for i in range(8):signal[i*RATE:i*RATE+len(data)]+=data
    return signal


def main():
    gate=ImportGate(ROOT);records=[];results=[]
    for c in candidates():
        sid=('rl-' if c['role']=='launch' else 're-')+c['slug']
        assert sha(c['path'])==c['digest']
        if not gate.allow(sid,c['path'],c['source']):continue
        folder=DIST/'audio'/('rocket-'+c['role'])/('rocket-'+c['role'])/sid
        evidence=DIST/'licenses'/sid
        for d in [folder/'originals',folder/'processed',evidence]:d.mkdir(parents=True,exist_ok=True)
        original=folder/('originals/source'+c['path'].suffix)
        shutil.copyfile(c['path'],original)
        probe=json.loads(run(['ffprobe','-v','error','-show_format','-show_streams','-of','json',str(original)]).stdout)
        channels=min(2,int(probe['streams'][0]['channels']));source_duration=float(probe['format']['duration'])
        filters=('highpass=f=65:p=2' if c['role']=='launch' else 'highpass=f=80:p=2')+',lowpass=f=6000:p=2'
        raw=run(['ffmpeg','-v','error','-i',str(original),'-ac',str(channels),'-ar',str(RATE),'-af',filters,'-f','f32le','pipe:1']).stdout
        data=np.frombuffer(raw,dtype='<f4').reshape(-1,channels).copy()
        blocks=np.array([np.mean(data[k:k+960]**2) for k in range(0,len(data),960)])
        active=np.where(blocks>blocks.max()*10**(-35/10))[0]
        start=max(0,active[0]*.02-.005)
        cap=1.5 if c['role']=='launch' else 2.0
        end=min(len(data)/RATE,(active[-1]+1)*.02+.04,start+cap)
        data=data[round(start*RATE):round(end*RATE)]
        fade_in=min(round(.002*RATE),len(data)//4);fade_out=min(round(.06*RATE),len(data)//4)
        data[:fade_in]*=np.linspace(0,1,fade_in)[:,None]
        data[-fade_out:]*=np.linspace(1,0,fade_out)[:,None]
        before=measure(sequence(data),channels)
        gain=min(-27-before['lufs'],-10-before['truePeakDb'])
        assert math.isfinite(gain)
        data*=10**(gain/20)
        after=measure(sequence(data),channels)
        assert after['truePeakDb']<=-9.9
        preview=folder/'processed/audition.wav'
        run(['ffmpeg','-v','error','-y','-f','f32le','-ar',str(RATE),'-ac',str(channels),'-i','pipe:0','-c:a','pcm_s24le',str(preview)],data.astype('<f4').tobytes())
        preparation=dict(sourceStart=start,sourceDuration=source_duration,selectedDuration=end-start,duration=len(data)/RATE,
                         filters=filters,fadeInSeconds=.002,fadeOutSeconds=fade_out/RATE,gainDb=gain,
                         before=before,after=after,loudnessMethod='8 identical events at 1 Hz in a 10-second reference; constant gain, no limiter',
                         sampleRate=RATE,channels=channels,format='PCM24 WAV',synthesis=False)
        (folder/'processed/preparation.json').write_text(json.dumps(preparation,indent=2),encoding='utf8')
        for path in c['evidence']:shutil.copyfile(path,evidence/path.name)
        processing=(f"Оригинал сохранён без изменений. Превью: {start:.3f}–{end:.3f} с; {filters}; PCM24/48 kHz; края 2/{fade_out/RATE*1000:g} ms. "
                    f"Постоянное усиление {gain:+.2f} dB; {after['lufs']:.2f} LUFS / {after['truePeakDb']:.2f} dBTP на эталоне 8 событий с частотой 1/с. "
                    'Без новых слоёв, изменения высоты/темпа и компрессии. Техническая проверка не заменяет оценку на слух.')
        (evidence/'license.txt').write_text(c['licenseName']+'\n'+c['licenseUrl']+'\n'+c['conditions'],encoding='utf8')
        (evidence/'attribution.txt').write_text(c['source']['recordingTitle']+' — '+c['source']['author']+'\n'+c['source']['url']+'\n'+c['licenseName']+': '+c['licenseUrl']+'\n'+c['conditions']+'\nИзменения: '+processing,encoding='utf8')
        role=c['role'];record=dict(id=sid,objectId='rocket-'+role,title=c['title'],
            description=c['character']+'. Оригинал доступен для сравнения; пригодность частого повторения оцените в игровом миксе.',
            tags=['Rocket Launch' if role=='launch' else 'Rocket Explosion','Game SFX','Stylized / Sci-Fi'],
            file=preview.relative_to(DIST).as_posix(),originalFile=original.relative_to(DIST).as_posix(),
            sha256=sha(preview),originalSha256=sha(original),originalFilename=c['source']['recordingTitle'],
            duration=len(data)/RATE,downloadedAt=DATE,source=c['source'],processing=processing,preparation=preparation,
            license=dict(id=c['licenseId'],name=c['licenseName'],url=c['licenseUrl'],commercialUse=True,
                         attribution=c['conditions'],verifiedAt=DATE,evidenceFile=f'licenses/{sid}/license.txt',attributionFile=f'licenses/{sid}/attribution.txt'))
        shortlist={'steampunk-rocket':1,'michel-rocket':2,'vehicle-blastoff':3,'pure-scifi-fast':4,'soft-forceful':5,
                   'future-launcher-hit':1,'michel-mini':2,'combat-drone':3,'magic-short':4,'scifi-big-blast':5}
        if c['slug'] in shortlist:record['shortlist']=dict(rank=shortlist[c['slug']],reason='Предварительный кандидат по назначению исходника и компактной огибающей. Комфорт и совместимость с музыкой требуют прослушивания в миксе.')
        records.append(record)
        (evidence/'source.txt').write_text(json.dumps(record,ensure_ascii=False,indent=2),encoding='utf8')
        print(sid,round(len(data)/RATE,3),after,flush=True)
    categories=[dict(id='rocket-'+r,name='Rocket '+r.capitalize()) for r in ('launch','explosion')]
    objects=[dict(id=c['id'],categoryId=c['id'],name=c['name'],description='Готовые игровые SFX разных авторов. Оригиналы сохранены; превью выровнены по громкости. Пятёрка отмечена отдельно от избранного.') for c in categories]
    result=gate.store.import_records(records,categories,objects)
    (WORK/'import-result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps(result),flush=True)


if __name__=='__main__':main()
