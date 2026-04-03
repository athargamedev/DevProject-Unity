using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Network_Game.Dialogue.Editor
{
    /// <summary>
    /// Scans the curated animation metadata table, creates/updates one
    /// AnimationDefinition asset per entry, and rebuilds the AnimationCatalog list.
    ///
    /// Run via:  Tools → Network Game → Rebuild Animation Catalog
    ///
    /// Re-entrant: running it again updates fields without losing existing GUIDs.
    /// </summary>
    public static class AnimationCatalogBuilder
    {
        private const string DefFolder   = "Assets/Network_Game/Dialogue/Animations/AnimationsDefinitions";
        private const string CatalogPath = "Assets/Network_Game/Dialogue/Animations/AnimationCatalog.asset";

        // ── Animation metadata table ─────────────────────────────────────────
        // stateName = the Unity Animator state to CrossFade to.
        //   • States already in modelAndre_Animator:
        //       Locomotion, Jump, Falling, Land, Attack, Death, Emote
        //   • All other stateName values will need matching states added to the
        //     animator controller before they play (gracefully skipped if absent).
        // ─────────────────────────────────────────────────────────────────────
        private readonly struct AnimMeta
        {
            public readonly string Tag;
            public readonly string Description;
            public readonly string StateName;
            public readonly float  CrossFade;
            public readonly string[] Alts;

            public AnimMeta(string tag, string desc, string stateName,
                            float cf, params string[] alts)
            {
                Tag         = tag;
                Description = desc;
                StateName   = stateName;
                CrossFade   = cf;
                Alts        = alts;
            }
        }

        private static readonly AnimMeta[] k_Defs =
        {
            // ── Social / Greeting ─────────────────────────────────────────────
            new AnimMeta("Wave",
                "Wave hand in greeting — hello, goodbye, drawing someone's attention.",
                "Wave", 0.15f,
                "Hello", "Hi", "Greet", "Goodbye", "Bye", "HeyThere"),

            new AnimMeta("Cheer",
                "Raise arms in triumph — victory, celebration, joy, excitement.",
                "Cheer", 0.15f,
                "Celebrate", "Victory", "Hooray", "Excited", "Triumph"),

            new AnimMeta("Clap",
                "Clap hands in applause — appreciation, agreement, encouragement.",
                "Clap", 0.15f,
                "Applaud", "Bravo", "WellDone", "Approve"),

            new AnimMeta("Taunt",
                "Taunt or mock — challenge, cocky bravado, provocative gesture.",
                "Taunt", 0.12f,
                "Mock", "Challenge", "Brag", "ShowOff", "Tease"),

            new AnimMeta("PowerUp",
                "Power-surge pose — sudden confidence, energized, ready for action.",
                "PowerUp", 0.15f,
                "Energize", "PowerSurge", "GetReady", "Fired", "PumpUp"),

            // ── Dance / Emotes ────────────────────────────────────────────────
            new AnimMeta("HipHopDance",
                "Break into a hip-hop groove — playful, celebratory, high energy.",
                "HipHopDance", 0.20f,
                "Dance", "HipHop", "Groove", "BreakOut"),

            new AnimMeta("GangnamStyle",
                "Gangnam Style horse-riding dance — humorous, lighthearted, comedic.",
                "GangnamStyle", 0.20f,
                "Gangnam", "FunnyDance", "HorseDance"),

            new AnimMeta("Moonwalk",
                "Moonwalk backwards — showing off, stylish exit, comedic.",
                "Moonwalk", 0.20f,
                "MoonWalk", "SlideBack", "BackSlide"),

            new AnimMeta("Breakdance",
                "Acrobatic breakdance — high energy, skilled showing-off, excited.",
                "Breakdance", 0.20f,
                "BreakDance", "BGirl", "BBoy", "Spin"),

            new AnimMeta("Salsa",
                "Salsa dance — joyful, festive, celebratory Latin rhythm.",
                "Salsa", 0.20f,
                "SalsaDance", "LatinDance", "Fiesta"),

            new AnimMeta("RobotDance",
                "Robot dance — quirky, stiff, mechanical humor.",
                "RobotDance", 0.20f,
                "Robot", "RoboMove", "Mechanical"),

            new AnimMeta("Twerk",
                "Twerk dance — playful, energetic, comedic performance.",
                "Twerk", 0.20f,
                "TwerkDance", "BounceDown"),

            new AnimMeta("Thriller",
                "Thriller zombie dance — eerie, horror-themed, comedic undead.",
                "Thriller", 0.20f,
                "ZombieDance", "HorrorDance", "Undead"),

            // ── Magical / Supernatural ────────────────────────────────────────
            new AnimMeta("CastingSpell",
                "Cast a magical spell — mystical gesture, arcane power, conjuring.",
                "CastingSpell", 0.15f,
                "CastMagic", "Spell", "Magic", "Conjure", "Enchant"),

            new AnimMeta("SuperheroIdle",
                "Heroic standing pose — powerful, confident, cape-billowing authority.",
                "SuperheroIdle", 0.20f,
                "HeroStance", "Heroic", "Powerful", "MightyPose"),

            new AnimMeta("SuperheroLanding",
                "Superhero landing — dramatic impact-crouch arrival, imposing entrance.",
                "SuperheroLanding", 0.15f,
                "HeroLand", "DramaticLand", "CrouchImpact"),

            new AnimMeta("Floating",
                "Levitate and float — ethereal, magical, weightless suspension.",
                "Floating", 0.30f,
                "Levitate", "Float", "Hover", "Ascend"),

            new AnimMeta("Flying",
                "Soar through the air — magical power, freedom, dramatic flight.",
                "Flying", 0.30f,
                "Fly", "Soar", "Aerial", "Glide"),

            // ── Sport / Athletic ──────────────────────────────────────────────
            new AnimMeta("SoccerKick",
                "Soccer kick — forceful, athletic, passionate strike.",
                "SoccerKick", 0.12f,
                "FootballKick", "PenaltyKick", "Strike", "Shoot"),

            new AnimMeta("Basketball",
                "Basketball dribble move — athletic, playful, sporty vibe.",
                "Basketball", 0.15f,
                "Dribble", "Bball", "HoopMove"),

            new AnimMeta("BaseballPitch",
                "Baseball pitch — athletic wind-up and throw, focused and forceful.",
                "BaseballPitch", 0.15f,
                "Pitch", "ThrowBall", "BaseballThrow"),

            new AnimMeta("GolfSwing",
                "Golf swing — calm, precise, casual sport gesture.",
                "GolfSwing", 0.20f,
                "Golf", "Swing", "PutterSwing"),

            new AnimMeta("TennisServe",
                "Tennis serve — overhead smash, precise athletic motion.",
                "TennisServe", 0.15f,
                "Tennis", "Serve", "OverheadSmash"),

            new AnimMeta("Swimming",
                "Swimming motion — fluid, graceful, aquatic movement.",
                "Swimming", 0.30f,
                "Swim", "Paddle", "Stroke"),

            // ── Combat / Action ───────────────────────────────────────────────
            new AnimMeta("Attack",
                "Launch an attack — strike forward, aggressive offensive action.",
                "Attack", 0.10f,
                "Strike", "Hit", "Swing", "SlashAttack"),

            new AnimMeta("Boxing",
                "Box in a fighting stance — combative, aggressive, ready to brawl.",
                "Boxing", 0.12f,
                "Fight", "Spar", "Brawl", "PunchCombo"),

            new AnimMeta("Punch",
                "Single decisive punch — anger, firm point made, striking blow.",
                "Punch", 0.10f,
                "Jab", "Uppercut", "Haymaker"),

            new AnimMeta("Kick",
                "Martial-arts kick — disciplined combat, anger, sharp action.",
                "Kick", 0.10f,
                "KickAttack", "RoundKick", "SidekKick"),

            new AnimMeta("Block",
                "Defensive guard stance — cautious, protecting, wary, on edge.",
                "Block", 0.10f,
                "Defend", "Guard", "Shield", "Parry"),

            new AnimMeta("BigBodyBlow",
                "Stagger from a massive hit — pain, shock, knocked back by force.",
                "BigBodyBlow", 0.08f,
                "HitReaction", "Stagger", "Recoil", "Struck", "Ouch"),

            // ── Situational / State ───────────────────────────────────────────
            new AnimMeta("Jump",
                "Jump up — surprise, excitement, burst of energy.",
                "Jump", 0.10f,
                "Leap", "JumpUp", "Bounce", "SpringUp"),

            new AnimMeta("SitDown",
                "Sit down — relaxed, tired, story-time, contemplative pause.",
                "SitDown", 0.20f,
                "Sit", "TakeASeat", "Rest", "Settle"),

            new AnimMeta("StandUp",
                "Stand up from seated — ready, attentive, rising to meet a challenge.",
                "StandUp", 0.20f,
                "Rise", "GetUp", "Stand", "Arise"),

            new AnimMeta("Lifting",
                "Lift something heavy — effort, strength, laborious hard work.",
                "Lifting", 0.15f,
                "Lift", "HeavyLift", "CarryWeight", "PickUp"),

            new AnimMeta("CrouchIdle",
                "Crouch in place — sneaky, cautious, hiding, lying in wait.",
                "CrouchIdle", 0.20f,
                "Crouch", "Sneak", "Hide", "LowProfile", "Cower"),

            new AnimMeta("HardLanding",
                "Heavy crash landing — dramatic impact, painful fall, stumble.",
                "HardLanding", 0.10f,
                "CrashLand", "HardImpact", "BrutalLand"),

            new AnimMeta("FallingToRoll",
                "Roll out of a fall — acrobatic, nimble, skilled recovery.",
                "FallingToRoll", 0.10f,
                "TuckAndRoll", "AcrobaticFall", "ParkourRoll"),

            // ── Death / Defeat ────────────────────────────────────────────────
            new AnimMeta("Death",
                "Collapse and die — dramatic defeat, life leaving the body.",
                "Death", 0.15f,
                "Die", "Collapse", "FallDead", "Perish", "KilledDead"),

            new AnimMeta("FlyingBackDeath",
                "Blasted backward — overwhelming force, thrown off feet, extreme hit.",
                "FlyingBackDeath", 0.10f,
                "BlastBack", "KnockedOut", "BlownAway", "SendFlying"),

            // ── Idle / Locomotion reference ───────────────────────────────────
            new AnimMeta("Idle",
                "Stand still in a neutral pose — waiting, at rest, calm default.",
                "Locomotion", 0.30f,
                "StandStill", "Wait", "Neutral", "AtRest"),
        };

        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Network Game/Rebuild Animation Catalog")]
        public static void Build()
        {
            // Ensure definitions subfolder exists
            if (!AssetDatabase.IsValidFolder(DefFolder))
            {
                string parent = Path.GetDirectoryName(DefFolder);
                string child  = Path.GetFileName(DefFolder);
                AssetDatabase.CreateFolder(parent, child);
            }

            // Load or create the catalog asset
            var catalog = AssetDatabase.LoadAssetAtPath<AnimationCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AnimationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.allAnimations.Clear();

            int created = 0, updated = 0;

            foreach (AnimMeta m in k_Defs)
            {
                string path = $"{DefFolder}/AnimDef_{m.Tag}.asset";
                var def = AssetDatabase.LoadAssetAtPath<AnimationDefinition>(path);
                bool isNew = def == null;

                if (isNew)
                {
                    def = ScriptableObject.CreateInstance<AnimationDefinition>();
                    AssetDatabase.CreateAsset(def, path);
                    created++;
                }
                else
                {
                    updated++;
                }

                def.animTag          = m.Tag;
                def.description      = m.Description;
                def.stateName        = m.StateName;
                def.crossFadeDuration = m.CrossFade;
                def.alternativeTags  = m.Alts;
                def.name             = $"AnimDef_{m.Tag}";

                EditorUtility.SetDirty(def);
                catalog.allAnimations.Add(def);
            }

            catalog.RebuildLookup();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AnimationCatalogBuilder] Rebuilt AnimationCatalog.\n" +
                      $"  Created: {created} new definitions\n" +
                      $"  Updated: {updated} existing definitions\n" +
                      $"  Catalog total: {catalog.allAnimations.Count} entries\n" +
                      $"  Path: {CatalogPath}");
        }
    }
}
