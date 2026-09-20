/**
 * Swappable Magnetic Butterfly Wings — hair clip mechanism
 * Onshape FeatureScript — parametric, generates all printed parts.
 *
 * HOW TO USE
 *   1. In an Onshape document: + tab -> FeatureScript.
 *   2. Replace the whole default file with this one. Commit.
 *   3. In a Part Studio: Insert custom feature -> "Butterfly Clip".
 *   4. Pick which part to generate, set your servo/magnet sizes, OK.
 *   5. Generate each part into its own Part Studio (or use the Part dropdown
 *      and re-insert the feature once per part), then right-click -> Export STL.
 *
 * The wing blades themselves are deliberately NOT generated here — a butterfly
 * wing outline is an artistic spline, not something you want defined by code.
 * This builds the MECHANISM: chassis, magnetic hinge, rockers, pushrods, clip.
 * Sketch your wing outline separately and add the magnet bosses from the
 * WING_MOUNT part as a reference. See docs/WINGS.md.
 *
 * Coordinate convention: +X is to the wearer's right, +Y is up (toward the
 * upstroke), +Z points away from the head. Origin sits at the servo shaft axis.
 */

FeatureScript 2588;
import(path : "onshape/std/geometry.fs", version : "2588.0");

// ───────────────────────── enums ─────────────────────────

export enum ClipPart
{
    annotation { "Name" : "Chassis (servo cradle + clip mount)" }
    CHASSIS,
    annotation { "Name" : "Hinge block (magnet receiver)" }
    HINGE_BLOCK,
    annotation { "Name" : "Wing mount plate (magnet side)" }
    WING_MOUNT,
    annotation { "Name" : "Rocker arm (pair)" }
    ROCKER,
    annotation { "Name" : "Pushrod (pair)" }
    PUSHROD,
    annotation { "Name" : "Electronics tray" }
    TRAY,
    annotation { "Name" : "All parts, laid out for printing" }
    ALL
}

export enum ServoSize
{
    annotation { "Name" : "9g micro (SG90 / AIMIKE)" }
    SG90,
    annotation { "Name" : "Sub-micro (MG90S metal gear)" }
    MG90S,
    annotation { "Name" : "Nano (3.7g linear)" }
    NANO
}

// ───────────────────────── feature ─────────────────────────

annotation { "Feature Type Name" : "Butterfly Clip",
             "Feature Type Description" : "Parametric magnetic swappable butterfly wing hair clip mechanism.",
             "Editing Logic Function" : "editLogic" }
export const butterflyClip = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Part to generate" }
        definition.part is ClipPart;

        annotation { "Group Name" : "Servo", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Servo size" }
            definition.servo is ServoSize;

            annotation { "Name" : "Override servo body size" }
            definition.customServo is boolean;

            if (definition.customServo)
            {
                annotation { "Name" : "Body length" }
                isLength(definition.servoL, SERVO_BOUNDS);
                annotation { "Name" : "Body width" }
                isLength(definition.servoW, SERVO_BOUNDS);
                annotation { "Name" : "Body height" }
                isLength(definition.servoH, SERVO_BOUNDS);
            }
        }

        annotation { "Group Name" : "Magnets", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Magnet diameter" }
            isLength(definition.magD, MAGNET_D_BOUNDS);

            annotation { "Name" : "Magnet thickness" }
            isLength(definition.magT, MAGNET_T_BOUNDS);

            annotation { "Name" : "Magnets per wing" }
            isInteger(definition.magCount, MAG_COUNT_BOUNDS);

            annotation { "Name" : "Pocket clearance (per side)" }
            isLength(definition.magClear, MAG_CLEARANCE_BOUNDS);

            annotation { "Name" : "Cap thickness over magnet" }
            isLength(definition.magCap, CAP_BOUNDS);
        }

        annotation { "Group Name" : "Wing linkage", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Wing span (one side, root to tip)" }
            isLength(definition.wingSpan, WINGSPAN_BOUNDS);

            annotation { "Name" : "Flap angle (total sweep)" }
            isAngle(definition.flapAngle, FLAP_BOUNDS);

            annotation { "Name" : "Rocker arm length" }
            isLength(definition.rockerLen, ROCKER_BOUNDS);

            annotation { "Name" : "Pin diameter" }
            isLength(definition.pinD, PIN_BOUNDS);
        }

        annotation { "Group Name" : "Print / fit", "Collapsed By Default" : true }
        {
            annotation { "Name" : "Wall thickness" }
            isLength(definition.wall, WALL_BOUNDS);

            annotation { "Name" : "Running clearance (pins, slots)" }
            isLength(definition.fitClear, FIT_CLEARANCE_BOUNDS);

            annotation { "Name" : "Add hair-clip mounting slots" }
            definition.clipSlots is boolean;

            if (definition.clipSlots)
            {
                annotation { "Name" : "Clip width (alligator clip band)" }
                isLength(definition.clipW, CLIP_W_BOUNDS);
            }

            annotation { "Name" : "Layout spacing (ALL mode)" }
            isLength(definition.layoutGap, GAP_BOUNDS);
        }
    }
    {
        // ── resolve servo dimensions ──
        var sv = servoDims(definition);

        const wall = definition.wall;
        const magPocketD = definition.magD + 2 * definition.magClear;
        const magPocketT = definition.magT + definition.magClear;

        // Build the requested part(s). In ALL mode each is offset along X so the
        // result drops straight onto a print bed without manual arranging.
        var parts = definition.part == ClipPart.ALL
            ? [ClipPart.CHASSIS, ClipPart.HINGE_BLOCK, ClipPart.WING_MOUNT,
               ClipPart.ROCKER, ClipPart.PUSHROD, ClipPart.TRAY]
            : [definition.part];

        var xCursor = 0 * millimeter;

        for (var i = 0; i < size(parts); i += 1)
        {
            var p = parts[i];
            var sub = id + ("part" ~ i);
            var built = false;
            var width = 0 * millimeter;

            if (p == ClipPart.CHASSIS)
            {
                buildChassis(context, sub, definition, sv, magPocketD, magPocketT);
                width = sv.L + 4 * wall + 16 * millimeter;
                built = true;
            }
            else if (p == ClipPart.HINGE_BLOCK)
            {
                buildHingeBlock(context, sub, definition, magPocketD, magPocketT);
                width = hingeBlockWidth(definition, magPocketD);
                built = true;
            }
            else if (p == ClipPart.WING_MOUNT)
            {
                buildWingMount(context, sub, definition, magPocketD, magPocketT);
                width = hingeBlockWidth(definition, magPocketD);
                built = true;
            }
            else if (p == ClipPart.ROCKER)
            {
                buildRockerPair(context, sub, definition);
                width = definition.rockerLen + 12 * millimeter;
                built = true;
            }
            else if (p == ClipPart.PUSHROD)
            {
                buildPushrodPair(context, sub, definition);
                width = 10 * millimeter;
                built = true;
            }
            else if (p == ClipPart.TRAY)
            {
                buildTray(context, sub, definition);
                width = 34 * millimeter;
                built = true;
            }

            if (built && xCursor > TOLERANCE.zeroLength * meter)
            {
                // shift this part clear of the previous ones
                var bodies = qCreatedBy(sub, EntityType.BODY);
                opTransform(context, sub + "shift", {
                            "bodies" : bodies,
                            "transform" : transform(vector(xCursor, 0 * millimeter, 0 * millimeter))
                        });
            }
            xCursor = xCursor + width + definition.layoutGap;
        }
    });

// ───────────────────────── bounds ─────────────────────────

const SERVO_BOUNDS = { (millimeter) : [4, 23, 60] } as LengthBoundSpec;
const MAGNET_D_BOUNDS = { (millimeter) : [2, 6, 20] } as LengthBoundSpec;
const MAGNET_T_BOUNDS = { (millimeter) : [0.5, 3, 10] } as LengthBoundSpec;
const MAG_COUNT_BOUNDS = { (unitless) : [2, 4, 8] } as IntegerBoundSpec;
const MAG_CLEARANCE_BOUNDS = { (millimeter) : [0, 0.15, 1.5] } as LengthBoundSpec;
const FIT_CLEARANCE_BOUNDS = { (millimeter) : [0, 0.25, 1.5] } as LengthBoundSpec;
const CAP_BOUNDS = { (millimeter) : [0, 0.6, 4] } as LengthBoundSpec;
const WINGSPAN_BOUNDS = { (millimeter) : [20, 70, 250] } as LengthBoundSpec;
const FLAP_BOUNDS = { (degree) : [5, 34, 80] } as AngleBoundSpec;
const ROCKER_BOUNDS = { (millimeter) : [6, 18, 60] } as LengthBoundSpec;
const PIN_BOUNDS = { (millimeter) : [0.8, 1.75, 5] } as LengthBoundSpec;
const WALL_BOUNDS = { (millimeter) : [0.4, 1.6, 6] } as LengthBoundSpec;
const CLIP_W_BOUNDS = { (millimeter) : [4, 10, 40] } as LengthBoundSpec;
const GAP_BOUNDS = { (millimeter) : [0, 6, 50] } as LengthBoundSpec;

// ───────────────────────── helpers ─────────────────────────

/** Servo body dimensions plus the mounting-flange details we cut around. */
function servoDims(definition is map) returns map
{
    if (definition.customServo)
    {
        return { "L" : definition.servoL, "W" : definition.servoW, "H" : definition.servoH,
                 "flangeT" : 2.5 * millimeter, "flangeL" : 32 * millimeter,
                 "screwD" : 2.2 * millimeter, "screwSpacing" : 28 * millimeter,
                 "shaftOffset" : definition.servoL / 2 - 6 * millimeter };
    }
    if (definition.servo == ServoSize.MG90S)
    {
        return { "L" : 22.8 * millimeter, "W" : 12.2 * millimeter, "H" : 22.5 * millimeter,
                 "flangeT" : 2.5 * millimeter, "flangeL" : 32.2 * millimeter,
                 "screwD" : 2.2 * millimeter, "screwSpacing" : 27.8 * millimeter,
                 "shaftOffset" : 5.9 * millimeter };
    }
    if (definition.servo == ServoSize.NANO)
    {
        return { "L" : 20 * millimeter, "W" : 8.6 * millimeter, "H" : 17 * millimeter,
                 "flangeT" : 1.8 * millimeter, "flangeL" : 26 * millimeter,
                 "screwD" : 1.8 * millimeter, "screwSpacing" : 23 * millimeter,
                 "shaftOffset" : 5.0 * millimeter };
    }
    // SG90 / AIMIKE 9g
    return { "L" : 22.5 * millimeter, "W" : 11.8 * millimeter, "H" : 22.7 * millimeter,
             "flangeT" : 2.5 * millimeter, "flangeL" : 32.0 * millimeter,
             "screwD" : 2.2 * millimeter, "screwSpacing" : 27.5 * millimeter,
             "shaftOffset" : 5.9 * millimeter };
}

/** Overall width of the hinge/mount plates, driven by magnet layout. */
function hingeBlockWidth(definition is map, magPocketD is ValueWithUnits) returns ValueWithUnits
{
    var perRow = floor(definition.magCount / 2);
    if (perRow < 1) perRow = 1;
    return perRow * (magPocketD + definition.wall) + definition.wall;
}

/**
 * Magnet centre positions for one plate, in the plate's local XY.
 * Two rows (upper/lower) so the wing can't rotate once attached — this is why
 * the reference build shows four pockets rather than one big magnet.
 */
function magnetCenters(definition is map, magPocketD is ValueWithUnits) returns array
{
    var perRow = floor(definition.magCount / 2);
    if (perRow < 1) perRow = 1;
    var pitch = magPocketD + definition.wall;
    var rowGap = magPocketD + definition.wall;
    var out = [];
    for (var r = 0; r < 2; r += 1)
    {
        for (var c = 0; c < perRow; c += 1)
        {
            var x = (c - (perRow - 1) / 2) * pitch;
            var y = (r - 0.5) * rowGap;
            out = append(out, vector(x, y));
        }
    }
    return out;
}

/** Cut a blind magnet pocket at each centre, opening toward +Z. */
function cutMagnetPockets(context is Context, id is Id, definition is map,
                          centers is array, plateZ is ValueWithUnits,
                          magPocketD is ValueWithUnits, magPocketT is ValueWithUnits,
                          target is Query)
{
    for (var i = 0; i < size(centers); i += 1)
    {
        var c = centers[i];
        var pid = id + ("mag" ~ i);
        var sk = newSketchOnPlane(context, pid + "sk", {
                    "sketchPlane" : plane(vector(0 * millimeter, 0 * millimeter, plateZ),
                                          vector(0, 0, 1), vector(1, 0, 0))
                });
        skCircle(sk, "c", { "center" : c, "radius" : magPocketD / 2 });
        skSolve(sk);

        extrude(context, pid + "ex", {
                    "entities" : qSketchRegion(pid + "sk", false),
                    "endBound" : BoundingType.BLIND,
                    "depth" : magPocketT,
                    "oppositeDirection" : true,
                    "operationType" : NewBodyOperationType.REMOVE
                });
        opDeleteBodies(context, pid + "del", { "entities" : qCreatedBy(pid + "sk", EntityType.BODY) });
    }
}

// ───────────────────────── part builders ─────────────────────────

/**
 * CHASSIS — cradles the servo, carries the rocker pivots, and mounts the clip.
 * The servo drops in from +Z and is retained by its own flange screws.
 */
function buildChassis(context is Context, id is Id, definition is map, sv is map,
                      magPocketD is ValueWithUnits, magPocketT is ValueWithUnits)
{
    const wall = definition.wall;
    const bodyL = sv.L + 2 * wall;
    const bodyW = sv.W + 2 * wall;
    const bodyH = sv.H * 0.62;           // cradle only the lower portion; horn end stays open

    // outer shell
    var sk = newSketchOnPlane(context, id + "base", {
                "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0))
            });
    skRectangle(sk, "r", {
                "firstCorner" : vector(-bodyL / 2, -bodyW / 2),
                "secondCorner" : vector(bodyL / 2, bodyW / 2)
            });
    skSolve(sk);
    extrude(context, id + "baseEx", {
                "entities" : qSketchRegion(id + "base", false),
                "endBound" : BoundingType.BLIND,
                "depth" : bodyH,
                "operationType" : NewBodyOperationType.NEW
            });
    opDeleteBodies(context, id + "baseDel", { "entities" : qCreatedBy(id + "base", EntityType.BODY) });

    // servo cavity
    var sk2 = newSketchOnPlane(context, id + "cav", {
                "sketchPlane" : plane(vector(0 * millimeter, 0 * millimeter, bodyH),
                                      vector(0, 0, 1), vector(1, 0, 0))
            });
    skRectangle(sk2, "r", {
                "firstCorner" : vector(-sv.L / 2, -sv.W / 2),
                "secondCorner" : vector(sv.L / 2, sv.W / 2)
            });
    skSolve(sk2);
    extrude(context, id + "cavEx", {
                "entities" : qSketchRegion(id + "cav", false),
                "endBound" : BoundingType.BLIND,
                "depth" : bodyH - wall,
                "oppositeDirection" : true,
                "operationType" : NewBodyOperationType.REMOVE
            });
    opDeleteBodies(context, id + "cavDel", { "entities" : qCreatedBy(id + "cav", EntityType.BODY) });

    // cable exit slot at the tail
    var sk3 = newSketchOnPlane(context, id + "cable", {
                "sketchPlane" : plane(vector(-bodyL / 2, 0 * millimeter, wall + 2 * millimeter),
                                      vector(1, 0, 0), vector(0, 1, 0))
            });
    skRectangle(sk3, "r", {
                "firstCorner" : vector(-4 * millimeter, 0 * millimeter),
                "secondCorner" : vector(4 * millimeter, 5 * millimeter)
            });
    skSolve(sk3);
    extrude(context, id + "cableEx", {
                "entities" : qSketchRegion(id + "cable", false),
                "endBound" : BoundingType.BLIND,
                "depth" : 3 * wall,
                "oppositeDirection" : true,
                "operationType" : NewBodyOperationType.REMOVE
            });
    opDeleteBodies(context, id + "cableDel", { "entities" : qCreatedBy(id + "cable", EntityType.BODY) });

    // rocker pivot posts — two towers straddling the servo horn
    var postH = bodyH + 7 * millimeter;
    var postX = sv.shaftOffset + 7 * millimeter;
    for (var s = -1; s <= 1; s += 2)
    {
        var pid = id + ("post" ~ (s + 1));
        var skp = newSketchOnPlane(context, pid + "sk", {
                    "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0))
                });
        skRectangle(skp, "r", {
                    "firstCorner" : vector(postX - 2.2 * millimeter, s * (bodyW / 2 - 2.4 * millimeter)),
                    "secondCorner" : vector(postX + 2.2 * millimeter, s * (bodyW / 2))
                });
        skSolve(skp);
        extrude(context, pid + "ex", {
                    "entities" : qSketchRegion(pid + "sk", false),
                    "endBound" : BoundingType.BLIND,
                    "depth" : postH,
                    "operationType" : NewBodyOperationType.ADD
                });
        opDeleteBodies(context, pid + "del", { "entities" : qCreatedBy(pid + "sk", EntityType.BODY) });

        // pivot hole through the post
        var skh = newSketchOnPlane(context, pid + "hsk", {
                    "sketchPlane" : plane(vector(0 * millimeter, s * bodyW / 2, 0 * millimeter),
                                          vector(0, s * 1, 0), vector(1, 0, 0))
                });
        skCircle(skh, "c", {
                    "center" : vector(postX, postH - 3.5 * millimeter),
                    "radius" : (definition.pinD + definition.fitClear) / 2
                });
        skSolve(skh);
        extrude(context, pid + "hex", {
                    "entities" : qSketchRegion(pid + "hsk", false),
                    "endBound" : BoundingType.BLIND,
                    "depth" : 4 * millimeter,
                    "operationType" : NewBodyOperationType.REMOVE
                });
        opDeleteBodies(context, pid + "hdel", { "entities" : qCreatedBy(pid + "hsk", EntityType.BODY) });
    }

    // hair-clip mounting slots underneath
    if (definition.clipSlots)
    {
        var skc = newSketchOnPlane(context, id + "clip", {
                    "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0))
                });
        // two transverse slots the clip band threads through
        for (var s = -1; s <= 1; s += 2)
        {
            skRectangle(skc, "s" ~ s, {
                        "firstCorner" : vector(s * (bodyL / 4) - 1.2 * millimeter, -definition.clipW / 2),
                        "secondCorner" : vector(s * (bodyL / 4) + 1.2 * millimeter, definition.clipW / 2)
                    });
        }
        skSolve(skc);
        extrude(context, id + "clipEx", {
                    "entities" : qSketchRegion(id + "clip", false),
                    "endBound" : BoundingType.BLIND,
                    "depth" : wall,
                    "operationType" : NewBodyOperationType.REMOVE
                });
        opDeleteBodies(context, id + "clipDel", { "entities" : qCreatedBy(id + "clip", EntityType.BODY) });
    }

    // round the outer vertical edges so it doesn't catch hair
    try silent
    {
        opFillet(context, id + "fil", {
                    "entities" : qCreatedBy(id + "baseEx", EntityType.EDGE)->qParallelEdges(vector(0, 0, 1)),
                    "radius" : 1.2 * millimeter
                });
    }
}

/**
 * HINGE_BLOCK — the fixed half of the magnetic joint. Bonds to the rocker arm;
 * its magnets face +Z to meet the wing mount plate.
 */
function buildHingeBlock(context is Context, id is Id, definition is map,
                         magPocketD is ValueWithUnits, magPocketT is ValueWithUnits)
{
    const wall = definition.wall;
    const plateW = hingeBlockWidth(definition, magPocketD);
    const plateH = 2 * (magPocketD + wall) + wall;
    const plateT = magPocketT + definition.magCap + wall * 0.5;

    var sk = newSketchOnPlane(context, id + "p", {
                "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0))
            });
    skRectangle(sk, "r", {
                "firstCorner" : vector(-plateW / 2, -plateH / 2),
                "secondCorner" : vector(plateW / 2, plateH / 2)
            });
    skSolve(sk);
    extrude(context, id + "pEx", {
                "entities" : qSketchRegion(id + "p", false),
                "endBound" : BoundingType.BLIND,
                "depth" : plateT,
                "operationType" : NewBodyOperationType.NEW
            });
    opDeleteBodies(context, id + "pDel", { "entities" : qCreatedBy(id + "p", EntityType.BODY) });

    // stem that bonds into the rocker arm
    var sks = newSketchOnPlane(context, id + "stem", {
                "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0))
            });
    skRectangle(sks, "r", {
                "firstCorner" : vector(-3 * millimeter, -plateH / 2 - 6 * millimeter),
                "secondCorner" : vector(3 * millimeter, -plateH / 2 + 0.1 * millimeter)
            });
    skSolve(sks);
    extrude(context, id + "stemEx", {
                "entities" : qSketchRegion(id + "stem", false),
                "endBound" : BoundingType.BLIND,
                "depth" : min(plateT, 3 * millimeter),
                "operationType" : NewBodyOperationType.ADD
            });
    opDeleteBodies(context, id + "stemDel", { "entities" : qCreatedBy(id + "stem", EntityType.BODY) });

    cutMagnetPockets(context, id, definition, magnetCenters(definition, magPocketD),
                     plateT, magPocketD, magPocketT, qCreatedBy(id, EntityType.BODY));

    // keying notch — guarantees the wing only goes on one way round
    var skk = newSketchOnPlane(context, id + "key", {
                "sketchPlane" : plane(vector(0 * millimeter, 0 * millimeter, plateT),
                                      vector(0, 0, 1), vector(1, 0, 0))
            });
    skCircle(skk, "c", { "center" : vector(0 * millimeter, 0 * millimeter),
                         "radius" : 1.6 * millimeter });
    skSolve(skk);
    extrude(context, id + "keyEx", {
                "entities" : qSketchRegion(id + "key", false),
                "endBound" : BoundingType.BLIND,
                "depth" : 1.2 * millimeter,
                "oppositeDirection" : true,
                "operationType" : NewBodyOperationType.REMOVE
            });
    opDeleteBodies(context, id + "keyDel", { "entities" : qCreatedBy(id + "key", EntityType.BODY) });
}

/**
 * WING_MOUNT — the swappable half. Same magnet grid mirrored, plus a keying pip
 * and a flat pad you bond the printed wing blade onto.
 */
function buildWingMount(context is Context, id is Id, definition is map,
                        magPocketD is ValueWithUnits, magPocketT is ValueWithUnits)
{
    const wall = definition.wall;
    const plateW = hingeBlockWidth(definition, magPocketD);
    const plateH = 2 * (magPocketD + wall) + wall;
    const plateT = magPocketT + definition.magCap + wall * 0.5;

    var sk = newSketchOnPlane(context, id + "p", {
                "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0))
            });
    skRectangle(sk, "r", {
                "firstCorner" : vector(-plateW / 2, -plateH / 2),
                "secondCorner" : vector(plateW / 2, plateH / 2)
            });
    skSolve(sk);
    extrude(context, id + "pEx", {
                "entities" : qSketchRegion(id + "p", false),
                "endBound" : BoundingType.BLIND,
                "depth" : plateT,
                "operationType" : NewBodyOperationType.NEW
            });
    opDeleteBodies(context, id + "pDel", { "entities" : qCreatedBy(id + "p", EntityType.BODY) });

    // wing bonding tab — glue the filigree blade here
    var skt = newSketchOnPlane(context, id + "tab", {
                "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0))
            });
    skRectangle(skt, "r", {
                "firstCorner" : vector(-plateW / 2, plateH / 2 - 0.1 * millimeter),
                "secondCorner" : vector(plateW / 2, plateH / 2 + 9 * millimeter)
            });
    skSolve(skt);
    extrude(context, id + "tabEx", {
                "entities" : qSketchRegion(id + "tab", false),
                "endBound" : BoundingType.BLIND,
                "depth" : max(wall, 1.2 * millimeter),
                "operationType" : NewBodyOperationType.ADD
            });
    opDeleteBodies(context, id + "tabDel", { "entities" : qCreatedBy(id + "tab", EntityType.BODY) });

    cutMagnetPockets(context, id, definition, magnetCenters(definition, magPocketD),
                     plateT, magPocketD, magPocketT, qCreatedBy(id, EntityType.BODY));

    // keying pip — mates with the hinge block's notch
    var skk = newSketchOnPlane(context, id + "key", {
                "sketchPlane" : plane(vector(0 * millimeter, 0 * millimeter, plateT),
                                      vector(0, 0, 1), vector(1, 0, 0))
            });
    skCircle(skk, "c", { "center" : vector(0 * millimeter, 0 * millimeter),
                         "radius" : 1.6 * millimeter - definition.fitClear });
    skSolve(skk);
    extrude(context, id + "keyEx", {
                "entities" : qSketchRegion(id + "key", false),
                "endBound" : BoundingType.BLIND,
                "depth" : 1.0 * millimeter,
                "operationType" : NewBodyOperationType.ADD
            });
    opDeleteBodies(context, id + "keyDel", { "entities" : qCreatedBy(id + "key", EntityType.BODY) });
}

/**
 * ROCKER — pivots on the chassis posts, driven by a pushrod from the servo horn.
 * Built as a mirrored pair. Lever ratio sets the wing sweep for a given horn arc.
 */
function buildRockerPair(context is Context, id is Id, definition is map)
{
    const len = definition.rockerLen;
    const th = max(definition.wall, 2.0 * millimeter);
    const holeR = (definition.pinD + definition.fitClear) / 2;

    for (var s = -1; s <= 1; s += 2)
    {
        var rid = id + ("rock" ~ (s + 1));
        var yOff = s * 9 * millimeter;

        var sk = newSketchOnPlane(context, rid + "sk", {
                    "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0))
                });
        // tapered arm: wide at the pivot, narrow at the wing end
        skPolyline(sk, "outline", {
                    "points" : [
                        vector(-4.0 * millimeter, yOff - 3.5 * millimeter),
                        vector(-4.0 * millimeter, yOff + 3.5 * millimeter),
                        vector(len - 2 * millimeter, yOff + 2.2 * millimeter),
                        vector(len, yOff + 0 * millimeter),
                        vector(len - 2 * millimeter, yOff - 2.2 * millimeter),
                        vector(-4.0 * millimeter, yOff - 3.5 * millimeter)
                    ]
                });
        skSolve(sk);
        extrude(context, rid + "ex", {
                    "entities" : qSketchRegion(rid + "sk", false),
                    "endBound" : BoundingType.BLIND,
                    "depth" : th,
                    "operationType" : NewBodyOperationType.NEW
                });
        opDeleteBodies(context, rid + "del", { "entities" : qCreatedBy(rid + "sk", EntityType.BODY) });

        // pivot hole + pushrod hole
        var skh = newSketchOnPlane(context, rid + "holes", {
                    "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0))
                });
        skCircle(skh, "pivot", { "center" : vector(0 * millimeter, yOff), "radius" : holeR });
        skCircle(skh, "rod", { "center" : vector(5.5 * millimeter, yOff), "radius" : holeR });
        skSolve(skh);
        extrude(context, rid + "holesEx", {
                    "entities" : qSketchRegion(rid + "holes", false),
                    "endBound" : BoundingType.BLIND,
                    "depth" : th,
                    "operationType" : NewBodyOperationType.REMOVE
                });
        opDeleteBodies(context, rid + "holesDel", { "entities" : qCreatedBy(rid + "holes", EntityType.BODY) });
    }
}

/** PUSHROD — two simple links. Print flat; or just use bent 1.75 mm filament. */
function buildPushrodPair(context is Context, id is Id, definition is map)
{
    const th = max(definition.wall, 1.8 * millimeter);
    const holeR = (definition.pinD + definition.fitClear) / 2;
    const rodLen = 14 * millimeter;

    for (var s = -1; s <= 1; s += 2)
    {
        var pid = id + ("rod" ~ (s + 1));
        var yOff = s * 6 * millimeter;

        var sk = newSketchOnPlane(context, pid + "sk", {
                    "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0))
                });
        skRectangle(sk, "body", {
                    "firstCorner" : vector(0 * millimeter, yOff - 2.0 * millimeter),
                    "secondCorner" : vector(rodLen, yOff + 2.0 * millimeter)
                });
        skCircle(sk, "e1", { "center" : vector(0 * millimeter, yOff), "radius" : 2.0 * millimeter });
        skCircle(sk, "e2", { "center" : vector(rodLen, yOff), "radius" : 2.0 * millimeter });
        skSolve(sk);
        extrude(context, pid + "ex", {
                    "entities" : qSketchRegion(pid + "sk", false),
                    "endBound" : BoundingType.BLIND,
                    "depth" : th,
                    "operationType" : NewBodyOperationType.NEW
                });
        opDeleteBodies(context, pid + "del", { "entities" : qCreatedBy(pid + "sk", EntityType.BODY) });

        var skh = newSketchOnPlane(context, pid + "holes", {
                    "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0))
                });
        skCircle(skh, "h1", { "center" : vector(0 * millimeter, yOff), "radius" : holeR });
        skCircle(skh, "h2", { "center" : vector(rodLen, yOff), "radius" : holeR });
        skSolve(skh);
        extrude(context, pid + "holesEx", {
                    "entities" : qSketchRegion(pid + "holes", false),
                    "endBound" : BoundingType.BLIND,
                    "depth" : th,
                    "operationType" : NewBodyOperationType.REMOVE
                });
        opDeleteBodies(context, pid + "holesDel", { "entities" : qCreatedBy(pid + "holes", EntityType.BODY) });
    }
}

/** TRAY — holds the XIAO ESP32-C3 and a small LiPo, clips under the chassis. */
function buildTray(context is Context, id is Id, definition is map)
{
    const wall = definition.wall;
    // XIAO ESP32-C3 is 21 x 17.5 mm; leave room for the USB-C end and wiring.
    const innerL = 23 * millimeter;
    const innerW = 19 * millimeter;
    const innerH = 9 * millimeter;

    var sk = newSketchOnPlane(context, id + "o", {
                "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0))
            });
    skRectangle(sk, "r", {
                "firstCorner" : vector(-(innerL / 2 + wall), -(innerW / 2 + wall)),
                "secondCorner" : vector(innerL / 2 + wall, innerW / 2 + wall)
            });
    skSolve(sk);
    extrude(context, id + "oEx", {
                "entities" : qSketchRegion(id + "o", false),
                "endBound" : BoundingType.BLIND,
                "depth" : innerH + wall,
                "operationType" : NewBodyOperationType.NEW
            });
    opDeleteBodies(context, id + "oDel", { "entities" : qCreatedBy(id + "o", EntityType.BODY) });

    var sk2 = newSketchOnPlane(context, id + "i", {
                "sketchPlane" : plane(vector(0 * millimeter, 0 * millimeter, innerH + wall),
                                      vector(0, 0, 1), vector(1, 0, 0))
            });
    skRectangle(sk2, "r", {
                "firstCorner" : vector(-innerL / 2, -innerW / 2),
                "secondCorner" : vector(innerL / 2, innerW / 2)
            });
    skSolve(sk2);
    extrude(context, id + "iEx", {
                "entities" : qSketchRegion(id + "i", false),
                "endBound" : BoundingType.BLIND,
                "depth" : innerH,
                "oppositeDirection" : true,
                "operationType" : NewBodyOperationType.REMOVE
            });
    opDeleteBodies(context, id + "iDel", { "entities" : qCreatedBy(id + "i", EntityType.BODY) });

    // USB-C access + button access
    var sk3 = newSketchOnPlane(context, id + "usb", {
                "sketchPlane" : plane(vector(innerL / 2 + wall, 0 * millimeter, wall + 1 * millimeter),
                                      vector(1, 0, 0), vector(0, 1, 0))
            });
    skRectangle(sk3, "r", {
                "firstCorner" : vector(-5 * millimeter, 0 * millimeter),
                "secondCorner" : vector(5 * millimeter, 4 * millimeter)
            });
    skSolve(sk3);
    extrude(context, id + "usbEx", {
                "entities" : qSketchRegion(id + "usb", false),
                "endBound" : BoundingType.BLIND,
                "depth" : 3 * wall,
                "oppositeDirection" : true,
                "operationType" : NewBodyOperationType.REMOVE
            });
    opDeleteBodies(context, id + "usbDel", { "entities" : qCreatedBy(id + "usb", EntityType.BODY) });
}

// ───────────────────────── editing logic ─────────────────────────

/**
 * Keeps magnet geometry physically sane: the pocket plus its cap must not
 * exceed a printable plate, and an odd magnet count can't split into two rows.
 */
export function editLogic(context is Context, id is Id, oldDefinition is map,
                          definition is map, isCreating is boolean,
                          specifiedParameters is map) returns map
{
    // two rows of magnets — force an even count
    if (definition.magCount % 2 != 0)
        definition.magCount = definition.magCount + 1;

    // a thin magnet under a thick cap just wastes stack height
    if (definition.magCap > definition.magT)
        definition.magCap = definition.magT;

    return definition;
}
