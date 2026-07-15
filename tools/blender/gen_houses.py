# SLATE — house kit v3, generated headless in Blender:
#   blender --background --python tools/blender/gen_houses.py
# Twelve houses (4 cultures x 3 variants) with real doors, windows, timber
# framing, chimneys, parapets and domes. Exports FBX into the Unity project
# (Resources/Models/houses); Git LFS carries the binaries. Material slot
# order is the contract with SettlementRenderer:
#   0 Walls, 1 Accent (roof/dome), 2 Trim (doors/windows/beams).
import bpy
import bmesh
import math
import os
from mathutils import Euler, Vector

OUT = os.path.normpath(os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..", "..", "unity", "SlateWorld", "Assets", "Slate", "Resources", "Models", "houses"))

WALLS, ACCENT, TRIM = 0, 1, 2


def make_materials():
    mats = []
    for name, color in (("Walls", (0.8, 0.74, 0.62)),
                        ("Accent", (0.6, 0.45, 0.3)),
                        ("Trim", (0.2, 0.16, 0.12))):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.diffuse_color = (*color, 1.0)
        mats.append(m)
    return mats


class B:
    """Tiny mesh builder: boxes, gables, domes with per-face material ids."""

    def __init__(self):
        self.v = []
        self.f = []
        self.m = []

    def box(self, c, s, mi, rot=(0, 0, 0)):
        e = Euler(rot, 'XYZ')
        h = Vector(s) * 0.5
        base = len(self.v)
        for dz in (-1, 1):
            for dy in (-1, 1):
                for dx in (-1, 1):
                    p = Vector((h.x * dx, h.y * dy, h.z * dz))
                    p.rotate(e)
                    self.v.append((c[0] + p.x, c[1] + p.y, c[2] + p.z))
        quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 2, 6, 4), (1, 5, 7, 3), (0, 4, 5, 1), (2, 3, 7, 6)]
        for q in quads:
            self.f.append(tuple(base + i for i in q))
            self.m.append(mi)

    def gable(self, base_c, w, h, d, mi, overhang=0.1):
        hw, hd = w * 0.5 + overhang, d * 0.5 + overhang
        x0, y0, z0 = base_c
        a = (x0 - hw, y0 - hd, z0)
        b = (x0 + hw, y0 - hd, z0)
        c = (x0 + hw, y0 + hd, z0)
        d2 = (x0 - hw, y0 + hd, z0)
        r1 = (x0, y0 - hd, z0 + h)
        r2 = (x0, y0 + hd, z0 + h)
        base = len(self.v)
        self.v.extend([a, b, c, d2, r1, r2])
        for face in ((0, 4, 5, 3), (1, 2, 5, 4), (0, 1, 4), (2, 3, 5)):
            self.f.append(tuple(base + i for i in face))
            self.m.append(mi)

    def dome(self, c, r, mi, seg=8, rings=3):
        for ring in range(rings):
            a0 = math.pi * 0.5 * ring / rings
            a1 = math.pi * 0.5 * (ring + 1) / rings
            z0, z1 = math.sin(a0) * r, math.sin(a1) * r
            r0, r1 = math.cos(a0) * r, math.cos(a1) * r
            for s in range(seg):
                b0 = s * 2 * math.pi / seg
                b1 = (s + 1) * 2 * math.pi / seg
                base = len(self.v)
                self.v.extend([
                    (c[0] + math.cos(b0) * r0, c[1] + math.sin(b0) * r0, c[2] + z0),
                    (c[0] + math.cos(b1) * r0, c[1] + math.sin(b1) * r0, c[2] + z0),
                    (c[0] + math.cos(b1) * r1, c[1] + math.sin(b1) * r1, c[2] + z1),
                    (c[0] + math.cos(b0) * r1, c[1] + math.sin(b0) * r1, c[2] + z1),
                ])
                self.f.append((base, base + 1, base + 2, base + 3))
                self.m.append(mi)

    # convenience dressing ------------------------------------------------

    def door(self, x, y, z, facing, w=0.28, h=0.62):
        """A recessed-looking dark door plate on a wall; facing = 'N/S/E/W'."""
        t = 0.05
        if facing in ('N', 'S'):
            self.box((x, y, z + h * 0.5), (w, t, h), TRIM)
        else:
            self.box((x, y, z + h * 0.5), (t, w, h), TRIM)

    def window(self, x, y, z, facing, w=0.17, h=0.2):
        t = 0.045
        if facing in ('N', 'S'):
            self.box((x, y, z), (w, t, h), TRIM)
        else:
            self.box((x, y, z), (t, w, h), TRIM)

    def beam_v(self, x, y, z0, z1):
        self.box((x, y, (z0 + z1) * 0.5), (0.055, 0.055, z1 - z0), TRIM)

    def beam_h(self, x0, x1, y, z):
        self.box(((x0 + x1) * 0.5, y, z), (x1 - x0, 0.055, 0.055), TRIM)

    def build(self, name, mats):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.v, [], self.f)
        for m in mats:
            me.materials.append(m)
        for poly, mi in zip(me.polygons, self.m):
            poly.material_index = mi
        me.update()
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
        obj = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(obj)
        bev = obj.modifiers.new("Bevel", 'BEVEL')
        bev.width = 0.012
        bev.segments = 1
        bev.angle_limit = math.radians(50)
        return obj


# ---------------------------------------------------------------- cultures

def aldish(v):
    b = B()
    if v == 0:  # cottage: plaster, steep thatch, stone chimney, timber frame
        b.box((0, 0, 0.5), (1, 1, 1), WALLS)
        b.gable((0, 0, 1.0), 1, 0.62, 1, ACCENT)
        b.box((0.3, 0.18, 1.35), (0.13, 0.13, 0.7), WALLS)
        b.door(0, -0.52, 0, 'S')
        b.window(-0.3, -0.52, 0.68, 'S'); b.window(0.3, -0.52, 0.68, 'S')
        b.window(-0.52, -0.2, 0.6, 'W'); b.window(-0.52, 0.25, 0.6, 'W')
        for x in (-0.47, 0.47):
            b.beam_v(x, -0.505, 0.05, 0.98)
        b.beam_h(-0.5, 0.5, -0.505, 0.96)
    elif v == 1:  # long cottage
        b.box((0, 0, 0.45), (0.95, 1.5, 0.9), WALLS)
        b.gable((0, 0, 0.9), 0.95, 0.55, 1.5, ACCENT)
        b.box((0.25, -0.4, 1.1), (0.12, 0.12, 0.6), WALLS)
        b.door(-0.5, 0.2, 0, 'W')
        for y in (-0.5, 0.0, 0.5):
            b.window(-0.5, y, 0.55, 'W'); b.window(0.5, y, 0.55, 'W')
        b.beam_h(-0.48, 0.48, -0.77, 0.86)
    else:  # two-story jettied townhouse
        b.box((0, 0, 0.55), (0.92, 0.92, 1.1), WALLS)
        b.box((0, 0, 1.32), (1.04, 1.04, 0.45), WALLS)
        b.gable((0, 0, 1.55), 1.04, 0.6, 1.04, ACCENT)
        b.box((-0.3, 0.2, 1.9), (0.13, 0.13, 0.7), WALLS)
        b.door(0.1, -0.47, 0, 'S')
        b.window(-0.28, -0.47, 0.7, 'S')
        b.window(-0.3, -0.53, 1.35, 'S'); b.window(0.3, -0.53, 1.35, 'S')
        for x in (-0.48, 0.48):
            b.beam_v(x, -0.535, 1.1, 1.53)
        b.beam_h(-0.5, 0.5, -0.535, 1.12)
    return b


def vasker(v):
    b = B()
    if v == 0:  # longhouse with crossed gable boards
        b.box((0, 0, 0.38), (0.9, 1.7, 0.76), WALLS)
        b.gable((0, 0, 0.76), 0.9, 0.85, 1.7, ACCENT)
        b.door(0, -0.87, 0, 'S', w=0.32, h=0.58)
        b.window(-0.46, -0.4, 0.5, 'W'); b.window(-0.46, 0.4, 0.5, 'W')
        for sy in (-0.88, 0.88):
            b.box((0.16, sy, 1.52), (0.05, 0.05, 0.5), TRIM, rot=(0, math.radians(25), 0))
            b.box((-0.16, sy, 1.52), (0.05, 0.05, 0.5), TRIM, rot=(0, math.radians(-25), 0))
    elif v == 1:  # great longhouse with entrance porch
        b.box((0, 0, 0.42), (1.0, 2.2, 0.84), WALLS)
        b.gable((0, 0, 0.84), 1.0, 0.95, 2.2, ACCENT)
        b.box((0, -1.25, 0.3), (0.5, 0.35, 0.6), WALLS)
        b.gable((0, -1.25, 0.6), 0.5, 0.35, 0.35, ACCENT)
        b.door(0, -1.44, 0, 'S', w=0.26, h=0.5)
        b.window(-0.51, -0.6, 0.55, 'W'); b.window(-0.51, 0.0, 0.55, 'W'); b.window(-0.51, 0.6, 0.55, 'W')
    else:  # cabin + store shed
        b.box((0, 0.2, 0.45), (0.9, 1.0, 0.9), WALLS)
        b.gable((0, 0.2, 0.9), 0.9, 0.9, 1.0, ACCENT)
        b.box((0, -0.72, 0.28), (0.6, 0.5, 0.56), WALLS)
        b.gable((0, -0.72, 0.56), 0.6, 0.4, 0.5, ACCENT)
        b.door(-0.46, 0.2, 0, 'W')
        b.window(0.46, 0.0, 0.55, 'E'); b.window(0.46, 0.45, 0.55, 'E')
    return b


def serai(v):
    b = B()
    if v == 0:  # flat-roofed cube, parapet, high windows
        b.box((0, 0, 0.5), (1, 1, 1), WALLS)
        b.box((0, 0, 1.04), (1.08, 1.08, 0.08), ACCENT)
        for i in range(3):
            f = (i + 0.5) / 3 - 0.5
            b.box((f * 0.95, -0.475, 1.15), (0.14, 0.14, 0.22), WALLS)
            b.box((f * 0.95, 0.475, 1.15), (0.14, 0.14, 0.22), WALLS)
            b.box((-0.475, f * 0.95, 1.15), (0.14, 0.14, 0.22), WALLS)
            b.box((0.475, f * 0.95, 1.15), (0.14, 0.14, 0.22), WALLS)
        b.door(0, -0.52, 0, 'S', w=0.3, h=0.7)
        b.window(-0.3, -0.52, 0.8, 'S', w=0.12, h=0.16); b.window(0.3, -0.52, 0.8, 'S', w=0.12, h=0.16)
        b.window(-0.52, 0.0, 0.8, 'W', w=0.12, h=0.16)
    elif v == 1:  # L-shaped courtyard house
        b.box((-0.2, 0, 0.45), (0.7, 1.3, 0.9), WALLS)
        b.box((0.35, -0.35, 0.4), (0.55, 0.6, 0.8), WALLS)
        b.box((-0.2, 0, 0.94), (0.78, 1.38, 0.08), ACCENT)
        b.box((0.35, -0.35, 0.84), (0.62, 0.68, 0.08), ACCENT)
        b.door(0.35, -0.66, 0, 'S', w=0.26, h=0.6)
        b.window(-0.56, -0.3, 0.6, 'W', w=0.12, h=0.16); b.window(-0.56, 0.3, 0.6, 'W', w=0.12, h=0.16)
    else:  # tower house with dome
        b.box((0, 0, 0.75), (0.85, 0.85, 1.5), WALLS)
        b.box((0, 0, 1.54), (0.93, 0.93, 0.08), ACCENT)
        b.dome((0, 0, 1.58), 0.3, ACCENT, seg=8, rings=2)
        b.door(0, -0.44, 0, 'S', w=0.26, h=0.6)
        b.window(-0.44, 0, 0.7, 'W', w=0.12, h=0.16)
        b.window(-0.44, 0, 1.2, 'W', w=0.12, h=0.16)
        b.window(0, -0.44, 1.2, 'S', w=0.12, h=0.16)
    return b


def tessian(v):
    b = B()
    if v == 0:  # villa under low tile
        b.box((0, 0, 0.45), (1.2, 1.0, 0.9), WALLS)
        b.gable((0, 0, 0.9), 1.2, 0.32, 1.0, ACCENT)
        b.door(0, -0.52, 0, 'S', w=0.3, h=0.66)
        b.window(-0.4, -0.52, 0.55, 'S', w=0.16, h=0.3); b.window(0.4, -0.52, 0.55, 'S', w=0.16, h=0.3)
        b.window(-0.62, 0, 0.55, 'W', w=0.16, h=0.3)
    elif v == 1:  # two-story townhouse with cornice
        b.box((0, 0, 0.7), (0.95, 0.95, 1.4), WALLS)
        b.box((0, 0, 1.42), (1.05, 1.05, 0.07), WALLS)
        b.gable((0, 0, 1.46), 0.95, 0.3, 0.95, ACCENT)
        b.door(0, -0.5, 0, 'S', w=0.28, h=0.66)
        for z in (0.55, 1.1):
            b.window(-0.28, -0.5, z, 'S', w=0.15, h=0.28); b.window(0.28, -0.5, z, 'S', w=0.15, h=0.28)
    else:  # porticoed house
        b.box((0, -0.1, 0.5), (1.1, 0.9, 1.0), WALLS)
        b.gable((0, -0.1, 1.0), 1.1, 0.34, 0.9, ACCENT)
        for i in (-1, 0, 1):
            b.box((i * 0.4, 0.45, 0.4), (0.09, 0.09, 0.8), WALLS)
        b.box((0, 0.45, 0.84), (1.1, 0.34, 0.08), ACCENT)
        b.door(0, 0.36, 0, 'N', w=0.3, h=0.66)
        b.window(-0.57, -0.2, 0.6, 'W', w=0.15, h=0.28)
    return b


def export(obj, path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,
        use_mesh_modifiers=True,
        axis_forward='-Z',
        axis_up='Y',
        add_leaf_bones=False,
    )


def main():
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    mats = make_materials()

    cultures = {"aldish": aldish, "vasker": vasker, "serai": serai, "tessian": tessian}
    for cname, fn in cultures.items():
        for v in range(3):
            builder = fn(v)
            obj = builder.build(f"{cname}_v{v}", mats)
            export(obj, os.path.join(OUT, f"{cname}_v{v}.fbx"))
            bpy.data.objects.remove(obj, do_unlink=True)
            print(f"EXPORTED {cname}_v{v}")

    print("HOUSES_DONE")


main()
