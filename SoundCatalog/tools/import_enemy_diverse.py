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


def item(sound_id, path, title, author, library, page, download, license_info,
         original_name=None, archive_member=None, duration=None, credit=None,
         shortlist=None):
    return {
        "id": sound_id,
        "path": ROOT / path,
        "title": title,
        "author": author,
        "library": library,
        "page": page,
        "download": download,
        "license": license_info,
        "original_name": original_name or Path(path).name,
        "archive_member": archive_member,
        "duration": duration,
        "credit": credit,
        "shortlist": shortlist,
    }


ITEMS = [
    item("ed-comigo-blast-impact-01", ".local/research/enemy-diverse/untamed/BlastImpact_01_s01.wav", "Blast Impact 01", "Cosmo Myzrail Gorynych / CoMiGo", "itch.io (via 150 Combat Sound Effects)", "https://comigo.itch.io/bleeps-n-bloops", "https://opengameart.org/sites/default/files/150gameeffectsounds.zip", CC0, archive_member="s01/BlastImpact_01_s01.wav", duration=0.609, shortlist=(4, "Short synthetic blast-impact profile; 0.61 s.")),
    item("ed-comigo-break-02", ".local/research/enemy-diverse/untamed/Break_02_s01.wav", "Break 02", "Cosmo Myzrail Gorynych / CoMiGo", "itch.io (via 150 Combat Sound Effects)", "https://comigo.itch.io/bleeps-n-bloops", "https://opengameart.org/sites/default/files/150gameeffectsounds.zip", CC0, archive_member="s01/Break_02_s01.wav", duration=0.221),
    item("ed-comigo-impact-01", ".local/research/enemy-diverse/untamed/Impact_01_s01.wav", "Impact 01", "Cosmo Myzrail Gorynych / CoMiGo", "itch.io (via 150 Combat Sound Effects)", "https://comigo.itch.io/bleeps-n-bloops", "https://opengameart.org/sites/default/files/150gameeffectsounds.zip", CC0, archive_member="s01/Impact_01_s01.wav", duration=0.245),
    item("ed-comigo-small-impact-01", ".local/research/enemy-diverse/untamed/SmallImpact_01_s01.wav", "Small Impact 01", "Cosmo Myzrail Gorynych / CoMiGo", "itch.io (via 150 Combat Sound Effects)", "https://comigo.itch.io/bleeps-n-bloops", "https://opengameart.org/sites/default/files/150gameeffectsounds.zip", CC0, archive_member="s01/SmallImpact_01_s01.wav", duration=0.270),
    item("ed-comigo-small-impact-03", ".local/research/enemy-diverse/untamed/SmallImpact_03_s01.wav", "Small Impact 03", "Cosmo Myzrail Gorynych / CoMiGo", "itch.io (via 150 Combat Sound Effects)", "https://comigo.itch.io/bleeps-n-bloops", "https://opengameart.org/sites/default/files/150gameeffectsounds.zip", CC0, archive_member="s01/SmallImpact_03_s01.wav", duration=0.241),
    item("ed-kronbits-electric-short-03", ".local/research/enemy-diverse/untamed/Retro Electric Short 03_s05.wav", "Retro Electric Short 03", "Kronbits", "itch.io (via 150 Combat Sound Effects)", "https://kronbits.itch.io/freesfx", "https://opengameart.org/sites/default/files/150gameeffectsounds.zip", CC0, archive_member="s05/Retro Electric Short 03_s05.wav", duration=0.566),
    item("ed-kronbits-electric-short-17", ".local/research/enemy-diverse/untamed/Retro Electric Short 17_s05.wav", "Retro Electric Short 17", "Kronbits", "itch.io (via 150 Combat Sound Effects)", "https://kronbits.itch.io/freesfx", "https://opengameart.org/sites/default/files/150gameeffectsounds.zip", CC0, archive_member="s05/Retro Electric Short 17_s05.wav", duration=0.773),
    item("ed-messersm-explosion-02", ".local/research/enemy-diverse/more/space/SpaceSFX1/explosion2.ogg", "Space Explosion 02", "messersm", "OpenGameArt", "https://opengameart.org/content/space-sound-effects", "https://opengameart.org/sites/default/files/SpaceSFX1.zip", CC0, archive_member="explosion2.ogg", duration=0.454, shortlist=(1, "Space-game explosion with a compact 0.45 s envelope.")),
    item("ed-messersm-explosion-03", ".local/research/enemy-diverse/more/space/SpaceSFX1/explosion3.ogg", "Space Explosion 03", "messersm", "OpenGameArt", "https://opengameart.org/content/space-sound-effects", "https://opengameart.org/sites/default/files/SpaceSFX1.zip", CC0, archive_member="explosion3.ogg", duration=0.536),
    item("ed-teamalpha-explosion-01", ".local/research/enemy-diverse/more/teamalpha-Explosion.wav", "8-bit/NES Explosion 01", "TeamAlpha", "OpenGameArt", "https://opengameart.org/content/8-bitnes-explosion-sound-effecs", "https://opengameart.org/sites/default/files/Explosion.wav", CC_BY_3, duration=0.294, credit="TeamAlpha"),
    item("ed-teamalpha-explosion-02", ".local/research/enemy-diverse/more/teamalpha-Explosion2.wav", "8-bit/NES Explosion 02", "TeamAlpha", "OpenGameArt", "https://opengameart.org/content/8-bitnes-explosion-sound-effecs", "https://opengameart.org/sites/default/files/Explosion2.wav", CC_BY_3, duration=0.302, credit="TeamAlpha"),
    item("ed-teamalpha-explosion-03", ".local/research/enemy-diverse/more/teamalpha-Explosion3.wav", "8-bit/NES Explosion 03", "TeamAlpha", "OpenGameArt", "https://opengameart.org/content/8-bitnes-explosion-sound-effecs", "https://opengameart.org/sites/default/files/Explosion3.wav", CC_BY_3, duration=0.426, credit="TeamAlpha", shortlist=(2, "Arcade explosion profile with a compact 0.43 s envelope.")),
    item("ed-teamalpha-explosion-06", ".local/research/enemy-diverse/more/teamalpha-Explosion6.wav", "8-bit/NES Explosion 06", "TeamAlpha", "OpenGameArt", "https://opengameart.org/content/8-bitnes-explosion-sound-effecs", "https://opengameart.org/sites/default/files/Explosion6.wav", CC_BY_3, duration=0.256, credit="TeamAlpha"),
    item("ed-lukerust-8bit-bomb", ".local/research/enemy-diverse/more/8bit_bomb_explosion.wav", "8-bit Bomb Explosion", "Luke.RUSTLTD", "OpenGameArt", "https://opengameart.org/content/bombexplosion8bit", "https://opengameart.org/sites/default/files/8bit_bomb_explosion.wav", CC0, duration=0.584, shortlist=(3, "CFXR arcade explosion; 0.58 s.")),
    item("ed-dklon-atari-boom-03", ".local/research/enemy-diverse/more/atari_boom3.wav", "Atari Boom 03", "dklon", "OpenGameArt", "https://opengameart.org/content/atari-booms", "https://opengameart.org/sites/default/files/atari_boom3.wav", CC_BY_3, duration=0.348, credit="dklon", shortlist=(5, "Short TIA-style boom; 0.35 s.")),
    item("ed-artisticdude-retro-01", ".local/research/enemy-diverse/more/retro_sound_1.wav", "Retro Sound 01", "artisticdude", "OpenGameArt", "https://opengameart.org/content/retro-sounds-0", "https://opengameart.org/sites/default/files/retro_sound_1_0.wav", CC0, original_name="retro_sound_1_0.wav", duration=0.845),
    item("ed-artisticdude-retro-02", ".local/research/enemy-diverse/more/retro_sound_2.wav", "Retro Sound 02", "artisticdude", "OpenGameArt", "https://opengameart.org/content/retro-sounds-0", "https://opengameart.org/sites/default/files/retro_sound_2.wav", CC0, duration=0.695),
    item("ed-kutejnikov-explosion", ".local/research/enemy-diverse/kutejnikov-explosion.wav", "Explosion", "Kutejnikov", "OpenGameArt", "https://opengameart.org/content/explosion-8", "https://opengameart.org/sites/default/files/explosion_1.wav", CC0, original_name="explosion_1.wav", duration=0.742),
    item("ed-springspring-splat", ".local/research/enemy-diverse/more/snd_splat.wav", "Splat", "Spring Spring / Julie Damsgaard", "OpenGameArt", "https://opengameart.org/content/various-sound-effects-0", "https://opengameart.org/sites/default/files/snd_splat.wav", CC0, duration=0.551),
    item("ed-wubitog-short-explosion", ".local/research/enemy-diverse/more/wubitog-200466-hq.mp3", "Short Explosion (HQ preview)", "wubitog", "Freesound", "https://freesound.org/people/wubitog/sounds/200466/", "https://cdn.freesound.org/previews/200/200466_3501487-hq.mp3", CC0, original_name="200466_3501487-hq.mp3", duration=0.770),
]


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    gate = ImportGate(ROOT)
    records = []
    rejected = []
    today = str(date.today())

    for candidate in ITEMS:
        source_path = candidate["path"]
        gate_metadata = {
            "url": candidate["page"],
            "downloadUrl": candidate["download"],
            "author": candidate["author"],
            "title": candidate["title"],
        }
        if candidate["archive_member"]:
            gate_metadata["archiveMember"] = candidate["archive_member"]

        decision = gate.allow(candidate["id"], source_path, gate_metadata)
        if not decision:
            rejected.append(candidate["id"])
            continue

        suffix = source_path.suffix.lower()
        relative_audio = f'audio/enemy-destruction/enemy-destruction/{candidate["id"]}/originals/source{suffix}'
        destination = ROOT / "dist" / relative_audio
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source_path, destination)

        evidence_dir = ROOT / "dist" / "licenses" / candidate["id"]
        evidence_dir.mkdir(parents=True, exist_ok=True)
        source_lines = [
            f'Library: {candidate["library"]}',
            f'Author: {candidate["author"]}',
            f'Page: {candidate["page"]}',
            f'Download: {candidate["download"]}',
            f'Original: {candidate["original_name"]}',
        ]
        if candidate["archive_member"]:
            source_lines.append(f'Archive member: {candidate["archive_member"]}')
        source_lines.extend((f'Duration: {candidate["duration"]:.3f} s', f'SHA-256: {sha256(source_path)}'))
        (evidence_dir / "source.txt").write_text("\n".join(source_lines) + "\n", encoding="utf-8")

        attribution = candidate["credit"] or candidate["license"].get("attribution", "")
        (evidence_dir / "license.txt").write_text(
            f'{candidate["license"]["name"]}\n'
            f'{candidate["license"]["url"]}\n'
            f'Commercial use: permitted.\n'
            f'Attribution: {attribution or "not required"}\n'
            f'Verified: {today}\n',
            encoding="utf-8",
        )

        record = {
            "id": candidate["id"],
            "objectId": "enemy-destruction",
            "title": f'{candidate["author"]} · {candidate["title"]}',
            "description": f'Unaltered source candidate ({candidate["duration"]:.3f} s). Technical duration/source screening only; requires listening review.',
            "tags": ["enemy-destruction", "sci-fi", "arcade", "unaltered"],
            "file": relative_audio,
            "originalFile": relative_audio,
            "originalFilename": candidate["original_name"],
            "downloadedAt": today,
            "sha256": sha256(destination),
            "originalSha256": sha256(source_path),
            "source": {
                "name": candidate["library"],
                "url": candidate["page"],
                "author": candidate["author"],
                "downloadUrl": candidate["download"],
            },
            "license": {
                **candidate["license"],
                "attribution": attribution or "Attribution is not required.",
                "verifiedAt": today,
                "evidenceFile": f'licenses/{candidate["id"]}/license.txt',
            },
        }
        if candidate["archive_member"]:
            record["source"]["archiveMember"] = candidate["archive_member"]
        if candidate["shortlist"]:
            rank, reason = candidate["shortlist"]
            record["shortlist"] = {"rank": rank, "reason": reason}
        records.append(record)

    result = gate.store.import_records(records)
    print(json.dumps({"result": result, "gateRejected": rejected}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
