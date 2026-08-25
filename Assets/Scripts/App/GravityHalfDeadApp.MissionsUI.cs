using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private GameObject gameMissionsContent;
        private GameObject missionListContent;
        private GameObject missionAchievementsContent;
        private Button missionTabButton;
        private Button achievementsTabButton;
        private Text missionSetText;
        private Text missionScoreText;
        private Text missionStatusText;
        private RawImage missionAvatar;
        private readonly RectTransform[] missionCardRects = new RectTransform[ActiveMissionCount];
        private readonly Vector2[] missionCardBasePositions = new Vector2[ActiveMissionCount];
        private readonly Text[] missionCardTitles = new Text[ActiveMissionCount];
        private readonly Text[] missionCardTierTexts = new Text[ActiveMissionCount];
        private readonly Text[] missionCardProgressTexts = new Text[ActiveMissionCount];
        private readonly Image[] missionCardProgressFills = new Image[ActiveMissionCount];
        private readonly Button[] missionSkipButtons = new Button[ActiveMissionCount];
        private readonly Text[] missionSkipCostTexts = new Text[ActiveMissionCount];
        private readonly Button[] missionAdButtons = new Button[ActiveMissionCount];
        private readonly Text[] missionAdButtonTexts = new Text[ActiveMissionCount];
        private readonly GameObject[] missionResolvedBadges = new GameObject[ActiveMissionCount];
        private readonly Text[] missionResolvedTexts = new Text[ActiveMissionCount];
        private Coroutine missionSetTransitionCoroutine;
        private bool missionSetTransitionInProgress;
        private long displayedMissionSetNumber;

        private void BuildMissionPanel(Transform parent)
        {
            gameMissionsContent = new GameObject("MISSIONS · Mission and Achievements");
            gameMissionsContent.transform.SetParent(parent, false);
            Stretch(gameMissionsContent.AddComponent<RectTransform>());

            var background = CreateImage("Mission full-screen deep void", gameMissionsContent.transform,
                Hex("01040B"), FullStretch());
            background.raycastTarget = true;
            BuildUpgradesCircuitBackdrop(gameMissionsContent.transform);

            BuildMissionTabs(gameMissionsContent.transform);

            missionListContent = new GameObject("Mission tab content");
            missionListContent.transform.SetParent(gameMissionsContent.transform, false);
            Stretch(missionListContent.AddComponent<RectTransform>());
            BuildMissionHeader(missionListContent.transform);
            for (var i = 0; i < ActiveMissionCount; i++)
                BuildMissionCard(missionListContent.transform, i, 215f - i * 265f);

            missionStatusText = MakeText(missionListContent.transform, string.Empty, 17,
                FontStyle.Bold, Muted, new Vector2(0f, -475f), new Vector2(900f, 34f),
                TextAnchor.MiddleCenter, 1);

            missionAchievementsContent = new GameObject("Achievements tab · intentionally empty");
            missionAchievementsContent.transform.SetParent(gameMissionsContent.transform, false);
            Stretch(missionAchievementsContent.AddComponent<RectTransform>());

            ShowMissionSection(true);
            gameMissionsContent.SetActive(false);
        }

        private void BuildMissionTabs(Transform parent)
        {
            var strip = CreateCard("Mission top tabs", parent, new Vector2(0f, 675f),
                new Vector2(900f, 82f), Hex("040A16"), 22);
            AddGraphicOutline(strip.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.52f), 1.5f);
            missionTabButton = MakeButton(strip.transform, "MISSIONS", new Vector2(-220f, 0f),
                new Vector2(430f, 68f), Hex("0D4771"), Color.white, 28,
                () => ShowMissionSection(true));
            achievementsTabButton = MakeButton(strip.transform, "ACHIEVEMENTS", new Vector2(220f, 0f),
                new Vector2(430f, 68f), Hex("091126"), Muted, 25,
                () => ShowMissionSection(false));
            AddGraphicOutline(missionTabButton.GetComponent<Image>(), Cyan, 1.8f);
            AddGraphicOutline(achievementsTabButton.GetComponent<Image>(), NeonPurple, 1.8f);
        }

        private void BuildMissionHeader(Transform parent)
        {
            var banner = CreateCard("Mission set banner", parent, new Vector2(0f, 555f),
                new Vector2(900f, 142f), Hex("0A3153"), 32);
            AddGraphicOutline(banner.GetComponent<Image>(), Cyan, 2f);
            var avatarGlow = CreateCard("Mission Nova avatar glow", banner.transform,
                new Vector2(-365f, 0f), new Vector2(150f, 150f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.76f), 75);
            avatarGlow.GetComponent<Image>().raycastTarget = false;
            var avatarMask = CreateCard("Mission Nova avatar mask", avatarGlow.transform,
                Vector2.zero, new Vector2(136f, 136f), Hex("071323"), 68);
            avatarMask.AddComponent<Mask>().showMaskGraphic = true;
            missionAvatar = MakeTextureImage("Mission Nova face", avatarMask.transform,
                Resources.Load<Texture2D>("UI/Avatars/nova"), Vector2.zero,
                new Vector2(136f, 136f));
            if (missionAvatar.texture == null)
            {
                missionAvatar.texture = Resources.Load<Texture2D>("UI/Characters/nova");
                missionAvatar.uvRect = new Rect(0.08f, 0.50f, 0.84f, 0.47f);
            }

            missionSetText = MakeText(banner.transform, "MISSION SET #1", 47,
                FontStyle.Bold, Color.white, new Vector2(85f, 8f), new Vector2(650f, 86f),
                TextAnchor.MiddleCenter, 2);
            AddGraphicOutline(missionSetText, Hex("07101D"), 2f);

            var scorePill = CreateCard("Mission multiplier score", parent, new Vector2(0f, 450f),
                new Vector2(520f, 62f), Hex("111A38"), 23);
            AddGraphicOutline(scorePill.GetComponent<Image>(), NeonPurple, 1.6f);
            missionScoreText = MakeText(scorePill.transform, "SCORE: x1", 27,
                FontStyle.Bold, Cream, Vector2.zero, new Vector2(485f, 50f),
                TextAnchor.MiddleCenter, 1);

            var hint = MakeText(parent, "Complete the missions below to get a bonus reward", 23,
                FontStyle.Bold, Hex("B8E8FF"), new Vector2(0f, 390f),
                new Vector2(900f, 54f), TextAnchor.MiddleCenter, 1);
            hint.resizeTextMinSize = 17;
            hint.resizeTextMaxSize = 23;
            hint.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private void BuildMissionCard(Transform parent, int index, float y)
        {
            var accent = MissionCardAccent(index);
            var glow = CreateCard("Mission " + (index + 1) + " glow", parent,
                new Vector2(0f, y), new Vector2(970f, 238f),
                new Color(accent.r, accent.g, accent.b, 0.46f), 29);
            glow.GetComponent<Image>().raycastTarget = false;
            var card = CreateCard("Mission " + (index + 1) + " card", glow.transform,
                Vector2.zero, new Vector2(958f, 226f),
                Color.Lerp(Hex("040A15"), accent, 0.075f), 25);
            AddGraphicOutline(card.GetComponent<Image>(), accent, 1.8f);
            missionCardRects[index] = glow.GetComponent<RectTransform>();
            missionCardBasePositions[index] = new Vector2(0f, y);

            missionCardTierTexts[index] = MakeText(card.transform, "TIER 1", 16,
                FontStyle.Bold, accent, new Vector2(-365f, 82f), new Vector2(160f, 32f),
                TextAnchor.MiddleLeft, 1);
            missionCardTitles[index] = MakeText(card.transform, "MISSION", 29,
                FontStyle.Bold, Color.white, new Vector2(-175f, 42f),
                new Vector2(560f, 78f), TextAnchor.MiddleLeft, 1);
            missionCardTitles[index].resizeTextForBestFit = true;
            missionCardTitles[index].resizeTextMinSize = 18;
            missionCardTitles[index].resizeTextMaxSize = 29;
            missionCardTitles[index].verticalOverflow = VerticalWrapMode.Truncate;

            var progressTrack = CreateCard("Mission progress track", card.transform,
                new Vector2(-180f, -65f), new Vector2(540f, 48f), Hex("07101F"), 18);
            AddGraphicOutline(progressTrack.GetComponent<Image>(),
                new Color(accent.r, accent.g, accent.b, 0.65f), 1.3f);
            var fillObject = CreateImage("Mission progress fill", progressTrack.transform, accent,
                FullStretch(), RoundedSprite(16));
            var fillRect = fillObject.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(5f, 5f);
            fillRect.offsetMax = new Vector2(-5f, -5f);
            fillObject.type = Image.Type.Filled;
            fillObject.fillMethod = Image.FillMethod.Horizontal;
            fillObject.fillOrigin = 0;
            fillObject.fillAmount = 0f;
            fillObject.raycastTarget = false;
            missionCardProgressFills[index] = fillObject;
            missionCardProgressTexts[index] = MakeText(progressTrack.transform, "0 / 1", 21,
                FontStyle.Bold, Color.white, Vector2.zero, new Vector2(500f, 42f),
                TextAnchor.MiddleCenter, 1);
            AddGraphicOutline(missionCardProgressTexts[index], Hex("02040A"), 1.4f);

            var actionPanel = CreateCard("Mission action panel", card.transform,
                new Vector2(352f, 0f), new Vector2(230f, 190f),
                new Color(0.03f, 0.06f, 0.12f, 0.92f), 20);
            actionPanel.GetComponent<Image>().raycastTarget = false;

            var capturedIndex = index;
            missionSkipButtons[index] = MakeButton(actionPanel.transform, string.Empty,
                Vector2.zero, new Vector2(208f, 112f),
                Color.Lerp(Hex("101522"), accent, 0.28f), Color.white, 22,
                () => CompleteMissionWithCoins(capturedIndex));
            AddGraphicOutline(missionSkipButtons[index].GetComponent<Image>(), accent, 1.5f);
            BuildCanonicalGameCoin(missionSkipButtons[index].transform, new Vector2(-70f, 20f), 40f);
            missionSkipCostTexts[index] = MakeText(missionSkipButtons[index].transform, "5,000", 25,
                FontStyle.Bold, Color.white, new Vector2(26f, 20f), new Vector2(126f, 54f),
                TextAnchor.MiddleCenter, 1);
            var completeForCoinsLabel = MakeText(missionSkipButtons[index].transform, "COMPLETE", 17,
                FontStyle.Bold, accent, new Vector2(0f, -30f), new Vector2(180f, 30f),
                TextAnchor.MiddleCenter, 1);
            completeForCoinsLabel.raycastTarget = false;

            missionAdButtons[index] = MakeButton(actionPanel.transform, string.Empty,
                Vector2.zero, new Vector2(208f, 88f),
                Color.Lerp(Hex("101522"), accent, 0.20f), Color.white, 21,
                () => WatchMissionRemovalAd(capturedIndex));
            AddGraphicOutline(missionAdButtons[index].GetComponent<Image>(), accent, 1.5f);
            missionAdButtonTexts[index] = MakeText(missionAdButtons[index].transform,
                "▶  WATCH AD", 20, FontStyle.Bold, Color.white, Vector2.zero,
                new Vector2(190f, 54f), TextAnchor.MiddleCenter, 1);
            missionAdButtonTexts[index].resizeTextMinSize = 15;
            missionAdButtonTexts[index].resizeTextMaxSize = 20;

            missionResolvedBadges[index] = CreateCard("Mission complete badge", card.transform,
                new Vector2(352f, 0f), new Vector2(210f, 150f),
                new Color(Green.r, Green.g, Green.b, 0.20f), 28);
            AddGraphicOutline(missionResolvedBadges[index].GetComponent<Image>(), Green, 2.2f);
            missionResolvedTexts[index] = MakeText(missionResolvedBadges[index].transform,
                "✓\nCOMPLETE", 30, FontStyle.Bold, Green, Vector2.zero, new Vector2(190f, 130f),
                TextAnchor.MiddleCenter, 1);
            missionResolvedBadges[index].SetActive(false);
        }

        private static Color MissionCardAccent(int index)
        {
            return index switch
            {
                0 => Cyan,
                1 => NeonLime,
                _ => NeonPink
            };
        }

        private void ShowMissionSection(bool showMissions)
        {
            if (missionListContent != null)
                missionListContent.SetActive(showMissions);
            if (missionAchievementsContent != null)
                missionAchievementsContent.SetActive(!showMissions);
            SetMissionTabPalette(missionTabButton, showMissions, Cyan);
            SetMissionTabPalette(achievementsTabButton, !showMissions, NeonPurple);
            if (showMissions)
                RefreshMissionUI();
        }

        private static void SetMissionTabPalette(Button button, bool active, Color accent)
        {
            if (button == null)
                return;
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = active ? Color.Lerp(Hex("071126"), accent, 0.36f) : Hex("071020");
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.color = active ? Color.white : Hex("8E9AB0");
        }

        private void RefreshMissionUI()
        {
            if (missionSetTransitionInProgress)
                return;
            RefreshMissionHeader();
            for (var i = 0; i < ActiveMissionCount; i++)
                RefreshMissionCard(i);
        }

        private void RefreshMissionHeader()
        {
            if (missionSetText != null)
                missionSetText.text = "MISSION SET #" + Math.Max(1L, missionSetNumber).ToString("N0");
            RefreshMissionScoreText();
            displayedMissionSetNumber = Math.Max(1L, missionSetNumber);
        }

        private void RefreshMissionScoreText()
        {
            if (missionScoreText != null)
                missionScoreText.text = "SCORE: x" + Math.Max(1L,
                    bootstrapState.ScoreMultiplier).ToString("N0");
        }

        private void RefreshMissionCard(int index)
        {
            if (index < 0 || index >= ActiveMissionCount || missionCardRects[index] == null)
                return;
            var slot = activeMissionSlots[index];
            missionCardRects[index].gameObject.SetActive(slot != null);
            if (slot == null)
                return;

            var accent = MissionCardAccent(index);
            missionCardTitles[index].text = slot.Title;
            missionCardTierTexts[index].text = "TIER " + slot.Tier.ToString("N0");
            var safeTarget = Math.Max(1L, slot.Target);
            var safeProgress = Math.Min(safeTarget, Math.Max(0L, slot.Progress));
            missionCardProgressTexts[index].text = safeProgress.ToString("N0") + " / " +
                                                   safeTarget.ToString("N0");
            missionCardProgressFills[index].fillAmount = Mathf.Clamp01((float)safeProgress / safeTarget);
            missionCardProgressFills[index].color = string.Equals(slot.Status, "completed",
                StringComparison.Ordinal) ? Green : accent;

            var resolved = slot.IsResolved;
            var usesAd = IsMissionAdActionSlot(index);
            missionSkipButtons[index].gameObject.SetActive(!resolved && !usesAd);
            missionAdButtons[index].gameObject.SetActive(!resolved && usesAd);
            missionResolvedBadges[index].SetActive(resolved);
            if (resolved)
            {
                missionResolvedTexts[index].text = "✓\nCOMPLETE";
                missionResolvedTexts[index].fontSize = 30;
                missionResolvedTexts[index].color = Green;
            }

            if (!resolved)
            {
                var canSkip = !usesAd && !missionActionInFlight && realtimeMissionPlayerReference != null
                              && bootstrapState.Coins >= MissionSkipCoinCost;
                missionSkipButtons[index].interactable = canSkip;
                missionSkipButtons[index].GetComponent<Image>().color = canSkip
                    ? Color.Lerp(Hex("101522"), accent, 0.28f)
                    : Hex("252C38");
                missionSkipCostTexts[index].color = canSkip ? Color.white : Hex("9299A5");

                var placementId = MissionAdPlacementPrefix + slot.InstanceId;
                var canWatch = usesAd && !missionActionInFlight && rewardedAdProvider != null
                               && rewardedAdProvider.IsRewardedAdReady(placementId);
                missionAdButtons[index].interactable = canWatch;
                missionAdButtons[index].GetComponent<Image>().color = canWatch
                    ? Color.Lerp(Hex("101522"), accent, 0.20f)
                    : Hex("252C38");
                missionAdButtonTexts[index].color = canWatch ? Color.white : Hex("9299A5");
            }
        }

        private void SetMissionStatus(string message, Color color)
        {
            if (missionStatusText == null)
                return;
            missionStatusText.text = message ?? string.Empty;
            missionStatusText.color = color;
        }

        private void BeginMissionSetTransition()
        {
            if (missionSetTransitionCoroutine != null)
                StopCoroutine(missionSetTransitionCoroutine);
            missionSetTransitionCoroutine = StartCoroutine(AnimateMissionSetReplacement());
        }

        private IEnumerator AnimateMissionSetReplacement()
        {
            missionSetTransitionInProgress = true;
            for (var index = ActiveMissionCount - 1; index >= 0; index--)
            {
                var rect = missionCardRects[index];
                if (rect == null)
                    continue;
                var home = missionCardBasePositions[index];
                yield return AnimateMissionCardPosition(rect, home, home + new Vector2(1200f, 0f), 0.22f);
                RefreshMissionCard(index);
                rect.anchoredPosition = home - new Vector2(1200f, 0f);
                yield return AnimateMissionCardPosition(rect, rect.anchoredPosition, home, 0.28f);
            }
            missionSetTransitionInProgress = false;
            missionSetTransitionCoroutine = null;
            RefreshMissionHeader();
            RefreshMissionUI();
        }

        private static IEnumerator AnimateMissionCardPosition(RectTransform rect, Vector2 from,
            Vector2 to, float duration)
        {
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                rect.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                yield return null;
            }
            rect.anchoredPosition = to;
        }
    }
}
