using UnityEditor;
using UnityEngine;

public class SortingLayerSetup : EditorWindow
{
    [MenuItem("Tools/Setup Sorting Layers")]
    public static void ShowWindow()
    {
        GetWindow<SortingLayerSetup>("Setup Sorting Layers");
    }

    private void OnGUI()
    {
        GUILayout.Label("Configure Prefab Sorting Layers", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Execute Setup"))
        {
            SetupLayers();
        }
    }

    private void SetupLayers()
    {
        AddSortingLayer("VFX");

        string[] allGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int updatedCount = 0;

        foreach (string guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            bool isPlayerOrEnemy = path.Contains("__Players") || path.Contains("Enemies") || path.Contains("Monster") || prefab.CompareTag("Player") || prefab.CompareTag("Enemy");
            bool isWeapon = path.Contains("__Abilities") || path.Contains("Weapon") || path.Contains("Projectiles");

            bool modified = false;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            Renderer[] renderers = contents.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r is SpriteRenderer || r is ParticleSystemRenderer)
                {
                    // 1. Reset everything to Default 0
                    string newLayer = "Default";
                    int newOrder = 0;

                    // 2. Players and Enemies to Default 1
                    if (isPlayerOrEnemy)
                    {
                        newLayer = "Default";
                        newOrder = 1;
                    }
                    // 3. Weapons to VFX
                    else if (isWeapon)
                    {
                        newLayer = "VFX";
                    }

                    if (r.sortingLayerName != newLayer || r.sortingOrder != newOrder)
                    {
                        r.sortingLayerName = newLayer;
                        r.sortingOrder = newOrder;
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                updatedCount++;
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }

        Debug.Log($"Sorting Layers setup complete. Reset ALL prefabs to Default 0. Applied Default 1 to Player/Enemy prefabs and VFX to Weapon prefabs. Modified {updatedCount} prefabs.");
    }

    private void AddSortingLayer(string layerName)
    {
        Object[] tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManagerAsset == null || tagManagerAsset.Length == 0) return;

        SerializedObject tagManager = new SerializedObject(tagManagerAsset[0]);
        SerializedProperty sortingLayers = tagManager.FindProperty("m_SortingLayers");

        if (sortingLayers == null) return;

        for (int i = 0; i < sortingLayers.arraySize; i++)
        {
            SerializedProperty layer = sortingLayers.GetArrayElementAtIndex(i);
            if (layer.FindPropertyRelative("name").stringValue == layerName)
            {
                return; // Already exists
            }
        }

        sortingLayers.InsertArrayElementAtIndex(sortingLayers.arraySize);
        SerializedProperty newLayer = sortingLayers.GetArrayElementAtIndex(sortingLayers.arraySize - 1);
        newLayer.FindPropertyRelative("name").stringValue = layerName;
        newLayer.FindPropertyRelative("uniqueID").intValue = layerName.GetHashCode();
        
        tagManager.ApplyModifiedProperties();
    }
}