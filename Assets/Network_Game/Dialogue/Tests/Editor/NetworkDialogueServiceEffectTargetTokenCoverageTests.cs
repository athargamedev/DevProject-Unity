using NUnit.Framework;

namespace Network_Game.Dialogue.Tests
{
    // Coverage-focused EditMode tests for player/ground target token parsing.
    public class NetworkDialogueServiceEffectTargetTokenTests
    {
        [TestCase("player", true)]
        [TestCase("listener", true)]
        [TestCase("role:player", true)]
        [TestCase("semantic:player:2", true)]
        [TestCase("player:7", true)]
        [TestCase("npc", false)]
        [TestCase("ground", false)]
        public void IsPlayerTargetToken_RecognizesGenericPlayerTokens(string token, bool expected)
        {
            bool result = NetworkDialogueServiceTestReflection.InvokeBool(
                "IsPlayerTargetToken",
                token
            );

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("requester", true)]
        [TestCase("host", true)]
        [TestCase("player:requester", true)]
        [TestCase("p2", true)]
        [TestCase("player3", true)]
        [TestCase("client:7", true)]
        [TestCase("p2 head", true)]
        [TestCase("requester feet", true)]
        [TestCase("player", false)]
        [TestCase("terrain", false)]
        public void LooksLikeExplicitPlayerTargetToken_SeparatesExplicitFromGenericTokens(
            string token,
            bool expected
        )
        {
            bool result = NetworkDialogueServiceTestReflection.InvokeBool(
                "LooksLikeExplicitPlayerTargetToken",
                token
            );

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void TryParseOrderedPlayerTargetToken_ParsesSupportedFormats()
        {
            object[] p2Args = { "p2", 0 };
            object[] player7Args = { "player7", 0 };
            object[] semanticArgs = { "semantic:player:4", 0 };
            object[] invalidArgs = { "p0", 0 };

            bool p2 = NetworkDialogueServiceTestReflection.InvokeBool(
                "TryParseOrderedPlayerTargetToken",
                p2Args
            );
            bool player7 = NetworkDialogueServiceTestReflection.InvokeBool(
                "TryParseOrderedPlayerTargetToken",
                player7Args
            );
            bool semantic = NetworkDialogueServiceTestReflection.InvokeBool(
                "TryParseOrderedPlayerTargetToken",
                semanticArgs
            );
            bool invalid = NetworkDialogueServiceTestReflection.InvokeBool(
                "TryParseOrderedPlayerTargetToken",
                invalidArgs
            );

            Assert.That(p2, Is.True);
            Assert.That((int)p2Args[1], Is.EqualTo(2));
            Assert.That(player7, Is.True);
            Assert.That((int)player7Args[1], Is.EqualTo(7));
            Assert.That(semantic, Is.True);
            Assert.That((int)semanticArgs[1], Is.EqualTo(4));
            Assert.That(invalid, Is.False);
            Assert.That((int)invalidArgs[1], Is.EqualTo(0));
        }

        [Test]
        public void TryParseClientPlayerTargetToken_ParsesSupportedFormats()
        {
            object[] directArgs = { "client:7", (ulong)0 };
            object[] namespacedArgs = { "player:client:22", (ulong)0 };
            object[] invalidArgs = { "client:abc", (ulong)0 };

            bool direct = NetworkDialogueServiceTestReflection.InvokeBool(
                "TryParseClientPlayerTargetToken",
                directArgs
            );
            bool namespaced = NetworkDialogueServiceTestReflection.InvokeBool(
                "TryParseClientPlayerTargetToken",
                namespacedArgs
            );
            bool invalid = NetworkDialogueServiceTestReflection.InvokeBool(
                "TryParseClientPlayerTargetToken",
                invalidArgs
            );

            Assert.That(direct, Is.True);
            Assert.That((ulong)directArgs[1], Is.EqualTo(7));
            Assert.That(namespaced, Is.True);
            Assert.That((ulong)namespacedArgs[1], Is.EqualTo(22));
            Assert.That(invalid, Is.False);
            Assert.That((ulong)invalidArgs[1], Is.EqualTo(0));
        }

        [TestCase("ground", "IsGroundAlias", true)]
        [TestCase("p2 ground", "IsGroundAlias", true)]
        [TestCase("player head", "IsPlayerHeadAlias", true)]
        [TestCase("requester face", "IsPlayerHeadAlias", true)]
        [TestCase("feet", "IsPlayerFeetAlias", true)]
        [TestCase("under player", "IsPlayerFeetAlias", true)]
        [TestCase("sky", "IsGroundAlias", false)]
        [TestCase("torso", "IsPlayerHeadAlias", false)]
        [TestCase("hands", "IsPlayerFeetAlias", false)]
        public void AliasHelpers_ClassifyGroundHeadAndFeetPhrases(
            string token,
            string methodName,
            bool expected
        )
        {
            bool result = NetworkDialogueServiceTestReflection.InvokeBool(methodName, token);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
