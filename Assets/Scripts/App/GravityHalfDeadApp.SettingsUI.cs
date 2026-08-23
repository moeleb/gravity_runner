using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
#if UNITY_IOS
using System.Runtime.InteropServices;
#endif
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private const string ResolutionQualityKey = "ghd.settings.resolution_quality";
        private const string TargetFpsKey = "ghd.settings.target_fps";

        private static readonly string[] ResolutionQualityNames = { "LOW", "NORMAL", "HIGH" };
        private static readonly int[] FpsChoices = { 30, 60, 90, 120 };

        private GameObject gameSettingsMainPage;
        private GameObject gameSettingsSupportPage;
        private RawImage gameSettingsAvatarImage;
        private GameObject gameSettingsAvatarFallback;
        private Text gameSettingsEmailText;
        private Text gameSettingsProviderText;
        private Text gameSettingsPlayerIdText;
        private Text gameSettingsLinkStatusText;
        private Text gameSettingsProviderBadgeText;
        private Button gameSettingsLogoutButton;
        private Button gameSettingsFacebookButton;
        private Button gameSettingsPlatformButton;
        private Text gameSettingsFacebookButtonText;
        private Text gameSettingsPlatformButtonText;
        private Text gameSettingsFacebookIconText;
        private Text gameSettingsPlatformIconText;

        private Button gameResolutionCycleButton;
        private Button gameResolutionPreviousButton;
        private Button gameResolutionNextButton;
        private Text gameResolutionPreviousText;
        private Text gameResolutionValueText;
        private Text gameResolutionNextText;
        private Button gameFpsCycleButton;
        private Button gameFpsPreviousButton;
        private Button gameFpsNextButton;
        private Text gameFpsPreviousText;
        private Text gameFpsValueText;
        private Text gameFpsNextText;
        private int resolutionQualityIndex = 1;
        private int targetFps = 60;

        private int settingsAvatarRequestVersion;
        private bool settingsProviderLinkInProgress;
        private string pendingSettingsProvider = string.Empty;

        // Contact-support page runtime UI. The technical identity block is visible but
        // read-only, so players can never accidentally remove the identifiers support needs.
        private Text supportIdentityText;
        private readonly GameObject[] supportPreviewCards = new GameObject[2];
        private readonly RawImage[] supportPreviewImages = new RawImage[2];
        private readonly Texture2D[] supportPreviewTextures = new Texture2D[2];
        private readonly string[] supportPreviewLoadedPaths = new string[2];
        private RectTransform supportAddImageCardRect;

        private static readonly Color SettingsBackground = Hex("050816");
        private static readonly Color SettingsCard = Hex("0C1328");
        private static readonly Color SettingsCardInner = Hex("10182E");
        private static readonly Color SettingsBorder = Hex("24304D");
        private static readonly Color SettingsCyan = Hex("6EDCFF");
        private static readonly Color SettingsTeal = Hex("22DCC5");
        private static readonly Color SettingsGreen = Hex("39E164");
        private static readonly Color SettingsRed = Hex("D52D58");
        private static readonly Color SettingsPurple = Hex("8E52FF");
        private static readonly Color SettingsPink = Hex("F74C91");
        private static readonly Color SettingsYellow = Hex("FFD14D");

        private void BuildGameSettings(Transform parent)
        {
            // The settings background must cover the COMPLETE canvas, including the areas
            // outside the gameplay safe-area. The controls themselves still live inside a
            // SafeAreaFitter so notches / rounded corners cannot cover interactive content.
            var canvas = parent.GetComponentInParent<Canvas>();
            var overlayParent = canvas != null ? canvas.transform : parent;

            var panel = new GameObject("Settings full-screen overlay");
            panel.transform.SetParent(overlayParent, false);
            var panelRect = panel.AddComponent<RectTransform>();
            Stretch(panelRect);
            var background = panel.AddComponent<Image>();
            background.color = SettingsBackground;
            background.raycastTarget = true;
            gameSettingsPanel = panel.AddComponent<CanvasGroup>();

            var safeContent = new GameObject("Settings safe content");
            safeContent.transform.SetParent(panel.transform, false);
            var safeRect = safeContent.AddComponent<RectTransform>();
            Stretch(safeRect);
            safeContent.AddComponent<SafeAreaFitter>();

            BuildSettingsMainPage(safeContent.transform);
            BuildSettingsSupportPage(safeContent.transform);

            LoadLocalSettings();
            ApplyAudioSettings();
            ApplyGraphicsSettings();
            RefreshSettingsUI();

            gameSettingsPanel.alpha = 0f;
            gameSettingsPanel.blocksRaycasts = false;
            gameSettingsPanel.interactable = false;
            panel.SetActive(false);
        }

        private void BuildSettingsMainPage(Transform parent)
        {
            gameSettingsMainPage = new GameObject("Settings main page");
            gameSettingsMainPage.transform.SetParent(parent, false);
            var mainRect = gameSettingsMainPage.AddComponent<RectTransform>();
            Stretch(mainRect);

            var close = MakeButton(gameSettingsMainPage.transform, "←", new Vector2(-440f, 805f),
                new Vector2(86f, 86f), Hex("07172C"), SettingsTeal, 46, CloseGameSettings);
            AddSettingsOutline(close.GetComponent<Image>(), new Color(SettingsTeal.r, SettingsTeal.g, SettingsTeal.b, 0.32f), 2f);

            MakeText(gameSettingsMainPage.transform, "SETTINGS", 48, FontStyle.Bold, Cream,
                new Vector2(0f, 805f), new Vector2(560f, 80f), TextAnchor.MiddleCenter, 2);

            var headerDivider = CreateImage("Settings header divider", gameSettingsMainPage.transform,
                new Color(SettingsBorder.r, SettingsBorder.g, SettingsBorder.b, 0.75f),
                Centered(new Vector2(0f, 742f), new Vector2(950f, 2f)), RoundedSprite(2));
            headerDivider.raycastTarget = false;

            BuildSettingsAccountCard(gameSettingsMainPage.transform);
            BuildSettingsAudioCard(gameSettingsMainPage.transform);
            BuildSettingsGraphicsCard(gameSettingsMainPage.transform);
            BuildSettingsLinksCard(gameSettingsMainPage.transform);

            MakeText(gameSettingsMainPage.transform, "GAME VERSION", 19, FontStyle.Bold, Hex("707A98"),
                new Vector2(0f, -765f), new Vector2(420f, 34f), TextAnchor.MiddleCenter, 2);
            MakeText(gameSettingsMainPage.transform, Application.version, 21, FontStyle.Normal, Hex("929AB4"),
                new Vector2(0f, -804f), new Vector2(520f, 40f), TextAnchor.MiddleCenter);
        }

        private void BuildSettingsAccountCard(Transform parent)
        {
            var card = CreateSettingsSectionCard("Account settings", parent, new Vector2(0f, 475f),
                new Vector2(910f, 430f));
            MakeText(card.transform, "ACCOUNT", 22, FontStyle.Bold, SettingsCyan,
                new Vector2(-285f, 172f), new Vector2(320f, 42f), TextAnchor.MiddleLeft, 1);

            var identity = CreateCard("Account identity", card.transform, new Vector2(0f, 62f),
                new Vector2(850f, 150f), SettingsCardInner, 26);
            AddSettingsOutline(identity.GetComponent<Image>(), SettingsBorder, 2f);

            var avatarRing = CreateCard("Settings avatar ring", identity.transform, new Vector2(-345f, 4f),
                new Vector2(108f, 108f), SettingsCyan, 54);
            var avatarMask = CreateCard("Settings avatar mask", avatarRing.transform, Vector2.zero,
                new Vector2(98f, 98f), Hex("070B18"), 49);
            avatarMask.GetComponent<Image>().type = Image.Type.Simple;
            avatarMask.AddComponent<Mask>().showMaskGraphic = true;

            gameSettingsAvatarImage = MakeTextureImage("Settings account avatar", avatarMask.transform, null,
                Vector2.zero, new Vector2(98f, 98f));
            gameSettingsAvatarImage.uvRect = new Rect(0f, 0f, 1f, 1f);

            gameSettingsAvatarFallback = new GameObject("Settings avatar fallback");
            gameSettingsAvatarFallback.transform.SetParent(avatarMask.transform, false);
            var fallbackRect = gameSettingsAvatarFallback.AddComponent<RectTransform>();
            Stretch(fallbackRect);
            CreateImage("Settings guest head", gameSettingsAvatarFallback.transform, Cream,
                Centered(new Vector2(0f, 18f), new Vector2(34f, 34f)), RoundedSprite(17)).raycastTarget = false;
            CreateImage("Settings guest shoulders", gameSettingsAvatarFallback.transform, Cream,
                Centered(new Vector2(0f, -23f), new Vector2(62f, 34f)), RoundedSprite(17)).raycastTarget = false;

            var providerBadge = CreateCard("Settings provider badge", identity.transform, new Vector2(-304f, -34f),
                new Vector2(38f, 38f), Cream, 19);
            gameSettingsProviderBadgeText = MakeText(providerBadge.transform, "?", 24, FontStyle.Bold, Hex("1C2A48"),
                Vector2.zero, new Vector2(34f, 34f), TextAnchor.MiddleCenter);

            gameSettingsEmailText = MakeText(identity.transform, "Guest player", 21, FontStyle.Bold, Cream,
                new Vector2(-40f, 31f), new Vector2(395f, 40f), TextAnchor.MiddleLeft);
            gameSettingsProviderText = MakeText(identity.transform, "Not linked yet", 16, FontStyle.Bold, SettingsGreen,
                new Vector2(-40f, -10f), new Vector2(395f, 34f), TextAnchor.MiddleLeft);
            gameSettingsPlayerIdText = MakeText(identity.transform, "", 13, FontStyle.Normal, Hex("8893B2"),
                new Vector2(-40f, -44f), new Vector2(395f, 26f), TextAnchor.MiddleLeft);
            gameSettingsAccountText = gameSettingsProviderText;

            gameSettingsLogoutButton = MakeButton(identity.transform, "LOGOUT", new Vector2(330f, 4f),
                new Vector2(160f, 76f), SettingsRed, Cream, 19, LogoutFromGame);
            AddSettingsOutline(gameSettingsLogoutButton.GetComponent<Image>(), Hex("FF5276"), 2f);

            MakeText(card.transform, "CONNECT ANOTHER LOGIN", 14, FontStyle.Bold, Hex("68728F"),
                new Vector2(0f, -45f), new Vector2(360f, 30f), TextAnchor.MiddleCenter, 2);
            CreateImage("Connect divider left", card.transform, SettingsBorder,
                Centered(new Vector2(-260f, -45f), new Vector2(215f, 2f)), RoundedSprite(2)).raycastTarget = false;
            CreateImage("Connect divider right", card.transform, SettingsBorder,
                Centered(new Vector2(260f, -45f), new Vector2(215f, 2f)), RoundedSprite(2)).raycastTarget = false;

            gameLinkedAccountActions = new GameObject("Provider connection actions");
            gameLinkedAccountActions.transform.SetParent(card.transform, false);
            var actionsRect = gameLinkedAccountActions.AddComponent<RectTransform>();
            SetRect(actionsRect, new Vector2(0f, -125f), new Vector2(850f, 96f));
            gameGuestAccountActions = gameLinkedAccountActions;

            gameSettingsFacebookButton = MakeProviderButton(gameLinkedAccountActions.transform, "f", "CONNECT FACEBOOK",
                new Vector2(205f, 0f), Hex("1877F2"), OnSettingsFacebookPressed,
                out gameSettingsFacebookIconText, out gameSettingsFacebookButtonText);

#if UNITY_IOS
            gameSettingsPlatformButton = MakeProviderButton(gameLinkedAccountActions.transform, "A", "CONNECT APPLE",
                new Vector2(-205f, 0f), Hex("101218"), OnSettingsApplePressed,
                out gameSettingsPlatformIconText, out gameSettingsPlatformButtonText);
#else
            gameSettingsPlatformButton = MakeProviderButton(gameLinkedAccountActions.transform, "G", "CONNECT GOOGLE",
                new Vector2(-205f, 0f), Hex("FFFFFF"), OnSettingsGooglePressed,
                out gameSettingsPlatformIconText, out gameSettingsPlatformButtonText);
#endif

            gameSettingsLinkStatusText = MakeText(card.transform, string.Empty, 14, FontStyle.Bold, SettingsGreen,
                new Vector2(0f, -190f), new Vector2(780f, 28f), TextAnchor.MiddleCenter);
        }

        private void BuildSettingsAudioCard(Transform parent)
        {
            var card = CreateSettingsSectionCard("Audio and notifications", parent, new Vector2(0f, 90f),
                new Vector2(910f, 300f));
            MakeText(card.transform, "AUDIO & NOTIFICATIONS", 22, FontStyle.Bold, SettingsCyan,
                new Vector2(-225f, 112f), new Vector2(430f, 42f), TextAnchor.MiddleLeft, 1);

            gameMusicButton = MakeSettingsToggle(card.transform, "♫", "MUSIC", SettingsPurple,
                new Vector2(-220f, 40f), ToggleMusic);
            gameSfxButton = MakeSettingsToggle(card.transform, "◖", "SFX", Hex("31B9FF"),
                new Vector2(220f, 40f), ToggleSfx);
            gameVibrationButton = MakeSettingsToggle(card.transform, "▣", "VIBRATION", SettingsYellow,
                new Vector2(-220f, -72f), ToggleVibration);
            gameNotificationsButton = MakeSettingsToggle(card.transform, "●", "NOTIFICATIONS", SettingsPink,
                new Vector2(220f, -72f), ToggleNotifications);
        }

        private void BuildSettingsGraphicsCard(Transform parent)
        {
            var card = CreateSettingsSectionCard("Graphics", parent, new Vector2(0f, -225f),
                new Vector2(910f, 280f));
            MakeText(card.transform, "GRAPHICS", 22, FontStyle.Bold, SettingsCyan,
                new Vector2(-285f, 108f), new Vector2(320f, 42f), TextAnchor.MiddleLeft, 1);

            var inner = CreateCard("Graphics controls", card.transform, new Vector2(0f, -8f),
                new Vector2(850f, 190f), SettingsCardInner, 24);
            AddSettingsOutline(inner.GetComponent<Image>(), SettingsBorder, 2f);

            MakeText(inner.transform, "RESOLUTION", 18, FontStyle.Bold, Cream,
                new Vector2(-305f, 47f), new Vector2(230f, 40f), TextAnchor.MiddleLeft);
            MakeText(inner.transform, "FPS (FRAME RATE)", 18, FontStyle.Bold, Cream,
                new Vector2(-270f, -47f), new Vector2(300f, 40f), TextAnchor.MiddleLeft);

            var divider = CreateImage("Graphics divider", inner.transform, SettingsBorder,
                Centered(Vector2.zero, new Vector2(850f, 2f)), RoundedSprite(2));
            divider.raycastTarget = false;

            BuildResolutionSelector(inner.transform);
            BuildFpsSelector(inner.transform);
        }

        private void BuildResolutionSelector(Transform parent)
        {
            BuildSingleValueSelector(
                "Resolution selector", parent, new Vector2(225f, 47f),
                out gameResolutionCycleButton, out gameResolutionPreviousButton, out gameResolutionNextButton,
                out gameResolutionPreviousText, out gameResolutionValueText, out gameResolutionNextText,
                () => StepResolutionQuality(1), () => StepResolutionQuality(-1), () => StepResolutionQuality(1));
        }

        private void BuildFpsSelector(Transform parent)
        {
            BuildSingleValueSelector(
                "FPS selector", parent, new Vector2(225f, -47f),
                out gameFpsCycleButton, out gameFpsPreviousButton, out gameFpsNextButton,
                out gameFpsPreviousText, out gameFpsValueText, out gameFpsNextText,
                () => StepTargetFps(1), () => StepTargetFps(-1), () => StepTargetFps(1));
        }

        private void BuildSingleValueSelector(string name, Transform parent, Vector2 position,
            out Button cycleButton, out Button previousButton, out Button nextButton,
            out Text previousText, out Text valueText, out Text nextText,
            UnityEngine.Events.UnityAction cycleAction, UnityEngine.Events.UnityAction previousAction,
            UnityEngine.Events.UnityAction nextAction)
        {
            // Visually this is ONE control. The left / right arrow zones are invisible hit
            // regions so the user can move backwards or forwards without showing 3-4 buttons.
            var control = CreateCard(name, parent, position, new Vector2(330f, 62f), Hex("111A30"), 14);
            var controlImage = control.GetComponent<Image>();
            AddSettingsOutline(controlImage, new Color(SettingsTeal.r, SettingsTeal.g, SettingsTeal.b, 0.38f), 1f);

            cycleButton = control.AddComponent<Button>();
            cycleButton.targetGraphic = controlImage;
            cycleButton.navigation = new Navigation { mode = Navigation.Mode.None };
            cycleButton.onClick.AddListener(cycleAction);

            // Fixed thirds keep both arrows mathematically centered in the selector.
            // ASCII < and > render fully with Unity's built-in fonts, unlike the narrower
            // single-chevron glyphs that can look clipped or vertically off-center.
            previousButton = BuildSelectorArrowHitTarget(control.transform, "Previous", -126f, previousAction);
            nextButton = BuildSelectorArrowHitTarget(control.transform, "Next", 126f, nextAction);

            previousText = MakeText(control.transform, "<", 28, FontStyle.Bold, SettingsTeal,
                new Vector2(-126f, 0f), new Vector2(78f, 62f), TextAnchor.MiddleCenter);
            previousText.alignByGeometry = true;
            previousText.raycastTarget = false;

            valueText = MakeText(control.transform, string.Empty, 18, FontStyle.Bold, Cream,
                Vector2.zero, new Vector2(176f, 50f), TextAnchor.MiddleCenter);
            valueText.alignByGeometry = true;
            valueText.raycastTarget = false;

            nextText = MakeText(control.transform, ">", 28, FontStyle.Bold, SettingsTeal,
                new Vector2(126f, 0f), new Vector2(78f, 62f), TextAnchor.MiddleCenter);
            nextText.alignByGeometry = true;
            nextText.raycastTarget = false;
        }

        private Button BuildSelectorArrowHitTarget(Transform parent, string name, float x,
            UnityEngine.Events.UnityAction action)
        {
            var hit = new GameObject(name + " selector hit target");
            hit.transform.SetParent(parent, false);
            var rect = hit.AddComponent<RectTransform>();
            SetRect(rect, new Vector2(x, 0f), new Vector2(78f, 62f));
            var image = hit.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
            var button = hit.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);
            return button;
        }

        private void BuildSettingsLinksCard(Transform parent)
        {
            var card = CreateSettingsSectionCard("Privacy and help", parent, new Vector2(0f, -485f),
                new Vector2(910f, 190f));

            BuildSettingsLinkRow(card.transform, "◉", "CONTACT SUPPORT", SettingsCyan,
                new Vector2(0f, 47f), OpenSettingsSupportPage);
            CreateImage("Support privacy divider", card.transform, SettingsBorder,
                Centered(Vector2.zero, new Vector2(850f, 2f)), RoundedSprite(2)).raycastTarget = false;
            BuildSettingsLinkRow(card.transform, "▣", "PRIVACY POLICY", SettingsPurple,
                new Vector2(0f, -47f), OpenPrivacyPolicy);
        }

        private void BuildSettingsSupportPage(Transform parent)
        {
            gameSettingsSupportPage = new GameObject("Settings support page");
            gameSettingsSupportPage.transform.SetParent(parent, false);
            var rootRect = gameSettingsSupportPage.AddComponent<RectTransform>();
            Stretch(rootRect);

            // Fill the safe area from top to bottom. The settings overlay itself already
            // covers the COMPLETE device screen; this page occupies the full safe viewport.
            var back = MakeButton(gameSettingsSupportPage.transform, "←", new Vector2(-446f, 814f),
                new Vector2(96f, 96f), Hex("07172C"), SettingsTeal, 50, CloseSettingsSupportPage);
            AddSettingsOutline(back.GetComponent<Image>(),
                new Color(SettingsTeal.r, SettingsTeal.g, SettingsTeal.b, 0.62f), 2f);

            var title = MakeText(gameSettingsSupportPage.transform, "CONTACT SUPPORT", 54,
                FontStyle.BoldAndItalic, Cream, new Vector2(0f, 814f),
                new Vector2(760f, 92f), TextAnchor.MiddleCenter, 1);
            title.resizeTextForBestFit = false;

            var headerGlow = CreateImage("Support header glow", gameSettingsSupportPage.transform,
                new Color(SettingsTeal.r, SettingsTeal.g, SettingsTeal.b, 0.86f),
                Centered(new Vector2(0f, 744f), new Vector2(420f, 4f)), RoundedSprite(2));
            headerGlow.raycastTarget = false;

            var formRoot = new GameObject("Support form root");
            formRoot.transform.SetParent(gameSettingsSupportPage.transform, false);
            var formRect = formRoot.AddComponent<RectTransform>();
            Stretch(formRect);

            // ── NEED HELP ───────────────────────────────────────────────────
            var helpCard = CreateCard("Support help card", formRoot.transform,
                new Vector2(0f, 610f), new Vector2(930f, 210f), Hex("09162B"), 34);
            AddSettingsOutline(helpCard.GetComponent<Image>(),
                new Color(SettingsCyan.r, SettingsCyan.g, SettingsCyan.b, 0.64f), 1.8f);

            BuildSupportModeratorIcon(helpCard.transform, new Vector2(-360f, 0f));

            var helpTitle = MakeText(helpCard.transform, "Need help?", 34, FontStyle.Bold, Cream,
                new Vector2(70f, 38f), new Vector2(650f, 56f), TextAnchor.MiddleLeft);
            helpTitle.resizeTextForBestFit = false;

            var helpBody = MakeText(helpCard.transform,
                "Describe what happened and our support team will reply to the email linked to your account.",
                23, FontStyle.Normal, Hex("B8C4E1"), new Vector2(70f, -30f),
                new Vector2(650f, 96f), TextAnchor.MiddleLeft);
            helpBody.resizeTextForBestFit = false;
            helpBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            helpBody.verticalOverflow = VerticalWrapMode.Truncate;

            // ── SUBJECT ─────────────────────────────────────────────────────
            BuildSupportSectionLabel(formRoot.transform, "SUBJECT", 458f);
            supportSubjectInput = MakeSupportInputField(formRoot.transform,
                "Subject (e.g. Missing item from inventory)", new Vector2(0f, 382f),
                new Vector2(930f, 104f), false);

            // ── DESCRIPTION ─────────────────────────────────────────────────
            BuildSupportSectionLabel(formRoot.transform, "DESCRIPTION", 275f);

            // One large description card. The top area is editable; the lower technical
            // block is permanently read-only and is also appended to the outgoing request.
            var descriptionCard = CreateCard("Support description card", formRoot.transform,
                new Vector2(0f, 35f), new Vector2(930f, 410f), Hex("08162B"), 30);
            AddSettingsOutline(descriptionCard.GetComponent<Image>(),
                new Color(SettingsCyan.r, SettingsCyan.g, SettingsCyan.b, 0.56f), 1.5f);

            MakeText(descriptionCard.transform, "✎", 38, FontStyle.Bold, SettingsCyan,
                new Vector2(-404f, 130f), new Vector2(48f, 48f), TextAnchor.MiddleCenter);

            supportBodyInput = MakeSupportInputField(descriptionCard.transform,
                "Describe your issue here...", new Vector2(30f, 115f),
                new Vector2(810f, 128f), true, false);

            var diagnosticsDivider = CreateImage("Support diagnostics divider", descriptionCard.transform,
                new Color(SettingsBorder.r, SettingsBorder.g, SettingsBorder.b, 0.92f),
                Centered(new Vector2(0f, 42f), new Vector2(820f, 2f)), RoundedSprite(1));
            diagnosticsDivider.raycastTarget = false;

            MakeText(descriptionCard.transform, "SYSTEM INFO · ATTACHED AUTOMATICALLY", 16,
                FontStyle.Bold, SettingsTeal, new Vector2(-190f, 15f),
                new Vector2(460f, 28f), TextAnchor.MiddleLeft, 1);

            supportIdentityText = MakeText(descriptionCard.transform,
                "#########\nPlayer ID: loading…\nGmail address linked in: loading…\nUUID / Firebase UID: loading…\nDevice ID: loading…\n#########",
                19, FontStyle.Normal, Hex("D3D9EA"), new Vector2(30f, -83f),
                new Vector2(810f, 166f), TextAnchor.UpperLeft);
            supportIdentityText.resizeTextForBestFit = false;
            supportIdentityText.horizontalOverflow = HorizontalWrapMode.Wrap;
            supportIdentityText.verticalOverflow = VerticalWrapMode.Truncate;
            supportIdentityText.raycastTarget = false;

            // ── ATTACHMENTS ─────────────────────────────────────────────────
            var attachments = CreateCard("Support attachments", formRoot.transform,
                new Vector2(0f, -340f), new Vector2(930f, 300f), Hex("09162A"), 30);
            AddSettingsOutline(attachments.GetComponent<Image>(),
                new Color(SettingsCyan.r, SettingsCyan.g, SettingsCyan.b, 0.48f), 1.5f);

            MakeText(attachments.transform, "▣", 32, FontStyle.Bold, SettingsCyan,
                new Vector2(-407f, 113f), new Vector2(48f, 44f), TextAnchor.MiddleCenter);
            MakeText(attachments.transform, "ATTACH SCREENSHOTS", 24, FontStyle.Bold, Cream,
                new Vector2(-205f, 113f), new Vector2(380f, 42f), TextAnchor.MiddleLeft, 1);
            MakeText(attachments.transform, "PNG / JPG • 10 MB maximum", 17, FontStyle.Normal, Hex("9AA7C6"),
                new Vector2(263f, 113f), new Vector2(360f, 36f), TextAnchor.MiddleRight);

            BuildSupportPreviewSlot(attachments.transform, 0);
            BuildSupportPreviewSlot(attachments.transform, 1);
            BuildSupportAddImageCard(attachments.transform);

            supportAttachmentText = MakeText(attachments.transform, "No screenshots selected", 17,
                FontStyle.Normal, Hex("95A2C0"), new Vector2(0f, -126f),
                new Vector2(830f, 32f), TextAnchor.MiddleCenter);
            supportAttachmentText.resizeTextForBestFit = false;

            // ── SEND ────────────────────────────────────────────────────────
            supportSendButton = MakeButton(formRoot.transform, "SEND REQUEST  >", new Vector2(0f, -595f),
                new Vector2(930f, 126f), SettingsTeal, Hex("061018"), 32, SubmitSupportRequestWithIdentity);
            supportSendButton.gameObject.AddComponent<ButtonGlow>();
            AddSettingsOutline(supportSendButton.GetComponent<Image>(),
                new Color(SettingsTeal.r, SettingsTeal.g, SettingsTeal.b, 0.82f), 3f);

            supportStatusText = MakeText(formRoot.transform, string.Empty, 19, FontStyle.Bold, Hex("9AA7C6"),
                new Vector2(0f, -682f), new Vector2(900f, 58f), TextAnchor.MiddleCenter);
            supportStatusText.resizeTextForBestFit = false;

            MakeText(formRoot.transform, "Gravity: Half Dead  •  v" + Application.version, 18,
                FontStyle.Normal, Hex("8995B4"), new Vector2(0f, -775f),
                new Vector2(700f, 40f), TextAnchor.MiddleCenter);

            // Refresh real identity values whenever the support page opens and rebuild the
            // screenshot thumbnails whenever the picker changes supportImagePaths.
            var runtimeRefresh = gameSettingsSupportPage.AddComponent<SupportPageRuntimeRefresher>();
            runtimeRefresh.Configure(this);

            // When the keyboard opens, only the form content lifts. Header/back remain fixed.
            var keyboardAvoider = formRoot.AddComponent<SupportKeyboardAvoider>();
            keyboardAvoider.Configure(formRect, supportSubjectInput, supportBodyInput, 42f, 500f);

            RefreshSupportIdentityText();
            RefreshSupportAttachmentPreviews();
            gameSettingsSupportPage.SetActive(false);
        }

        private void BuildSupportModeratorIcon(Transform parent, Vector2 position)
        {
            // Pure Unity UI headset/moderator icon — no external sprite required.
            var badge = CreateCard("Support moderator badge", parent, position,
                new Vector2(132f, 132f), new Color(SettingsCyan.r, SettingsCyan.g, SettingsCyan.b, 0.08f), 66);
            AddSettingsOutline(badge.GetComponent<Image>(),
                new Color(SettingsCyan.r, SettingsCyan.g, SettingsCyan.b, 0.72f), 2f);

            // Headset band: cyan outer circle, dark inner circle, then cover the lower half
            // so only the upper arc remains visible.
            var bandOuter = CreateCard("Headset band outer", badge.transform, new Vector2(0f, 4f),
                new Vector2(78f, 78f), SettingsCyan, 39);
            bandOuter.GetComponent<Image>().raycastTarget = false;
            var bandInner = CreateCard("Headset band inner", bandOuter.transform, Vector2.zero,
                new Vector2(64f, 64f), Hex("09162B"), 32);
            bandInner.GetComponent<Image>().raycastTarget = false;
            var bandCover = CreateCard("Headset band cover", badge.transform, new Vector2(0f, -18f),
                new Vector2(92f, 48f), Hex("09162B"), 8);
            bandCover.GetComponent<Image>().raycastTarget = false;

            CreateCard("Headset left cup", badge.transform, new Vector2(-37f, -4f),
                new Vector2(15f, 42f), SettingsCyan, 7).GetComponent<Image>().raycastTarget = false;
            CreateCard("Headset right cup", badge.transform, new Vector2(37f, -4f),
                new Vector2(15f, 42f), SettingsCyan, 7).GetComponent<Image>().raycastTarget = false;
            CreateCard("Headset mic arm", badge.transform, new Vector2(24f, -31f),
                new Vector2(38f, 9f), SettingsCyan, 4).GetComponent<Image>().raycastTarget = false;
            CreateCard("Headset mic", badge.transform, new Vector2(4f, -31f),
                new Vector2(13f, 13f), SettingsCyan, 6).GetComponent<Image>().raycastTarget = false;
        }

        private void BuildSupportSectionLabel(Transform parent, string label, float y)
        {
            var accent = CreateImage(label + " accent", parent, SettingsTeal,
                Centered(new Vector2(-455f, y), new Vector2(7f, 34f)), RoundedSprite(3));
            accent.raycastTarget = false;
            var text = MakeText(parent, label, 25, FontStyle.Bold, Cream,
                new Vector2(-330f, y), new Vector2(250f, 46f), TextAnchor.MiddleLeft, 1);
            text.resizeTextForBestFit = false;
        }

        private InputField MakeSupportInputField(Transform parent, string placeholderText,
            Vector2 position, Vector2 size, bool multiline, bool addOutline = true)
        {
            var input = MakeInputField(parent, placeholderText, position, size, multiline);
            var background = input.GetComponent<Image>();
            if (background != null)
            {
                background.color = Hex("08162B");
                if (addOutline)
                {
                    AddSettingsOutline(background,
                        new Color(SettingsCyan.r, SettingsCyan.g, SettingsCyan.b, multiline ? 0.46f : 0.78f),
                        multiline ? 1.3f : 1.8f);
                }
            }

            input.shouldHideMobileInput = true;
            input.caretWidth = 4;
            input.caretBlinkRate = 0.75f;
            input.selectionColor = new Color(SettingsCyan.r, SettingsCyan.g, SettingsCyan.b, 0.30f);
            input.navigation = new Navigation { mode = Navigation.Mode.None };

            if (input.textComponent != null)
            {
                input.textComponent.fontSize = multiline ? 23 : 24;
                input.textComponent.resizeTextForBestFit = false;
                input.textComponent.color = Cream;
                input.textComponent.rectTransform.anchoredPosition = new Vector2(10f, multiline ? -5f : 0f);
                input.textComponent.rectTransform.sizeDelta = size - new Vector2(82f, multiline ? 30f : 30f);
            }

            if (input.placeholder is Text placeholder)
            {
                placeholder.fontSize = multiline ? 22 : 23;
                placeholder.resizeTextForBestFit = false;
                placeholder.color = Hex("8290B1");
                placeholder.rectTransform.anchoredPosition = new Vector2(10f, multiline ? -5f : 0f);
                placeholder.rectTransform.sizeDelta = size - new Vector2(82f, multiline ? 30f : 30f);
            }

            return input;
        }

        private void BuildSupportPreviewSlot(Transform parent, int index)
        {
            var card = CreateCard("Support preview " + (index + 1), parent, Vector2.zero,
                new Vector2(250f, 172f), Hex("071426"), 24);
            AddSettingsOutline(card.GetComponent<Image>(),
                new Color(SettingsCyan.r, SettingsCyan.g, SettingsCyan.b, 0.56f), 1.4f);

            // The rounded card doubles as a mask so real screenshots fill the tile cleanly.
            var mask = card.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var imageObject = new GameObject("Screenshot image " + (index + 1));
            imageObject.transform.SetParent(card.transform, false);
            var imageRect = imageObject.AddComponent<RectTransform>();
            Stretch(imageRect);
            var raw = imageObject.AddComponent<RawImage>();
            raw.color = Color.white;
            raw.raycastTarget = false;
            supportPreviewImages[index] = raw;

            var remove = MakeButton(card.transform, "×", new Vector2(98f, 61f),
                new Vector2(46f, 46f), Hex("121A31"), Cream, 31,
                () => RemoveSupportImageAt(index));
            AddSettingsOutline(remove.GetComponent<Image>(),
                new Color(Color.white.r, Color.white.g, Color.white.b, 0.72f), 1f);

            supportPreviewCards[index] = card;
            card.SetActive(false);
        }

        private void BuildSupportAddImageCard(Transform parent)
        {
            var card = CreateCard("Support add image card", parent, Vector2.zero,
                new Vector2(250f, 172f), Hex("0B1A31"), 24);
            AddSettingsOutline(card.GetComponent<Image>(),
                new Color(SettingsCyan.r, SettingsCyan.g, SettingsCyan.b, 0.58f), 1.5f);
            supportAddImageCardRect = card.GetComponent<RectTransform>();

            supportAttachButton = card.AddComponent<Button>();
            supportAttachButton.targetGraphic = card.GetComponent<Image>();
            supportAttachButton.navigation = new Navigation { mode = Navigation.Mode.None };
            supportAttachButton.onClick.AddListener(OpenSupportImagePicker);

            MakeText(card.transform, "+", 58, FontStyle.Normal, SettingsTeal,
                new Vector2(0f, 26f), new Vector2(100f, 72f), TextAnchor.MiddleCenter);
            var addText = MakeText(card.transform, "Add\nImage", 22, FontStyle.Normal, SettingsCyan,
                new Vector2(0f, -38f), new Vector2(150f, 72f), TextAnchor.MiddleCenter);
            addText.resizeTextForBestFit = false;
        }

        private void RefreshSupportIdentityText()
        {
            if (supportIdentityText == null)
                return;
            supportIdentityText.text = BuildSupportIdentityBlock();
        }

        private void RefreshSupportAttachmentPreviews()
        {
            int count = supportImagePaths != null ? supportImagePaths.Count : 0;
            int shown = Mathf.Min(2, count);

            for (int i = 0; i < 2; i++)
            {
                bool visible = i < shown && File.Exists(supportImagePaths[i]);
                if (supportPreviewCards[i] != null)
                    supportPreviewCards[i].SetActive(visible);

                if (!visible)
                {
                    ClearSupportPreviewTexture(i);
                    continue;
                }

                string path = supportImagePaths[i];
                if (!string.Equals(supportPreviewLoadedPaths[i], path, StringComparison.Ordinal))
                    LoadSupportPreviewTexture(i, path);
            }

            // Match the reference layout: 2 previews + Add Image, 1 preview + Add Image,
            // or a centered Add Image card when nothing has been selected yet.
            if (count <= 0)
            {
                if (supportAddImageCardRect != null)
                    supportAddImageCardRect.anchoredPosition = new Vector2(0f, -4f);
            }
            else if (count == 1)
            {
                SetSupportPreviewPosition(0, new Vector2(-145f, -4f));
                if (supportAddImageCardRect != null)
                    supportAddImageCardRect.anchoredPosition = new Vector2(145f, -4f);
            }
            else
            {
                SetSupportPreviewPosition(0, new Vector2(-285f, -4f));
                SetSupportPreviewPosition(1, new Vector2(0f, -4f));
                if (supportAddImageCardRect != null)
                    supportAddImageCardRect.anchoredPosition = new Vector2(285f, -4f);
            }

            if (supportAttachmentText != null)
            {
                long totalBytes = 0L;
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        if (File.Exists(supportImagePaths[i]))
                            totalBytes += new FileInfo(supportImagePaths[i]).Length;
                    }
                    catch { }
                }

                supportAttachmentText.text = count == 0
                    ? "No screenshots selected"
                    : count + " image" + (count == 1 ? string.Empty : "s") + " • "
                      + FormatMegabytes(totalBytes) + " MB / 10 MB";
            }
        }

        private void SetSupportPreviewPosition(int index, Vector2 position)
        {
            if (index < 0 || index >= supportPreviewCards.Length || supportPreviewCards[index] == null)
                return;
            var rect = supportPreviewCards[index].GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = position;
        }

        private void LoadSupportPreviewTexture(int index, string path)
        {
            if (index < 0 || index >= supportPreviewImages.Length || supportPreviewImages[index] == null)
                return;

            ClearSupportPreviewTexture(index);
            try
            {
                var bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, false))
                {
                    Destroy(texture);
                    return;
                }

                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                supportPreviewTextures[index] = texture;
                supportPreviewLoadedPaths[index] = path;
                supportPreviewImages[index].texture = texture;
                supportPreviewImages[index].uvRect = CoverUvRect(texture, 250f / 172f);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Support screenshot preview failed: " + exception.Message);
            }
        }

        private void ClearSupportPreviewTexture(int index)
        {
            if (index < 0 || index >= supportPreviewTextures.Length)
                return;
            if (supportPreviewTextures[index] != null)
                Destroy(supportPreviewTextures[index]);
            supportPreviewTextures[index] = null;
            supportPreviewLoadedPaths[index] = string.Empty;
            if (supportPreviewImages[index] != null)
            {
                supportPreviewImages[index].texture = null;
                supportPreviewImages[index].uvRect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        private static Rect CoverUvRect(Texture2D texture, float targetAspect)
        {
            if (texture == null || texture.height <= 0 || targetAspect <= 0f)
                return new Rect(0f, 0f, 1f, 1f);

            float sourceAspect = texture.width / (float)texture.height;
            if (sourceAspect > targetAspect)
            {
                float visibleWidth = targetAspect / sourceAspect;
                return new Rect((1f - visibleWidth) * 0.5f, 0f, visibleWidth, 1f);
            }

            float visibleHeight = sourceAspect / targetAspect;
            return new Rect(0f, (1f - visibleHeight) * 0.5f, 1f, visibleHeight);
        }

        private void RemoveSupportImageAt(int index)
        {
            if (supportImagePaths == null || index < 0 || index >= supportImagePaths.Count)
                return;

            string removed = supportImagePaths[index];
            supportImagePaths.RemoveAt(index);
            try
            {
                if (!string.IsNullOrWhiteSpace(removed) && File.Exists(removed))
                    File.Delete(removed);
            }
            catch { }

            RefreshSupportAttachmentPreviews();
            SetSupportStatus(supportImagePaths.Count > 0 ? "Screenshot removed." : "No screenshots selected.",
                supportImagePaths.Count > 0 ? SettingsCyan : Hex("95A2C0"));
        }

        /// <summary>
        /// Sends the player's typed issue plus the fixed diagnostics block visible in the
        /// description card. The identifiers are regenerated at send time so they cannot be
        /// stale and the player cannot remove them from the outgoing request.
        /// </summary>
        private void SubmitSupportRequestWithIdentity()
        {
            if (supportBodyInput == null)
            {
                SubmitSupportRequest();
                return;
            }

            var subject = supportSubjectInput != null ? supportSubjectInput.text.Trim() : string.Empty;
            var originalBody = supportBodyInput.text ?? string.Empty;

            // Keep the original validation: automatic diagnostics cannot make an empty issue valid.
            if (subject.Length < 3 || originalBody.Trim().Length < 10)
            {
                SubmitSupportRequest();
                return;
            }

            var bodyWithIdentity = originalBody.TrimEnd() + "\n\n" + BuildSupportIdentityBlock();
            supportBodyInput.SetTextWithoutNotify(bodyWithIdentity);
            SubmitSupportRequest(); // reads the body synchronously before its first await

            if (supportBodyInput != null && string.Equals(supportBodyInput.text, bodyWithIdentity, StringComparison.Ordinal))
                supportBodyInput.SetTextWithoutNotify(originalBody);
        }

        private string BuildSupportIdentityBlock()
        {
            var user = auth != null ? auth.CurrentUser : null;
            var playerId = !string.IsNullOrWhiteSpace(bootstrapState.PlayerId)
                ? bootstrapState.PlayerId
                : "Unavailable";
            var linkedEmail = user != null && !string.IsNullOrWhiteSpace(user.Email)
                ? user.Email
                : "Not linked / unavailable";
            var firebaseUid = user != null && !string.IsNullOrWhiteSpace(user.UserId)
                ? user.UserId
                : "Unavailable";

            return "#########\n"
                 + "Player ID: " + playerId + "\n"
                 + "Gmail address linked in: " + linkedEmail + "\n"
                 + "UUID / Firebase UID: " + firebaseUid + "\n"
                 + "Device ID: " + SupportDeviceIdentifier() + "\n"
                 + "#########";
        }

        private static string SupportDeviceIdentifier()
        {
            // Match the project database's privacy-first device_id_hash convention.
            var raw = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrWhiteSpace(raw) || raw == SystemInfo.unsupportedIdentifier)
                raw = SystemInfo.deviceModel + "|" + SystemInfo.operatingSystem;

            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private sealed class SupportPageRuntimeRefresher : MonoBehaviour
        {
            private GravityHalfDeadApp owner;
            private int lastCount = -1;
            private string lastFirst = string.Empty;
            private string lastSecond = string.Empty;

            public void Configure(GravityHalfDeadApp app)
            {
                owner = app;
            }

            private void OnEnable()
            {
                if (owner == null)
                    return;
                owner.RefreshSupportIdentityText();
                owner.RefreshSupportAttachmentPreviews();
                Snapshot();
            }

            private void LateUpdate()
            {
                if (owner == null || owner.supportImagePaths == null)
                    return;

                int count = owner.supportImagePaths.Count;
                string first = count > 0 ? owner.supportImagePaths[0] : string.Empty;
                string second = count > 1 ? owner.supportImagePaths[1] : string.Empty;
                if (count == lastCount && string.Equals(first, lastFirst, StringComparison.Ordinal)
                    && string.Equals(second, lastSecond, StringComparison.Ordinal))
                    return;

                owner.RefreshSupportAttachmentPreviews();
                Snapshot();
            }

            private void Snapshot()
            {
                if (owner == null || owner.supportImagePaths == null)
                    return;
                lastCount = owner.supportImagePaths.Count;
                lastFirst = lastCount > 0 ? owner.supportImagePaths[0] : string.Empty;
                lastSecond = lastCount > 1 ? owner.supportImagePaths[1] : string.Empty;
            }
        }

        private sealed class SupportKeyboardAvoider : MonoBehaviour
        {
            private RectTransform target;
            private InputField subject;
            private InputField body;
            private RectTransform parentRect;
            private Canvas canvas;
            private Vector2 restingPosition;
            private float paddingPixels;
            private float maxLift;
            private float velocity;
            private readonly Vector3[] corners = new Vector3[4];

            public void Configure(RectTransform targetRect, InputField subjectInput,
                InputField bodyInput, float padding, float maximumLift)
            {
                target = targetRect;
                subject = subjectInput;
                body = bodyInput;
                parentRect = target != null ? target.parent as RectTransform : null;
                canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
                paddingPixels = Mathf.Max(0f, padding);
                maxLift = Mathf.Max(0f, maximumLift);
                restingPosition = target != null ? target.anchoredPosition : Vector2.zero;
            }

            private void OnEnable()
            {
                if (target != null)
                    restingPosition = target.anchoredPosition;
                velocity = 0f;
            }

            private void OnDisable()
            {
                if (target != null)
                    target.anchoredPosition = restingPosition;
                velocity = 0f;
            }

            private void LateUpdate()
            {
                if (target == null || parentRect == null)
                    return;

                var active = body != null && body.isFocused ? body
                    : subject != null && subject.isFocused ? subject
                    : null;

                float desiredY = restingPosition.y;
                if (active != null)
                {
                    float keyboardHeight = TouchScreenKeyboard.area.height;
                    if (keyboardHeight <= 1f && Application.isMobilePlatform)
                        keyboardHeight = Screen.height * 0.42f;

                    if (keyboardHeight > Screen.height * 0.12f)
                    {
                        var activeRect = active.transform as RectTransform;
                        if (activeRect != null)
                        {
                            activeRect.GetWorldCorners(corners);
                            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                                ? null
                                : canvas.worldCamera;
                            float fieldBottomPixels = RectTransformUtility.WorldToScreenPoint(camera, corners[0]).y;
                            float overlapPixels = keyboardHeight + paddingPixels - fieldBottomPixels;
                            if (overlapPixels > 0f)
                            {
                                float parentUnitsPerPixel = 1f /
                                    Mathf.Max(0.0001f, canvas != null ? canvas.scaleFactor : 1f);
                                desiredY = restingPosition.y + Mathf.Min(maxLift,
                                    overlapPixels * parentUnitsPerPixel);
                            }
                        }
                    }
                }

                var p = target.anchoredPosition;
                p.y = Mathf.SmoothDamp(p.y, desiredY, ref velocity, 0.10f,
                    Mathf.Infinity, Time.unscaledDeltaTime);
                target.anchoredPosition = p;
            }
        }

        private GameObject CreateSettingsSectionCard(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var card = CreateCard(name, parent, position, size, SettingsCard, 30);
            AddSettingsOutline(card.GetComponent<Image>(), SettingsBorder, 2f);
            return card;
        }

        private static Outline AddSettingsOutline(Image image, Color color, float distance)
        {
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = false;
            return outline;
        }

        private Button MakeProviderButton(Transform parent, string icon, string label, Vector2 position,
            Color iconColor, UnityEngine.Events.UnityAction action, out Text iconText, out Text labelText)
        {
            var card = CreateCard(label, parent, position, new Vector2(390f, 82f), SettingsCardInner, 18);
            var image = card.GetComponent<Image>();
            AddSettingsOutline(image, SettingsBorder, 1f);
            var button = card.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);

            var iconBubble = CreateCard(label + " icon", card.transform, new Vector2(-145f, 0f),
                new Vector2(50f, 50f), iconColor, 25);
            var iconForeground = iconColor.grayscale > 0.75f ? Hex("2756C7") : Color.white;
            iconText = MakeText(iconBubble.transform, icon, 30, FontStyle.Bold, iconForeground,
                Vector2.zero, new Vector2(45f, 45f), TextAnchor.MiddleCenter);
            labelText = MakeText(card.transform, label, 16, FontStyle.Bold, Cream,
                new Vector2(36f, 0f), new Vector2(275f, 42f), TextAnchor.MiddleCenter);
            return button;
        }

        private Button MakeSettingsToggle(Transform parent, string icon, string label, Color iconColor,
            Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var card = CreateCard(label + " setting", parent, position, new Vector2(410f, 96f),
                SettingsCardInner, 20);
            var image = card.GetComponent<Image>();
            AddSettingsOutline(image, SettingsBorder, 1f);
            var button = card.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);

            var iconBubble = CreateCard("Icon", card.transform, new Vector2(-157f, 0f),
                new Vector2(58f, 58f), new Color(iconColor.r, iconColor.g, iconColor.b, 0.18f), 20);
            var iconText = MakeText(iconBubble.transform, icon, 34, FontStyle.Bold, iconColor,
                Vector2.zero, new Vector2(54f, 54f), TextAnchor.MiddleCenter);
            iconText.gameObject.name = "Toggle Icon";

            var labelText = MakeText(card.transform, label, 17, FontStyle.Bold, Cream,
                new Vector2(-12f, 0f), new Vector2(190f, 44f), TextAnchor.MiddleLeft);
            labelText.gameObject.name = "Toggle Label";

            var track = CreateCard("Toggle Track", card.transform, new Vector2(145f, 0f),
                new Vector2(84f, 46f), SettingsGreen, 23);
            track.GetComponent<Image>().raycastTarget = false;
            var knob = CreateCard("Toggle Knob", track.transform, new Vector2(19f, 0f),
                new Vector2(36f, 36f), Color.white, 18);
            knob.GetComponent<Image>().raycastTarget = false;
            return button;
        }

        private void BuildSettingsLinkRow(Transform parent, string icon, string label, Color iconColor,
            Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var row = new GameObject(label + " row");
            row.transform.SetParent(parent, false);
            var rect = row.AddComponent<RectTransform>();
            SetRect(rect, position, new Vector2(850f, 88f));
            var hit = row.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            var button = row.AddComponent<Button>();
            button.targetGraphic = hit;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);

            var iconBubble = CreateCard(label + " icon", row.transform, new Vector2(-360f, 0f),
                new Vector2(62f, 62f), new Color(iconColor.r, iconColor.g, iconColor.b, 0.16f), 31);
            AddSettingsOutline(iconBubble.GetComponent<Image>(), new Color(iconColor.r, iconColor.g, iconColor.b, 0.38f), 1f);
            MakeText(iconBubble.transform, icon, 31, FontStyle.Bold, iconColor,
                Vector2.zero, new Vector2(56f, 56f), TextAnchor.MiddleCenter);
            MakeText(row.transform, label, 18, FontStyle.Bold, Cream,
                new Vector2(-160f, 0f), new Vector2(300f, 48f), TextAnchor.MiddleLeft);
            MakeText(row.transform, "›", 42, FontStyle.Normal, Hex("8790A8"),
                new Vector2(375f, 0f), new Vector2(50f, 60f), TextAnchor.MiddleCenter);
        }

        private InputField MakeInputField(Transform parent, string placeholderText, Vector2 position,
            Vector2 size, bool multiline)
        {
            var fieldObject = new GameObject(placeholderText);
            fieldObject.transform.SetParent(parent, false);
            var rect = fieldObject.AddComponent<RectTransform>();
            SetRect(rect, position, size);
            var background = fieldObject.AddComponent<Image>();
            background.sprite = RoundedSprite(22);
            background.type = Image.Type.Sliced;
            background.color = SettingsCardInner;
            AddSettingsOutline(background, SettingsBorder, 1f);

            var input = fieldObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.characterLimit = multiline ? 2000 : 120;

            var text = MakeText(fieldObject.transform, string.Empty, 18, FontStyle.Normal, Cream,
                Vector2.zero, size - new Vector2(44f, 24f), multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            text.supportRichText = false;
            input.textComponent = text;

            var placeholder = MakeText(fieldObject.transform, placeholderText, 18, FontStyle.Italic, Hex("73809E"),
                Vector2.zero, size - new Vector2(44f, 24f), multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            placeholder.supportRichText = false;
            input.placeholder = placeholder;
            return input;
        }
    }
}
