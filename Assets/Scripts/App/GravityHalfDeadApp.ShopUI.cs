using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private const int BoosterCount = 4;
        private const int DailyRewardedAdLimit = 3;

        private static readonly string[] BoosterIds =
            { "gravity_disc", "headstart", "mega_headstart", "multiplier_booster" };
        private static readonly string[] BoosterTitles =
            { "GRAVITY DISC", "HEADSTART", "MEGA HEADSTART", "MULTIPLIER BOOSTER" };
        private static readonly string[] BoosterTaglines =
        {
            "Double-tap to ride!",
            "Start your run ahead!",
            "Start much farther ahead!",
            "Score more, faster!"
        };
        private static readonly string[] BoosterDescriptions =
        {
            "Ride the gravity field and survive one fatal crash.\nLasts for 30 seconds.",
            "Start the run 750 meters ahead of the starting point.",
            "Start the run 1.5 km ahead of the starting point.",
            "Each use adds +1 to your multiplier.\nUse up to 3 times per run."
        };
        private static readonly long[] BoosterCoinCosts = { 300L, 1500L, 3000L, 2000L };
        private static readonly string[] BoosterArtworkResources =
        {
            "UI/Shop/Boosters/gravity_disc",
            "UI/Shop/Boosters/headstart",
            "UI/Shop/Boosters/mega_headstart",
            "UI/Shop/Boosters/multiplier_booster"
        };

        private GameObject gameShopContent;
        private GameObject shopBoostsPage;
        private GameObject shopStorePage;
        private Text shopStatusText;
        private Button shopBoostsNavigationButton;
        private Button shopStoreNavigationButton;
        private readonly Text[] shopBoosterInventoryTexts = new Text[BoosterCount];
        private readonly Text[] shopBoosterAdButtonTexts = new Text[BoosterCount];
        private readonly Text[] shopBoosterCostTexts = new Text[BoosterCount];
        private readonly Button[] shopBoosterAdButtons = new Button[BoosterCount];
        private readonly Button[] shopBoosterCoinButtons = new Button[BoosterCount];

        private static Color BoosterAccent(int index)
        {
            return index switch
            {
                0 => Hex("20CFFF"),
                1 => Hex("8EF13C"),
                2 => Hex("AD4CFF"),
                _ => Hex("FFAE2D")
            };
        }

        private void BuildShopPanel(Transform parent)
        {
            gameShopContent = new GameObject("SHOP · Store and Boosts");
            gameShopContent.transform.SetParent(parent, false);
            var rootRect = gameShopContent.AddComponent<RectTransform>();
            Stretch(rootRect);

            var background = CreateImage("Shop deep circuit void", gameShopContent.transform,
                Hex("01040B"), FullStretch());
            background.raycastTarget = true;
            BuildUpgradesCircuitBackdrop(gameShopContent.transform);

            shopBoostsPage = new GameObject("SHOP · Boosts Page");
            shopBoostsPage.transform.SetParent(gameShopContent.transform, false);
            var boostersRect = shopBoostsPage.AddComponent<RectTransform>();
            Stretch(boostersRect);
            BuildShopHeader(shopBoostsPage.transform);
            BuildBoosterScrollArea(shopBoostsPage.transform);

            shopStorePage = new GameObject("SHOP · Dynamic Store Page");
            shopStorePage.transform.SetParent(gameShopContent.transform, false);
            var catalogRect = shopStorePage.AddComponent<RectTransform>();
            Stretch(catalogRect);
            BuildStoreCatalogPage(shopStorePage.transform);

            BuildShopBottomNavigation(gameShopContent.transform);
            ShowShopSubPage(1);
            gameShopContent.SetActive(false);
        }

        private void BuildShopHeader(Transform parent)
        {
            var headerWash = CreateCard("Boosters header wash", parent, new Vector2(0f, 650f),
                new Vector2(1080f, 320f), new Color(0.005f, 0.018f, 0.052f, 0.98f), 0);
            headerWash.GetComponent<Image>().raycastTarget = false;

            BuildShopTitleWing(headerWash.transform, -385f, false);
            BuildShopTitleWing(headerWash.transform, 385f, true);

            var title = MakeText(headerWash.transform, "BOOSTERS", 84, FontStyle.BoldAndItalic,
                Color.white, new Vector2(0f, 55f), new Vector2(700f, 100f),
                TextAnchor.MiddleCenter, 4);
            AddGraphicOutline(title, Hex("0A3978"), 4f);
            var glow = title.gameObject.AddComponent<Shadow>();
            glow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f);
            glow.effectDistance = new Vector2(0f, -7f);

            MakeText(headerWash.transform, "POWER-UPS TO HELP YOU RUN FARTHER!", 27,
                FontStyle.Bold, Hex("9FEAFF"), new Vector2(0f, -18f),
                new Vector2(780f, 40f), TextAnchor.MiddleCenter, 2);

            shopStatusText = MakeText(headerWash.transform, string.Empty, 17, FontStyle.Bold,
                Hex("97B2D4"), new Vector2(0f, -88f), new Vector2(800f, 30f),
                TextAnchor.MiddleCenter, 1);
        }

        private void BuildShopTitleWing(Transform parent, float x, bool mirror)
        {
            var rail = CreateCard("Boosters title wing rail", parent, new Vector2(x, 58f),
                new Vector2(170f, 7f), Cyan, 4);
            rail.GetComponent<Image>().raycastTarget = false;
            for (var i = 0; i < 3; i++)
            {
                var direction = mirror ? -1f : 1f;
                var slash = CreateCard("Boosters title wing slash " + i, parent,
                    new Vector2(x + direction * (45f + i * 20f), 38f - i * 9f),
                    new Vector2(62f, 8f), Hex("168FEA"), 4);
                slash.transform.localEulerAngles = new Vector3(0f, 0f, mirror ? 38f : -38f);
                slash.GetComponent<Image>().raycastTarget = false;
            }
        }

        private void BuildBoosterScrollArea(Transform parent)
        {
            var scrollObject = new GameObject("Responsive booster cards");
            scrollObject.transform.SetParent(parent, false);
            var scrollRectTransform = scrollObject.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(12f, 220f);
            scrollRectTransform.offsetMax = new Vector2(-12f, -400f);
            var scrollHit = scrollObject.AddComponent<Image>();
            scrollHit.color = new Color(1f, 1f, 1f, 0.001f);

            var viewportObject = new GameObject("Booster card viewport");
            viewportObject.transform.SetParent(scrollObject.transform, false);
            var viewportRect = viewportObject.AddComponent<RectTransform>();
            Stretch(viewportRect);
            viewportObject.AddComponent<RectMask2D>();

            const float cardHeight = 276f;
            const float cardGap = 15f;
            const float padding = 10f;
            var contentHeight = padding * 2f + BoosterCount * cardHeight +
                                (BoosterCount - 1) * cardGap;

            var contentObject = new GameObject("Booster card content");
            contentObject.transform.SetParent(viewportObject.transform, false);
            var contentRect = contentObject.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, contentHeight);
            contentRect.anchoredPosition = Vector2.zero;

            for (var i = 0; i < BoosterCount; i++)
            {
                var y = -padding - cardHeight * 0.5f - i * (cardHeight + cardGap);
                BuildBoosterCard(contentObject.transform, i, new Vector2(0f, y));
            }

            var scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.10f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.14f;
            scroll.scrollSensitivity = 52f;
            scroll.verticalNormalizedPosition = 1f;
        }

        private void BuildBoosterCard(Transform parent, int index, Vector2 position)
        {
            var accent = BoosterAccent(index);
            var glow = CreateCard(BoosterTitles[index] + " outer glow", parent, position,
                new Vector2(1026f, 276f), new Color(accent.r, accent.g, accent.b, 0.38f), 25);
            var glowRect = glow.GetComponent<RectTransform>();
            // Stretch every card to the current viewport width. The old fixed-width card became
            // wider than the virtual canvas on narrow phones and forced artwork under the text.
            glowRect.anchorMin = new Vector2(0f, 1f);
            glowRect.anchorMax = new Vector2(1f, 1f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.anchoredPosition = new Vector2(0f, position.y);
            glowRect.sizeDelta = new Vector2(-4f, 276f);
            glow.GetComponent<Image>().raycastTarget = false;

            var faceColor = Color.Lerp(Hex("020610"), accent, 0.075f);
            faceColor.a = 0.995f;
            var card = CreateCard(BoosterTitles[index] + " booster card", glow.transform,
                Vector2.zero, new Vector2(1018f, 268f), faceColor, 22);
            SetShopArea(card.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var cardImage = card.GetComponent<Image>();
            cardImage.raycastTarget = false;
            AddGraphicOutline(cardImage, accent, 2.2f);

            var artWindow = CreateCard(BoosterTitles[index] + " art window", card.transform,
                Vector2.zero, Vector2.one, Hex("020711"), 19);
            SetShopArea(artWindow.GetComponent<RectTransform>(),
                new Vector2(0.012f, 0.065f), new Vector2(0.282f, 0.935f),
                Vector2.zero, Vector2.zero);
            var artWindowImage = artWindow.GetComponent<Image>();
            artWindowImage.raycastTarget = false;
            AddGraphicOutline(artWindowImage, new Color(accent.r, accent.g, accent.b, 0.92f), 1.8f);
            artWindow.AddComponent<RectMask2D>();
            var art = MakeTextureImage(BoosterTitles[index] + " artwork", artWindow.transform,
                Resources.Load<Texture2D>(BoosterArtworkResources[index]), Vector2.zero,
                new Vector2(270f, 226f));
            var artAspect = art.GetComponent<AspectRatioFitter>();
            if (artAspect != null)
                artAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            if (art.texture == null)
                Debug.LogError("Missing booster artwork: Resources/" + BoosterArtworkResources[index] + ".png");

            var textColumn = new GameObject(BoosterTitles[index] + " responsive text column");
            textColumn.transform.SetParent(card.transform, false);
            var textColumnRect = textColumn.AddComponent<RectTransform>();
            SetShopArea(textColumnRect, new Vector2(0.30f, 0.07f), new Vector2(0.725f, 0.93f),
                Vector2.zero, Vector2.zero);

            var title = MakeText(textColumn.transform, BoosterTitles[index], 33, FontStyle.Bold,
                Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleLeft, 2);
            SetShopArea(title.rectTransform, new Vector2(0f, 0.72f), new Vector2(1f, 1f),
                new Vector2(8f, 0f), new Vector2(-4f, 0f));
            title.resizeTextMinSize = 23;
            title.resizeTextMaxSize = 33;
            title.verticalOverflow = VerticalWrapMode.Truncate;

            var tagline = MakeText(textColumn.transform, BoosterTaglines[index], 21, FontStyle.Bold,
                accent, Vector2.zero, Vector2.one, TextAnchor.MiddleLeft, 1);
            SetShopArea(tagline.rectTransform, new Vector2(0f, 0.52f), new Vector2(1f, 0.72f),
                new Vector2(8f, 0f), new Vector2(-4f, 0f));
            tagline.resizeTextMinSize = 15;
            tagline.resizeTextMaxSize = 21;
            tagline.verticalOverflow = VerticalWrapMode.Truncate;

            var description = MakeText(textColumn.transform, BoosterDescriptions[index], 19,
                FontStyle.Normal, Hex("D0D8E7"), Vector2.zero, Vector2.one,
                TextAnchor.UpperLeft, 0);
            SetShopArea(description.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.50f),
                new Vector2(8f, 2f), new Vector2(-4f, -2f));
            description.resizeTextForBestFit = true;
            description.resizeTextMinSize = 14;
            description.resizeTextMaxSize = 19;
            description.lineSpacing = 0.90f;
            description.verticalOverflow = VerticalWrapMode.Truncate;

            BuildBoosterPurchaseControls(card.transform, index, accent);
        }

        private void BuildBoosterPurchaseControls(Transform parent, int index, Color accent)
        {
            var controls = CreateCard("Booster purchase controls", parent, Vector2.zero,
                Vector2.one, Color.Lerp(Hex("030713"), accent, 0.11f), 18);
            SetShopArea(controls.GetComponent<RectTransform>(),
                new Vector2(0.745f, 0.07f), new Vector2(0.988f, 0.93f),
                Vector2.zero, Vector2.zero);
            controls.GetComponent<Image>().raycastTarget = false;

            var capturedIndex = index;
            shopBoosterCoinButtons[index] = MakeButton(controls.transform, string.Empty,
                Vector2.zero, Vector2.one, Hex("08162A"),
                Color.white, 25, () => PurchaseBoosterWithCoins(capturedIndex));
            SetShopArea(shopBoosterCoinButtons[index].GetComponent<RectTransform>(),
                new Vector2(0.045f, 0.64f), new Vector2(0.955f, 0.96f),
                Vector2.zero, Vector2.zero);
            AddGraphicOutline(shopBoosterCoinButtons[index].GetComponent<Image>(), accent, 1.8f);
            BuildCanonicalGameCoin(shopBoosterCoinButtons[index].transform, new Vector2(-72f, 0f), 42f);
            shopBoosterCostTexts[index] = MakeText(shopBoosterCoinButtons[index].transform,
                BoosterCoinCosts[index].ToString("N0"), 29, FontStyle.Bold, Color.white,
                new Vector2(30f, 0f), new Vector2(135f, 56f), TextAnchor.MiddleCenter, 1);

            var orLabel = MakeText(controls.transform, "OR", 15, FontStyle.Bold, Hex("C6D0E0"),
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, 1);
            SetShopArea(orLabel.rectTransform, new Vector2(0f, 0.54f), new Vector2(1f, 0.66f),
                Vector2.zero, Vector2.zero);

            shopBoosterAdButtons[index] = MakeButton(controls.transform, string.Empty,
                Vector2.zero, Vector2.one,
                Color.Lerp(Hex("071123"), accent, 0.25f), Color.white, 22,
                () => WatchBoosterAd(capturedIndex));
            SetShopArea(shopBoosterAdButtons[index].GetComponent<RectTransform>(),
                new Vector2(0.045f, 0.25f), new Vector2(0.955f, 0.57f),
                Vector2.zero, Vector2.zero);
            AddGraphicOutline(shopBoosterAdButtons[index].GetComponent<Image>(), accent, 1.8f);
            shopBoosterAdButtonTexts[index] = MakeText(shopBoosterAdButtons[index].transform,
                "▶  WATCH AD · 0/3", 20, FontStyle.Bold, Color.white, Vector2.zero,
                Vector2.one, TextAnchor.MiddleCenter, 1);
            SetShopArea(shopBoosterAdButtonTexts[index].rectTransform, Vector2.zero, Vector2.one,
                new Vector2(5f, 3f), new Vector2(-5f, -3f));
            shopBoosterAdButtonTexts[index].resizeTextForBestFit = true;
            shopBoosterAdButtonTexts[index].resizeTextMinSize = 14;
            shopBoosterAdButtonTexts[index].resizeTextMaxSize = 20;
            shopBoosterAdButtonTexts[index].verticalOverflow = VerticalWrapMode.Truncate;

            shopBoosterInventoryTexts[index] = MakeText(controls.transform, "YOU HAVE: 0", 19,
                FontStyle.Bold, accent, Vector2.zero, Vector2.one,
                TextAnchor.MiddleCenter, 1);
            SetShopArea(shopBoosterInventoryTexts[index].rectTransform,
                new Vector2(0f, 0.02f), new Vector2(1f, 0.22f),
                new Vector2(3f, 0f), new Vector2(-3f, 0f));
            shopBoosterInventoryTexts[index].resizeTextMinSize = 14;
            shopBoosterInventoryTexts[index].resizeTextMaxSize = 19;
        }

        private static void SetShopArea(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private void BuildShopBottomNavigation(Transform parent)
        {
            var navigation = new GameObject("Fixed Store and Boosts navigation");
            navigation.transform.SetParent(parent, false);
            var navigationRect = navigation.AddComponent<RectTransform>();
            navigationRect.anchorMin = new Vector2(0f, 0f);
            navigationRect.anchorMax = new Vector2(1f, 0f);
            navigationRect.pivot = new Vector2(0.5f, 0f);
            navigationRect.offsetMin = Vector2.zero;
            navigationRect.offsetMax = new Vector2(0f, 180f);

            var background = CreateImage("Shop navigation background", navigation.transform,
                new Color(0.003f, 0.014f, 0.040f, 0.995f), FullStretch());
            background.raycastTarget = true;
            AddGraphicOutline(background, new Color(Cyan.r, Cyan.g, Cyan.b, 0.70f), 2f);

            shopStoreNavigationButton = MakeButton(navigation.transform, "▰  STORE",
                new Vector2(-255f, -14f), new Vector2(492f, 112f), Hex("061126"),
                Hex("BECBE0"), 28, () => ShowShopSubPage(0));
            AddGraphicOutline(shopStoreNavigationButton.GetComponent<Image>(), NeonPurple, 2f);

            shopBoostsNavigationButton = MakeButton(navigation.transform, "▤  BOOSTS",
                new Vector2(255f, -14f), new Vector2(492f, 112f), Hex("0B315F"),
                Cyan, 28, () => ShowShopSubPage(1));
            AddGraphicOutline(shopBoostsNavigationButton.GetComponent<Image>(), Cyan, 2f);

            var close = MakeButton(parent, "×", Vector2.zero, new Vector2(108f, 108f),
                Hex("B92E48"), Color.white, 66, HideGameTab);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(0f, 181f);
            closeRect.sizeDelta = new Vector2(108f, 108f);
            AddGraphicOutline(close.GetComponent<Image>(), Hex("FF9A8A"), 2.2f);
            close.gameObject.AddComponent<ButtonGlow>();
        }

        private void ShowShopSubPage(int page)
        {
            var showingBoosters = page == 1;
            if (shopBoostsPage != null)
                shopBoostsPage.SetActive(showingBoosters);
            if (shopStorePage != null)
                shopStorePage.SetActive(!showingBoosters);
            SetShopNavigationState(shopStoreNavigationButton, !showingBoosters, NeonPurple);
            SetShopNavigationState(shopBoostsNavigationButton, showingBoosters, Cyan);
            if (showingBoosters)
                RefreshShopUI();
            else
            {
                EnsureStoreCatalogSync();
                RefreshStoreCatalogUI();
            }
        }

        private static void SetShopNavigationState(Button button, bool active, Color accent)
        {
            if (button == null)
                return;
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = active ? Color.Lerp(Hex("061126"), accent, 0.28f) : Hex("061126");
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.color = active ? accent : Hex("BECBE0");
        }
    }
}
