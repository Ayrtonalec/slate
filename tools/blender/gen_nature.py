# SLATE — nature kit + ship hull, generated headless in Blender:
#   blender --background --python tools/blender/gen_nature.py
# Branching broadleaf trees, tiered conifers, curved palms, umbrella pines,
# mountain boulders, and a real curved expedition-ship hull. Exports FBX into
# the Unity project. Material slot contracts:
#   trees: 0 Bark, 1 Foliage · rocks: 0 Rock · ship: 0 Hull, 1 Deck
import bpy
import bmesh
import math
import os
import random
from mathutils import Vector

OUT = os.path.normpath(os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..", "..", "unity", "SlateWorld", "Assets", "Slate", "Resources", "Models", "nature"))


def mats(*names):
    out = []
    for n in names:
        m = bpy.data.materials.get(n) or bpy.data.materials.new(n)
        out.append(m)
    return out


class B:
    def __init__(self):
        self.v = []
        self.f = []
        self.m = []

    def _frame(self, d):
        d = d.normalized()
        up = Vector((0, 0, 1)) if abs(d.z) < 0.95 else Vector((1, 0, 0))
        s = d.cross(up).normalized()
        u = s.cross(d).normalized()
        return s, u

    def cyl(self, p0, p1, r0, r1, mi, seg=6, cap=False):
        p0, p1 = Vector(p0), Vector(p1)
        s, u = self._frame(p1 - p0)
        base = len(self.v)
        for i in range(seg):
            a = i * 2 * math.pi / seg
            off = s * math.cos(a) + u * math.sin(a)
            self.v.append(tuple(p0 + off * r0))
            self.v.append(tuple(p1 + off * r1))
        for i in range(seg):
            j = (i + 1) % seg
            self.f.append((base + i * 2, base + j * 2, base + j * 2 + 1, base + i * 2 + 1))
            self.m.append(mi)
        if cap:
            c0, c1 = len(self.v), len(self.v) + 1
            self.v.append(tuple(p0))
            self.v.append(tuple(p1))
            for i in range(seg):
                j = (i + 1) % seg
                self.f.append((c0, base + j * 2, base + i * 2)); self.m.append(mi)
                self.f.append((c1, base + i * 2 + 1, base + j * 2 + 1)); self.m.append(mi)

    def blob(self, c, r, mi, seg=7, rings=5, squash=1.0, jitter=0.0, rnd=None):
        c = Vector(c)
        ring_rows = []
        for ring in range(rings + 1):
            a = math.pi * (-0.5 + ring / rings)
            z = math.sin(a) * r * squash
            rr = math.cos(a) * r
            row = []
            for sgi in range(seg):
                b = sgi * 2 * math.pi / seg
                p = Vector((math.cos(b) * rr, math.sin(b) * rr, z))
                if jitter > 0 and rnd is not None and 0 < ring < rings:
                    p *= 1.0 + (rnd.random() - 0.5) * 2 * jitter
                row.append(len(self.v))
                self.v.append(tuple(c + p))
            ring_rows.append(row)
        for ring in range(rings):
            for sgi in range(seg):
                j = (sgi + 1) % seg
                self.f.append((ring_rows[ring][sgi], ring_rows[ring][j],
                               ring_rows[ring + 1][j], ring_rows[ring + 1][sgi]))
                self.m.append(mi)

    def build(self, name, materials):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.v, [], self.f)
        for m in materials:
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
        return obj


BARK, FOLIAGE = 0, 1


def broadleaf(variant):
    rnd = random.Random(100 + variant)
    b = B()
    top = Vector((0, 0, 1.0 + rnd.random() * 0.2))
    b.cyl((0, 0, 0), top, 0.10, 0.055, BARK, cap=True)
    crowns = [(top, 0.42 + rnd.random() * 0.1)]
    for i in range(4 + variant):
        ang = rnd.random() * 2 * math.pi
        h = 0.55 + rnd.random() * 0.45
        length = 0.35 + rnd.random() * 0.3
        start = Vector((0, 0, h))
        end = start + Vector((math.cos(ang) * length, math.sin(ang) * length, 0.22 + rnd.random() * 0.25))
        b.cyl(start, end, 0.045, 0.02, BARK)
        crowns.append((end, 0.28 + rnd.random() * 0.14))
    for c, r in crowns:
        b.blob(c + Vector((0, 0, r * 0.35)), r, FOLIAGE, jitter=0.16, rnd=rnd, squash=0.85)
    return b


def conifer(variant):
    rnd = random.Random(200 + variant)
    b = B()
    b.cyl((0, 0, 0), (0, 0, 1.35), 0.085, 0.02, BARK, cap=True)
    tiers = 3 + variant
    for t in range(tiers):
        f = t / tiers
        z0 = 0.28 + f * 0.85
        r = (0.5 - f * 0.33) * (1 + (rnd.random() - 0.5) * 0.14)
        dx = (rnd.random() - 0.5) * 0.07
        dy = (rnd.random() - 0.5) * 0.07
        b.cyl((dx, dy, z0), (dx, dy, z0 + 0.42 - f * 0.1), r, 0.02, FOLIAGE, seg=7)
    return b


def umbrella_pine(variant):
    rnd = random.Random(300 + variant)
    b = B()
    kink = Vector(((rnd.random() - 0.5) * 0.3, (rnd.random() - 0.5) * 0.3, 0.62))
    top = kink + Vector(((rnd.random() - 0.5) * 0.2, (rnd.random() - 0.5) * 0.2, 0.5))
    b.cyl((0, 0, 0), kink, 0.09, 0.06, BARK, cap=True)
    b.cyl(kink, top, 0.06, 0.035, BARK)
    b.blob(top + Vector((0, 0, 0.08)), 0.62, FOLIAGE, squash=0.32, jitter=0.12, rnd=rnd)
    return b


def palm(variant):
    rnd = random.Random(400 + variant)
    b = B()
    # Curved trunk: stacked tapering segments leaning progressively.
    p = Vector((0, 0, 0))
    lean = Vector(((rnd.random() - 0.5) * 0.16, (rnd.random() - 0.5) * 0.16, 0))
    r0 = 0.075
    for i in range(4):
        nxt = p + Vector((lean.x * (i + 1), lean.y * (i + 1), 0.34))
        b.cyl(p, nxt, r0, r0 * 0.82, BARK, cap=(i == 0))
        p, r0 = nxt, r0 * 0.82
    # Fronds: tapered drooping blades.
    for i in range(7):
        ang = i * 2 * math.pi / 7 + rnd.random() * 0.3
        d = Vector((math.cos(ang), math.sin(ang), 0))
        end = p + d * 0.75 + Vector((0, 0, -0.18))
        mid = p + d * 0.4 + Vector((0, 0, 0.10))
        b.cyl(p, mid, 0.035, 0.025, FOLIAGE, seg=4)
        b.cyl(mid, end, 0.025, 0.004, FOLIAGE, seg=4)
    b.blob(p + Vector((0, 0, 0.05)), 0.09, FOLIAGE, seg=5, rings=3)
    return b


def rock(variant):
    rnd = random.Random(500 + variant)
    b = B()
    b.blob((0, 0, 0.28), 0.5, 0, seg=7, rings=5, squash=0.62, jitter=0.24, rnd=rnd)
    return b


def ship():
    b = B()
    HULL, DECK = 0, 1
    # Lofted hull sections along Y (stern -> bow): (y, half-width, keel-depth, gunwale-height)
    sections = [
        (-1.0, 0.10, 0.10, 0.42),
        (-0.7, 0.30, 0.26, 0.36),
        (-0.2, 0.38, 0.32, 0.33),
        (0.35, 0.34, 0.30, 0.36),
        (0.8, 0.18, 0.20, 0.48),
        (1.05, 0.02, 0.06, 0.62),  # raised prow
    ]
    rows = []
    for (y, w, keel, gun) in sections:
        row = []
        profile = [(-w, gun), (-w * 0.92, 0.05), (-w * 0.45, -keel * 0.7), (0, -keel),
                   (w * 0.45, -keel * 0.7), (w * 0.92, 0.05), (w, gun)]
        for (x, z) in profile:
            row.append(len(b.v))
            b.v.append((x, y, z + 0.32))  # lift so waterline sits at z≈0.1
        rows.append(row)
    for i in range(len(rows) - 1):
        for j in range(len(rows[i]) - 1):
            b.f.append((rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]))
            b.m.append(HULL)
    # Stern transom + deck strip.
    b.f.append(tuple(reversed(rows[0]))); b.m.append(HULL)
    for i in range(len(rows) - 1):
        b.f.append((rows[i][0], rows[i + 1][0], rows[i + 1][-1], rows[i][-1]))
        b.m.append(DECK)
    return b


def export(obj, path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True, use_mesh_modifiers=True,
        axis_forward='-Z', axis_up='Y', add_leaf_bones=False)


def main():
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()

    tree_mats = mats("Bark", "Foliage")
    rock_mats = mats("Rock")
    ship_mats = mats("Hull", "Deck")

    kits = []
    for v in range(2):
        kits.append((f"broadleaf_v{v}", broadleaf(v), tree_mats))
        kits.append((f"conifer_v{v}", conifer(v), tree_mats))
        kits.append((f"rock_v{v}", rock(v), rock_mats))
    kits.append(("umbrella_v0", umbrella_pine(0), tree_mats))
    kits.append(("palm_v0", palm(0), tree_mats))
    kits.append(("rock_v2", rock(2), rock_mats))
    kits.append(("ship_hull", ship(), ship_mats))

    for name, builder, materials in kits:
        obj = builder.build(name, materials)
        export(obj, os.path.join(OUT, f"{name}.fbx"))
        bpy.data.objects.remove(obj, do_unlink=True)
        print(f"EXPORTED {name}")
    print("NATURE_DONE")


main()
