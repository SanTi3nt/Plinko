using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FunFlow.Plinko.Editor
{
    public static class PlinkoSceneBuilder
    {
        private const float DefaultPixelsPerUnit = 100f;

        private const string RootFolder = "Assets/_Project";
        private const string SpritesFolder = RootFolder + "/Art/Sprites";
        private const string MaterialsFolder = RootFolder + "/Art/Materials";
        private const string ConfigFolder = RootFolder + "/Config";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string ScenesFolder = RootFolder + "/Scenes";

        private const string CircleSpritePath = SpritesFolder + "/Circle.png";
        private const string GlowSpritePath = SpritesFolder + "/Glow.png";
        private const string RoundedSquareSpritePath = SpritesFolder + "/RoundedSquare.png";

        private const string BallMatPath = MaterialsFolder + "/BallBounce.physicsMaterial2D";
        private const string PegMatPath = MaterialsFolder + "/PegBounce.physicsMaterial2D";
        private const string ConfigAssetPath = ConfigFolder + "/PlinkoConfig.asset";

        private const string PegPrefabPath = PrefabsFolder + "/Peg.prefab";
        private const string SlotPrefabPath = PrefabsFolder + "/Slot.prefab";
        private const string BallPrefabPath = PrefabsFolder + "/Ball.prefab";
        private const string MainScenePath = ScenesFolder + "/Main.unity";

        [MenuItem("Plinko/Rebuild Scene & Assets")]
        public static void BuildProjectAssets()
        {
            EnsureDirectories();

            Sprite circleSprite = CreateAndImportSprite(CircleSpritePath, CreateCircleTexture(128));
            Sprite glowSprite = CreateAndImportSprite(GlowSpritePath, CreateGlowTexture(128));
            Sprite roundedSquareSprite = CreateAndImportSprite(RoundedSquareSpritePath, CreateRoundedSquareTexture(128, 0.22f));

            PhysicsMaterial2D ballMat = CreateOrUpdatePhysicsMaterial(BallMatPath, 0.45f, 0.05f);
            PhysicsMaterial2D pegMat = CreateOrUpdatePhysicsMaterial(PegMatPath, 0.52f, 0.02f);

            PlinkoConfig config = CreateOrLoadConfig();

            GameObject pegPrefab = CreatePegPrefab(config, circleSprite, glowSprite, pegMat);
            GameObject slotPrefab = CreateSlotPrefab(config, roundedSquareSprite, glowSprite);
            GameObject ballPrefabGo = CreateBallPrefab(config, circleSprite, ballMat);

            BuildMainScene(config, circleSprite, roundedSquareSprite, pegMat, pegPrefab, slotPrefab, ballPrefabGo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Plinko] Assets and Main.unity rebuilt.");
        }

        private static void EnsureDirectories()
        {
            string[] folders =
            {
                SpritesFolder,
                MaterialsFolder,
                ConfigFolder,
                PrefabsFolder,
                ScenesFolder
            };

            foreach (string assetPath in folders)
            {
                string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                }
            }

            AssetDatabase.Refresh();
        }

        private static Sprite CreateAndImportSprite(string assetPath, Texture2D generatedTexture)
        {
            byte[] pngBytes = generatedTexture.EncodeToPNG();
            Object.DestroyImmediate(generatedTexture);

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            File.WriteAllBytes(fullPath, pngBytes);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = DefaultPixelsPerUnit;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static PhysicsMaterial2D CreateOrUpdatePhysicsMaterial(string assetPath, float bounciness, float friction)
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(assetPath);
            if (mat == null)
            {
                mat = new PhysicsMaterial2D(Path.GetFileNameWithoutExtension(assetPath));
                mat.bounciness = bounciness;
                mat.friction = friction;
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else
            {
                mat.bounciness = bounciness;
                mat.friction = friction;
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        private static PlinkoConfig CreateOrLoadConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<PlinkoConfig>(ConfigAssetPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<PlinkoConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigAssetPath);
            }

            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        private static GameObject CreatePegPrefab(
            PlinkoConfig cfg,
            Sprite circleSprite,
            Sprite glowSprite,
            PhysicsMaterial2D pegMat)
        {
            var root = new GameObject("Peg");
            try
            {
                var bodySr = root.AddComponent<SpriteRenderer>();
                bodySr.sprite = circleSprite;
                bodySr.color = cfg.pegColor;
                bodySr.sortingOrder = 2;

                var col = root.AddComponent<CircleCollider2D>();
                col.radius = 0.58f;
                col.sharedMaterial = pegMat;

                var glowGo = new GameObject("Glow");
                glowGo.transform.SetParent(root.transform, false);
                glowGo.transform.localScale = new Vector3(2.15f, 2.15f, 1f);
                var glowSr = glowGo.AddComponent<SpriteRenderer>();
                glowSr.sprite = glowSprite;
                Color g = cfg.pegPulseColor;
                g.a = 0f;
                glowSr.color = g;
                glowSr.sortingOrder = 1;

                var pegView = root.AddComponent<PegView>();
                var so = new SerializedObject(pegView);
                so.FindProperty("bodyRenderer").objectReferenceValue = bodySr;
                so.FindProperty("glowRenderer").objectReferenceValue = glowSr;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, PegPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateSlotPrefab(
            PlinkoConfig cfg,
            Sprite roundedSquareSprite,
            Sprite glowSprite)
        {
            var root = new GameObject("Slot");
            try
            {
                root.transform.localScale = new Vector3(0.76f, 0.62f, 1f);

                var fillSr = root.AddComponent<SpriteRenderer>();
                fillSr.sprite = roundedSquareSprite;
                fillSr.color = cfg.slotNormalColor;
                fillSr.sortingOrder = 2;

                var boxCol = root.AddComponent<BoxCollider2D>();
                boxCol.isTrigger = true;
                boxCol.size = new Vector2(1.08f, 1.08f);

                var borderGo = new GameObject("Border");
                borderGo.transform.SetParent(root.transform, false);
                borderGo.transform.localScale = new Vector3(1.09f, 1.11f, 1f);
                var borderSr = borderGo.AddComponent<SpriteRenderer>();
                borderSr.sprite = roundedSquareSprite;
                borderSr.color = cfg.slotBorderColor;
                borderSr.sortingOrder = 1;

                var glowGo = new GameObject("Glow");
                glowGo.transform.SetParent(root.transform, false);
                glowGo.transform.localScale = new Vector3(1.65f, 1.65f, 1f);
                var glowSr = glowGo.AddComponent<SpriteRenderer>();
                glowSr.sprite = glowSprite;
                Color g = cfg.slotSelectedColor;
                g.a = 0f;
                glowSr.color = g;
                glowSr.sortingOrder = 0;

                var canvasGo = new GameObject("LabelCanvas");
                canvasGo.transform.SetParent(root.transform, false);
                canvasGo.transform.localScale = new Vector3(0.01f / 0.76f, 0.01f / 0.62f, 0.01f);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 4;

                var canvasRect = canvasGo.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(80f, 60f);

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(canvasGo.transform, false);
                var labelRect = labelGo.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                var labelText = labelGo.AddComponent<Text>();
                labelText.font = PlinkoHudController.GetBuiltinFont();
                labelText.fontSize = 36;
                labelText.fontStyle = FontStyle.Bold;
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.color = Color.white;
                labelText.text = "1";
                labelText.raycastTarget = false;

                var slotView = root.AddComponent<SlotView>();
                var so = new SerializedObject(slotView);
                so.FindProperty("fillRenderer").objectReferenceValue = fillSr;
                so.FindProperty("borderRenderer").objectReferenceValue = borderSr;
                so.FindProperty("glowRenderer").objectReferenceValue = glowSr;
                so.FindProperty("labelText").objectReferenceValue = labelText;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateBallPrefab(
            PlinkoConfig cfg,
            Sprite circleSprite,
            PhysicsMaterial2D ballMat)
        {
            var root = new GameObject("Ball");
            try
            {
                var bodySr = root.AddComponent<SpriteRenderer>();
                bodySr.sprite = circleSprite;
                bodySr.color = cfg.ballColor;
                bodySr.sortingOrder = 5;

                var rb = root.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 1.8f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;

                var col = root.AddComponent<CircleCollider2D>();
                col.radius = 0.58f;
                col.sharedMaterial = ballMat;

                var ball = root.AddComponent<PlinkoBall>();
                var ballSo = new SerializedObject(ball);
                ballSo.FindProperty("rb").objectReferenceValue = rb;
                ballSo.FindProperty("circleCollider").objectReferenceValue = col;
                ballSo.FindProperty("bodyRenderer").objectReferenceValue = bodySr;
                ballSo.ApplyModifiedPropertiesWithoutUndo();

                ball.ApplyConfig(cfg);

                return PrefabUtility.SaveAsPrefabAsset(root, BallPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildMainScene(
            PlinkoConfig cfg,
            Sprite circleSprite,
            Sprite roundedSquareSprite,
            PhysicsMaterial2D pegMat,
            GameObject pegPrefab,
            GameObject slotPrefab,
            GameObject ballPrefabGo)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Main Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 0.25f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = cfg.backgroundColor;
            camGo.AddComponent<AudioListener>();

            // PlinkoBoard
            var boardGo = new GameObject("PlinkoBoard");
            var board = boardGo.AddComponent<PlinkoBoard>();
            board.BuildBoard(cfg, circleSprite, roundedSquareSprite, pegMat, pegPrefab, slotPrefab);
            board.HighlightSelectedSlot(3);

            // ProceduralSfx
            var audioGo = new GameObject("ProceduralSfx");
            var sfx = audioGo.AddComponent<ProceduralSfx>();

            // Canvas + HUD
            PlinkoHudController hud = BuildHudCanvas(cfg, roundedSquareSprite);

            // PlinkoGameController
            var controllerGo = new GameObject("PlinkoGameController");
            var controller = controllerGo.AddComponent<PlinkoGameController>();
            var ballPrefab = ballPrefabGo.GetComponent<PlinkoBall>();
            controller.SetReferences(cfg, board, ballPrefab, hud, sfx, cam);
            EditorUtility.SetDirty(controller);

            // EventSystem
            var eventSystemGo = new GameObject("EventSystem");
            var eventSystem = eventSystemGo.AddComponent<EventSystem>();
            eventSystem.sendNavigationEvents = false;
            eventSystemGo.AddComponent<StandaloneInputModule>();

            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };
        }

        private static PlinkoHudController BuildHudCanvas(PlinkoConfig cfg, Sprite roundedSquareSprite)
        {
            Font font = PlinkoHudController.GetBuiltinFont();

            var canvasGo = new GameObject("HUDCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            var hud = canvasGo.AddComponent<PlinkoHudController>();

            // Top header bar
            RectTransform topBar = CreateUiPanel(
                "TopBar",
                canvasGo.transform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -14f),
                new Vector2(-80f, 92f),
                new Color(0.10f, 0.08f, 0.28f, 0.72f),
                roundedSquareSprite
            );

            CreateUiText(
                "TitleText",
                topBar,
                "PLINKO",
                font,
                34,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleLeft,
                new Vector2(0f, 0f),
                new Vector2(0.28f, 1f),
                new Vector2(28f, 0f),
                new Vector2(0f, 0f)
            );

            // Mode buttons in top center
            CreateUiButton(
                "ClassicModeButton",
                topBar,
                "Классика",
                font,
                24,
                new Color(0xF5 / 255f, 0xA6 / 255f, 0x23 / 255f, 1f),
                roundedSquareSprite,
                new Vector2(0.39f, 0.5f),
                new Vector2(0.39f, 0.5f),
                Vector2.zero,
                new Vector2(210f, 60f),
                out Button classicBtn,
                out Image classicBg
            );

            CreateUiButton(
                "GuidedModeButton",
                topBar,
                "Довести",
                font,
                24,
                new Color(0x47 / 255f, 0x3E / 255f, 0x91 / 255f, 1f),
                roundedSquareSprite,
                new Vector2(0.52f, 0.5f),
                new Vector2(0.52f, 0.5f),
                Vector2.zero,
                new Vector2(210f, 60f),
                out Button guidedBtn,
                out Image guidedBg
            );

            // Stats on top right
            Text scoreTxt = CreateUiText(
                "ScoreText",
                topBar,
                "Попадания: 0",
                font,
                24,
                FontStyle.Bold,
                new Color(0xFF / 255f, 0xD1 / 255f, 0x66 / 255f, 1f),
                TextAnchor.MiddleRight,
                new Vector2(0.60f, 0f),
                new Vector2(0.74f, 1f),
                Vector2.zero,
                Vector2.zero
            );

            Text attemptsTxt = CreateUiText(
                "AttemptsText",
                topBar,
                "Попытки: 0",
                font,
                24,
                FontStyle.Normal,
                Color.white,
                TextAnchor.MiddleRight,
                new Vector2(0.74f, 0f),
                new Vector2(0.87f, 1f),
                Vector2.zero,
                Vector2.zero
            );

            Text streakTxt = CreateUiText(
                "StreakText",
                topBar,
                "Серия: 0",
                font,
                24,
                FontStyle.Bold,
                new Color(0x9D / 255f, 0x8F / 255f, 0xF0 / 255f, 1f),
                TextAnchor.MiddleRight,
                new Vector2(0.87f, 0f),
                new Vector2(1f, 1f),
                Vector2.zero,
                new Vector2(-28f, 0f)
            );

            // Mode description line under header
            Text modeDescTxt = CreateUiText(
                "ModeDescriptionText",
                canvasGo.transform,
                "Режим: Классика (физика 2D) — угадай ячейку [1-7], [Space] бросить",
                font,
                22,
                FontStyle.Normal,
                new Color(0.85f, 0.83f, 0.98f, 0.9f),
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -126f),
                new Vector2(1300f, 40f),
                true
            );

            // Result Banner near bottom above control panel
            Text resultTxt = CreateUiText(
                "ResultBannerText",
                canvasGo.transform,
                string.Empty,
                font,
                32,
                FontStyle.Bold,
                new Color(0xFF / 255f, 0xD1 / 255f, 0x66 / 255f, 1f),
                TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 152f),
                new Vector2(900f, 54f),
                true
            );

            // Bottom Control Bar
            RectTransform bottomBar = CreateUiPanel(
                "BottomBar",
                canvasGo.transform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 18f),
                new Vector2(-80f, 112f),
                new Color(0.10f, 0.08f, 0.28f, 0.78f),
                roundedSquareSprite
            );

            var slotButtons = new Button[cfg.slotCount];
            var slotBgs = new Image[cfg.slotCount];

            float totalSlotsWidth = cfg.slotCount * 82f;
            float startX = -320f - totalSlotsWidth * 0.5f + 41f;
            for (int i = 0; i < cfg.slotCount; i++)
            {
                Color initialColor = i == 3 ? cfg.slotSelectedColor : cfg.slotNormalColor;
                CreateUiButton(
                    $"SlotQuickButton_{i + 1}",
                    bottomBar,
                    (i + 1).ToString(),
                    font,
                    28,
                    initialColor,
                    roundedSquareSprite,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(startX + i * 82f, 0f),
                    new Vector2(70f, 70f),
                    out slotButtons[i],
                    out slotBgs[i]
                );
            }

            CreateUiButton(
                "DropButton",
                bottomBar,
                "БРОСИТЬ ШАРИК",
                font,
                28,
                cfg.ballColor,
                roundedSquareSprite,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(280f, 0f),
                new Vector2(360f, 74f),
                out Button dropBtn,
                out _
            );

            hud.SetReferences(
                classicBtn,
                classicBg,
                guidedBtn,
                guidedBg,
                slotButtons,
                slotBgs,
                dropBtn,
                scoreTxt,
                attemptsTxt,
                streakTxt,
                modeDescTxt,
                resultTxt
            );

            EditorUtility.SetDirty(hud);
            return hud;
        }

        private static RectTransform CreateUiPanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color color,
            Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return rect;
        }

        private static Text CreateUiText(
            string name,
            Transform parent,
            string content,
            Font font,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMinOrPos,
            Vector2 offsetMaxOrSize,
            bool useAnchoredSize = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            if (useAnchoredSize)
            {
                rect.anchoredPosition = offsetMinOrPos;
                rect.sizeDelta = offsetMaxOrSize;
            }
            else
            {
                rect.offsetMin = offsetMinOrPos;
                rect.offsetMax = offsetMaxOrSize;
            }

            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.text = content;
            txt.raycastTarget = false;
            return txt;
        }

        private static void CreateUiButton(
            string name,
            Transform parent,
            string label,
            Font font,
            int fontSize,
            Color bgColor,
            Sprite bgSprite,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            out Button button,
            out Image background)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;

            background = go.AddComponent<Image>();
            background.sprite = bgSprite;
            background.color = bgColor;

            button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var txt = textGo.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = label;
            txt.raycastTarget = false;
        }

        private static Texture2D CreateCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float half = (size - 1) * 0.5f;
            float radius = size * 0.5f * PlinkoConfig.CircleFillFraction;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.75f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateGlowTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Glow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float half = (size - 1) * 0.5f;
            float maxDist = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxDist);
                    float falloff = 1f - t;
                    float alpha = Mathf.Clamp01(falloff * falloff * (1f - 0.15f * t));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateRoundedSquareTexture(int size, float cornerRadiusFraction)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RoundedSquare",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float half = (size - 1) * 0.5f;
            float halfBox = size * 0.5f * 0.94f;
            float cornerRadius = halfBox * Mathf.Clamp01(cornerRadiusFraction);
            float innerHalf = halfBox - cornerRadius;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x - half) - innerHalf;
                    float py = Mathf.Abs(y - half) - innerHalf;
                    float qx = Mathf.Max(px, 0f);
                    float qy = Mathf.Max(py, 0f);
                    float dist = Mathf.Sqrt(qx * qx + qy * qy) + Mathf.Min(Mathf.Max(px, py), 0f) - cornerRadius;
                    float alpha = Mathf.Clamp01(0.75f - dist);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
