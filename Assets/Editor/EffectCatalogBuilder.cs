using System.IO;
using Network_Game.Dialogue.Effects;
using UnityEditor;
using UnityEngine;

namespace Network_Game.Dialogue.Editor
{
    /// <summary>
    /// Scans the curated effect metadata table, creates/updates one EffectDefinition
    /// asset per entry (with prefab resolved from ParticlePack), and rebuilds the EffectCatalog list.
    ///
    /// Run via:  Tools → Network Game → Rebuild Effect Catalog
    ///
    /// Re-entrant: running again updates all fields without losing existing GUIDs.
    /// Prefab assignment: if the prefab is found in ParticlePack it is assigned;
    /// if not found, any existing prefab reference on the asset is preserved.
    ///
    /// Filename convention: Tag with spaces stripped, e.g. "Fire Blast" → "FireBlast.asset"
    /// (matches the 6 hand-authored assets already in the folder).
    /// </summary>
    public static class EffectCatalogBuilder
    {
        private const string DefFolder   = "Assets/Network_Game/Dialogue/Effects/EffectDefinitions";
        private const string CatalogPath = "Assets/Network_Game/Dialogue/Effects/EffectCatalog.asset";

        // All ParticlePack sub-folders the prefab search will walk through.
        private static readonly string[] k_SearchFolders =
        {
            "Assets/Network_Game/ParticlePack/EffectExamples/Fire & Explosion Effects/Prefabs/",
            "Assets/Network_Game/ParticlePack/EffectExamples/Misc Effects/Prefabs/",
            "Assets/Network_Game/ParticlePack/EffectExamples/Smoke & Steam Effects/Prefabs/",
            "Assets/Network_Game/ParticlePack/EffectExamples/Water Effects/Prefabs/",
            "Assets/Network_Game/ParticlePack/EffectExamples/Magic Effects/Prefabs/",
            "Assets/Network_Game/ParticlePack/EffectExamples/Legacy Particles/Prefabs/",
            "Assets/Network_Game/ParticlePack/EffectExamples/Weapon Effects/Prefabs/",
            "Assets/Network_Game/ParticlePack/EffectExamples/Goop Effects/Prefabs/",
            "Assets/Network_Game/ParticlePack/EffectExamples/Ice Effects/Prefabs/",
            "Assets/Network_Game/ParticlePack/EffectExamples/Storm Effects/Prefabs/",
        };

        // ── Effect metadata table ─────────────────────────────────────────────
        // PlacementMode: 0=Auto 1=AttachMesh 2=GroundAoe 3=SkyVolume 4=Projectile
        // TargetType:    0=Auto 1=Player    2=Floor     3=Npc
        // ─────────────────────────────────────────────────────────────────────
        private readonly struct EffMeta
        {
            public readonly string   Tag;
            public readonly string   Description;
            public readonly string   PrefabName;
            public readonly float    Scale;
            public readonly float    Duration;
            public readonly float    R, G, B;
            public readonly int      PlacementMode;
            public readonly int      TargetType;
            public readonly bool     EnableDamage;
            public readonly bool     EnableHoming;
            public readonly float    ProjectileSpeed;
            public readonly float    HomingTurnRate;
            public readonly float    DamageAmount;
            public readonly float    DamageRadius;
            public readonly string   DamageType;
            public readonly string[] Alts;

            public EffMeta(
                string tag, string desc, string prefab,
                float scale, float dur, float r, float g, float b,
                int placement, int target,
                bool dmg, bool homing, float speed, float turn,
                float dmgAmt, float dmgRad, string dmgType,
                params string[] alts)
            {
                Tag             = tag;
                Description     = desc;
                PrefabName      = prefab;
                Scale           = scale;
                Duration        = dur;
                R = r; G = g; B = b;
                PlacementMode   = placement;
                TargetType      = target;
                EnableDamage    = dmg;
                EnableHoming    = homing;
                ProjectileSpeed = speed;
                HomingTurnRate  = turn;
                DamageAmount    = dmgAmt;
                DamageRadius    = dmgRad;
                DamageType      = dmgType;
                Alts            = alts;
            }
        }

        private static readonly EffMeta[] k_Defs =
        {
            // ── Fire & Explosive ──────────────────────────────────────────────

            new EffMeta("Fire Blast",
                "Eruption of molten flame that scorches the ground ahead. Hurls a fireball at the player.",
                "FireBall",
                1f, 4f, 1f, 0.6f, 0.2f,
                0, 0, true, true, 12f, 200f, 14f, 1.2f, "fire",
                "fireball", "burn", "incinerate", "fire"),

            new EffMeta("Big Explosion",
                "Massive detonation engulfs the area in flame and debris. Devastating area-of-effect blast.",
                "BigExplosion",
                1.5f, 3f, 1f, 0.7f, 0.2f,
                2, 2, true, false, 0f, 0f, 20f, 3f, "fire",
                "explosion", "detonate", "blast", "kaboom", "boom"),

            new EffMeta("Energy Burst",
                "Pure arcane energy erupts outward — crackling detonation with blinding light.",
                "EnergyExplosion",
                1.2f, 2.5f, 0.7f, 0.5f, 1f,
                2, 2, true, false, 0f, 0f, 18f, 2.5f, "arcane",
                "energy", "arcane blast", "power burst", "dispel"),

            new EffMeta("Plasma Blast",
                "Superheated plasma orb that detonates on contact — volatile, radiant destruction.",
                "PlasmaExplosionEffect",
                1f, 2f, 0.7f, 0.4f, 1f,
                0, 0, true, true, 14f, 180f, 16f, 1.5f, "plasma",
                "plasma", "plasma ball", "energy bolt", "charged shot"),

            new EffMeta("Wild Fire",
                "Raging conflagration spreads across the ground — relentless, consuming inferno.",
                "WildFire",
                1.5f, 8f, 1f, 0.4f, 0.1f,
                2, 2, true, false, 0f, 0f, 10f, 2f, "fire",
                "wildfire", "spread fire", "conflagration", "inferno", "raging fire"),

            new EffMeta("Flame Stream",
                "Sustained stream of fire that tracks toward the player. Scorching continuous burn.",
                "FlameStream",
                1f, 5f, 1f, 0.5f, 0.1f,
                0, 1, true, true, 8f, 120f, 8f, 0.8f, "fire",
                "fire beam", "flame jet", "fire breath", "scorch"),

            new EffMeta("Flamethrower",
                "Continuous burst of flame — unrelenting spray of fire in a wide cone.",
                "FlameThrower",
                1f, 4f, 1f, 0.5f, 0f,
                0, 0, true, false, 0f, 0f, 8f, 1.2f, "fire",
                "flamethrower", "fire spray", "torch", "flame burst"),

            new EffMeta("Inferno",
                "Towering wall of roaring flame — overwhelming, furnace-heat destruction.",
                "LargeFlames",
                1.5f, 5f, 1f, 0.4f, 0f,
                2, 2, true, false, 0f, 0f, 12f, 2f, "fire",
                "large fire", "blaze", "wall of fire", "pillar of flame"),

            // ── Ice ───────────────────────────────────────────────────────────

            new EffMeta("Ice Lance",
                "Frozen crystalline lance that shatters into frost shards. Launches at the player with piercing cold.",
                "IceLance",
                0.8f, 3f, 0.7f, 0.9f, 1f,
                1, 1, true, true, 16f, 180f, 12f, 1f, "cold",
                "freeze", "ice", "frost", "glacial", "frostbolt"),

            // ── Storm / Lightning ─────────────────────────────────────────────

            new EffMeta("Lightning Storm",
                "Crackling lightning bolt that arcs toward the player, leaving a trail of sparks. Calls down a storm strike from above.",
                "LightnigStormCloud",
                1.2f, 5f, 0.6f, 0.85f, 1f,
                0, 0, true, true, 14f, 200f, 15f, 1.5f, "electric",
                "lightning", "thunder", "storm", "electrify"),

            new EffMeta("Electrical Sparks",
                "Arcing electrical discharge — crackling sparks that shock anything nearby.",
                "ElectricalSparks",
                0.8f, 3f, 0.7f, 0.9f, 1f,
                1, 1, true, false, 0f, 0f, 8f, 1f, "electric",
                "spark", "sparks", "arc", "zap", "shock", "electrocute"),

            // ── Forge ─────────────────────────────────────────────────────────

            new EffMeta("Forge Sparks",
                "Shower of white-hot sparks from an invisible anvil. Creates a burst of electrical discharge.",
                "ElectricalSparks",
                0.9f, 3f, 1f, 0.8f, 0.3f,
                0, 0, false, false, 0f, 0f, 0f, 1f, "effect",
                "spark", "sparks", "electric", "electrical discharge", "discharge"),

            // ── Water ─────────────────────────────────────────────────────────

            new EffMeta("Waterfall",
                "Cascading torrent of water falls from above — dramatic, overwhelming aquatic force.",
                "WaterFall",
                1.2f, 6f, 0.3f, 0.6f, 1f,
                3, 0, false, false, 0f, 0f, 0f, 1f, "water",
                "waterfall", "cascade", "torrent", "flood"),

            new EffMeta("Big Splash",
                "Explosive water impact erupts from the ground — sudden aquatic burst.",
                "BigSplash",
                1.2f, 2f, 0.3f, 0.6f, 1f,
                2, 2, false, false, 0f, 0f, 0f, 1f, "water",
                "splash", "water burst", "aqua blast", "wave crash"),

            new EffMeta("Rain",
                "Steady rainfall descends from above — somber, atmospheric, relentless drizzle.",
                "RainEffect",
                1f, 10f, 0.5f, 0.7f, 1f,
                3, 0, false, false, 0f, 0f, 0f, 1f, "water",
                "rain", "rainfall", "drizzle", "downpour", "storm rain"),

            // ── Smoke & Atmosphere ────────────────────────────────────────────

            new EffMeta("Ground Fog",
                "Creeping corrosive fog that damages anyone who lingers inside. Obscures vision and lingers.",
                "GroundFog",
                1.5f, 6f, 0.8f, 0.85f, 0.95f,
                2, 2, true, false, 0f, 0f, 8f, 2.5f, "arcane",
                "fog", "mist", "obscure", "void", "freeze ground"),

            new EffMeta("Smoke Cloud",
                "Dense billowing smoke obscures the battlefield — cover, concealment, disorientation.",
                "SmokeEffect",
                1.5f, 6f, 0.5f, 0.5f, 0.5f,
                2, 2, false, false, 0f, 0f, 0f, 1f, "effect",
                "smoke", "cloud", "smog", "fume", "obscure"),

            new EffMeta("Poison Gas",
                "Toxic green cloud seeps across the ground — corrosive, suffocating, lethal over time.",
                "PoisonGas",
                1.2f, 6f, 0.4f, 0.8f, 0.3f,
                2, 2, true, false, 0f, 0f, 6f, 2f, "poison",
                "poison", "toxic gas", "venom cloud", "noxious fumes", "toxic", "blight"),

            new EffMeta("Dust Storm",
                "Swirling vortex of wind and debris — blinding, chaotic, all-consuming sandstorm.",
                "DustStorm",
                2f, 8f, 0.8f, 0.7f, 0.5f,
                3, 0, false, false, 0f, 0f, 0f, 1f, "effect",
                "sandstorm", "whirlwind", "dust devil", "gust", "wind"),

            new EffMeta("Steam Vent",
                "Pressurized steam erupts from beneath — scalding burst, geothermal power.",
                "PressurisedSteam",
                1f, 3f, 0.9f, 0.9f, 1f,
                2, 2, false, false, 0f, 0f, 0f, 1f, "effect",
                "steam", "geyser", "hot steam", "vapor burst"),

            // ── Magic / Special ───────────────────────────────────────────────

            new EffMeta("Earth Shatter",
                "The ground fractures and erupts in a cascade of boulders — seismic, cataclysmic tremor.",
                "EarthShatter",
                1.5f, 3f, 0.6f, 0.5f, 0.3f,
                2, 2, true, false, 0f, 0f, 15f, 2.5f, "earth",
                "earthquake", "quake", "ground crack", "fissure", "tremor", "shatter"),

            new EffMeta("Dissolve",
                "Subject disintegrates into a shower of arcane particles — erased from existence.",
                "Dissolve",
                1f, 3f, 0.8f, 0.4f, 1f,
                1, 3, false, false, 0f, 0f, 0f, 1f, "effect",
                "disintegrate", "fade", "vanish", "erase"),

            new EffMeta("Respawn",
                "Burst of light as the subject materializes out of thin air — dramatic reappearance.",
                "Respawn",
                1f, 2.5f, 0.9f, 0.95f, 1f,
                1, 3, false, false, 0f, 0f, 0f, 1f, "effect",
                "appear", "materialize", "revive", "conjure", "summon"),

            new EffMeta("Magic Glow",
                "Radiant light particles swirl around the subject — arcane blessing, mystical aura.",
                "ParticlesLight",
                1.2f, 5f, 1f, 1f, 0.8f,
                1, 3, false, false, 0f, 0f, 0f, 1f, "effect",
                "glow", "aura", "radiance", "shimmer", "blessing", "enchant"),

            // ── Nature / Ambient ──────────────────────────────────────────────

            new EffMeta("Guide Fireflies",
                "Soft glowing fireflies that illuminate the area and guide the player. Peaceful, ambient light.",
                "FireFlies",
                1.2f, 8f, 0.9f, 1f, 0.6f,
                0, 0, false, false, 0f, 0f, 0f, 1f, "effect",
                "fireflies", "firefly", "fire fly", "fire flies", "fairy light", "fairy lights",
                "illuminate", "light", "guide", "glow", "glowing bugs"),

            new EffMeta("Dust Motes",
                "Gently drifting dust particles catch the light — serene, timeless ambience.",
                "DustMotesEffect",
                1f, 8f, 1f, 0.95f, 0.85f,
                0, 0, false, false, 0f, 0f, 0f, 1f, "effect",
                "dust", "dust mote", "floating particles", "mote", "motes", "airborne"),

            new EffMeta("Sand Swirls",
                "Swirling patterns of sand rise from the earth — desert wind, ancient mystique.",
                "SandSwirlsEffect",
                1f, 5f, 0.85f, 0.75f, 0.5f,
                0, 0, false, false, 0f, 0f, 0f, 1f, "effect",
                "sand", "swirl", "desert wind", "dust vortex"),

            new EffMeta("Candle Light",
                "Warm flickering candle flames appear around the subject — intimate, mysterious, ritualistic.",
                "Candles",
                1f, 10f, 1f, 0.85f, 0.5f,
                1, 3, false, false, 0f, 0f, 0f, 1f, "effect",
                "candles", "flame", "ritual", "lit", "illumination"),

            new EffMeta("Heat Haze",
                "Shimmering heat distortion warps the air — intense thermal radiation, scorching aura.",
                "HeatDistortion",
                1.5f, 6f, 1f, 0.7f, 0.3f,
                0, 0, false, false, 0f, 0f, 0f, 1f, "effect",
                "heat distortion", "heat wave", "shimmer", "thermal"),

            new EffMeta("Rocket Trail",
                "Fiery exhaust trail blazing through the air — speed, propulsion, dramatic exit.",
                "RocketTrail",
                1f, 4f, 1f, 0.6f, 0.2f,
                0, 0, false, false, 0f, 0f, 0f, 1f, "effect",
                "rocket", "exhaust", "thrust", "smoke trail"),

            // ── Goo & Slime ───────────────────────────────────────────────────

            new EffMeta("Goo Spray",
                "Viscous green slime erupts in a spray — corrosive ichor, disgusting and debilitating.",
                "GoopSpray",
                1f, 4f, 0.3f, 0.8f, 0.2f,
                0, 1, true, false, 0f, 0f, 5f, 1.5f, "poison",
                "slime", "goo", "sludge", "ichor", "ooze"),

            // ── Combat Impacts ────────────────────────────────────────────────

            new EffMeta("Muzzle Flash",
                "Brief blinding flash from weapon discharge — gunshot, energy weapon fire.",
                "MuzzleFlash",
                0.8f, 0.5f, 1f, 1f, 0.8f,
                1, 0, false, false, 0f, 0f, 0f, 1f, "effect",
                "gunfire", "shot", "bang", "weapon fire"),

            new EffMeta("Metal Spark",
                "Shower of sparks from a metal strike — clang of impact, sword on armor.",
                "MetalImpacts",
                0.5f, 0.5f, 1f, 0.9f, 0.4f,
                1, 0, false, false, 0f, 0f, 0f, 1f, "effect",
                "metal hit", "metal spark", "metal sparks", "clang", "armor spark", "sword clash"),

            new EffMeta("Flesh Hit",
                "Visceral impact burst on flesh — the shock of a heavy blow connecting.",
                "FleshImpacts",
                0.5f, 0.3f, 0.8f, 0.2f, 0.2f,
                1, 1, false, false, 0f, 0f, 0f, 1f, "effect",
                "hit", "impact", "strike", "wound"),
        };

        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Tools/Network Game/Rebuild Effect Catalog")]
        public static void Build()
        {
            // Ensure definitions folder exists
            if (!AssetDatabase.IsValidFolder(DefFolder))
            {
                AssetDatabase.CreateFolder(
                    Path.GetDirectoryName(DefFolder),
                    Path.GetFileName(DefFolder));
            }

            // Load or create the catalog asset
            var catalog = AssetDatabase.LoadAssetAtPath<EffectCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EffectCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.allEffects.Clear();

            int created = 0, updated = 0, prefabsFound = 0, prefabsMissing = 0;

            foreach (EffMeta m in k_Defs)
            {
                // "Fire Blast" → "FireBlast.asset"  (matches hand-authored naming convention)
                string safeName = m.Tag.Replace(" ", "");
                string path     = $"{DefFolder}/{safeName}.asset";

                var def   = AssetDatabase.LoadAssetAtPath<EffectDefinition>(path);
                bool isNew = def == null;

                if (isNew)
                {
                    def = ScriptableObject.CreateInstance<EffectDefinition>();
                    AssetDatabase.CreateAsset(def, path);
                    created++;
                }
                else
                {
                    updated++;
                }

                // Always refresh non-prefab fields from the metadata table
                def.effectTag           = m.Tag;
                def.description         = m.Description;
                def.defaultScale        = m.Scale;
                def.defaultDuration     = m.Duration;
                def.defaultColor        = new Color(m.R, m.G, m.B, 1f);
                def.placementMode       = (EffectPlacementMode)m.PlacementMode;
                def.targetType          = (EffectTargetType)m.TargetType;
                def.allowCustomScale    = true;
                def.allowCustomDuration = true;
                def.allowCustomColor    = true;
                def.enableGameplayDamage  = m.EnableDamage;
                def.enableHoming          = m.EnableHoming;
                def.projectileSpeed       = m.ProjectileSpeed;
                def.homingTurnRateDegrees = m.HomingTurnRate;
                def.damageAmount          = m.DamageAmount;
                def.damageRadius          = m.DamageRadius > 0f ? m.DamageRadius : 1f;
                def.damageType            = m.DamageType;
                def.affectPlayerOnly      = m.EnableDamage;
                def.alternativeTags       = m.Alts;
                def.name                  = safeName;

                // Resolve prefab — preserve existing reference if lookup fails
                var prefab = FindParticlePrefab(m.PrefabName);
                if (prefab != null)
                {
                    def.effectPrefab = prefab;
                    prefabsFound++;
                }
                else if (def.effectPrefab == null)
                {
                    Debug.LogWarning($"[EffectCatalogBuilder] Prefab '{m.PrefabName}' not found for tag '{m.Tag}'");
                    prefabsMissing++;
                }
                // else: already has a valid prefab reference — keep it

                EditorUtility.SetDirty(def);
                catalog.allEffects.Add(def);
            }

            catalog.RebuildLookup();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EffectCatalogBuilder] Rebuilt EffectCatalog.\n" +
                      $"  Created: {created} new definitions\n" +
                      $"  Updated: {updated} existing definitions\n" +
                      $"  Prefabs resolved: {prefabsFound}\n" +
                      $"  Prefabs missing (existing ref preserved): {prefabsMissing}\n" +
                      $"  Catalog total: {catalog.allEffects.Count} entries\n" +
                      $"  Path: {CatalogPath}");
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Searches all known ParticlePack sub-folders for a prefab by filename,
        /// then falls back to a project-wide FindAssets search inside ParticlePack.
        /// </summary>
        private static GameObject FindParticlePrefab(string prefabName)
        {
            // Direct path checks (fast, no allocation)
            foreach (string folder in k_SearchFolders)
            {
                string path = folder + prefabName + ".prefab";
                if (File.Exists(path))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            // Fallback: asset database search
            string[] guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("ParticlePack") && path.Contains("EffectExamples"))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }
    }
}
