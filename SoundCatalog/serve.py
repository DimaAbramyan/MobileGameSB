"""Offline sound catalogue. Standard library only; binds exclusively to loopback."""

import argparse
import functools
import json
import os
from pathlib import Path
import re
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import threading
import urllib.error
import urllib.request
import webbrowser

from catalog_store import CatalogStore


ROOT = Path(__file__).resolve().parent
CATALOG_ID = "sci-fi-bullet-hell-sfx"
STATE_LOCK = threading.Lock()
MAX_BODY = 2 * 1024 * 1024


def empty_state():
    return {"schemaVersion": 1, "catalogId": CATALOG_ID, "selections": {}}


def validate_state(data):
    if not isinstance(data, dict) or data.get("schemaVersion") != 1 or data.get("catalogId") != CATALOG_ID:
        raise ValueError("Unsupported catalogue or schema")
    entries = data.get("selections")
    if not isinstance(entries, dict):
        raise ValueError("Selections must be an object")
    result = empty_state()
    for sound_id, entry in entries.items():
        if not re.fullmatch(r"[a-z0-9][a-z0-9_-]{0,119}", sound_id) or not isinstance(entry, dict):
            raise ValueError("Invalid sound ID or selection")
        favorite, note, timestamp = entry.get("favorite"), entry.get("note"), entry.get("updatedAt")
        if not isinstance(favorite, bool) or not isinstance(note, str) or len(note) > 10000:
            raise ValueError("Invalid favorite or note")
        if type(timestamp) is not int or timestamp < 0 or timestamp > 9007199254740991:
            raise ValueError("Invalid timestamp")
        result["selections"][sound_id] = {"favorite": favorite, "note": note, "updatedAt": timestamp}
    return result


class CatalogServer(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, port, state_path=None, root=ROOT):
        self.root = Path(root)
        self.store = CatalogStore(self.root)
        self.store.library()  # Recover an interrupted transfer before accepting requests.
        self.state_path = Path(state_path) if state_path else self.root / "data" / "selections.json"
        self.playback_check = {"status": "not-run"}
        handler = functools.partial(CatalogHandler, directory=str(self.root / "dist"))
        super().__init__(("127.0.0.1", port), handler)

    def read_state(self):
        if not self.state_path.exists():
            return empty_state()
        return validate_state(json.loads(self.state_path.read_text(encoding="utf-8")))

    def merge_state(self, incoming):
        with STATE_LOCK:
            current = self.read_state()
            for sound_id, entry in incoming["selections"].items():
                previous = current["selections"].get(sound_id)
                if previous is None or entry["updatedAt"] > previous["updatedAt"]:
                    current["selections"][sound_id] = entry
            self.state_path.parent.mkdir(parents=True, exist_ok=True)
            temporary = self.state_path.with_suffix(".tmp")
            with temporary.open("w", encoding="utf-8", newline="\n") as output:
                json.dump(current, output, ensure_ascii=False, indent=2)
                output.flush()
                os.fsync(output.fileno())
            os.replace(temporary, self.state_path)
            return current


class CatalogHandler(SimpleHTTPRequestHandler):
    server_version = "SoundCatalog/1"
    range_remaining = None

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; media-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'")
        super().end_headers()

    def json_response(self, status, body):
        payload = json.dumps(body, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def do_GET(self):
        route = self.path.split("?", 1)[0]
        if route == "/api/health":
            self.json_response(200, {"catalogId": CATALOG_ID, "version": 2})
        elif route == "/api/library":
            try:
                self.json_response(200, self.server.store.library())
            except (ValueError, OSError) as error:
                self.json_response(500, {"error": str(error)})
        elif route == "/api/selections":
            try:
                with STATE_LOCK:
                    state = self.server.read_state()
                self.json_response(200, state)
            except (ValueError, OSError) as error:
                self.log_error("Cannot read selection file: %s", error)
                self.json_response(500, {"error": "Selection file could not be read; original preserved"})
        elif route == "/api/playback-check":
            self.json_response(200, self.server.playback_check)
        elif route.startswith("/api/"):
            self.json_response(404, {"error": "Unknown endpoint"})
        else:
            super().do_GET()

    def do_POST(self):
        if self.path not in ("/api/selections", "/api/playback-check", "/api/remove", "/api/restore"):
            self.json_response(404, {"error": "Unknown endpoint"})
            return
        expected_origin = f"http://127.0.0.1:{self.server.server_port}"
        if self.headers.get("Origin") not in (None, expected_origin):
            self.json_response(403, {"error": "Only the local catalogue may save selections"})
            return
        if self.headers.get_content_type() != "application/json":
            self.json_response(415, {"error": "Expected application/json"})
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if length <= 0 or length > MAX_BODY:
                self.json_response(413, {"error": "Invalid request size"})
                return
            body = json.loads(self.rfile.read(length).decode("utf-8"))
            if self.path in ("/api/remove", "/api/restore"):
                if not isinstance(body, dict):
                    raise ValueError("Expected sound ID")
                try:
                    library = self.server.store.change_removed(body.get('id'), self.path == '/api/remove')
                    self.json_response(200, library)
                except KeyError:
                    self.json_response(404, {"error": "Track not found"})
                except (OSError, ValueError) as error:
                    self.json_response(409, {"error": str(error)})
                return
            if self.path == "/api/playback-check":
                if not isinstance(body, dict) or body.get("status") not in ("passed", "failed"):
                    raise ValueError("Invalid playback report")
                # Only a small technical result; no browser identity or user data.
                self.server.playback_check = {
                    "status": body["status"],
                    "ended": body.get("ended") is True,
                    "muted": body.get("muted") is True,
                    "duration": body.get("duration") if isinstance(body.get("duration"), (int, float)) else None,
                    "currentTime": body.get("currentTime") if isinstance(body.get("currentTime"), (int, float)) else None,
                }
                if isinstance(body.get("files"), list):
                    results = []
                    for item in body["files"][:200]:
                        if not isinstance(item, dict) or not isinstance(item.get("id"), str):
                            raise ValueError("Invalid file check result")
                        results.append({
                            "id": item["id"][:120],
                            "kind": str(item.get("kind", ""))[:30],
                            "status": "passed" if item.get("status") == "passed" else "failed",
                            "ended": item.get("ended") is True,
                            "duration": item.get("duration") if isinstance(item.get("duration"), (int, float)) else None,
                            "currentTime": item.get("currentTime") if isinstance(item.get("currentTime"), (int, float)) else None,
                        })
                    self.server.playback_check["files"] = results
                    self.server.playback_check["total"] = len(results)
                    self.server.playback_check["passed"] = sum(item["status"] == "passed" for item in results)
                self.json_response(200, self.server.playback_check)
                return
            incoming = validate_state(body)
        except (ValueError, UnicodeError):
            self.json_response(400, {"error": "Invalid selection file"})
            return
        try:
            self.json_response(200, self.server.merge_state(incoming))
        except (ValueError, OSError) as error:
            self.log_error("Cannot save selection file: %s", error)
            self.json_response(500, {"error": "Save failed; existing selection file preserved"})

    def list_directory(self, path):
        self.send_error(404, "Directory listing is disabled")
        return None

    def send_head(self):
        self.range_remaining = None
        request_range = self.headers.get("Range")
        if not request_range:
            return super().send_head()
        path = Path(self.translate_path(self.path))
        if not path.is_file():
            return super().send_head()
        size = path.stat().st_size
        match = re.fullmatch(r"bytes=(\d*)-(\d*)", request_range)
        if not match or not any(match.groups()):
            self.send_error(400, "Unsupported byte range")
            return None
        first, last = match.groups()
        if first:
            start, end = int(first), min(int(last), size - 1) if last else size - 1
        else:
            start, end = max(0, size - int(last)), size - 1
        if start >= size or start > end:
            self.send_response(416)
            self.send_header("Content-Range", f"bytes */{size}")
            self.send_header("Content-Length", "0")
            self.end_headers()
            return None
        try:
            source = path.open("rb")
        except OSError:
            self.send_error(404, "File unavailable")
            return None
        self.send_response(206)
        self.send_header("Content-Type", self.guess_type(str(path)))
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
        self.send_header("Content-Length", str(end - start + 1))
        self.end_headers()
        source.seek(start)
        self.range_remaining = end - start + 1
        return source

    def copyfile(self, source, outputfile):
        if self.range_remaining is None:
            return super().copyfile(source, outputfile)
        remaining = self.range_remaining
        while remaining:
            block = source.read(min(64 * 1024, remaining))
            if not block:
                break
            outputfile.write(block)
            remaining -= len(block)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--open", action="store_true", help="Open catalogue in the default browser")
    options = parser.parse_args()
    url = f"http://127.0.0.1:{options.port}"
    # Reuse this catalogue if already running; never choose a different origin silently.
    try:
        # A localhost health probe must not pass through an HTTP(S) proxy from the environment.
        loopback = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        with loopback.open(f"{url}/api/health", timeout=1) as response:
            running = json.load(response)
        if running.get("catalogId") != CATALOG_ID:
            raise SystemExit("Port is occupied by another application. Use --port explicitly.")
        if options.open:
            webbrowser.open(url)
        print(f"Catalogue already running: {url}")
        return
    except (urllib.error.URLError, OSError, ValueError):
        pass
    try:
        server = CatalogServer(options.port)
    except OSError as error:
        raise SystemExit(f"Cannot start local server on {url}: {error}") from error
    print(f"Sound catalogue: {url}\nPress Ctrl+C to stop.", flush=True)
    if options.open:
        threading.Timer(0.5, webbrowser.open, args=(url,)).start()
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
