using HunterSystem.Editor;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class LowPolyImportRulesTest
    {
        [TestCase("Assets/Art/Models/Hunter.fbx", true)]
        [TestCase("Assets/Art/Models/Props/Crate.fbx", true)]
        [TestCase("Assets/Art/Textures/Hunter.png", false)]
        [TestCase("Assets/ThirdParty/Tree.fbx", false)]
        [TestCase("Assets/Art/ModelsExtra/Tree.fbx", false)]
        public void FlatShadesOnlyModelsUnderTheLowPolyFolder(string assetPath, bool expected)
        {
            Assert.That(LowPolyImportRules.ShouldFlatShade(assetPath), Is.EqualTo(expected));
        }

        [Test]
        public void FlatShadingUsesHardEdgesEverywhere()
        {
            Assert.That(LowPolyImportRules.NormalSmoothingAngle, Is.EqualTo(0f));
        }
    }
}
