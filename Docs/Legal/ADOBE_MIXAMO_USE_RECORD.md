# Adobe Mixamo Embedded-Game Use Record

Verified: 2026-09-01

This record documents the project's distribution decision for the four acquired
Mixamo character models. It is not a substitute for Adobe's terms.

## Applicable Adobe sources

- Mixamo FAQ: https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html
- Adobe General Terms, Content Files section 3.6:
  https://www.adobe.com/legal/terms.html
- Mixamo Additional Terms, effective 2021-06-23:
  https://wwwimages2.adobe.com/content/dam/cc/en/legal/servicetou/Mixamo-Addl-Terms-en_US-20210623.pdf

## Project decision

- Adobe permits royalty-free personal, commercial, and nonprofit use of Mixamo
  characters and animations, including video games.
- Adobe General Terms permit Content Files to be modified before they or their
  derivatives are embedded in an End Use and distributed only with that End Use.
- Original or modified Mixamo FBX, texture, animation, or other Content Files must
  not be distributed standalone, as an asset pack/template, or through a public
  source package.
- Mixamo content must not be used to create, train, test, or improve an AI/ML
  system under the Mixamo Additional Terms.
- Attribution is not required by the published Mixamo FAQ. This project retains
  provider and acquisition provenance regardless.

The selected models may therefore be used and manually customized for an embedded
commercial game. Clothes, hair, face, proportions, mesh, textures, and materials
may change, but the resulting character remains Mixamo-derived and retains the
standalone-file restriction.

## Repository controls

- Immutable source and staged files are hash-locked by their manifests and Unity
  validators.
- Runtime model animation import remains disabled. Project-owned controllers and
  deterministic combat timing remain authoritative.
- Erika's downloaded animation set remains outside runtime `Assets` because it
  failed project motion and weapon-staging gates; that is a technical decision.
- Non-Development release-candidate builds are permitted after source validation.
- Any externally shared source package must exclude both original and modified
  Mixamo Content Files. Team/contractor access must remain limited to the game
  project and must not create a standalone redistribution channel.

## Selected sources

- Erika Archer With Bow/Arrow — Hunter
- Uriel A Plotexia — Priest
- Vampire A Lusth — Rogue
- Demon T Wiezzorek — Mage

Acquisition date, exact file hashes, admitted runtime files, and exclusions are in
`SourceArt/Trials/AdobeMixamo/ErikaArcher/manifest.json` and
`SourceArt/Trials/AdobeMixamo/PlayableClasses/manifest.json`.
