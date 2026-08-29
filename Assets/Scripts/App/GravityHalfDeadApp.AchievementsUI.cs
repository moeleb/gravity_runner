using System;
using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private ScrollRect achievementScrollRect;
        private RectTransform achievementScrollContent;
        private readonly Image[] achievementProgressFills = new Image[17];
        private readonly Text[] achievementProgressTexts = new Text[17];
        private readonly Text[] achievementBadgeStatusTexts = new Text[17];
        private readonly Text[] achievementTitleTexts = new Text[17];
        private readonly Text[] achievementDescriptionTexts = new Text[17];
        private readonly Image[,] achievementTierCards = new Image[17, 4];
        private readonly Text[,] achievementTierTexts = new Text[17, 4];
        private Button playerProfileEditorBadgeTab;
        private GameObject playerProfileEditorBadgeContent;
        private RectTransform profileBadgeGrid;
        private Text profileBadgeEmptyText;
        private Text profileBadgeCountText;
        private readonly GameObject[] profileBadgeCards = new GameObject[17];

        private void BuildAchievementPanel(Transform parent)
        {
            var title = MakeText(parent, "ACHIEVEMENTS", 59, FontStyle.Bold, Color.white,
                new Vector2(0f, 574f), new Vector2(800f, 76f), TextAnchor.MiddleCenter, 4);
            AddGraphicOutline(title, Hex("58239B"), 3f);
            var titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(NeonPurple.r, NeonPurple.g, NeonPurple.b, 0.62f);
            titleShadow.effectDistance = new Vector2(0f, -4f);
            MakeText(parent, "COMPLETE EVERY TIER TO COLLECT ITS BADGE", 19,
                FontStyle.Bold, Hex("C9B5F5"), new Vector2(0f, 520f),
                new Vector2(860f, 32f), TextAnchor.MiddleCenter, 2);

            var scrollObject = new GameObject("Achievement single-page scroll view");
            scrollObject.transform.SetParent(parent, false);
            var scrollRectTransform = scrollObject.AddComponent<RectTransform>();
            SetRect(scrollRectTransform, new Vector2(0f, -120f), new Vector2(1030f, 1050f));

            var viewport = CreateCard("Achievement clipped viewport", scrollObject.transform,
                Vector2.zero, new Vector2(1000f, 1036f), new Color(0.01f, 0.02f, 0.06f, 0.86f), 24);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewport.AddComponent<Mask>().showMaskGraphic = true;
            AddGraphicOutline(viewport.GetComponent<Image>(),
                new Color(NeonPurple.r, NeonPurple.g, NeonPurple.b, 0.42f), 1.2f);

            var content = new GameObject("All achievements #1 through #17");
            content.transform.SetParent(viewport.transform, false);
            achievementScrollContent = content.AddComponent<RectTransform>();
            achievementScrollContent.anchorMin = new Vector2(0.5f, 1f);
            achievementScrollContent.anchorMax = new Vector2(0.5f, 1f);
            achievementScrollContent.pivot = new Vector2(0.5f, 1f);
            achievementScrollContent.sizeDelta = new Vector2(968f, 17f * 198f + 30f);
            achievementScrollContent.anchoredPosition = new Vector2(0f, -12f);

            for (var i = 0; i < AchievementCatalog.Length; i++)
                BuildAchievementRow(achievementScrollContent, i, -100f - i * 198f);

            achievementScrollRect = scrollObject.AddComponent<ScrollRect>();
            achievementScrollRect.viewport = viewportRect;
            achievementScrollRect.content = achievementScrollContent;
            achievementScrollRect.horizontal = false;
            achievementScrollRect.vertical = true;
            achievementScrollRect.movementType = ScrollRect.MovementType.Clamped;
            achievementScrollRect.inertia = true;
            achievementScrollRect.decelerationRate = 0.12f;
            achievementScrollRect.scrollSensitivity = 58f;

            var scrollbarTrack = CreateCard("Achievement scrollbar track", scrollObject.transform,
                new Vector2(500f, 0f), new Vector2(8f, 968f), Hex("13162A"), 4);
            scrollbarTrack.GetComponent<Image>().raycastTarget = false;
            var scrollbar = scrollbarTrack.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = CreateCard("Achievement scrollbar handle", scrollbarTrack.transform,
                Vector2.zero, new Vector2(8f, 220f), Hex("8A45FF"), 4);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            achievementScrollRect.verticalScrollbar = scrollbar;
            achievementScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            achievementScrollRect.verticalScrollbarSpacing = 4f;

            RefreshAchievementUI();
        }

        private void BuildAchievementRow(Transform parent, int index, float y)
        {
            var definition = AchievementCatalog[index];
            var accent = definition.Accent;
            var glow = CreateCard("Achievement " + definition.Number + " glow", parent,
                new Vector2(0f, y), new Vector2(954f, 184f),
                new Color(accent.r, accent.g, accent.b, 0.28f), 24);
            var glowRect = glow.GetComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.5f, 1f);
            glowRect.anchorMax = new Vector2(0.5f, 1f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.anchoredPosition = new Vector2(0f, y);
            glow.GetComponent<Image>().raycastTarget = false;
            var row = CreateCard("Achievement " + definition.Number + " · " + definition.Title,
                glow.transform, Vector2.zero, new Vector2(946f, 176f), Hex("060A18"), 21);
            AddGraphicOutline(row.GetComponent<Image>(),
                new Color(accent.r, accent.g, accent.b, 0.76f), 1.4f);

            var iconHost = new GameObject("Achievement badge preview");
            iconHost.transform.SetParent(row.transform, false);
            var iconHostRect = iconHost.AddComponent<RectTransform>();
            SetRect(iconHostRect, new Vector2(-399f, 0f), new Vector2(128f, 128f));
            BuildAchievementBadgeVisual(iconHost.transform, index, 118f, true);

            var heading = MakeText(row.transform,
                definition.Number + ".  " + definition.Title, 25, FontStyle.Bold, Color.white,
                new Vector2(-155f, 43f), new Vector2(330f, 42f), TextAnchor.MiddleLeft, 2);
            heading.resizeTextMinSize = 18;
            heading.resizeTextMaxSize = 25;
            heading.horizontalOverflow = HorizontalWrapMode.Wrap;
            heading.verticalOverflow = VerticalWrapMode.Truncate;
            achievementTitleTexts[index] = heading;
            var description = MakeText(row.transform, definition.Description, 18, FontStyle.Normal,
                Hex("C2CBE0"), new Vector2(-155f, 4f), new Vector2(330f, 48f),
                TextAnchor.MiddleLeft, 1);
            description.resizeTextMinSize = 14;
            description.resizeTextMaxSize = 18;
            description.verticalOverflow = VerticalWrapMode.Truncate;
            achievementDescriptionTexts[index] = description;

            // Build the maximum four cells once so a remote catalog refresh can safely
            // change labels/thresholds without rebuilding the row. Time Lord simply hides IV.
            const int tierCount = 4;
            const float tierWidth = 82f;
            const float gap = 7f;
            var tierSpan = tierCount * tierWidth + (tierCount - 1) * gap;
            var tierCenterX = 190f;
            var tierStartX = tierCenterX - tierSpan * 0.5f + tierWidth * 0.5f;
            for (var tier = 0; tier < tierCount; tier++)
            {
                var tierExists = tier < definition.Thresholds.Length;
                var tierCard = CreateCard("Tier " + (tier + 1), row.transform,
                    new Vector2(tierStartX + tier * (tierWidth + gap), 44f),
                    new Vector2(tierWidth, 66f), Hex("0A1225"), 10);
                achievementTierCards[index, tier] = tierCard.GetComponent<Image>();
                AddGraphicOutline(achievementTierCards[index, tier], Hex("39415A"), 1f);
                achievementTierTexts[index, tier] = MakeText(tierCard.transform,
                    tierExists ? RomanTier(tier + 1) + "\n" + definition.TierLabels[tier] : string.Empty, 15,
                    FontStyle.Bold, Hex("AEBAD3"), Vector2.zero, new Vector2(76f, 58f),
                    TextAnchor.MiddleCenter, 0);
                achievementTierTexts[index, tier].resizeTextMinSize = 11;
                achievementTierTexts[index, tier].resizeTextMaxSize = 15;
                tierCard.SetActive(tierExists);
            }

            var track = CreateCard("Achievement progress track", row.transform,
                new Vector2(130f, -45f), new Vector2(360f, 42f), Hex("080D1A"), 12);
            AddGraphicOutline(track.GetComponent<Image>(), Hex("35405A"), 1f);
            var fill = CreateImage("Achievement progress fill", track.transform, accent,
                FullStretch(), RoundedSprite(10));
            fill.rectTransform.offsetMin = new Vector2(4f, 4f);
            fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            achievementProgressFills[index] = fill;
            achievementProgressTexts[index] = MakeText(track.transform, "0 / 1", 18,
                FontStyle.Bold, Color.white, Vector2.zero, new Vector2(335f, 34f),
                TextAnchor.MiddleCenter, 1);
            AddGraphicOutline(achievementProgressTexts[index], Hex("02030A"), 1f);

            achievementBadgeStatusTexts[index] = MakeText(row.transform, "BADGE LOCKED", 14,
                FontStyle.Bold, Hex("78849C"), new Vector2(412f, -47f),
                new Vector2(112f, 38f), TextAnchor.MiddleCenter, 1);
        }

        private void RefreshAchievementUI()
        {
            for (var i = 0; i < AchievementCatalog.Length; i++)
            {
                if (achievementProgressFills[i] == null)
                    continue;
                var definition = AchievementCatalog[i];
                var state = achievementStates[i];
                var finalTarget = Math.Max(1L, definition.Thresholds[^1]);
                var progress = Math.Max(0L, state.Progress);
                achievementProgressFills[i].fillAmount = Mathf.Clamp01((float)progress / finalTarget);
                achievementProgressFills[i].color = state.Complete ? Green : definition.Accent;
                achievementProgressTexts[i].text = AchievementProgressLabel(definition, progress);

                for (var tier = 0; tier < definition.Thresholds.Length; tier++)
                {
                    if (achievementTierCards[i, tier] == null)
                        continue;
                    achievementTierCards[i, tier].gameObject.SetActive(true);
                    var complete = tier < state.CompletedTiers;
                    achievementTierCards[i, tier].color = complete
                        ? Color.Lerp(Hex("07101E"), definition.Accent, 0.36f)
                        : Hex("0A1225");
                    achievementTierTexts[i, tier].color = complete ? Color.white : Hex("8490A7");
                }
                for (var tier = definition.Thresholds.Length; tier < 4; tier++)
                {
                    if (achievementTierCards[i, tier] != null)
                        achievementTierCards[i, tier].gameObject.SetActive(false);
                }

                if (state.BadgeCollected)
                {
                    achievementBadgeStatusTexts[i].text = "✓ COLLECTED";
                    achievementBadgeStatusTexts[i].color = Green;
                }
                else if (state.BadgeUnlocked)
                {
                    achievementBadgeStatusTexts[i].text = "BADGE READY";
                    achievementBadgeStatusTexts[i].color = definition.Accent;
                }
                else
                {
                    achievementBadgeStatusTexts[i].text = "BADGE LOCKED";
                    achievementBadgeStatusTexts[i].color = Hex("78849C");
                }
            }
        }

        private static string AchievementProgressLabel(AchievementDefinition definition, long progress)
        {
            var target = Math.Max(1L, definition.Thresholds[^1]);
            var clamped = Math.Min(target, Math.Max(0L, progress));
            if (definition.Number is 5 or 7)
                return FormatAchievementDistance(clamped) + " / " + FormatAchievementDistance(target);
            if (definition.Number == 6)
                return FormatAchievementTime(clamped) + " / " + FormatAchievementTime(target);
            if (definition.Number is >= 9 and <= 14)
                return "Lv." + clamped + " / Max Lv." + target;
            if (definition.Number == 15)
                return clamped + " / " + target + " power-ups maxed";
            return clamped.ToString("N0") + " / " + target.ToString("N0");
        }

        private static string FormatAchievementDistance(long meters)
            => meters >= 1000L ? (meters / 1000f).ToString("0.#") + " km" : meters + " m";

        private static string FormatAchievementTime(long seconds)
        {
            var minutes = seconds / 60L;
            var remainder = seconds % 60L;
            return minutes.ToString("00") + ":" + remainder.ToString("00");
        }

        private static string RomanTier(int tier)
            => tier switch { 1 => "I", 2 => "II", 3 => "III", _ => "IV" };

        private void RefreshAchievementCatalogText()
        {
            for (var i = 0; i < AchievementCatalog.Length; i++)
            {
                var definition = AchievementCatalog[i];
                if (achievementTitleTexts[i] != null)
                    achievementTitleTexts[i].text = definition.Number + ".  " + definition.Title;
                if (achievementDescriptionTexts[i] != null)
                    achievementDescriptionTexts[i].text = definition.Description;
                for (var tier = 0; tier < 4; tier++)
                {
                    if (achievementTierCards[i, tier] == null)
                        continue;
                    var exists = tier < definition.Thresholds.Length;
                    achievementTierCards[i, tier].gameObject.SetActive(exists);
                    if (exists && achievementTierTexts[i, tier] != null)
                        achievementTierTexts[i, tier].text = RomanTier(tier + 1) + "\n" +
                                                             definition.TierLabels[tier];
                }
            }
        }

        private void ResetAchievementScroll()
        {
            if (achievementScrollRect != null)
                achievementScrollRect.verticalNormalizedPosition = 1f;
            if (achievementScrollContent != null)
                achievementScrollContent.anchoredPosition = new Vector2(0f, -12f);
        }

        private void BuildProfileBadgeCollection(Transform parent)
        {
            profileBadgeCountText = MakeText(parent, "0 / 17 BADGES COLLECTED", 18,
                FontStyle.Bold, Hex("B9A2F7"), new Vector2(0f, 200f),
                new Vector2(700f, 34f), TextAnchor.MiddleCenter, 1);

            var viewport = CreateCard("Profile badge clipped viewport", parent, new Vector2(0f, -15f),
                new Vector2(814f, 386f), new Color(0.01f, 0.02f, 0.05f, 0.72f), 22);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewport.AddComponent<Mask>().showMaskGraphic = true;

            var gridObject = new GameObject("Collected badges grid");
            gridObject.transform.SetParent(viewport.transform, false);
            profileBadgeGrid = gridObject.AddComponent<RectTransform>();
            profileBadgeGrid.anchorMin = new Vector2(0.5f, 1f);
            profileBadgeGrid.anchorMax = new Vector2(0.5f, 1f);
            profileBadgeGrid.pivot = new Vector2(0.5f, 1f);
            profileBadgeGrid.sizeDelta = new Vector2(790f, 386f);
            profileBadgeGrid.anchoredPosition = Vector2.zero;

            for (var i = 0; i < AchievementCatalog.Length; i++)
            {
                var card = new GameObject("Collected badge card " + (i + 1));
                card.transform.SetParent(profileBadgeGrid, false);
                var cardRect = card.AddComponent<RectTransform>();
                SetRect(cardRect, Vector2.zero, new Vector2(146f, 174f));
                cardRect.anchorMin = new Vector2(0.5f, 1f);
                cardRect.anchorMax = new Vector2(0.5f, 1f);
                var badgeHost = new GameObject("Badge art");
                badgeHost.transform.SetParent(card.transform, false);
                var badgeRect = badgeHost.AddComponent<RectTransform>();
                SetRect(badgeRect, new Vector2(0f, 20f), new Vector2(116f, 116f));
                BuildAchievementBadgeVisual(badgeHost.transform, i, 108f, false);
                var label = MakeText(card.transform, AchievementCatalog[i].Title, 13,
                    FontStyle.Bold, Color.white, new Vector2(0f, -61f),
                    new Vector2(140f, 39f), TextAnchor.MiddleCenter, 0);
                label.resizeTextMinSize = 10;
                label.resizeTextMaxSize = 13;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                profileBadgeCards[i] = card;
            }

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = profileBadgeGrid;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.14f;
            scroll.scrollSensitivity = 45f;

            profileBadgeEmptyText = MakeText(viewport.transform,
                "NO BADGES COLLECTED YET\nCOMPLETE EVERY TIER OF AN ACHIEVEMENT",
                20, FontStyle.Bold, Hex("77859D"), Vector2.zero,
                new Vector2(660f, 90f), TextAnchor.MiddleCenter, 2);
            RefreshProfileBadgeCollectionUI();
        }

        private void RefreshProfileBadgeCollectionUI()
        {
            if (profileBadgeGrid == null)
                return;
            var visible = 0;
            const int columns = 5;
            const float cardWidth = 146f;
            const float cardHeight = 174f;
            const float horizontalGap = 11f;
            const float verticalGap = 10f;
            for (var i = 0; i < AchievementCatalog.Length; i++)
            {
                var collected = achievementStates[i].BadgeCollected;
                profileBadgeCards[i].SetActive(collected);
                if (!collected)
                    continue;
                var column = visible % columns;
                var row = visible / columns;
                var x = -(columns - 1) * (cardWidth + horizontalGap) * 0.5f
                        + column * (cardWidth + horizontalGap);
                var y = -cardHeight * 0.5f - row * (cardHeight + verticalGap);
                profileBadgeCards[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
                visible++;
            }

            var rows = Mathf.Max(1, Mathf.CeilToInt(visible / (float)columns));
            profileBadgeGrid.sizeDelta = new Vector2(790f,
                Mathf.Max(386f, rows * cardHeight + Mathf.Max(0, rows - 1) * verticalGap + 12f));
            if (profileBadgeEmptyText != null)
                profileBadgeEmptyText.gameObject.SetActive(visible == 0);
            if (profileBadgeCountText != null)
                profileBadgeCountText.text = visible + " / 17 BADGES COLLECTED";
        }
    }
}
