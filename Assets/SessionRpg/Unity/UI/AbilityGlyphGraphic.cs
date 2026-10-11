using SessionRpg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Unity.UI
{
    /// <summary>
    /// Draws compact, resolution-independent ability silhouettes directly into the uGUI mesh.
    /// The glyphs are original presentation assets and never participate in combat logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityGlyphGraphic : MaskableGraphic
    {
        private const int CircleSegments = 20;

        private enum HunterArrowGlyph
        {
            Chimera,
            Serpent,
            Viper,
            Scorpid,
            Aimed,
            Arcane,
            Kill,
            Steady,
            Freezing,
            Silencing,
            Tranquilizing,
            Concussive,
        }

        private enum PetCommandGlyph
        {
            Attack,
            Follow,
            Stay,
        }

        [SerializeField] private AbilityId _abilityId;
        [SerializeField] private Color _primaryColor = Color.white;
        [SerializeField] private Color _accentColor = new Color(1f, 0.86f, 0.45f, 1f);

        public AbilityId AbilityId => _abilityId;

        public int VisualIdentity => (int)_abilityId;

        /// <summary>
        /// Returns whether the current vector-glyph authoring switch has a dedicated
        /// silhouette for the stable ability ID. The range is append-only: a future
        /// ID fails validation until this endpoint and the drawing switch are both
        /// extended deliberately.
        /// </summary>
        public static bool HasExplicitGlyph(AbilityId abilityId)
        {
            int value = (int)abilityId;
            return value >= (int)AbilityId.HealerBolt &&
                value <= (int)AbilityId.ArrowRain;
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Bind(AbilityId abilityId, Color primaryColor)
        {
            _abilityId = abilityId;
            _primaryColor = WithAlpha(primaryColor, 1f);
            _accentColor = WithAlpha(Color.Lerp(primaryColor, Color.white, 0.68f), 1f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (_abilityId == AbilityId.None)
            {
                DrawUnknown(vertexHelper);
                return;
            }

            switch (_abilityId)
            {
                case AbilityId.HealerBolt:
                case AbilityId.ArcaneBolt:
                    DrawBolt(vertexHelper, false);
                    break;
                case AbilityId.Mend:
                case AbilityId.FlashHeal:
                case AbilityId.Renew:
                    DrawHeal(vertexHelper);
                    break;
                case AbilityId.Aegis:
                case AbilityId.PowerWordShield:
                    DrawShield(vertexHelper, false);
                    break;
                case AbilityId.Purify:
                case AbilityId.DispelMagic:
                    DrawDispel(vertexHelper);
                    break;
                case AbilityId.Rescue:
                    DrawRescue(vertexHelper);
                    break;
                case AbilityId.QuickStrike:
                    DrawQuickStrike(vertexHelper);
                    break;
                case AbilityId.Ambush:
                    DrawAmbush(vertexHelper);
                    break;
                case AbilityId.Kick:
                case AbilityId.Counterspell:
                    DrawInterrupt(vertexHelper, _abilityId == AbilityId.Kick);
                    break;
                case AbilityId.Shadowstep:
                case AbilityId.Blink:
                    DrawStep(vertexHelper);
                    break;
                case AbilityId.Anchor:
                    DrawAnchor(vertexHelper);
                    break;
                case AbilityId.Gust:
                    DrawGust(vertexHelper);
                    break;
                case AbilityId.FrostNova:
                    DrawFrostNova(vertexHelper);
                    break;
                case AbilityId.BossCleave:
                    DrawCleave(vertexHelper);
                    break;
                case AbilityId.BossMeteor:
                    DrawMeteor(vertexHelper);
                    break;
                case AbilityId.BossKnockback:
                    DrawBossRoar(vertexHelper);
                    break;
                case AbilityId.Penance:
                case AbilityId.Smite:
                    DrawPenance(vertexHelper);
                    break;
                case AbilityId.PainSuppression:
                    DrawPainSuppression(vertexHelper);
                    break;
                case AbilityId.PsychicScream:
                    DrawScream(vertexHelper);
                    break;
                case AbilityId.ShadowWordDeath:
                    DrawDeathWord(vertexHelper);
                    break;
                case AbilityId.Shadowfiend:
                    DrawShadowfiend(vertexHelper);
                    break;
                case AbilityId.Hemorrhage:
                    DrawHemorrhage(vertexHelper);
                    break;
                case AbilityId.Eviscerate:
                    DrawFinisher(vertexHelper, false);
                    break;
                case AbilityId.KidneyShot:
                case AbilityId.CheapShot:
                    DrawKidneyShot(vertexHelper);
                    break;
                case AbilityId.ShadowDance:
                    DrawShadowDance(vertexHelper);
                    break;
                case AbilityId.CloakOfShadows:
                    DrawCloak(vertexHelper);
                    break;
                case AbilityId.Mutilate:
                    DrawMutilate(vertexHelper);
                    break;
                case AbilityId.Envenom:
                    DrawFinisher(vertexHelper, true);
                    break;
                case AbilityId.Shiv:
                    DrawShiv(vertexHelper);
                    break;
                case AbilityId.BladeTempest:
                    DrawBladeTempest(vertexHelper);
                    break;
                case AbilityId.DawnNova:
                    DrawDawnNova(vertexHelper);
                    break;
                case AbilityId.UmbralCollapse:
                    DrawUmbralCollapse(vertexHelper);
                    break;
                case AbilityId.FlameSurge:
                    DrawFlameSurge(vertexHelper);
                    break;
                case AbilityId.Blizzard:
                    DrawBlizzard(vertexHelper);
                    break;
                case AbilityId.ArrowRain:
                    DrawArrowRain(vertexHelper);
                    break;
                case AbilityId.Blind:
                    DrawBlind(vertexHelper);
                    break;
                case AbilityId.Vanish:
                case AbilityId.Stealth:
                    DrawVanish(vertexHelper);
                    break;
                case AbilityId.Fireball:
                    DrawFireball(vertexHelper);
                    break;
                case AbilityId.FireBlast:
                    DrawFireBlast(vertexHelper);
                    break;
                case AbilityId.LivingBomb:
                    DrawLivingBomb(vertexHelper);
                    break;
                case AbilityId.DragonBreath:
                    DrawDragonBreath(vertexHelper);
                    break;
                case AbilityId.BlastWave:
                    DrawBlastWave(vertexHelper);
                    break;
                case AbilityId.Frostbolt:
                    DrawBolt(vertexHelper, true);
                    break;
                case AbilityId.IceLance:
                    DrawIceLance(vertexHelper);
                    break;
                case AbilityId.DeepFreeze:
                    DrawDeepFreeze(vertexHelper);
                    break;
                case AbilityId.IceBarrier:
                    DrawShield(vertexHelper, true);
                    break;
                case AbilityId.Polymorph:
                    DrawPolymorph(vertexHelper);
                    break;
                case AbilityId.ArenaTrinket:
                    DrawArenaTrinket(vertexHelper);
                    break;
                case AbilityId.PowerInfusion:
                    DrawPowerInfusion(vertexHelper);
                    break;
                case AbilityId.Combustion:
                    DrawCombustion(vertexHelper);
                    break;
                case AbilityId.IcyVeins:
                    DrawIcyVeins(vertexHelper);
                    break;
                case AbilityId.Evasion:
                    DrawEvasion(vertexHelper);
                    break;
                case AbilityId.ColdBlood:
                    DrawColdBlood(vertexHelper);
                    break;
                case AbilityId.VampiricTouch:
                    DrawVampiricTouch(vertexHelper);
                    break;
                case AbilityId.ShadowWordPain:
                    DrawShadowWordPain(vertexHelper);
                    break;
                case AbilityId.DevouringPlague:
                    DrawDevouringPlague(vertexHelper);
                    break;
                case AbilityId.MindFlay:
                    DrawMindFlay(vertexHelper);
                    break;
                case AbilityId.MindBlast:
                    DrawMindBlast(vertexHelper);
                    break;
                case AbilityId.Silence:
                    DrawSilence(vertexHelper);
                    break;
                case AbilityId.Dispersion:
                case AbilityId.Shadowform:
                    DrawDispersion(vertexHelper);
                    break;
                case AbilityId.PsychicHorror:
                    DrawPsychicHorror(vertexHelper);
                    break;
                case AbilityId.ManaBurn:
                    DrawManaBurn(vertexHelper);
                    break;
                case AbilityId.MassDispel:
                    DrawMassDispel(vertexHelper);
                    break;
                case AbilityId.Fade:
                    DrawFade(vertexHelper);
                    break;
                case AbilityId.ChimeraShot:
                    DrawHunterArrow(vertexHelper, HunterArrowGlyph.Chimera);
                    break;
                case AbilityId.SerpentSting:
                    DrawHunterArrow(vertexHelper, HunterArrowGlyph.Serpent);
                    break;
                case AbilityId.ViperSting:
                    DrawHunterArrow(vertexHelper, HunterArrowGlyph.Viper);
                    break;
                case AbilityId.ScorpidSting:
                    DrawHunterArrow(vertexHelper, HunterArrowGlyph.Scorpid);
                    break;
                case AbilityId.AimedShot:
                    DrawHunterArrow(vertexHelper, HunterArrowGlyph.Aimed);
                    break;
                case AbilityId.ArcaneShot:
                    DrawHunterArrow(vertexHelper, HunterArrowGlyph.Arcane);
                    break;
                case AbilityId.KillShot:
                    DrawHunterArrow(vertexHelper, HunterArrowGlyph.Kill);
                    break;
                case AbilityId.SteadyShot:
                    DrawHunterArrow(vertexHelper, HunterArrowGlyph.Steady);
                    break;
                case AbilityId.HuntersMark:
                    DrawHuntersMark(vertexHelper);
                    break;
                case AbilityId.ScatterShot:
                    DrawScatterShot(vertexHelper);
                    break;
                case AbilityId.FreezingArrow:
                    DrawHunterArrow(vertexHelper, HunterArrowGlyph.Freezing);
                    break;
                case AbilityId.FrostTrap:
                    DrawFrostTrap(vertexHelper);
                    break;
                case AbilityId.SilencingShot:
                    DrawHunterArrow(vertexHelper, HunterArrowGlyph.Silencing);
                    break;
                case AbilityId.FeignDeath:
                    DrawFeignDeath(vertexHelper);
                    break;
                case AbilityId.Disengage:
                    DrawDisengage(vertexHelper);
                    break;
                case AbilityId.WingClip:
                    DrawWingClip(vertexHelper);
                    break;
                case AbilityId.MastersCall:
                    DrawMastersCall(vertexHelper);
                    break;
                case AbilityId.Deterrence:
                    DrawDeterrence(vertexHelper);
                    break;
                case AbilityId.RoarOfSacrifice:
                    DrawRoarOfSacrifice(vertexHelper);
                    break;
                case AbilityId.Readiness:
                    DrawReadiness(vertexHelper);
                    break;
                case AbilityId.RapidFire:
                    DrawRapidFire(vertexHelper);
                    break;
                case AbilityId.PetAttack:
                    DrawPetCommand(vertexHelper, PetCommandGlyph.Attack);
                    break;
                case AbilityId.PetFollow:
                    DrawPetCommand(vertexHelper, PetCommandGlyph.Follow);
                    break;
                case AbilityId.PetStay:
                    DrawPetCommand(vertexHelper, PetCommandGlyph.Stay);
                    break;
                case AbilityId.SpiderWeb:
                    DrawSpiderWeb(vertexHelper);
                    break;
                case AbilityId.MendPet:
                    DrawMendPet(vertexHelper);
                    break;
                case AbilityId.TranquilizingShot:
                    DrawHunterArrow(vertexHelper, HunterArrowGlyph.Tranquilizing);
                    break;
                case AbilityId.ConcussiveShot:
                    DrawHunterArrow(vertexHelper, HunterArrowGlyph.Concussive);
                    break;
                case AbilityId.Flare:
                    DrawFlare(vertexHelper);
                    break;
                case AbilityId.AspectOfTheDragonhawk:
                    DrawDragonhawkAspect(vertexHelper);
                    break;
                case AbilityId.AspectOfTheViper:
                    DrawViperAspect(vertexHelper);
                    break;
                case AbilityId.PrayerOfMending:
                    DrawPrayerOfMending(vertexHelper);
                    break;
                case AbilityId.InnerFocus:
                    DrawInnerFocus(vertexHelper);
                    break;
                case AbilityId.FearWard:
                    DrawFearWard(vertexHelper);
                    break;
                case AbilityId.Scorch:
                    DrawScorch(vertexHelper);
                    break;
                case AbilityId.Pyroblast:
                    DrawPyroblast(vertexHelper);
                    break;
                case AbilityId.ConeOfCold:
                    DrawConeOfCold(vertexHelper);
                    break;
                case AbilityId.Spellsteal:
                    DrawSpellsteal(vertexHelper);
                    break;
                case AbilityId.IceBlock:
                    DrawIceBlock(vertexHelper);
                    break;
                case AbilityId.ManaShield:
                    DrawManaShield(vertexHelper);
                    break;
                case AbilityId.MoltenArmor:
                    DrawMoltenArmor(vertexHelper);
                    break;
                case AbilityId.MageArmor:
                    DrawMageArmor(vertexHelper);
                    break;
                case AbilityId.IceArmor:
                    DrawIceArmor(vertexHelper);
                    break;
                case AbilityId.WaterElementalFreeze:
                    DrawWaterElementalFreeze(vertexHelper);
                    break;
                case AbilityId.ColdSnap:
                    DrawColdSnap(vertexHelper);
                    break;
                case AbilityId.Sap:
                    DrawSap(vertexHelper);
                    break;
                case AbilityId.Garrote:
                    DrawGarrote(vertexHelper);
                    break;
                case AbilityId.Gouge:
                    DrawGouge(vertexHelper);
                    break;
                case AbilityId.Sprint:
                    DrawSprint(vertexHelper);
                    break;
                case AbilityId.Dismantle:
                    DrawDismantle(vertexHelper);
                    break;
                case AbilityId.Preparation:
                    DrawPreparation(vertexHelper);
                    break;
                default:
                    DrawUnknown(vertexHelper);
                    break;
            }

        }

        private void DrawUnknown(VertexHelper mesh)
        {
            AddCircle(mesh, new Vector2(-0.42f, 0f), 0.12f, _primaryColor);
            AddCircle(mesh, Vector2.zero, 0.12f, _accentColor);
            AddCircle(mesh, new Vector2(0.42f, 0f), 0.12f, _primaryColor);
        }

        private void DrawBolt(VertexHelper mesh, bool frost)
        {
            AddSegment(mesh, new Vector2(-0.72f, -0.48f), new Vector2(0.35f, 0.3f), 0.18f, _primaryColor);
            AddTriangle(mesh, new Vector2(0.12f, 0.08f), new Vector2(0.78f, 0.48f), new Vector2(0.48f, -0.18f), _accentColor);
            AddSegment(mesh, new Vector2(-0.78f, -0.12f), new Vector2(-0.3f, 0.22f), 0.08f, _accentColor);
            if (frost)
            {
                AddDiamond(mesh, new Vector2(0.08f, 0.12f), new Vector2(0.18f, 0.34f), _accentColor);
                AddSegment(mesh, new Vector2(-0.12f, -0.2f), new Vector2(0.28f, -0.5f), 0.08f, _primaryColor);
            }
        }

        private void DrawHeal(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.62f, 0.08f, WithAlpha(_primaryColor, 0.7f));
            AddSegment(mesh, new Vector2(-0.48f, 0f), new Vector2(0.48f, 0f), 0.18f, _primaryColor);
            AddSegment(mesh, new Vector2(0f, -0.48f), new Vector2(0f, 0.48f), 0.18f, _primaryColor);
            AddStar(mesh, new Vector2(0.48f, 0.44f), 0.22f, 0.07f, _accentColor);
        }

        private void DrawShield(VertexHelper mesh, bool frost)
        {
            AddShield(mesh, Vector2.zero, 0.78f, frost ? _accentColor : _primaryColor);
            if (frost)
            {
                AddSegment(mesh, new Vector2(-0.38f, 0.25f), new Vector2(0.36f, -0.2f), 0.09f, WithAlpha(_primaryColor, 0.9f));
                AddSegment(mesh, new Vector2(-0.34f, -0.06f), new Vector2(0.22f, 0.34f), 0.09f, WithAlpha(_primaryColor, 0.9f));
                AddDiamond(mesh, new Vector2(0.08f, 0.02f), new Vector2(0.15f, 0.26f), Color.white);
            }
            else
            {
                AddStar(mesh, new Vector2(0f, 0.06f), 0.3f, 0.1f, _accentColor);
            }
        }

        private void DrawDispel(VertexHelper mesh)
        {
            AddArc(mesh, Vector2.zero, 0.58f, 0.12f, 20f, 300f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.58f, -0.54f), new Vector2(0.58f, 0.56f), 0.13f, _accentColor);
            AddStar(mesh, new Vector2(0.58f, 0.55f), 0.2f, 0.07f, Color.white);
        }

        private void DrawRescue(VertexHelper mesh)
        {
            AddCircle(mesh, new Vector2(-0.48f, -0.28f), 0.2f, _primaryColor);
            AddCircle(mesh, new Vector2(0.48f, 0.28f), 0.2f, _accentColor);
            AddArc(mesh, Vector2.zero, 0.48f, 0.1f, 205f, 405f, _primaryColor);
            AddTriangle(mesh, new Vector2(0.46f, 0.02f), new Vector2(0.76f, 0.38f), new Vector2(0.28f, 0.42f), _accentColor);
        }

        private void DrawQuickStrike(VertexHelper mesh)
        {
            AddSegment(mesh, new Vector2(-0.62f, -0.58f), new Vector2(0.56f, 0.58f), 0.18f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.52f, -0.22f), new Vector2(0.2f, 0.5f), 0.07f, _accentColor);
            AddTriangle(mesh, new Vector2(0.34f, 0.36f), new Vector2(0.74f, 0.72f), new Vector2(0.62f, 0.18f), _accentColor);
        }

        private void DrawAmbush(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(-0.08f, 0f), 0.62f, 0.14f, 65f, 285f, _primaryColor);
            AddDagger(mesh, new Vector2(0.2f, -0.05f), 46f, 0.78f, _accentColor);
        }

        private void DrawInterrupt(VertexHelper mesh, bool physical)
        {
            AddRing(mesh, Vector2.zero, 0.56f, 0.11f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.62f, 0.58f), new Vector2(0.62f, -0.58f), 0.18f, _accentColor);
            AddSegment(mesh, new Vector2(-0.28f, -0.02f), new Vector2(-0.06f, -0.32f), 0.08f, Color.white);
            AddSegment(mesh, new Vector2(0.06f, 0.32f), new Vector2(0.3f, 0.02f), 0.08f, Color.white);
            if (physical)
            {
                AddTriangle(mesh, new Vector2(-0.55f, -0.08f), new Vector2(-0.82f, -0.42f), new Vector2(-0.24f, -0.38f), _primaryColor);
            }
        }

        private void DrawStep(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(-0.28f, 0f), 0.42f, 0.12f, 55f, 305f, WithAlpha(_primaryColor, 0.72f));
            AddArc(mesh, new Vector2(0.3f, 0f), 0.42f, 0.12f, 235f, 485f, _accentColor);
            AddChevron(mesh, new Vector2(-0.1f, 0f), 0.36f, _primaryColor);
            AddChevron(mesh, new Vector2(0.38f, 0f), 0.36f, _accentColor);
        }

        private void DrawAnchor(VertexHelper mesh)
        {
            AddSegment(mesh, new Vector2(0f, 0.68f), new Vector2(0f, -0.46f), 0.16f, _primaryColor);
            AddRing(mesh, new Vector2(0f, 0.58f), 0.18f, 0.08f, _accentColor);
            AddArc(mesh, new Vector2(0f, -0.18f), 0.52f, 0.14f, 190f, 350f, _primaryColor);
            AddTriangle(mesh, new Vector2(-0.5f, -0.32f), new Vector2(-0.76f, -0.1f), new Vector2(-0.28f, -0.02f), _accentColor);
            AddTriangle(mesh, new Vector2(0.5f, -0.32f), new Vector2(0.76f, -0.1f), new Vector2(0.28f, -0.02f), _accentColor);
        }

        private void DrawGust(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(-0.18f, 0.28f), 0.62f, 0.11f, 280f, 430f, _primaryColor);
            AddArc(mesh, new Vector2(-0.1f, -0.18f), 0.52f, 0.11f, 280f, 430f, _accentColor);
            AddTriangle(mesh, new Vector2(0.45f, 0.38f), new Vector2(0.82f, 0.2f), new Vector2(0.48f, 0.02f), _primaryColor);
        }

        private void DrawFrostNova(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.63f, 0.08f, WithAlpha(_primaryColor, 0.8f));
            DrawSnowflake(mesh, Vector2.zero, 0.72f, _accentColor);
        }

        private void DrawCleave(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(-0.1f, -0.12f), 0.7f, 0.18f, 15f, 155f, _primaryColor);
            AddTriangle(mesh, new Vector2(0.5f, 0.22f), new Vector2(0.83f, 0.56f), new Vector2(0.68f, -0.05f), _accentColor);
            AddSegment(mesh, new Vector2(-0.58f, -0.5f), new Vector2(0.45f, 0.44f), 0.07f, _accentColor);
        }

        private void DrawMeteor(VertexHelper mesh)
        {
            AddCircle(mesh, new Vector2(0.24f, -0.18f), 0.36f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.72f, 0.66f), new Vector2(0f, 0.04f), 0.2f, _accentColor);
            AddSegment(mesh, new Vector2(-0.62f, 0.3f), new Vector2(-0.06f, -0.12f), 0.09f, _primaryColor);
            AddStar(mesh, new Vector2(0.26f, -0.18f), 0.18f, 0.07f, _accentColor);
        }

        private void DrawBossRoar(VertexHelper mesh)
        {
            AddTriangle(mesh, new Vector2(-0.42f, 0.48f), new Vector2(-0.7f, 0.08f), new Vector2(-0.2f, 0.16f), _primaryColor);
            AddTriangle(mesh, new Vector2(0.42f, 0.48f), new Vector2(0.7f, 0.08f), new Vector2(0.2f, 0.16f), _primaryColor);
            AddArc(mesh, new Vector2(0f, -0.1f), 0.36f, 0.14f, 205f, 335f, _accentColor);
            AddArc(mesh, Vector2.zero, 0.62f, 0.09f, -50f, 50f, _primaryColor);
        }

        private void DrawPenance(VertexHelper mesh)
        {
            AddRing(mesh, new Vector2(0f, 0.2f), 0.54f, 0.09f, _primaryColor);
            AddCircle(mesh, new Vector2(-0.42f, -0.3f), 0.13f, _accentColor);
            AddCircle(mesh, new Vector2(0f, -0.48f), 0.13f, _accentColor);
            AddCircle(mesh, new Vector2(0.42f, -0.3f), 0.13f, _accentColor);
            AddSegment(mesh, new Vector2(-0.36f, -0.2f), new Vector2(-0.1f, 0.16f), 0.07f, _primaryColor);
            AddSegment(mesh, new Vector2(0f, -0.35f), new Vector2(0f, 0.14f), 0.07f, _primaryColor);
            AddSegment(mesh, new Vector2(0.36f, -0.2f), new Vector2(0.1f, 0.16f), 0.07f, _primaryColor);
        }

        private void DrawPainSuppression(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(-0.22f, 0f), 0.56f, 0.13f, 95f, 265f, _primaryColor);
            AddArc(mesh, new Vector2(0.22f, 0f), 0.56f, 0.13f, -85f, 85f, _primaryColor);
            AddCircle(mesh, new Vector2(0f, 0.22f), 0.16f, _accentColor);
            AddShield(mesh, new Vector2(0f, -0.18f), 0.4f, _accentColor);
        }

        private void DrawScream(VertexHelper mesh)
        {
            AddArc(mesh, Vector2.zero, 0.62f, 0.14f, 25f, 155f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.62f, 0.14f, 205f, 335f, _primaryColor);
            AddDiamond(mesh, Vector2.zero, new Vector2(0.23f, 0.35f), _accentColor);
            AddCircle(mesh, Vector2.zero, 0.08f, Color.white);
            AddArc(mesh, Vector2.zero, 0.82f, 0.07f, 25f, 155f, WithAlpha(_accentColor, 0.75f));
        }

        private void DrawDeathWord(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.52f, 0.15f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.35f, 0.35f), new Vector2(0.34f, -0.34f), 0.1f, _accentColor);
            AddSegment(mesh, new Vector2(0.34f, 0.45f), new Vector2(-0.25f, -0.26f), 0.08f, _accentColor);
            AddTriangle(mesh, new Vector2(0f, -0.3f), new Vector2(0.24f, -0.78f), new Vector2(-0.24f, -0.78f), _accentColor);
        }

        private void DrawShadowfiend(VertexHelper mesh)
        {
            AddDiamond(mesh, Vector2.zero, new Vector2(0.3f, 0.42f), _accentColor);
            AddCircle(mesh, Vector2.zero, 0.1f, Color.white);
            AddSegment(mesh, new Vector2(-0.58f, 0.6f), new Vector2(-0.2f, 0.24f), 0.13f, _primaryColor);
            AddSegment(mesh, new Vector2(0f, 0.72f), new Vector2(0f, 0.35f), 0.13f, _primaryColor);
            AddSegment(mesh, new Vector2(0.58f, 0.6f), new Vector2(0.2f, 0.24f), 0.13f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.68f, 0.1f, 205f, 335f, _primaryColor);
        }

        private void DrawHemorrhage(VertexHelper mesh)
        {
            DrawQuickStrike(mesh);
            AddDrop(mesh, new Vector2(0.42f, -0.48f), 0.24f, _accentColor);
        }

        private void DrawFinisher(VertexHelper mesh, bool poison)
        {
            AddSegment(mesh, new Vector2(-0.62f, -0.5f), new Vector2(0.56f, 0.54f), 0.22f, _primaryColor);
            for (int index = 0; index < 5; index++)
            {
                float x = -0.48f + (index * 0.24f);
                AddCircle(mesh, new Vector2(x, -0.68f), 0.07f, poison ? _accentColor : Color.white);
            }

            if (poison)
            {
                AddDrop(mesh, new Vector2(0.48f, 0.18f), 0.25f, _accentColor);
            }
            else
            {
                AddTriangle(mesh, new Vector2(0.35f, 0.34f), new Vector2(0.78f, 0.75f), new Vector2(0.64f, 0.12f), _accentColor);
            }
        }

        private void DrawKidneyShot(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(-0.18f, 0f), 0.48f, 0.15f, 75f, 285f, _primaryColor);
            AddStar(mesh, new Vector2(0.34f, 0f), 0.46f, 0.14f, _accentColor);
            AddSegment(mesh, new Vector2(-0.7f, 0f), new Vector2(-0.15f, 0f), 0.16f, _primaryColor);
        }

        private void DrawShadowDance(VertexHelper mesh)
        {
            AddArc(mesh, Vector2.zero, 0.63f, 0.1f, 15f, 165f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.63f, 0.1f, 195f, 345f, _primaryColor);
            AddDagger(mesh, new Vector2(-0.28f, 0.02f), -32f, 0.65f, _accentColor);
            AddDagger(mesh, new Vector2(0.28f, 0.02f), 32f, 0.65f, _accentColor);
        }

        private void DrawCloak(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(0f, 0.18f), 0.42f, 0.16f, 15f, 165f, _primaryColor);
            AddTriangle(mesh, new Vector2(-0.38f, 0.18f), new Vector2(0.38f, 0.18f), new Vector2(0f, -0.72f), _primaryColor);
            AddArc(mesh, Vector2.zero, 0.72f, 0.1f, 205f, 340f, _accentColor);
            AddSegment(mesh, new Vector2(0.4f, -0.52f), new Vector2(0.68f, -0.18f), 0.1f, _accentColor);
        }

        private void DrawMutilate(VertexHelper mesh)
        {
            AddDagger(mesh, new Vector2(-0.16f, 0f), -38f, 0.92f, _primaryColor);
            AddDagger(mesh, new Vector2(0.16f, 0f), 38f, 0.92f, _accentColor);
            AddCircle(mesh, Vector2.zero, 0.12f, Color.white);
        }

        private void DrawShiv(VertexHelper mesh)
        {
            AddDagger(mesh, new Vector2(-0.12f, 0.08f), 48f, 0.9f, _primaryColor);
            AddDrop(mesh, new Vector2(0.48f, -0.36f), 0.24f, _accentColor);
        }

        private void DrawBladeTempest(VertexHelper mesh)
        {
            AddCircle(mesh, Vector2.zero, 0.1f, _accentColor);
            for (int index = 0; index < 7; index++)
            {
                float angle = 90f - (index * (360f / 7f));
                Vector2 direction = Direction(angle);
                AddDagger(
                    mesh,
                    direction * 0.38f,
                    angle - 90f,
                    0.5f,
                    (index & 1) == 0 ? _primaryColor : _accentColor);
            }
        }

        private void DrawDawnNova(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.68f, 0.1f, WithAlpha(_primaryColor, 0.86f));
            AddStar(mesh, Vector2.zero, 0.56f, 0.16f, _primaryColor);
            AddCircle(mesh, Vector2.zero, 0.19f, _accentColor);
            AddSegment(mesh, new Vector2(-0.3f, 0f), new Vector2(0.3f, 0f), 0.1f, Color.white);
            AddSegment(mesh, new Vector2(0f, -0.3f), new Vector2(0f, 0.3f), 0.1f, Color.white);
        }

        private void DrawUmbralCollapse(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.7f, 0.1f, WithAlpha(_primaryColor, 0.76f));
            AddRing(mesh, Vector2.zero, 0.42f, 0.12f, _primaryColor);
            AddDiamond(mesh, Vector2.zero, new Vector2(0.22f, 0.32f), _accentColor);
            for (int index = 0; index < 4; index++)
            {
                Vector2 direction = Direction(45f + (index * 90f));
                AddSegment(mesh, direction * 0.72f, direction * 0.42f, 0.1f, _accentColor);
            }
        }

        private void DrawFlameSurge(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.68f, 0.11f, _primaryColor);
            AddStar(mesh, Vector2.zero, 0.63f, 0.19f, _accentColor);
            AddCircle(mesh, Vector2.zero, 0.22f, _primaryColor);
            AddTriangle(mesh, new Vector2(-0.2f, 0.06f), new Vector2(0f, 0.7f), new Vector2(0.2f, 0.06f), Color.white);
        }

        private void DrawBlizzard(VertexHelper mesh)
        {
            DrawSnowflake(mesh, new Vector2(0f, 0.28f), 0.34f, _accentColor);
            DrawSnowflake(mesh, new Vector2(-0.38f, -0.24f), 0.29f, _primaryColor);
            DrawSnowflake(mesh, new Vector2(0.38f, -0.24f), 0.29f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.75f, 0.08f, 195f, 345f, WithAlpha(_accentColor, 0.75f));
        }

        private void DrawArrowRain(VertexHelper mesh)
        {
            for (int index = -1; index <= 1; index++)
            {
                float x = index * 0.38f;
                AddArrow(
                    mesh,
                    new Vector2(x - 0.16f, 0.72f),
                    new Vector2(x + 0.14f, -0.48f),
                    0.075f,
                    index == 0 ? _accentColor : _primaryColor,
                    _accentColor);
            }

            AddArc(mesh, new Vector2(0f, -0.3f), 0.64f, 0.09f, 195f, 345f, WithAlpha(_primaryColor, 0.72f));
        }

        private void DrawBlind(VertexHelper mesh)
        {
            AddArc(mesh, Vector2.zero, 0.62f, 0.15f, 25f, 155f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.62f, 0.15f, 205f, 335f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.65f, 0.56f), new Vector2(0.65f, -0.56f), 0.16f, _accentColor);
            AddStar(mesh, new Vector2(0.42f, 0.42f), 0.2f, 0.07f, Color.white);
        }

        private void DrawVanish(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(0f, 0.18f), 0.44f, 0.15f, 15f, 165f, _primaryColor);
            AddTriangle(mesh, new Vector2(-0.38f, 0.18f), new Vector2(0.38f, 0.18f), new Vector2(0f, -0.5f), WithAlpha(_primaryColor, 0.86f));
            AddDiamond(mesh, new Vector2(0.5f, 0.38f), new Vector2(0.12f, 0.16f), _accentColor);
            AddDiamond(mesh, new Vector2(0.62f, -0.02f), new Vector2(0.09f, 0.12f), WithAlpha(_accentColor, 0.7f));
            AddDiamond(mesh, new Vector2(0.42f, -0.42f), new Vector2(0.07f, 0.1f), WithAlpha(_accentColor, 0.45f));
        }

        private void DrawFireball(VertexHelper mesh)
        {
            AddCircle(mesh, new Vector2(0.24f, 0.08f), 0.38f, _primaryColor);
            AddTriangle(mesh, new Vector2(-0.12f, 0.25f), new Vector2(-0.76f, 0.65f), new Vector2(-0.46f, 0.02f), _accentColor);
            AddTriangle(mesh, new Vector2(-0.08f, -0.08f), new Vector2(-0.7f, -0.52f), new Vector2(-0.38f, 0.1f), _primaryColor);
            AddStar(mesh, new Vector2(0.25f, 0.1f), 0.2f, 0.07f, _accentColor);
        }

        private void DrawFireBlast(VertexHelper mesh)
        {
            AddStar(mesh, Vector2.zero, 0.72f, 0.28f, _primaryColor);
            AddCircle(mesh, Vector2.zero, 0.24f, _accentColor);
            AddStar(mesh, Vector2.zero, 0.18f, 0.06f, Color.white);
        }

        private void DrawLivingBomb(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.5f, 0.16f, _primaryColor);
            AddCircle(mesh, Vector2.zero, 0.23f, _accentColor);
            AddArc(mesh, new Vector2(0.08f, 0.25f), 0.46f, 0.1f, 35f, 112f, _accentColor);
            AddStar(mesh, new Vector2(0.48f, 0.58f), 0.19f, 0.06f, Color.white);
        }

        private void DrawDragonBreath(VertexHelper mesh)
        {
            AddTriangle(mesh, new Vector2(-0.62f, 0.4f), new Vector2(-0.12f, 0.05f), new Vector2(-0.58f, -0.4f), _primaryColor);
            AddCircle(mesh, new Vector2(-0.42f, 0.24f), 0.08f, Color.white);
            AddTriangle(mesh, new Vector2(-0.12f, 0.2f), new Vector2(0.82f, 0.58f), new Vector2(0.7f, -0.12f), _accentColor);
            AddTriangle(mesh, new Vector2(-0.08f, -0.02f), new Vector2(0.7f, -0.58f), new Vector2(0.38f, 0.18f), _primaryColor);
        }

        private void DrawBlastWave(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.42f, 0.12f, _accentColor);
            AddRing(mesh, Vector2.zero, 0.72f, 0.1f, _primaryColor);
            AddStar(mesh, Vector2.zero, 0.34f, 0.16f, Color.white);
        }

        private void DrawIceLance(VertexHelper mesh)
        {
            AddTriangle(mesh, new Vector2(-0.65f, -0.42f), new Vector2(0.82f, 0.56f), new Vector2(0.22f, -0.2f), _accentColor);
            AddDiamond(mesh, new Vector2(-0.2f, -0.14f), new Vector2(0.2f, 0.34f), _primaryColor);
            AddSegment(mesh, new Vector2(-0.62f, -0.56f), new Vector2(-0.22f, -0.26f), 0.1f, Color.white);
        }

        private void DrawDeepFreeze(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.65f, 0.1f, _primaryColor);
            AddDiamond(mesh, Vector2.zero, new Vector2(0.34f, 0.58f), _accentColor);
            AddSegment(mesh, new Vector2(-0.42f, 0.02f), new Vector2(0.42f, 0.02f), 0.12f, Color.white);
            AddSegment(mesh, new Vector2(0f, -0.4f), new Vector2(0f, 0.42f), 0.12f, Color.white);
        }

        private void DrawPolymorph(VertexHelper mesh)
        {
            AddArc(mesh, Vector2.zero, 0.58f, 0.12f, -55f, 245f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.34f, 0.1f, 120f, 420f, _accentColor);
            AddCircle(mesh, new Vector2(0.12f, -0.04f), 0.09f, Color.white);
            AddStar(mesh, new Vector2(0.5f, 0.5f), 0.18f, 0.06f, _accentColor);
            AddChevron(mesh, new Vector2(-0.46f, -0.4f), 0.22f, _primaryColor);
        }

        private void DrawArenaTrinket(VertexHelper mesh)
        {
            AddArc(mesh, Vector2.zero, 0.66f, 0.12f, 28f, 136f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.66f, 0.12f, 208f, 316f, _primaryColor);
            AddDiamond(mesh, Vector2.zero, new Vector2(0.25f, 0.36f), _accentColor);
            AddSegment(mesh, new Vector2(-0.6f, -0.56f), new Vector2(0.6f, 0.56f), 0.13f, Color.white);
            AddTriangle(mesh, new Vector2(-0.66f, 0.12f), new Vector2(-0.88f, -0.04f), new Vector2(-0.58f, -0.14f), _accentColor);
            AddTriangle(mesh, new Vector2(0.66f, -0.12f), new Vector2(0.88f, 0.04f), new Vector2(0.58f, 0.14f), _accentColor);
        }

        private void DrawPowerInfusion(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.68f, 0.08f, WithAlpha(_primaryColor, 0.82f));
            AddChevron(mesh, new Vector2(0f, -0.26f), 0.52f, _primaryColor);
            AddChevron(mesh, new Vector2(0f, 0.12f), 0.46f, _accentColor);
            AddStar(mesh, new Vector2(0f, 0.6f), 0.2f, 0.06f, Color.white);
        }

        private void DrawCombustion(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(0f, -0.14f), 0.62f, 0.14f, 198f, 342f, _primaryColor);
            AddTriangle(mesh, new Vector2(-0.52f, -0.06f), new Vector2(-0.36f, 0.64f), new Vector2(-0.05f, 0.02f), _primaryColor);
            AddTriangle(mesh, new Vector2(-0.12f, -0.02f), new Vector2(0.08f, 0.82f), new Vector2(0.26f, -0.02f), _accentColor);
            AddTriangle(mesh, new Vector2(0.18f, -0.02f), new Vector2(0.52f, 0.58f), new Vector2(0.56f, -0.12f), _primaryColor);
            AddCircle(mesh, new Vector2(0.04f, -0.22f), 0.22f, Color.white);
        }

        private void DrawIcyVeins(VertexHelper mesh)
        {
            DrawSnowflake(mesh, new Vector2(-0.08f, 0.04f), 0.54f, _accentColor);
            AddSegment(mesh, new Vector2(-0.74f, -0.62f), new Vector2(0.62f, 0.54f), 0.09f, _primaryColor);
            AddChevron(mesh, new Vector2(0.42f, 0.34f), 0.38f, Color.white);
            AddSegment(mesh, new Vector2(-0.76f, 0.42f), new Vector2(-0.44f, 0.68f), 0.08f, WithAlpha(_primaryColor, 0.8f));
            AddSegment(mesh, new Vector2(0.42f, -0.68f), new Vector2(0.74f, -0.42f), 0.08f, WithAlpha(_primaryColor, 0.8f));
        }

        private void DrawEvasion(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(-0.18f, 0f), 0.62f, 0.11f, 82f, 278f, _primaryColor);
            AddArc(mesh, new Vector2(0.18f, 0f), 0.62f, 0.11f, -98f, 98f, _accentColor);
            AddSegment(mesh, new Vector2(-0.34f, 0.2f), new Vector2(-0.66f, 0f), 0.09f, Color.white);
            AddSegment(mesh, new Vector2(-0.66f, 0f), new Vector2(-0.34f, -0.2f), 0.09f, Color.white);
            AddSegment(mesh, new Vector2(0.34f, 0.2f), new Vector2(0.66f, 0f), 0.09f, Color.white);
            AddSegment(mesh, new Vector2(0.66f, 0f), new Vector2(0.34f, -0.2f), 0.09f, Color.white);
            AddDiamond(mesh, Vector2.zero, new Vector2(0.11f, 0.18f), WithAlpha(_primaryColor, 0.58f));
        }

        private void DrawColdBlood(VertexHelper mesh)
        {
            AddDrop(mesh, new Vector2(0f, 0.06f), 1.04f, _primaryColor);
            AddDiamond(mesh, new Vector2(0f, -0.08f), new Vector2(0.18f, 0.28f), _accentColor);
            AddSegment(mesh, new Vector2(-0.62f, -0.54f), new Vector2(-0.26f, -0.24f), 0.08f, Color.white);
            AddSegment(mesh, new Vector2(0.26f, -0.24f), new Vector2(0.62f, -0.54f), 0.08f, Color.white);
            AddCircle(mesh, new Vector2(0f, -0.08f), 0.07f, Color.white);
        }

        private void DrawVampiricTouch(VertexHelper mesh)
        {
            AddRing(mesh, new Vector2(0f, 0.06f), 0.54f, 0.1f, WithAlpha(_primaryColor, 0.84f));
            AddArc(mesh, new Vector2(0f, 0.05f), 0.34f, 0.12f, 205f, 335f, _accentColor);
            AddTriangle(mesh, new Vector2(-0.3f, 0.14f), new Vector2(-0.04f, -0.62f), new Vector2(-0.02f, 0.2f), _primaryColor);
            AddTriangle(mesh, new Vector2(0.3f, 0.14f), new Vector2(0.04f, -0.62f), new Vector2(0.02f, 0.2f), _accentColor);
            AddCircle(mesh, new Vector2(0f, 0.36f), 0.09f, Color.white);
        }

        private void DrawShadowWordPain(VertexHelper mesh)
        {
            AddArc(mesh, Vector2.zero, 0.58f, 0.13f, 20f, 160f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.58f, 0.13f, 200f, 340f, _primaryColor);
            AddDiamond(mesh, Vector2.zero, new Vector2(0.2f, 0.3f), _accentColor);
            AddSegment(mesh, new Vector2(-0.68f, 0.6f), new Vector2(-0.38f, 0.3f), 0.1f, _accentColor);
            AddSegment(mesh, new Vector2(0f, 0.78f), new Vector2(0f, 0.42f), 0.1f, _accentColor);
            AddSegment(mesh, new Vector2(0.68f, 0.6f), new Vector2(0.38f, 0.3f), 0.1f, _accentColor);
        }

        private void DrawDevouringPlague(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.5f, 0.14f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.72f, 0.08f, 35f, 305f, WithAlpha(_accentColor, 0.8f));
            AddDrop(mesh, new Vector2(0f, 0.14f), 0.5f, _accentColor);
            AddDrop(mesh, new Vector2(-0.52f, -0.3f), 0.2f, _primaryColor);
            AddDrop(mesh, new Vector2(0.52f, -0.3f), 0.2f, _primaryColor);
        }

        private void DrawMindFlay(VertexHelper mesh)
        {
            AddDiamond(mesh, new Vector2(0.54f, 0f), new Vector2(0.2f, 0.3f), _accentColor);
            AddSegment(mesh, new Vector2(-0.72f, 0.42f), new Vector2(-0.2f, 0.28f), 0.12f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.05f, 0.24f), new Vector2(0.4f, 0.08f), 0.1f, _accentColor);
            AddSegment(mesh, new Vector2(-0.78f, 0f), new Vector2(0.34f, 0f), 0.14f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.72f, -0.42f), new Vector2(-0.2f, -0.28f), 0.12f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.05f, -0.24f), new Vector2(0.4f, -0.08f), 0.1f, _accentColor);
        }

        private void DrawMindBlast(VertexHelper mesh)
        {
            AddArc(mesh, Vector2.zero, 0.68f, 0.11f, 20f, 138f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.68f, 0.11f, 202f, 320f, _primaryColor);
            AddStar(mesh, Vector2.zero, 0.58f, 0.2f, _accentColor);
            AddCircle(mesh, Vector2.zero, 0.12f, Color.white);
            AddSegment(mesh, new Vector2(-0.78f, 0f), new Vector2(-0.5f, 0f), 0.1f, _primaryColor);
            AddSegment(mesh, new Vector2(0.5f, 0f), new Vector2(0.78f, 0f), 0.1f, _primaryColor);
        }

        private void DrawSilence(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.58f, 0.13f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.42f, 0f), new Vector2(0.42f, 0f), 0.16f, WithAlpha(_primaryColor, 0.78f));
            AddSegment(mesh, new Vector2(-0.64f, 0.58f), new Vector2(0.64f, -0.58f), 0.18f, _accentColor);
            AddCircle(mesh, new Vector2(-0.2f, 0f), 0.06f, Color.white);
            AddCircle(mesh, new Vector2(0.2f, 0f), 0.06f, Color.white);
        }

        private void DrawDispersion(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.72f, 0.09f, WithAlpha(_primaryColor, 0.78f));
            AddDiamond(mesh, new Vector2(0f, 0.1f), new Vector2(0.34f, 0.66f), _primaryColor);
            AddDiamond(mesh, new Vector2(-0.32f, -0.06f), new Vector2(0.2f, 0.42f), WithAlpha(_accentColor, 0.72f));
            AddDiamond(mesh, new Vector2(0.32f, -0.06f), new Vector2(0.2f, 0.42f), _accentColor);
            AddCircle(mesh, new Vector2(0f, 0.06f), 0.08f, Color.white);
        }

        private void DrawPsychicHorror(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(0f, -0.04f), 0.55f, 0.13f, 18f, 162f, _primaryColor);
            AddArc(mesh, new Vector2(0f, -0.04f), 0.55f, 0.13f, 198f, 342f, _primaryColor);
            AddDiamond(mesh, new Vector2(0f, -0.04f), new Vector2(0.18f, 0.28f), Color.white);
            AddTriangle(mesh, new Vector2(-0.54f, 0.34f), new Vector2(-0.42f, 0.82f), new Vector2(-0.18f, 0.4f), _accentColor);
            AddTriangle(mesh, new Vector2(-0.18f, 0.42f), new Vector2(0f, 0.88f), new Vector2(0.18f, 0.42f), _primaryColor);
            AddTriangle(mesh, new Vector2(0.18f, 0.4f), new Vector2(0.42f, 0.82f), new Vector2(0.54f, 0.34f), _accentColor);
        }

        private void DrawManaBurn(VertexHelper mesh)
        {
            AddDrop(mesh, new Vector2(0f, 0.04f), 1f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.44f, 0.46f), new Vector2(0.44f, -0.5f), 0.15f, _accentColor);
            AddDiamond(mesh, new Vector2(0.14f, -0.08f), new Vector2(0.16f, 0.25f), Color.white);
            AddArc(mesh, new Vector2(0f, -0.16f), 0.62f, 0.08f, 200f, 340f, WithAlpha(_accentColor, 0.72f));
        }

        private void DrawMassDispel(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.7f, 0.08f, _primaryColor);
            AddRing(mesh, Vector2.zero, 0.42f, 0.1f, WithAlpha(_accentColor, 0.84f));
            AddSegment(mesh, new Vector2(-0.52f, -0.52f), new Vector2(0.52f, 0.52f), 0.11f, _accentColor);
            AddSegment(mesh, new Vector2(-0.52f, 0.52f), new Vector2(0.52f, -0.52f), 0.11f, _accentColor);
            AddStar(mesh, Vector2.zero, 0.28f, 0.08f, Color.white);
        }

        private void DrawFade(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(-0.3f, 0.18f), 0.38f, 0.14f, 20f, 160f, _primaryColor);
            AddTriangle(mesh, new Vector2(-0.62f, 0.16f), new Vector2(0f, 0.16f), new Vector2(-0.3f, -0.64f), _primaryColor);
            AddArc(mesh, new Vector2(0.12f, 0.22f), 0.34f, 0.12f, 20f, 160f, WithAlpha(_accentColor, 0.62f));
            AddTriangle(mesh, new Vector2(-0.16f, 0.2f), new Vector2(0.4f, 0.2f), new Vector2(0.12f, -0.52f), WithAlpha(_accentColor, 0.48f));
            AddDiamond(mesh, new Vector2(0.58f, 0.34f), new Vector2(0.1f, 0.14f), WithAlpha(_accentColor, 0.38f));
            AddDiamond(mesh, new Vector2(0.68f, -0.14f), new Vector2(0.07f, 0.1f), WithAlpha(_accentColor, 0.22f));
        }

        private void DrawHunterArrow(VertexHelper mesh, HunterArrowGlyph glyph)
        {
            Vector2 from = new Vector2(-0.7f, -0.46f);
            Vector2 to = new Vector2(0.68f, 0.48f);
            AddArrow(mesh, from, to, 0.12f, _primaryColor, _accentColor);

            switch (glyph)
            {
                case HunterArrowGlyph.Chimera:
                    AddArc(mesh, new Vector2(-0.05f, 0.06f), 0.46f, 0.09f, 45f, 205f, _accentColor);
                    AddArc(mesh, new Vector2(0.08f, -0.04f), 0.46f, 0.09f, 225f, 385f, Color.white);
                    AddTriangle(mesh, new Vector2(0.34f, 0.52f), new Vector2(0.68f, 0.78f), new Vector2(0.62f, 0.34f), _accentColor);
                    break;
                case HunterArrowGlyph.Serpent:
                    AddArc(mesh, new Vector2(-0.08f, 0.02f), 0.42f, 0.1f, 40f, 215f, _accentColor);
                    AddArc(mesh, new Vector2(0.12f, -0.02f), 0.34f, 0.1f, 220f, 405f, _accentColor);
                    break;
                case HunterArrowGlyph.Viper:
                    AddDrop(mesh, new Vector2(0.04f, 0.02f), 0.48f, _accentColor);
                    AddDiamond(mesh, new Vector2(0.04f, -0.06f), new Vector2(0.11f, 0.18f), Color.white);
                    break;
                case HunterArrowGlyph.Scorpid:
                    AddArc(mesh, new Vector2(-0.06f, -0.06f), 0.48f, 0.12f, 205f, 445f, _accentColor);
                    AddTriangle(mesh, new Vector2(0.36f, 0.22f), new Vector2(0.62f, 0.06f), new Vector2(0.34f, -0.06f), Color.white);
                    AddCircle(mesh, new Vector2(-0.44f, -0.18f), 0.1f, _accentColor);
                    break;
                case HunterArrowGlyph.Aimed:
                    AddRing(mesh, new Vector2(0.08f, 0.04f), 0.46f, 0.1f, _accentColor);
                    AddSegment(mesh, new Vector2(0.08f, -0.58f), new Vector2(0.08f, -0.36f), 0.08f, Color.white);
                    AddSegment(mesh, new Vector2(0.08f, 0.44f), new Vector2(0.08f, 0.66f), 0.08f, Color.white);
                    break;
                case HunterArrowGlyph.Arcane:
                    AddStar(mesh, new Vector2(0.02f, 0f), 0.4f, 0.12f, _accentColor);
                    AddCircle(mesh, new Vector2(0.02f, 0f), 0.08f, Color.white);
                    break;
                case HunterArrowGlyph.Kill:
                    AddTriangle(mesh, new Vector2(-0.18f, 0.18f), new Vector2(0.32f, -0.68f), new Vector2(0.32f, 0.06f), _accentColor);
                    AddRing(mesh, new Vector2(0.22f, 0.12f), 0.26f, 0.09f, Color.white);
                    break;
                case HunterArrowGlyph.Steady:
                    AddSegment(mesh, new Vector2(-0.5f, -0.12f), new Vector2(0.12f, 0.3f), 0.07f, _accentColor);
                    AddSegment(mesh, new Vector2(-0.36f, -0.52f), new Vector2(0.28f, -0.08f), 0.07f, Color.white);
                    break;
                case HunterArrowGlyph.Freezing:
                    DrawSnowflake(mesh, new Vector2(0.04f, 0.02f), 0.38f, _accentColor);
                    break;
                case HunterArrowGlyph.Silencing:
                    AddRing(mesh, new Vector2(0.04f, 0.02f), 0.38f, 0.1f, _accentColor);
                    AddSegment(mesh, new Vector2(-0.28f, 0.4f), new Vector2(0.34f, -0.38f), 0.14f, Color.white);
                    break;
                case HunterArrowGlyph.Tranquilizing:
                    AddArc(mesh, new Vector2(0.02f, 0f), 0.4f, 0.1f, 35f, 325f, _accentColor);
                    AddSegment(mesh, new Vector2(-0.28f, -0.36f), new Vector2(0.34f, 0.34f), 0.12f, Color.white);
                    AddStar(mesh, new Vector2(0.42f, -0.2f), 0.16f, 0.05f, _accentColor);
                    break;
                case HunterArrowGlyph.Concussive:
                    AddCircle(mesh, new Vector2(0.12f, 0.04f), 0.26f, _accentColor);
                    AddArc(mesh, new Vector2(0.12f, 0.04f), 0.48f, 0.1f, 28f, 152f, Color.white);
                    AddArc(mesh, new Vector2(0.12f, 0.04f), 0.48f, 0.1f, 208f, 332f, Color.white);
                    break;
            }
        }

        private void DrawHuntersMark(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.55f, 0.11f, _primaryColor);
            AddRing(mesh, Vector2.zero, 0.22f, 0.08f, _accentColor);
            AddSegment(mesh, new Vector2(-0.82f, 0f), new Vector2(-0.54f, 0f), 0.09f, _accentColor);
            AddSegment(mesh, new Vector2(0.54f, 0f), new Vector2(0.82f, 0f), 0.09f, _accentColor);
            AddSegment(mesh, new Vector2(0f, -0.82f), new Vector2(0f, -0.54f), 0.09f, _accentColor);
            AddSegment(mesh, new Vector2(0f, 0.54f), new Vector2(0f, 0.82f), 0.09f, _accentColor);
            AddChevron(mesh, Vector2.zero, 0.34f, Color.white);
        }

        private void DrawScatterShot(VertexHelper mesh)
        {
            Vector2 origin = new Vector2(-0.7f, 0f);
            AddArrow(mesh, origin, new Vector2(0.68f, 0.58f), 0.1f, _primaryColor, _accentColor);
            AddArrow(mesh, origin, new Vector2(0.78f, 0f), 0.11f, _accentColor, Color.white);
            AddArrow(mesh, origin, new Vector2(0.68f, -0.58f), 0.1f, _primaryColor, _accentColor);
            AddCircle(mesh, origin, 0.1f, Color.white);
        }

        private void DrawFrostTrap(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.66f, 0.08f, WithAlpha(_primaryColor, 0.78f));
            AddTriangle(mesh, new Vector2(-0.66f, 0.04f), new Vector2(-0.18f, 0.34f), new Vector2(-0.18f, -0.28f), _primaryColor);
            AddTriangle(mesh, new Vector2(0.66f, 0.04f), new Vector2(0.18f, -0.28f), new Vector2(0.18f, 0.34f), _primaryColor);
            DrawSnowflake(mesh, Vector2.zero, 0.34f, _accentColor);
            AddSegment(mesh, new Vector2(-0.48f, -0.58f), new Vector2(0.48f, -0.58f), 0.11f, Color.white);
        }

        private void DrawFeignDeath(VertexHelper mesh)
        {
            AddCircle(mesh, new Vector2(-0.54f, -0.36f), 0.16f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.34f, -0.34f), new Vector2(0.5f, -0.38f), 0.2f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.04f, -0.32f), new Vector2(0.28f, 0.02f), 0.1f, _accentColor);
            AddArc(mesh, new Vector2(0.12f, 0.04f), 0.52f, 0.1f, 35f, 155f, WithAlpha(_accentColor, 0.72f));
            AddChevron(mesh, new Vector2(0.18f, 0.34f), 0.3f, Color.white);
        }

        private void DrawDisengage(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(0.14f, -0.06f), 0.66f, 0.12f, 45f, 235f, _primaryColor);
            AddTriangle(mesh, new Vector2(-0.46f, -0.4f), new Vector2(-0.82f, -0.5f), new Vector2(-0.58f, -0.12f), _accentColor);
            AddChevron(mesh, new Vector2(-0.1f, 0.08f), 0.46f, _accentColor);
            AddChevron(mesh, new Vector2(0.34f, 0.08f), 0.34f, Color.white);
        }

        private void DrawWingClip(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(-0.16f, 0.02f), 0.58f, 0.14f, 70f, 275f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.36f, 0.38f), new Vector2(0.3f, 0.18f), 0.11f, _accentColor);
            AddSegment(mesh, new Vector2(-0.42f, 0.04f), new Vector2(0.26f, -0.1f), 0.11f, _accentColor);
            AddSegment(mesh, new Vector2(-0.36f, -0.3f), new Vector2(0.2f, -0.36f), 0.11f, _accentColor);
            AddSegment(mesh, new Vector2(-0.62f, 0.58f), new Vector2(0.66f, -0.58f), 0.17f, Color.white);
        }

        private void DrawMastersCall(VertexHelper mesh)
        {
            AddPaw(mesh, new Vector2(-0.28f, 0f), 0.62f, _primaryColor);
            AddArc(mesh, new Vector2(0.08f, 0f), 0.52f, 0.1f, 250f, 425f, _accentColor);
            AddChevron(mesh, new Vector2(0.44f, 0f), 0.34f, _accentColor);
            AddStar(mesh, new Vector2(0.52f, 0.52f), 0.18f, 0.06f, Color.white);
        }

        private void DrawDeterrence(VertexHelper mesh)
        {
            AddShield(mesh, Vector2.zero, 0.9f, WithAlpha(_primaryColor, 0.7f));
            AddDagger(mesh, new Vector2(-0.16f, 0.02f), -38f, 0.92f, _accentColor);
            AddDagger(mesh, new Vector2(0.16f, 0.02f), 38f, 0.92f, Color.white);
            AddArc(mesh, Vector2.zero, 0.75f, 0.08f, 25f, 155f, _primaryColor);
        }

        private void DrawRoarOfSacrifice(VertexHelper mesh)
        {
            AddShield(mesh, new Vector2(-0.18f, -0.08f), 0.74f, WithAlpha(_primaryColor, 0.78f));
            AddPaw(mesh, new Vector2(-0.18f, -0.02f), 0.48f, _accentColor);
            AddArc(mesh, new Vector2(0.12f, 0.02f), 0.54f, 0.1f, -42f, 42f, Color.white);
            AddArc(mesh, new Vector2(0.12f, 0.02f), 0.76f, 0.1f, -42f, 42f, _primaryColor);
        }

        private void DrawReadiness(VertexHelper mesh)
        {
            AddArc(mesh, Vector2.zero, 0.62f, 0.13f, 35f, 190f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.62f, 0.13f, 215f, 370f, _accentColor);
            AddTriangle(mesh, new Vector2(-0.64f, -0.08f), new Vector2(-0.82f, -0.38f), new Vector2(-0.45f, -0.36f), _primaryColor);
            AddTriangle(mesh, new Vector2(0.64f, 0.08f), new Vector2(0.82f, 0.38f), new Vector2(0.45f, 0.36f), _accentColor);
            AddStar(mesh, Vector2.zero, 0.34f, 0.1f, Color.white);
        }

        private void DrawRapidFire(VertexHelper mesh)
        {
            AddArrow(mesh, new Vector2(-0.72f, 0.38f), new Vector2(0.7f, 0.58f), 0.09f, _primaryColor, _accentColor);
            AddArrow(mesh, new Vector2(-0.78f, 0f), new Vector2(0.78f, 0f), 0.11f, _accentColor, Color.white);
            AddArrow(mesh, new Vector2(-0.72f, -0.38f), new Vector2(0.7f, -0.58f), 0.09f, _primaryColor, _accentColor);
        }

        private void DrawPetCommand(VertexHelper mesh, PetCommandGlyph command)
        {
            AddPaw(mesh, new Vector2(-0.16f, 0f), 0.72f, _primaryColor);
            switch (command)
            {
                case PetCommandGlyph.Attack:
                    AddChevron(mesh, new Vector2(0.48f, 0f), 0.52f, _accentColor);
                    AddStar(mesh, new Vector2(0.52f, 0.54f), 0.16f, 0.05f, Color.white);
                    break;
                case PetCommandGlyph.Follow:
                    AddChevron(mesh, new Vector2(0.3f, 0.18f), 0.34f, _accentColor);
                    AddChevron(mesh, new Vector2(0.5f, -0.18f), 0.28f, Color.white);
                    break;
                case PetCommandGlyph.Stay:
                    AddRing(mesh, new Vector2(-0.16f, 0f), 0.72f, 0.1f, _accentColor);
                    AddSegment(mesh, new Vector2(0.52f, -0.5f), new Vector2(0.52f, 0.5f), 0.14f, Color.white);
                    break;
            }
        }

        private void DrawSpiderWeb(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.66f, 0.08f, _primaryColor);
            AddRing(mesh, Vector2.zero, 0.36f, 0.07f, _accentColor);
            for (int index = 0; index < 6; index++)
            {
                Vector2 direction = Direction(index * 60f);
                AddSegment(mesh, direction * 0.08f, direction * 0.74f, 0.07f, index % 2 == 0 ? _primaryColor : _accentColor);
            }

            AddCircle(mesh, Vector2.zero, 0.09f, Color.white);
        }

        private void DrawMendPet(VertexHelper mesh)
        {
            AddPaw(mesh, new Vector2(-0.26f, -0.08f), 0.62f, _primaryColor);
            AddRing(mesh, new Vector2(0.38f, 0.2f), 0.36f, 0.08f, WithAlpha(_accentColor, 0.78f));
            AddSegment(mesh, new Vector2(0.08f, 0.2f), new Vector2(0.68f, 0.2f), 0.14f, _accentColor);
            AddSegment(mesh, new Vector2(0.38f, -0.1f), new Vector2(0.38f, 0.5f), 0.14f, _accentColor);
            AddStar(mesh, new Vector2(0.58f, 0.58f), 0.15f, 0.05f, Color.white);
        }

        private void DrawFlare(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.62f, 0.08f, WithAlpha(_primaryColor, 0.8f));
            AddStar(mesh, Vector2.zero, 0.56f, 0.19f, _accentColor);
            AddCircle(mesh, Vector2.zero, 0.14f, Color.white);
            AddArc(mesh, new Vector2(0f, -0.12f), 0.82f, 0.07f, 205f, 335f, _primaryColor);
        }

        private void DrawDragonhawkAspect(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(-0.28f, 0f), 0.54f, 0.14f, 75f, 280f, _primaryColor);
            AddArc(mesh, new Vector2(0.28f, 0f), 0.54f, 0.14f, -100f, 105f, _primaryColor);
            AddDiamond(mesh, new Vector2(0f, -0.02f), new Vector2(0.2f, 0.38f), _accentColor);
            AddTriangle(mesh, new Vector2(-0.14f, 0.28f), new Vector2(0f, 0.76f), new Vector2(0.14f, 0.28f), Color.white);
            AddStar(mesh, new Vector2(0f, -0.48f), 0.18f, 0.06f, _accentColor);
        }

        private void DrawViperAspect(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(-0.12f, 0.24f), 0.46f, 0.14f, 20f, 225f, _primaryColor);
            AddArc(mesh, new Vector2(0.12f, -0.22f), 0.46f, 0.14f, 200f, 405f, _primaryColor);
            AddTriangle(mesh, new Vector2(0.24f, 0.5f), new Vector2(0.68f, 0.66f), new Vector2(0.5f, 0.24f), _accentColor);
            AddCircle(mesh, new Vector2(0.46f, 0.5f), 0.06f, Color.white);
            AddDrop(mesh, new Vector2(-0.44f, -0.36f), 0.22f, _accentColor);
        }

        private void DrawPrayerOfMending(VertexHelper mesh)
        {
            AddCircle(mesh, new Vector2(-0.48f, -0.12f), 0.16f, _primaryColor);
            AddCircle(mesh, new Vector2(0f, 0.32f), 0.16f, _accentColor);
            AddCircle(mesh, new Vector2(0.48f, -0.12f), 0.16f, _primaryColor);
            AddArc(mesh, new Vector2(0f, -0.08f), 0.52f, 0.1f, 25f, 155f, WithAlpha(_accentColor, 0.78f));
            AddArc(mesh, new Vector2(0f, -0.08f), 0.52f, 0.1f, 205f, 335f, WithAlpha(_primaryColor, 0.78f));
            AddSegment(mesh, new Vector2(-0.28f, -0.46f), new Vector2(0.28f, -0.46f), 0.13f, Color.white);
            AddSegment(mesh, new Vector2(0f, -0.72f), new Vector2(0f, -0.2f), 0.13f, Color.white);
        }

        private void DrawInnerFocus(VertexHelper mesh)
        {
            AddRing(mesh, Vector2.zero, 0.66f, 0.09f, _primaryColor);
            AddRing(mesh, Vector2.zero, 0.38f, 0.1f, _accentColor);
            AddDiamond(mesh, Vector2.zero, new Vector2(0.14f, 0.24f), Color.white);
            AddStar(mesh, Vector2.zero, 0.7f, 0.06f, WithAlpha(_accentColor, 0.72f));
        }

        private void DrawFearWard(VertexHelper mesh)
        {
            AddShield(mesh, Vector2.zero, 0.88f, _primaryColor);
            AddArc(mesh, new Vector2(0f, 0.02f), 0.34f, 0.11f, 25f, 155f, _accentColor);
            AddArc(mesh, new Vector2(0f, 0.02f), 0.34f, 0.11f, 205f, 335f, _accentColor);
            AddCircle(mesh, Vector2.zero, 0.08f, Color.white);
            AddSegment(mesh, new Vector2(-0.42f, 0.42f), new Vector2(0.42f, -0.42f), 0.12f, Color.white);
        }

        private void DrawScorch(VertexHelper mesh)
        {
            AddSegment(mesh, new Vector2(-0.72f, -0.34f), new Vector2(0.34f, 0.28f), 0.15f, _primaryColor);
            AddTriangle(mesh, new Vector2(0.12f, 0.12f), new Vector2(0.8f, 0.5f), new Vector2(0.48f, -0.18f), _accentColor);
            AddTriangle(mesh, new Vector2(-0.18f, -0.02f), new Vector2(-0.68f, 0.34f), new Vector2(-0.42f, -0.24f), _primaryColor);
            AddStar(mesh, new Vector2(0.28f, 0.2f), 0.18f, 0.06f, Color.white);
        }

        private void DrawPyroblast(VertexHelper mesh)
        {
            AddCircle(mesh, new Vector2(0.2f, 0.02f), 0.46f, _primaryColor);
            AddStar(mesh, new Vector2(0.2f, 0.02f), 0.34f, 0.11f, _accentColor);
            AddTriangle(mesh, new Vector2(-0.08f, 0.3f), new Vector2(-0.82f, 0.68f), new Vector2(-0.48f, -0.02f), _accentColor);
            AddTriangle(mesh, new Vector2(-0.08f, -0.12f), new Vector2(-0.72f, -0.62f), new Vector2(-0.38f, 0.04f), _primaryColor);
            AddRing(mesh, new Vector2(0.2f, 0.02f), 0.64f, 0.07f, WithAlpha(Color.white, 0.82f));
        }

        private void DrawConeOfCold(VertexHelper mesh)
        {
            AddTriangle(mesh, new Vector2(-0.74f, 0f), new Vector2(0.68f, 0.7f), new Vector2(0.68f, -0.7f), WithAlpha(_primaryColor, 0.62f));
            AddArc(mesh, new Vector2(-0.68f, 0f), 1.38f, 0.1f, -30f, 30f, _accentColor);
            DrawSnowflake(mesh, new Vector2(0.2f, 0f), 0.32f, Color.white);
        }

        private void DrawSpellsteal(VertexHelper mesh)
        {
            AddRing(mesh, new Vector2(-0.42f, 0f), 0.28f, 0.09f, _primaryColor);
            AddRing(mesh, new Vector2(0.42f, 0f), 0.28f, 0.09f, _accentColor);
            AddArrow(mesh, new Vector2(-0.18f, 0f), new Vector2(0.2f, 0f), 0.1f, Color.white, Color.white);
            AddStar(mesh, new Vector2(-0.42f, 0f), 0.17f, 0.05f, _accentColor);
            AddShield(mesh, new Vector2(0.42f, -0.02f), 0.34f, _primaryColor);
        }

        private void DrawIceBlock(VertexHelper mesh)
        {
            AddDiamond(mesh, Vector2.zero, new Vector2(0.58f, 0.8f), WithAlpha(_primaryColor, 0.8f));
            AddDiamond(mesh, Vector2.zero, new Vector2(0.34f, 0.58f), _accentColor);
            DrawSnowflake(mesh, Vector2.zero, 0.28f, Color.white);
            AddSegment(mesh, new Vector2(-0.58f, -0.48f), new Vector2(0.58f, 0.48f), 0.07f, WithAlpha(Color.white, 0.72f));
        }

        private void DrawManaShield(VertexHelper mesh)
        {
            AddShield(mesh, Vector2.zero, 0.88f, _primaryColor);
            AddDrop(mesh, new Vector2(0f, 0.02f), 0.54f, _accentColor);
            AddDiamond(mesh, new Vector2(0f, -0.06f), new Vector2(0.12f, 0.2f), Color.white);
            AddRing(mesh, Vector2.zero, 0.72f, 0.07f, WithAlpha(_accentColor, 0.75f));
        }

        private void DrawMoltenArmor(VertexHelper mesh)
        {
            AddShield(mesh, new Vector2(0f, -0.08f), 0.82f, WithAlpha(_primaryColor, 0.85f));
            AddTriangle(mesh, new Vector2(-0.44f, 0.04f), new Vector2(-0.22f, 0.7f), new Vector2(0.02f, 0.02f), _accentColor);
            AddTriangle(mesh, new Vector2(-0.12f, 0.02f), new Vector2(0.08f, 0.82f), new Vector2(0.28f, 0.02f), Color.white);
            AddTriangle(mesh, new Vector2(0.12f, 0.02f), new Vector2(0.44f, 0.62f), new Vector2(0.5f, -0.06f), _accentColor);
        }

        private void DrawMageArmor(VertexHelper mesh)
        {
            AddShield(mesh, Vector2.zero, 0.88f, _primaryColor);
            AddRing(mesh, new Vector2(0f, 0.02f), 0.38f, 0.09f, _accentColor);
            AddStar(mesh, new Vector2(0f, 0.02f), 0.32f, 0.1f, Color.white);
            AddDiamond(mesh, new Vector2(-0.48f, 0.38f), new Vector2(0.08f, 0.12f), _accentColor);
            AddDiamond(mesh, new Vector2(0.48f, 0.38f), new Vector2(0.08f, 0.12f), _accentColor);
        }

        private void DrawIceArmor(VertexHelper mesh)
        {
            AddShield(mesh, Vector2.zero, 0.88f, WithAlpha(_primaryColor, 0.86f));
            DrawSnowflake(mesh, Vector2.zero, 0.4f, _accentColor);
            AddDiamond(mesh, new Vector2(-0.46f, 0.22f), new Vector2(0.12f, 0.28f), Color.white);
            AddDiamond(mesh, new Vector2(0.46f, 0.22f), new Vector2(0.12f, 0.28f), Color.white);
        }

        private void DrawWaterElementalFreeze(VertexHelper mesh)
        {
            AddDrop(mesh, new Vector2(-0.28f, 0.02f), 0.78f, _primaryColor);
            AddRing(mesh, new Vector2(0.28f, -0.08f), 0.46f, 0.09f, _accentColor);
            DrawSnowflake(mesh, new Vector2(0.28f, -0.08f), 0.34f, Color.white);
            AddArc(mesh, new Vector2(0f, -0.26f), 0.72f, 0.08f, 205f, 335f, WithAlpha(_accentColor, 0.8f));
        }

        private void DrawColdSnap(VertexHelper mesh)
        {
            AddArc(mesh, Vector2.zero, 0.64f, 0.12f, 35f, 190f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.64f, 0.12f, 215f, 370f, _accentColor);
            AddTriangle(mesh, new Vector2(-0.64f, -0.08f), new Vector2(-0.84f, -0.4f), new Vector2(-0.42f, -0.36f), _primaryColor);
            AddTriangle(mesh, new Vector2(0.64f, 0.08f), new Vector2(0.84f, 0.4f), new Vector2(0.42f, 0.36f), _accentColor);
            DrawSnowflake(mesh, Vector2.zero, 0.32f, Color.white);
        }

        private void DrawSap(VertexHelper mesh)
        {
            AddCircle(mesh, new Vector2(-0.18f, -0.1f), 0.32f, _primaryColor);
            AddArc(mesh, new Vector2(-0.18f, 0.12f), 0.42f, 0.13f, 20f, 160f, _primaryColor);
            AddStar(mesh, new Vector2(0.42f, 0.38f), 0.24f, 0.08f, _accentColor);
            AddStar(mesh, new Vector2(0.58f, -0.12f), 0.15f, 0.05f, Color.white);
            AddArc(mesh, new Vector2(0.1f, 0f), 0.64f, 0.07f, -55f, 55f, WithAlpha(_accentColor, 0.75f));
        }

        private void DrawGarrote(VertexHelper mesh)
        {
            AddArc(mesh, new Vector2(0f, 0.12f), 0.48f, 0.12f, 20f, 160f, _primaryColor);
            AddSegment(mesh, new Vector2(-0.62f, -0.08f), new Vector2(0.62f, -0.08f), 0.11f, Color.white);
            AddDagger(mesh, new Vector2(-0.18f, -0.24f), 42f, 0.76f, _primaryColor);
            AddDrop(mesh, new Vector2(0.5f, -0.42f), 0.24f, _accentColor);
        }

        private void DrawGouge(VertexHelper mesh)
        {
            AddArc(mesh, Vector2.zero, 0.58f, 0.14f, 25f, 155f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.58f, 0.14f, 205f, 335f, _primaryColor);
            AddCircle(mesh, Vector2.zero, 0.1f, Color.white);
            AddDagger(mesh, new Vector2(0.2f, -0.12f), 38f, 0.72f, _accentColor);
            AddStar(mesh, new Vector2(-0.42f, 0.42f), 0.16f, 0.05f, _accentColor);
        }

        private void DrawSprint(VertexHelper mesh)
        {
            AddChevron(mesh, new Vector2(-0.42f, 0.28f), 0.44f, WithAlpha(_primaryColor, 0.48f));
            AddChevron(mesh, new Vector2(-0.02f, 0.08f), 0.54f, _primaryColor);
            AddChevron(mesh, new Vector2(0.42f, -0.12f), 0.62f, _accentColor);
            AddSegment(mesh, new Vector2(-0.78f, -0.5f), new Vector2(0.46f, -0.5f), 0.08f, Color.white);
        }

        private void DrawDismantle(VertexHelper mesh)
        {
            AddDagger(mesh, new Vector2(-0.22f, 0.06f), 42f, 0.88f, _primaryColor);
            AddDagger(mesh, new Vector2(0.22f, 0.06f), 138f, 0.88f, _accentColor);
            AddSegment(mesh, new Vector2(-0.18f, 0.22f), new Vector2(0.08f, -0.02f), 0.12f, Color.white);
            AddSegment(mesh, new Vector2(-0.02f, -0.08f), new Vector2(0.22f, -0.34f), 0.12f, Color.white);
            AddTriangle(mesh, new Vector2(-0.12f, -0.12f), new Vector2(0.02f, -0.5f), new Vector2(0.16f, -0.1f), _accentColor);
        }

        private void DrawPreparation(VertexHelper mesh)
        {
            AddArc(mesh, Vector2.zero, 0.66f, 0.11f, 35f, 190f, _primaryColor);
            AddArc(mesh, Vector2.zero, 0.66f, 0.11f, 215f, 370f, _accentColor);
            AddTriangle(mesh, new Vector2(-0.66f, -0.08f), new Vector2(-0.84f, -0.38f), new Vector2(-0.44f, -0.36f), _primaryColor);
            AddTriangle(mesh, new Vector2(0.66f, 0.08f), new Vector2(0.84f, 0.38f), new Vector2(0.44f, 0.36f), _accentColor);
            AddDagger(mesh, new Vector2(-0.12f, 0f), 45f, 0.66f, Color.white);
            AddDagger(mesh, new Vector2(0.12f, 0f), 135f, 0.66f, _accentColor);
        }

        private void DrawSnowflake(VertexHelper mesh, Vector2 center, float radius, Color glyphColor)
        {
            for (int index = 0; index < 3; index++)
            {
                float angle = index * 60f;
                Vector2 direction = Direction(angle);
                AddSegment(mesh, center - (direction * radius), center + (direction * radius), 0.1f, glyphColor);
            }
        }

        private void AddArrow(
            VertexHelper mesh,
            Vector2 from,
            Vector2 to,
            float width,
            Color shaftColor,
            Color headColor)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 neck = to - (direction * 0.34f);
            AddSegment(mesh, from, neck + (direction * 0.08f), width, shaftColor);
            AddTriangle(mesh, to, neck + (perpendicular * 0.24f), neck - (perpendicular * 0.24f), headColor);
            AddSegment(mesh, from, from - (direction * 0.18f) + (perpendicular * 0.2f), width * 0.72f, headColor);
            AddSegment(mesh, from, from - (direction * 0.18f) - (perpendicular * 0.2f), width * 0.72f, headColor);
        }

        private void AddPaw(VertexHelper mesh, Vector2 center, float size, Color glyphColor)
        {
            AddCircle(mesh, center + new Vector2(0f, -0.13f) * size, 0.3f * size, glyphColor, 14);
            AddCircle(mesh, center + new Vector2(-0.34f, 0.26f) * size, 0.15f * size, glyphColor, 12);
            AddCircle(mesh, center + new Vector2(-0.11f, 0.43f) * size, 0.15f * size, glyphColor, 12);
            AddCircle(mesh, center + new Vector2(0.14f, 0.43f) * size, 0.15f * size, glyphColor, 12);
            AddCircle(mesh, center + new Vector2(0.36f, 0.24f) * size, 0.15f * size, glyphColor, 12);
        }

        private void AddShield(VertexHelper mesh, Vector2 center, float size, Color glyphColor)
        {
            Vector2 topLeft = center + new Vector2(-0.58f, 0.52f) * size;
            Vector2 topRight = center + new Vector2(0.58f, 0.52f) * size;
            Vector2 middleRight = center + new Vector2(0.5f, -0.14f) * size;
            Vector2 bottom = center + new Vector2(0f, -0.72f) * size;
            Vector2 middleLeft = center + new Vector2(-0.5f, -0.14f) * size;
            AddTriangle(mesh, topLeft, topRight, middleRight, glyphColor);
            AddTriangle(mesh, topLeft, middleRight, middleLeft, glyphColor);
            AddTriangle(mesh, middleLeft, middleRight, bottom, glyphColor);
        }

        private void AddDagger(
            VertexHelper mesh,
            Vector2 center,
            float angleDegrees,
            float length,
            Color glyphColor)
        {
            Vector2 forward = Direction(angleDegrees);
            Vector2 side = new Vector2(-forward.y, forward.x);
            Vector2 tip = center + (forward * length * 0.58f);
            Vector2 heel = center - (forward * length * 0.42f);
            AddTriangle(
                mesh,
                tip,
                heel + (side * length * 0.14f),
                heel - (side * length * 0.14f),
                glyphColor);
            AddSegment(mesh, heel - (side * length * 0.22f), heel + (side * length * 0.22f), length * 0.08f, _accentColor);
            AddSegment(mesh, heel, heel - (forward * length * 0.2f), length * 0.1f, glyphColor);
        }

        private void AddDrop(VertexHelper mesh, Vector2 center, float size, Color glyphColor)
        {
            AddCircle(mesh, center + new Vector2(0f, -size * 0.14f), size * 0.48f, glyphColor, 12);
            AddTriangle(
                mesh,
                center + new Vector2(0f, size * 0.65f),
                center + new Vector2(-size * 0.42f, -size * 0.05f),
                center + new Vector2(size * 0.42f, -size * 0.05f),
                glyphColor);
        }

        private void AddChevron(VertexHelper mesh, Vector2 center, float size, Color glyphColor)
        {
            AddSegment(
                mesh,
                center + new Vector2(-size * 0.5f, size * 0.55f),
                center + new Vector2(size * 0.42f, 0f),
                size * 0.24f,
                glyphColor);
            AddSegment(
                mesh,
                center + new Vector2(size * 0.42f, 0f),
                center + new Vector2(-size * 0.5f, -size * 0.55f),
                size * 0.24f,
                glyphColor);
        }

        private void AddStar(
            VertexHelper mesh,
            Vector2 center,
            float outerRadius,
            float innerRadius,
            Color glyphColor)
        {
            for (int index = 0; index < 8; index++)
            {
                Vector2 outer = center + (Direction(index * 45f) * outerRadius);
                Vector2 innerA = center + (Direction((index * 45f) - 15f) * innerRadius);
                Vector2 innerB = center + (Direction((index * 45f) + 15f) * innerRadius);
                AddTriangle(mesh, center, innerA, outer, glyphColor);
                AddTriangle(mesh, center, outer, innerB, glyphColor);
            }
        }

        private void AddDiamond(VertexHelper mesh, Vector2 center, Vector2 radius, Color glyphColor)
        {
            AddTriangle(
                mesh,
                center + new Vector2(0f, radius.y),
                center + new Vector2(radius.x, 0f),
                center + new Vector2(0f, -radius.y),
                glyphColor);
            AddTriangle(
                mesh,
                center + new Vector2(0f, radius.y),
                center + new Vector2(0f, -radius.y),
                center + new Vector2(-radius.x, 0f),
                glyphColor);
        }

        private void AddSegment(
            VertexHelper mesh,
            Vector2 from,
            Vector2 to,
            float width,
            Color glyphColor)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < 0.000001f)
            {
                return;
            }

            Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            AddQuad(mesh, from - perpendicular, from + perpendicular, to + perpendicular, to - perpendicular, glyphColor);
        }

        private void AddCircle(
            VertexHelper mesh,
            Vector2 center,
            float radius,
            Color glyphColor,
            int segments = CircleSegments)
        {
            int centerIndex = mesh.currentVertCount;
            AddVertex(mesh, center, glyphColor);
            for (int index = 0; index <= segments; index++)
            {
                AddVertex(mesh, center + (Direction(index * 360f / segments) * radius), glyphColor);
            }

            for (int index = 0; index < segments; index++)
            {
                mesh.AddTriangle(centerIndex, centerIndex + index + 1, centerIndex + index + 2);
            }
        }

        private void AddRing(
            VertexHelper mesh,
            Vector2 center,
            float radius,
            float width,
            Color glyphColor)
        {
            AddArc(mesh, center, radius, width, 0f, 360f, glyphColor);
        }

        private void AddArc(
            VertexHelper mesh,
            Vector2 center,
            float radius,
            float width,
            float startDegrees,
            float endDegrees,
            Color glyphColor)
        {
            float sweep = endDegrees - startDegrees;
            int segments = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweep) / 18f));
            float inner = Mathf.Max(0f, radius - (width * 0.5f));
            float outer = radius + (width * 0.5f);
            int start = mesh.currentVertCount;
            for (int index = 0; index <= segments; index++)
            {
                float angle = startDegrees + (sweep * index / segments);
                Vector2 direction = Direction(angle);
                AddVertex(mesh, center + (direction * inner), glyphColor);
                AddVertex(mesh, center + (direction * outer), glyphColor);
            }

            for (int index = 0; index < segments; index++)
            {
                int current = start + (index * 2);
                mesh.AddTriangle(current, current + 1, current + 3);
                mesh.AddTriangle(current, current + 3, current + 2);
            }
        }

        private void AddTriangle(
            VertexHelper mesh,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Color glyphColor)
        {
            int start = mesh.currentVertCount;
            AddVertex(mesh, first, glyphColor);
            AddVertex(mesh, second, glyphColor);
            AddVertex(mesh, third, glyphColor);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        private void AddQuad(
            VertexHelper mesh,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Vector2 fourth,
            Color glyphColor)
        {
            int start = mesh.currentVertCount;
            AddVertex(mesh, first, glyphColor);
            AddVertex(mesh, second, glyphColor);
            AddVertex(mesh, third, glyphColor);
            AddVertex(mesh, fourth, glyphColor);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private void AddVertex(VertexHelper mesh, Vector2 normalized, Color glyphColor)
        {
            Rect bounds = rectTransform.rect;
            float scale = Mathf.Min(bounds.width, bounds.height) * 0.44f;
            Vector2 center = bounds.center;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = center + (normalized * scale);
            vertex.color = glyphColor * color;
            mesh.AddVert(vertex);
        }

        private static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static Color WithAlpha(Color source, float alpha)
        {
            source.a = alpha;
            return source;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            raycastTarget = false;
            SetVerticesDirty();
        }
#endif
    }
}
