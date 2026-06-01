using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZBreakOut.Editor
{
    [InitializeOnLoad]
    public static class ProjectStartup
    {
        private static double _focusAt;
        private static int _remainingFocusAttempts;

        static ProjectStartup()
        {
            QueueVisualEditorPreparation();
        }

        [InitializeOnLoadMethod]
        private static void QueueVisualEditorPreparation()
        {
            EditorApplication.delayCall -= PrepareVisualEditor;
            EditorApplication.delayCall += PrepareVisualEditor;
        }

        private static void PrepareVisualEditor()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.path) && File.Exists(ProjectBuilder.ScenePath))
            {
                EditorSceneManager.OpenScene(ProjectBuilder.ScenePath, OpenSceneMode.Single);
                Debug.Log("zBreakOut loaded the visual Main scene automatically.");
            }

            if (!string.Equals(SceneManager.GetActiveScene().path, ProjectBuilder.ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _focusAt = EditorApplication.timeSinceStartup + 2d;
            _remainingFocusAttempts = 120;
            EditorApplication.update -= FocusVisualEditorWhenReady;
            EditorApplication.update += FocusVisualEditorWhenReady;
        }

        private static void FocusVisualEditorWhenReady()
        {
            if (EditorApplication.timeSinceStartup < _focusAt)
            {
                return;
            }

            _remainingFocusAttempts--;
            if (ProjectBuilder.TryFocusVisualPreview() || _remainingFocusAttempts <= 0)
            {
                EditorApplication.update -= FocusVisualEditorWhenReady;
            }
        }
    }

    public static class ProjectBuilder
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        private const string ArtDirectory = "Assets/Art";
        private const string PixelSpritePath = ArtDirectory + "/Pixel.png";
        private const string BallSpritePath = ArtDirectory + "/Ball.png";
        private const string BounceMaterialPath = ArtDirectory + "/BallBounce.physicsMaterial2D";

        private static readonly Color Background = FromHex("#071426");
        private static readonly Color Cyan = FromHex("#4CE6E6");
        private static readonly Color Cream = FromHex("#FFF2C9");

        [MenuItem("zBreakOut/Configure Visual Project")]
        public static void ConfigureProject()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));
            EnsureArtAssets();

            Sprite pixelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PixelSpritePath);
            Sprite ballSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BallSpritePath);
            PhysicsMaterial2D bounceMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(BounceMaterialPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject gameManager = new GameObject("GameManager - zBreakOut");
            gameManager.AddComponent<BreakoutGame>();

            GameObject preview = new GameObject("VISUAL PREVIEW - Breakout Level");
            preview.AddComponent<EditorPreviewOnly>();
            BuildVisualPreview(preview.transform, pixelSprite, ballSprite, bounceMaterial);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            PlayerSettings.companyName = "zSmerkat";
            PlayerSettings.productName = "zBreakOut";
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 540;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.bundleVersion = "1.0.0";

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = preview;
            Debug.Log("zBreakOut visual scene and player settings configured.");
        }

        [MenuItem("zBreakOut/Focus Visual Preview")]
        public static void FocusVisualPreview()
        {
            TryFocusVisualPreview();
        }

        public static bool TryFocusVisualPreview()
        {
            if (Application.isBatchMode)
            {
                return false;
            }

            GameObject preview = GameObject.Find("VISUAL PREVIEW - Breakout Level");
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                SceneView[] sceneViews = Resources.FindObjectsOfTypeAll<SceneView>();
                if (sceneViews.Length > 0)
                {
                    sceneView = sceneViews[0];
                }
            }

            if (preview == null || sceneView == null)
            {
                return false;
            }

            Selection.activeGameObject = preview;
            sceneView.in2DMode = true;
            sceneView.LookAt(new Vector3(0f, -0.1f, 0f), Quaternion.identity, 8f, true, true);
            sceneView.Repaint();
            return true;
        }

        [MenuItem("zBreakOut/Build Windows")]
        public static void BuildWindows()
        {
            ConfigureProject();
            string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "build", "zBreakOut.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            }

            Debug.Log("zBreakOut Windows build completed: " + outputPath);
        }

        private static void BuildVisualPreview(Transform preview, Sprite pixelSprite, Sprite ballSprite, PhysicsMaterial2D bounceMaterial)
        {
            CreatePreviewCamera(preview);

            Transform backgroundRoot = CreateGroup("01 - Background", preview);
            CreateRectangle("Background", Vector2.zero, new Vector2(16f, 18f), Background, -20, backgroundRoot, pixelSprite);
            CreateStars(backgroundRoot, pixelSprite);
            CreateRectangle("Header Line", new Vector2(0f, 6.1f), new Vector2(12.8f, 0.045f), new Color(0.3f, 0.9f, 0.9f, 0.24f), -5, backgroundRoot, pixelSprite);

            Transform arenaRoot = CreateGroup("02 - Arena Walls", preview);
            CreateWall("Left Wall", new Vector2(-6.65f, 0f), new Vector2(0.24f, 13.9f), arenaRoot, pixelSprite);
            CreateWall("Right Wall", new Vector2(6.65f, 0f), new Vector2(0.24f, 13.9f), arenaRoot, pixelSprite);
            CreateWall("Top Wall", new Vector2(0f, 6.85f), new Vector2(13.5f, 0.24f), arenaRoot, pixelSprite);
            GameObject dangerLine = CreateRectangle("Danger Line - Lose Zone", new Vector2(0f, -7.05f), new Vector2(13.2f, 0.05f), new Color(1f, 0.25f, 0.4f, 0.52f), -2, arenaRoot, pixelSprite);
            dangerLine.AddComponent<BoxCollider2D>().isTrigger = true;

            Transform gameplayRoot = CreateGroup("03 - Gameplay Objects", preview);
            CreatePreviewPaddle(gameplayRoot, pixelSprite);
            CreatePreviewBall(gameplayRoot, ballSprite, bounceMaterial);

            Transform brickRoot = CreateGroup("04 - Bricks (60)", preview);
            CreatePreviewBricks(brickRoot, pixelSprite);
        }

        private static void CreatePreviewCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Preview Camera - Visible in Game Tab");
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.localPosition = new Vector3(0f, -0.1f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
        }

        private static void CreateStars(Transform parent, Sprite pixelSprite)
        {
            Transform starsRoot = CreateGroup("Decorative Stars", parent);
            System.Random random = new System.Random(2204);
            for (int index = 0; index < 58; index++)
            {
                float x = Mathf.Lerp(-6.2f, 6.2f, (float)random.NextDouble());
                float y = Mathf.Lerp(-6.8f, 6.5f, (float)random.NextDouble());
                float size = Mathf.Lerp(0.025f, 0.085f, (float)random.NextDouble());
                Color color = index % 4 == 0 ? Cyan : Cream;
                color.a = Mathf.Lerp(0.28f, 0.8f, (float)random.NextDouble());
                CreateRectangle("Star " + (index + 1).ToString("00"), new Vector2(x, y), new Vector2(size, size), color, -10, starsRoot, pixelSprite);
            }
        }

        private static void CreateWall(string objectName, Vector2 position, Vector2 size, Transform parent, Sprite pixelSprite)
        {
            GameObject wall = CreateRectangle(objectName, position, size, new Color(0.29f, 0.9f, 0.9f, 0.62f), -1, parent, pixelSprite);
            wall.AddComponent<BoxCollider2D>();
        }

        private static void CreatePreviewPaddle(Transform parent, Sprite pixelSprite)
        {
            GameObject paddle = CreateRectangle("Paddle", new Vector2(0f, -6.25f), new Vector2(2.45f, 0.4f), Cyan, 5, parent, pixelSprite);
            paddle.AddComponent<BoxCollider2D>();
            AddChildRectangle(paddle, "Paddle Shadow", new Vector2(0.04f, -0.2f), Vector2.one, new Color(0f, 0f, 0f, 0.34f), 2, pixelSprite);
            AddChildRectangle(paddle, "Paddle Highlight", new Vector2(0f, 0.21f), new Vector2(0.82f, 0.16f), Cream, 6, pixelSprite);
        }

        private static void CreatePreviewBall(Transform parent, Sprite ballSprite, PhysicsMaterial2D bounceMaterial)
        {
            GameObject ball = new GameObject("Ball");
            ball.transform.SetParent(parent);
            ball.transform.localPosition = new Vector2(0f, -5.7f);
            ball.transform.localScale = Vector3.one * 0.58f;
            SpriteRenderer renderer = ball.AddComponent<SpriteRenderer>();
            renderer.sprite = ballSprite;
            renderer.color = Cream;
            renderer.sortingOrder = 8;
            CircleCollider2D collider = ball.AddComponent<CircleCollider2D>();
            collider.sharedMaterial = bounceMaterial;
            Rigidbody2D body = ball.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        private static void CreatePreviewBricks(Transform parent, Sprite pixelSprite)
        {
            Color[] rowColors =
            {
                FromHex("#FF5C7A"),
                FromHex("#FF9F68"),
                FromHex("#FFD166"),
                FromHex("#76E39F"),
                FromHex("#4CE6E6"),
                FromHex("#7CA8FF")
            };

            const int rows = 6;
            const int columns = 10;
            const float spacingX = 1.22f;
            const float spacingY = 0.72f;
            for (int row = 0; row < rows; row++)
            {
                Transform rowRoot = CreateGroup("Row " + (row + 1) + (row < 2 ? " - Reinforced" : string.Empty), parent);
                for (int column = 0; column < columns; column++)
                {
                    float x = (column - (columns - 1) * 0.5f) * spacingX;
                    float y = 4.9f - row * spacingY;
                    GameObject brick = CreateRectangle(
                        "Brick R" + (row + 1) + " C" + (column + 1),
                        new Vector2(x, y),
                        new Vector2(1.08f, 0.52f),
                        rowColors[row],
                        3,
                        rowRoot,
                        pixelSprite);
                    brick.AddComponent<BoxCollider2D>();
                    AddChildRectangle(brick, "Shadow", new Vector2(0.055f, -0.15f), Vector2.one, new Color(0f, 0f, 0f, 0.28f), 1, pixelSprite);
                    AddChildRectangle(brick, "Highlight", new Vector2(0f, 0.24f), new Vector2(0.78f, 0.12f), new Color(1f, 1f, 1f, 0.62f), 4, pixelSprite);
                }
            }
        }

        private static Transform CreateGroup(string objectName, Transform parent)
        {
            GameObject group = new GameObject(objectName);
            group.transform.SetParent(parent);
            return group.transform;
        }

        private static GameObject CreateRectangle(string objectName, Vector2 position, Vector2 size, Color color, int sortingOrder, Transform parent, Sprite pixelSprite)
        {
            GameObject rectangle = new GameObject(objectName);
            rectangle.transform.SetParent(parent);
            rectangle.transform.localPosition = position;
            rectangle.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = rectangle.AddComponent<SpriteRenderer>();
            renderer.sprite = pixelSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return rectangle;
        }

        private static void AddChildRectangle(GameObject parent, string objectName, Vector2 localPosition, Vector2 localScale, Color color, int sortingOrder, Sprite pixelSprite)
        {
            GameObject child = CreateRectangle(objectName, localPosition, localScale, color, sortingOrder, parent.transform, pixelSprite);
            child.transform.localPosition = localPosition;
        }

        private static void EnsureArtAssets()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Art"));
            WritePixelTexture(PixelSpritePath, 16, false);
            WritePixelTexture(BallSpritePath, 64, true);

            PhysicsMaterial2D bounceMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(BounceMaterialPath);
            if (bounceMaterial == null)
            {
                bounceMaterial = new PhysicsMaterial2D("Ball Bounce")
                {
                    bounciness = 1f,
                    friction = 0f
                };
                AssetDatabase.CreateAsset(bounceMaterial, BounceMaterialPath);
            }

            AssetDatabase.SaveAssets();
        }

        private static void WritePixelTexture(string assetPath, int textureSize, bool circle)
        {
            string fullPath = Path.GetFullPath(assetPath);
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[textureSize * textureSize];
            Vector2 center = new Vector2(textureSize * 0.5f, textureSize * 0.5f);
            float radius = textureSize * 0.48f;
            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    bool visible = !circle || Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) <= radius;
                    pixels[y * textureSize + x] = visible ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = textureSize;
            importer.alphaIsTransparency = true;
            importer.filterMode = circle ? FilterMode.Bilinear : FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static Color FromHex(string value)
        {
            Color color;
            return ColorUtility.TryParseHtmlString(value, out color) ? color : Color.white;
        }
    }
}
