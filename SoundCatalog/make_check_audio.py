"""Create a quiet synthetic WAV used only for playback diagnostics."""
import math
from pathlib import Path
import struct
import wave

target = Path(__file__).resolve().parent / "dist/audio/_diagnostics/playback-check.wav"
target.parent.mkdir(parents=True, exist_ok=True)
rate = 44100
samples = []
for index in range(rate):
    time = index / rate
    envelope = min(1.0, time / 0.06, (1.0 - time) / 0.15)
    signal = 0.08 * envelope * (math.sin(2 * math.pi * 440 * time) + 0.35 * math.sin(2 * math.pi * 660 * time))
    samples.append(struct.pack("<h", round(signal * 32767)))
with wave.open(str(target), "wb") as audio:
    audio.setnchannels(1)
    audio.setsampwidth(2)
    audio.setframerate(rate)
    audio.writeframes(b"".join(samples))
print(f"Created diagnostic WAV: {target.name} (1 second, mono PCM16, 44100 Hz)")
