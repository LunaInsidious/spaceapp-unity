// AI-assisted code (generated with Claude Code) — see README.
using System;
using LunarOutpost.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LunarOutpost.Game
{
    /// <summary>
    /// Procedural south-pole terrain, the low Sun, Earth, and the IEnv implementation.
    /// TODO(人間に確認): replace the generated terrain with PGDA DEM data (SPEC step 5).
    /// </summary>
    public sealed class MoonWorld : IEnv
    {
        public const float Size = 600f;          // m, square terrain
        public const float PlayRadius = 220f;    // player is kept inside this
        const int Res = 241;

        public static readonly Vector2 LanderPos = new Vector2(0f, 0f);
        public static readonly Vector2 CraterCenter = new Vector2(75f, 70f);
        const float CraterRadius = 38f;
        const float CraterDepth = 16f;
        const float IceRadius = 26f;

        readonly float[,] _h = new float[Res, Res];
        readonly float _sunElevationRad;
        public Light Sun { get; }
        public Transform Earth { get; }
        public Material BaseMaterial { get; }

        public MoonWorld(float sunElevationDeg)
        {
            _sunElevationRad = sunElevationDeg * Mathf.Deg2Rad;

            // A material in Resources keeps the URP Lit shader in WebGL builds (it is stripped otherwise).
            BaseMaterial = Resources.Load<Material>("materials/Lit");
            if (BaseMaterial == null) throw new MissingReferenceException("Resources/materials/Lit.mat is missing");

            BuildHeights();
            BuildTerrainObject();
            BuildIce();

            var sunGo = new GameObject("Sun");
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.intensity = 2.2f;
            Sun.color = new Color(1f, 0.98f, 0.94f);
            Sun.shadows = LightShadows.Soft;
            Sun.shadowBias = 0.05f;
            Sun.shadowNormalBias = 0.4f;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.shadowDistance = 450f;
                urp.shadowCascadeCount = 4;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.07f, 0.09f);
            RenderSettings.skybox = null;

            var earth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            earth.name = "Earth";
            UnityEngine.Object.Destroy(earth.GetComponent<Collider>());
            earth.transform.localScale = Vector3.one * 60f;
            earth.GetComponent<Renderer>().material = UnlitMat(new Color(0.3f, 0.5f, 1f));
            earth.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Earth = earth.transform;

            BuildStars();
        }

        public Material Mat(Color c) => new Material(BaseMaterial) { color = c };

        public static Material UnlitMat(Color c)
        {
            var src = Resources.Load<Material>("materials/Unlit");
            if (src == null) throw new MissingReferenceException("Resources/materials/Unlit.mat is missing");
            return new Material(src) { color = c };
        }

        // ---------- terrain ----------

        float RawHeight(float x, float z)
        {
            float h = 0f;
            h += (Mathf.PerlinNoise(x * 0.012f + 10f, z * 0.012f + 10f) - 0.5f) * 8f;
            h += (Mathf.PerlinNoise(x * 0.05f + 3f, z * 0.05f + 7f) - 0.5f) * 1.6f;
            // Massifs that throw long moving shadows across the plain.
            h += Gauss(x, z, -90f, 60f, 32f) * 34f;
            h += Gauss(x, z, 110f, -90f, 28f) * 26f;
            h += Gauss(x, z, -60f, -130f, 40f) * 20f;
            // Keep the landing site flat.
            float dl = new Vector2(x - LanderPos.x, z - LanderPos.y).magnitude;
            h *= Mathf.SmoothStep(0.35f, 1f, dl / 40f);
            // Crater: bowl + rim. Its floor never sees the low Sun (permanently shadowed region → ice).
            float dc = new Vector2(x - CraterCenter.x, z - CraterCenter.y).magnitude / CraterRadius;
            if (dc < 1f) h += -CraterDepth * (1f - dc * dc);
            h += Mathf.Exp(-(dc - 1f) * (dc - 1f) * 18f) * 6f;
            return h;
        }

        static float Gauss(float x, float z, float cx, float cz, float s)
        {
            float dx = x - cx, dz = z - cz;
            return Mathf.Exp(-(dx * dx + dz * dz) / (2f * s * s));
        }

        void BuildHeights()
        {
            for (int j = 0; j < Res; j++)
            for (int i = 0; i < Res; i++)
                _h[i, j] = RawHeight(-Size / 2 + i * Size / (Res - 1), -Size / 2 + j * Size / (Res - 1));
        }

        public float HeightAt(float x, float z)
        {
            float fx = Mathf.Clamp((x + Size / 2) / Size * (Res - 1), 0, Res - 1.001f);
            float fz = Mathf.Clamp((z + Size / 2) / Size * (Res - 1), 0, Res - 1.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = Mathf.Lerp(_h[i, j], _h[i + 1, j], tx);
            float b = Mathf.Lerp(_h[i, j + 1], _h[i + 1, j + 1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        void BuildTerrainObject()
        {
            var verts = new Vector3[Res * Res];
            var uv = new Vector2[Res * Res];
            for (int j = 0; j < Res; j++)
            for (int i = 0; i < Res; i++)
            {
                float x = -Size / 2 + i * Size / (Res - 1), z = -Size / 2 + j * Size / (Res - 1);
                verts[j * Res + i] = new Vector3(x, _h[i, j], z);
                uv[j * Res + i] = new Vector2(i / (float)(Res - 1), j / (float)(Res - 1));
            }
            var tris = new int[(Res - 1) * (Res - 1) * 6];
            int t = 0;
            for (int j = 0; j < Res - 1; j++)
            for (int i = 0; i < Res - 1; i++)
            {
                int a = j * Res + i;
                tris[t++] = a; tris[t++] = a + Res; tris[t++] = a + 1;
                tris[t++] = a + 1; tris[t++] = a + Res; tris[t++] = a + Res + 1;
            }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, vertices = verts, uv = uv, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Terrain");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat(new Color(0.52f, 0.51f, 0.49f));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;

            // A wide dark ground beyond the play area so the horizon is not an edge.
            var outer = GameObject.CreatePrimitive(PrimitiveType.Plane);
            outer.name = "OuterGround";
            UnityEngine.Object.Destroy(outer.GetComponent<Collider>());
            outer.transform.position = new Vector3(0, -6f, 0);
            outer.transform.localScale = new Vector3(400f, 1f, 400f);
            outer.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.35f, 0.35f, 0.34f));
        }

        void BuildIce()
        {
            var rnd = new System.Random(7);
            var iceMat = Mat(new Color(0.75f, 0.9f, 1f));
            for (int k = 0; k < 40; k++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2;
                float r = Mathf.Sqrt((float)rnd.NextDouble()) * IceRadius * 0.9f;
                float x = CraterCenter.x + Mathf.Cos(ang) * r, z = CraterCenter.y + Mathf.Sin(ang) * r;
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Ice";
                UnityEngine.Object.Destroy(c.GetComponent<Collider>());
                float s = 0.4f + (float)rnd.NextDouble() * 0.9f;
                c.transform.position = new Vector3(x, HeightAt(x, z) + s * 0.2f, z);
                c.transform.rotation = Quaternion.Euler(rnd.Next(0, 40), rnd.Next(0, 360), rnd.Next(0, 40));
                c.transform.localScale = new Vector3(s, s * 0.6f, s * 1.2f);
                c.GetComponent<Renderer>().sharedMaterial = iceMat;
            }
        }

        void BuildStars()
        {
            var rnd = new System.Random(42);
            var starMat = UnlitMat(new Color(0.85f, 0.85f, 0.9f));
            var parent = new GameObject("Stars").transform;
            for (int k = 0; k < 250; k++)
            {
                var d = new Vector3((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 0.9f + 0.05f, (float)rnd.NextDouble() * 2 - 1).normalized;
                var s = GameObject.CreatePrimitive(PrimitiveType.Quad);
                UnityEngine.Object.Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(parent);
                s.transform.position = d * 1500f;
                s.transform.rotation = Quaternion.LookRotation(d);
                s.transform.localScale = Vector3.one * (1.5f + (float)rnd.NextDouble() * 2.5f);
                var r = s.GetComponent<Renderer>();
                r.sharedMaterial = starMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ---------- sky ----------

        public Vector3 SunDirection(double azimuthRad)
        {
            float az = (float)azimuthRad;
            return new Vector3(Mathf.Sin(az) * Mathf.Cos(_sunElevationRad), Mathf.Sin(_sunElevationRad),
                Mathf.Cos(az) * Mathf.Cos(_sunElevationRad));
        }

        public void UpdateSky(double sunAzimuthRad, double timeHours, double sunPeriodHours, Vector3 cameraPos)
        {
            Sun.transform.rotation = Quaternion.LookRotation(-SunDirection(sunAzimuthRad));
            // Earth hangs near the horizon at the pole and bobs below it for part of the month.
            float phase = (float)Math.Cos(2 * Math.PI * timeHours / sunPeriodHours);
            float elev = Mathf.Lerp(-4f, 6f, (phase + 1f) / 2f) * Mathf.Deg2Rad;
            const float earthAz = -0.6f;
            var dir = new Vector3(Mathf.Sin(earthAz) * Mathf.Cos(elev), Mathf.Sin(elev), Mathf.Cos(earthAz) * Mathf.Cos(elev));
            Earth.position = cameraPos + dir * 1200f;
        }

        // ---------- IEnv ----------

        public bool IsSunlit(float x, float z, double sunAzimuthRad)
        {
            var origin = new Vector3(x, HeightAt(x, z) + 1.0f, z);
            return !Physics.Raycast(origin, SunDirection(sunAzimuthRad), 2000f);
        }

        public bool IsIceAt(float x, float z) =>
            new Vector2(x - CraterCenter.x, z - CraterCenter.y).magnitude < IceRadius;
    }
}
