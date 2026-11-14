// AI-assisted code (generated with Claude Code) — see README.
using LunarOutpost.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarOutpost.Game
{
    /// <summary>Simple primitive models for the lander and the eight facilities.</summary>
    public static class FacilityViews
    {
        static GameObject Part(MoonWorld w, Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(t);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.GetComponent<Renderer>().sharedMaterial = w.Mat(c);
            return g;
        }

        public static GameObject Lander(MoonWorld w, Vector3 pos)
        {
            var root = new GameObject("Lander").transform;
            root.position = pos;
            var gold = new Color(0.85f, 0.68f, 0.25f);
            var grey = new Color(0.7f, 0.7f, 0.72f);
            Part(w, root, PrimitiveType.Cube, new Vector3(0, 2.2f, 0), new Vector3(3.2f, 1.8f, 3.2f), gold);
            Part(w, root, PrimitiveType.Cylinder, new Vector3(0, 4.0f, 0), new Vector3(2.2f, 0.9f, 2.2f), grey);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var d = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Part(w, root, PrimitiveType.Cylinder, d * 2.2f + Vector3.up * 1.0f, new Vector3(0.18f, 1.1f, 0.18f), grey, new Vector3(25f, a, 0) );
                Part(w, root, PrimitiveType.Cylinder, d * 2.7f + Vector3.up * 0.05f, new Vector3(0.7f, 0.05f, 0.7f), grey);
            }
            return root.gameObject;
        }

        public static GameObject Create(MoonWorld w, FacilityKind kind, Vector3 pos)
        {
            var root = new GameObject(kind.ToString()).transform;
            root.position = pos;
            switch (kind)
            {
                case FacilityKind.Habitat:
                    Part(w, root, PrimitiveType.Cylinder, new Vector3(0, 1.8f, 0), new Vector3(3.6f, 3.5f, 3.6f), new Color(0.9f, 0.9f, 0.88f), new Vector3(0, 0, 90));
                    Part(w, root, PrimitiveType.Cube, new Vector3(0, 1.2f, 2.0f), new Vector3(1.2f, 2f, 1f), new Color(0.6f, 0.6f, 0.65f));
                    break;
                case FacilityKind.Solar:
                    Part(w, root, PrimitiveType.Cylinder, new Vector3(0, 1.5f, 0), new Vector3(0.15f, 1.5f, 0.15f), new Color(0.6f, 0.6f, 0.6f));
                    Part(w, root, PrimitiveType.Cube, new Vector3(0, 3.2f, 0), new Vector3(3.5f, 2.4f, 0.08f), new Color(0.12f, 0.2f, 0.55f)).name = "Panel";
                    break;
                case FacilityKind.Battery:
                    Part(w, root, PrimitiveType.Cube, new Vector3(0, 0.7f, 0), new Vector3(1.8f, 1.4f, 1.2f), new Color(0.25f, 0.6f, 0.3f));
                    break;
                case FacilityKind.Antenna:
                    Part(w, root, PrimitiveType.Cylinder, new Vector3(0, 2.5f, 0), new Vector3(0.15f, 2.5f, 0.15f), new Color(0.7f, 0.7f, 0.7f));
                    Part(w, root, PrimitiveType.Sphere, new Vector3(0, 5.2f, 0), new Vector3(2.2f, 0.4f, 2.2f), new Color(0.95f, 0.95f, 0.95f), new Vector3(70f, 0, 0));
                    break;
                case FacilityKind.LifeSupport:
                    Part(w, root, PrimitiveType.Cube, new Vector3(0, 1f, 0), new Vector3(2.2f, 2f, 1.6f), new Color(0.8f, 0.45f, 0.2f));
                    Part(w, root, PrimitiveType.Cylinder, new Vector3(1.4f, 1.2f, 0), new Vector3(0.8f, 1.2f, 0.8f), new Color(0.75f, 0.85f, 0.95f));
                    break;
                case FacilityKind.Greenhouse:
                    Part(w, root, PrimitiveType.Cube, new Vector3(0, 1.1f, 0), new Vector3(3.5f, 2.2f, 2.5f), new Color(0.45f, 0.85f, 0.45f));
                    break;
                case FacilityKind.IceDrill:
                    Part(w, root, PrimitiveType.Cube, new Vector3(0, 0.6f, 0), new Vector3(2f, 1.2f, 2f), new Color(0.85f, 0.75f, 0.2f));
                    Part(w, root, PrimitiveType.Cylinder, new Vector3(0, 3f, 0), new Vector3(0.4f, 3f, 0.4f), new Color(0.5f, 0.5f, 0.55f));
                    break;
                case FacilityKind.Shield:
                    Part(w, root, PrimitiveType.Cube, new Vector3(0, 1.5f, 0), new Vector3(5f, 3f, 0.8f), new Color(0.45f, 0.43f, 0.4f));
                    break;
            }
            return root.gameObject;
        }

        /// <summary>Tints every renderer of a preview ghost.</summary>
        public static void Tint(GameObject g, Color c)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                r.material.color = c;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
