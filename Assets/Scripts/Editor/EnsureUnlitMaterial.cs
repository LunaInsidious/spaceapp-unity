// AI-assisted code (generated with Claude Code) — see README.
using UnityEditor;
using UnityEngine;

namespace LunarOutpost.EditorTools
{
    /// <summary>Creates Resources/materials/{Lit,Unlit}.mat so the URP shaders ship in player builds.</summary>
    [InitializeOnLoad]
    static class EnsureUnlitMaterial
    {
        static EnsureUnlitMaterial()
        {
            Ensure("Assets/Resources/materials/Unlit.mat", "Universal Render Pipeline/Unlit");
            Ensure("Assets/Resources/materials/Lit.mat", "Universal Render Pipeline/Lit");
        }

        static void Ensure(string path, string shaderName)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException($"{shaderName} shader not found");
            AssetDatabase.CreateAsset(new Material(shader), path);
            AssetDatabase.SaveAssets();
        }
    }
}
