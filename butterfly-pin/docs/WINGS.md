# The wings

The FeatureScript builds the mechanism but **not** the wing blades. That's
deliberate: a butterfly wing outline is a set of hand-tuned splines, and anything
procedural looks generated. Draw them yourself — it's the fun part, and it's where
the piece becomes yours.

In the reference build the wings are thin white filigree: an outer outline plus
interior vein ribs, with open space between. Light passes through, they weigh
almost nothing, and they read as delicate from a distance.

## Drawing them in Onshape

1. New sketch on the **Front** plane.
2. Find a butterfly silhouette for reference (monarch and swallowtail both work
   well), insert it as an image, scale so one wing root-to-tip is ~70 mm.
3. Trace the outer edge with **spline** — not line segments. 8–12 points per wing.
   Keep the scalloped trailing edge; it's what makes it read as a butterfly.
4. Offset the outline **inward by 1.2 mm** to make the rim band.
5. Draw the vein ribs as splines fanning from the wing root, each ~1.0–1.4 mm
   wide. 5–7 veins for the upper wing, 3–4 for the lower.
6. Delete the enclosed regions between veins so only rim and veins remain.
7. Extrude **1.2 mm**.
8. Mirror across the origin plane for the other side.

### Dimensions that work

| Feature | Size | Why |
|---|---|---|
| Wing span (root → tip) | 60–80 mm | Beyond ~90 mm the servo struggles and it looks costumey |
| Blade thickness | 1.2 mm | 3 layers at 0.4 mm — thinner warps, thicker kills translucency |
| Rim band width | 1.2–1.6 mm | Below 1.2 mm it snaps in hair |
| Vein width | 1.0–1.4 mm | Thin enough to read as filigree, thick enough to print |
| Bonding tab overlap | 8–10 mm | Full contact with the wing mount plate |

Keep total wing mass under ~4 g per side. An SG90 can move more, but the inertia
makes the flap sloppy and drains the battery fast.

## Printing

**Wings** — the hard part. Thin filigree at 1.2 mm is fragile in the wrong material.

| Setting | Value | Notes |
|---|---|---|
| Material | **PETG** or PLA+ | Plain PLA snaps at the vein junctions. PETG flexes instead. |
| Layer height | 0.12–0.16 mm | Finer layers matter on curved thin walls |
| Walls | 3 (or "thin wall" mode on) | Thin features must be solid wall, not sparse infill |
| Infill | 100% | At 1.2 mm thick there's no interior anyway |
| Print speed | 25–35 mm/s | Fast printing whips thin standing features |
| Cooling | 100% | Essential for bridging the open vein gaps |
| Supports | none | Print flat on the bed |
| Orientation | **flat**, wings splayed | Never upright — layer adhesion becomes the failure plane |
| Brim | 5 mm | Thin outlines lift easily |

White translucent PLA/PETG matches the reference build. Clear PETG over a pearl
underlayer also looks striking.

**Mechanism parts** — chassis, hinge block, wing mount, rockers, pushrods:

| Setting | Value |
|---|---|
| Material | PETG or PLA+ |
| Layer height | 0.16–0.20 mm |
| Walls | 4 |
| Infill | 40–60% |
| Supports | Only the chassis pivot-post holes may need them |
| Orientation | Rockers/pushrods **flat** so pin holes are round and layers resist the load |

Pin holes printed vertically come out undersized and slightly oval. Ream with a
1.8 mm drill bit by hand for a 1.75 mm filament pin.

## Attaching the blades

The wing mount plate has a 9 mm tab. Roughen both surfaces with sandpaper, then
bond with **thin CA** (fast, brittle) or **slow epoxy** (stronger, repositionable
— better here).

Clamp for the full cure. The joint takes every bit of flap load, and a wing that
lets go mid-wear will do it in the worst place.

## Design variations

Because the hinge is magnetic and keyed, one clip takes any number of wing sets —
print several mount plates and swap. That's the whole point of the original build.

- **Moth** — broader, rounder, feathery edge, fewer veins
- **Dragonfly** — two long narrow pairs, dense cross-hatch venation
- **Fairy** — elongated teardrop, spiral interior instead of veins
- **Abstract** — leaf skeleton, geometric lattice, or a stained-glass cell pattern

For colour: print in white and paint the rim with alcohol inks — they soak into
PLA and keep translucency in a way acrylic doesn't. Or glue on iridescent
cellophane behind the veins for a shifting effect in sunlight.
