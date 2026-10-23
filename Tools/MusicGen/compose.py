"""
Composes and renders the Shards of Eternity soundtrack, written note by note in this file.

    python3 Tools/MusicGen/compose.py           # every track
    python3 Tools/MusicGen/compose.py boss camp # some of them

Needs numpy and ffmpeg (with libvorbis). Each track loops seamlessly: its reverb tail is folded back onto its start.
Output goes to Assets/ShardsOfEternity/Resources/Music as .ogg, with a .meta that streams it.
"""
import hashlib
import os
import subprocess
import sys
import wave

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from synth import (SR, Mix, anvil, bell, choir, drum, flute, frame_drum, organ, pluck, reverb_ir, saw_stack,  # noqa: E402
                   shaker, wind, lowpassed_noise)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "ShardsOfEternity", "Resources", "Music")

# Note names to MIDI numbers, e.g. N("D4") = 62, N("Bb3") = 58.
_NAMES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def N(name):
    letter, rest = name[0], name[1:]
    acc = 0
    while rest and rest[0] in "#b":
        acc += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 12 * (int(rest) + 1) + _NAMES[letter] + acc


def chord(root, kind="m"):
    """Root position triad as MIDI notes from a root name, e.g. chord('D3', 'm')."""
    r = N(root)
    third = 3 if kind == "m" else 4
    fifth = 6 if kind == "dim" else 7
    return [r, r + third, r + fifth]


def mel(text):
    """A melody as 'note:beats' tokens, '-' for a rest: 'A4:2 D5:1 E5:1 -:1'."""
    out = []
    for tok in text.split():
        name, beats = tok.split(":")
        out.append((None if name == "-" else N(name), float(beats)))
    return out


class Track:
    def __init__(self, name, bpm, bars, beats=4, tail=7.0):
        self.name = name
        self.spb = 60.0 / bpm
        self.beats = beats
        self.bars = bars
        self.mix = Mix(self.at(bars), tail=tail)

    def at(self, bar, beat=0.0):
        return (bar * self.beats + beat) * self.spb

    def play_melody(self, stem, start_bar, notes, voice, **kw):
        t = start_bar * self.beats
        for m, beats in notes:
            if m is not None:
                sig = voice(m, beats * self.spb, **kw)
                self.mix.add(stem, sig, t * self.spb)
            t += beats
        return t / self.beats


def write_ogg(name, audio):
    os.makedirs(OUT, exist_ok=True)
    wav = os.path.join(OUT, name + ".tmp.wav")
    pcm = (np.clip(audio.T, -1, 1) * 32767).astype("<i2")
    with wave.open(wav, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    ogg = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", wav, "-c:a", "libvorbis", "-q:a", "4",
                    "-map_metadata", "-1", "-fflags", "+bitexact", "-flags:a", "+bitexact", ogg], check=True)
    os.remove(wav)
    meta = ogg + ".meta"
    guid = hashlib.md5(("shards-of-eternity/Music/" + name).encode()).hexdigest()
    with open(meta, "w", newline="\n") as f:
        f.write(META.format(guid=guid))
    if not os.path.exists(OUT + ".meta"):
        with open(OUT + ".meta", "w", newline="\n") as f:
            f.write(FOLDER_META.format(guid=hashlib.md5(b"shards-of-eternity/Resources/Music").hexdigest()))
    print(f"{name:10s} {audio.shape[1] / SR:6.1f}s  {os.path.getsize(ogg) / 1e6:.2f} MB")


META = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 2
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.7
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 1
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def pad_chords(tr, stem, prog, voice, start=0, octave_shift=0, bars_per_chord=1, **kw):
    for i, ch in enumerate(prog):
        bar = start + i * bars_per_chord
        for k, m in enumerate(ch):
            sig = voice(m + octave_shift, tr.at(bars_per_chord) * 1.02, seed=bar * 7 + k, **kw)
            tr.mix.add(stem, sig, tr.at(bar), pan=(k - 1) * 0.35)


# =====================================================================================================  title

def title():
    """Shards of Eternity: a heaven-and-hell hymn in D minor. Choir above, brass below, bells between."""
    tr = Track("title", bpm=70, bars=32)
    m = tr.mix
    m.stem("strings", reverb=0.45, highcut=2200, width=0.012)
    m.stem("choir", reverb=0.6, highcut=5000, lowcut=180, width=0.015)
    m.stem("lowbrass", reverb=0.35, highcut=900, gain=0.9)
    m.stem("lead", reverb=0.5, highcut=3500, gain=0.75, pan=0.1)
    m.stem("bells", reverb=0.7, gain=0.55, pan=-0.2)
    m.stem("drums", reverb=0.35, gain=0.9, highcut=4000)
    A = [chord("D3"), chord("Bb2", "M"), chord("F3", "M"), chord("C3", "M"),
         chord("D3"), chord("G2"), chord("Bb2", "M"), chord("A2", "M")]
    prog = A * 4
    pad_chords(tr, "strings", prog, saw_stack, vel=0.3, attack=1.2, release=2.0)
    # The choir enters in the second half of the first phrase and stays.
    pad_chords(tr, "choir", prog[4:], choir, start=4, octave_shift=12, vel=0.32, vowel="o", attack=1.0, release=2.5)
    for i, ch in enumerate(prog):
        if i >= 16:
            for k, n in enumerate(ch[:1]):
                m.add("lowbrass", saw_stack(n - 12, tr.at(1), vel=0.35, voices=3, attack=0.25, release=1.0, seed=i), tr.at(i))
                m.add("lowbrass", saw_stack(n - 5, tr.at(1), vel=0.22, voices=3, attack=0.25, release=1.0, seed=i + 50), tr.at(i))
    melody = mel("A4:2 D5:1 E5:1  F5:3 E5:0.5 D5:0.5  C5:2 A4:1 C5:1  E5:2 D5:1 C5:1 "
                 "D5:1.5 E5:0.5 F5:1 A5:1  G5:2 F5:1 D5:1  F5:1.5 E5:0.5 D5:1 Bb4:1  C#5:3 E5:1")
    tr.play_melody("lead", 8, melody, choir, vel=0.55, vowel="a", voices=3, attack=0.15, release=0.8)
    high = [(n + 12 if n else None, b) for n, b in melody]
    tr.play_melody("lead", 16, high, saw_stack, vel=0.32, voices=4, attack=0.12, release=0.7)
    tr.play_melody("lead", 16, melody, choir, vel=0.4, vowel="a", voices=4, attack=0.12, release=0.8)
    coda = mel("A4:4  F4:4  A4:2 C5:2  E5:4  D5:3 E5:1  Bb4:4  D5:2 F5:2  E5:4")
    tr.play_melody("lead", 24, coda, choir, vel=0.4, vowel="o", voices=3, attack=0.4, release=1.2)
    for b in range(0, 32, 2):
        m.add("bells", bell(N("D5") if b % 4 == 0 else N("A4"), vel=0.6, length=6), tr.at(b))
    for b in range(16, 24):
        m.add("drums", drum(0.9, f_hi=110, f_lo=46, seed=b), tr.at(b))
        m.add("drums", drum(0.5, f_hi=130, f_lo=60, seed=b + 100), tr.at(b, 2.5))
    # A timpani roll into the full section and into the loop point.
    for bar in (15, 31):
        for k in range(16):
            m.add("drums", drum(0.12 + 0.03 * k, f_hi=90, f_lo=55, length=0.7, seed=k + bar), tr.at(bar, k * 0.25))
    return tr


# =====================================================================================================  crypt

def crypt():
    """Ashen Crypt: cold and hollow. A drone, a distant choir, a music box and bells in the dark."""
    tr = Track("crypt", bpm=58, bars=24)
    m = tr.mix
    m.stem("drone", reverb=0.4, highcut=600, gain=0.9)
    m.stem("choir", reverb=0.75, highcut=4500, lowcut=250, width=0.02)
    m.stem("box", reverb=0.65, gain=0.5, highcut=6000, pan=0.25)
    m.stem("bells", reverb=0.8, gain=0.45, pan=-0.3)
    m.stem("heart", reverb=0.3, gain=0.8, highcut=500)
    total = tr.at(24)
    for k, (n, v) in enumerate([(N("A1"), 0.35), (N("E2"), 0.22), (N("A2"), 0.12)]):
        sig = saw_stack(n, total - 1.5, vel=v, voices=3, detune=0.004, attack=3.0, release=4.0, seed=k)
        m.add("drone", sig, 0.0)
    prog = [chord("A3"), chord("F3", "M"), chord("D3"), chord("E3", "M"), chord("A3"), chord("G3", "M"), chord("F3", "M"), chord("E3", "M")]
    for i, ch in enumerate(prog * 3):
        if i < 2:
            continue
        top = ch[2] + 12 if i % 2 else ch[1] + 12
        m.add("choir", choir(top, tr.at(1) * 0.95, vel=0.32, vowel="u", voices=4, attack=1.5, release=3.0, seed=i), tr.at(i))
    rng = np.random.default_rng(4)
    arp = [0, 2, 1, 2, 0, 2, 1, 2]
    for i, ch in enumerate(prog * 3):
        if i < 4 or 12 <= i < 14:
            continue
        for k in range(8):
            n = ch[arp[k]] + 24
            if rng.random() < 0.82:
                m.add("box", pluck(n, vel=0.35 + 0.1 * rng.random(), length=2.2, bright=0.9, seed=i * 8 + k), tr.at(i, k * 0.5))
    for b in range(0, 24, 3):
        m.add("bells", bell(N("E5") if b % 2 else N("A4"), vel=0.55, length=8, bright=0.6), tr.at(b, 1.0))
    for b in range(8, 24):
        if b % 4 != 3:
            m.add("heart", drum(0.5, f_hi=70, f_lo=40, length=0.8, noise=0.05, seed=b), tr.at(b))
            m.add("heart", drum(0.3, f_hi=70, f_lo=40, length=0.8, noise=0.05, seed=b + 40), tr.at(b, 0.45))
    return tr


# =====================================================================================================  hellforge

def hellforge():
    """Hellforge: hammering war drums, anvils and low brass in E Phrygian."""
    tr = Track("hellforge", bpm=100, bars=32)
    m = tr.mix
    m.stem("drums", reverb=0.25, gain=1.0, highcut=5000)
    m.stem("anvil", reverb=0.35, gain=0.45, pan=0.35)
    m.stem("brass", reverb=0.3, highcut=1400, gain=0.85, width=0.01)
    m.stem("drone", reverb=0.2, highcut=380, gain=0.85)
    m.stem("choir", reverb=0.5, highcut=3500, lowcut=150, gain=0.8)
    m.stem("lead", reverb=0.4, highcut=2600, gain=0.7, pan=-0.1)
    m.add("drone", saw_stack(N("E1"), tr.at(32) - 1, vel=0.4, voices=4, detune=0.01, attack=2.0, release=3.0), 0.0)
    prog = [chord("E3"), chord("F3", "M"), chord("E3"), chord("D3", "M"), chord("E3"), chord("F3", "M"), chord("G3", "M"), chord("F3", "M")]
    pattern = [(0, 1.0), (1.5, 0.6), (2, 0.9), (3, 0.5), (3.5, 0.7)]
    for bar in range(32):
        intensity = 0.7 if bar < 8 else 1.0
        for beat, v in pattern:
            m.add("drums", drum(v * intensity, f_hi=140, f_lo=52, length=1.0, noise=0.35, seed=bar * 9 + int(beat * 2)),
                  tr.at(bar, beat))
        if bar >= 4:
            for beat in (1, 3):
                m.add("anvil", anvil(0.6 if beat == 1 else 0.45, seed=bar * 2 + beat), tr.at(bar, beat))
        if bar >= 16:
            for k in range(8):
                m.add("drums", shaker(0.25 if k % 2 else 0.4, seed=bar * 8 + k), tr.at(bar, k * 0.5), pan=0.3)
    for i, ch in enumerate(prog * 4):
        if i < 8:
            continue
        for k, n in enumerate(ch):
            for beat, dur in ((0, 1.2), (2.5, 0.5)):
                sig = saw_stack(n - 12, dur * tr.spb, vel=0.32, voices=4, attack=0.03, decay=0.25, sustain=0.6, release=0.3,
                                seed=i * 10 + k)
                m.add("brass", sig, tr.at(i, beat), pan=(k - 1) * 0.4)
    for i, ch in enumerate(prog * 4):
        if i >= 16 and i % 2 == 0:
            for k, n in enumerate(ch):
                m.add("choir", choir(n, tr.at(2) * 0.9, vel=0.33, vowel="a", voices=3, attack=0.25, release=1.2, seed=i * 3 + k),
                      tr.at(i), pan=(k - 1) * 0.5)
    riff = mel("E4:1 F4:0.5 E4:0.5 G4:1 F4:1  E4:1.5 D4:0.5 E4:2  "
               "B4:1 C5:0.5 B4:0.5 A4:1 G4:1  A4:1.5 G4:0.5 F4:2")
    tr.play_melody("lead", 16, riff, saw_stack, vel=0.38, voices=4, attack=0.05, release=0.4)
    tr.play_melody("lead", 20, riff, saw_stack, vel=0.38, voices=4, attack=0.05, release=0.4)
    high = [(n + 12 if n else None, b) for n, b in riff]
    tr.play_melody("lead", 24, high, saw_stack, vel=0.3, voices=4, attack=0.05, release=0.4)
    tr.play_melody("lead", 28, riff, choir, vel=0.45, vowel="a", voices=3, attack=0.08, release=0.6)
    return tr


# =====================================================================================================  cathedral

def cathedral():
    """Shadow Cathedral: a corrupted mass. Organ, choir and bells in C minor."""
    tr = Track("cathedral", bpm=64, bars=24)
    m = tr.mix
    m.stem("organ", reverb=0.7, highcut=4500, gain=0.8, width=0.01)
    m.stem("choir", reverb=0.75, highcut=5000, lowcut=200, width=0.02)
    m.stem("bells", reverb=0.8, gain=0.5, pan=0.3)
    m.stem("solo", reverb=0.7, highcut=4000, gain=0.6, pan=-0.15)
    m.stem("timp", reverb=0.5, gain=0.7, highcut=1200)
    prog = [chord("C3"), chord("Ab2", "M"), chord("F2"), chord("G2", "M"), chord("C3"), chord("Eb3", "M"), chord("Ab2", "M"), chord("G2", "M")]
    for i, ch in enumerate(prog * 3):
        notes = [ch[0] - 12] + ch + [ch[0] + 12]
        for k, n in enumerate(notes):
            m.add("organ", organ(n, tr.at(1) * 0.98, vel=0.3, release=1.5), tr.at(i), pan=(k - 2) * 0.2)
    for i, ch in enumerate(prog * 3):
        if i >= 4:
            for k, n in enumerate(ch[1:]):
                m.add("choir", choir(n + 12, tr.at(1), vel=0.3, vowel="a" if i >= 8 else "o", voices=4, attack=0.9, release=2.0,
                                     seed=i * 5 + k), tr.at(i), pan=(k - 0.5) * 0.6)
    solo = mel("G4:2 Ab4:1 G4:1  C5:3 Bb4:1  Ab4:2 F4:1 Ab4:1  G4:4  "
               "Eb5:2 D5:1 C5:1  Bb4:2 G4:2  C5:1.5 D5:0.5 Eb5:1 C5:1  B4:4")
    tr.play_melody("solo", 8, solo, organ, vel=0.45, release=1.0, stops=(1, 0, 0.5, 0, 0.3, 0, 0.2, 0))
    tr.play_melody("solo", 16, solo, choir, vel=0.5, vowel="a", voices=3, attack=0.2, release=1.0)
    for b in range(0, 24, 2):
        m.add("bells", bell(N("C5") if b % 4 == 0 else N("G4"), vel=0.55, length=7), tr.at(b))
        m.add("bells", bell(N("Eb5"), vel=0.25, length=6), tr.at(b, 2))
    for b in (7, 15, 23):
        for k in range(12):
            m.add("timp", drum(0.1 + 0.04 * k, f_hi=80, f_lo=48, length=0.6, seed=b * 12 + k), tr.at(b, 1 + k * 0.25))
    return tr


# =====================================================================================================  wastes

def wastes():
    """Howling Wastes: wind over bone fields, frame drums, a throat-sung drone and a lonely flute."""
    tr = Track("wastes", bpm=84, bars=28)
    m = tr.mix
    m.stem("wind", reverb=0.3, gain=0.7, lowcut=120)
    m.stem("drone", reverb=0.4, highcut=2500, gain=0.85)
    m.stem("drums", reverb=0.3, gain=0.85)
    m.stem("flute", reverb=0.6, highcut=6000, gain=0.6, pan=0.15)
    m.stem("pluck", reverb=0.5, gain=0.5, pan=-0.3, highcut=5000)
    total = tr.at(28)
    n = int(total * SR) + SR
    m.add("wind", wind(n, seed=3, center=420, width=260, gust=0.06) * 0.5, 0.0, pan=-0.4)
    m.add("wind", wind(n, seed=8, center=650, width=300, gust=0.045) * 0.45, 0.0, pan=0.4)
    for bar in range(0, 28, 4):
        m.add("drone", choir(N("B1"), tr.at(4), vel=0.55, vowel="o", voices=3, attack=1.5, release=2.5, seed=bar, bright=1.4), tr.at(bar))
        m.add("drone", choir(N("F#2"), tr.at(4), vel=0.3, vowel="o", voices=2, attack=2.0, release=2.5, seed=bar + 1), tr.at(bar))
    pattern = [(0, 0.9), (1.5, 0.5), (2, 0.7), (2.75, 0.35), (3.5, 0.5)]
    for bar in range(4, 28):
        for beat, v in pattern:
            m.add("drums", frame_drum(v, seed=bar * 5 + int(beat * 4)), tr.at(bar, beat), pan=-0.15)
        if bar >= 12:
            for k in range(4):
                m.add("drums", shaker(0.2, seed=bar * 4 + k, length=0.2), tr.at(bar, k + 0.5), pan=0.35)
    tune = mel("F#4:1.5 A4:0.5 B4:2  D5:1 C#5:0.5 B4:0.5 A4:2  F#4:1 E4:1 F#4:2  -:4 "
               "B4:1.5 D5:0.5 E5:2  F#5:1 E5:0.5 D5:0.5 B4:2  A4:1 F#4:1 B4:2  -:4")
    tr.play_melody("flute", 8, tune, flute, vel=0.6)
    tr.play_melody("flute", 16, tune, flute, vel=0.65)
    arp = [N("B2"), N("F#3"), N("B3"), N("D4"), N("E4"), N("D4"), N("B3"), N("F#3")]
    for bar in range(12, 28):
        for k, nn in enumerate(arp):
            if (bar + k) % 3 != 2:
                m.add("pluck", pluck(nn, vel=0.3, length=1.8, bright=0.5, seed=bar * 8 + k), tr.at(bar, k * 0.5))
    return tr


# =====================================================================================================  boss

def boss():
    """The Rift Lord: driving taiko, racing strings, brass and choir in D minor."""
    tr = Track("boss", bpm=132, bars=32)
    m = tr.mix
    m.stem("drums", reverb=0.25, gain=1.0, highcut=6000)
    m.stem("strings", reverb=0.3, highcut=3200, gain=0.7, width=0.012)
    m.stem("brass", reverb=0.35, highcut=1600, gain=0.85)
    m.stem("choir", reverb=0.5, highcut=5000, lowcut=180, gain=0.9, width=0.015)
    m.stem("bass", reverb=0.15, highcut=300, gain=0.9)
    prog = [chord("D3"), chord("Bb2", "M"), chord("C3", "M"), chord("A2", "M"), chord("D3"), chord("Bb2", "M"), chord("G2"), chord("A2", "M")]
    full = prog * 4
    for bar in range(32):
        for k in range(8):
            accent = 1.0 if k in (0, 3, 6) else 0.45
            if bar < 4 and k % 2:
                continue
            m.add("drums", drum(0.75 * accent, f_hi=150, f_lo=55, length=0.7, noise=0.4, seed=bar * 8 + k), tr.at(bar, k * 0.5))
        if bar >= 8:
            m.add("drums", drum(0.9, f_hi=95, f_lo=40, length=1.4, noise=0.2, seed=bar + 500), tr.at(bar))
            for k in range(16):
                m.add("drums", shaker(0.18 if k % 2 else 0.3, seed=bar * 16 + k, length=0.1), tr.at(bar, k * 0.25), pan=0.3)
    for i, ch in enumerate(full):
        m.add("bass", saw_stack(ch[0] - 12, tr.at(1), vel=0.5, voices=2, attack=0.02, release=0.2, seed=i), tr.at(i))
        if i >= 4:
            ost = [ch[0] + 12, ch[1] + 12, ch[2] + 12, ch[1] + 12]
            for k in range(16):
                n = ost[k % 4] + (12 if (k // 4) % 2 and i >= 16 else 0)
                sig = saw_stack(n, 0.22 * tr.spb, vel=0.25, voices=3, attack=0.005, decay=0.08, sustain=0.4, release=0.08,
                                seed=i * 16 + k)
                m.add("strings", sig, tr.at(i, k * 0.25), pan=-0.3 if k % 2 else 0.3)
        if i >= 8:
            for k, n in enumerate(ch):
                for beat, dur in ((0, 1.5), (2, 1.5)):
                    m.add("brass", saw_stack(n, dur * tr.spb, vel=0.3, voices=4, attack=0.04, decay=0.3, sustain=0.7, release=0.3,
                                             seed=i * 30 + k), tr.at(i, beat), pan=(k - 1) * 0.4)
        if i >= 16:
            for k, n in enumerate(ch):
                m.add("choir", choir(n + 12, tr.at(1) * 0.9, vel=0.33, vowel="a", voices=3, attack=0.12, release=0.9,
                                     seed=i * 3 + k), tr.at(i), pan=(k - 1) * 0.5)
    hook = mel("D5:1.5 A4:0.5 D5:1 F5:1  E5:1.5 D5:0.5 C5:2  A4:1.5 C5:0.5 E5:1 G5:1  F5:1 E5:1 C#5:2 "
               "D5:1.5 A4:0.5 D5:1 F5:1  G5:2 F5:1 D5:1  Bb4:1.5 D5:0.5 G5:2  A5:3 -:1")
    tr.play_melody("brass", 16, hook, saw_stack, vel=0.42, voices=5, attack=0.03, release=0.4)
    tr.play_melody("choir", 24, hook, choir, vel=0.5, vowel="a", voices=4, attack=0.06, release=0.6)
    tr.play_melody("brass", 24, [(n - 12 if n else None, b) for n, b in hook], saw_stack, vel=0.35, voices=4, attack=0.03, release=0.4)
    return tr


# =====================================================================================================  camp

def camp():
    """Camp: a moment of rest by the fire. Warm pads, a harp, a gentle flute and the crackle of embers."""
    tr = Track("camp", bpm=66, bars=20)
    m = tr.mix
    m.stem("pad", reverb=0.55, highcut=1500, gain=0.75, width=0.015)
    m.stem("harp", reverb=0.55, gain=0.6, highcut=7000, pan=-0.2)
    m.stem("flute", reverb=0.6, highcut=6000, gain=0.55, pan=0.2)
    m.stem("fire", reverb=0.1, gain=0.35, highcut=9000, lowcut=300)
    prog = [chord("F3", "M"), chord("C3", "M"), chord("D3"), chord("Bb2", "M"), chord("F3", "M"), chord("A2"), chord("Bb2", "M"), chord("C3", "M")]
    full = (prog * 3)[:20]
    pad_chords(tr, "pad", full, saw_stack, vel=0.22, voices=4, attack=1.5, release=2.5, detune=0.004)
    arp = [0, 1, 2, 1, 2, 3, 2, 1]
    for i, ch in enumerate(full):
        notes = ch + [ch[0] + 12]
        for k in range(8):
            m.add("harp", pluck(notes[arp[k]] + 12, vel=0.4, length=2.4, bright=0.55, seed=i * 8 + k), tr.at(i, k * 0.5))
    tune = mel("C5:1.5 A4:0.5 F4:2  G4:1 A4:1 C5:2  D5:1.5 C5:0.5 A4:2  F4:3 -:1 "
               "A4:1.5 C5:0.5 F5:2  E5:1 D5:1 C5:2  D5:1 C5:1 A4:1 G4:1  F4:4")
    tr.play_melody("flute", 4, tune, flute, vel=0.55)
    tr.play_melody("flute", 12, tune, flute, vel=0.5)
    # Fire: a bed of soft noise and random crackles.
    rng = np.random.default_rng(12)
    n = int(tr.at(20) * SR)
    m.add("fire", lowpassed_noise(n, 900, rng) * 0.15, 0.0)
    t = 0.0
    while t < tr.at(20) - 0.2:
        t += rng.exponential(0.12)
        L = int(0.02 * SR)
        pop = rng.standard_normal(L) * np.exp(-np.arange(L) / (0.003 * SR)) * rng.uniform(0.1, 0.6)
        m.add("fire", pop, t, pan=rng.uniform(-0.6, 0.6))
    return tr


TRACKS = {"title": title, "crypt": crypt, "hellforge": hellforge, "cathedral": cathedral, "wastes": wastes,
          "boss": boss, "camp": camp}

ROOMS = {"title": (3.8, 0.5), "crypt": (5.0, 0.6), "hellforge": (2.6, 0.35), "cathedral": (6.0, 0.5),
         "wastes": (3.2, 0.4), "boss": (2.4, 0.35), "camp": (3.0, 0.6)}


def main(names):
    for name, fn in TRACKS.items():
        if names and name not in names:
            continue
        tr = fn()
        secs, damp = ROOMS[name]
        audio = tr.mix.render(reverb_ir(secs, seed=len(name), damp=damp))
        write_ogg(name, audio)


if __name__ == "__main__":
    main(set(sys.argv[1:]))
