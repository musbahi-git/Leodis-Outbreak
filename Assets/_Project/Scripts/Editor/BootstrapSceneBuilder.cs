using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace LeodisOutbreak.EditorTools
{
    /// <summary>
    /// Builds the Bootstrap scene from code so the scene is reproducible and reviewable:
    /// no hand-made Inspector wiring is required. Safe to re-run at any time.
    /// </summary>
    public static class BootstrapSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Bootstrap.unity";
        public const string UxmlPath = "Assets/_Project/UI/Bootstrap.uxml";
        public const string ThemePath = "Assets/_Project/UI/LeodisTheme.tss";
        public const string PanelSettingsPath = "Assets/_Project/UI/LeodisPanelSettings.asset";

        private static readonly Color CameraBackground = new Color(0.043f, 0.075f, 0.125f, 1f); // #0B1320

        [MenuItem("Leodis/Setup/Rebuild Bootstrap Scene", priority = 0)]
        public static void RebuildBootstrapScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Leodis: exit Play Mode before rebuilding the Bootstrap scene.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError("Leodis: could not load " + UxmlPath + ". Is the file present and imported?");
                return;
            }

            PanelSettings panelSettings = EnsurePanelSettings();
            if (panelSettings == null)
            {
                return;
            }

            Scene scene;
            bool isNew = !File.Exists(ScenePath);
            if (isNew)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Object.DestroyImmediate(root);
                }
            }

            CreateCamera();
            CreateEventSystem();
            CreateApp(uxml, panelSettings);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = isNew ? EditorSceneManager.SaveScene(scene, ScenePath) : EditorSceneManager.SaveScene(scene);
            if (!saved)
            {
                Debug.LogError("Leodis: failed to save " + ScenePath);
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("Leodis: Bootstrap scene rebuilt at " + ScenePath + " and set as the only scene in Build Profiles. Press Play to test.");
        }

        [MenuItem("Leodis/Setup/Validate Project Settings", priority = 1)]
        public static void ValidateProjectSettings()
        {
            const string expectedBundleId = "com.musbahi.leodisoutbreak";
            const string expectedProductName = "Leodis: Outbreak";
            int problems = 0;

            problems += Check(PlayerSettings.productName == expectedProductName,
                "Product name is '" + PlayerSettings.productName + "', expected '" + expectedProductName + "'.");

            problems += Check(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) == expectedBundleId,
                "Android package name is not " + expectedBundleId + ".");

            problems += Check(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS) == expectedBundleId,
                "iOS bundle identifier is not " + expectedBundleId + ".");

            problems += Check(PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait,
                "Default orientation is not Portrait.");

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            bool bootstrapFirst = scenes.Length > 0 && scenes[0].enabled && scenes[0].path == ScenePath;
            problems += Check(bootstrapFirst, "Bootstrap is not the first enabled scene in Build Profiles. Run Leodis > Setup > Rebuild Bootstrap Scene.");

            problems += Check(File.Exists(PanelSettingsPath), PanelSettingsPath + " is missing. Run Leodis > Setup > Rebuild Bootstrap Scene.");

            if (problems == 0)
            {
                Debug.Log("Leodis: project settings look correct (product name, identifiers, portrait, Bootstrap scene).");
            }
            else
            {
                Debug.LogWarning("Leodis: " + problems + " project setting issue(s) found. See warnings above.");
            }
        }

        // ------------------------------------------------------------------ helpers

        private static int Check(bool ok, string message)
        {
            if (ok)
            {
                return 0;
            }

            Debug.LogWarning("Leodis: " + message);
            return 1;
        }

        private static void CreateCamera()
        {
            GameObject go = ObjectFactory.CreateGameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            var camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CameraBackground;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
        }

        private static void CreateEventSystem()
        {
            GameObject go = ObjectFactory.CreateGameObject("EventSystem", typeof(EventSystem));
            var module = ObjectFactory.AddComponent<InputSystemUIInputModule>(go);

            // Use the project's default UI actions so UI Toolkit buttons react to touch and mouse.
            if (module.actionsAsset == null)
            {
                module.AssignDefaultActions();
            }
        }

        private static void CreateApp(VisualTreeAsset uxml, PanelSettings panelSettings)
        {
            GameObject go = ObjectFactory.CreateGameObject("App", typeof(UIDocument));
            var document = go.GetComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = uxml;
            ObjectFactory.AddComponent<GameApp>(go);
        }

        private static PanelSettings EnsurePanelSettings()
        {
            ThemeStyleSheet theme = EnsureTheme();
            if (theme == null)
            {
                return null;
            }

            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            bool created = false;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                created = true;
            }

            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1080, 1920);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.clearColor = false;
            settings.sortingOrder = 0;

            if (created)
            {
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }
            else
            {
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
            return settings;
        }

        private static ThemeStyleSheet EnsureTheme()
        {
            if (!File.Exists(ThemePath))
            {
                File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceSynchronousImport);
            }

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
            {
                Debug.LogError("Leodis: could not load theme at " + ThemePath);
            }

            return theme;
        }
    }
}
