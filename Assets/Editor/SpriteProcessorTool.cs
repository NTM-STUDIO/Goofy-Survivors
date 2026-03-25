using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using System.Collections.Generic;

public class SpriteProcessorTool : EditorWindow
{
    [MenuItem("Tools/Auto-Process Enemy Sprites (Pivot Only)")]
    public static void ProcessEnemySprites()
    {
        string folderPath = "Assets/__Scripts/Enemy/Animations/EsqMelee";
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

        if (guids.Length == 0)
        {
            Debug.LogWarning($"[SpriteProcessor] Não foram encontradas texturas na pasta: {folderPath}");
            return;
        }

        int processed = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null && importer.spriteImportMode == SpriteImportMode.Multiple)
            {
                bool needsReimport = false;
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    needsReimport = true;
                }

                if (needsReimport)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }

                var factory = new SpriteDataProviderFactories();
                factory.Init();
                var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
                dataProvider.InitSpriteEditorDataProvider();

                var spriteRects = dataProvider.GetSpriteRects();
                bool changed = false;

                for (int i = 0; i < spriteRects.Length; i++)
                {
                    var rect = spriteRects[i];
                    if (rect.alignment != SpriteAlignment.BottomCenter)
                    {
                        rect.alignment = SpriteAlignment.BottomCenter;
                        rect.pivot = new Vector2(0.5f, 0f);
                        spriteRects[i] = rect;
                        changed = true;
                    }
                }

                if (changed)
                {
                    dataProvider.SetSpriteRects(spriteRects);
                    dataProvider.Apply();
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    processed++;
                }
            }
        }

        Debug.Log($"[SpriteProcessor] Sucesso! {processed} spritesheets tiveram o seu Pivot alterado para BottomCenter.");
    }
}