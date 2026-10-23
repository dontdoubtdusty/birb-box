# Synthesizes Birb Box sound effects into WAV files.
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, sosfiltfilt
import sys, os

RATE = 44100
OUT = sys.argv[1] if len(sys.argv) > 1 else "."
rng = np.random.default_rng(7)


def save(name, x, peak):
    x = x / np.max(np.abs(x)) * peak
    wavfile.write(os.path.join(OUT, name), RATE, (x * 32767).astype(np.int16))


def lowpass(x, hz, order=2):
    return sosfilt(butter(order, hz, "low", fs=RATE, output="sos"), x)


def bandpass(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], "band", fs=RATE, output="sos"), x)


# Evens out loudness: anything louder than threshold (as a share of the peak) is turned down,
# by more the louder it is. ratio 2.5 means 2.5x more input above the threshold gives only 1x more output.
# The loudness curve is smoothed forwards and backwards (filtfilt), so it has no delay
# and the start of each note gets turned down too, not just the middle.
def compress(x, threshold=0.5, ratio=2.5, smooth_hz=40):
    x = x / np.max(np.abs(x))
    env = sosfiltfilt(butter(2, smooth_hz, "low", fs=RATE, output="sos"), np.abs(x)) * 1.57    # 1.57 turns a sine's average into its peak
    over = np.maximum(env / threshold, 1.0)
    return x * over ** (1 / ratio - 1)


# Takes the piercing edge off a bright call: compress it, then roll off the top end steeply.
def soften(x, hz):
    return lowpass(compress(x), hz, order=4)


# A small brass disc: a few inharmonic ringing modes, higher ones die faster.
def clink(base, length, brightness=1.0, decay=1.0):
    t = np.arange(int(RATE * length)) / RATE
    modes = [(1.00, 1.00, 9.0), (2.32, 0.55, 14.0), (4.25, 0.30 * brightness, 22.0), (6.63, 0.15 * brightness, 34.0)]
    x = np.zeros_like(t)
    for ratio, amp, rate in modes:
        phase = rng.uniform(0, 2 * np.pi)
        x += amp * np.sin(2 * np.pi * base * ratio * t + phase) * np.exp(-rate * decay * t)
    attack = np.minimum(1.0, t / 0.0015)     # soften the first click
    return x * attack


def place(buffer, sound, at):
    start = int(RATE * at)
    end = min(len(buffer), start + len(sound))
    buffer[start:end] += sound[: end - start]


# Coin landing on dirt: a soft thump, the clink, then one smaller bounce.
def coin_land():
    out = np.zeros(int(RATE * 0.6))
    thump = lowpass(rng.normal(0, 1, int(RATE * 0.05)), 400) * np.exp(-np.arange(int(RATE * 0.05)) / RATE * 80)
    place(out, thump * 1.5, 0.0)
    place(out, clink(1350, 0.55, brightness=0.7), 0.002)
    place(out, clink(1350, 0.4, brightness=0.5, decay=1.6) * 0.35, 0.095)
    out = lowpass(out, 5500)
    fade = np.ones_like(out)
    fade[-2000:] = np.linspace(1, 0, 2000)
    return out * fade


# Collecting: two quick rising clinks, a little brighter than landing.
def coin_pickup():
    out = np.zeros(int(RATE * 0.7))
    place(out, clink(1500, 0.6, brightness=0.9) * 0.8, 0.0)
    place(out, clink(2250, 0.6, brightness=0.8), 0.07)
    out = lowpass(out, 7000)
    fade = np.ones_like(out)
    fade[-3000:] = np.linspace(1, 0, 3000)
    return out * fade


# The hologram hum. Every frequency fits a whole number of times into the loop,
# so the file loops without a seam. Loop is 8 s, so frequencies are multiples of 1/8 Hz.
def holo_hum():
    seconds = 8
    n = RATE * seconds
    t = np.arange(n) / RATE
    wobble = 1 + 0.12 * np.sin(2 * np.pi * 0.25 * t)            # slow swell, 2 per loop
    x = 0.60 * np.sin(2 * np.pi * 55 * t)
    x += 0.35 * np.sin(2 * np.pi * 110 * t)
    x += 0.22 * np.sin(2 * np.pi * 110.125 * t)                 # slightly detuned twin, slow beating
    x += 0.04 * np.sin(2 * np.pi * 165 * t) * (1 + 0.5 * np.sin(2 * np.pi * 0.125 * t))
    x *= wobble

    # Airy shimmer: noise built in the frequency domain, which makes it loop perfectly too
    spectrum = np.zeros(n // 2 + 1, dtype=complex)
    freqs = np.fft.rfftfreq(n, 1 / RATE)
    band = (freqs > 2500) & (freqs < 5000)
    spectrum[band] = np.exp(1j * rng.uniform(0, 2 * np.pi, band.sum()))
    air = np.fft.irfft(spectrum, n)
    air = air / np.max(np.abs(air)) * 0.008
    x += air * (1 + 0.4 * np.sin(2 * np.pi * 0.375 * t))
    return x


# ---------------- BIRDS ----------------

# One whistled note. freq is a function of 0..1 (how far through the note), so a note can slide.
# The frequency is summed up sample by sample (the phase), which keeps slides smooth.
def note(length, freq, attack=0.01, release=0.03, harmonic=0.15, vibrato=0.0, vib_rate=30.0):
    n = int(RATE * length)
    t = np.arange(n) / RATE
    f = freq(np.linspace(0, 1, n)) * (1 + vibrato * np.sin(2 * np.pi * vib_rate * t))
    phase = 2 * np.pi * np.cumsum(f) / RATE
    x = np.sin(phase) + harmonic * np.sin(2 * phase)
    env = np.minimum(1.0, t / attack) * np.minimum(1.0, (length - t) / release)
    return x * np.clip(env, 0, 1)


def slide(a, b, curve=1.0):
    return lambda p: a + (b - a) * p ** curve


def sequence(parts, tail=0.05):
    total = max(at + len(s) / RATE for at, s in parts) + tail
    out = np.zeros(int(RATE * total))
    for at, s in parts:
        place(out, s, at)
    return out


# Emberwing: clear, ringing slurs that drop in pitch, like "cheer cheer cheer".
def emberwing_1():
    parts = [(i * 0.24, note(0.17, slide(3900, 2100, 0.7), attack=0.02, harmonic=0.08)) for i in range(3)]
    return soften(sequence(parts), 4500)


# A rising "wheet wheet", then a quick low tail.
def emberwing_2():
    parts = [(0.0, note(0.22, slide(1700, 3600, 1.6), harmonic=0.1)),
             (0.30, note(0.22, slide(1700, 3600, 1.6), harmonic=0.1)),
             (0.58, note(0.12, slide(2600, 2000), harmonic=0.1) * 0.7)]
    return soften(sequence(parts), 4500)


# Breath for the dove: soft noise shaped like the note, mixed under it.
def breathy(x, amount, lo, hi):
    env = np.abs(x)
    env = lowpass(env, 30)
    return x + amount * bandpass(rng.normal(0, 1, len(x)), lo, hi) * env


# Dusk Dove: a low, soft "coo-OOO-oo" that swells in the middle note.
def duskdove_1():
    parts = [(0.0, note(0.22, slide(520, 560), attack=0.05, release=0.08, harmonic=0.25) * 0.6),
             (0.30, note(0.55, lambda p: 600 + 40 * np.sin(np.pi * p), attack=0.08, release=0.2, harmonic=0.25, vibrato=0.01, vib_rate=6)),
             (0.92, note(0.35, slide(560, 500), attack=0.06, release=0.15, harmonic=0.25) * 0.5)]
    x = sequence(parts, tail=0.1)
    return lowpass(breathy(x, 0.35, 300, 1200), 1800)


# Two short coos.
def duskdove_2():
    parts = [(0.0, note(0.3, slide(560, 610, 0.5), attack=0.06, release=0.12, harmonic=0.25)),
             (0.42, note(0.42, slide(600, 520), attack=0.06, release=0.18, harmonic=0.25) * 0.8)]
    x = sequence(parts, tail=0.1)
    return lowpass(breathy(x, 0.35, 300, 1200), 1800)


# Glintfinch: a bright, bouncy "per-chic-o-ree" of quick high chips.
def glintfinch_1():
    chips = [(0.00, 4200, 5200, 0.05), (0.08, 5600, 4600, 0.04), (0.14, 4800, 6200, 0.05), (0.22, 6000, 4300, 0.09)]
    parts = [(at, note(length, slide(a, b), attack=0.008, release=0.015, harmonic=0.05)) for at, a, b, length in chips]
    return soften(sequence(parts), 6500)


# A fast twittering run of tiny chips that wobble in pitch.
def glintfinch_2():
    parts = []
    for i in range(9):
        base = 4800 + 700 * np.sin(i * 1.3) + rng.uniform(-200, 200)
        parts.append((i * 0.045, note(0.03, slide(base, base + rng.uniform(-900, 900)), attack=0.006, release=0.01, harmonic=0.05) * rng.uniform(0.6, 1.0)))
    return soften(sequence(parts), 6500)


# A glassy ring added to each note: slightly off-pitch copies that keep ringing after the note ends.
def icy(x, freq, ring=0.18):
    t = np.arange(int(RATE * ring)) / RATE
    bell = sum(amp * np.sin(2 * np.pi * freq * ratio * t) * np.exp(-rate * t)
               for ratio, amp, rate in [(1.003, 0.35, 16), (1.502, 0.2, 22), (2.01, 0.1, 30)])
    bell *= np.minimum(1.0, t / 0.004) * np.minimum(1.0, (ring - t) / 0.03)    # no click at either end
    out = np.zeros(max(len(x), len(bell)))
    out[: len(x)] += x
    out[: len(bell)] += bell
    return out


# Frostcrest: a shimmering, icy trill with a faint bell ring on each note.
def frostcrest_1():
    parts = []
    for i in range(7):
        f = 5200 + 300 * (i % 2)
        parts.append((i * 0.06, icy(note(0.05, slide(f, f - 200), attack=0.006, release=0.02, harmonic=0.0), f, 0.12) * (1 - i * 0.07)))
    return soften(sequence(parts, tail=0.15), 7500)


# Three falling glassy notes, like icicles.
def frostcrest_2():
    parts = []
    for i, f in enumerate([6100, 5300, 4500]):
        parts.append((i * 0.16, icy(note(0.12, slide(f, f * 0.96), attack=0.006, release=0.06, harmonic=0.0, vibrato=0.004, vib_rate=40), f, 0.3)))
    return soften(sequence(parts, tail=0.15), 7500)


# One wingbeat: a soft whoosh of filtered noise.
def wingbeat(length, lo, hi):
    n = int(RATE * length)
    t = np.arange(n) / RATE
    env = np.sin(np.pi * t / length) ** 2
    return bandpass(rng.normal(0, 1, n), lo, hi) * env


# Taking off: a quick flurry of wingbeats that fades as the bird gets going.
def flap_takeoff():
    parts = [(i * 0.07, wingbeat(0.06, 500, 2600) * (1 - i * 0.13)) for i in range(6)]
    return lowpass(sequence(parts), 3500)


# Landing: two braking beats and a tiny thump on the dirt.
def flap_land():
    n = int(RATE * 0.04)
    thump = lowpass(rng.normal(0, 1, n), 300) * np.exp(-np.arange(n) / RATE * 90)
    parts = [(0.0, wingbeat(0.07, 450, 2200)), (0.08, wingbeat(0.06, 450, 2200) * 0.7), (0.15, thump * 1.2)]
    return lowpass(sequence(parts), 3500)


# A beak tapping the dirt: a tiny, dull tick.
def peck(tone):
    n = int(RATE * 0.04)
    t = np.arange(n) / RATE
    tick = bandpass(rng.normal(0, 1, n), tone * 0.6, tone * 1.6) * np.exp(-t * 180)
    knock = np.sin(2 * np.pi * tone * 0.5 * t) * np.exp(-t * 250) * 0.5
    return tick + knock


# ---------------- RADIO ----------------

# Static between stations. Built in the frequency domain like the hum's shimmer, so it loops without a seam.
# A slow swell and a few crackles keep it from sounding like flat white noise.
def radio_static():
    seconds = 4
    n = RATE * seconds
    t = np.arange(n) / RATE
    spectrum = np.zeros(n // 2 + 1, dtype=complex)
    freqs = np.fft.rfftfreq(n, 1 / RATE)
    band = (freqs > 250) & (freqs < 6000)
    tilt = 1 / np.sqrt(np.maximum(freqs[band], 1) / 250)       # darker toward the top, like an old speaker
    spectrum[band] = tilt * np.exp(1j * rng.uniform(0, 2 * np.pi, band.sum()))
    hiss = np.fft.irfft(spectrum, n)
    hiss = hiss / np.max(np.abs(hiss))
    hiss *= 1 + 0.25 * np.sin(2 * np.pi * 0.5 * t) + 0.1 * np.sin(2 * np.pi * 3.25 * t)

    # Crackles: short dull pops, kept clear of the loop's ends
    for at in rng.uniform(0.1, seconds - 0.1, 14):
        length = int(RATE * 0.012)
        pop = lowpass(rng.normal(0, 1, length), 2500) * np.exp(-np.arange(length) / RATE * 400)
        place(hiss, pop * rng.uniform(0.8, 2.0), at)
    return hiss


# A radio push button: a plastic tick as it goes down, and a low thunk as it bottoms out.
def radio_button():
    out = np.zeros(int(RATE * 0.12))
    n = int(RATE * 0.012)
    t = np.arange(n) / RATE
    tick = bandpass(rng.normal(0, 1, n), 1800, 5000) * np.exp(-t * 500)
    place(out, tick, 0.0)
    m = int(RATE * 0.08)
    t = np.arange(m) / RATE
    thunk = np.sin(2 * np.pi * 140 * t) * np.exp(-t * 60) + lowpass(rng.normal(0, 1, m), 600) * np.exp(-t * 90) * 0.6
    place(out, thunk * 0.8, 0.008)
    return out


# ---------------- PROJECTOR ----------------

# A heavy relay closing: a sharp tick and a short metal thunk.
def relay_click():
    out = np.zeros(int(RATE * 0.15))
    n = int(RATE * 0.01)
    t = np.arange(n) / RATE
    place(out, bandpass(rng.normal(0, 1, n), 1500, 6000) * np.exp(-t * 600), 0.0)
    m = int(RATE * 0.12)
    t = np.arange(m) / RATE
    thunk = np.sin(2 * np.pi * 95 * t) * np.exp(-t * 45) + lowpass(rng.normal(0, 1, m), 500) * np.exp(-t * 70) * 0.5
    place(out, thunk * 0.9, 0.006)
    return out


# A spinning-up motor and a rising electric tone. pitch goes from start to end (as a share of the hum's 55 Hz),
# and loudness follows env (a function of 0..1 through the sound).
def whir(length, start, end, env):
    n = int(RATE * length)
    p = np.linspace(0, 1, n)
    f = 55 * (start + (end - start) * (1 - (1 - p) ** 2))     # fast at first, settling at the end
    phase = 2 * np.pi * np.cumsum(f) / RATE
    motor = 0.6 * np.sin(phase) + 0.35 * np.sin(2 * phase) + 0.12 * np.sin(3 * phase)
    tone = 0.08 * np.sin(8 * phase) * p                           # a thin whine that grows as it speeds up
    air = bandpass(rng.normal(0, 1, n), 400, 2400) * 0.05
    return (motor + tone + air) * env(p)


# Switching on: the relay, then the whir rising for about three seconds and fading as the hum takes over.
def projector_start():
    out = np.zeros(int(RATE * 3.2))
    place(out, relay_click(), 0.0)
    place(out, whir(3.1, 0.3, 1.0, lambda p: np.minimum(1, p / 0.15) * (1 - p) ** 0.7) * 0.8, 0.05)
    return lowpass(out, 6000)


# Switching off: the relay, then the whir winding down.
def projector_stop():
    out = np.zeros(int(RATE * 1.3))
    place(out, relay_click(), 0.0)
    place(out, whir(1.2, 1.0, 0.25, lambda p: np.minimum(1, p / 0.05) * (1 - p) ** 1.5) * 0.8, 0.03)
    return lowpass(out, 6000)


os.makedirs(OUT, exist_ok=True)
save("coin_land.wav", coin_land(), 0.7)
save("coin_pickup.wav", coin_pickup(), 0.7)
save("holo_hum.wav", holo_hum(), 0.5)

# Bird calls are named call_<species>_<number>.wav. The game loads every number it finds,
# so a recording can replace any of these, or add a third, without changing code.
save("call_emberwing_1.wav", emberwing_1(), 0.6)
save("call_emberwing_2.wav", emberwing_2(), 0.6)
save("call_duskdove_1.wav", duskdove_1(), 0.6)
save("call_duskdove_2.wav", duskdove_2(), 0.6)
save("call_glintfinch_1.wav", glintfinch_1(), 0.55)
save("call_glintfinch_2.wav", glintfinch_2(), 0.55)
save("call_frostcrest_1.wav", frostcrest_1(), 0.5)
save("call_frostcrest_2.wav", frostcrest_2(), 0.5)
save("flap_takeoff.wav", flap_takeoff(), 0.6)
save("flap_land.wav", flap_land(), 0.6)
save("peck_1.wav", peck(2200), 0.5)
save("peck_2.wav", peck(2800), 0.5)
save("radio_static.wav", radio_static(), 0.6)
save("radio_button.wav", radio_button(), 0.6)
save("projector_start.wav", projector_start(), 0.6)
save("projector_stop.wav", projector_stop(), 0.6)
print("done")
