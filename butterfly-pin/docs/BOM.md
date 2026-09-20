# Bill of materials

Prices are rough 2026 single-unit; everything here is commodity and cheaper in
multiples.

## Electronics

| Qty | Part | Spec | ~Cost | Notes |
|---|---|---|---|---|
| 1 | Seeed XIAO ESP32-C3 | 21 × 17.5 mm | $5 | Any ESP32-C3 works; tray is sized for the XIAO. Has a LiPo charger built in. |
| 1 | Micro servo | SG90 / AIMIKE 9 g | $3 | MG90S (metal gear) if you want it to survive being knocked. Same footprint. |
| 1 | LiPo battery | 1S 3.7 V, 250–400 mAh, **with protection** | $6 | 301240 or 401030 pouch fits the tray. Protection circuit is not optional. |
| 1 | P-channel MOSFET | AO3401 / DMG2305UX, SOT-23 | $0.30 | Any P-FET ≥1 A with low Vgs(th). |
| 1 | Electrolytic capacitor | 470 µF, 6.3 V+ | $0.20 | 220 µF acceptable. **Do not omit.** |
| 2 | Resistor | 220 kΩ, 1% | $0.05 | Battery divider. Match them — 1% or better. |
| 1 | Resistor | 100 kΩ | $0.02 | MOSFET gate pullup |
| 1 | Resistor | 10 kΩ | $0.02 | Gate series |
| 1 | Resistor | 330 Ω | $0.02 | LED current limit |
| 1 | Tactile button | 6 × 6 mm through-hole | $0.10 | Mode / sleep / portal |
| 1 | Resistor | 100 kΩ | $0.02 | Button pull-up. Optional — the internal pull-up is re-applied during deep sleep — but Espressif recommends an external one for level-triggered wake. |
| 1 | LED | 3 mm, any colour | $0.10 | Optional status indicator |

## Magnets

| Qty | Part | Spec | ~Cost |
|---|---|---|---|
| 8 | Neodymium disc | **N52, 6 mm ⌀ × 3 mm** | $2 |

Four per wing side — two in the hinge block, two in the mount plate, × 2 sides.
Add more sets if you're printing multiple swappable wings (4 per extra wing pair).

N52 is worth it over N35 here; you want a firm snap from a small disc. If you
change size, update *Magnet diameter* / *Magnet thickness* in the FeatureScript —
all pocket geometry follows from those two numbers.

## Mechanical

| Qty | Part | Spec | ~Cost |
|---|---|---|---|
| 1 | Alligator hair clip | ~10 mm band, flat top | $0.50 |
| ~10 cm | PLA filament | 1.75 mm, for hinge pins | — |
| 2 | Servo screws | come with the servo | — |

## Filament

| Part | Material | Amount |
|---|---|---|
| Wings | White translucent **PETG** (or PLA+) | ~8 g |
| Mechanism | PETG or PLA+, any colour | ~15 g |

Plain PLA works for the mechanism but snaps at the wing vein junctions. See
`WINGS.md`.

## Tools

- Soldering iron, thin solder, flux
- Flush cutters
- 1.8 mm drill bit (reaming pin holes)
- Sandpaper, ~200 grit
- Thin CA glue **or** slow epoxy
- Multimeter — for verifying the divider and checking magnet polarity by feel
- 3D printer, 0.4 mm nozzle

## Total

**~$18** in parts, assuming you have filament, a printer, and solder on hand.

## Substitutions

**No load switch?** You can wire the servo directly to BAT+ and skip GPIO6 —
set `PIN_SERVO_EN` to an unused pin and the code still runs. You lose power
gating, so expect ~6 mA idle drain and occasional servo jitter at rest. Keep the
470 µF cap regardless.

**Different ESP32-C3 board?** Check your board actually breaks out GPIO3/4/5/6.
Battery sense must stay on GPIO0–4 (ADC1 — ADC2 fails with Wi-Fi active) and the
button must stay on GPIO0–5 (deep-sleep wake domain). The `static_assert`s in the
firmware will catch violations of both at compile time.

**Bigger servo?** Set *Override servo body size* in the FeatureScript and enter
your dimensions; the chassis regenerates around them. Anything past ~15 g is too
heavy to wear comfortably in hair.
