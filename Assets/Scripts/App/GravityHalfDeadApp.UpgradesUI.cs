using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private Button meCharactersNavigationButton;
        private Button meUpgradesNavigationButton;
        private Button meDiscsNavigationButton;
        private GameObject meUpgradeNavigationRoot;
        private Button meUpgradeCloseButton;

        private void BuildMeUpgradesPanel(Transform parent)
        {
            mePowerupsContent = new GameObject("ME · Full Screen Upgrades");
            mePowerupsContent.transform.SetParent(parent, false);
            var contentRect = mePowerupsContent.AddComponent<RectTransform>();
            Stretch(contentRect);

            var background = CreateImage("Upgrades deep void", mePowerupsContent.transform,
                Hex("010611"), FullStretch());
            background.raycastTarget = false;
            BuildUpgradesCircuitBackdrop(mePowerupsContent.transform);
            BuildUpgradesFixedHeader(mePowerupsContent.transform);
            BuildUpgradesScrollArea(mePowerupsContent.transform);

            mePowerupsContent.SetActive(false);
        }

        private void BuildUpgradesFixedHeader(Transform parent)
        {
            var header = new GameObject("Fixed upgrades header");
            header.transform.SetParent(parent, false);
            var headerRect = header.AddComponent<RectTransform>();
            SetRect(headerRect, new Vector2(0f, 655f), new Vector2(1080f, 250f));

            var wash = CreateImage("Header navy wash", header.transform,
                new Color(0.005f, 0.025f, 0.07f, 0.98f), FullStretch());
            wash.raycastTarget = false;
            CreateImage("Header cyan rail", header.transform, Cyan,
                Centered(new Vector2(-250f, -122f), new Vector2(500f, 5f)), RoundedSprite(3)).raycastTarget = false;
            CreateImage("Header blue rail", header.transform, Hex("075FB9"),
                Centered(new Vector2(250f, -122f), new Vector2(500f, 5f)), RoundedSprite(3)).raycastTarget = false;

            BuildUpgradeTitleWing(header.transform, -410f, false);
            BuildUpgradeTitleWing(header.transform, 410f, true);

            var title = MakeText(header.transform, "UPGRADES", 78, FontStyle.Bold, Color.white,
                new Vector2(0f, 78f), new Vector2(650f, 84f), TextAnchor.MiddleCenter, 4);
            AddGraphicOutline(title, Hex("102D63"), 4f);
            var titleGlow = title.gameObject.AddComponent<Shadow>();
            titleGlow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.70f);
            titleGlow.effectDistance = new Vector2(0f, -6f);

            MakeText(header.transform, "UPGRADE YOUR ABILITIES TO SURVIVE LONGER", 27,
                FontStyle.Bold, Hex("A8C6F4"), new Vector2(0f, 12f),
                new Vector2(880f, 38f), TextAnchor.MiddleCenter, 2);

            var walletGlow = CreateCard("Wallet cyan glow", header.transform, new Vector2(0f, -68f),
                new Vector2(422f, 82f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.30f), 26);
            walletGlow.GetComponent<Image>().raycastTarget = false;
            var wallet = CreateCard("Wallet face", walletGlow.transform, Vector2.zero,
                new Vector2(408f, 70f), Hex("061225"), 21);
            wallet.GetComponent<Image>().raycastTarget = false;
            AddGraphicOutline(wallet.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.90f), 2f);
            BuildCanonicalGameCoin(wallet.transform, new Vector2(-128f, 0f), 50f);
            mePowerupWalletText = MakeText(wallet.transform, "0", 39, FontStyle.Bold, Color.white,
                new Vector2(42f, 0f), new Vector2(270f, 58f), TextAnchor.MiddleCenter, 1);
        }

        private void BuildUpgradesScrollArea(Transform parent)
        {
            var scrollObject = new GameObject("Scrollable upgrade cards");
            scrollObject.transform.SetParent(parent, false);
            var scrollRectTransform = scrollObject.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(16f, 250f);
            scrollRectTransform.offsetMax = new Vector2(-16f, -430f);
            var scrollHit = scrollObject.AddComponent<Image>();
            scrollHit.color = new Color(1f, 1f, 1f, 0.001f);

            var viewportObject = new GameObject("Upgrade card viewport");
            viewportObject.transform.SetParent(scrollObject.transform, false);
            var viewportRect = viewportObject.AddComponent<RectTransform>();
            Stretch(viewportRect);
            viewportObject.AddComponent<RectMask2D>();

            const float cardHeight = 195f;
            const float cardGap = 12f;
            const float padding = 14f;
            var contentHeight = padding * 2f + PowerupIds.Length * cardHeight +
                                (PowerupIds.Length - 1) * cardGap;

            var contentObject = new GameObject("Upgrade card content");
            contentObject.transform.SetParent(viewportObject.transform, false);
            var cardsRect = contentObject.AddComponent<RectTransform>();
            cardsRect.anchorMin = new Vector2(0f, 1f);
            cardsRect.anchorMax = new Vector2(1f, 1f);
            cardsRect.pivot = new Vector2(0.5f, 1f);
            cardsRect.sizeDelta = new Vector2(0f, contentHeight);
            cardsRect.anchoredPosition = Vector2.zero;

            for (var i = 0; i < PowerupIds.Length; i++)
            {
                var y = -padding - cardHeight * 0.5f - i * (cardHeight + cardGap);
                BuildPowerupUpgradeCard(contentObject.transform, i, new Vector2(0f, y),
                    new Vector2(1016f, cardHeight));
            }

            var scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = cardsRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.10f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.14f;
            scroll.scrollSensitivity = 52f;
            scroll.verticalNormalizedPosition = 1f;
        }

        private void BuildUpgradesCircuitBackdrop(Transform parent)
        {
            for (var i = 0; i < 12; i++)
            {
                var x = -510f + i * 92f;
                var line = CreateImage("Circuit vertical " + i, parent,
                    new Color(0.02f, 0.34f, 0.65f, 0.11f),
                    Centered(new Vector2(x, 0f), new Vector2(2f, 1880f)), RoundedSprite(1));
                line.raycastTarget = false;
                var node = CreateImage("Circuit node " + i, parent,
                    new Color(Cyan.r, Cyan.g, Cyan.b, 0.18f),
                    Centered(new Vector2(x, 690f - i * 113f), new Vector2(10f, 10f)), RoundedSprite(5));
                node.raycastTarget = false;
            }

            for (var i = 0; i < 10; i++)
            {
                var horizontal = CreateImage("Circuit horizontal " + i, parent,
                    new Color(0.10f, 0.16f, 0.48f, 0.10f),
                    Centered(new Vector2(i % 2 == 0 ? -310f : 310f, 760f - i * 166f),
                        new Vector2(390f, 2f)), RoundedSprite(1));
                horizontal.raycastTarget = false;
            }
        }

        private void BuildUpgradeTitleWing(Transform parent, float x, bool mirror)
        {
            var rail = CreateImage("Title wing rail", parent, Cyan,
                Centered(new Vector2(x, 77f), new Vector2(205f, 7f)), RoundedSprite(4));
            rail.raycastTarget = false;
            for (var i = 0; i < 3; i++)
            {
                var slash = CreateImage("Title wing slash " + i, parent, Hex("138EEB"),
                    Centered(new Vector2(x + (mirror ? 66f : -66f) + (mirror ? -1f : 1f) * i * 25f,
                            63f - i * 7f), new Vector2(65f, 8f)), RoundedSprite(4));
                slash.rectTransform.localEulerAngles = new Vector3(0f, 0f, mirror ? 42f : -42f);
                slash.raycastTarget = false;
            }
        }

        private void BuildPowerupUpgradeCard(Transform parent, int index, Vector2 position, Vector2 size)
        {
            var accent = PowerupAccentColor(index);
            var surface = Color.Lerp(Hex("020814"), accent, 0.085f);
            surface.a = 0.995f;

            var glow = CreateCard(PowerupNames[index] + " outer glow", parent, position,
                size, new Color(accent.r, accent.g, accent.b, 0.34f), 27);
            var glowRect = glow.GetComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.5f, 1f);
            glowRect.anchorMax = new Vector2(0.5f, 1f);
            glowRect.anchoredPosition = position;
            glow.GetComponent<Image>().raycastTarget = false;
            var card = CreateCard(PowerupNames[index] + " upgrade card", glow.transform, Vector2.zero,
                size - new Vector2(7f, 7f), surface, 25);
            var cardImage = card.GetComponent<Image>();
            cardImage.raycastTarget = false;
            AddGraphicOutline(cardImage, new Color(accent.r, accent.g, accent.b, 0.96f), 2.3f);
            var cardGlow = card.AddComponent<Shadow>();
            cardGlow.effectColor = new Color(accent.r, accent.g, accent.b, 0.34f);
            cardGlow.effectDistance = new Vector2(0f, -5f);

            var edgeY = size.y * 0.5f - 4f;
            CreateImage("Top neon rail", card.transform, accent,
                Centered(new Vector2(0f, edgeY), new Vector2(916f, 3f)), RoundedSprite(2)).raycastTarget = false;
            CreateImage("Bottom neon rail", card.transform, new Color(accent.r, accent.g, accent.b, 0.56f),
                Centered(new Vector2(0f, -edgeY), new Vector2(916f, 2f)), RoundedSprite(1)).raycastTarget = false;

            var iconGlow = CreateCard("Powerup icon glow", card.transform, new Vector2(-398f, 0f),
                new Vector2(166f, 166f), new Color(accent.r, accent.g, accent.b, 0.30f), 21);
            iconGlow.GetComponent<Image>().raycastTarget = false;
            var iconFrame = CreateCard("Powerup icon frame", iconGlow.transform, Vector2.zero,
                new Vector2(156f, 156f), Hex("030A16"), 19);
            var iconFrameImage = iconFrame.GetComponent<Image>();
            iconFrameImage.raycastTarget = false;
            AddGraphicOutline(iconFrameImage, accent, 2.5f);
            BuildPowerupIconCorners(iconFrame.transform, accent);

            var iconTexture = Resources.Load<Texture2D>("UI/Powerups/" + PowerupIds[index]);
            if (iconTexture != null)
            {
                var icon = MakeTextureImage(PowerupNames[index] + " actual asset", iconFrame.transform,
                    iconTexture, Vector2.zero, new Vector2(144f, 144f));
                var aspect = icon.GetComponent<AspectRatioFitter>();
                if (aspect != null)
                    aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            }
            else if (PowerupIds[index] == "timezone")
            {
                BuildTimezoneClockIcon(iconFrame.transform, accent);
            }
            else
            {
                Debug.LogError("Missing upgrade icon: Resources/UI/Powerups/" + PowerupIds[index] + ".png");
            }

            MakeText(card.transform, PowerupNames[index], 35, FontStyle.Bold, Color.white,
                new Vector2(-120f, 48f), new Vector2(330f, 48f), TextAnchor.MiddleLeft, 1);
            var description = MakeText(card.transform, PowerupDescriptions[index], 20, FontStyle.Normal,
                Hex("BCD1EF"), new Vector2(-120f, 7f), new Vector2(330f, 46f), TextAnchor.MiddleLeft);
            description.resizeTextMinSize = 16;
            description.resizeTextMaxSize = 20;

            mePowerupDurationTexts[index] = MakeText(card.transform, "BASE EFFECT", 17, FontStyle.Bold,
                Hex("C2D2EF"), new Vector2(145f, 39f), new Vector2(205f, 30f), TextAnchor.MiddleCenter, 1);

            var segmentCount = PowerupMaxLevel(index);
            const float segmentGroupCenterX = -95f;
            const float segmentStride = 56f;
            var segmentStartX = segmentGroupCenterX - (segmentCount - 1) * segmentStride * 0.5f;
            for (var segment = 0; segment < segmentCount; segment++)
            {
                // Use a card instead of CreateImage here. CreateImage also applies stretch offsets,
                // which can collapse a centered RectTransform and make the level bars invisible.
                var segmentCard = CreateCard("Level segment " + (segment + 1), card.transform,
                    new Vector2(segmentStartX + segment * segmentStride, -62f),
                    new Vector2(46f, 20f), Hex("343A49"), 8);
                var segmentImage = segmentCard.GetComponent<Image>();
                segmentImage.raycastTarget = false;
                AddGraphicOutline(segmentImage, Hex("8291A8"), 2f);
                mePowerupSegments[index, segment] = segmentImage;
            }

            BuildPowerupPurchasePanel(card.transform, index, accent);
        }

        private void BuildPowerupPurchasePanel(Transform parent, int index, Color accent)
        {
            var panelGlow = CreateCard("Purchase panel glow", parent, new Vector2(378f, 0f),
                new Vector2(252f, 172f), new Color(accent.r, accent.g, accent.b, 0.33f), 22);
            panelGlow.GetComponent<Image>().raycastTarget = false;
            var panel = CreateCard("Purchase panel", panelGlow.transform, Vector2.zero,
                new Vector2(238f, 160f), Color.Lerp(Hex("050814"), accent, 0.16f), 19);
            var panelImage = panel.GetComponent<Image>();
            panelImage.raycastTarget = false;
            AddGraphicOutline(panelImage, accent, 2.2f);

            BuildCanonicalGameCoin(panel.transform, new Vector2(-69f, 35f), 44f);
            mePowerupCostTexts[index] = MakeText(panel.transform, "500", 34, FontStyle.Bold, Color.white,
                new Vector2(35f, 35f), new Vector2(154f, 50f), TextAnchor.MiddleCenter, 1);

            var capturedIndex = index;
            mePowerupButtons[index] = MakeButton(panel.transform, string.Empty,
                new Vector2(0f, -38f), new Vector2(210f, 62f),
                Color.Lerp(Hex("06101E"), accent, 0.20f), accent, 29,
                () => UpgradePowerup(PowerupIds[capturedIndex]));
            var buttonImage = mePowerupButtons[index].GetComponent<Image>();
            AddGraphicOutline(buttonImage, accent, 2.1f);
            mePowerupButtonLabelTexts[index] = MakeText(mePowerupButtons[index].transform, "UPGRADE", 29,
                FontStyle.Bold, accent, Vector2.zero, new Vector2(198f, 56f),
                TextAnchor.MiddleCenter, 1);
        }

        private void BuildPowerupIconCorners(Transform parent, Color accent)
        {
            var positions = new[]
            {
                new Vector2(-69f, 69f), new Vector2(69f, 69f),
                new Vector2(-69f, -69f), new Vector2(69f, -69f)
            };
            for (var i = 0; i < positions.Length; i++)
            {
                var rail = CreateImage("Angular icon corner " + i, parent, accent,
                    Centered(positions[i], new Vector2(34f, 5f)), RoundedSprite(3));
                rail.rectTransform.localEulerAngles = new Vector3(0f, 0f, i % 2 == 0 ? -45f : 45f);
                rail.raycastTarget = false;
            }
        }

        private void BuildTimezoneClockIcon(Transform parent, Color accent)
        {
            var aura = CreateImage("Timezone energy aura", parent,
                new Color(accent.r, accent.g, accent.b, 0.30f),
                Centered(Vector2.zero, new Vector2(180f, 180f)), RoundedSprite(90));
            aura.raycastTarget = false;
            var face = CreateImage("Timezone clock face", parent, Hex("F5A700"),
                Centered(Vector2.zero, new Vector2(148f, 148f)), RoundedSprite(74));
            face.raycastTarget = false;
            AddGraphicOutline(face, Hex("FFF09A"), 3f);
            var inner = CreateImage("Timezone clock inner", face.transform, Hex("241307"),
                Centered(Vector2.zero, new Vector2(116f, 116f)), RoundedSprite(58));
            inner.raycastTarget = false;

            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI * 2f / 12f;
                var position = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 47f;
                var tick = CreateImage("Clock tick " + i, inner.transform, Hex("FFD63D"),
                    Centered(position, new Vector2(5f, 14f)), RoundedSprite(3));
                tick.rectTransform.localEulerAngles = new Vector3(0f, 0f, -i * 30f);
                tick.raycastTarget = false;
            }

            var minuteHand = CreateImage("Clock minute hand", inner.transform, Hex("FFF0A0"),
                Centered(new Vector2(0f, 18f), new Vector2(7f, 42f)), RoundedSprite(4));
            minuteHand.raycastTarget = false;
            var hourHand = CreateImage("Clock hour hand", inner.transform, Hex("FFD02B"),
                Centered(new Vector2(12f, -5f), new Vector2(34f, 8f)), RoundedSprite(4));
            hourHand.rectTransform.localEulerAngles = new Vector3(0f, 0f, 28f);
            hourHand.raycastTarget = false;
            CreateImage("Clock center", inner.transform, Color.white,
                Centered(Vector2.zero, new Vector2(15f, 15f)), RoundedSprite(8)).raycastTarget = false;
        }

        private void BuildMeUpgradeNavigation(Transform parent)
        {
            meUpgradeNavigationRoot = new GameObject("Fixed ME navigation");
            meUpgradeNavigationRoot.transform.SetParent(parent, false);
            var navRect = meUpgradeNavigationRoot.AddComponent<RectTransform>();
            navRect.anchorMin = new Vector2(0f, 0f);
            navRect.anchorMax = new Vector2(1f, 0f);
            navRect.pivot = new Vector2(0.5f, 0f);
            navRect.offsetMin = Vector2.zero;
            navRect.offsetMax = new Vector2(0f, 168f);

            var navBackground = CreateImage("ME navigation background", meUpgradeNavigationRoot.transform,
                new Color(0.005f, 0.02f, 0.055f, 0.99f), FullStretch());
            navBackground.raycastTarget = true;
            AddGraphicOutline(navBackground, new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f), 2f);
            CreateImage("ME nav cyan rail", meUpgradeNavigationRoot.transform, Cyan,
                Centered(new Vector2(-260f, 80f), new Vector2(520f, 4f)), RoundedSprite(2)).raycastTarget = false;
            CreateImage("ME nav purple rail", meUpgradeNavigationRoot.transform, NeonPurple,
                Centered(new Vector2(260f, 80f), new Vector2(520f, 4f)), RoundedSprite(2)).raycastTarget = false;

            meCharactersNavigationButton = BuildMeNavigationButton(meUpgradeNavigationRoot.transform,
                "◆  CHARACTERS", new Vector2(-344f, -18f), () => ShowMeSection(0));
            meUpgradesNavigationButton = BuildMeNavigationButton(meUpgradeNavigationRoot.transform,
                "⇈  UPGRADES", new Vector2(0f, -18f), () => ShowMeSection(1));
            meDiscsNavigationButton = BuildMeNavigationButton(meUpgradeNavigationRoot.transform,
                "◎  DISCS", new Vector2(344f, -18f), () => ShowMeSection(2));

            meUpgradeCloseButton = MakeButton(parent, "×", Vector2.zero,
                new Vector2(106f, 106f), Hex("C62C35"), Color.white, 68, HideGameTab);
            var closeRect = meUpgradeCloseButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(0f, 170f);
            closeRect.sizeDelta = new Vector2(106f, 106f);
            AddGraphicOutline(meUpgradeCloseButton.GetComponent<Image>(), Hex("FF9B8E"), 2.4f);
            meUpgradeCloseButton.gameObject.AddComponent<ButtonGlow>();

            RefreshMeUpgradeNavigation(0);
        }

        private Button BuildMeNavigationButton(Transform parent, string label, Vector2 position,
            UnityEngine.Events.UnityAction action)
        {
            var button = MakeButton(parent, label, position, new Vector2(330f, 108f),
                Hex("061126"), Hex("B8C8E6"), 25, action);
            AddGraphicOutline(button.GetComponent<Image>(), new Color(0.13f, 0.39f, 0.72f, 0.92f), 2f);
            return button;
        }

        private void RefreshMeUpgradeNavigation(int activeSection)
        {
            SetMeNavigationButtonState(meCharactersNavigationButton, activeSection == 0);
            SetMeNavigationButtonState(meUpgradesNavigationButton, activeSection == 1);
            SetMeNavigationButtonState(meDiscsNavigationButton, activeSection == 2);
        }

        private void SetMeNavigationButtonState(Button button, bool active)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = active ? Hex("103D70") : Hex("061126");

            var text = button.GetComponentInChildren<Text>();
            if (text != null)
                text.color = active ? Cyan : Hex("B8C8E6");
        }
    }
}
