# BodyV3 clothing-fit checkpoint authoring

This directory now retains only
`build_body_v3_clothing_fit_study.py`, the builder for the user-confirmed
`SessionHumanoidClothingFitStudyV1` static fit-direction checkpoint.

The rejected Hunter V3 macro builder, bytecode, documentation, and outputs were
moved without modification to
`SourceArt/Archive/RejectedCharacterResearch_2026-08-20/`. They are historical
rejection evidence and are excluded from the production lineage.

The clothing-fit builder creates a dense body-derived calibration envelope for a
plain long-sleeve top and trousers. Those meshes demonstrate appropriate fit and
bulk on the accepted masculine BodyV3 bind pose; they are not production garment
topology and must not be exported, surfaced, or copied into Unity.

Reproduce the checkpoint only when its frozen evidence intentionally needs to be
replaced:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --factory-startup --python-exit-code 1 \
  --python SourceArt/Tools/HunterProductionV3MacroAuthoring/build_body_v3_clothing_fit_study.py
```

Fresh Hunter class art belongs to the isolated
`HunterProductionBodyV3V1Authoring` path and must pass macro-form, deformation,
UV/PBR, LOD, and FBX gates before any reversible Unity A/B import.
