# Calibration

Do this once, with the linkage connected but **before** the wings are glued on.
Wrong endpoints make the servo grind against its own travel limit and strip the
nylon gears in seconds — an SG90 will happily destroy itself trying to reach a
position the linkage physically blocks.

## The four numbers

In `firmware/butterfly_clip.ino`:

```cpp
static const uint16_t SERVO_MIN_US  = 700;   // hard floor — never commanded below
static const uint16_t SERVO_MAX_US  = 2300;  // hard ceiling
static const uint16_t WING_DOWN_US  = 1150;  // wings at rest / down
static const uint16_t WING_UP_US    = 1850;  // wings at full upstroke
```

`MIN`/`MAX` are safety clamps on the servo itself. `WING_DOWN`/`WING_UP` define
the actual stroke and are what you tune. All motion is generated between those
two, scaled by the amplitude slider, so once they're right nothing can overtravel.

## Procedure

**1. Find centre.** Flash this scratch sketch (or temporarily set both wing
values to 1500 and pick mode Off):

```cpp
void setup() {
  ledcAttach(3, 50, 14);
  ledcWrite(3, (uint32_t)1500 * 16384 / 20000);   // 1500us = centre
}
void loop() {}
```

Fit the servo horn so the rockers sit at roughly **mid-stroke** here. Horn teeth
are coarse — you may not get it exact; get it close, then trim in firmware.

**2. Walk down to the rest position.** Step the pulse down in 50 µs increments
until the wings sit where you want them at rest (slightly below horizontal looks
best). **Stop the moment you hear the servo strain or see the linkage bind.**
Back off 50 µs from wherever that happened. That's `WING_DOWN_US`.

**3. Walk up.** Same in the other direction for full upstroke. Back off 50 µs
from any binding. That's `WING_UP_US`.

**4. Set the clamps.** Put `SERVO_MIN_US` ~100 µs below your `WING_DOWN_US` and
`SERVO_MAX_US` ~100 µs above `WING_UP_US`. Margin, not travel.

**5. Re-flash and test each mode** at amplitude 100% via the web UI. Listen — a
healthy servo is near-silent at the stroke ends. Buzzing or clicking at an
endpoint means you're still 30–50 µs too far.

## Sanity limits

| Pulse | Meaning |
|---|---|
| 500 µs | Absolute minimum any hobby servo accepts |
| 1000 µs | Conventional −90° |
| 1500 µs | Centre |
| 2000 µs | Conventional +90° |
| 2500 µs | Absolute maximum |

Most SG90s only travel ~180° total and many bind before 500/2500. The defaults in
the firmware (700–2300) are already conservative; narrow them further if needed,
never widen past 500/2500.

## Symptoms

| What you observe | Cause | Fix |
|---|---|---|
| Buzzing at rest, no motion | Commanded past mechanical limit | Bring `WING_DOWN/UP` toward centre |
| Board reboots when wings move | Servo current browning out the rail | Servo must run off battery + 470 µF cap — see `WIRING.md` |
| Wings twitch at power-on | Normal — servo seeks position before the first pulse | Harmless; power gating minimises it |
| One wing lags | Linkage friction or slop | Check rocker pin holes aren't over-tight; ream with a 1.8 mm bit |
| Grinding, then free movement | **Gears already stripped** | Replace servo, recalibrate before reassembly |
| Motion is jerky, not smooth | Speed set very high on a heavy wing | Lower speed, or reduce amplitude |

## Tuning feel

Via the web UI (no reflashing):

- **Speed** 0.25×–3.0×. Wings much larger than ~70 mm want 0.5–0.8× — big wings
  have real inertia and fast settings just make them wobble.
- **Amplitude** 10–100%. Scales the stroke inside the calibrated endpoints, so
  lowering it is always mechanically safe.

Settings persist to flash and survive deep sleep.

## If the servo strains regardless of endpoints

The linkage geometry is wrong, not the firmware. In the FeatureScript, reduce
*Flap angle* or increase *Rocker arm length* — a longer rocker means less angular
travel at the wing for the same horn arc, which is gentler on the gears and looks
more graceful anyway.
