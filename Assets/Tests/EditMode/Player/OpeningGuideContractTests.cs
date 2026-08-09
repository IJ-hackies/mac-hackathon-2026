using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Player.Tests
{
    public sealed class OpeningGuideContractTests
    {
        private const string GuideScriptGuid = "f4c0f07b5e784e92a6f27d35c8197ab1";

        [Test]
        public void GuideExposesThreePagesAndFastDismissControls()
        {
            Type guideType = Type.GetType("Player.UI.OpeningGuideController, Assembly-CSharp");
            Assert.That(guideType, Is.Not.Null);

            FieldInfo pageCount = guideType.GetField("TotalPages", BindingFlags.Public | BindingFlags.Static);
            Assert.That(pageCount, Is.Not.Null);
            Assert.That(pageCount.GetRawConstantValue(), Is.EqualTo(3));
            Assert.That(guideType.GetProperty("IsOpen"), Is.Not.Null);
            Assert.That(guideType.GetProperty("CurrentPageIndex"), Is.Not.Null);
            Assert.That(guideType.GetMethod("Advance", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(guideType.GetMethod("Skip", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
        }

        [Test]
        public void OpeningRaisesGuideHandoffOnlyAfterGameplayRestoration()
        {
            Type openingType = Type.GetType("Player.OpeningCutsceneController, Assembly-CSharp");
            Assert.That(openingType, Is.Not.Null);
            EventInfo completed = openingType.GetEvent("Completed", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(completed, Is.Not.Null);
            Assert.That(completed.EventHandlerType, Is.EqualTo(typeof(Action<bool>)));

            string source = File.ReadAllText(Path.GetFullPath(
                "Assets/Scripts/Player/OpeningCutsceneController.cs"));
            int method = source.IndexOf("private void CompleteCutscene", StringComparison.Ordinal);
            int restored = source.IndexOf("hudCanvas.enabled = _hudWasEnabled;", method, StringComparison.Ordinal);
            int eventRaised = source.IndexOf("Completed?.Invoke(skipped);", method, StringComparison.Ordinal);

            Assert.That(method, Is.GreaterThanOrEqualTo(0));
            Assert.That(restored, Is.GreaterThan(method));
            Assert.That(eventRaised, Is.GreaterThan(restored));
        }

        [Test]
        public void GuideDefersSettingsRestoreSoEscapeCannotLeakIntoPause()
        {
            string source = File.ReadAllText(Path.GetFullPath(
                "Assets/Scripts/UI/OpeningGuideController.cs"));
            StringAssert.Contains("RestoreGameplay(true);", source);
            StringAssert.Contains("private IEnumerator RestoreSettingsNextFrame()", source);
            StringAssert.Contains("yield return null;", source);
        }

        [Test]
        public void SampleSceneSerializesOneGuideAndAllFiveHelperImages()
        {
            string scene = File.ReadAllText(Path.GetFullPath("Assets/Scenes/SampleScene.unity"));
            Assert.That(Count(scene, GuideScriptGuid), Is.EqualTo(1));
            StringAssert.Contains(
                "openingCutscene: {fileID: 9200000000000000102}", scene);
            StringAssert.Contains(
                "skillImage: {fileID: 2800000, guid: 5ed23c7f705c5304a939ca94ea64613b, type: 3}", scene);
            StringAssert.Contains(
                "baseImage: {fileID: 2800000, guid: 390a540506ad7ae48b5a3e17b5b8ae5b, type: 3}", scene);
            StringAssert.Contains(
                "specialImage: {fileID: 2800000, guid: 2a1fe9b026cd17e4b89fd357da5df867, type: 3}", scene);
            StringAssert.Contains(
                "outsideImage: {fileID: 2800000, guid: 1af3cd24490dc8d428512d83e91a9a07, type: 3}", scene);
            StringAssert.Contains(
                "arenaImage: {fileID: 2800000, guid: 40534983f55b39d4d8cc24140319ad62, type: 3}", scene);
        }

        [Test]
        public void HelperScreenshotsKeepNativeAspectAndAvoidUiMipmaps()
        {
            string[] names = { "skill", "base", "special", "outside", "arena" };
            foreach (string name in names)
            {
                string meta = File.ReadAllText(Path.GetFullPath($"Assets/Art/helper/{name}.png.meta"));
                StringAssert.Contains("enableMipMap: 0", meta, name);
                StringAssert.Contains("nPOTScale: 0", meta, name);
                Assert.That(meta.Split('\n').Count(line => line.Trim() == "wrapU: 1"), Is.EqualTo(1), name);
                Assert.That(meta.Split('\n').Count(line => line.Trim() == "wrapV: 1"), Is.EqualTo(1), name);
            }
        }

        private static int Count(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
