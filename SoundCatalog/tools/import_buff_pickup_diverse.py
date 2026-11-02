import hashlib
import json
import shutil
import sys
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from audio_identity import ImportGate

CC0 = {
    "id": "CC0",
    "name": "CC0 1.0",
    "url": "https://creativecommons.org/publicdomain/zero/1.0/",
    "commercialUse": True,
    "attribution": "Attribution is not required.",
}
CC_BY_3 = {
    "id": "CC-BY-3.0",
    "name": "Creative Commons Attribution 3.0",
    "url": "https://creativecommons.org/licenses/by/3.0/",
    "commercialUse": True,
}
CC_BY_4 = {
    "id": "CC-BY-4.0",
    "name": "Creative Commons Attribution 4.0",
    "url": "https://creativecommons.org/licenses/by/4.0/",
    "commercialUse": True,
}


def candidate(sound_id, relative, title, author, library, page, download, license_info,
              duration, original=None, member=None, credit=None, shortlist=None):
    return {
        "id": sound_id,
        "path": ROOT / relative,
        "title": title,
        "author": author,
        "library": library,
        "page": page,
        "download": download,
        "license": license_info,
        "duration": duration,
        "original": original or Path(relative).name,
        "member": member,
        "credit": credit,
        "shortlist": shortlist,
    }


BUFF = ".local/research/buff-diverse"
COMIGO = ".local/research/enemy-diverse/untamed"

ITEMS = [
    candidate("buff-circlerun-btn", f"{BUFF}/circle-SFX_0-x/audio/Btn.wav", "Btn", "Circlerun", "OpenGameArt", "https://opengameart.org/node/17038", "https://opengameart.org/sites/default/files/SFX_0.zip", CC0, 0.500, member="SFX.rar/Btn.wav", credit="SFX by Circlerun"),
    candidate("buff-circlerun-button-15", f"{BUFF}/circle-SFX_0-x/audio/Button Sound 15.wav", "Button Sound 15", "Circlerun", "OpenGameArt", "https://opengameart.org/node/17038", "https://opengameart.org/sites/default/files/SFX_0.zip", CC0, 0.602, member="SFX.rar/Button Sound 15.wav", credit="SFX by Circlerun"),
    candidate("buff-circlerun-triplet", f"{BUFF}/circle-SFX_0-x/audio/Triplet Accept Button.wav", "Triplet Accept Button", "Circlerun", "OpenGameArt", "https://opengameart.org/node/17038", "https://opengameart.org/sites/default/files/SFX_0.zip", CC0, 0.625, member="SFX.rar/Triplet Accept Button.wav", credit="SFX by Circlerun", shortlist=(2, "Hi-tech accept triplet; compact 0.63 s envelope.")),
    candidate("buff-circlerun-warm-digital", f"{BUFF}/circle-SFX_0-x/audio/Warm Digital Accept Button.wav", "Warm Digital Accept Button", "Circlerun", "OpenGameArt", "https://opengameart.org/node/17038", "https://opengameart.org/sites/default/files/SFX_0.zip", CC0, 0.250, member="SFX.rar/Warm Digital Accept Button.wav", credit="SFX by Circlerun"),
    candidate("buff-joth-item-pickup", f"{BUFF}/joth-UISoundEffects-x/Item Pickup.mp3", "Item Pickup", "Joth", "OpenGameArt", "https://opengameart.org/content/7-assorted-sound-effects-menu-level-up", "https://opengameart.org/sites/default/files/UISoundEffects.zip", CC0, 0.679, member="Item Pickup.mp3", credit="Joth", shortlist=(5, "Purpose-labelled item pickup; 0.68 s.")),
    candidate("buff-joth-menu-confirm", f"{BUFF}/joth-UISoundEffects-x/Menu Confirm.mp3", "Menu Confirm", "Joth", "OpenGameArt", "https://opengameart.org/content/7-assorted-sound-effects-menu-level-up", "https://opengameart.org/sites/default/files/UISoundEffects.zip", CC0, 0.679, member="Menu Confirm.mp3", credit="Joth"),
    candidate("buff-virix-click-1", f"{BUFF}/virix3-UI%20SFX%20Pack%202%20sample-x/Click 1.wav", "Click 1", "David McKee / ViRiX Dreamcore", "OpenGameArt", "https://opengameart.org/content/ui-sfx-pack-2-sample", "https://opengameart.org/sites/default/files/UI%20SFX%20Pack%202%20sample.zip", CC_BY_4, 0.398, member="Click 1.wav", credit="Some of the sounds and/or music was created by David McKee (VIRiX Dreamcore)"),
    candidate("buff-virix-menu-select-1", f"{BUFF}/virix3-UI%20SFX%20Pack%202%20sample-x/Menu Select 1.wav", "Menu Select 1", "David McKee / ViRiX Dreamcore", "OpenGameArt", "https://opengameart.org/content/ui-sfx-pack-2-sample", "https://opengameart.org/sites/default/files/UI%20SFX%20Pack%202%20sample.zip", CC_BY_4, 0.442, member="Menu Select 1.wav", credit="Some of the sounds and/or music was created by David McKee (VIRiX Dreamcore)", shortlist=(1, "Bright modern UI/item design; 0.44 s.")),
    candidate("buff-michel-itemback", f"{BUFF}/michel-x/generic_sounds/itemback.wav", "Item Back", "Michel Baradari", "OpenGameArt", "https://opengameart.org/content/9-sci-fi-computer-sounds-and-beeps", "https://opengameart.org/sites/default/files/sci-fi_computer_beeps.7z", CC_BY_3, 0.630, member="generic_sounds/itemback.wav", credit="Michel Baradari"),
    candidate("buff-michel-itempick-1", f"{BUFF}/michel-x/generic_sounds/itempick1.wav", "Item Pick 1", "Michel Baradari", "OpenGameArt", "https://opengameart.org/content/9-sci-fi-computer-sounds-and-beeps", "https://opengameart.org/sites/default/files/sci-fi_computer_beeps.7z", CC_BY_3, 0.551, member="generic_sounds/itempick1.wav", credit="Michel Baradari", shortlist=(3, "Purpose-labelled sci-fi item pickup; 0.55 s.")),
    candidate("buff-freesound-kianda-328120", f"{BUFF}/freesound-kianda-hq.mp3", "PowerUp (HQ preview)", "kianda", "Freesound", "https://freesound.org/people/kianda/sounds/328120/", "https://cdn.freesound.org/previews/328/328120_5668811-hq.mp3", CC0, 0.321, original="328120_5668811-hq.mp3"),
    candidate("buff-freesound-gabriel-242501", f"{BUFF}/freesound-gabriel-hq.mp3", "Powerup / Success (HQ preview)", "GabrielAraujo", "Freesound", "https://freesound.org/people/GabrielAraujo/sounds/242501/", "https://cdn.freesound.org/previews/242/242501_4414128-hq.mp3", CC0, 0.900, original="242501_4414128-hq.mp3"),
    candidate("buff-yofrankie-life-pickup", f"{BUFF}/life-life_pickup.flac", "Life Pickup", "Blender Foundation", "OpenGameArt", "https://opengameart.org/content/life-pickup-yo-frankie", "https://opengameart.org/sites/default/files/life_pickup.flac", CC_BY_3, 0.385, original="life_pickup.flac", credit="Life Pickup (Yo Frankie!) by Blender Foundation, CC BY 3.0"),
    candidate("buff-yofrankie-item-pickup", f"{BUFF}/positive-item_pickup.flac", "Positive Item Pickup", "Blender Foundation", "OpenGameArt", "https://opengameart.org/content/positive-item-pickup-yo-frankie", "https://opengameart.org/sites/default/files/item_pickup.flac", CC_BY_3, 0.263, original="item_pickup.flac", credit="Positive Item Pickup (Yo Frankie!) by Blender Foundation, CC BY 3.0"),
    candidate("buff-springspring-pleasing-bell", f"{BUFF}/bell-pleasing-bell.wav", "Pleasing Bell", "Julie Damsgaard / Spring Spring", "OpenGameArt", "https://opengameart.org/content/pleasing-bell-sound-effect", "https://opengameart.org/sites/default/files/pleasing-bell.wav", CC0, 0.619, original="pleasing-bell.wav"),
    candidate("buff-berklee-shimmer", f"{BUFF}/shimmer-shimmer_1.flac", "Shimmer Glitter Magic", "The Berklee College of Music", "OpenGameArt", "https://opengameart.org/content/shimmer-glitter-magic", "https://opengameart.org/sites/default/files/shimmer_1.flac", CC_BY_3, 0.658, original="shimmer_1.flac", credit="Shimmer glitter magic by The Berklee College of Music, CC BY 3.0", shortlist=(4, "Shimmer/chime source explicitly tagged modern and sci-fi; 0.66 s.")),
    candidate("buff-qubodup-upshort", f"{BUFF}/up3-x/up/upshort.wav", "Up Short", "Iwan Gabovitch / qubodup", "OpenGameArt", "https://opengameart.org/content/up-3x", "https://opengameart.org/sites/default/files/up.7z", CC0, 0.808, member="up/upshort.wav"),
    candidate("buff-comigo-electrozap", f"{COMIGO}/ElectroZap_s01.wav", "Electro Zap", "Cosmo Myzrail Gorynych / CoMiGo", "itch.io (via 150 Combat Sound Effects)", "https://comigo.itch.io/bleeps-n-bloops", "https://opengameart.org/sites/default/files/150gameeffectsounds.zip", CC0, 0.337, member="s01/ElectroZap_s01.wav"),
    candidate("buff-comigo-energyzap", f"{COMIGO}/EnergyZap_s01.wav", "Energy Zap", "Cosmo Myzrail Gorynych / CoMiGo", "itch.io (via 150 Combat Sound Effects)", "https://comigo.itch.io/bleeps-n-bloops", "https://opengameart.org/sites/default/files/150gameeffectsounds.zip", CC0, 0.371, member="s01/EnergyZap_s01.wav"),
    candidate("buff-comigo-heavyzap", f"{COMIGO}/HeavyZap_s01.wav", "Heavy Zap", "Cosmo Myzrail Gorynych / CoMiGo", "itch.io (via 150 Combat Sound Effects)", "https://comigo.itch.io/bleeps-n-bloops", "https://opengameart.org/sites/default/files/150gameeffectsounds.zip", CC0, 0.735, member="s01/HeavyZap_s01.wav"),
]


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    gate = ImportGate(ROOT)
    records = []
    rejected = []
    today = str(date.today())

    for item in ITEMS:
        metadata = {
            "url": item["page"],
            "downloadUrl": item["download"],
            "author": item["author"],
            "title": item["title"],
        }
        if item["member"]:
            metadata["archiveMember"] = item["member"]
        if not gate.allow(item["id"], item["path"], metadata):
            rejected.append(item["id"])
            continue

        relative_audio = f'audio/upgrade-pickup/upgrade-pickup/{item["id"]}/originals/source{item["path"].suffix.lower()}'
        destination = ROOT / "dist" / relative_audio
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(item["path"], destination)

        evidence = ROOT / "dist" / "licenses" / item["id"]
        evidence.mkdir(parents=True, exist_ok=True)
        source_lines = [
            f'Library: {item["library"]}',
            f'Author: {item["author"]}',
            f'Page: {item["page"]}',
            f'Download: {item["download"]}',
            f'Original: {item["original"]}',
        ]
        if item["member"]:
            source_lines.append(f'Archive member: {item["member"]}')
        source_lines += [f'Duration: {item["duration"]:.3f} s', f'SHA-256: {sha256(item["path"])}']
        (evidence / "source.txt").write_text("\n".join(source_lines) + "\n", encoding="utf-8")

        attribution = item["credit"] or item["license"].get("attribution", "Attribution is not required.")
        (evidence / "license.txt").write_text(
            f'{item["license"]["name"]}\n{item["license"]["url"]}\n'
            f'Commercial game use: permitted.\nAttribution: {attribution}\nVerified: {today}\n',
            encoding="utf-8",
        )

        record = {
            "id": item["id"],
            "objectId": "upgrade-pickup",
            "title": f'{item["author"]} · {item["title"]}',
            "description": f'Buff Pickup candidate, unaltered source ({item["duration"]:.3f} s). Technical source/duration screening only; listening review required.',
            "tags": ["buff-pickup", "upgrade-pickup", "sci-fi", "modern", "unaltered"],
            "file": relative_audio,
            "originalFile": relative_audio,
            "originalFilename": item["original"],
            "downloadedAt": today,
            "sha256": sha256(destination),
            "originalSha256": sha256(item["path"]),
            "source": {
                "name": item["library"],
                "url": item["page"],
                "author": item["author"],
                "downloadUrl": item["download"],
            },
            "license": {
                **item["license"],
                "attribution": attribution,
                "verifiedAt": today,
                "evidenceFile": f'licenses/{item["id"]}/license.txt',
            },
        }
        if item["member"]:
            record["source"]["archiveMember"] = item["member"]
        if item["shortlist"]:
            rank, reason = item["shortlist"]
            record["shortlist"] = {"rank": rank, "reason": reason}
        records.append(record)

    result = gate.store.import_records(records)
    print(json.dumps({"result": result, "gateRejected": rejected}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
