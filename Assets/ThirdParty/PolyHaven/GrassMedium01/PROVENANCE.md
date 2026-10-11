# GrassMedium01 — immutable Poly Haven source

Intake: POLYHAVEN-2026-005; status TECH_REVIEW, not production acceptance.
Creator: Rob Tuytel (photography), Rico Cilliers (modeling). Acquired 2026-09-07 from the official asset page:
https://polyhaven.com/a/grass_medium_01

License: CC0 1.0, verified at https://polyhaven.com/license.
Retained legal code: CC0-1.0.txt; canonical source:
https://creativecommons.org/publicdomain/zero/1.0/legalcode.txt

manifest.json records exact official download URLs, original names, byte lengths,
official MD5 and independently measured SHA-256. The model extension includes
.source solely to prevent Unity auto-importing the full high-density original;
its bytes are untouched. Original 2K diffuse, GL normal, roughness and real alpha
maps are unchanged. No normals, height or occlusion are inferred from color.

Derivative procedure: Tools/Art/Blender/prepare_sunbreak_vegetation.py.
Selected Unity-axis mesh buffers live outside Assets under
SourceArt/Environment/SunbreakVegetationV1. SunbreakVegetationAssets configures
the bounded imports and generated meshes/materials; never edit generated output.
Desktop texture cap is 2K; Android cap is 1K. Gameplay-camera, device GPU/memory,
thermal and user art acceptance remain open.

