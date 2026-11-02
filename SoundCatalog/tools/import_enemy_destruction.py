"""Import unmodified short CC0 enemy-destruction candidates through CatalogStore.

The original archives were already downloaded from their official source pages into
.local/research.  This script never rewrites catalog.js directly and deliberately
uses one file per sound design group rather than sibling variations.
"""
from __future__ import annotations

import hashlib
import json
import shutil
import subprocess
import sys
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from audio_identity import ImportGate


SOURCES = ROOT / ".local" / "research" / "enemy-destruction" / "sources"
CATEGORY = {"id": "enemy-destruction", "name": "Enemy Destruction"}
OBJECT = {
    "id": "enemy-destruction",
    "categoryId": "enemy-destruction",
    "name": "Обычный противник — уничтожение",
    "description": "Короткие sci-fi/arcade кандидаты для частого уничтожения небольших врагов.",
}
CC0_URL = "https://creativecommons.org/publicdomain/zero/1.0/"

PACKS = {
    "kenney": {
        "name": "Kenney · Sci-fi Sounds",
        "author": "Kenney",
        "url": "https://kenney.nl/assets/sci-fi-sounds",
        "download": "https://kenney.nl/media/pages/assets/sci-fi-sounds/6b296f9ecf-1677589334/kenney_sci-fi-sounds.zip",
        "member_prefix": "Audio/",
    },
    "rubberduck50": {
        "name": "OpenGameArt · 50 CC0 Sci-Fi SFX",
        "author": "rubberduck",
        "url": "https://opengameart.org/content/50-cc0-sci-fi-sfx",
        "download": "https://opengameart.org/sites/default/files/sci-fi-sfx.zip",
        "member_prefix": "",
    },
    "rubberduck60": {
        "name": "OpenGameArt · 60 CC0 Sci-Fi SFX",
        "author": "rubberduck",
        "url": "https://opengameart.org/content/60-cc0-sci-fi-sfx",
        "download": "https://opengameart.org/sites/default/files/60-sci-fi-sfx_0.zip",
        "member_prefix": "",
    },
}

# The opaque Rubberduck pack IDs are separate authored designs.  One representative
# per group is intentionally selected; sfx_02 is excluded because sfx_02a is Removed.
CANDIDATES = [
    ("ed-kenney-metal-002", "kenney", "impactMetal_002.ogg", "Металлический энергетический распад", 0),
    ("ed-kenney-metal-004", "kenney", "impactMetal_004.ogg", "Короткий sci-fi металлический импульс", 0),
    ("ed-rd50-misc-01", "rubberduck50", "misc_01.ogg", "Электронный импульс · 01", 0),
    ("ed-rd50-misc-02", "rubberduck50", "misc_02.ogg", "Электронный импульс · 02", 0),
    ("ed-rd50-misc-05", "rubberduck50", "misc_05.ogg", "Электронный импульс · 05", 0),
    ("ed-rd50-misc-07", "rubberduck50", "misc_07.ogg", "Электронный импульс · 07", 0),
    ("ed-rd50-misc-08", "rubberduck50", "misc_08.ogg", "Электронный импульс · 08", 0),
    ("ed-rd60-03", "rubberduck60", "sfx_03a.ogg", "Sci-fi impact · 03", 0),
    ("ed-rd60-04", "rubberduck60", "sfx_04a.ogg", "Sci-fi impact · 04", 0),
    ("ed-rd60-05", "rubberduck60", "sfx_05a.ogg", "Sci-fi impact · 05", 0),
    ("ed-rd60-07", "rubberduck60", "sfx_07a.ogg", "Sci-fi impact · 07", 0),
    ("ed-rd60-10", "rubberduck60", "sfx_10a.ogg", "Sci-fi impact · 10", 0),
    ("ed-rd60-12", "rubberduck60", "sfx_12a.ogg", "Sci-fi impact · 12", 0),
    ("ed-rd60-13", "rubberduck60", "sfx_13c.ogg", "Sci-fi impact · 13", 0),
    ("ed-rd60-14", "rubberduck60", "sfx_14a.ogg", "Sci-fi impact · 14", 0),
    ("ed-rd60-15", "rubberduck60", "sfx_15a.ogg", "Sci-fi impact · 15", 0),
    ("ed-rd60-17", "rubberduck60", "sfx_17a.ogg", "Sci-fi impact · 17", 0),
    ("ed-rd60-20", "rubberduck60", "sfx_20a.ogg", "Sci-fi impact · 20", 0),
    ("ed-rd60-21", "rubberduck60", "sfx_21a.ogg", "Sci-fi impact · 21", 0),
    ("ed-rd60-22", "rubberduck60", "sfx_22a.ogg", "Sci-fi impact · 22", 0),
]


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def duration(path: Path) -> float:
    completed = subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "json", str(path)],
        check=True, capture_output=True, text=True,
    )
    return round(float(json.loads(completed.stdout)["format"]["duration"]), 3)


def evidence(sound_id: str, pack: dict, member: str, source: Path, seconds: float) -> tuple[str, str]:
    folder = ROOT / "dist" / "licenses" / sound_id
    folder.mkdir(parents=True, exist_ok=True)
    source_file = folder / "source.txt"
    license_file = folder / "license.txt"
    source_file.write_text(
        f"Author: {pack['author']}\nLibrary: {pack['name']}\nPage: {pack['url']}\n"
        f"Download: {pack['download']}\nArchive member: {member}\n"
        f"Original SHA-256: {digest(source)}\nDuration: {seconds:.3f} s\n"
        "Imported unchanged; no gain, trimming, pitch, or time processing.\n",
        encoding="utf-8",
    )
    license_file.write_text(
        f"{pack['name']}\nAuthor: {pack['author']}\nSource page: {pack['url']}\n"
        f"Verified: {date.today().isoformat()}\nLicense: CC0 1.0 Universal\n"
        f"License URL: {CC0_URL}\nCommercial use: permitted.\n"
        "Attribution: not required; author/source retained in catalogue metadata.\n",
        encoding="utf-8",
    )
    return source_file.relative_to(ROOT / "dist").as_posix(), license_file.relative_to(ROOT / "dist").as_posix()


def main() -> None:
    gate = ImportGate(ROOT)
    records = []
    for rank, (sound_id, pack_id, filename, title, _) in enumerate(CANDIDATES, start=1):
        pack = PACKS[pack_id]
        source = SOURCES / filename
        if not source.is_file():
            raise FileNotFoundError(source)
        seconds = duration(source)
        if not .15 <= seconds <= .6:
            raise ValueError(f"Duration outside requested range for {sound_id}: {seconds}")
        member = pack["member_prefix"] + filename
        source_info = {"url": pack["url"], "downloadUrl": pack["download"], "archiveMember": member,
                       "author": pack["author"], "title": title}
        if not gate.allow(sound_id, source, source_info):
            continue
        relative = f"audio/enemy-destruction/enemy-destruction/{sound_id}/originals/source.ogg"
        destination = ROOT / "dist" / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        if not destination.exists():
            shutil.copy2(source, destination)
        source_file, license_file = evidence(sound_id, pack, member, source, seconds)
        record = {
            "id": sound_id, "objectId": OBJECT["id"], "title": title,
            "description": (f"Необработанный CC0-кандидат для частого уничтожения малого врага; "
                            f"длительность {seconds:.3f} с. Требует художественного прослушивания."),
            "tags": ["enemy-destruction", "impact", "sci-fi", "arcade", "frequent-shot", "unaltered"],
            "file": relative, "originalFile": relative, "originalFilename": member,
            "downloadedAt": date.today().isoformat(), "sha256": digest(destination), "originalSha256": digest(source),
            "source": {"name": pack["name"], "url": pack["url"], "author": pack["author"],
                       "downloadUrl": pack["download"], "archiveMember": member},
            "license": {"id": "CC0", "name": "CC0 1.0 Universal", "url": CC0_URL,
                        "commercialUse": True, "attribution": "Атрибуция не требуется (CC0 1.0).",
                        "verifiedAt": date.today().isoformat(), "evidenceFile": license_file},
            "evidenceFile": source_file,
        }
        if rank <= 5:
            record["shortlist"] = {
                "rank": rank,
                "reason": "Короткая длительность и отдельный исходный дизайн; до финального выбора прослушайте в частом наложении.",
            }
        records.append(record)
    result = gate.store.import_records(records, [CATEGORY], [OBJECT])
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
