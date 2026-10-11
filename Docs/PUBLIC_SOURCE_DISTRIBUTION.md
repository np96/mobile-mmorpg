# Public source distribution

The public project uses **CC0 Quaternius character sources for all four classes**
and a regenerated playable foundation. Licensed Adobe Mixamo character files are
not prerequisites for opening or running this checkout. Core combat, stable class
identities, abilities and progression remain separate from that visual selection.

The original local development project may retain its independently acquired
licensed sources. It is not the same artifact as this cleaned public distribution.
Do not synchronize its licensed folders or older generated character assets into
the public repository.

## Historical screenshots

The README's Arena/Fire and PvE/Discipline images are unedited 1920×1080 macOS
Development-build captures dated **2026-09-08**. They show the local licensed
character bases, not the public fallback. They document historical in-game
presentation rather than promise identical public-checkout characters, phone
performance, production art acceptance or online play.

## Files intentionally excluded

- `Assets/ThirdParty/Trials/AdobeMixamo/**`: original/staged FBXs, extracted
  textures and associated Unity metadata.
- `SourceArt/Trials/AdobeMixamo/**`: acquired originals, rejected animation
  downloads and evaluation/DCC payloads.
- Generated character assets derived from those sources, including
  `Assets/SessionRpg/Art/Characters/*Mixamo*`, the
  `MixamoErikaTrial_Bow.asset` and `MixamoErikaTrial_Arrow.asset` meshes,
  their materials and associated `.meta` files.
- Licensed character references in the old generated Combatant foundation. The
  public foundation is regenerated with Quaternius roots rather than shipping
  broken references to excluded models.

Source code can retain optional licensed-import specifications and historical
names without including the model/texture/mesh payloads. Notices and historical
acceptance records do not prove that licensed bytes belong in a public commit.
Unity caches, build products, raw logs, local databases, credentials and disposable
capture iterations are also not source-checkout content.

Rejected character experiments (`SourceArt/Archive/`), dormant character R&D
(`SourceArt/Blender/Characters/` and `SourceArt/ThirdParty/`), and vegetation
`ClusterBakes/` intermediates are omitted. Their originals remain in the local
development workspace. Historical character scripts/docs can describe these
optional inputs; they are not public-game prerequisites. Accepted environment
DCC sources, final atlases, UI masters, provenance and Unity metadata are retained.

## Optional private character work

The following exact catalog names were recorded for the local development build.
Use the official [Adobe Mixamo character catalog](https://www.mixamo.com/) through
your own eligible account; this project supplies no mirrored downloads, signed
download URLs or transferable license.

| Local role | Recorded Mixamo catalog name |
|---|---|
| Hunter | Erika Archer With Bow/Arrow |
| Priest | Uriel A Plotexia |
| Rogue | Vampire A Lusth |
| Mage | Demon T Wiezzorek |

Adobe's [Mixamo FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html)
describes royalty-free game use. That does not make downloadable source files CC0:
[Adobe General Terms, section 3.6](https://www.adobe.com/legal/terms.html)
distinguishes embedded End Use from standalone Content File distribution.
Review the applicable [Mixamo Additional Terms](https://wwwimages2.adobe.com/content/dam/cc/en/legal/servicetou/Mixamo-Addl-Terms-en_US-20210623.pdf)
and your acquisition terms before using or sharing content.

For an independently licensed, non-public customization workspace:

1. Acquire the source lawfully and record provider, exact catalog identity,
   acquisition date, applicable terms and cryptographic hashes. Keep originals
   and derived files out of public commits; ignored paths alone are not a rights
   review.
2. Review the project's optional importer specifications and locked hashes in
   `Assets/SessionRpg/Editor/Assets/`. A new download may not match the historical
   bytes. Do not falsify provenance or disable integrity checks to admit it.
3. Verify rig, textures, model-only import, generated motion compatibility and
   the technical/art acceptance gates in
   [the character pipeline](Art/PRODUCTION_CHARACTER_PIPELINE.md).
   Only then make an explicit private source selection. Switching constants is
   not acquisition, import configuration or acceptance.
4. Run `Tools/Agent/unity.sh doctor` before invoking Unity. After verified
   source/import work, use the one relevant configuration command:
   `Tools/Agent/unity.sh configure-mixamo-trial` for Hunter or
   `Tools/Agent/unity.sh configure-mixamo-class-trials` for Priest/Rogue/Mage.
   These commands are not downloaders; do not retry them against missing sources.
5. With the Editor closed, run the supported `rebuild`, `validate` and
   `test-all` commands. Review source and generated
   changes, actual gameplay pixels, deformation and target-device budgets before
   claiming acceptance. Recheck distribution rights before any later sharing.

## Rights remain separate

Public visibility is not an open-source license grant for this game's own code
or artwork. No blanket project license has been selected. Third-party assets keep
their individual licenses and retained notices; a CC0 dependency does not relicense
the surrounding project. See [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md)
and [the recorded Adobe boundary](Legal/ADOBE_MIXAMO_USE_RECORD.md).
