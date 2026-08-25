using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        // Home menu is authored on a 1080 x 1920 portrait design surface and then
        // uniformly fitted inside the device safe area. This prevents the home UI from
        // overflowing on short phones while also keeping proportions on tall tablets/phones.
        private RectTransform gameHomeRoot;
        private Button gameCasinoLeverButton;
        private bool gameCasinoLeverBusy;
        private bool gameModeWasUserSelected;
        private bool gameLeverDragging;
        private Vector2 gameLeverDragPointerOffset;
        // Physical state positions: Endless = UP, Story = DOWN.
        private Vector2 gameCasinoLeverRestPosition;
        private Vector2 gameCasinoLeverDownPosition;
        private Button gameModeToggleButton;
        private Image gameModeToggleFrame;
        private Image gameModeToggleSurface;
        private Text gameModeToggleIcon;
        private Text gameModeToggleLabel;
        private Outline gameModeToggleOutline;
        private Image gameStoryLeverLamp;
        private Image gameEndlessLeverLamp;
        private RawImage gameLeverThumbImage;
        private RectTransform tapToPlayRoot;
        private RectTransform[] tapToPlayLetters;
        private Vector2[] tapToPlayLetterBasePositions;
        private GameObject topPlayerSummaryRoot;
        private Text gameHighScoreValueText;
        private Text gameMultiplierText;
        private Text gameGravityDiscAmountText;

        private static readonly Color HomeSteel = Hex("10151E");
        private static readonly Color HomeSteelSoft = Hex("1D2734");
        private static readonly Color HomeBrass = Hex("9D7432");
        private static readonly Color HomeBrassLight = Hex("E0BA68");
        private static readonly Color HomeRoseGold = Hex("E7B9A1");
        private static readonly Color HomePurple = Hex("9B37ED");
        private static readonly Color HomeVoid = Hex("02050D");

        private void BuildGameScreen()
        {
            var screen = CreateScreen("Game");

            // The old static/procedural wallpaper is intentionally removed. The new black-hole
            // wallpaper fills the complete Game screen and sits behind every existing UI element.
            CreateImage("Home full bleed void", screen.transform, HomeVoid, FullStretch()).raycastTarget = false;
            HomeBlackHoleBackgroundController.Create(screen.transform);

            gameHomeRoot = BuildResponsiveHomeRoot(screen.transform);
            BuildGameHeader(gameHomeRoot, screen.transform);
            BuildTapToPlay(gameHomeRoot);
            BuildGameModeSelector(gameHomeRoot);

            var bottomBar = BuildBottomNavigation(gameHomeRoot);
            BuildGameSectionPanel(gameHomeRoot, bottomBar);

            // Settings is an overlay. Keep it on the safe-area screen rather than inside
            // the 9:16 composition so it has the maximum usable area on every device.
            BuildGameSettings(screen.transform);
        }

        private static RectTransform BuildResponsiveHomeRoot(Transform parent)
        {
            var rootObject = new GameObject("Responsive Home · 1080x1920");
            rootObject.transform.SetParent(parent, false);
            var rect = rootObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1080f, 1920f);

            var fitter = rootObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1080f / 1920f;
            return rect;
        }

        private void BuildCodeGalaxyBackdrop(Transform parent)
        {
            CreateImage("Reference deep space", parent, Hex("020612"), FullStretch()).raycastTarget = false;

            // Soft nebula fields. They are deliberately oversized so scaling never exposes
            // a hard edge on wide or very tall devices.
            CreateCard("Nebula cyan field", parent, new Vector2(-330f, 150f),
                new Vector2(650f, 980f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.055f), 64)
                .GetComponent<Image>().raycastTarget = false;
            CreateCard("Nebula purple field", parent, new Vector2(330f, 90f),
                new Vector2(720f, 1040f), new Color(HomePurple.r, HomePurple.g, HomePurple.b, 0.065f), 64)
                .GetComponent<Image>().raycastTarget = false;

            var random = new System.Random(240822);
            for (var i = 0; i < 62; i++)
            {
                var x = random.Next(-510, 511);
                var y = random.Next(-930, 931);
                var size = random.Next(2, 8);
                var alpha = 0.18f + (float)random.NextDouble() * 0.46f;
                var star = CreateImage("Home star " + i, parent, new Color(1f, 1f, 1f, alpha),
                    Centered(new Vector2(x, y), new Vector2(size, size)), RoundedSprite(size));
                star.raycastTarget = false;
            }

            // Subtle circuit/digital rain details behind the glass hero.
            for (var i = 0; i < 13; i++)
            {
                var x = -470f + i * 78f;
                var line = CreateImage("Circuit rain " + i, parent,
                    new Color(Cyan.r, Cyan.g, Cyan.b, i % 2 == 0 ? 0.11f : 0.065f),
                    Centered(new Vector2(x, 120f + (i % 3) * 32f), new Vector2(3f, 910f)), RoundedSprite(2));
                line.raycastTarget = false;
            }
        }
        private void BuildGameHeader(Transform compositionParent, Transform safeScreenParent)
        {
            // Keep the black-hole wallpaper untouched. This HUD is just the top utility row.
            // No cyan horizontal border/rail is drawn around it.
            var utilityBarObject = new GameObject("Top utility HUD");
            utilityBarObject.transform.SetParent(safeScreenParent, false);
            var utilityBarRect = utilityBarObject.AddComponent<RectTransform>();
            // Stretch across the real safe-area width and pin to its top. This bypasses the
            // centered 9:16 composition's letterbox space on extra-tall phones.
            utilityBarRect.anchorMin = new Vector2(0f, 1f);
            utilityBarRect.anchorMax = new Vector2(1f, 1f);
            utilityBarRect.pivot = new Vector2(0.5f, 1f);
            utilityBarRect.anchoredPosition = Vector2.zero;
            // A shorter bar keeps the HUD at the very top and gives full-screen sections
            // breathing room instead of leaving a heavy empty strip below the resources.
            utilityBarRect.sizeDelta = new Vector2(0f, 222f);
            var utilityBar = utilityBarObject.AddComponent<Image>();
            utilityBar.color = new Color(0.012f, 0.040f, 0.060f, 0.94f);
            utilityBar.raycastTarget = false;

            BuildTopResourceCluster(utilityBarObject.transform);
            BuildTopGravityDisc(utilityBarObject.transform);
            BuildTopMultiplier(utilityBarObject.transform);
            BuildHardCodedSettingsButton(utilityBarObject.transform);
            BuildHardCodedPlayerAvatar(utilityBarObject.transform);

            // Restore the larger handwritten logo below the utility row.
            var titleFont = handwritingFont != null
                ? handwritingFont
                : ResolveHomeFont("Bangers", "Kalam", "Luckiest", "Comic");

            var title = MakeText(compositionParent, "GRAVITY:", 88, FontStyle.BoldAndItalic, Cream,
                new Vector2(0f, 668f), new Vector2(760f, 122f), TextAnchor.MiddleCenter, 5);
            title.font = titleFont;
            var cyanShadow = title.gameObject.AddComponent<Shadow>();
            cyanShadow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.82f);
            cyanShadow.effectDistance = new Vector2(-4f, -4f);
            var pinkShadow = title.gameObject.AddComponent<Shadow>();
            pinkShadow.effectColor = new Color(NeonPink.r, NeonPink.g, NeonPink.b, 0.80f);
            pinkShadow.effectDistance = new Vector2(5f, -4f);

            var subtitle = MakeText(compositionParent, "HALF DEAD", 43, FontStyle.BoldAndItalic, Hex("FF64DF"),
                new Vector2(0f, 587f), new Vector2(590f, 66f), TextAnchor.MiddleCenter, 10);
            subtitle.font = titleFont;
            var subGlow = subtitle.gameObject.AddComponent<Shadow>();
            subGlow.effectColor = new Color(NeonPink.r, NeonPink.g, NeonPink.b, 0.82f);
            subGlow.effectDistance = new Vector2(3f, -3f);

            RefreshTopCoinCounter();
            RefreshHomeHeaderDynamicValues();
        }
        private void BuildHardCodedPlayerAvatar(Transform parent)
        {
            // Borderless signed-in account photo. The transparent root is only the touch target;
            // the rounded mask crops the downloaded image without drawing a frame around it.
            topPlayerSummaryRoot = new GameObject("Top signed-in player summary");
            topPlayerSummaryRoot.transform.SetParent(parent, false);
            var summaryRect = topPlayerSummaryRoot.AddComponent<RectTransform>();
            SetRect(summaryRect, new Vector2(58f, -67f), new Vector2(86f, 86f));
            summaryRect.anchorMin = new Vector2(0f, 0.5f);
            summaryRect.anchorMax = new Vector2(0f, 0.5f);
            var rootImage = topPlayerSummaryRoot.AddComponent<Image>();
            rootImage.color = new Color(1f, 1f, 1f, 0.001f);
            rootImage.raycastTarget = true;
            var button = topPlayerSummaryRoot.AddComponent<Button>();
            button.targetGraphic = rootImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(OpenPlayerProfileOverlay);

            var maskObject = CreateCard("Borderless account avatar mask", topPlayerSummaryRoot.transform,
                Vector2.zero, new Vector2(82f, 82f), Color.white, 22);
            var maskImage = maskObject.GetComponent<Image>();
            var mask = maskObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var portraitObject = new GameObject("Signed-in account avatar");
            portraitObject.transform.SetParent(maskObject.transform, false);
            var portraitRect = portraitObject.AddComponent<RectTransform>();
            Stretch(portraitRect);
            topPlayerAvatarImage = portraitObject.AddComponent<RawImage>();
            topPlayerAvatarImage.color = Color.white;
            topPlayerAvatarImage.raycastTarget = false;

            topPlayerAvatarFallback = null;
            topPlayerAvatarFrameImage = null;

            var highScoreLabel = MakeText(topPlayerSummaryRoot.transform, "HIGH SCORE", 22,
                FontStyle.Bold, Cream, new Vector2(142f, 18f), new Vector2(190f, 32f),
                TextAnchor.MiddleLeft, 1);
            highScoreLabel.gameObject.name = "Realtime high score label";
            gameHighScoreValueText = MakeText(topPlayerSummaryRoot.transform, "0", 39,
                FontStyle.Bold, Color.white, new Vector2(142f, -20f), new Vector2(190f, 48f),
                TextAnchor.MiddleLeft, 0);
            gameHighScoreValueText.gameObject.name = "Realtime high score value";
            gameHighScoreValueText.resizeTextForBestFit = true;
            gameHighScoreValueText.resizeTextMinSize = 27;
            gameHighScoreValueText.resizeTextMaxSize = 39;
        }
        private void BuildTopResourceCluster(Transform parent)
        {
            // One relaxed left-to-right resource capsule:
            // revive icon -> revive amount -> G Coin -> coin amount -> Store plus.
            var resources = CreateCard("Top Gravity Core and G Coin resources", parent, Vector2.zero,
                new Vector2(420f, 82f), new Color(0.006f, 0.014f, 0.025f, 0.98f), 27);
            var resourcesRect = resources.GetComponent<RectTransform>();
            resourcesRect.anchorMin = new Vector2(0f, 0.5f);
            resourcesRect.anchorMax = new Vector2(0f, 0.5f);
            resourcesRect.pivot = new Vector2(0f, 0.5f);
            resourcesRect.anchoredPosition = new Vector2(18f, 30f);
            var resourcesImage = resources.GetComponent<Image>();
            resourcesImage.raycastTarget = false;
            AddGraphicOutline(resourcesImage, new Color(0f, 0f, 0f, 0.94f), 2.5f);

            gameGravityCoreAmountText = MakeText(resources.transform,
                bootstrapState != null ? bootstrapState.GravityCores.ToString("N0") : "0",
                34, FontStyle.Bold, Color.white, new Vector2(-126f, 0f),
                new Vector2(62f, 62f), TextAnchor.MiddleCenter, 0);
            gameGravityCoreAmountText.gameObject.name = "Top Gravity Core amount";
            gameGravityCoreAmountText.resizeTextForBestFit = true;
            gameGravityCoreAmountText.resizeTextMinSize = 25;
            gameGravityCoreAmountText.resizeTextMaxSize = 36;

            // This is a fixed-size HUD icon, so it intentionally does not use MakeTextureImage's
            // EnvelopeParent fitter (which is appropriate for large hero art, not compact icons).
            var gravityCoreObject = new GameObject("Label-free Gravity Core revive asset");
            gravityCoreObject.transform.SetParent(resources.transform, false);
            var gravityCoreRect = gravityCoreObject.AddComponent<RectTransform>();
            SetRect(gravityCoreRect, new Vector2(-177f, 0f), new Vector2(50f, 50f));
            var gravityCore = gravityCoreObject.AddComponent<RawImage>();
            // Use the first store crystal as the compact revive symbol so the HUD and store
            // share one visual language. The larger bundles use their own progressive art.
            gravityCore.texture = Resources.Load<Texture2D>(
                "UI/Shop/Store/Revive/revive_bundle_0");
            gravityCore.color = Color.white;
            gravityCore.raycastTarget = false;

            BuildCanonicalGameCoin(resources.transform, new Vector2(-68f, 0f), 46f);
            gameCoinAmountText = MakeText(resources.transform,
                bootstrapState != null ? bootstrapState.Coins.ToString("N0") : "0",
                36, FontStyle.Bold, Color.white, new Vector2(21f, 0f),
                new Vector2(118f, 62f), TextAnchor.MiddleCenter, 0);
            gameCoinAmountText.gameObject.name = "Top coin amount";
            gameCoinAmountText.resizeTextForBestFit = true;
            gameCoinAmountText.resizeTextMinSize = 25;
            gameCoinAmountText.resizeTextMaxSize = 38;

            BuildStorePlusButton(resources.transform, "Top resource Store add button",
                new Vector2(169f, 0f), () => OpenStoreForCurrency(StoreCurrencyKind.Coins));
        }

        private void BuildStorePlusButton(Transform parent, string objectName, Vector2 position,
            UnityEngine.Events.UnityAction action)
        {
            var add = CreateCard(objectName, parent, position, new Vector2(54f, 54f), Hex("78D85A"), 17);
            var addImage = add.GetComponent<Image>();
            var addButton = add.AddComponent<Button>();
            addButton.targetGraphic = addImage;
            addButton.navigation = new Navigation { mode = Navigation.Mode.None };
            addButton.onClick.AddListener(action);
            var plus = MakeText(add.transform, "+", 44, FontStyle.Normal, Color.white,
                new Vector2(0f, 1f), new Vector2(54f, 54f), TextAnchor.MiddleCenter, 0);
            plus.raycastTarget = false;
        }

        private void BuildTopGravityDisc(Transform parent)
        {
            // A self-contained black capsule like the left wallet: Disc -> count -> green plus.
            // The compact 170 px unit is placed at the midpoint of the free space between the
            // fixed left wallet content and the right multiplier content. This remains separated
            // even when the CanvasScaler produces a narrower local safe-area on tall phones.
            var discRoot = CreateCard("Top Gravity Disc inventory and Boosts shortcut", parent,
                new Vector2(76f, 30f), new Vector2(170f, 82f),
                new Color(0.006f, 0.014f, 0.025f, 0.98f), 27);
            discRoot.GetComponent<Image>().raycastTarget = false;

            gameGravityDiscAmountText = MakeText(discRoot.transform, "0", 36, FontStyle.Bold,
                Color.white, new Vector2(-2f, 0f), new Vector2(34f, 58f),
                TextAnchor.MiddleCenter, 0);
            gameGravityDiscAmountText.resizeTextForBestFit = true;
            gameGravityDiscAmountText.resizeTextMinSize = 23;
            gameGravityDiscAmountText.resizeTextMaxSize = 34;
            AddGraphicOutline(gameGravityDiscAmountText, Hex("04101E"), 1.2f);

            var discObject = new GameObject("Gravity Disc inventory asset");
            discObject.transform.SetParent(discRoot.transform, false);
            var discRect = discObject.AddComponent<RectTransform>();
            SetRect(discRect, new Vector2(-50f, 0f), new Vector2(58f, 40f));
            var disc = discObject.AddComponent<RawImage>();
            disc.texture = Resources.Load<Texture2D>("UI/Shop/Boosters/gravity_disc_enhanced");
            disc.color = Color.white;
            disc.raycastTarget = false;

            BuildStorePlusButton(discRoot.transform, "Top Gravity Disc Boosts shortcut",
                new Vector2(52f, 0f), OpenBoostsStore);

            RefreshGravityDiscHeader();
        }

        private void BuildTopMultiplier(Transform parent)
        {
            // Keep the multiplier outside the star: x1  ★  gear.
            var root = new GameObject("Firestore score multiplier");
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            SetRect(rect, new Vector2(-188f, 30f), new Vector2(174f, 94f));
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);

            gameMultiplierText = MakeText(root.transform, "x1", 34, FontStyle.Bold, Hex("FFD65A"),
                new Vector2(-47f, -4f), new Vector2(76f, 94f), TextAnchor.MiddleCenter, 0);
            gameMultiplierText.resizeTextForBestFit = true;
            gameMultiplierText.resizeTextMinSize = 23;
            gameMultiplierText.resizeTextMaxSize = 34;
            gameMultiplierText.alignByGeometry = true;
            AddGraphicOutline(gameMultiplierText, Hex("5C3508"), 1.8f);

            var star = MakeText(root.transform, "★", 88, FontStyle.Bold, Hex("FFD23E"),
                new Vector2(44f, 0f), new Vector2(92f, 92f), TextAnchor.MiddleCenter, 0);
            star.alignByGeometry = true;
            AddGraphicOutline(star, Hex("B86A16"), 2.5f);
            var starGlow = star.gameObject.AddComponent<Shadow>();
            starGlow.effectColor = new Color(1f, 0.68f, 0.08f, 0.40f);
            starGlow.effectDistance = new Vector2(2f, -2f);

        }

        // Canonical Gravity: Half Dead coin. All UI surfaces use this same construction so a future
        // world-space pickup can reproduce the same rim, face, core and highlight without redesigning it.
        private void BuildCanonicalGameCoin(Transform parent, Vector2 position, float diameter)
        {
            var scale = diameter / 60f;
            var coinOuter = CreateCard("Canonical game coin outer", parent, position,
                new Vector2(diameter, diameter), Hex("F6A90F"), Mathf.RoundToInt(diameter * 0.5f));
            coinOuter.GetComponent<Image>().raycastTarget = false;

            var coinInner = CreateCard("Canonical game coin inner", coinOuter.transform, Vector2.zero,
                new Vector2(48f, 48f) * scale, Hex("FFD65A"), Mathf.RoundToInt(24f * scale));
            coinInner.GetComponent<Image>().raycastTarget = false;

            var coinCore = CreateCard("Canonical game coin core", coinInner.transform,
                new Vector2(2f, -2f) * scale, new Vector2(36f, 36f) * scale,
                Hex("EFAE25"), Mathf.RoundToInt(18f * scale));
            coinCore.GetComponent<Image>().raycastTarget = false;

            var shine = CreateCard("Canonical game coin shine", coinInner.transform,
                new Vector2(-9f, 10f) * scale, new Vector2(12f, 17f) * scale,
                new Color(1f, 1f, 0.88f, 0.95f), Mathf.Max(1, Mathf.RoundToInt(6f * scale)));
            shine.GetComponent<Image>().raycastTarget = false;

            var gravityMark = MakeText(coinCore.transform, "G", Mathf.Max(9, Mathf.RoundToInt(29f * scale)),
                FontStyle.Bold, Hex("FFF1A0"), Vector2.zero, new Vector2(34f, 34f) * scale,
                TextAnchor.MiddleCenter, 1);
            gravityMark.raycastTarget = false;
            AddGraphicOutline(gravityMark, Hex("A85C08"), Mathf.Max(1f, 1.3f * scale));
        }

        private void BuildHardCodedSettingsButton(Transform parent)
        {
            // Borderless reference-sized gear. Keep a larger invisible touch target for accessibility.
            var outer = CreateCard("Top settings transparent hit target", parent, new Vector2(458f, 0f),
                new Vector2(96f, 96f), new Color(1f, 1f, 1f, 0.001f), 26);
            var outerRect = outer.GetComponent<RectTransform>();
            outerRect.anchorMin = new Vector2(1f, 0.5f);
            outerRect.anchorMax = new Vector2(1f, 0.5f);
            outerRect.anchoredPosition = new Vector2(-55f, 30f);
            var outerImage = outer.GetComponent<Image>();
            var button = outer.AddComponent<Button>();
            button.targetGraphic = outerImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(OpenGameSettings);

            BuildProceduralGear(outer.transform, 0.74f);
        }
        private void BuildProceduralGear(Transform parent, float scale)
        {
            var gearColor = Color.white;
            for (var i = 0; i < 8; i++)
            {
                var angleDegrees = i * 45f;
                var angle = angleDegrees * Mathf.Deg2Rad;
                var position = new Vector2(Mathf.Sin(angle) * 31f, Mathf.Cos(angle) * 31f) * scale;
                var tooth = CreateCard("Gear tooth " + i, parent, position,
                    new Vector2(16f, 28f) * scale, gearColor, Mathf.Max(2, Mathf.RoundToInt(4f * scale)));
                tooth.transform.localEulerAngles = new Vector3(0f, 0f, -angleDegrees);
                tooth.GetComponent<Image>().raycastTarget = false;
            }

            var ring = CreateCard("Gear outer ring", parent, Vector2.zero,
                new Vector2(72f, 72f) * scale, gearColor, Mathf.RoundToInt(36f * scale));
            ring.GetComponent<Image>().raycastTarget = false;
            var ringHole = CreateCard("Gear ring hole", ring.transform, Vector2.zero,
                new Vector2(46f, 46f) * scale, Hex("122235"), Mathf.RoundToInt(23f * scale));
            ringHole.GetComponent<Image>().raycastTarget = false;
            var hub = CreateCard("Gear hub", ringHole.transform, Vector2.zero,
                new Vector2(20f, 20f) * scale, gearColor, Mathf.RoundToInt(10f * scale));
            hub.GetComponent<Image>().raycastTarget = false;
        }

        private void BuildReferenceHeroScene(Transform parent)
        {
            // Wide rear glass card.
            var rearGlow = CreateCard("Rear glass cyan glow", parent, new Vector2(0f, 185f),
                new Vector2(930f, 420f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.34f), 58);
            rearGlow.GetComponent<Image>().raycastTarget = false;
            var rearRim = CreateCard("Rear glass chrome rim", rearGlow.transform, Vector2.zero,
                new Vector2(908f, 398f), new Color(0.78f, 0.93f, 1f, 0.52f), 54);
            rearRim.GetComponent<Image>().raycastTarget = false;
            var rearGlass = CreateCard("Rear glass surface", rearRim.transform, Vector2.zero,
                new Vector2(884f, 374f), new Color(0.05f, 0.16f, 0.27f, 0.32f), 48);
            rearGlass.GetComponent<Image>().raycastTarget = false;
            CreateImage("Rear glass cyan sweep", rearGlass.transform, new Color(Cyan.r, Cyan.g, Cyan.b, 0.13f),
                Centered(new Vector2(-180f, 45f), new Vector2(470f, 118f)), RoundedSprite(42)).raycastTarget = false;
            CreateImage("Rear glass purple sweep", rearGlass.transform, new Color(NeonPink.r, NeonPink.g, NeonPink.b, 0.08f),
                Centered(new Vector2(235f, -72f), new Vector2(390f, 100f)), RoundedSprite(38)).raycastTarget = false;

            // Tilted foreground glass card, matching the approved composition.
            var frontGlow = CreateCard("Foreground glass magenta glow", parent, new Vector2(0f, 215f),
                new Vector2(485f, 690f), new Color(NeonPink.r, NeonPink.g, NeonPink.b, 0.26f), 54);
            frontGlow.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
            frontGlow.GetComponent<Image>().raycastTarget = false;
            var frontChrome = CreateCard("Foreground glass chrome", frontGlow.transform, Vector2.zero,
                new Vector2(455f, 660f), new Color(0.82f, 0.92f, 1f, 0.58f), 50);
            frontChrome.GetComponent<Image>().raycastTarget = false;
            var frontGlass = CreateCard("Foreground glass surface", frontChrome.transform, Vector2.zero,
                new Vector2(427f, 630f), new Color(0.035f, 0.09f, 0.18f, 0.56f), 44);
            frontGlass.GetComponent<Image>().raycastTarget = false;

            var reflection = CreateImage("Foreground glass reflection", frontGlass.transform,
                new Color(1f, 1f, 1f, 0.12f),
                Centered(new Vector2(-55f, 122f), new Vector2(390f, 95f)), RoundedSprite(40));
            reflection.rectTransform.localEulerAngles = new Vector3(0f, 0f, 24f);
            reflection.raycastTarget = false;
            var lowerFacet = CreateImage("Foreground lower facet", frontGlass.transform,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.075f),
                Centered(new Vector2(80f, -185f), new Vector2(300f, 82f)), RoundedSprite(32));
            lowerFacet.rectTransform.localEulerAngles = new Vector3(0f, 0f, -18f);
            lowerFacet.raycastTarget = false;

            // Text remains upright, like a HUD printed over the tilted crystal.
            var infinity = MakeText(parent, "∞", 112, FontStyle.Bold, HomeRoseGold,
                new Vector2(0f, 178f), new Vector2(260f, 132f), TextAnchor.MiddleCenter, 2);
            var infinityShadow = infinity.gameObject.AddComponent<Shadow>();
            infinityShadow.effectColor = new Color(0f, 0f, 0f, 0.70f);
            infinityShadow.effectDistance = new Vector2(4f, -5f);
            var infinityGlow = infinity.gameObject.AddComponent<Shadow>();
            infinityGlow.effectColor = new Color(NeonPink.r, NeonPink.g, NeonPink.b, 0.25f);
            infinityGlow.effectDistance = new Vector2(-3f, 3f);

            var heroFont = ResolveHomeFont("Orbitron", "Oxanium", "Exo", "Rajdhani");
            var exploration = MakeText(parent, "INFINITE\nEXPLORATION", 27, FontStyle.Normal, HomeRoseGold,
                new Vector2(0f, 88f), new Vector2(420f, 100f), TextAnchor.MiddleCenter, 5);
            exploration.font = heroFont;
        }

        private void BuildTapToPlay(Transform parent)
        {
            // Free-floating text only: no frame, panel, border or visible background.
            var root = new GameObject("Free floating TAP TO PLAY");
            root.transform.SetParent(parent, false);
            tapToPlayRoot = root.AddComponent<RectTransform>();
            SetRect(tapToPlayRoot, new Vector2(0f, -320f), new Vector2(600f, 112f));

            // Keep a large invisible thumb-friendly hit target without changing the visual.
            var hitImage = root.AddComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            var button = root.AddComponent<Button>();
            button.targetGraphic = hitImage;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(OnTapToPlayPressed);

            var playFont = ResolveHomeFont("Bangers", "Luckiest", "Comic", "Kalam");
            const string caption = "Tap to Play";

            var letterCount = 0;
            for (var i = 0; i < caption.Length; i++)
                if (caption[i] != ' ')
                    letterCount++;

            tapToPlayLetters = new RectTransform[letterCount];
            tapToPlayLetterBasePositions = new Vector2[letterCount];

            // Slightly wider word gaps make the free text read like the reference.
            var totalWidth = 0f;
            for (var i = 0; i < caption.Length; i++)
                totalWidth += caption[i] == ' ' ? 30f : 49f;

            var cursorX = -totalWidth * 0.5f;
            var visibleIndex = 0;
            for (var i = 0; i < caption.Length; i++)
            {
                var character = caption[i];
                var advance = character == ' ' ? 30f : 49f;
                if (character == ' ')
                {
                    cursorX += advance;
                    continue;
                }

                var basePosition = new Vector2(cursorX + advance * 0.5f, 0f);
                var letter = MakeText(root.transform, character.ToString(), 54, FontStyle.BoldAndItalic, Color.white,
                    basePosition, new Vector2(70f, 88f), TextAnchor.MiddleCenter, 0);
                letter.font = playFont;

                // Letter-level shadow/outline only. The caption itself remains completely free-floating.
                var outline = letter.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.025f, 0.035f, 0.08f, 0.96f);
                outline.effectDistance = new Vector2(3f, -3f);
                outline.useGraphicAlpha = true;
                var glow = letter.gameObject.AddComponent<Shadow>();
                glow.effectColor = visibleIndex < letterCount / 2
                    ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.34f)
                    : new Color(NeonPink.r, NeonPink.g, NeonPink.b, 0.30f);
                glow.effectDistance = new Vector2(2f, -2f);
                glow.useGraphicAlpha = true;

                tapToPlayLetters[visibleIndex] = letter.rectTransform;
                tapToPlayLetterBasePositions[visibleIndex] = basePosition;
                visibleIndex++;
                cursorX += advance;
            }

            // Decorative wave happens automatically approximately every five seconds.
            StartCoroutine(TapToPlayIdleWaveLoop());
        }

        private void OnTapToPlayPressed()
        {
            if (tapToPlayRoot == null)
                return;
            BeginMissionRun();
            BeginGravityCoreRun();
            StopCoroutine(nameof(AnimateTapToPlay));
            StartCoroutine(AnimateTapToPlay());
        }

        private IEnumerator AnimateTapToPlay()
        {
            var start = Vector3.one;
            var pressed = new Vector3(0.97f, 0.92f, 1f);
            for (var t = 0f; t < 0.07f; t += Time.unscaledDeltaTime)
            {
                tapToPlayRoot.localScale = Vector3.Lerp(start, pressed, Smooth(t / 0.07f));
                yield return null;
            }
            tapToPlayRoot.localScale = pressed;
            for (var t = 0f; t < 0.12f; t += Time.unscaledDeltaTime)
            {
                tapToPlayRoot.localScale = Vector3.Lerp(pressed, start, Smooth(t / 0.12f));
                yield return null;
            }
            tapToPlayRoot.localScale = start;

            // Existing project does not expose a confirmed gameplay scene-launch method here.
            Debug.Log(endlessMode ? "TAP TO PLAY · ENDLESS" : "TAP TO PLAY · STORY");
        }

        private IEnumerator TapToPlayIdleWaveLoop()
        {
            // Keep the first frame calm, then run a short letter-by-letter dance every five seconds.
            while (tapToPlayLetters != null && tapToPlayLetters.Length > 0)
            {
                yield return new WaitForSecondsRealtime(5f);

                const float letterDelay = 0.065f;
                const float letterDanceDuration = 0.38f;
                var totalDuration = letterDanceDuration + (tapToPlayLetters.Length - 1) * letterDelay;

                for (var elapsed = 0f; elapsed < totalDuration; elapsed += Time.unscaledDeltaTime)
                {
                    for (var i = 0; i < tapToPlayLetters.Length; i++)
                    {
                        var letter = tapToPlayLetters[i];
                        if (letter == null)
                            continue;

                        var local = (elapsed - i * letterDelay) / letterDanceDuration;
                        if (local <= 0f || local >= 1f)
                        {
                            letter.anchoredPosition = tapToPlayLetterBasePositions[i];
                            letter.localScale = Vector3.one;
                            letter.localRotation = Quaternion.identity;
                            continue;
                        }

                        var wave = Mathf.Sin(local * Mathf.PI);
                        var side = Mathf.Sin(local * Mathf.PI * 2f);
                        letter.anchoredPosition = tapToPlayLetterBasePositions[i] + new Vector2(0f, wave * 18f);
                        letter.localScale = Vector3.one * (1f + wave * 0.12f);
                        letter.localRotation = Quaternion.Euler(0f, 0f, side * 4.5f);
                    }
                    yield return null;
                }

                ResetTapToPlayLetters();
            }
        }

        private void ResetTapToPlayLetters()
        {
            if (tapToPlayLetters == null || tapToPlayLetterBasePositions == null)
                return;

            for (var i = 0; i < tapToPlayLetters.Length; i++)
            {
                var letter = tapToPlayLetters[i];
                if (letter == null)
                    continue;
                letter.anchoredPosition = tapToPlayLetterBasePositions[i];
                letter.localScale = Vector3.one;
                letter.localRotation = Quaternion.identity;
            }
        }

        private void BuildGameModeSelector(Transform parent)
        {
            var root = new GameObject("Compact single-button casino mode selector");
            root.transform.SetParent(parent, false);
            var rootRect = root.AddComponent<RectTransform>();
            SetRect(rootRect, new Vector2(0f, -520f), new Vector2(1080f, 240f));

            // There is only ONE mode button. It must stay truly centered on the screen.
            // The lever lives to its right and visually indicates the active state.
            var modeFrameObject = CreateCard("Centered current mode accent frame", root.transform, new Vector2(0f, 0f),
                new Vector2(500f, 140f), NeonPink, 26);
            gameModeToggleFrame = modeFrameObject.GetComponent<Image>();
            gameModeToggleFrame.raycastTarget = false;
            gameModeToggleOutline = null;
            var frameShadow = modeFrameObject.AddComponent<Shadow>();
            frameShadow.effectColor = new Color(0f, 0f, 0f, 0.58f);
            frameShadow.effectDistance = new Vector2(0f, -6f);
            frameShadow.useGraphicAlpha = true;

            // Single clean border only: accent frame + dark face. No second inset ring.
            var modeSurfaceObject = CreateCard("Current mode button", modeFrameObject.transform, Vector2.zero,
                new Vector2(476f, 116f), HomeSteelSoft, 22);
            gameModeToggleSurface = modeSurfaceObject.GetComponent<Image>();
            gameModeToggleButton = modeSurfaceObject.AddComponent<Button>();
            gameModeToggleButton.targetGraphic = gameModeToggleSurface;
            gameModeToggleButton.transition = Selectable.Transition.ColorTint;
            gameModeToggleButton.navigation = new Navigation { mode = Navigation.Mode.None };
            gameModeToggleButton.onClick.AddListener(PullCasinoModeLever);

            gameModeToggleIcon = MakeText(modeSurfaceObject.transform, "∞", 52, FontStyle.Bold, HomeRoseGold,
                new Vector2(-146f, 1f), new Vector2(96f, 78f), TextAnchor.MiddleCenter, 1);
            gameModeToggleIcon.font = ResolveHomeFont("Kalam", "Bangers", "Luckiest", "Comic");

            gameModeToggleLabel = MakeText(modeSurfaceObject.transform, "ENDLESS MODE", 31,
                FontStyle.BoldAndItalic, Cream, new Vector2(42f, 0f), new Vector2(290f, 82f),
                TextAnchor.MiddleCenter, 1);
            gameModeToggleLabel.font = ResolveHomeFont("Kalam", "Bangers", "Luckiest", "Comic");
            var labelShadow = gameModeToggleLabel.gameObject.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0f, 0f, 0f, 0.88f);
            labelShadow.effectDistance = new Vector2(3f, -3f);
            labelShadow.useGraphicAlpha = true;

            BuildCasinoModeLever(root.transform);

            // Endless is the default and therefore the lever starts visibly UP.
            endlessMode = true;
            gameModeWasUserSelected = false;
            gameLeverDragging = false;
            UpdateGameModeUI();
        }

        private void BuildCasinoModeLever(Transform parent)
        {
            // The lever sits to the RIGHT of the centered button instead of shifting the
            // button away from center. The handle is intentionally prominent and draggable.
            var housing = CreateCard("Casino lever outer brass", parent, new Vector2(344f, 0f),
                new Vector2(144f, 226f), Hex("5B3C15"), 28);
            AddGraphicOutline(housing.GetComponent<Image>(), new Color(0.04f, 0.025f, 0.01f, 0.95f), 5f);

            var goldRim = CreateCard("Casino lever gold rim", housing.transform, Vector2.zero,
                new Vector2(130f, 212f), HomeBrassLight, 25);
            goldRim.GetComponent<Image>().raycastTarget = false;
            var face = CreateCard("Casino lever dark housing", goldRim.transform, Vector2.zero,
                new Vector2(114f, 196f), HomeSteel, 20);
            face.GetComponent<Image>().raycastTarget = false;

            var slotRim = CreateCard("Casino lever slot rim", face.transform, new Vector2(0f, -6f),
                new Vector2(58f, 148f), HomeBrassLight, 18);
            slotRim.GetComponent<Image>().raycastTarget = false;
            var slot = CreateCard("Casino lever slot", slotRim.transform, Vector2.zero,
                new Vector2(38f, 128f), Hex("050709"), 14);
            slot.GetComponent<Image>().raycastTarget = false;

            gameStoryLeverLamp = CreateImage("Story lever lamp", face.transform, Cyan,
                Centered(new Vector2(-43f, -63f), new Vector2(9f, 42f)), RoundedSprite(4));
            gameStoryLeverLamp.raycastTarget = false;
            gameEndlessLeverLamp = CreateImage("Endless lever lamp", face.transform, NeonPink,
                Centered(new Vector2(43f, -63f), new Vector2(9f, 42f)), RoundedSprite(4));
            gameEndlessLeverLamp.raycastTarget = false;

            var moving = new GameObject("Casino lever moving handle");
            moving.transform.SetParent(housing.transform, false);
            gameLeverHandle = moving.AddComponent<RectTransform>();
            SetRect(gameLeverHandle, new Vector2(0f, 36f), new Vector2(74f, 84f));
            gameCasinoLeverRestPosition = new Vector2(0f, 36f);
            gameCasinoLeverDownPosition = new Vector2(0f, -42f);
            gameLeverHandle.anchoredPosition = gameCasinoLeverRestPosition;

            CreateImage("Lever handle shadow", moving.transform, new Color(0f, 0f, 0f, 0.26f),
                Centered(new Vector2(3f, -3f), new Vector2(68f, 80f)), RoundedSprite(22)).raycastTarget = false;
            CreateImage("Lever handle glow", moving.transform, new Color(HomeBrassLight.r, HomeBrassLight.g, HomeBrassLight.b, 0.22f),
                Centered(Vector2.zero, new Vector2(70f, 82f)), RoundedSprite(24)).raycastTarget = false;

            var leverThumbTexture = Resources.Load<Texture2D>("UI/HomeArt/lever_handle");
            if (leverThumbTexture != null)
            {
                var handleArtObject = new GameObject("Lever visible handle");
                handleArtObject.transform.SetParent(moving.transform, false);
                var handleArtRect = handleArtObject.AddComponent<RectTransform>();
                SetRect(handleArtRect, new Vector2(0f, -1f), new Vector2(72f, 84f));
                gameLeverThumbImage = handleArtObject.AddComponent<RawImage>();
                gameLeverThumbImage.texture = leverThumbTexture;
                gameLeverThumbImage.color = Color.white;
                gameLeverThumbImage.raycastTarget = false;
                gameLeverThumbImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                var handleAspect = handleArtObject.AddComponent<AspectRatioFitter>();
                handleAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                handleAspect.aspectRatio = (float)leverThumbTexture.width / Mathf.Max(1f, leverThumbTexture.height);
            }
            else
            {
                CreateImage("Lever thumb stem", moving.transform, Hex("B78634"),
                    Centered(new Vector2(-2f, -9f), new Vector2(10f, 40f)), RoundedSprite(5)).raycastTarget = false;
                var thumbBase = CreateImage("Lever thumb base", moving.transform, Hex("6B4B1A"),
                    Centered(new Vector2(0f, -18f), new Vector2(28f, 16f)), RoundedSprite(8));
                thumbBase.raycastTarget = false;
                var thumbBallShadow = CreateImage("Lever thumb shadow", moving.transform, Hex("6B4B1A"),
                    Centered(new Vector2(0f, 9f), new Vector2(44f, 44f)), RoundedSprite(22));
                thumbBallShadow.raycastTarget = false;
                var thumbBall = CreateImage("Lever thumb ball", thumbBallShadow.transform, Hex("D8BD74"),
                    Centered(Vector2.zero, new Vector2(38f, 38f)), RoundedSprite(19));
                thumbBall.raycastTarget = false;
                CreateImage("Lever thumb gloss", thumbBall.transform, new Color(1f, 1f, 1f, 0.78f),
                    Centered(new Vector2(-7f, 7f), new Vector2(11f, 11f)), RoundedSprite(5)).raycastTarget = false;
            }

            var hit = new GameObject("Casino lever hit target");
            hit.transform.SetParent(housing.transform, false);
            var hitRect = hit.AddComponent<RectTransform>();
            SetRect(hitRect, Vector2.zero, new Vector2(184f, 248f));
            var hitImage = hit.AddComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            gameCasinoLeverButton = hit.AddComponent<Button>();
            gameCasinoLeverButton.targetGraphic = hitImage;
            gameCasinoLeverButton.navigation = new Navigation { mode = Navigation.Mode.None };
            gameCasinoLeverButton.onClick.AddListener(PullCasinoModeLever);
            ConfigureCasinoLeverDrag(hit);
            // Keep the artwork above every housing/hit graphic so it can never be hidden.
            gameLeverHandle.SetAsLastSibling();
        }

        private void ConfigureCasinoLeverDrag(GameObject target)
        {
            var trigger = target.AddComponent<EventTrigger>();
            AddLeverTrigger(trigger, EventTriggerType.BeginDrag, OnCasinoLeverBeginDrag);
            AddLeverTrigger(trigger, EventTriggerType.Drag, OnCasinoLeverDrag);
            AddLeverTrigger(trigger, EventTriggerType.EndDrag, OnCasinoLeverEndDrag);
        }

        private void AddLeverTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => callback(data));
            trigger.triggers.Add(entry);
        }

        private void OnCasinoLeverBeginDrag(BaseEventData data)
        {
            if (gameCasinoLeverBusy || gameLeverHandle == null)
                return;

            var pointer = data as PointerEventData;
            var parentRect = gameLeverHandle.parent as RectTransform;
            if (pointer == null || parentRect == null)
                return;

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, pointer.position, pointer.pressEventCamera, out localPoint))
                return;

            gameLeverDragging = true;
            gameLeverDragPointerOffset = gameLeverHandle.anchoredPosition - localPoint;
            if (gameModeToggleButton != null)
                gameModeToggleButton.interactable = false;
        }

        private void OnCasinoLeverDrag(BaseEventData data)
        {
            if (!gameLeverDragging || gameLeverHandle == null)
                return;

            var pointer = data as PointerEventData;
            var parentRect = gameLeverHandle.parent as RectTransform;
            if (pointer == null || parentRect == null)
                return;

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, pointer.position, pointer.pressEventCamera, out localPoint))
                return;

            var y = Mathf.Clamp(localPoint.y + gameLeverDragPointerOffset.y,
                gameCasinoLeverDownPosition.y, gameCasinoLeverRestPosition.y);
            gameLeverHandle.anchoredPosition = new Vector2(0f, y);
        }

        private void OnCasinoLeverEndDrag(BaseEventData data)
        {
            if (!gameLeverDragging || gameLeverHandle == null)
                return;

            gameLeverDragging = false;
            var midpoint = (gameCasinoLeverRestPosition.y + gameCasinoLeverDownPosition.y) * 0.5f;
            var snapToEndless = gameLeverHandle.anchoredPosition.y >= midpoint;
            StartCoroutine(AnimateCasinoModeLeverTo(snapToEndless));
        }

        private void PullCasinoModeLever()
        {
            if (gameCasinoLeverBusy || gameLeverHandle == null || gameLeverDragging)
                return;
            StartCoroutine(AnimateCasinoModeLeverTo(!endlessMode));
        }

        private IEnumerator AnimateCasinoModeLeverTo(bool nextEndless)
        {
            gameCasinoLeverBusy = true;
            if (gameCasinoLeverButton != null)
                gameCasinoLeverButton.interactable = false;
            if (gameModeToggleButton != null)
                gameModeToggleButton.interactable = false;

            var from = gameLeverHandle.anchoredPosition;
            var target = nextEndless ? gameCasinoLeverRestPosition : gameCasinoLeverDownPosition;
            var distance = Mathf.Abs(target.y - from.y);
            var duration = Mathf.Lerp(0.08f, 0.22f, Mathf.Clamp01(distance / 90f));
            var direction = nextEndless ? 1f : -1f;
            var overshoot = target + new Vector2(0f, direction * 7f);
            yield return MoveCasinoLever(from, overshoot, duration);
            yield return MoveCasinoLever(overshoot, target, 0.08f);

            gameModeWasUserSelected = true;
            endlessMode = nextEndless;
            UpdateGameModeUI();

            if (gameCasinoLeverButton != null)
                gameCasinoLeverButton.interactable = true;
            if (gameModeToggleButton != null)
                gameModeToggleButton.interactable = true;
            gameCasinoLeverBusy = false;
        }

        private IEnumerator MoveCasinoLever(Vector2 from, Vector2 to, float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Smooth(Mathf.Clamp01(elapsed / duration));
                gameLeverHandle.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                yield return null;
            }
            gameLeverHandle.anchoredPosition = to;
        }

        private void UpdateGameModeUI()
        {
            if (!gameModeWasUserSelected)
                endlessMode = true;

            // Keep the physical switch aligned with the mode whenever some other app flow
            // refreshes the UI. Never snap it while its own transition is playing.
            if (!gameCasinoLeverBusy && !gameLeverDragging && gameLeverHandle != null)
                gameLeverHandle.anchoredPosition = endlessMode
                    ? gameCasinoLeverRestPosition
                    : gameCasinoLeverDownPosition;

            var accent = endlessMode ? NeonPink : Cyan;
            var darkSurface = endlessMode ? Hex("351332") : Hex("12343D");

            if (gameModeToggleFrame != null)
                gameModeToggleFrame.color = accent;
            if (gameModeToggleOutline != null)
                gameModeToggleOutline.effectColor = new Color(accent.r, accent.g, accent.b, 0.82f);
            if (gameModeToggleSurface != null)
                gameModeToggleSurface.color = darkSurface;
            if (gameModeToggleIcon != null)
            {
                gameModeToggleIcon.text = endlessMode ? "∞" : "▤";
                gameModeToggleIcon.color = endlessMode ? HomeRoseGold : Cyan;
            }
            if (gameModeToggleLabel != null)
            {
                gameModeToggleLabel.text = endlessMode ? "ENDLESS MODE" : "STORY MODE";
                gameModeToggleLabel.color = Color.white;
            }

            if (gameStoryLeverLamp != null)
                gameStoryLeverLamp.color = endlessMode
                    ? new Color(Cyan.r, Cyan.g, Cyan.b, 0.18f)
                    : new Color(Cyan.r, Cyan.g, Cyan.b, 1f);
            if (gameEndlessLeverLamp != null)
                gameEndlessLeverLamp.color = endlessMode
                    ? new Color(NeonPink.r, NeonPink.g, NeonPink.b, 1f)
                    : new Color(NeonPink.r, NeonPink.g, NeonPink.b, 0.18f);

            if (gameModeText != null)
                gameModeText.text = endlessMode ? "ENDLESS" : "STORY";
            if (gameModeHintText != null)
            {
                gameModeHintText.text = endlessMode ? "ENDLESS BREACH" : "STORY BREACH";
                gameModeHintText.color = accent;
            }
        }

        private Font ResolveHomeFont(params string[] preferredNameParts)
        {
            var fonts = Resources.LoadAll<Font>("Fonts");
            for (var p = 0; p < preferredNameParts.Length; p++)
            {
                var wanted = preferredNameParts[p];
                for (var i = 0; i < fonts.Length; i++)
                {
                    if (fonts[i] != null && fonts[i].name.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return fonts[i];
                }
            }
            return handwritingFont != null ? handwritingFont : font;
        }
    }
}
