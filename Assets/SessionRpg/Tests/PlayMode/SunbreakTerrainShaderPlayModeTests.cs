using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class SunbreakTerrainShaderPlayModeTests
    {
        [SetUp]
        public void InitializeNativeRenderPipeline()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
                "This is the native-rendering gate; PlayMode must run with graphics, never silently skip it on Null.");
            Assert.That(GraphicsSettings.currentRenderPipeline, Is.InstanceOf<UniversalRenderPipelineAsset>(),
                "The native gate must use the project's configured URP asset.");

            // Batch-mode PlayMode does not necessarily render a camera frame. Until the
            // pipeline is instantiated, a URP-only shader exposes its error fallback pass.
            // A supported render request initializes the real pipeline without changing
            // project/quality settings or depending on another fixture loading Bootstrap.
            var cameraObject = new GameObject("SunbreakShaderGateCamera");
            RenderTexture previousTarget = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(16, 16, 24);
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 0;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True,
                    "The configured URP must initialize and support a native camera render request.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                Assert.That(RenderPipelineManager.currentPipeline, Is.InstanceOf<UniversalRenderPipeline>());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                Object.DestroyImmediate(cameraObject);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ScannedTerrain_NativeGraphicsSupportsGroundSolidAndVistaMaterialPasses(bool groundLayer, bool vista)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
                "This is the native-rendering gate; PlayMode must run with graphics, never silently skip it on Null.");
            Shader shader = Shader.Find("SessionRpg/Sunbreak Terrain");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True,
                "The native graphics device must support the terrain shader, not merely parse its source.");
            var material = new Material(shader);
            try
            {
                material.SetFloat("_SecondLayer", groundLayer ? 1f : 0f);
                if (groundLayer) { material.EnableKeyword("_SECOND_LAYER"); }
                else { material.DisableKeyword("_SECOND_LAYER"); }
                material.SetFloat("_VertexLayer", vista ? 1f : 0f);
                if (vista) { material.EnableKeyword("_VERTEX_LAYER"); }
                else { material.DisableKeyword("_VERTEX_LAYER"); }
                TestContext.WriteLine("Sunbreak native pipeline: graphics={0}, asset={1}, pipeline={2}, passes={3}",
                    SystemInfo.graphicsDeviceType, GraphicsSettings.currentRenderPipeline,
                    RenderPipelineManager.currentPipeline, material.passCount);
                foreach (string pass in new[] { "ForwardLit", "DepthOnly", "DepthNormals", "ShadowCaster" })
                {
                    Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0),
                        pass + " must be present in the supported native subshader.");
                }
                Assert.That(material.passCount, Is.EqualTo(4));
                Assert.That(material.IsKeywordEnabled("_SECOND_LAYER"), Is.EqualTo(groundLayer));
                Assert.That(material.IsKeywordEnabled("_VERTEX_LAYER"), Is.EqualTo(vista));
                TestContext.WriteLine("Sunbreak terrain native gate: {0}, second layer={1}, Vista triplanar={2}, all four passes supported.",
                    SystemInfo.graphicsDeviceType, groundLayer, vista);
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
