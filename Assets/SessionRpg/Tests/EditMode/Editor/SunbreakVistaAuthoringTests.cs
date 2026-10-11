using System;
using System.Collections.Generic;
using NUnit.Framework;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Editor.Authoring;
using SessionRpg.Unity.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using StrategyFollowCamera = SessionRpg.Unity.Camera.StrategyFollowCamera;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakVistaAuthoringTests
    {
        private const string BootstrapPath = "Assets/SessionRpg/Scenes/Bootstrap.unity";

        [Test]
        public void Bootstrap_BindsOneInactiveResidentVistaAndTheAuthoredSharedSky()
        {
            WithVista((lighting, vista) =>
            {
                Assert.That(lighting.HasAuthoredBindings, Is.True);
                Assert.That(lighting.HasSunbreakVistaBindings, Is.True);
                Assert.That(vista.scene, Is.EqualTo(lighting.gameObject.scene));
                Assert.That(vista.name, Is.EqualTo("SunbreakDistantVista"));
                Assert.That(vista.activeSelf, Is.False, "The vista must not render at Home before PvE entry.");
                Assert.That(vista.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(vista.transform.rotation, Is.EqualTo(Quaternion.identity));
                Assert.That(vista.transform.lossyScale, Is.EqualTo(Vector3.one));
                Assert.That(vista.transform.childCount, Is.EqualTo(4));
                var serialized = new SerializedObject(lighting);
                Assert.That(serialized.FindProperty("_sunbreakSky").objectReferenceValue,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>(SunbreakVistaAuthoring.SkyPath)));
                Assert.DoesNotThrow(() => SunbreakVistaAuthoring.Validate(vista));
            });
        }

        [Test]
        public void GeneratedVista_UsesFourBoundedTerrainAndWoodlandBatches()
        {
            WithVista((lighting, vista) =>
            {
                MeshFilter[] filters = vista.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters, Has.Length.EqualTo(4));
                Assert.That(vista.GetComponentsInChildren<MeshRenderer>(true), Has.Length.EqualTo(4));
                Assert.That(vista.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(vista.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(vista.GetComponentsInChildren<Light>(true), Is.Empty);
                Assert.That(vista.GetComponentsInChildren<Camera>(true), Is.Empty);
                Assert.That(vista.GetComponentsInChildren<AudioListener>(true), Is.Empty);
                int triangles = 0;
                foreach (MeshFilter filter in filters)
                {
                    Mesh mesh = filter.sharedMesh;
                    Assert.That(mesh, Is.Not.Null);
                    Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                    triangles += (int)mesh.GetIndexCount(0) / 3;
                    Assert.That(AssetDatabase.GetAssetPath(mesh),
                        Is.EqualTo(SunbreakVistaAuthoring.Folder + "/" + filter.name + ".asset"));
                    MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                    Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1));
                    Assert.That(renderer.sharedMaterial, Is.Not.Null);
                    Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                    Assert.That(renderer.receiveShadows, Is.False);
                    Assert.That(filter.gameObject.layer, Is.EqualTo(2));
                    Assert.That(GameObjectUtility.GetStaticEditorFlags(filter.gameObject) &
                        StaticEditorFlags.BatchingStatic, Is.EqualTo(StaticEditorFlags.BatchingStatic));
                }
                Assert.That(triangles, Is.EqualTo(27042));
                Assert.That(triangles, Is.LessThanOrEqualTo(SunbreakVistaAuthoring.MaximumTriangles));
            });
        }

        [Test]
        public void DistantMountains_AllRaisedTriangleSamplesRemainOutsideTheCoreRectangle()
        {
            WithVista((lighting, vista) =>
            {
                MeshFilter filter = vista.transform.Find("ErodedMountainHorizon").GetComponent<MeshFilter>();
                Vector3[] vertices = filter.sharedMesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = filter.transform.TransformPoint(vertices[i]);
                    AssertOutsideCoreWhenRaised(vertices[i]);
                }
                int[] indices = filter.sharedMesh.triangles;
                float highest = float.NegativeInfinity;
                foreach (Vector3 vertex in vertices) { highest = Mathf.Max(highest, vertex.y); }
                Assert.That(highest, Is.InRange(40f, 55f),
                    "Separated peaks replace the former uniform 25 m profile; projected sky space is guarded separately.");
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                    AssertOutsideCoreWhenRaised((a + b + c) / 3f);
                    AssertOutsideCoreWhenRaised((a + b) * 0.5f);
                    AssertOutsideCoreWhenRaised((b + c) * 0.5f);
                    AssertOutsideCoreWhenRaised((c + a) * 0.5f);
                    Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0f),
                        "The visible mountain surface must face upward; no reversed or degenerate terrain triangles.");
                }
                Assert.That(filter.GetComponent<MeshRenderer>().sharedMaterial.name, Is.EqualTo("Sunbreak_VistaTerrain"));
            });
        }

        [Test]
        public void Underlay_RemainsBelowTheCombatFloorWithContinuousWorldBlendCoordinates()
        {
            WithVista((lighting, vista) =>
            {
                MeshFilter filter = vista.transform.Find("DistantGroundUnderlay").GetComponent<MeshFilter>();
                Mesh mesh = filter.sharedMesh;
                Assert.That(mesh.vertexCount, Is.EqualTo(4));
                Assert.That(mesh.triangles, Has.Length.EqualTo(6));
                Assert.That(mesh.bounds.min, Is.EqualTo(new Vector3(-64f, SunbreakVistaAuthoring.UnderlayHeight, -64f)));
                Assert.That(mesh.bounds.max, Is.EqualTo(new Vector3(128f, SunbreakVistaAuthoring.UnderlayHeight, 128f)));
                Vector3[] vertices = mesh.vertices;
                Vector2[] uvs = mesh.uv;
                Vector3[] normals = mesh.normals;
                Assert.That(uvs, Has.Length.EqualTo(vertices.Length));
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 world = filter.transform.TransformPoint(vertices[i]);
                    Assert.That(world.y, Is.LessThan(0f));
                    Assert.That(world.y, Is.EqualTo(SunbreakVistaAuthoring.UnderlayHeight).Within(0.00001f));
                    Assert.That(uvs[i].x, Is.EqualTo((world.x + 64f) / 192f).Within(0.00001f));
                    Assert.That(uvs[i].y, Is.EqualTo((world.z + 64f) / 192f).Within(0.00001f));
                    Assert.That(Vector3.Dot(normals[i], Vector3.up), Is.GreaterThan(0.9999f));
                }
                Assert.That(filter.GetComponent<MeshRenderer>().sharedMaterial.name, Is.EqualTo("Sunbreak_Meadow"));
            });
        }

        [Test]
        public void SkyAsset_UsesTheFrozenPanoramicDaylightSource()
        {
            Assert.DoesNotThrow(() => SunbreakDaylightSkyAssets.Validate());
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(SunbreakVistaAuthoring.SkyPath);
            Assert.That(sky, Is.Not.Null);
            Assert.That(sky.shader, Is.Not.Null);
            Assert.That(sky.shader.name, Is.EqualTo("Skybox/Panoramic"));
            Assert.That(sky.shader, Is.SameAs(Shader.Find("Skybox/Panoramic")));
            Assert.That(sky.GetTexture("_MainTex"), Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(sky.GetTexture("_MainTex")),
                Does.EndWith("/kloofendal_48d_partly_cloudy_puresky_1k.hdr"));
            Assert.That(sky.GetFloat("_Exposure"), Is.EqualTo(1f));
            Assert.That(sky.GetFloat("_Rotation"), Is.Zero);
            Assert.That(sky.GetColor("_Tint"), Is.EqualTo(new Color(0.5f, 0.5f, 0.5f)));
        }

        [Test]
        public void MountainRings_SubdivideLongRadialCellsWithinTheExistingTwoBatchBudget()
        {
            WithVista((lighting, vista) =>
            {
                Mesh mesh = vista.transform.Find("ErodedMountainHorizon").GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.InRange(160 * 33, 160 * 33 * 3),
                    "Only dominant-axis UV seams may split the original surface vertices.");
                float[] distances = { 0f, 4f, 8f, 12f, 16f, 20f, 25f, 30f, 35f, 40f,
                    46f, 52f, 58f, 64f, 70f, 76f, 84f, 92f, 100f, 110f, 120f,
                    130f, 140f, 150f, 160f, 172f, 184f, 196f, 208f, 220f, 232f, 240f, 245f };
                Vector3[] vertices = mesh.vertices;
                var uniquePositions = new HashSet<Vector3>();
                foreach (Vector3 vertex in vertices) { uniquePositions.Add(vertex); }
                Assert.That(uniquePositions.Count, Is.EqualTo(160 * 33),
                    "Projection seams must not change the exact welded physical topology.");
                foreach (float distance in distances)
                {
                    bool found = false;
                    foreach (Vector3 vertex in vertices)
                    {
                        if (Mathf.Abs(vertex.x - (124f + distance)) < 0.001f &&
                            Mathf.Abs(vertex.z - 32f) < 0.001f) { found = true; break; }
                    }
                    Assert.That(found, Is.True, "Missing resolved radial sample at " + distance + " m beyond the eastern inner edge.");
                }
                Assert.That(mesh.GetIndexCount(0) / 3, Is.EqualTo(10240));
            });
        }

        [Test]
        public void MountainRing_ChangesCrestHeightWithAzimuthInsteadOfRepeatingOneProfile()
        {
            WithVista((lighting, vista) =>
            {
                Mesh mesh = vista.transform.Find("ErodedMountainHorizon").GetComponent<MeshFilter>().sharedMesh;
                float lowest = float.PositiveInfinity;
                float highest = float.NegativeInfinity;
                int samples = 0;
                var sampledPositions = new HashSet<Vector3>();
                foreach (Vector3 vertex in mesh.vertices)
                {
                    if (!sampledPositions.Add(vertex)) { continue; }
                    var radial = new Vector2(vertex.x - 32f, vertex.z - 32f);
                    Vector2 direction = radial.normalized;
                    float innerRadius = 92f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
                    if (Mathf.Abs(radial.magnitude - innerRadius - 110f) > 0.001f) { continue; }
                    lowest = Mathf.Min(lowest, vertex.y);
                    highest = Mathf.Max(highest, vertex.y);
                    samples++;
                }
                Assert.That(samples, Is.EqualTo(160));
                Assert.That(highest - lowest, Is.GreaterThan(20f),
                    "The same radial station needs distinct peaks and saddles, not a uniformly raised perimeter.");
            });
        }

        [Test]
        public void FarGeology_ExposesResolvedSteepRockFacesWithoutTurningTheWholeSlopeIntoACliff()
        {
            WithVista((lighting, vista) =>
            {
                Mesh mesh = vista.transform.Find("ErodedMountainHorizon").GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] indices = mesh.triangles;
                var distance = new float[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector2 radial = new Vector2(vertices[i].x - 32f, vertices[i].z - 32f);
                    Vector2 direction = radial.normalized;
                    distance[i] = radial.magnitude - 92f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
                }
                int farFaces = 0, rockFaces = 0, relaxedFaces = 0;
                var rockSectors = new HashSet<int>();
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                    if (distance[ia] < 99.99f || distance[ib] < 99.99f || distance[ic] < 99.99f ||
                        distance[ia] > 184.01f || distance[ib] > 184.01f || distance[ic] > 184.01f) { continue; }
                    Vector3 a = vertices[ia], b = vertices[ib], c = vertices[ic];
                    Vector3 faceNormal = Vector3.Cross(b - a, c - a).normalized;
                    farFaces++;
                    if (faceNormal.y > 0.94f) { relaxedFaces++; }
                    if (faceNormal.y >= 0.78f) { continue; }
                    rockFaces++;
                    Vector3 center = (a + b + c) / 3f;
                    float azimuth = Mathf.Atan2(center.z - 32f, center.x - 32f) * Mathf.Rad2Deg;
                    rockSectors.Add(Mathf.FloorToInt((azimuth + 180f) / 30f));
                }
                Assert.That(farFaces, Is.EqualTo(2560));
                Assert.That(rockFaces, Is.GreaterThanOrEqualTo(150),
                    "The rejected rounded horizon had only 25 steep faces in this same 2560-face far band.");
                Assert.That(rockSectors.Count, Is.GreaterThanOrEqualTo(4),
                    "Geology needs several distinct outcrops, not one hidden cliff.");
                Assert.That(relaxedFaces, Is.GreaterThan(farFaces / 2),
                    "Keep open saddles and gentle backslopes; a uniform raised perimeter is not a mountain range.");
            });
        }

        [Test]
        public void MassifComposition_LowWoodlandApronDoesNotRepeatTheTallRadialShoulder()
        {
            WithVista((lighting, vista) =>
            {
                Mesh mesh = vista.transform.Find("ErodedMountainHorizon").GetComponent<MeshFilter>().sharedMesh;
                int sampled = 0;
                var unique = new HashSet<Vector3>();
                foreach (Vector3 vertex in mesh.vertices)
                {
                    if (!unique.Add(vertex)) { continue; }
                    Vector2 radial = new Vector2(vertex.x - 32f, vertex.z - 32f);
                    Vector2 direction = radial.normalized;
                    float distance = radial.magnitude - 92f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
                    if (distance < 24.99f || distance > 70.01f) { continue; }
                    Assert.That(vertex.y, Is.InRange(1f, 4f),
                        "Woods now occupy a low continuous apron; separate rock masses rise behind it, not a uniform tall band.");
                    sampled++;
                }
                Assert.That(sampled, Is.GreaterThan(1000));
            });
        }

        [Test]
        public void MassifComposition_WideOrbitShowsSeparatePeaksAndAnOpenSouthernSaddle()
        {
            WithVista((lighting, vista) =>
            {
                var cameraOwner = new GameObject("MassifWideProjection", typeof(StrategyFollowCamera));
                var target = new GameObject("MassifProjectionTarget");
                try
                {
                    Camera projection = cameraOwner.GetComponent<Camera>();
                    projection.enabled = false;
                    projection.aspect = 2.4f;
                    Float3 camp = PveRegionLayout.GetCampCenter(1);
                    target.transform.position = new Vector3(camp.X - 14f, 0f, camp.Z);
                    StrategyFollowCamera rig = cameraOwner.GetComponent<StrategyFollowCamera>();
                    rig.SetGameplayComposition(GameModeId.PveLeveling);
                    rig.SetFollowTarget(target.transform);
                    rig.SetHeading(135f);
                    MeshFilter filter = vista.transform.Find("ErodedMountainHorizon").GetComponent<MeshFilter>();
                    float[] skyline = ProjectSkyline(projection, filter,
                        new[] { 0.05f, 0.15f, 0.25f, 0.35f, 0.45f, 0.5f, 0.55f, 0.65f, 0.75f, 0.85f, 0.95f });
                    float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
                    foreach (float height in skyline)
                    {
                        Assert.That(height, Is.InRange(0.80f, 0.995f));
                        minimum = Mathf.Min(minimum, height); maximum = Mathf.Max(maximum, height);
                    }
                    Assert.That((maximum - minimum) * 1080f, Is.GreaterThan(150f),
                        "The previous radial horizon varied only about117 px across this wide orbit.");
                    Assert.That((skyline[6] - skyline[3]) * 1080f, Is.GreaterThan(110f),
                        "Independent ridge groups must retain a genuine low saddle between their peaks.");
                    Assert.That(skyline[10] * 1080f, Is.LessThan(900f),
                        "The southern shoulder must fall into open space, not continue as a ring-height wall.");
                }
                finally { Object.DestroyImmediate(cameraOwner); Object.DestroyImmediate(target); }
            });
        }

        [Test]
        public void InnerFoot_EasesIntoTheRidgesInsteadOfRaisingANineMeterPerimeterCliff()
        {
            WithVista((lighting, vista) =>
            {
                Mesh mesh = vista.transform.Find("ErodedMountainHorizon").GetComponent<MeshFilter>().sharedMesh;
                var sampled = new HashSet<Vector3>();
                int innerSamples = 0;
                int firstRingSamples = 0;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    if (!sampled.Add(vertex)) { continue; }
                    var radial = new Vector2(vertex.x - 32f, vertex.z - 32f);
                    Vector2 direction = radial.normalized;
                    float distance = radial.magnitude -
                        92f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
                    if (Mathf.Abs(distance) < 0.001f)
                    {
                        Assert.That(vertex.y, Is.EqualTo(-0.08f).Within(0.00001f));
                        innerSamples++;
                    }
                    if (Mathf.Abs(distance - 4f) < 0.001f)
                    {
                        Assert.That(vertex.y, Is.InRange(-0.08f, 1f),
                            "The first four meters must remain a low foot, not the rejected steep boundary curtain.");
                        firstRingSamples++;
                    }
                }
                Assert.That(innerSamples, Is.EqualTo(160));
                Assert.That(firstRingSamples, Is.EqualTo(160));
            });
        }

        [Test]
        public void RockProjection_RetainsPhysicalTexelDensityOnSteepFacesWithoutHardNormalSeams()
        {
            WithVista((lighting, vista) =>
            {
                Mesh mesh = vista.transform.Find("ErodedMountainHorizon").GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uv = mesh.uv;
                int[] indices = mesh.triangles;
                var sharedNormals = new Dictionary<Vector3, Vector3>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (sharedNormals.TryGetValue(vertices[i], out Vector3 previous))
                    {
                        Assert.That(Vector3.Dot(previous, normals[i]), Is.GreaterThan(0.9999f),
                            "Changing texture projection must not create a lighting seam.");
                    }
                    else { sharedNormals.Add(vertices[i], normals[i]); }
                }
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                    Vector3 normal = Vector3.Cross(vertices[ib] - vertices[ia], vertices[ic] - vertices[ia]).normalized;
                    float x = Mathf.Abs(normal.x), y = Mathf.Abs(normal.y), z = Mathf.Abs(normal.z);
                    int axis = y >= x && y >= z ? 0 : x >= z ? 1 : 2;
                    foreach (int index in new[] { ia, ib, ic })
                    {
                        Vector3 point = vertices[index];
                        Vector2 expected = (axis == 0 ? new Vector2(point.x, point.z) :
                            axis == 1 ? new Vector2(point.z, point.y) : new Vector2(point.x, point.y)) * 0.15f;
                        Assert.That((uv[index] - expected).sqrMagnitude, Is.LessThan(0.00000001f));
                    }
                    Vector2 ab = uv[ib] - uv[ia], ac = uv[ic] - uv[ia];
                    float uvArea = Mathf.Abs(ab.x * ac.y - ab.y * ac.x);
                    float surfaceArea = Vector3.Cross(vertices[ib] - vertices[ia], vertices[ic] - vertices[ia]).magnitude;
                    Assert.That(uvArea / (surfaceArea * 0.15f * 0.15f), Is.InRange(0.575f, 1.005f),
                        "No visible rock face may collapse into the old XZ-only vertical texture stretch.");
                }
            });
        }

        [Test]
        public void MountainSurfaceLayer_UsesFiniteAuthoredWeightsWithoutCreatingProjectionSeams()
        {
            WithVista((lighting, vista) =>
            {
                Mesh mesh = vista.transform.Find("ErodedMountainHorizon").GetComponent<MeshFilter>().sharedMesh;
                Vector3[] positions = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Color[] colors = mesh.colors;
                Assert.That(colors, Has.Length.EqualTo(positions.Length));
                var shared = new Dictionary<Vector3, Color>();
                float minimum = 1f, maximum = 0f;
                for (int i = 0; i < positions.Length; i++)
                {
                    Color color = colors[i];
                    Assert.That(color.r, Is.InRange(0f, 1f));
                    Assert.That(color.g, Is.InRange(0f, 1f));
                    Assert.That(color.b, Is.InRange(0f, 1f));
                    Assert.That(color.a, Is.InRange(0f, 1f));
                    Assert.That(color, Is.EqualTo(SunbreakLandscapeSurfaceAuthoring.VertexColor(positions[i], normals[i])));
                    if (shared.TryGetValue(positions[i], out Color previous)) { Assert.That(color, Is.EqualTo(previous)); }
                    else { shared.Add(positions[i], color); }
                    minimum = Mathf.Min(minimum, color.r);
                    maximum = Mathf.Max(maximum, color.r);
                }
                Assert.That(maximum - minimum, Is.GreaterThan(0.4f),
                    "Wooded slopes and exposed peaks must not receive one uniform material weight.");
            });
        }

        [Test]
        public void CampOneDefaultCamera_ProjectsDistinctPeaksAndSaddlesWhileLeavingCloudSpace()
        {
            WithVista((lighting, vista) =>
            {
                var cameraOwner = new GameObject("VistaProjectionContract", typeof(StrategyFollowCamera));
                var target = new GameObject("VistaProjectionTarget");
                try
                {
                    Camera projection = cameraOwner.GetComponent<Camera>();
                    projection.enabled = false;
                    projection.aspect = 16f / 9f;
                    Float3 camp = PveRegionLayout.GetCampCenter(1);
                    target.transform.position = new Vector3(camp.X - 14f, 0f, camp.Z);
                    StrategyFollowCamera rig = cameraOwner.GetComponent<StrategyFollowCamera>();
                    rig.SetGameplayComposition(GameModeId.PveLeveling);
                    rig.SetFollowTarget(target.transform);
                    Assert.That(rig.Yaw, Is.EqualTo(70f));
                    Assert.That(rig.Pitch, Is.EqualTo(20f));
                    Assert.That(projection.fieldOfView, Is.EqualTo(58f));

                    MeshFilter filter = vista.transform.Find("ErodedMountainHorizon").GetComponent<MeshFilter>();
                    float[] columns = { 0.05f, 0.15f, 0.25f, 0.35f, 0.45f, 0.5f,
                        0.55f, 0.65f, 0.75f, 0.85f, 0.95f };
                    float[] skyline = ProjectSkyline(projection, filter, columns);
                    float lowest = float.PositiveInfinity;
                    float highest = float.NegativeInfinity;
                    int cloudColumns = 0;
                    foreach (float height in skyline)
                    {
                        Assert.That(height, Is.InRange(0.82f, 0.995f),
                            "Visible terrain must close the ground horizon without covering the entire sky.");
                        lowest = Mathf.Min(lowest, height);
                        highest = Mathf.Max(highest, height);
                        if ((1f - height) * 1080f >= 100f) { cloudColumns++; }
                    }
                    Assert.That((highest - lowest) * 1080f, Is.GreaterThanOrEqualTo(60f),
                        "The rejected low perimeter varied only about 21 px; count actual clipped triangle silhouettes, not an offscreen peak.");
                    Assert.That(cloudColumns, Is.GreaterThanOrEqualTo(3),
                        "Several azimuths must retain at least a 100 px cloud band at the authored gameplay lens.");
                    Assert.That((skyline[3] - skyline[5]) * 1080f, Is.GreaterThanOrEqualTo(60f),
                        "The middle crest must fall into a genuine central saddle rather than one uniform mountain wall.");
                }
                finally
                {
                    Object.DestroyImmediate(cameraOwner);
                    Object.DestroyImmediate(target);
                }
            });
        }

        [TestCase(typeof(BoxCollider))]
        [TestCase(typeof(Rigidbody))]
        [TestCase(typeof(Light))]
        [TestCase(typeof(Camera))]
        public void Validator_RejectsGameplayOrAdditionalRenderingComponents(Type componentType)
        {
            WithVista((lighting, vista) =>
            {
                GameObject copy = Object.Instantiate(vista);
                try
                {
                    copy.AddComponent(componentType);
                    Assert.Throws<InvalidOperationException>(() => SunbreakVistaAuthoring.Validate(copy));
                }
                finally { Object.DestroyImmediate(copy); }
            });
        }

        private static void AssertOutsideCoreWhenRaised(Vector3 point)
        {
            Assert.That(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z), Is.False);
            if (point.y <= 0.18f) { return; }
            bool inside = point.x >= PveRegionLayout.MinimumX && point.x <= PveRegionLayout.MaximumX &&
                point.z >= PveRegionLayout.MinimumZ && point.z <= PveRegionLayout.MaximumZ;
            Assert.That(inside, Is.False, "Raised vista geometry must not imply walkable or blocking terrain in Core: " + point);
        }

        private static float[] ProjectSkyline(Camera camera, MeshFilter filter, float[] columns)
        {
            var result = new float[columns.Length];
            for (int i = 0; i < result.Length; i++) { result[i] = float.NegativeInfinity; }
            Vector3[] vertices = filter.sharedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++) { vertices[i] = filter.transform.TransformPoint(vertices[i]); }
            int[] triangles = filter.sharedMesh.triangles;
            var first = new Vector3[8];
            var second = new Vector3[8];
            var projected = new Vector3[8];
            for (int triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                for (int corner = 0; corner < 3; corner++) { first[corner] = vertices[triangles[triangle + corner]]; }
                int count = ClipDepth(camera, first, 3, second, camera.nearClipPlane, true);
                count = ClipDepth(camera, second, count, first, camera.farClipPlane, false);
                if (count < 3) { continue; }
                for (int i = 0; i < count; i++) { projected[i] = camera.WorldToViewportPoint(first[i]); }
                for (int column = 0; column < columns.Length; column++)
                {
                    float x = columns[column];
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 a = projected[i], b = projected[(i + 1) % count];
                        if (x < Mathf.Min(a.x, b.x) || x > Mathf.Max(a.x, b.x) ||
                            Mathf.Abs(a.x - b.x) < 0.000001f) { continue; }
                        float y = Mathf.Lerp(a.y, b.y, (x - a.x) / (b.x - a.x));
                        result[column] = Mathf.Max(result[column], y);
                    }
                }
            }
            return result;
        }

        private static int ClipDepth(Camera camera, Vector3[] input, int count,
            Vector3[] output, float depth, bool keepFarther)
        {
            int written = 0;
            Vector3 position = camera.transform.position;
            Vector3 forward = camera.transform.forward;
            float sign = keepFarther ? 1f : -1f;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = input[i], b = input[(i + 1) % count];
                float distanceA = (Vector3.Dot(a - position, forward) - depth) * sign;
                float distanceB = (Vector3.Dot(b - position, forward) - depth) * sign;
                if (distanceA >= 0f) { output[written++] = a; }
                if ((distanceA >= 0f) != (distanceB >= 0f))
                {
                    output[written++] = Vector3.Lerp(a, b, distanceA / (distanceA - distanceB));
                }
            }
            return written;
        }

        private static void WithVista(Action<WorldLightingPresenter, GameObject> inspect)
        {
            Scene scene = SceneManager.GetSceneByPath(BootstrapPath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened) { scene = EditorSceneManager.OpenScene(BootstrapPath, OpenSceneMode.Additive); }
                WorldLightingPresenter lighting = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    WorldLightingPresenter candidate = root.GetComponentInChildren<WorldLightingPresenter>(true);
                    if (candidate == null) { continue; }
                    Assert.That(lighting, Is.Null, "Bootstrap must have exactly one world lighting owner.");
                    lighting = candidate;
                }
                Assert.That(lighting, Is.Not.Null);
                Assert.That(lighting.SunbreakVista, Is.Not.Null, "Rebuild the generated Bootstrap after adding the vista authoring.");
                inspect(lighting, lighting.SunbreakVista);
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) { EditorSceneManager.CloseScene(scene, true); }
            }
        }
    }
}
