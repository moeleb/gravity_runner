using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private GameObject BuildBottomNavigation(Transform parent)
        {
            // Only MISSIONS / ME / SHOP belong here. No extra Home/Trophy/Stats/Cart/Badge icon strip.
            var bottomBar = new GameObject("Responsive illustrated bottom navigation");
            bottomBar.transform.SetParent(parent, false);
            gameBottomNavigation = bottomBar;
            var barRect = bottomBar.AddComponent<RectTransform>();
            SetRect(barRect, new Vector2(0f, -800f), new Vector2(1030f, 286f));

            // A dark backing strip lets the individual neon/chrome cards read clearly on
            // every screen while preserving the reference proportions.
            var backing = CreateCard("Bottom nav dark backing", bottomBar.transform, new Vector2(0f, -4f),
                new Vector2(1000f, 270f), new Color(0.01f, 0.035f, 0.065f, 0.98f), 34);
            backing.GetComponent<Image>().raycastTarget = false;
            CreateImage("Bottom nav cyan top rail", backing.transform, new Color(Cyan.r, Cyan.g, Cyan.b, 0.48f),
                Centered(new Vector2(-245f, 128f), new Vector2(480f, 5f)), RoundedSprite(3)).raycastTarget = false;
            CreateImage("Bottom nav magenta top rail", backing.transform, new Color(NeonPink.r, NeonPink.g, NeonPink.b, 0.48f),
                Centered(new Vector2(245f, 128f), new Vector2(480f, 5f)), RoundedSprite(3)).raycastTarget = false;

            BuildReferenceNavCard(bottomBar.transform, "MISSIONS", "UI/HomeArt/missions_art", Cyan,
                new Vector2(-333f, 0f), () => ShowGameTab("MISSIONS"));
            BuildReferenceNavCard(bottomBar.transform, "ME", "UI/HomeArt/me_art", NeonLime,
                Vector2.zero, () => ShowGameTab("ME"));
            BuildReferenceNavCard(bottomBar.transform, "SHOP", "UI/HomeArt/shop_art", NeonPink,
                new Vector2(333f, 0f), () => ShowGameTab("SHOP"));
            return bottomBar;
        }

        private Button BuildReferenceNavCard(Transform parent, string label, string artPath, Color accent,
            Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var root = new GameObject(label + " reference navigation card");
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            SetRect(rect, position, new Vector2(318f, 276f));
            root.transform.localScale = Vector3.one * 0.90f;

            // Shadow -> accent glow -> metal shell -> dark face. All of this is hard coded;
            // only the detailed character/item illustration inside the window is a texture.
            var shadow = CreateCard(label + " card shadow", root.transform, new Vector2(0f, -6f),
                new Vector2(318f, 270f), new Color(0f, 0f, 0f, 0.78f), 30);
            shadow.GetComponent<Image>().raycastTarget = false;

            var glowColor = accent;
            glowColor.a = 0.72f;
            var glow = CreateCard(label + " neon card glow", root.transform, Vector2.zero,
                new Vector2(312f, 266f), glowColor, 29);
            glow.GetComponent<Image>().raycastTarget = false;

            var metal = CreateCard(label + " chrome card", glow.transform, Vector2.zero,
                new Vector2(296f, 250f), Hex("8895A6"), 27);
            metal.GetComponent<Image>().raycastTarget = false;
            var face = CreateCard(label + " dark card face", metal.transform, Vector2.zero,
                new Vector2(280f, 234f), Hex("06101C"), 24);
            face.GetComponent<Image>().raycastTarget = false;

            // Artwork viewport. The crop contains only the detailed illustration from the
            // approved reference; label, frame, glow, highlight and input are code-driven.
            var viewport = CreateCard(label + " artwork viewport", face.transform, new Vector2(0f, 31f),
                new Vector2(264f, 168f), Hex("09182A"), 19);
            var viewportImage = viewport.GetComponent<Image>();
            viewportImage.raycastTarget = false;
            viewport.AddComponent<Mask>().showMaskGraphic = true;

            var artObject = new GameObject(label + " illustration");
            artObject.transform.SetParent(viewport.transform, false);
            var artRect = artObject.AddComponent<RectTransform>();
            Stretch(artRect);
            var art = artObject.AddComponent<RawImage>();
            art.texture = Resources.Load<Texture2D>(artPath);
            art.color = Color.white;
            art.raycastTarget = false;
            if (art.texture == null)
                Debug.LogError("Missing bottom navigation art: Resources/" + artPath + ".png");

            // Light sweep and colored inner rails reproduce the polished card treatment.
            CreateImage(label + " glass sweep", viewport.transform, new Color(1f, 1f, 1f, 0.10f),
                Centered(new Vector2(-52f, 57f), new Vector2(210f, 20f)), RoundedSprite(10)).raycastTarget = false;
            CreateImage(label + " left inner rail", face.transform, accent,
                Centered(new Vector2(-132f, 27f), new Vector2(5f, 150f)), RoundedSprite(3)).raycastTarget = false;
            CreateImage(label + " right inner rail", face.transform, new Color(accent.r, accent.g, accent.b, 0.46f),
                Centered(new Vector2(132f, 27f), new Vector2(5f, 150f)), RoundedSprite(3)).raycastTarget = false;

            var labelFont = ResolveHomeFont("Bangers", "Luckiest", "Comic", "Kalam");
            var title = MakeText(face.transform, label, label == "MISSIONS" ? 42 : 49,
                FontStyle.BoldAndItalic, Hex("FFB32F"), new Vector2(0f, -82f),
                new Vector2(272f, 78f), TextAnchor.MiddleCenter, 1);
            title.font = labelFont;
            var heavyOutline = title.gameObject.AddComponent<Outline>();
            heavyOutline.effectColor = Hex("111319");
            heavyOutline.effectDistance = new Vector2(5f, -5f);
            heavyOutline.useGraphicAlpha = true;
            var whiteEdge = title.gameObject.AddComponent<Shadow>();
            whiteEdge.effectColor = Color.white;
            whiteEdge.effectDistance = new Vector2(2f, -2f);
            whiteEdge.useGraphicAlpha = true;
            var orangeGlow = title.gameObject.AddComponent<Shadow>();
            orangeGlow.effectColor = new Color(1f, 0.45f, 0.05f, 0.62f);
            orangeGlow.effectDistance = new Vector2(4f, -4f);

            // Small neon status lamp under every card, like the approved reference.
            var lampBase = CreateCard(label + " lower lamp base", root.transform, new Vector2(0f, -129f),
                new Vector2(92f, 17f), Hex("0A1018"), 8);
            lampBase.GetComponent<Image>().raycastTarget = false;
            CreateImage(label + " lower lamp", lampBase.transform, accent,
                Centered(Vector2.zero, new Vector2(65f, 6f)), RoundedSprite(3)).raycastTarget = false;

            // The root hit area is transparent and large enough for thumbs on small devices.
            var hit = root.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            var button = root.AddComponent<Button>();
            button.targetGraphic = hit;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            colors.pressedColor = new Color(0.84f, 0.88f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(action);
            return button;
        }

        private void AddGraphicOutline(Graphic graphic, Color color, float width)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
            outline.useGraphicAlpha = true;
        }

        private void BuildGameSectionPanel(Transform parent, GameObject bottomBar)
        {
            var tabPanel = CreateCard("Game section panel", parent, new Vector2(0, 115),
                new Vector2(1040, 1500), Hex("0A1020"), 0);
            gameTabPanel = tabPanel.AddComponent<CanvasGroup>();
            CreateImage("Tab cyan edge", tabPanel.transform, Cyan,
                Centered(new Vector2(-225, 700), new Vector2(430, 8)), RoundedSprite(4));
            CreateImage("Tab magenta edge", tabPanel.transform, NeonPink,
                Centered(new Vector2(225, 700), new Vector2(430, 8)), RoundedSprite(4));
            gameTabTitle = MakeText(tabPanel.transform, "MISSIONS", 52, FontStyle.Bold, Cream,
                new Vector2(0, 605), new Vector2(760, 80), TextAnchor.MiddleCenter, 4);
            gameTabSubtitle = MakeText(tabPanel.transform, "ACTIVE OBJECTIVES", 19, FontStyle.Bold, Cyan,
                new Vector2(0, 545), new Vector2(760, 40), TextAnchor.MiddleCenter, 4);

            gameGenericTabContent = new GameObject("Generic section content");
            gameGenericTabContent.transform.SetParent(tabPanel.transform, false);
            var contentRect = gameGenericTabContent.AddComponent<RectTransform>();
            Stretch(contentRect);
            for (var i = 0; i < 3; i++)
            {
                var y = 345 - i * 235;
                var item = CreateCard("Section item " + (i + 1), gameGenericTabContent.transform, new Vector2(0, y),
                    new Vector2(790, 190), i % 2 == 0 ? Hex("0D3555") : Hex("281B55"), 38);
                gameTabCardTitles[i] = MakeText(item.transform, "MISSION", 25, FontStyle.Bold, Cream,
                    new Vector2(-225, 42), new Vector2(300, 45), TextAnchor.MiddleLeft, 2);
                gameTabCardDetails[i] = MakeText(item.transform, "Objective details", 20, FontStyle.Normal, Muted,
                    new Vector2(-225, -20), new Vector2(520, 70), TextAnchor.MiddleLeft);
                gameTabCardTags[i] = MakeText(item.transform, "0 / 1", 20, FontStyle.Bold, Cyan,
                    new Vector2(270, 0), new Vector2(180, 52), TextAnchor.MiddleCenter, 2);
            }

            BuildMissionPanel(tabPanel.transform);
            BuildMeProfilePanel(tabPanel.transform);
            BuildShopPanel(tabPanel.transform);
            gameSectionCloseButton = MakeButton(tabPanel.transform, "×", new Vector2(0, -650),
                new Vector2(108, 108), Hex("B92E48"), Cream, 64, HideGameTab);
            gameSectionCloseButton.gameObject.AddComponent<ButtonGlow>();

            // The navigation remains visible and clickable above the section content.
            bottomBar.transform.SetAsLastSibling();
            gameTabPanel.alpha = 0f;
            gameTabPanel.blocksRaycasts = false;
            gameTabPanel.interactable = false;
            tabPanel.SetActive(false);
        }

        private void ShowGameTab(string tabName)
        {
            gameTabTitle.text = tabName;
            var showingMissions = tabName == "MISSIONS";
            var showingMe = tabName == "ME";
            var showingShop = tabName == "SHOP";
            var showingFullScreenSection = showingMissions || showingMe || showingShop;
            var sectionRect = gameTabPanel != null ? gameTabPanel.GetComponent<RectTransform>() : null;
            if (sectionRect != null)
            {
                SetRect(sectionRect, showingFullScreenSection ? Vector2.zero : new Vector2(0f, 115f),
                    showingFullScreenSection ? new Vector2(1080f, 1920f) : new Vector2(1040f, 1500f));
            }
            gameGenericTabContent.SetActive(!showingFullScreenSection && !showingMissions);
            if (gameMissionsContent != null)
                gameMissionsContent.SetActive(showingMissions);
            gameMeContent.SetActive(showingMe);
            if (gameShopContent != null)
                gameShopContent.SetActive(showingShop);
            if (gameBottomNavigation != null)
                gameBottomNavigation.SetActive(!showingFullScreenSection);
            if (gameSectionCloseButton != null)
                gameSectionCloseButton.gameObject.SetActive(showingMissions);
            if (topPlayerSummaryRoot != null)
                topPlayerSummaryRoot.SetActive(!showingMissions && auth != null && auth.CurrentUser != null);

            if (tabName == "MISSIONS")
            {
                gameTabTitle.text = string.Empty;
                gameTabSubtitle.text = string.Empty;
                ShowMissionSection(true);
                RefreshMissionUI();
                if (realtimeMissionStateReference == null)
                    _ = StartMissionRealtimeSyncAsync();
            }
            else if (tabName == "ME")
            {
                gameTabTitle.text = string.Empty;
                gameTabSubtitle.text = string.Empty;
                ShowMeSection(1);
                RefreshMeProfileUI();
            }
            else if (tabName == "SHOP")
            {
                gameTabTitle.text = string.Empty;
                gameTabSubtitle.text = string.Empty;
                // The bottom SHOP card keeps opening Boosts. Currency plus buttons call
                // OpenStoreForCurrency after this and switch to the Store catalog.
                ShowShopSubPage(1);
                RefreshShopUI();
                if (realtimeShopPlayerReference == null)
                    _ = StartShopRealtimeSyncAsync();
            }
            else
            {
                gameTabSubtitle.text = "COSMETIC LOADOUT";
                SetGameTabCard(0, "CYAN TRAIL", "Default breach trail equipped.", "OWNED", Cyan);
                SetGameTabCard(1, "MAGENTA TRAIL", "Unlock with gameplay rewards.", "LOCKED", NeonPink);
                SetGameTabCard(2, "ORANGE CORE", "A warm reactor glow for your runner.", "LOCKED", Coral);
            }

            gameTabPanel.gameObject.SetActive(true);
            gameTabPanel.alpha = 1f;
            gameTabPanel.blocksRaycasts = true;
            gameTabPanel.interactable = true;
        }

        private void SetGameTabCard(int index, string title, string details, string tag, Color accent)
        {
            gameTabCardTitles[index].text = title;
            gameTabCardDetails[index].text = details;
            gameTabCardTags[index].text = tag;
            gameTabCardTags[index].color = accent;
        }

        private void HideGameTab()
        {
            gameTabPanel.alpha = 0f;
            gameTabPanel.blocksRaycasts = false;
            gameTabPanel.interactable = false;
            gameTabPanel.gameObject.SetActive(false);
            if (gameBottomNavigation != null)
                gameBottomNavigation.SetActive(true);
            if (topPlayerSummaryRoot != null)
                topPlayerSummaryRoot.SetActive(auth != null && auth.CurrentUser != null);
        }

        private void OpenStoreForCurrency(StoreCurrencyKind currencyKind)
        {
            ShowGameTab("SHOP");
            selectedStoreCurrency = currencyKind;
            ShowShopSubPage(0);
            RefreshStoreCatalogUI();
        }

        private void OpenBoostsStore()
        {
            ShowGameTab("SHOP");
            ShowShopSubPage(1);
            RefreshShopUI();
        }
    }
}
