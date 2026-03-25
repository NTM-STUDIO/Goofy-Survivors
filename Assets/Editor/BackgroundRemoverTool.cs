using UnityEditor;
using UnityEngine;
using System.IO;

public class BackgroundRemoverTool : EditorWindow
{
    private Color chromaKeyColor = Color.green;
    private float tolerance = 0.1f;

    [MenuItem("Tools/Background Remover")]
    public static void ShowWindow()
    {
        GetWindow<BackgroundRemoverTool>("Background Remover");
    }

    private void OnGUI()
    {
        GUILayout.Label("Background Remover Settings", EditorStyles.boldLabel);
        
        chromaKeyColor = EditorGUILayout.ColorField("Chroma Key Color", chromaKeyColor);
        tolerance = EditorGUILayout.Slider("Tolerance", tolerance, 0f, 1f);

        GUILayout.Space(10);
        
        if (GUILayout.Button("Process Selected Images"))
        {
            ProcessSelectedImages();
        }
        
        GUILayout.Space(10);
        GUILayout.Label("" + Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets).Length + " images selected.", EditorStyles.helpBox);
    }

    private void ProcessSelectedImages()
    {
        Object[] selectedObjects = Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets);
        int processedCount = 0;

        foreach (Object obj in selectedObjects)
        {
            Texture2D tex = obj as Texture2D;
            if (tex == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(tex);
            if (!assetPath.ToLower().EndsWith(".png"))
            {
                Debug.LogWarning("Skipping non-PNG file: " + assetPath);
                continue;
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                bool wasReadable = importer.isReadable;
                TextureImporterCompression wasCompression = importer.textureCompression;
                
                if (!wasReadable || wasCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }

                // Resize via Graphics.Blit to 512x512 RenderTexture
                RenderTexture rt = RenderTexture.GetTemporary(512, 512, 0, RenderTextureFormat.ARGB32);
                RenderTexture activeRT = RenderTexture.active;
                RenderTexture.active = rt;

                GL.Clear(true, true, Color.clear);
                Graphics.Blit(tex, rt);

                Texture2D outputTex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                outputTex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                outputTex.Apply();

                RenderTexture.active = activeRT;
                RenderTexture.ReleaseTemporary(rt);

                // Chroma key via CPU
                Color[] pixels = outputTex.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (Vector3.Distance(new Vector3(pixels[i].r, pixels[i].g, pixels[i].b), 
                                         new Vector3(chromaKeyColor.r, chromaKeyColor.g, chromaKeyColor.b)) < tolerance)
                    {
                        pixels[i] = new Color(0, 0, 0, 0); // Transparent
                    }
                }
                outputTex.SetPixels(pixels);
                outputTex.Apply();

                // Overwrite original file
                byte[] pngData = outputTex.EncodeToPNG();
                File.WriteAllBytes(assetPath, pngData);
                
                DestroyImmediate(outputTex);

                if (!wasReadable || wasCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.isReadable = wasReadable;
                    importer.textureCompression = wasCompression;
                    importer.SaveAndReimport();
                }
                
                processedCount++;
            }
        }

        if (processedCount > 0)
        {
            AssetDatabase.Refresh();
            Debug.Log($"Processed {processedCount} image(s) successfully.");
        }
    }
}
