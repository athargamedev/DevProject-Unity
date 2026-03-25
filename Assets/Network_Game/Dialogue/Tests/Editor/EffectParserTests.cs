using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Network_Game.Dialogue.Effects;

namespace Network_Game.Dialogue.Tests
{
    /// <summary>
    /// Unit tests for EffectParser — the highest-complexity class in the Dialogue assembly.
    /// ParseColor (cyclomatic 127) is internal static — accessed via reflection.
    /// ExtractIntents (public static) drives ParseParameter (cyclomatic 169) and ParseColor
    /// together through normal parameter parsing with a null catalog.
    /// Coverage group E in the multiplayer readiness manifest.
    /// </summary>
    public class EffectParserTests
    {
        // ParseColor is internal static — accessed via reflection.
        private static readonly MethodInfo s_ParseColor = typeof(EffectParser).GetMethod(
            "ParseColor",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public
        );

        private static Color InvokeParseColor(string value)
        {
            Assert.That(s_ParseColor, Is.Not.Null, "ParseColor not found via reflection.");
            return (Color)s_ParseColor.Invoke(null, new object[] { value });
        }

        // ── E-01 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ParsesBracketSyntax_SingleTag()
        {
            var intents = EffectParser.ExtractIntents("[EFFECT: lightning_strike]", (EffectCatalog)null);

            Assert.That(intents, Has.Count.EqualTo(1));
            Assert.That(intents[0].rawTagName, Is.EqualTo("lightning_strike"));
        }

        // ── E-02 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ParsesBareTagSyntax_WhenNoBrackets()
        {
            string response = "EFFECT: fire_burst\nSome speech here.";

            var intents = EffectParser.ExtractIntents(response, (EffectCatalog)null);

            Assert.That(intents.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(intents[0].rawTagName, Is.EqualTo("fire_burst"));
        }

        // ── E-03 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ParsesMultipleTags_InOrder()
        {
            string response = "[EFFECT: spark] Some text. [EFFECT: explosion | scale: 2]";

            var intents = EffectParser.ExtractIntents(response, (EffectCatalog)null);

            Assert.That(intents, Has.Count.EqualTo(2));
            Assert.That(intents[0].rawTagName, Is.EqualTo("spark"));
            Assert.That(intents[1].rawTagName, Is.EqualTo("explosion"));
            Assert.That(intents[1].scale, Is.EqualTo(2f).Within(0.001f));
        }

        // ── E-04 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ReturnsEmpty_WhenNoTags()
        {
            var intents = EffectParser.ExtractIntents("Just plain NPC speech.", (EffectCatalog)null);

            Assert.That(intents, Is.Empty);
        }

        // ── E-05 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ReturnsEmpty_WhenWhitespaceOrEmpty()
        {
            Assert.That(EffectParser.ExtractIntents("   ", (EffectCatalog)null), Is.Empty);
            Assert.That(EffectParser.ExtractIntents(string.Empty, (EffectCatalog)null), Is.Empty);
        }

        // ── E-06 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ParsesScaleParameter()
        {
            var intents = EffectParser.ExtractIntents("[EFFECT: storm | scale: 3.5]", (EffectCatalog)null);

            Assert.That(intents, Has.Count.EqualTo(1));
            Assert.That(intents[0].scale, Is.EqualTo(3.5f).Within(0.001f));
        }

        // ── E-07 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ParsesTargetParameter()
        {
            var intents = EffectParser.ExtractIntents("[EFFECT: fire | target: Player]", (EffectCatalog)null);

            Assert.That(intents, Has.Count.EqualTo(1));
            Assert.That(intents[0].target, Is.EqualTo("Player"));
        }

        // ── E-08 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ParsesDurationParameter()
        {
            var intents = EffectParser.ExtractIntents("[EFFECT: aura | duration: 5.0]", (EffectCatalog)null);

            Assert.That(intents, Has.Count.EqualTo(1));
            Assert.That(intents[0].duration, Is.EqualTo(5.0f).Within(0.001f));
        }

        // ── E-09 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ParsesColorParameter_ViaParseColor()
        {
            // color: red should route through ParseColor and produce Color.red.
            var intents = EffectParser.ExtractIntents("[EFFECT: wave | color: red]", (EffectCatalog)null);

            Assert.That(intents, Has.Count.EqualTo(1));
            Assert.That(intents[0].color, Is.EqualTo(Color.red));
        }

        // ── E-10 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ParseColor_ReturnsCorrectColor_ForHexFormat()
        {
            Color result = InvokeParseColor("#FF0000");

            Assert.That(result.r, Is.EqualTo(1f).Within(0.01f));
            Assert.That(result.g, Is.EqualTo(0f).Within(0.01f));
            Assert.That(result.b, Is.EqualTo(0f).Within(0.01f));
        }

        // ── E-11 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ParseColor_ReturnsCorrectColor_ForElementalKeywords()
        {
            Color fire  = InvokeParseColor("fire");
            Color ice   = InvokeParseColor("ice");
            Color storm = InvokeParseColor("storm");

            // fire = warm orange-red
            Assert.That(fire.r, Is.GreaterThan(0.8f), "fire should be reddish");
            // ice = cool blue
            Assert.That(ice.b, Is.GreaterThan(0.8f), "ice should be bluish");
            // storm = purple-ish (high blue component)
            Assert.That(storm.b, Is.GreaterThan(0.5f), "storm should have high blue");
        }

        // ── E-12 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ParseColor_ParsesRgbFormat()
        {
            Color result = InvokeParseColor("rgb(0, 128, 255)");

            Assert.That(result.r, Is.EqualTo(0f).Within(0.01f));
            Assert.That(result.g, Is.EqualTo(128f / 255f).Within(0.01f));
            Assert.That(result.b, Is.EqualTo(1f).Within(0.01f));
        }

        // ── E-13 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ParseColor_FallsBackToWhite_ForUnknownName()
        {
            Color result = InvokeParseColor("notacolor_xyz");

            Assert.That(result, Is.EqualTo(Color.white));
        }

        // ── E-14 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ParseColor_FallsBackToWhite_WhenNullOrEmpty()
        {
            Assert.That(InvokeParseColor(null), Is.EqualTo(Color.white));
            Assert.That(InvokeParseColor(string.Empty), Is.EqualTo(Color.white));
            Assert.That(InvokeParseColor("   "), Is.EqualTo(Color.white));
        }

        // ── E-15 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ParsesEmotionParameter()
        {
            var intents = EffectParser.ExtractIntents("[EFFECT: aura | emotion: epic]", (EffectCatalog)null);

            Assert.That(intents, Has.Count.EqualTo(1));
            Assert.That(intents[0].emotion, Is.EqualTo("epic"));
        }

        // ── E-16 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ParsesPlacementType_Projectile()
        {
            var intents = EffectParser.ExtractIntents("[EFFECT: bolt | placement: projectile]", (EffectCatalog)null);

            Assert.That(intents, Has.Count.EqualTo(1));
            Assert.That(intents[0].placementType, Is.EqualTo("projectile"));
        }

        // ── E-17 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ParsesPercentScale()
        {
            // "200%" should parse as 2.0.
            var intents = EffectParser.ExtractIntents("[EFFECT: spark | scale: 200%]", (EffectCatalog)null);

            Assert.That(intents, Has.Count.EqualTo(1));
            Assert.That(intents[0].scale, Is.EqualTo(2f).Within(0.01f));
        }

        // ── E-18 ─────────────────────────────────────────────────────────────────

        [Test]
        public void ExtractIntents_ParsesLoosePlayerTargetToken()
        {
            // Bare "player" token after the tag name should be treated as the target.
            var intents = EffectParser.ExtractIntents("[EFFECT: lightning | player]", (EffectCatalog)null);

            Assert.That(intents, Has.Count.EqualTo(1));
            Assert.That(intents[0].target, Is.EqualTo("player").IgnoreCase);
        }
    }
}
