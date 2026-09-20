#!/usr/bin/env python3
# Generates stl/wings.stl: a filigree wing pair (rim + veins) traced from the
# reference blue-morpho photo, sized per docs/WINGS.md, with a bonding tab
# matching wing_mount.stl's ~17.4 x 9.5 mm bond face. Requires numpy, shapely, trimesh.
import os
import numpy as np
from shapely.geometry import Polygon, LineString, MultiPolygon
from shapely.ops import unary_union
import trimesh


def catmull_rom_closed(points, samples_per_seg=24):
    pts = np.array(points, dtype=float)
    n = len(pts)
    out = []
    for i in range(n):
        p0, p1, p2, p3 = pts[(i - 1) % n], pts[i], pts[(i + 1) % n], pts[(i + 2) % n]
        for t in np.linspace(0, 1, samples_per_seg, endpoint=False):
            t2, t3 = t * t, t ** 3
            a = -0.5 * t3 + t2 - 0.5 * t
            b = 1.5 * t3 - 2.5 * t2 + 1.0
            c = -1.5 * t3 + 2.0 * t2 + 0.5 * t
            d = 0.5 * t3 - 0.5 * t2
            out.append(a * p0 + b * p1 + c * p2 + d * p3)
    return np.array(out)


# Outer silhouette control points for one (right) wing. Root is near (0,0)
# against the body; the forewing lobe sweeps up+out to a sharp apex, the
# hindwing lobe sweeps down+out with a scalloped margin, with a notch where
# the two lobes meet on the outer edge.
OUTLINE_CTRL = [
    (1.0, 1.0), (2.0, 8.0), (4.5, 17.0), (10.0, 27.0), (19.0, 36.0),
    (29.0, 42.5), (37.0, 46.0),            # apex
    (42.5, 43.0), (46.5, 37.5), (43.5, 33.5), (48.0, 28.0), (45.5, 22.5),
    (49.5, 16.5), (48.0, 9.0),
    (40.0, 3.5),                            # notch between fore/hind wing
    (52.5, -1.0), (47.5, -7.0), (53.0, -13.5), (48.0, -19.5), (46.5, -25.0),
    (40.0, -27.5), (36.5, -32.0), (28.0, -32.0), (21.0, -29.0), (12.5, -23.5),
    (6.5, -16.5), (2.5, -9.0), (1.2, -3.0),
]

FOREWING_VEIN_TARGETS = [
    (4.5, 17.0), (10.0, 27.0), (19.0, 36.0), (37.0, 46.0), (46.5, 37.5), (49.5, 16.5)
]
HINDWING_VEIN_TARGETS = [
    (52.5, -1.0), (53.0, -13.5), (46.5, -25.0), (28.0, -32.0)
]

TARGET_SPAN = 70.0   # root-to-tip, mm (docs/WINGS.md: 60-80 mm)
RIM_W = 1.4          # rim band width, mm (spec 1.2-1.6)
VEIN_W = 1.2         # vein width, mm (spec 1.0-1.4)
BLADE_T = 1.2        # extrusion thickness, mm (spec 1.2)
TAB_W = 17.4         # bonding tab width, matches wing_mount.stl bond face
TAB_D = 9.5          # bonding tab depth (spec: 8-10 mm overlap)
PRINT_GAP = 12.0      # spacing between the two wings when laid out flat


def build_wing_polygon():
    outer_pts = catmull_rom_closed(OUTLINE_CTRL)
    outer_poly = Polygon(outer_pts).buffer(0)

    apex = np.array([37.0, 46.0])
    root_ref = np.array([1.5, -1.0])
    scale = TARGET_SPAN / np.linalg.norm(apex - root_ref)

    outer_poly = Polygon(outer_pts * scale).buffer(0)
    rim = outer_poly.difference(outer_poly.buffer(-RIM_W))

    root_pt = np.array([0.0, -1.0]) * scale

    def vein_polys(targets, width):
        polys = []
        for tx, ty in targets:
            tip = np.array([tx, ty]) * scale
            direction = (tip - root_pt)
            direction /= np.linalg.norm(direction)
            tip_ext = tip + direction * (RIM_W + 1.0)  # overlap the rim
            polys.append(LineString([root_pt, tip_ext]).buffer(width / 2.0, cap_style=2, join_style=1))
        return polys

    veins = vein_polys(FOREWING_VEIN_TARGETS, VEIN_W) + vein_polys(HINDWING_VEIN_TARGETS, VEIN_W)

    tab = Polygon([
        (-TAB_D, -TAB_W / 2 - 1.0), (0.5, -TAB_W / 2 - 1.0),
        (0.5, TAB_W / 2 + 1.0), (-TAB_D, TAB_W / 2 + 1.0),
    ])

    filigree = unary_union([rim] + veins).intersection(outer_poly.buffer(0.01))
    return unary_union([filigree, tab]).buffer(0)


def extrude(geom, thickness):
    polys = geom.geoms if isinstance(geom, MultiPolygon) else [geom]
    meshes = [trimesh.creation.extrude_polygon(p.buffer(0), height=thickness)
              for p in polys if p.area > 1e-6]
    return trimesh.util.concatenate(meshes)


def main():
    wing_poly = build_wing_polygon()
    mesh_right = extrude(wing_poly, BLADE_T)
    assert mesh_right.is_watertight

    mesh_left = mesh_right.copy()
    # trimesh corrects face winding for this reflection's negative determinant
    mesh_left.apply_transform(trimesh.transformations.reflection_matrix([0, 0, 0], [1, 0, 0]))

    right = mesh_right.copy()
    right.apply_translation([-mesh_right.bounds[0][0] + 2.0, -mesh_right.bounds[0][1] + 2.0, 0])
    left = mesh_left.copy()
    left.apply_translation([right.bounds[1][0] + PRINT_GAP - mesh_left.bounds[0][0],
                             -mesh_left.bounds[0][1] + 2.0, 0])

    combined = trimesh.util.concatenate([right, left])
    assert combined.is_watertight and combined.volume > 0

    out_path = os.path.join(os.path.dirname(__file__), "..", "stl", "wings.stl")
    combined.export(out_path)
    print(f"wrote {out_path}  bounds={combined.bounds.tolist()}  volume_mm3={combined.volume:.1f}")


if __name__ == "__main__":
    main()
