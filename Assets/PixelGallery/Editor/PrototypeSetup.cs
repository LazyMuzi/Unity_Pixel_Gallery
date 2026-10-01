using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PixelGallery
{
    public static class PrototypeSetup
    {
        const string Folder = "Assets/PixelGallery/";
        [MenuItem("Pixel Gallery/Create M0 Prototype")]
        public static void Create()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            const string scenePath = "Assets/Scenes/M0Prototype.unity";
            if (File.Exists(scenePath))
            {
                Configure3DRenderer();
                var existing = SceneManager.GetSceneByPath(scenePath);
                if (existing.IsValid() && existing.isLoaded) SceneManager.SetActiveScene(existing);
                else existing = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                bool wasDirty = existing.isDirty;
                foreach (var root in existing.GetRootGameObjects())
                {
                    var existingPrototype = root.GetComponent<GalleryPrototype>();
                    if (existingPrototype != null) { AssignGalleryMaterials(existingPrototype); AssignProfiles(existingPrototype); }
                    var existingLight = root.GetComponent<Light>();
                    if (existingLight != null) existingLight.cullingMask &= ~(1 << 30);
                }
                if (!wasDirty) EditorSceneManager.SaveScene(existing);
                return;
            }
            Directory.CreateDirectory(Folder + "Data");
            AssetDatabase.Refresh();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "Data/GalleryKorean.asset");
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(Folder + "Fonts/NotoSansKR-Regular.otf"), 48, 5,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                font.name = "GalleryKorean";
                AssetDatabase.CreateAsset(font, Folder + "Data/GalleryKorean.asset");
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
            }
            var profiles = new[] {
                Profile("minimalist", "미니멀리스트", new[] { 0,1,6,8 }, new[] { .15,.15,.08,1.0 }, new[] { .5,.2,.15,.15 }),
                Profile("warm", "붉은색 할머니", new[] { 2,3,4 }, new[] { 1.0,.8,.9 }, new[] { .45,.3,.25 }),
                Profile("detail", "디테일 장인", new[] { 0,6,7 }, new[] { .85,.5,.25 }, new[] { .5,.3,.2 }) };
            Configure3DRenderer();
            var scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path)) scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            foreach (var root in scene.GetRootGameObjects())
            {
                var camera = root.GetComponent<Camera>();
                if (camera != null) { camera.orthographic = true; camera.backgroundColor = new Color32(248,243,232,255); camera.clearFlags = CameraClearFlags.SolidColor; }
            }
            var game = new GameObject("PixelGalleryPrototype");
            var prototype = game.AddComponent<GalleryPrototype>();
            var serialized = new SerializedObject(prototype);
            serialized.FindProperty("font").objectReferenceValue = font;
            var npcs = serialized.FindProperty("visitors"); npcs.arraySize = profiles.Length;
            for (int i = 0; i < profiles.Length; i++) npcs.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssignGalleryMaterials(prototype); AssignProfiles(prototype);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.gameObject.scene == scene) light.cullingMask &= ~(1 << 30);
            EditorSceneManager.SaveScene(scene, scenePath);
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets(); Selection.activeGameObject = game;
        }
        [MenuItem("Pixel Gallery/Use 3D Gallery Renderer")]
        public static void Configure3DRenderer()
        {
            const string rendererPath = Folder + "Data/GalleryRenderer3D.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer,rendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Folder + "Data/GalleryPipeline.asset");
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline,Folder + "Data/GalleryPipeline.asset");
            }
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList"); list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            EnsureMaterial("GalleryLit", "Universal Render Pipeline/Lit",false);
            EnsureMaterial("GalleryUnlit", "Universal Render Pipeline/Unlit",true);
            AssetDatabase.SaveAssets();
        }
        static void EnsureMaterial(string name, string shader, bool cutout)
        {
            string path = Folder + "Data/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var material = new Material(Shader.Find(shader)) { name = name };
            if (cutout)
            {
                material.SetFloat("_AlphaClip",1); material.SetFloat("_Cutoff",.5f);
                material.EnableKeyword("_ALPHATEST_ON"); material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            AssetDatabase.CreateAsset(material,path);
        }
        static void AssignGalleryMaterials(GalleryPrototype prototype)
        {
            var serialized = new SerializedObject(prototype);
            serialized.FindProperty("galleryLit").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Data/GalleryLit.mat");
            serialized.FindProperty("galleryUnlit").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Data/GalleryUnlit.mat");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static void AssignProfiles(GalleryPrototype prototype)
        {
            var profiles = new[] {
                Profile("minimalist", "미니멀리스트", new[] { 0,1,6,8 }, new[] { .15,.15,.08,1.0 }, new[] { .5,.2,.15,.15 }),
                Profile("warm", "붉은색 할머니", new[] { 2,3,4 }, new[] { 1.0,.8,.9 }, new[] { .45,.3,.25 }),
                Profile("detail", "디테일 장인", new[] { 0,6,7 }, new[] { .85,.5,.25 }, new[] { .5,.3,.2 }),
                Profile("order", "정돈러", new[] { 5,1 }, new[] { 1.0,.15 }, new[] { .8,.2 }),
                Profile("trend", "유행 추종자", new[] { 9 }, new[] { 1.0 }, new[] { 1.0 }) };
            var serialized = new SerializedObject(prototype); var npcs = serialized.FindProperty("visitors"); npcs.arraySize = profiles.Length;
            for (int i=0;i<profiles.Length;i++) npcs.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
        }
        static NpcProfile Profile(string id, string name, int[] indices, double[] targets, double[] weights)
        {
            var profile = AssetDatabase.LoadAssetAtPath<NpcProfile>(Folder + "Data/" + id + ".asset");
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<NpcProfile>();
            profile.Taste = new NpcTaste { Id = id, Name = name };
            for (int i = 0; i < 10; i++) profile.Taste.Tolerance[i] = 1;
            for (int i = 0; i < indices.Length; i++)
            {
                profile.Taste.Targets[indices[i]] = targets[i]; profile.Taste.Weights[indices[i]] = weights[i];
                profile.Taste.Tolerance[indices[i]] = indices[i] == 0 ? .25 : .4;
            }
            AssetDatabase.CreateAsset(profile, Folder + "Data/" + id + ".asset"); return profile;
        }
    }
}
