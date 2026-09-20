# Wiring

## Pin map

| Function | GPIO | XIAO label | Why this pin |
|---|---|---|---|
| Servo signal | 3 | D1 | Safe general-purpose, LEDC-capable |
| Battery sense | 4 | D2 / A2 | ADC1 — ADC2 stops working when Wi-Fi is on |
| Mode button | 5 | D3 | Deep-sleep wake requires GPIO0–5 |
| Servo power enable | 6 | D4 | Plain output, no constraints |
| Status LED | 10 | D10 | Plain output |

**Pins deliberately avoided:**

- **GPIO0, GPIO1** — not broken out on the XIAO at all.
- **GPIO2 (D0)** — strapping pin, must be HIGH or floating at reset. Seeed's own
  battery-sense sample code uses A0/GPIO2, and that's a trap: a divider or button
  there can block boot and uploads. The Seeed forum author who published that
  snippet later recommended A2/GPIO4 instead, which is what this build uses.
- **GPIO8** — strapping. **GPIO9** — the BOOT button.
- **GPIO18/19** — USB D-/D+. Using them kills serial upload.
- **GPIO12–17** — SPI flash. Never touch on a module with integrated flash.

## Power architecture

```
  LiPo 1S (3.7V, 250-400mAh, WITH protection board)
    │
    ├─── XIAO BAT+ / BAT- pads ──> onboard regulator ──> 3V3 (logic)
    │
    ├─── 220k ──┬── 220k ── GND          battery divider
    │           └──────────── GPIO4 (A2)
    │
    └─── P-MOSFET load switch ──┬── servo V+ (red)
           (source = BAT+)      │
                                └── 470uF electrolytic ── GND
              gate ── 100k pullup to BAT+
                   └── 10k ── GPIO6  (LOW = servo ON)

  Servo signal (orange) ── GPIO3
  Servo ground (brown) ─── GND  ← MUST be common with the ESP32 ground

  Button: GPIO5 ── button ── GND   (+ optional 100k to 3V3, see below)
  LED:    GPIO10 ── 330R ── LED ── GND
```

The internal pull-up is re-applied automatically during deep sleep
(`ESP_SLEEP_GPIO_ENABLE_INTERNAL_RESISTORS` is on by default), so the button
works with no external resistor. Espressif still recommends an external pull-up
for level-triggered wake — a 100 kΩ from GPIO5 to 3V3 is cheap insurance against
spurious wakes from noise on a long button lead.

### Why the servo runs off the battery, not 3V3

An SG90 draws ~100–250 mA moving and **~700 mA stalled**. The XIAO's regulator
can't supply that. Feed the servo from 3V3 and you get brownouts and random
reboots mid-flap — the classic failure in this kind of build, and it looks like a
firmware bug when it isn't.

So: servo V+ comes straight off the LiPo through a load switch. Two benefits
beyond current headroom — the servo is fully unpowered in deep sleep (no idle
jitter, no ~6 mA quiescent drain), and it can't buzz while the wings sit still.

A 3.7 V LiPo underdrives a nominally-5 V servo. SG90s run fine at 3.7 V with
slightly less torque and speed, which is what you want here anyway — these wings
weigh almost nothing. Don't add a boost converter; the noise isn't worth it.

### The 470 µF capacitor is not optional

Servo inrush drags the shared rail down hard enough to reset the ESP32 even when
the battery can supply the average current. Put the cap physically close to the
servo connector, observe polarity. 220 µF works; 470 µF is better.

### Load switch

P-channel MOSFET (AO3401, DMG2305UX, or any P-FET rated ≥1 A with low Vgs(th)):

- **Source** → BAT+
- **Drain** → servo V+
- **Gate** → 100 kΩ up to BAT+, and 10 kΩ down to GPIO6

GPIO6 LOW pulls the gate low → FET conducts → servo powered. That's why
`servoPower()` writes `LOW` to enable. The 100 kΩ pullup guarantees the servo
stays **off** while the ESP32 is in reset or deep sleep, when GPIO6 floats.

## Battery sensing

Two matched 220 kΩ resistors give a 1:2 divider and ~8 µA of quiescent drain.
The firmware:

- uses `analogReadMilliVolts()`, not raw `analogRead()` — it applies the factory
  eFuse calibration. Scaling `analogRead()` by 3.3/4095 is simply wrong: full
  scale at default attenuation is ~2500 mV ±10% part-to-part, and the response is
  non-linear near the top of the range.
- averages 16 samples, to suppress the spikes Wi-Fi/BLE transmits put on the ADC.
- cuts the servo below **3.40 V** with 0.15 V of hysteresis so it doesn't chatter
  at the threshold.

The XIAO ESP32-C3 has **no onboard battery sensing** — the BAT pads connect to
nothing but the regulator. The external divider is required, not an upgrade.

Use a LiPo **with a protection circuit**. A 1S cell taken below ~3.0 V is damaged
permanently, and the firmware cutoff is a convenience, not a safety device.

## Magnet polarity — check before you glue

Assemble the magnet stack by hand and confirm every wing attracts before any
adhesive comes out.

1. Press the four magnets into the **hinge block** pockets. Note which pole faces
   out — mark it with a sharpie dot.
2. For the **wing mount** plate, the facing magnets must be the **opposite** pole.
3. Offer the plate up to the block. It should pull in firmly and self-centre onto
   the keying pip.
4. Only now apply glue.

A single reversed magnet makes that wing spring off. Once epoxy cures you're
reprinting the part, so this two-minute check is worth it.

Use **thin CA** or slow epoxy. Press magnets in with a flat object, not a
fingertip — N52 discs pinch hard. The pockets are sized with 0.15 mm clearance
per side for a press fit; if your printer runs tight, raise *Pocket clearance* in
the FeatureScript rather than forcing them (you will crack the plate).

## Assembly order

1. Print all parts. Check the hinge/mount plates mate before wiring anything.
2. Press and glue magnets — polarity checked as above.
3. Screw the servo into the chassis cradle with its own flange screws.
4. Pin the rockers to the chassis posts with snipped 1.75 mm filament; melt the
   ends flat with a soldering iron tip to capture them.
5. Link servo horn → pushrod → rocker. **Power up and calibrate before final
   assembly** — see `docs/CALIBRATION.md`.
6. Solder electronics, fit into the tray, route the servo cable through the
   chassis exit slot.
7. Thread the alligator clip band through the chassis slots.
8. Snap the wings on.

## First power-on

Do this **before** the linkage is connected to the wings:

1. Upload firmware with USB only, no battery.
2. Open Serial Monitor at 115200. Confirm `[boot] mode=... vbat=...`.
3. If you see `[fatal] ledcAttach failed`, the LED blinks fast — the LEDC config
   was rejected. Don't proceed; check `LEDC_RES_BITS` is 14.
4. Tap the button and confirm modes advance in the serial log.
5. Then connect the linkage and calibrate.
