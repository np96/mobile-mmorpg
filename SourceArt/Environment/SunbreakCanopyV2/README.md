# Sunbreak canopy V2 — secondary source-derived representation

TECH_REVIEW, not production acceptance. This candidate reuses the unchanged CC0
TreeSmall02 trunk and four measured leaf-cluster atlas variants from intake
POLYHAVEN-2026-005. All original deliveries and V1 JSON, atlases, provenance and
hash-locked generation procedure remain untouched.

`SunbreakCanopyAuthoring` is the reproducible Editor-only source for the new
generated `Art/Environment/SunbreakCanopyV2/TreeSmall02_DenseLeaves.asset`.
Each original pair of crossed branch-cluster cards is uniformly expanded around
its original source-placed cluster center, at most 1.9×. A whole-pair scale limit preserves
the original projected XZ convex envelope and vertical bounds; vertices are not
clamped or clipped. The rare shared source corners are split so adjacent clusters
cannot transform each other's vertices. The trunk, source normals/tangents, atlas
UVs, materials and four-cluster/two-view representation remain unchanged.

The tree remains 9,328 triangles: 3,000 original trunk and 6,328 foliage.
The new foliage has 18,984 independent triangle corners. No extra renderer,
runtime update, physics or per-frame allocation is introduced. Full-density near
trees are not accepted for wholesale distant instancing; distant scenery has its
own separate representation and budget. This candidate does not claim working
tree LOD transitions, wind, target-device cost or final canopy art acceptance.

Controlled comparison:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python Tools/Art/Blender/review_sunbreak_canopy.py
```

The diagnostic renders immutable V1 and 1.6×/1.9× candidates with 64 transparent
bounces, 32 fixed samples, a fixed seed and a gameplay-sized 310px tree.
It uses an orthographic 20° pitch / relative 60° yaw to isolate density; it is
not a Unity lighting/compression/MSAA or physical-phone result. Disposable output
and exact alpha-coverage measurements are below
`build/review/sunbreak-canopy-v2/`. Initial comparison measured 19,726, 25,031 and
27,006 alpha-area pixels respectively. The selected 1.9× representation joins leaf
masses while retaining major branch gaps. Actual gameplay/aspect/yaw acceptance,
alpha-test overdraw, motion, physical Android GPU/memory and thermals remain open.
