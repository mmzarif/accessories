# Swappable Magnetic Butterfly Wings — ESP32-C3

A recreation of [@resetwithregine](https://instagram.com/resetwithregine)'s flapping
butterfly hair clip, rebuilt around an ESP32-C3 instead of an Arduino UNO — so it
runs off a LiPo, fits in your hair, and has a phone-controllable web UI.

The original build's four steps, from the demo video:

1. 3D model the wings
2. Make the hinge — magnetic, so wings swap out
3. 3D print the parts
4. Assemble onto a 9 g micro servo

## What's here

| File | What it is |
|---|---|
| `firmware/butterfly_clip.ino` | ESP32-C3 firmware — 5 flap modes, web UI, battery cutoff, deep sleep |
| `cad/ButterflyClip.fs` | Onshape FeatureScript — parametric chassis, magnetic hinge, rockers, tray |
| `docs/WIRING.md` | Wiring, power gating, and the failure modes that matter |
| `docs/WINGS.md` | How to draw and print the filigree wings |
| `docs/CALIBRATION.md` | Tuning the servo endpoints so nothing strips a gear |
| `docs/BOM.md` | Parts list with sizes |

## Bill of materials (short version)

- Seeed XIAO ESP32-C3 (or any ESP32-C3 board) — **~$5**
- SG90 / AIMIKE 9 g micro servo — **~$3**. MG90S metal-gear if you want durability.
- 4 × N52 neodymium discs, 6 × 3 mm — **~$2**
- 1S LiPo, 250–400 mAh with protection circuit — **~$6**
- AO3401 P-MOSFET or equivalent load switch + 100 kΩ, 10 kΩ resistors
- 2 × 220 kΩ resistors (battery divider)
- 470 µF electrolytic (servo rail decoupling) — **don't skip this**
- Alligator hair clip, ~10 mm band
- PLA or PETG. White translucent looks closest to the original.

Full detail in `docs/BOM.md`.

## Quick start

**Firmware**

1. Arduino IDE → Preferences → Additional Boards Manager URLs:
   `https://espressif.github.io/arduino-esp32/package_esp32_index.json`
2. Boards Manager → install **esp32 by Espressif, version 3.x**
   (the code uses the core-3.x channel-less LEDC API and will not compile on 2.x)
3. Select *XIAO_ESP32C3*, open `firmware/butterfly_clip.ino`, upload.
4. No extra libraries needed — servo PWM is driven directly through LEDC.

**CAD**

1. New Onshape document → **+ → FeatureScript**, paste `cad/ButterflyClip.fs`, commit.
2. New Part Studio → **Insert custom feature** → *Butterfly Clip*.
3. Set *Part to generate* → `All parts, laid out for printing`.
4. Set your magnet diameter/thickness and servo size, OK.
5. Right-click each part → **Export** → STL.

**Wings** — sketch these yourself; see `docs/WINGS.md`. Code can't draw a wing
outline worth wearing, and the FeatureScript deliberately doesn't try.

## Controls

| Input | Action |
|---|---|
| Single tap | Next mode |
| Double tap | Previous mode |
| Hold 1.2 s | Deep sleep (~20 µA) — sleeps on release |
| Hold during power-on | Wi-Fi config portal |

The clip also sleeps itself if the battery hits the 3.40 V cutoff, rather than
sitting awake draining a cell that's already low.

Portal comes up as AP **`butterfly-wings`** / password `flutter123`, at
`http://192.168.4.1/`. Sliders for speed and amplitude, live battery readout.

## Flap modes

| Mode | Character |
|---|---|
| Slow Drift | Shallow, slow — ambient wear |
| **Flutter** | Bursts of 3–5 quick flaps, then a pause. Most lifelike; the default. |
| Excited | Fast, full range |
| Breathe | Very slow sine, like resting breath |
| Startle | Still for ~6 s, then a decaying burst |

Each stroke is eased — fast snap up, slower glide down — because a linear sweep
reads as robotic. The curves were range-checked in simulation: all stay within
[0,1] across the full 0.25×–3× speed range, so the servo is never commanded past
its mechanical limits.

## Two things worth knowing before you build

**Power the servo from the battery, not the board's 3V3.** A stalling SG90 pulls
~700 mA. Through a XIAO's regulator that browns out the ESP32 and you get random
reboots — the single most common failure in servo projects like this. Wiring in
`docs/WIRING.md` runs the servo straight off the LiPo with a gated load switch.

**Check magnet polarity before gluing.** Get one backwards and that wing repels
instead of holding. Test the stack-up by hand first, mark the faces, then glue.
This is unrecoverable once epoxy sets.

## Credit

Original concept, design, and demo by **@resetwithregine**. This is an
independent reimplementation for personal making — not affiliated with or
endorsed by the original creator. If you post a build, credit them.
