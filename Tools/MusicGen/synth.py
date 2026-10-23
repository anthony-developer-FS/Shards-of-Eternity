"""
A small offline synthesizer for the Shards of Eternity soundtrack. Every sound is made from oscillators and noise:
choirs from formant-shaped harmonics, strings from detuned saws, bells from inharmonic partials, drums from pitched
sweeps and filtered noise. No samples, loops or third-party recordings are used.
"""
import numpy as np

SR = 44100


def midi_hz(m):
    return 440.0 * 2 ** ((np.asarray(m, dtype=float) - 69) / 12)


def envelope(n_on, n_total, attack, decay=0.0, sustain=1.0, release=0.3, curve=1.0):
    """Attack/decay/sustain while the note is held (n_on samples), then an exponential-ish release."""
    t = np.arange(n_total) / SR
    on = n_on / SR
    env = np.ones(n_total)
    a = max(attack, 1e-4)
    env = np.where(t < a, (t / a) ** curve, env)
    if decay > 0:
        dmask = (t >= a) & (t < on)
        env = np.where(dmask, sustain + (1 - sustain) * np.exp(-(t - a) / decay), env)
    else:
        env = np.where((t >= a) & (t < on), sustain if a < on else env, env)
    level = env[max(0, min(n_on, n_total) - 1)]
    rmask = t >= on
    env = np.where(rmask, level * np.exp(-(t - on) / max(release / 4.0, 1e-3)), env)
    return env


def vibrato_phase(f0, n, rate=5.0, depth=0.004, delay=0.25, seed=0, drift=0.0015):
    """Phase (radians) of a pitch with delayed vibrato and slow random drift, so held notes breathe."""
    t = np.arange(n) / SR
    rng = np.random.default_rng(seed)
    ramp = np.clip((t - delay) / 0.4, 0, 1)
    vib = 1 + depth * ramp * np.sin(2 * np.pi * rate * t + rng.uniform(0, 6.28))
    dr = 1 + drift * np.sin(2 * np.pi * rng.uniform(0.1, 0.3) * t + rng.uniform(0, 6.28))
    f = f0 * vib * dr
    return 2 * np.pi * np.cumsum(f) / SR


# Formant centers (Hz) and bandwidths for sung vowels.
VOWELS = {
    "a": [(800, 80, 1.0), (1150, 90, 0.5), (2900, 120, 0.12), (3900, 130, 0.06)],
    "o": [(450, 70, 1.0), (800, 80, 0.35), (2830, 100, 0.06), (3800, 120, 0.03)],
    "u": [(325, 50, 1.0), (700, 60, 0.15), (2530, 170, 0.03)],
    "e": [(400, 60, 1.0), (1620, 80, 0.25), (2400, 100, 0.2), (3200, 120, 0.06)],
}


def formant_gain(freqs, vowel):
    g = np.zeros_like(freqs, dtype=float)
    for fc, bw, amp in VOWELS[vowel]:
        g += amp / (1 + ((freqs - fc) / bw) ** 2)
    return g + 0.002


def choir(m, dur, vel=0.5, vowel="a", voices=4, attack=0.6, release=1.4, seed=0, bright=1.0):
    """A small section singing one note: several detuned voices of formant-weighted harmonics."""
    f0 = float(midi_hz(m))
    n_on = int(dur * SR)
    n = n_on + int(release * SR)
    out = np.zeros(n)
    nh = int(min(40, 5000 / f0))
    harm = np.arange(1, nh + 1)
    gains = formant_gain(harm * f0, vowel) / harm ** (0.6 / bright)
    gains /= np.sqrt(np.sum(gains ** 2)) + 1e-9
    for v in range(voices):
        det = 1 + (v - (voices - 1) / 2) * 0.0035
        ph = vibrato_phase(f0 * det, n, rate=4.6 + 0.4 * v, depth=0.005, delay=0.15 + 0.1 * v, seed=seed * 7 + v)
        sig = np.zeros(n)
        for h, g in zip(harm, gains):
            sig += g * np.sin(h * ph + v * h)
        out += sig
    # breath
    rng = np.random.default_rng(seed + 99)
    out += 0.02 * lowpassed_noise(n, 3000, rng) * voices
    env = envelope(n_on, n, attack, release=release, curve=1.6)
    return out * env * vel / voices


def saw_stack(m, dur, vel=0.5, voices=5, detune=0.006, attack=0.4, release=1.0, seed=0, decay=0.0, sustain=1.0):
    """Detuned saws: string sections, brass and pads once filtered."""
    f0 = float(midi_hz(m))
    n_on = int(dur * SR)
    n = n_on + int(release * SR)
    out = np.zeros(n)
    rng = np.random.default_rng(seed)
    for v in range(voices):
        det = 1 + (v - (voices - 1) / 2) * detune / max(voices - 1, 1) * 2
        ph = vibrato_phase(f0 * det, n, rate=5.2, depth=0.003, delay=0.3, seed=seed * 13 + v) / (2 * np.pi)
        ph += rng.uniform(0, 1)
        out += 2 * (ph % 1.0) - 1
    env = envelope(n_on, n, attack, decay=decay, sustain=sustain, release=release, curve=1.3)
    return out * env * vel / voices


def organ(m, dur, vel=0.5, attack=0.08, release=0.8, stops=(1, 0.6, 0.0, 0.35, 0.0, 0.25, 0, 0.15)):
    """Pipe organ: additive drawbars, a slow tremulant, a chiff of noise at the start."""
    f0 = float(midi_hz(m))
    n_on = int(dur * SR)
    n = n_on + int(release * SR)
    t = np.arange(n) / SR
    ph = 2 * np.pi * f0 * t
    out = np.zeros(n)
    for i, g in enumerate(stops):
        if g and f0 * (i + 1) < 9000:
            out += g * np.sin((i + 1) * ph)
    out += 0.4 * np.sin(0.5 * ph)  # 16' stop
    out *= 1 + 0.04 * np.sin(2 * np.pi * 5.5 * t)
    rng = np.random.default_rng(int(m * 10))
    chiff = lowpassed_noise(n, 4000, rng) * np.exp(-t / 0.04) * 0.25
    env = envelope(n_on, n, attack, release=release)
    return (out * 0.3 + chiff) * env * vel


def bell(m, vel=0.5, length=5.0, bright=1.0):
    """Tolling bell: inharmonic partials with their own decays and a little beating."""
    f0 = float(midi_hz(m))
    n = int(length * SR)
    t = np.arange(n) / SR
    partials = [(0.5, 1.0, 1.0), (1.0, 0.8, 0.7), (1.19, 0.5, 0.6), (1.56, 0.4, 0.45), (2.0, 0.35, 0.4),
                (2.74, 0.25 * bright, 0.25), (3.76, 0.15 * bright, 0.15), (5.4, 0.08 * bright, 0.08)]
    out = np.zeros(n)
    for ratio, amp, dec in partials:
        f = f0 * ratio
        if f > 12000:
            continue
        out += amp * np.sin(2 * np.pi * f * t) * np.exp(-t / (length * dec * 0.45)) * (1 + 0.1 * np.sin(2 * np.pi * 1.3 * t))
    out *= np.minimum(1, t / 0.002)
    return out * vel * 0.4


def pluck(m, vel=0.5, length=2.5, bright=0.6, seed=0):
    """Harp or lute: decaying harmonics, the high ones fading first."""
    f0 = float(midi_hz(m))
    n = int(length * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    rng = np.random.default_rng(seed)
    for h in range(1, int(min(24, 8000 / f0)) + 1):
        amp = (1 / h ** 1.2) * (bright ** (h / 6))
        out += amp * np.sin(2 * np.pi * f0 * h * t + rng.uniform(0, 6.28)) * np.exp(-t * (1.2 + h * 0.7))
    out *= np.minimum(1, t / 0.003)
    return out * vel * 0.5


def flute(m, dur, vel=0.5, attack=0.12, release=0.35, seed=0):
    """A breathy wooden flute: mostly fundamental, a few soft harmonics, breath noise, vibrato."""
    f0 = float(midi_hz(m))
    n_on = int(dur * SR)
    n = n_on + int(release * SR)
    ph = vibrato_phase(f0, n, rate=5.0, depth=0.008, delay=0.3, seed=seed)
    out = np.sin(ph) + 0.25 * np.sin(2 * ph) + 0.08 * np.sin(3 * ph)
    rng = np.random.default_rng(seed + 3)
    out += 0.08 * bandpassed_noise(n, f0 * 2, f0 * 1.5, rng)
    env = envelope(n_on, n, attack, release=release, curve=1.5)
    return out * env * vel * 0.5


def drum(vel=0.8, f_hi=120, f_lo=48, length=1.6, noise=0.25, seed=0, tone_decay=0.45):
    """A big skin drum (taiko or timpani): a pitch sweep plus a thump of filtered noise."""
    n = int(length * SR)
    t = np.arange(n) / SR
    f = f_lo + (f_hi - f_lo) * np.exp(-t / 0.05)
    ph = 2 * np.pi * np.cumsum(f) / SR
    body = np.sin(ph) * np.exp(-t / tone_decay) + 0.4 * np.sin(1.5 * ph) * np.exp(-t / (tone_decay * 0.5))
    rng = np.random.default_rng(seed)
    hit = lowpassed_noise(n, 1800, rng) * np.exp(-t / 0.06) * noise
    out = (body + hit) * np.minimum(1, t / 0.001)
    return out * vel


def frame_drum(vel=0.6, seed=0, length=0.6):
    n = int(length * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(seed)
    f = 170 + 80 * np.exp(-t / 0.02)
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.12)
    slap = bandpassed_noise(n, 1500, 1200, rng) * np.exp(-t / 0.03)
    return (body * 0.7 + slap * 0.5) * vel


def anvil(vel=0.5, length=1.5, seed=0):
    """A struck anvil or forge hammer: bright inharmonic ring with a hard attack."""
    n = int(length * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(seed)
    out = np.zeros(n)
    for ratio, amp, dec in [(1.0, 1, 0.5), (2.76, 0.6, 0.3), (5.4, 0.4, 0.15), (8.93, 0.25, 0.08), (13.3, 0.15, 0.05)]:
        out += amp * np.sin(2 * np.pi * 820 * ratio * t * (1 + rng.uniform(-0.01, 0.01))) * np.exp(-t / dec)
    out += bandpassed_noise(n, 3000, 2500, rng) * np.exp(-t / 0.01) * 0.8
    return out * vel * 0.3


def shaker(vel=0.3, seed=0, length=0.15):
    n = int(length * SR)
    t = np.arange(n) / SR
    rng = np.random.default_rng(seed)
    return bandpassed_noise(n, 6000, 4000, rng) * np.exp(-t / 0.035) * np.minimum(1, t / 0.008) * vel


def wind(n, seed=0, center=500, width=400, gust=0.08):
    """Howling wind: band-passed noise whose loudness and pitch wander."""
    rng = np.random.default_rng(seed)
    t = np.arange(n) / SR
    a = bandpassed_noise(n, center, width, rng)
    b = bandpassed_noise(n, center * 2.2, width, rng)
    mod = 0.55 + 0.45 * np.sin(2 * np.pi * gust * t + 1) * np.sin(2 * np.pi * gust * 0.37 * t + 2)
    mod2 = 0.5 + 0.5 * np.sin(2 * np.pi * gust * 1.7 * t)
    return a * mod + 0.5 * b * mod2 * mod


# ---------------------------------------------------------------- filters (frequency domain, whole signal)

def _fft_filter(x, gain_fn):
    n = len(x)
    size = 1 << int(np.ceil(np.log2(max(n, 2))))
    X = np.fft.rfft(x, size)
    f = np.fft.rfftfreq(size, 1 / SR)
    y = np.fft.irfft(X * gain_fn(f), size)[:n]
    return y


def lowpass(x, cutoff, order=2):
    return _fft_filter(x, lambda f: 1 / np.sqrt(1 + (f / cutoff) ** (2 * order)))


def highpass(x, cutoff, order=2):
    return _fft_filter(x, lambda f: 1 / np.sqrt(1 + (cutoff / np.maximum(f, 1e-3)) ** (2 * order)))


def lowpassed_noise(n, cutoff, rng):
    return lowpass(rng.standard_normal(n), cutoff) * 0.5


def bandpassed_noise(n, center, width, rng):
    x = rng.standard_normal(n)
    y = _fft_filter(x, lambda f: 1 / (1 + ((f - center) / max(width, 1)) ** 2))
    return y / (np.std(y) + 1e-9) * 0.3


# ---------------------------------------------------------------- reverb and mixing

def reverb_ir(seconds=3.5, seed=1, damp=0.5, predelay=0.02):
    """A stereo hall: early reflections then decaying noise whose highs die faster than its lows."""
    rng = np.random.default_rng(seed)
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = np.zeros((2, n))
    bands = [(0, 400, 1.0), (400, 2000, 0.75), (2000, 6000, 0.45 * (1 - damp) + 0.2), (6000, 20000, 0.2 * (1 - damp) + 0.05)]
    for ch in range(2):
        noise = rng.standard_normal(n)
        acc = np.zeros(n)
        for lo, hi, k in bands:
            band = _fft_filter(noise, lambda f, lo=lo, hi=hi: ((f >= lo) & (f < hi)).astype(float))
            acc += band * np.exp(-t / (seconds * 0.16 * k))
        ir[ch] = acc
        for i in range(8):
            d = int((predelay + rng.uniform(0.005, 0.08)) * SR)
            ir[ch, d] += rng.uniform(0.3, 0.7) * (1 if rng.random() > 0.5 else -1)
    pd = int(predelay * SR)
    ir = np.concatenate([np.zeros((2, pd)), ir[:, : n - pd]], axis=1)
    ir /= np.sqrt(np.sum(ir ** 2, axis=1, keepdims=True)) + 1e-9
    return ir


def convolve(x, ir):
    n = len(x) + ir.shape[1] - 1
    size = 1 << int(np.ceil(np.log2(n)))
    X = np.fft.rfft(x, size)
    out = np.zeros((2, n))
    for ch in range(2):
        out[ch] = np.fft.irfft(X * np.fft.rfft(ir[ch], size), size)[:n]
    return out


class Mix:
    """Stems on a timeline. Each stem gets its own filter, reverb send and pan, then they are summed."""

    def __init__(self, seconds, tail=6.0):
        self.length = int(seconds * SR)
        self.n = self.length + int(tail * SR)
        self.stems = {}

    def stem(self, name, pan=0.0, gain=1.0, reverb=0.3, lowcut=None, highcut=None, width=0.0):
        if name not in self.stems:
            self.stems[name] = dict(buf=np.zeros((2, self.n)), pan=pan, gain=gain, reverb=reverb, lowcut=lowcut,
                                    highcut=highcut, width=width)
        return self.stems[name]

    def add(self, name, signal, start_seconds, pan=None, gain=1.0):
        st = self.stems[name]
        s = int(start_seconds * SR)
        if s >= self.n:
            return
        sig = signal[: self.n - s]
        p = st["pan"] if pan is None else pan
        lg, rg = np.cos((p + 1) * np.pi / 4), np.sin((p + 1) * np.pi / 4)
        st["buf"][0, s: s + len(sig)] += sig * lg * gain
        st["buf"][1, s: s + len(sig)] += sig * rg * gain

    def render(self, ir, loop=True, target_rms=0.075):
        dry = np.zeros((2, self.n))
        send = np.zeros(self.n)
        for name, st in self.stems.items():
            b = st["buf"]
            for ch in range(2):
                if st["highcut"]:
                    b[ch] = lowpass(b[ch], st["highcut"])
                if st["lowcut"]:
                    b[ch] = highpass(b[ch], st["lowcut"])
            if st["width"]:
                # Haas widening: delay one side a little.
                d = int(st["width"] * SR)
                b[1] = np.concatenate([np.zeros(d), b[1, :-d]])
            dry += b * st["gain"]
            send += (b[0] + b[1]) * 0.5 * st["gain"] * st["reverb"]
        wet = convolve(send, ir)[:, : self.n]
        mix = dry + wet * 0.9
        if loop:
            # Fold everything that rings past the end back onto the start, so the track loops seamlessly.
            tail = mix[:, self.length:]
            mix = mix[:, : self.length].copy()
            k = min(tail.shape[1], self.length)
            mix[:, :k] += tail[:, :k]
        # Clear sub-bass rumble from noise and reverb.
        mix = np.stack([highpass(mix[0], 35), highpass(mix[1], 35)])
        rms = np.sqrt(np.mean(mix ** 2)) + 1e-9
        mix *= target_rms / rms
        # Gentle soft clip, then leave headroom.
        mix = np.tanh(mix * 1.1) / np.tanh(1.1)
        peak = np.max(np.abs(mix))
        if peak > 0.95:
            mix *= 0.95 / peak
        return mix
