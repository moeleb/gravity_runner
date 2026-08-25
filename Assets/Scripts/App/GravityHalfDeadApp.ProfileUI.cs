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
        // Dedicated player-profile overlay opened from the small avatar in the home header.
        // The existing ME/character browser remains untouched and is only opened from EDIT.
        private GameObject playerProfileOverlay;
        private RawImage playerProfileAvatarImage;
        private Text playerProfileAvatarFallback;
        private Text playerProfileNameText;
        private RawImage playerProfileCountryFlagImage;
        private Text playerProfileCountryFlagFallback;
        private Text playerProfileCountryCodeText;
        private Text playerProfileCountryText;
        private RawImage playerProfileHeroImage;
        private Text playerProfileHeroNameText;
        private Text playerProfileHighScoreText;
        private Text playerProfileCoinsText;
        private Text playerProfilePlayTimeText;
        private Text playerProfileStatusText;
        private string playerProfileLoadedAvatarUrl = string.Empty;
        private string playerProfileLoadedCountryCode = string.Empty;
        private Texture2D playerProfileCountryTexture;
        private Texture2D playerProfileEnergyPlatformTexture;
        private RectTransform playerProfileCaptureRect;
        private GameObject playerProfileCloseControl;
        private GameObject playerProfileEditControl;
        private GameObject playerProfileShareControl;
        private GameObject playerProfileCaptureSignature;
        private GameObject playerProfileSharePreviewOverlay;
        private RawImage playerProfileSharePreviewImage;
        private AspectRatioFitter playerProfileSharePreviewAspect;
        private Text playerProfileSharePreviewStatusText;
        private Texture2D playerProfileSharePreviewTexture;
        private string playerProfilePendingSharePath = string.Empty;
        private GameObject playerProfileCaptureFlashOverlay;
        private CanvasGroup playerProfileCaptureFlashGroup;
        private bool playerProfileShareInProgress;

        // Dedicated identity editor. Portrait and Frame are intentionally empty until their
        // catalogues are ready; the editor only exposes the currently selected portrait and name.
        private GameObject playerProfileEditorOverlay;
        private RawImage playerProfileEditorAvatarImage;
        private Text playerProfileEditorAvatarFallback;
        private InputField playerProfileEditorNameInput;
        private Button playerProfileEditorNameButton;
        private Text playerProfileEditorNameButtonText;
        private Text playerProfileEditorStatusText;
        private Button playerProfileEditorPortraitTab;
        private Button playerProfileEditorFrameTab;
        private GameObject playerProfileEditorPortraitContent;
        private GameObject playerProfileEditorFrameContent;
        private GameObject playerProfileNamePopup;
        private InputField playerProfileNamePopupInput;
        private Button playerProfileNameContinueButton;
        private Text playerProfileNameContinueText;
        private Text playerProfileNamePopupStatusText;
        private string playerProfileOriginalName = string.Empty;
        private bool playerProfileNameEditing;
        private bool playerProfileNameUpdateInProgress;
        private bool playerProfileNameCooldownRefreshInProgress;
        private long playerProfileNameChangedAtUtcMs;
        private const long PlayerProfileNameCooldownMs = 7L * 24L * 60L * 60L * 1000L;

        private void BuildTopPlayerAvatar(Transform parent)
        {
            var aura = CreateCard("Player profile aura", parent, new Vector2(-376, 817),
                new Vector2(140, 140), new Color(Cyan.r, Cyan.g, Cyan.b, 0.42f), 44);
            var button = aura.AddComponent<Button>();
            button.targetGraphic = aura.GetComponent<Image>();
            button.onClick.AddListener(OpenPlayerProfileOverlay);

            var portraitMask = CreateCard("Player portrait mask", aura.transform, Vector2.zero,
                new Vector2(112, 112), Hex("07182C"), 56);
            portraitMask.GetComponent<Image>().type = Image.Type.Simple;
            portraitMask.AddComponent<Mask>().showMaskGraphic = true;
            topPlayerAvatarImage = MakeTextureImage("Player profile picture", portraitMask.transform, null,
                Vector2.zero, new Vector2(112, 112));

            topPlayerAvatarFallback = new GameObject("No profile picture");
            topPlayerAvatarFallback.transform.SetParent(aura.transform, false);
            var fallbackRect = topPlayerAvatarFallback.AddComponent<RectTransform>();
            SetRect(fallbackRect, Vector2.zero, new Vector2(104, 104));
            CreateImage("Guest head", topPlayerAvatarFallback.transform, Muted,
                Centered(new Vector2(0, 21), new Vector2(38, 38)), RoundedSprite(19));
            CreateImage("Guest shoulders", topPlayerAvatarFallback.transform, Muted,
                Centered(new Vector2(0, -25), new Vector2(72, 40)), RoundedSprite(20));

            topPlayerAvatarFrameImage = MakeTextureImage("Selected player frame", aura.transform,
                Resources.Load<Texture2D>("UI/AvatarFrames/neon_recruit"), Vector2.zero, new Vector2(146, 146));
            topPlayerAvatarFrameImage.raycastTarget = false;
        }


        private void OpenPlayerProfileOverlay()
        {
            if (playerProfileOverlay == null)
                BuildPlayerProfileOverlay();

            RefreshPlayerProfileUI();
            playerProfileOverlay.transform.SetAsLastSibling();
            playerProfileOverlay.SetActive(true);

            // Country and remote avatar data are refreshed asynchronously so the page opens instantly.
            _ = RefreshPlayerProfileCountryAsync();
        }

        private void ClosePlayerProfileOverlay()
        {
            ClosePlayerProfileSharePreview();
            ClosePlayerProfileEditor();
            if (playerProfileOverlay != null)
                playerProfileOverlay.SetActive(false);
        }

        private void OpenPlayerProfileEditor()
        {
            if (playerProfileEditorOverlay == null)
                BuildPlayerProfileEditor();

            playerProfileNameEditing = false;
            playerProfileNameUpdateInProgress = false;
            LoadPlayerProfileNameCooldownFromLocal();
            RefreshPlayerProfileEditorUI();
            ShowPlayerProfileEditorTab(true);
            playerProfileEditorOverlay.transform.SetAsLastSibling();
            playerProfileEditorOverlay.SetActive(true);
            _ = RefreshPlayerProfileNameCooldownAsync();
        }

        private void ClosePlayerProfileEditor()
        {
            ClosePlayerProfileNamePopup();
            playerProfileNameEditing = false;
            playerProfileNameUpdateInProgress = false;
            if (playerProfileEditorOverlay != null)
                playerProfileEditorOverlay.SetActive(false);
        }

        private void BuildPlayerProfileEditor()
        {
            var host = safeRoot != null ? safeRoot : transform as RectTransform;
            playerProfileEditorOverlay = new GameObject("Player Profile Editor Overlay");
            playerProfileEditorOverlay.transform.SetParent(host != null ? host : transform, false);
            var overlayRect = playerProfileEditorOverlay.AddComponent<RectTransform>();
            Stretch(overlayRect);
            var overlayBlocker = playerProfileEditorOverlay.AddComponent<Image>();
            overlayBlocker.color = new Color(0.005f, 0.012f, 0.035f, 0.985f);
            overlayBlocker.raycastTarget = true;

            var cyanGlow = CreateCard("Editor cyan atmosphere", playerProfileEditorOverlay.transform,
                new Vector2(-360f, 420f), new Vector2(520f, 760f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.055f), 180);
            cyanGlow.GetComponent<Image>().raycastTarget = false;
            var purpleGlow = CreateCard("Editor purple atmosphere", playerProfileEditorOverlay.transform,
                new Vector2(370f, -420f), new Vector2(590f, 820f),
                new Color(NeonPurple.r, NeonPurple.g, NeonPurple.b, 0.075f), 200);
            purpleGlow.GetComponent<Image>().raycastTarget = false;

            var panel = CreateCard("Profile Editor Panel", playerProfileEditorOverlay.transform, Vector2.zero,
                new Vector2(930f, 1510f), Hex("060D1D"), 38);
            AddProfileOutline(panel.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.70f), 2f);
            var panelShadow = panel.AddComponent<Shadow>();
            panelShadow.effectColor = new Color(0.05f, 0.68f, 1f, 0.22f);
            panelShadow.effectDistance = new Vector2(0f, -5f);
            panelShadow.useGraphicAlpha = false;

            MakeText(panel.transform, "EDIT PROFILE", 48, FontStyle.Bold, Cream,
                new Vector2(0f, 660f), new Vector2(620f, 72f), TextAnchor.MiddleCenter, 3);
            MakeText(panel.transform, "PILOT IDENTITY", 19, FontStyle.Bold, Cyan,
                new Vector2(0f, 610f), new Vector2(420f, 36f), TextAnchor.MiddleCenter, 2);
            CreateImage("Editor title line", panel.transform, new Color(Cyan.r, Cyan.g, Cyan.b, 0.52f),
                Centered(new Vector2(0f, 578f), new Vector2(410f, 3f)), RoundedSprite(2)).raycastTarget = false;

            var close = MakeButton(panel.transform, "X", new Vector2(390f, 662f),
                new Vector2(72f, 72f), Hex("A51D45"), Cream, 34, ClosePlayerProfileEditor);
            AddProfileOutline(close.GetComponent<Image>(), new Color(1f, 0.23f, 0.47f, 0.90f), 2f);
            close.gameObject.AddComponent<ButtonGlow>();

            // One clean square portrait. There are deliberately no badges or circular accessory slots.
            var portraitGlow = CreateCard("Current Portrait Glow", panel.transform, new Vector2(0f, 390f),
                new Vector2(322f, 322f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.26f), 32);
            AddProfileOutline(portraitGlow.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.92f), 2.5f);
            var portraitFrame = CreateCard("Current Portrait Frame", portraitGlow.transform, Vector2.zero,
                new Vector2(298f, 298f), Hex("102B47"), 26);
            AddProfileOutline(portraitFrame.GetComponent<Image>(), new Color(0.55f, 0.90f, 1f, 0.82f), 1.5f);
            var portraitMask = CreateCard("Current Portrait Mask", portraitFrame.transform, Vector2.zero,
                new Vector2(270f, 270f), Hex("07182C"), 20);
            portraitMask.GetComponent<Image>().type = Image.Type.Simple;
            portraitMask.AddComponent<Mask>().showMaskGraphic = true;
            playerProfileEditorAvatarImage = MakeTextureImage("Selected Avatar", portraitMask.transform, null,
                Vector2.zero, new Vector2(270f, 270f));
            playerProfileEditorAvatarImage.raycastTarget = false;
            playerProfileEditorAvatarFallback = MakeText(portraitMask.transform, "PILOT", 27,
                FontStyle.Bold, Cyan, Vector2.zero, new Vector2(220f, 60f), TextAnchor.MiddleCenter, 2);

            MakeText(panel.transform, "PLAYER NAME", 20, FontStyle.Bold, Muted,
                new Vector2(-255f, 174f), new Vector2(300f, 38f), TextAnchor.MiddleLeft, 2);
            playerProfileEditorNameInput = MakeInputField(panel.transform, "Enter player name",
                new Vector2(-64f, 104f), new Vector2(610f, 92f), false);
            playerProfileEditorNameInput.characterLimit = 20;
            playerProfileEditorNameInput.interactable = false;
            var inputBackground = playerProfileEditorNameInput.GetComponent<Image>();
            if (inputBackground != null)
            {
                inputBackground.color = Hex("0A1930");
                AddProfileOutline(inputBackground, new Color(0.18f, 0.56f, 0.88f, 0.58f), 1.2f);
            }
            if (playerProfileEditorNameInput.textComponent != null)
            {
                playerProfileEditorNameInput.textComponent.fontSize = 26;
                playerProfileEditorNameInput.textComponent.fontStyle = FontStyle.Bold;
            }

            playerProfileEditorNameButton = MakeButton(panel.transform, "EDIT", new Vector2(334f, 104f),
                new Vector2(170f, 92f), Hex("13719A"), Cream, 23, HandlePlayerProfileNameButton);
            AddProfileOutline(playerProfileEditorNameButton.GetComponent<Image>(),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.76f), 1.5f);
            playerProfileEditorNameButton.gameObject.AddComponent<ButtonGlow>();
            playerProfileEditorNameButtonText = playerProfileEditorNameButton.GetComponentInChildren<Text>();

            playerProfileEditorStatusText = MakeText(panel.transform,
                "NAME CHANGES ARE AVAILABLE ONCE EVERY 7 DAYS", 18, FontStyle.Bold, Muted,
                new Vector2(0f, 30f), new Vector2(790f, 42f), TextAnchor.MiddleCenter, 2);

            // Exactly two rectangular categories. No Badges tab and no circular add controls.
            playerProfileEditorPortraitTab = MakeButton(panel.transform, "PORTRAIT", new Vector2(-217f, -83f),
                new Vector2(420f, 92f), Hex("124D75"), Cream, 26,
                () => ShowPlayerProfileEditorTab(true));
            playerProfileEditorFrameTab = MakeButton(panel.transform, "FRAME", new Vector2(217f, -83f),
                new Vector2(420f, 92f), Hex("101B36"), Muted, 26,
                () => ShowPlayerProfileEditorTab(false));

            var contentHost = CreateCard("Empty Profile Options", panel.transform, new Vector2(0f, -380f),
                new Vector2(854f, 470f), Hex("081226"), 30);
            AddProfileOutline(contentHost.GetComponent<Image>(), new Color(0.16f, 0.43f, 0.72f, 0.42f), 1.2f);
            playerProfileEditorPortraitContent = new GameObject("Portrait Options - Empty");
            playerProfileEditorPortraitContent.transform.SetParent(contentHost.transform, false);
            Stretch(playerProfileEditorPortraitContent.AddComponent<RectTransform>());
            playerProfileEditorFrameContent = new GameObject("Frame Options - Empty");
            playerProfileEditorFrameContent.transform.SetParent(contentHost.transform, false);
            Stretch(playerProfileEditorFrameContent.AddComponent<RectTransform>());

            var done = MakeButton(panel.transform, "DONE", new Vector2(0f, -676f),
                new Vector2(520f, 100f), Cyan, Ink, 30, ClosePlayerProfileEditor);
            AddProfileOutline(done.GetComponent<Image>(), new Color(0.58f, 0.94f, 1f, 0.90f), 2f);
            done.gameObject.AddComponent<ButtonGlow>();

            playerProfileEditorOverlay.SetActive(false);
        }

        private void RefreshPlayerProfileEditorUI()
        {
            if (playerProfileEditorNameInput != null)
            {
                playerProfileEditorNameInput.SetTextWithoutNotify(CurrentPlayerProfileDisplayName());
                playerProfileEditorNameInput.interactable = false;
            }

            if (playerProfileEditorNameButtonText != null)
                playerProfileEditorNameButtonText.text = "EDIT";
            if (playerProfileEditorNameButton != null)
                playerProfileEditorNameButton.interactable = true;

            var heroId = string.IsNullOrWhiteSpace(bootstrapState.SelectedCharacter)
                ? "nova"
                : bootstrapState.SelectedCharacter;
            var portrait = Resources.Load<Texture2D>("UI/Avatars/" + heroId);
            var character = portrait != null ? portrait : Resources.Load<Texture2D>("UI/Characters/" + heroId);
            if (playerProfileEditorAvatarImage != null)
            {
                playerProfileEditorAvatarImage.texture = character;
                playerProfileEditorAvatarImage.uvRect = portrait != null
                    ? new Rect(0f, 0f, 1f, 1f)
                    : character != null
                        ? new Rect(0.08f, 0.50f, 0.84f, 0.47f)
                        : new Rect(0f, 0f, 1f, 1f);
                playerProfileEditorAvatarImage.gameObject.SetActive(character != null);
            }
            if (playerProfileEditorAvatarFallback != null)
                playerProfileEditorAvatarFallback.gameObject.SetActive(character == null);

            UpdatePlayerProfileNameCooldownStatus(false);
        }

        private string CurrentPlayerProfileDisplayName()
        {
            var user = auth != null ? auth.CurrentUser : null;
            if (user != null && !string.IsNullOrWhiteSpace(user.DisplayName))
                return user.DisplayName.Trim();
            if (user != null && !string.IsNullOrWhiteSpace(user.UserId))
                return user.UserId;
            return string.IsNullOrWhiteSpace(bootstrapState.PlayerId) ? "GHD-PILOT" : bootstrapState.PlayerId;
        }

        private void ShowPlayerProfileEditorTab(bool portraitSelected)
        {
            if (playerProfileEditorPortraitContent != null)
                playerProfileEditorPortraitContent.SetActive(portraitSelected);
            if (playerProfileEditorFrameContent != null)
                playerProfileEditorFrameContent.SetActive(!portraitSelected);
            SetPlayerProfileEditorTabPalette(playerProfileEditorPortraitTab, portraitSelected);
            SetPlayerProfileEditorTabPalette(playerProfileEditorFrameTab, !portraitSelected);
        }

        private static void SetPlayerProfileEditorTabPalette(Button button, bool selected)
        {
            if (button == null)
                return;
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? Hex("135D88") : Hex("101B36");
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.color = selected ? Cream : Muted;
        }

        private void HandlePlayerProfileNameButton()
        {
            if (playerProfileNameUpdateInProgress)
                return;

            OpenPlayerProfileNamePopup();
        }

        private void BuildPlayerProfileNamePopup()
        {
            playerProfileNamePopup = new GameObject("Change Name Popup");
            playerProfileNamePopup.transform.SetParent(playerProfileEditorOverlay.transform, false);
            var popupRect = playerProfileNamePopup.AddComponent<RectTransform>();
            Stretch(popupRect);
            var dimmer = playerProfileNamePopup.AddComponent<Image>();
            dimmer.color = new Color(0.002f, 0.006f, 0.02f, 0.88f);
            dimmer.raycastTarget = true;

            var cyanAtmosphere = CreateCard("Name Popup Cyan Glow", playerProfileNamePopup.transform,
                new Vector2(-315f, 55f), new Vector2(560f, 650f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.08f), 190);
            cyanAtmosphere.GetComponent<Image>().raycastTarget = false;
            var magentaAtmosphere = CreateCard("Name Popup Magenta Glow", playerProfileNamePopup.transform,
                new Vector2(325f, -80f), new Vector2(520f, 600f),
                new Color(1f, 0.08f, 0.62f, 0.07f), 180);
            magentaAtmosphere.GetComponent<Image>().raycastTarget = false;

            var panel = CreateCard("Change Name Panel", playerProfileNamePopup.transform, Vector2.zero,
                new Vector2(840f, 690f), Hex("071124"), 42);
            AddProfileOutline(panel.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.92f), 2.2f);
            var shadow = panel.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.05f, 0.60f, 1f, 0.34f);
            shadow.effectDistance = new Vector2(0f, -6f);
            shadow.useGraphicAlpha = false;

            CreateImage("Popup Top Rail", panel.transform, Cyan,
                Centered(new Vector2(-125f, 341f), new Vector2(430f, 6f)), RoundedSprite(3)).raycastTarget = false;
            CreateImage("Popup Magenta Rail", panel.transform, Hex("FF2BC2"),
                Centered(new Vector2(250f, 341f), new Vector2(220f, 6f)), RoundedSprite(3)).raycastTarget = false;

            var title = MakeText(panel.transform, "CHANGE NAME", 50, FontStyle.Bold, Cream,
                new Vector2(0f, 245f), new Vector2(600f, 86f), TextAnchor.MiddleCenter, 3);
            title.font = ResolveHomeFont("Bangers", "Luckiest", "Kalam", "Comic");
            var titleOutline = title.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = Hex("173A67");
            titleOutline.effectDistance = new Vector2(3f, -3f);

            var close = MakeButton(panel.transform, "X", new Vector2(370f, 297f),
                new Vector2(82f, 82f), Hex("A51D45"), Cream, 36, ClosePlayerProfileNamePopup);
            AddProfileOutline(close.GetComponent<Image>(), new Color(1f, 0.25f, 0.47f, 0.95f), 2f);
            close.gameObject.AddComponent<ButtonGlow>();

            playerProfileNamePopupInput = MakeInputField(panel.transform, "Type your name",
                new Vector2(0f, 118f), new Vector2(620f, 104f), false);
            playerProfileNamePopupInput.characterLimit = 20;
            playerProfileNamePopupInput.onValueChanged.AddListener(_ => RefreshPlayerProfileNameContinueState());
            var inputImage = playerProfileNamePopupInput.GetComponent<Image>();
            if (inputImage != null)
            {
                inputImage.color = Hex("111B3A");
                AddProfileOutline(inputImage, new Color(0.19f, 0.67f, 1f, 0.85f), 1.8f);
            }
            if (playerProfileNamePopupInput.textComponent != null)
            {
                playerProfileNamePopupInput.textComponent.font = ResolveHomeFont("Kalam", "Comic", "Bangers");
                playerProfileNamePopupInput.textComponent.fontSize = 28;
                playerProfileNamePopupInput.textComponent.fontStyle = FontStyle.Bold;
            }
            var placeholder = playerProfileNamePopupInput.placeholder as Text;
            if (placeholder != null)
            {
                placeholder.text = "Type your name";
                placeholder.font = ResolveHomeFont("Kalam", "Comic", "Bangers");
                placeholder.fontSize = 27;
                placeholder.fontStyle = FontStyle.BoldAndItalic;
            }

            MakeText(panel.transform, "YOU CAN CHANGE YOUR NAME ONCE EVERY 7 DAYS", 22,
                FontStyle.Bold, Cream, new Vector2(0f, 25f), new Vector2(690f, 48f),
                TextAnchor.MiddleCenter, 1);
            MakeText(panel.transform,
                "DISPLAY NAMES ARE PUBLIC AND NOT UNIQUE.\nOTHER PLAYERS MAY USE THE SAME NAME.",
                17, FontStyle.Bold, Hex("78DFFF"), new Vector2(0f, -58f),
                new Vector2(690f, 72f), TextAnchor.MiddleCenter, 1);

            playerProfileNamePopupStatusText = MakeText(panel.transform,
                "ENTER A NAME BETWEEN 2 AND 20 CHARACTERS", 17, FontStyle.Bold, Muted,
                new Vector2(0f, -136f), new Vector2(690f, 44f), TextAnchor.MiddleCenter, 1);

            playerProfileNameContinueButton = MakeButton(panel.transform, "CONTINUE",
                new Vector2(0f, -248f), new Vector2(520f, 104f), Hex("26334B"), Muted, 30,
                () => _ = SavePlayerProfileNameAsync());
            playerProfileNameContinueText = playerProfileNameContinueButton.GetComponentInChildren<Text>();
            if (playerProfileNameContinueText != null)
                playerProfileNameContinueText.font = ResolveHomeFont("Bangers", "Luckiest", "Kalam", "Comic");
            AddProfileOutline(playerProfileNameContinueButton.GetComponent<Image>(),
                new Color(0.33f, 0.48f, 0.65f, 0.62f), 1.6f);
            var colors = playerProfileNameContinueButton.colors;
            colors.disabledColor = Color.white;
            playerProfileNameContinueButton.colors = colors;

            playerProfileNamePopup.SetActive(false);
        }

        private void OpenPlayerProfileNamePopup()
        {
            if (playerProfileNamePopup == null)
                BuildPlayerProfileNamePopup();

            playerProfileOriginalName = CurrentPlayerProfileDisplayName();
            playerProfileNameEditing = true;
            playerProfileNamePopupInput.SetTextWithoutNotify(playerProfileOriginalName);
            playerProfileNamePopup.transform.SetAsLastSibling();
            playerProfileNamePopup.SetActive(true);
            RefreshPlayerProfileNameContinueState();

            if (playerProfileNamePopupInput.interactable)
            {
                playerProfileNamePopupInput.Select();
                playerProfileNamePopupInput.ActivateInputField();
            }
        }

        private void ClosePlayerProfileNamePopup()
        {
            if (playerProfileNameUpdateInProgress)
                return;

            playerProfileNameEditing = false;
            if (playerProfileNamePopupInput != null)
                playerProfileNamePopupInput.DeactivateInputField();
            if (playerProfileNamePopup != null)
                playerProfileNamePopup.SetActive(false);
        }

        private void RefreshPlayerProfileNameContinueState()
        {
            if (playerProfileNamePopupInput == null || playerProfileNameContinueButton == null)
                return;

            var user = auth != null ? auth.CurrentUser : null;
            var remainingMs = PlayerProfileNameCooldownRemainingMs();
            var candidate = playerProfileNamePopupInput.text.Trim();
            var validLength = candidate.Length >= 2 && candidate.Length <= 20;
            var changed = !string.Equals(candidate, playerProfileOriginalName, StringComparison.Ordinal);
            var canEdit = user != null
                && remainingMs <= 0L
                && !playerProfileNameUpdateInProgress
                && !playerProfileNameCooldownRefreshInProgress;
            var canContinue = canEdit && validLength && changed;

            playerProfileNamePopupInput.interactable = canEdit;
            playerProfileNameContinueButton.interactable = canContinue;
            var buttonImage = playerProfileNameContinueButton.GetComponent<Image>();
            if (buttonImage != null)
                buttonImage.color = canContinue ? Cyan : Hex("26334B");
            if (playerProfileNameContinueText != null)
                playerProfileNameContinueText.color = canContinue ? Ink : Muted;

            if (playerProfileNamePopupStatusText == null)
                return;
            if (user == null)
            {
                SetPlayerProfileNamePopupStatus("SIGN IN BEFORE CHANGING YOUR NAME", Hex("FF5B91"));
            }
            else if (playerProfileNameCooldownRefreshInProgress)
            {
                SetPlayerProfileNamePopupStatus("CHECKING NAME CHANGE AVAILABILITY...", Cyan);
            }
            else if (remainingMs > 0L)
            {
                SetPlayerProfileNamePopupStatus("NAME CAN BE CHANGED AGAIN IN "
                    + FormatPlayerProfileNameCooldown(remainingMs), Hex("FF5B91"));
            }
            else if (!validLength)
            {
                SetPlayerProfileNamePopupStatus("NAME MUST BE BETWEEN 2 AND 20 CHARACTERS", Hex("FF5B91"));
            }
            else if (!changed)
            {
                SetPlayerProfileNamePopupStatus("TYPE A DIFFERENT NAME TO CONTINUE", Muted);
            }
            else
            {
                SetPlayerProfileNamePopupStatus("READY TO UPDATE PLAYER NAME", Cyan);
            }
        }

        private void SetPlayerProfileNamePopupStatus(string message, Color color)
        {
            if (playerProfileNamePopupStatusText == null)
                return;
            playerProfileNamePopupStatusText.text = message;
            playerProfileNamePopupStatusText.color = color;
        }

        private async Task SavePlayerProfileNameAsync()
        {
            if (playerProfileNameCooldownRefreshInProgress || PlayerProfileNameCooldownRemainingMs() > 0L)
            {
                RefreshPlayerProfileNameContinueState();
                return;
            }

            var newName = playerProfileNamePopupInput != null
                ? playerProfileNamePopupInput.text.Trim()
                : string.Empty;
            if (newName.Length < 2 || newName.Length > 20)
            {
                SetPlayerProfileNamePopupStatus("NAME MUST BE BETWEEN 2 AND 20 CHARACTERS", Hex("FF5B91"));
                return;
            }

            if (string.Equals(newName, CurrentPlayerProfileDisplayName(), StringComparison.Ordinal))
            {
                RefreshPlayerProfileNameContinueState();
                return;
            }

            var user = auth != null ? auth.CurrentUser : null;
            if (user == null)
            {
                SetPlayerProfileNamePopupStatus("SIGN IN BEFORE CHANGING YOUR NAME", Hex("FF5B91"));
                return;
            }

            playerProfileNameUpdateInProgress = true;
            playerProfileNamePopupInput.interactable = false;
            playerProfileEditorNameButton.interactable = false;
            playerProfileNameContinueButton.interactable = false;
            SetPlayerProfileNamePopupStatus("SAVING PLAYER NAME...", Cyan);

            try
            {
                await user.UpdateUserProfileAsync(new UserProfile { DisplayName = newName });
                playerProfileNameChangedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                PlayerPrefs.SetString(PlayerProfileNameCooldownPrefsKey(user.UserId),
                    playerProfileNameChangedAtUtcMs.ToString());
                PlayerPrefs.Save();

                try
                {
                    if (firestore != null)
                    {
                        await firestore.Collection("users").Document(user.UserId).SetAsync(
                            new Dictionary<string, object>
                            {
                                { "display_name", newName },
                                { "profile_name_changed_at_utc_ms", playerProfileNameChangedAtUtcMs }
                            }, SetOptions.MergeAll);
                    }
                }
                catch (Exception exception)
                {
                    // The Auth profile and local cooldown are already saved. Firestore can retry next session.
                    Debug.LogWarning("Player name cloud metadata could not be synced: " + exception.Message);
                }

                try
                {
                    await user.ReloadAsync();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Player name refresh delayed: " + exception.Message);
                }

                playerProfileNameEditing = false;
                if (playerProfileEditorNameButtonText != null)
                    playerProfileEditorNameButtonText.text = "EDIT";
                if (playerProfileEditorNameInput != null)
                    playerProfileEditorNameInput.SetTextWithoutNotify(newName);
                RefreshPlayerProfileUI();
                UpdatePlayerProfileNameCooldownStatus(false);
                playerProfileNameUpdateInProgress = false;
                ClosePlayerProfileNamePopup();
            }
            catch (Exception exception)
            {
                SetPlayerProfileNamePopupStatus("NAME COULD NOT BE SAVED. TRY AGAIN.", Hex("FF5B91"));
                Debug.LogWarning("Player name update failed: " + exception.Message);
            }
            finally
            {
                playerProfileNameUpdateInProgress = false;
                if (playerProfileEditorNameButton != null)
                    playerProfileEditorNameButton.interactable = true;
                RefreshPlayerProfileNameContinueState();
            }
        }

        private async Task RefreshPlayerProfileNameCooldownAsync()
        {
            playerProfileNameCooldownRefreshInProgress = true;
            var user = auth != null ? auth.CurrentUser : null;
            if (user == null)
            {
                playerProfileNameCooldownRefreshInProgress = false;
                return;
            }

            var userId = user.UserId;
            var latestTimestamp = 0L;
            var localValue = PlayerPrefs.GetString(PlayerProfileNameCooldownPrefsKey(userId), string.Empty);
            if (long.TryParse(localValue, out var localTimestamp))
                latestTimestamp = localTimestamp;

            try
            {
                if (firestore != null)
                {
                    var snapshot = await firestore.Collection("users").Document(userId).GetSnapshotAsync();
                    if (snapshot.Exists)
                    {
                        var values = snapshot.ToDictionary();
                        if (values.TryGetValue("profile_name_changed_at_utc_ms", out var rawTimestamp)
                            && rawTimestamp != null
                            && long.TryParse(rawTimestamp.ToString(), out var cloudTimestamp))
                            latestTimestamp = Math.Max(latestTimestamp, cloudTimestamp);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Player name cooldown could not be refreshed: " + exception.Message);
            }

            if (auth == null || auth.CurrentUser == null || auth.CurrentUser.UserId != userId)
            {
                playerProfileNameCooldownRefreshInProgress = false;
                return;
            }
            playerProfileNameChangedAtUtcMs = latestTimestamp;
            if (latestTimestamp > 0L)
            {
                PlayerPrefs.SetString(PlayerProfileNameCooldownPrefsKey(userId), latestTimestamp.ToString());
                PlayerPrefs.Save();
            }
            playerProfileNameCooldownRefreshInProgress = false;
            UpdatePlayerProfileNameCooldownStatus(false);
            if (playerProfileNamePopup != null && playerProfileNamePopup.activeSelf)
                RefreshPlayerProfileNameContinueState();
        }

        private static string PlayerProfileNameCooldownPrefsKey(string userId)
            => "ghd.profile_name_changed_at_utc_ms." + (string.IsNullOrWhiteSpace(userId) ? "guest" : userId);

        private void LoadPlayerProfileNameCooldownFromLocal()
        {
            var userId = auth != null && auth.CurrentUser != null ? auth.CurrentUser.UserId : "guest";
            var rawValue = PlayerPrefs.GetString(PlayerProfileNameCooldownPrefsKey(userId), string.Empty);
            playerProfileNameChangedAtUtcMs = long.TryParse(rawValue, out var timestamp) ? timestamp : 0L;
        }

        private long PlayerProfileNameCooldownRemainingMs()
        {
            if (playerProfileNameChangedAtUtcMs <= 0L)
                return 0L;
            var unlockAt = playerProfileNameChangedAtUtcMs + PlayerProfileNameCooldownMs;
            return Math.Max(0L, unlockAt - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        private void UpdatePlayerProfileNameCooldownStatus(bool attemptedEdit)
        {
            var remainingMs = PlayerProfileNameCooldownRemainingMs();
            if (remainingMs <= 0L)
            {
                SetPlayerProfileEditorStatus("NAME CHANGES ARE AVAILABLE ONCE EVERY 7 DAYS", Muted);
                return;
            }

            var timeText = FormatPlayerProfileNameCooldown(remainingMs);
            SetPlayerProfileEditorStatus(
                attemptedEdit
                    ? "NAME CANNOT BE EDITED YET - TRY AGAIN IN " + timeText
                    : "NAME CAN BE CHANGED AGAIN IN " + timeText,
                attemptedEdit ? Hex("FF5B91") : Muted);
        }

        private static string FormatPlayerProfileNameCooldown(long remainingMs)
        {
            var remaining = TimeSpan.FromMilliseconds(Math.Max(0L, remainingMs));
            return remaining.Days > 0
                ? remaining.Days + "D " + remaining.Hours + "H"
                : Math.Max(1, remaining.Hours) + "H " + remaining.Minutes + "M";
        }

        private void SetPlayerProfileEditorStatus(string message, Color color)
        {
            if (playerProfileEditorStatusText == null)
                return;
            playerProfileEditorStatusText.text = message;
            playerProfileEditorStatusText.color = color;
        }

        private void BuildPlayerProfileOverlay()
        {
            var host = safeRoot != null ? safeRoot : transform as RectTransform;
            playerProfileOverlay = new GameObject("Player Profile Overlay");
            playerProfileOverlay.transform.SetParent(host != null ? host : transform, false);
            var overlayRect = playerProfileOverlay.AddComponent<RectTransform>();
            Stretch(overlayRect);

            // Reference screen is almost black outside the profile card. Keep the rest of the game hidden
            // so the profile reads like one focused premium screen.
            var blocker = playerProfileOverlay.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.965f);
            blocker.raycastTarget = true;

            // Tall, narrow neon panel matching the supplied reference proportions.
            var panel = new GameObject("Profile Main Panel");
            panel.transform.SetParent(playerProfileOverlay.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.035f, 0.11f);
            panelRect.anchorMax = new Vector2(0.965f, 0.90f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            playerProfileCaptureRect = panelRect;
            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = RoundedSprite(38);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Hex("030A18");
            AddProfileOutline(panelImage, new Color(0.16f, 0.63f, 1f, 0.74f), 1.7f);
            var panelGlow = panel.AddComponent<Shadow>();
            panelGlow.effectColor = new Color(0.04f, 0.47f, 1f, 0.26f);
            panelGlow.effectDistance = new Vector2(0f, -3f);
            panelGlow.useGraphicAlpha = false;

            // Header.
            MakeText(panel.transform, "PLAYER PROFILE", 46, FontStyle.Bold, Cream,
                new Vector2(0f, 635f), new Vector2(650f, 72f), TextAnchor.MiddleCenter, 4);
            CreateImage("Profile title line", panel.transform, new Color(Cyan.r, Cyan.g, Cyan.b, 0.56f),
                Centered(new Vector2(0f, 590f), new Vector2(340f, 3f)), RoundedSprite(2)).raycastTarget = false;
            BuildProfileHeaderMark(panel.transform, new Vector2(0f, 578f));

            // Keep the close control fully inset from the outer frame on every aspect ratio.
            var close = MakeButton(panel.transform, "X", new Vector2(396f, 635f),
                new Vector2(64f, 64f), Hex("9E173F"), Cream, 32, ClosePlayerProfileOverlay);
            AddProfileOutline(close.GetComponent<Image>(), new Color(1f, 0.22f, 0.45f, 0.88f), 2f);
            close.gameObject.AddComponent<ButtonGlow>();
            playerProfileCloseControl = close.gameObject;

            // Identity card: square avatar, UID/name, country row, edit directly below avatar.
            var identity = CreateCard("Profile Identity", panel.transform, new Vector2(0f, 370f),
                new Vector2(880f, 330f), Hex("061225"), 28);
            AddProfileOutline(identity.GetComponent<Image>(), new Color(0.12f, 0.44f, 0.82f, 0.46f), 1.25f);

            var avatarOuterGlow = CreateCard("Avatar outer glow", identity.transform, new Vector2(-318f, 22f),
                new Vector2(214f, 214f), new Color(0.04f, 0.34f, 0.68f, 0.68f), 28);
            AddProfileOutline(avatarOuterGlow.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.82f), 2f);
            var avatarFrame = CreateCard("Square Avatar Frame", avatarOuterGlow.transform, Vector2.zero,
                new Vector2(198f, 198f), Hex("0D284B"), 24);
            AddProfileOutline(avatarFrame.GetComponent<Image>(), new Color(0.55f, 0.86f, 1f, 0.86f), 1.5f);
            var avatarMask = CreateCard("Square Avatar Mask", avatarFrame.transform, Vector2.zero,
                new Vector2(176f, 176f), Hex("07182C"), 20);
            avatarMask.GetComponent<Image>().type = Image.Type.Simple;
            avatarMask.AddComponent<Mask>().showMaskGraphic = true;
            playerProfileAvatarImage = MakeTextureImage("Profile Avatar", avatarMask.transform, null,
                Vector2.zero, new Vector2(176f, 176f));
            playerProfileAvatarImage.raycastTarget = false;
            playerProfileAvatarFallback = MakeText(avatarMask.transform, "PILOT", 24, FontStyle.Bold, Cyan,
                Vector2.zero, new Vector2(154f, 56f), TextAnchor.MiddleCenter, 2);

            playerProfileNameText = MakeText(identity.transform, "PILOT", 36, FontStyle.Bold, Cream,
                new Vector2(92f, 61f), new Vector2(530f, 58f), TextAnchor.MiddleLeft, 2);
            playerProfileNameText.resizeTextForBestFit = true;
            playerProfileNameText.resizeTextMinSize = 17;
            playerProfileNameText.resizeTextMaxSize = 36;

            var countryPill = CreateCard("Profile Country", identity.transform, new Vector2(91f, -10f),
                new Vector2(540f, 66f), Hex("07152B"), 18);
            AddProfileOutline(countryPill.GetComponent<Image>(), new Color(0.14f, 0.40f, 0.72f, 0.38f), 1f);

            // Keep the flag inside a dedicated clipped holder. An AspectRatioFitter attached directly
            // to the full country pill expands against the pill and causes the flag to cover the text.
            var countryFlagHolder = CreateCard("Country Flag Holder", countryPill.transform,
                new Vector2(-226f, 0f), new Vector2(58f, 40f), Hex("04101F"), 8);
            countryFlagHolder.GetComponent<Image>().raycastTarget = false;
            countryFlagHolder.AddComponent<RectMask2D>();

            var countryFlagObject = new GameObject("Country Flag");
            countryFlagObject.transform.SetParent(countryFlagHolder.transform, false);
            var countryFlagRect = countryFlagObject.AddComponent<RectTransform>();
            SetRect(countryFlagRect, Vector2.zero, new Vector2(54f, 36f));
            playerProfileCountryFlagImage = countryFlagObject.AddComponent<RawImage>();
            playerProfileCountryFlagImage.raycastTarget = false;
            playerProfileCountryFlagImage.color = Color.white;
            var countryAspect = countryFlagObject.AddComponent<AspectRatioFitter>();
            countryAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            countryAspect.aspectRatio = CountryFlagPixelWidth / (float)CountryFlagPixelHeight;

            playerProfileCountryFlagFallback = MakeText(countryFlagHolder.transform, "--", 16,
                FontStyle.Bold, Cyan, Vector2.zero, new Vector2(52f, 34f), TextAnchor.MiddleCenter, 1);
            playerProfileCountryCodeText = MakeText(countryPill.transform, "--", 17, FontStyle.Bold, Cyan,
                new Vector2(-166f, 0f), new Vector2(50f, 38f), TextAnchor.MiddleCenter, 2);
            CreateImage("Country divider", countryPill.transform, new Color(0.32f, 0.45f, 0.67f, 0.44f),
                Centered(new Vector2(-130f, 0f), new Vector2(2f, 42f)), RoundedSprite(1)).raycastTarget = false;
            playerProfileCountryText = MakeText(countryPill.transform, "COUNTRY", 22, FontStyle.Bold, Cream,
                new Vector2(32f, 0f), new Vector2(300f, 44f), TextAnchor.MiddleLeft, 2);
            playerProfileCountryText.resizeTextForBestFit = true;
            playerProfileCountryText.resizeTextMinSize = 13;
            playerProfileCountryText.resizeTextMaxSize = 22;
            BuildProfileGlobeIcon(countryPill.transform, new Vector2(230f, 0f));

            // Compact icon-only edit control, like a profile-photo edit badge.
            var edit = MakeButton(identity.transform, string.Empty, new Vector2(-226f, -78f),
                new Vector2(64f, 64f), Hex("155A83"), Cream, 20, OpenPlayerProfileEditor);
            AddProfileOutline(edit.GetComponent<Image>(), new Color(0.24f, 0.76f, 1f, 0.72f), 1.4f);
            edit.gameObject.AddComponent<ButtonGlow>();
            BuildProfileEditPencilIcon(edit.transform);
            playerProfileEditControl = edit.gameObject;

            // Main showcase. Character takes the left half; three glowing stat cards stack on the right.
            var showcase = CreateCard("Profile Showcase", panel.transform, new Vector2(0f, -160f),
                new Vector2(880f, 680f), Hex("061225"), 28);
            AddProfileOutline(showcase.GetComponent<Image>(), new Color(0.12f, 0.42f, 0.78f, 0.42f), 1.25f);

            // Perspective energy circle matching the reference: concentric luminous rings, radial spokes,
            // and a bright front rim instead of the old capsule / hoverboard shape.
            BuildProfileEnergyPlatform(showcase.transform, new Vector2(-235f, -270f));

            var heroStage = new GameObject("Profile Hero Stage");
            heroStage.transform.SetParent(showcase.transform, false);
            var heroStageRect = heroStage.AddComponent<RectTransform>();
            // The bottom of this rect is exactly the platform centre, keeping every selected hero grounded.
            SetRect(heroStageRect, new Vector2(-235f, 15f), new Vector2(470f, 570f));
            playerProfileHeroImage = MakeTextureImage("Firebase Selected Hero", heroStage.transform, null,
                Vector2.zero, new Vector2(470f, 570f));
            playerProfileHeroImage.raycastTarget = false;
            var heroAspect = playerProfileHeroImage.GetComponent<AspectRatioFitter>();
            if (heroAspect != null)
                heroAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            // Only the front half is drawn over the boots. The rest of the power circle stays behind the hero.
            BuildProfileEnergyPlatformFrontRim(showcase.transform, new Vector2(-235f, -294f));

            // Decorative light points around the stage like the supplied mock-up.
            var stageDotA = CreateCard("Hero stage sparkle A", showcase.transform, new Vector2(-410f, 230f),
                new Vector2(5f, 5f), new Color(0.35f, 0.83f, 1f, 0.70f), 3);
            stageDotA.GetComponent<Image>().raycastTarget = false;
            var stageDotB = CreateCard("Hero stage sparkle B", showcase.transform, new Vector2(-88f, 175f),
                new Vector2(4f, 4f), new Color(0.40f, 0.69f, 1f, 0.52f), 2);
            stageDotB.GetComponent<Image>().raycastTarget = false;

            playerProfileHighScoreText = BuildProfileStatCard(showcase.transform, "trophy", "HIGH SCORE",
                new Vector2(235f, 178f));
            playerProfileCoinsText = BuildProfileStatCard(showcase.transform, "coin", "COINS COLLECTED",
                new Vector2(235f, 0f));
            playerProfilePlayTimeText = BuildProfileStatCard(showcase.transform, "clock", "TIME PLAYED",
                new Vector2(235f, -178f));

            // Bottom status panel doubles as the share action, matching the reference instead of adding
            // a separate bulky SHARE PROFILE button.
            var shareBanner = CreateCard("Profile Share Status", panel.transform, new Vector2(0f, -586f),
                new Vector2(880f, 126f), Hex("07152B"), 24);
            AddProfileOutline(shareBanner.GetComponent<Image>(), new Color(0.11f, 0.39f, 0.73f, 0.38f), 1f);
            var shareButton = shareBanner.AddComponent<Button>();
            shareButton.targetGraphic = shareBanner.GetComponent<Image>();
            shareButton.navigation = new Navigation { mode = Navigation.Mode.None };
            shareButton.onClick.AddListener(SharePlayerProfile);
            playerProfileShareControl = shareBanner;

            var leftStar = CreateCard("Share sparkle left", shareBanner.transform, new Vector2(-228f, 18f),
                new Vector2(14f, 14f), Cyan, 2);
            leftStar.transform.localEulerAngles = new Vector3(0f, 0f, 45f);
            leftStar.GetComponent<Image>().raycastTarget = false;
            var rightStar = CreateCard("Share sparkle right", shareBanner.transform, new Vector2(228f, 18f),
                new Vector2(14f, 14f), Cyan, 2);
            rightStar.transform.localEulerAngles = new Vector3(0f, 0f, 45f);
            rightStar.GetComponent<Image>().raycastTarget = false;

            playerProfileStatusText = MakeText(shareBanner.transform, "SHARE PROFILE", 24, FontStyle.Bold,
                Hex("8DFF79"), new Vector2(0f, 20f), new Vector2(540f, 38f), TextAnchor.MiddleCenter, 2);
            MakeText(shareBanner.transform, "Save the player card and open sharing", 18, FontStyle.Normal,
                Hex("A7B2C8"), new Vector2(0f, -23f), new Vector2(620f, 32f), TextAnchor.MiddleCenter);

            MakeText(panel.transform, "Gravity: Half Dead", 17, FontStyle.Normal, Hex("66718C"),
                new Vector2(0f, -686f), new Vector2(600f, 34f), TextAnchor.MiddleCenter);

            // Capture-only signature. It replaces the interactive controls in the image that gets shared,
            // so the exported card looks like a clean collectible profile card rather than a screenshot of UI.
            playerProfileCaptureSignature = new GameObject("Profile Capture Signature");
            playerProfileCaptureSignature.transform.SetParent(panel.transform, false);
            var signatureRect = playerProfileCaptureSignature.AddComponent<RectTransform>();
            SetRect(signatureRect, new Vector2(0f, -612f), new Vector2(760f, 132f));
            var signatureLine = CreateImage("Profile signature line", playerProfileCaptureSignature.transform,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.42f),
                Centered(new Vector2(0f, 46f), new Vector2(380f, 2f)), RoundedSprite(1));
            signatureLine.raycastTarget = false;
            var signatureTop = MakeText(playerProfileCaptureSignature.transform, "GRAVITY:", 31,
                FontStyle.BoldAndItalic, Cream, new Vector2(0f, 15f), new Vector2(520f, 44f),
                TextAnchor.MiddleCenter, 2);
            signatureTop.font = handwritingFont != null ? handwritingFont : font;
            var signatureBottom = MakeText(playerProfileCaptureSignature.transform, "HALF DEAD", 24,
                FontStyle.BoldAndItalic, Hex("FF64DF"), new Vector2(0f, -23f), new Vector2(420f, 38f),
                TextAnchor.MiddleCenter, 2);
            signatureBottom.font = handwritingFont != null ? handwritingFont : font;
            MakeText(playerProfileCaptureSignature.transform, "PLAYER CARD", 13, FontStyle.Bold, Hex("6C87A8"),
                new Vector2(0f, -51f), new Vector2(300f, 24f), TextAnchor.MiddleCenter, 1).raycastTarget = false;
            playerProfileCaptureSignature.SetActive(false);

            playerProfileOverlay.SetActive(false);
        }

        private void BuildPlayerProfileSharePreview()
        {
            var host = safeRoot != null ? safeRoot : transform as RectTransform;
            playerProfileSharePreviewOverlay = new GameObject("Player Profile Share Preview");
            playerProfileSharePreviewOverlay.transform.SetParent(host != null ? host : transform, false);
            var overlayRect = playerProfileSharePreviewOverlay.AddComponent<RectTransform>();
            Stretch(overlayRect);
            var backdrop = playerProfileSharePreviewOverlay.AddComponent<Image>();
            backdrop.color = Hex("030B19");
            backdrop.raycastTarget = true;

            MakeText(playerProfileSharePreviewOverlay.transform, "✦  SHARE YOUR PROFILE!  ✦", 43,
                FontStyle.Bold, Cream, new Vector2(0f, 820f), new Vector2(800f, 72f),
                TextAnchor.MiddleCenter, 3);
            MakeText(playerProfileSharePreviewOverlay.transform, "Your stats. Your journey. Your story.", 21,
                FontStyle.Normal, Hex("A7B2C8"), new Vector2(0f, 760f), new Vector2(720f, 42f),
                TextAnchor.MiddleCenter);

            var close = MakeButton(playerProfileSharePreviewOverlay.transform, "X", new Vector2(447f, 826f),
                new Vector2(78f, 78f), Hex("9E173F"), Cream, 38, ClosePlayerProfileSharePreview);
            AddProfileOutline(close.GetComponent<Image>(), new Color(1f, 0.52f, 0.60f, 0.92f), 2f);
            close.gameObject.AddComponent<ButtonGlow>();

            // Polaroid-style frame from the supplied reference. The generated profile card is shown here
            // before Android sharing is allowed to open.
            var polaroid = CreateCard("Share preview polaroid", playerProfileSharePreviewOverlay.transform,
                new Vector2(0f, 48f), new Vector2(820f, 1330f), Hex("F5F4F0"), 20);
            polaroid.transform.localEulerAngles = new Vector3(0f, 0f, 2.2f);
            var polaroidShadow = polaroid.AddComponent<Shadow>();
            polaroidShadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            polaroidShadow.effectDistance = new Vector2(12f, -16f);
            polaroidShadow.useGraphicAlpha = false;

            var previewViewport = CreateCard("Share preview image viewport", polaroid.transform,
                new Vector2(0f, 0f), new Vector2(758f, 1268f), Hex("020814"), 10);
            playerProfileSharePreviewImage = MakeTextureImage("Generated player profile card",
                previewViewport.transform, null, Vector2.zero, new Vector2(746f, 1256f));
            playerProfileSharePreviewImage.raycastTarget = false;
            playerProfileSharePreviewAspect = playerProfileSharePreviewImage.gameObject.AddComponent<AspectRatioFitter>();
            playerProfileSharePreviewAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            playerProfileSharePreviewAspect.aspectRatio = 9f / 16f;

            var share = MakeButton(playerProfileSharePreviewOverlay.transform, "SHARE", new Vector2(0f, -735f),
                new Vector2(540f, 108f), Hex("65913F"), Color.white, 36, ConfirmPlayerProfileShare);
            AddProfileOutline(share.GetComponent<Image>(), new Color(0.84f, 0.92f, 0.66f, 0.92f), 2f);
            share.gameObject.AddComponent<ButtonGlow>();
            BuildProfileShareIcon(share.transform, new Vector2(-142f, 0f));

            playerProfileSharePreviewStatusText = MakeText(playerProfileSharePreviewOverlay.transform,
                "Your screenshot will be saved to your gallery", 18, FontStyle.Bold, Hex("8C97AC"),
                new Vector2(0f, -815f), new Vector2(760f, 36f), TextAnchor.MiddleCenter, 1);

            playerProfileSharePreviewOverlay.SetActive(false);
        }

        private void BuildProfileShareIcon(Transform parent, Vector2 position)
        {
            var top = CreateCard("Share icon top", parent, position + new Vector2(18f, 17f),
                new Vector2(15f, 15f), Color.white, 8);
            var middle = CreateCard("Share icon middle", parent, position + new Vector2(-18f, 0f),
                new Vector2(15f, 15f), Color.white, 8);
            var bottom = CreateCard("Share icon bottom", parent, position + new Vector2(18f, -17f),
                new Vector2(15f, 15f), Color.white, 8);
            top.GetComponent<Image>().raycastTarget = false;
            middle.GetComponent<Image>().raycastTarget = false;
            bottom.GetComponent<Image>().raycastTarget = false;

            var upperLine = CreateImage("Share icon upper link", parent, Color.white,
                Centered(position + new Vector2(0f, 9f), new Vector2(37f, 5f)), RoundedSprite(3));
            upperLine.transform.localEulerAngles = new Vector3(0f, 0f, 25f);
            upperLine.raycastTarget = false;
            var lowerLine = CreateImage("Share icon lower link", parent, Color.white,
                Centered(position + new Vector2(0f, -9f), new Vector2(37f, 5f)), RoundedSprite(3));
            lowerLine.transform.localEulerAngles = new Vector3(0f, 0f, -25f);
            lowerLine.raycastTarget = false;
        }

        private void BuildPlayerProfileCaptureFlash()
        {
            var host = safeRoot != null ? safeRoot : transform as RectTransform;
            playerProfileCaptureFlashOverlay = new GameObject("Profile Camera Capture Flash");
            playerProfileCaptureFlashOverlay.transform.SetParent(host != null ? host : transform, false);
            var flashRect = playerProfileCaptureFlashOverlay.AddComponent<RectTransform>();
            Stretch(flashRect);
            var flashImage = playerProfileCaptureFlashOverlay.AddComponent<Image>();
            flashImage.color = Color.white;
            flashImage.raycastTarget = false;
            playerProfileCaptureFlashGroup = playerProfileCaptureFlashOverlay.AddComponent<CanvasGroup>();
            playerProfileCaptureFlashGroup.alpha = 0f;
            playerProfileCaptureFlashGroup.blocksRaycasts = false;
            playerProfileCaptureFlashGroup.interactable = false;
            playerProfileCaptureFlashOverlay.SetActive(false);
        }

        private IEnumerator PlayPlayerProfileCaptureAnimation()
        {
            if (playerProfileCaptureFlashOverlay == null)
                BuildPlayerProfileCaptureFlash();

            playerProfileCaptureFlashOverlay.transform.SetAsLastSibling();
            playerProfileCaptureFlashOverlay.SetActive(true);
            playerProfileCaptureFlashGroup.alpha = 0f;

            const float flashInDuration = 0.055f;
            var elapsed = 0f;
            while (elapsed < flashInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                playerProfileCaptureFlashGroup.alpha = Mathf.Clamp01(elapsed / flashInDuration);
                yield return null;
            }

            playerProfileCaptureFlashGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(0.075f);

            const float flashOutDuration = 0.24f;
            elapsed = 0f;
            while (elapsed < flashOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / flashOutDuration);
                playerProfileCaptureFlashGroup.alpha = 1f - progress * progress;
                yield return null;
            }

            playerProfileCaptureFlashGroup.alpha = 0f;
            playerProfileCaptureFlashOverlay.SetActive(false);
        }

        private void BuildProfileEditPencilIcon(Transform parent)
        {
            var iconRoot = new GameObject("Edit pencil icon");
            iconRoot.transform.SetParent(parent, false);
            var iconRect = iconRoot.AddComponent<RectTransform>();
            SetRect(iconRect, Vector2.zero, new Vector2(34f, 34f));
            iconRoot.transform.localEulerAngles = new Vector3(0f, 0f, -45f);

            var body = CreateCard("Pencil body", iconRoot.transform, Vector2.zero, new Vector2(28f, 8f),
                new Color(0.78f, 0.94f, 1f, 1f), 4);
            body.GetComponent<Image>().raycastTarget = false;
            var eraser = CreateCard("Pencil eraser", iconRoot.transform, new Vector2(-16f, 0f),
                new Vector2(6f, 8f), Hex("55BFEF"), 3);
            eraser.GetComponent<Image>().raycastTarget = false;
            var tip = CreateCard("Pencil tip", iconRoot.transform, new Vector2(16f, 0f),
                new Vector2(7f, 7f), Cream, 2);
            tip.transform.localEulerAngles = new Vector3(0f, 0f, 45f);
            tip.GetComponent<Image>().raycastTarget = false;
        }


        private void BuildProfileEnergyPlatform(Transform parent, Vector2 position)
        {
            if (playerProfileEnergyPlatformTexture == null)
                playerProfileEnergyPlatformTexture = CreateProfileEnergyPlatformTexture();

            // Soft halo behind the actual disc.
            var haloObject = new GameObject("Hero energy platform halo");
            haloObject.transform.SetParent(parent, false);
            var haloRect = haloObject.AddComponent<RectTransform>();
            SetRect(haloRect, position + new Vector2(0f, -2f), new Vector2(450f, 132f));
            var halo = haloObject.AddComponent<RawImage>();
            halo.texture = playerProfileEnergyPlatformTexture;
            halo.color = new Color(0.35f, 0.75f, 1f, 0.30f);
            halo.raycastTarget = false;

            // Crisp power-circle artwork. It is generated at runtime so there is no extra asset to import.
            var platformObject = new GameObject("Hero energy power circle");
            platformObject.transform.SetParent(parent, false);
            var platformRect = platformObject.AddComponent<RectTransform>();
            SetRect(platformRect, position, new Vector2(420f, 122f));
            var platform = platformObject.AddComponent<RawImage>();
            platform.texture = playerProfileEnergyPlatformTexture;
            platform.color = Color.white;
            platform.raycastTarget = false;
        }

        private void BuildProfileEnergyPlatformFrontRim(Transform parent, Vector2 position)
        {
            if (playerProfileEnergyPlatformTexture == null)
                playerProfileEnergyPlatformTexture = CreateProfileEnergyPlatformTexture();

            var rimObject = new GameObject("Hero energy platform front rim");
            rimObject.transform.SetParent(parent, false);
            var rimRect = rimObject.AddComponent<RectTransform>();
            SetRect(rimRect, position, new Vector2(420f, 58f));
            var rim = rimObject.AddComponent<RawImage>();
            rim.texture = playerProfileEnergyPlatformTexture;
            rim.uvRect = new Rect(0f, 0f, 1f, 0.48f);
            rim.color = Color.white;
            rim.raycastTarget = false;
        }

        private Texture2D CreateProfileEnergyPlatformTexture()
        {
            const int width = 512;
            const int height = 180;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Profile Energy Platform",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                var ny = ((y + 0.5f) - height * 0.52f) / (height * 0.5f);
                var ey = ny * 3.55f;
                for (var x = 0; x < width; x++)
                {
                    var nx = ((x + 0.5f) - width * 0.5f) / (width * 0.5f);
                    var radius = Mathf.Sqrt(nx * nx + ey * ey);
                    if (radius > 1.12f)
                        continue;

                    var outer = Mathf.Exp(-Mathf.Pow((radius - 0.93f) / 0.027f, 2f));
                    var ring2 = Mathf.Exp(-Mathf.Pow((radius - 0.74f) / 0.020f, 2f));
                    var ring3 = Mathf.Exp(-Mathf.Pow((radius - 0.55f) / 0.018f, 2f));
                    var ring4 = Mathf.Exp(-Mathf.Pow((radius - 0.36f) / 0.016f, 2f));
                    var center = Mathf.Exp(-radius * radius * 5.2f);

                    var angle = Mathf.Atan2(ey, nx);
                    var spokes = radius < 0.91f
                        ? Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 10f)), 24f) * Mathf.Clamp01(1f - radius) * 0.70f
                        : 0f;

                    // Brighter lower/front rim gives the reference platform its 3D thickness.
                    var frontMask = Mathf.Clamp01((-ny + 0.12f) * 2.4f);
                    var frontRim = Mathf.Exp(-Mathf.Pow((radius - 0.985f) / 0.040f, 2f)) * frontMask;
                    var innerFill = radius < 0.92f ? Mathf.Clamp01((0.94f - radius) * 0.42f) : 0f;

                    var glow = Mathf.Clamp01(
                        outer * 0.95f + ring2 * 0.70f + ring3 * 0.55f + ring4 * 0.45f
                        + frontRim * 0.95f + spokes * 0.62f + center * 0.28f + innerFill * 0.20f);
                    var alpha = Mathf.Clamp01(glow + outer * 0.16f + frontRim * 0.22f);
                    if (alpha <= 0.004f)
                        continue;

                    var hot = Mathf.Clamp01(outer + frontRim + ring2 * 0.55f + spokes * 0.38f);
                    var r = Mathf.Lerp(0.015f, 0.24f, hot);
                    var g = Mathf.Lerp(0.20f, 0.88f, hot);
                    var b = Mathf.Lerp(0.52f, 1.00f, Mathf.Clamp01(hot + center * 0.35f));
                    pixels[y * width + x] = new Color32(
                        (byte)(Mathf.Clamp01(r) * 255f),
                        (byte)(Mathf.Clamp01(g) * 255f),
                        (byte)(Mathf.Clamp01(b) * 255f),
                        (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private void BuildProfileHeaderMark(Transform parent, Vector2 position)
        {
            var left = CreateImage("Profile header wing left", parent, new Color(Cyan.r, Cyan.g, Cyan.b, 0.46f),
                Centered(position + new Vector2(-39f, 0f), new Vector2(58f, 3f)), RoundedSprite(2));
            left.raycastTarget = false;
            var right = CreateImage("Profile header wing right", parent, new Color(Cyan.r, Cyan.g, Cyan.b, 0.46f),
                Centered(position + new Vector2(39f, 0f), new Vector2(58f, 3f)), RoundedSprite(2));
            right.raycastTarget = false;
            var diamond = CreateCard("Profile header diamond", parent, position, new Vector2(19f, 19f),
                new Color(0.03f, 0.30f, 0.64f, 1f), 2);
            diamond.transform.localEulerAngles = new Vector3(0f, 0f, 45f);
            AddProfileOutline(diamond.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.58f), 1f);
            diamond.GetComponent<Image>().raycastTarget = false;
        }

        private void BuildProfileGlobeIcon(Transform parent, Vector2 position)
        {
            var globe = CreateCard("Profile globe", parent, position, new Vector2(42f, 42f), Hex("0B1C38"), 21);
            AddProfileOutline(globe.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.74f), 1.2f);
            globe.GetComponent<Image>().raycastTarget = false;
            CreateImage("Globe horizontal", globe.transform, new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f),
                Centered(Vector2.zero, new Vector2(28f, 2f)), RoundedSprite(1)).raycastTarget = false;
            CreateImage("Globe vertical", globe.transform, new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f),
                Centered(Vector2.zero, new Vector2(2f, 28f)), RoundedSprite(1)).raycastTarget = false;
            var equatorTop = CreateCard("Globe band top", globe.transform, new Vector2(0f, 8f),
                new Vector2(22f, 2f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.52f), 1);
            equatorTop.GetComponent<Image>().raycastTarget = false;
            var equatorBottom = CreateCard("Globe band bottom", globe.transform, new Vector2(0f, -8f),
                new Vector2(22f, 2f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.52f), 1);
            equatorBottom.GetComponent<Image>().raycastTarget = false;
        }

        private Text BuildProfileStatCard(Transform parent, string iconKind, string label, Vector2 position)
        {
            var glow = CreateCard("Profile stat glow " + label, parent, position + new Vector2(5f, -5f),
                new Vector2(370f, 148f), new Color(0.25f, 0.12f, 0.62f, 0.26f), 24);
            glow.GetComponent<Image>().raycastTarget = false;

            var card = CreateCard("Profile stat " + label, parent, position, new Vector2(370f, 148f),
                Hex("0A1733"), 24);
            AddProfileOutline(card.GetComponent<Image>(), new Color(0.18f, 0.55f, 1f, 0.54f), 1.25f);
            var cardShadow = card.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0.32f, 0.12f, 0.75f, 0.28f);
            cardShadow.effectDistance = new Vector2(4f, -4f);
            cardShadow.useGraphicAlpha = false;

            BuildProfileStatIcon(card.transform, iconKind, new Vector2(-142f, 0f));
            MakeText(card.transform, label, 17, FontStyle.Bold, Hex("AFC6E9"),
                new Vector2(30f, 31f), new Vector2(250f, 30f), TextAnchor.MiddleLeft, 2);
            var value = MakeText(card.transform, "0", 39, FontStyle.Bold, Cream,
                new Vector2(30f, -18f), new Vector2(250f, 54f), TextAnchor.MiddleLeft, 2);
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = 24;
            value.resizeTextMaxSize = 39;

            // Small dotted texture in the lower-right corner of each stat card.
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    var dot = CreateCard("Stat dot", card.transform,
                        new Vector2(151f + column * 7f, -48f + row * 7f), new Vector2(3f, 3f),
                        new Color(0.26f, 0.55f, 1f, 0.26f + row * 0.07f), 2);
                    dot.GetComponent<Image>().raycastTarget = false;
                }
            }

            return value;
        }

        private void BuildProfileStatIcon(Transform parent, string iconKind, Vector2 position)
        {
            var ring = CreateCard("Profile stat icon " + iconKind, parent, position, new Vector2(72f, 72f),
                Hex("07162D"), 36);
            AddProfileOutline(ring.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.82f), 1.4f);
            ring.GetComponent<Image>().raycastTarget = false;

            if (string.Equals(iconKind, "coin", StringComparison.OrdinalIgnoreCase))
            {
                BuildCanonicalGameCoin(ring.transform, Vector2.zero, 54f);
                return;
            }

            if (string.Equals(iconKind, "clock", StringComparison.OrdinalIgnoreCase))
            {
                var clockFace = CreateCard("Clock face", ring.transform, Vector2.zero, new Vector2(39f, 39f),
                    Hex("09213A"), 20);
                AddProfileOutline(clockFace.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.90f), 1.4f);
                clockFace.GetComponent<Image>().raycastTarget = false;
                var minute = CreateImage("Clock minute hand", clockFace.transform, Cyan,
                    Centered(new Vector2(0f, 6f), new Vector2(3f, 15f)), RoundedSprite(2));
                minute.raycastTarget = false;
                var hour = CreateImage("Clock hour hand", clockFace.transform, Cyan,
                    Centered(new Vector2(5f, -1f), new Vector2(13f, 3f)), RoundedSprite(2));
                hour.raycastTarget = false;
                var crown = CreateCard("Clock crown", ring.transform, new Vector2(0f, 23f),
                    new Vector2(11f, 6f), Cyan, 2);
                crown.GetComponent<Image>().raycastTarget = false;
                return;
            }

            // Trophy icon.
            var cup = CreateCard("Trophy cup", ring.transform, new Vector2(0f, 7f), new Vector2(28f, 21f),
                Cyan, 6);
            cup.GetComponent<Image>().raycastTarget = false;
            var handleLeft = CreateCard("Trophy handle left", ring.transform, new Vector2(-18f, 7f),
                new Vector2(8f, 15f), Cyan, 4);
            handleLeft.GetComponent<Image>().raycastTarget = false;
            var handleRight = CreateCard("Trophy handle right", ring.transform, new Vector2(18f, 7f),
                new Vector2(8f, 15f), Cyan, 4);
            handleRight.GetComponent<Image>().raycastTarget = false;
            var stem = CreateCard("Trophy stem", ring.transform, new Vector2(0f, -9f),
                new Vector2(6f, 13f), Cyan, 2);
            stem.GetComponent<Image>().raycastTarget = false;
            var basePlate = CreateCard("Trophy base", ring.transform, new Vector2(0f, -20f),
                new Vector2(24f, 6f), Cyan, 3);
            basePlate.GetComponent<Image>().raycastTarget = false;
        }

        private static Outline AddProfileOutline(Image image, Color color, float distance)
        {
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = false;
            return outline;
        }

        private void RefreshPlayerProfileUI()
        {
            var user = auth != null ? auth.CurrentUser : null;
            var playerId = string.IsNullOrWhiteSpace(bootstrapState.PlayerId) ? "GHD-PILOT" : bootstrapState.PlayerId;
            var displayName = user != null && !string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.DisplayName.Trim()
                : user != null && !string.IsNullOrWhiteSpace(user.UserId)
                    ? user.UserId
                    : playerId;

            if (playerProfileNameText != null)
                playerProfileNameText.text = displayName;

            var heroId = string.IsNullOrWhiteSpace(bootstrapState.SelectedCharacter) ? "nova" : bootstrapState.SelectedCharacter;
            var heroIndex = Array.IndexOf(CharacterIds, heroId);
            var heroName = heroIndex >= 0 && heroIndex < CharacterNames.Length
                ? CharacterNames[heroIndex]
                : heroId.ToUpperInvariant();
            if (playerProfileHeroNameText != null)
                playerProfileHeroNameText.text = heroName;
            if (playerProfileHeroImage != null)
            {
                var texture = Resources.Load<Texture2D>("UI/Characters/" + heroId);
                if (texture == null)
                    texture = Resources.Load<Texture2D>("UI/Avatars/" + heroId);
                playerProfileHeroImage.texture = texture;
                playerProfileHeroImage.material = heroIndex >= 3 ? characterCutoutMaterial : null;
                playerProfileHeroImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                playerProfileHeroImage.color = texture != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }

            if (playerProfileHighScoreText != null)
                playerProfileHighScoreText.text = bootstrapState.EndlessHighScore.ToString("N0");
            if (playerProfileCoinsText != null)
                playerProfileCoinsText.text = bootstrapState.LifetimeCoinsCollected.ToString("N0");
            if (playerProfilePlayTimeText != null)
                playerProfilePlayTimeText.text = FormatPlayTime(bootstrapState.TotalPlaySeconds);
            if (playerProfileStatusText != null)
            {
                playerProfileStatusText.text = "SHARE PROFILE";
                playerProfileStatusText.color = Hex("8DFF79");
            }

            // Immediate local country value; Firestore refresh below can replace it.
            var localCode = PlayerPrefs.GetString("ghd.country_code", string.Empty).Trim().ToUpperInvariant();
            var localName = PlayerPrefs.GetString("ghd.country_name", string.Empty).Trim();
            SetPlayerProfileCountryText(localCode, localName);

            // The share card always uses the selected hero, never an account-provider photo.
            if (playerProfileAvatarImage != null)
            {
                var selectedPortrait = Resources.Load<Texture2D>("UI/Avatars/" + heroId);
                var selectedHero = selectedPortrait != null
                    ? selectedPortrait
                    : Resources.Load<Texture2D>("UI/Characters/" + heroId);
                playerProfileAvatarImage.texture = selectedHero;
                playerProfileAvatarImage.uvRect = selectedPortrait != null
                    ? new Rect(0f, 0f, 1f, 1f)
                    : selectedHero != null
                        ? new Rect(0.08f, 0.50f, 0.84f, 0.47f)
                        : new Rect(0f, 0f, 1f, 1f);
                playerProfileAvatarImage.gameObject.SetActive(selectedHero != null);
                if (playerProfileAvatarFallback != null)
                    playerProfileAvatarFallback.gameObject.SetActive(selectedHero == null);
            }
        }

        private async Task RefreshPlayerProfileAvatarAsync()
        {
            var user = auth != null ? auth.CurrentUser : null;
            if (user == null || user.PhotoUrl == null || playerProfileAvatarImage == null)
                return;

            var url = user.PhotoUrl.ToString();
            if (string.IsNullOrWhiteSpace(url) || string.Equals(playerProfileLoadedAvatarUrl, url, StringComparison.Ordinal))
                return;

            try
            {
                using var request = UnityWebRequestTexture.GetTexture(url);
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();
                if (request.result != UnityWebRequest.Result.Success || auth == null || auth.CurrentUser == null
                    || auth.CurrentUser.UserId != user.UserId || playerProfileAvatarImage == null)
                    return;

                playerProfileAvatarImage.texture = DownloadHandlerTexture.GetContent(request);
                playerProfileAvatarImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                playerProfileAvatarImage.gameObject.SetActive(true);
                if (playerProfileAvatarFallback != null)
                    playerProfileAvatarFallback.gameObject.SetActive(false);
                playerProfileLoadedAvatarUrl = url;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Profile avatar could not be loaded: " + exception.Message);
            }
        }

        private async Task RefreshPlayerProfileCountryAsync()
        {
            var code = PlayerPrefs.GetString("ghd.country_code", string.Empty).Trim().ToUpperInvariant();
            var name = PlayerPrefs.GetString("ghd.country_name", string.Empty).Trim();

            try
            {
                if (firestore != null && auth != null && auth.CurrentUser != null)
                {
                    var userDoc = await firestore.Collection("users").Document(auth.CurrentUser.UserId).GetSnapshotAsync();
                    if (userDoc.Exists)
                    {
                        var values = userDoc.ToDictionary();
                        if (values.TryGetValue("country_code", out var codeValue) && codeValue != null)
                            code = codeValue.ToString().Trim().ToUpperInvariant();
                        if (values.TryGetValue("country_name", out var nameValue) && nameValue != null)
                            name = nameValue.ToString().Trim();
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Profile country user data could not be refreshed: " + exception.Message);
            }

            SetPlayerProfileCountryText(code, name);
            if (string.IsNullOrWhiteSpace(code) || playerProfileCountryFlagImage == null)
                return;

            if (playerProfileCountryTexture != null
                && string.Equals(playerProfileLoadedCountryCode, code, StringComparison.OrdinalIgnoreCase))
            {
                ShowPlayerProfileCountryFlag(playerProfileCountryTexture);
                return;
            }

            // Flags are static application art. Load the exact local ISO texture instead of downloading
            // a Firebase URL, so the age page, profile UI and shared card always show the same full flag.
            playerProfileCountryTexture = LoadCountryFlag(code);
            if (playerProfileCountryTexture != null)
            {
                playerProfileLoadedCountryCode = code;
                ShowPlayerProfileCountryFlag(playerProfileCountryTexture);
            }
        }

        private void SetPlayerProfileCountryText(string code, string name)
        {
            var normalizedCode = string.IsNullOrWhiteSpace(code) ? "--" : code.ToUpperInvariant();
            var displayName = string.IsNullOrWhiteSpace(name) ? "Country not selected" : name;
            if (playerProfileCountryCodeText != null)
                playerProfileCountryCodeText.text = normalizedCode;
            if (playerProfileCountryText != null)
                playerProfileCountryText.text = displayName.ToUpperInvariant();
            if (playerProfileCountryFlagFallback != null)
            {
                playerProfileCountryFlagFallback.text = normalizedCode;
                playerProfileCountryFlagFallback.gameObject.SetActive(true);
            }
            if (playerProfileCountryFlagImage != null &&
                !string.Equals(playerProfileLoadedCountryCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
                playerProfileCountryFlagImage.gameObject.SetActive(false);
        }

        private void ShowPlayerProfileCountryFlag(Texture2D texture)
        {
            if (texture == null || playerProfileCountryFlagImage == null)
                return;
            playerProfileCountryFlagImage.texture = texture;
            playerProfileCountryFlagImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            playerProfileCountryFlagImage.gameObject.SetActive(true);
            if (playerProfileCountryFlagFallback != null)
                playerProfileCountryFlagFallback.gameObject.SetActive(false);
        }

        private void SharePlayerProfile()
        {
            if (playerProfileShareInProgress)
                return;
            StartCoroutine(CapturePlayerProfileForPreview());
        }

        private IEnumerator CapturePlayerProfileForPreview()
        {
            if (playerProfileCaptureRect == null)
                yield break;

            playerProfileShareInProgress = true;
            if (playerProfileStatusText != null)
            {
                playerProfileStatusText.text = "PREPARING PROFILE IMAGE...";
                playerProfileStatusText.color = Cyan;
            }

            var closeWasActive = playerProfileCloseControl != null && playerProfileCloseControl.activeSelf;
            var editWasActive = playerProfileEditControl != null && playerProfileEditControl.activeSelf;
            var shareWasActive = playerProfileShareControl != null && playerProfileShareControl.activeSelf;
            var signatureWasActive = playerProfileCaptureSignature != null && playerProfileCaptureSignature.activeSelf;

            if (playerProfileCloseControl != null)
                playerProfileCloseControl.SetActive(false);
            if (playerProfileEditControl != null)
                playerProfileEditControl.SetActive(false);
            if (playerProfileShareControl != null)
                playerProfileShareControl.SetActive(false);
            if (playerProfileCaptureSignature != null)
                playerProfileCaptureSignature.SetActive(true);

            // Wait until Unity has rendered the clean share-card state.
            yield return new WaitForEndOfFrame();

            Texture2D fullScreen = null;
            Texture2D profileCard = null;
            string imagePath = null;
            Exception captureError = null;
            try
            {
                fullScreen = ScreenCapture.CaptureScreenshotAsTexture();
                profileCard = CropProfileCardTexture(fullScreen, playerProfileCaptureRect);
                imagePath = Path.Combine(Application.temporaryCachePath,
                    "GravityHalfDead_Profile_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                File.WriteAllBytes(imagePath, profileCard.EncodeToPNG());
            }
            catch (Exception exception)
            {
                captureError = exception;
            }

            if (playerProfileCloseControl != null)
                playerProfileCloseControl.SetActive(closeWasActive);
            if (playerProfileEditControl != null)
                playerProfileEditControl.SetActive(editWasActive);
            if (playerProfileShareControl != null)
                playerProfileShareControl.SetActive(shareWasActive);
            if (playerProfileCaptureSignature != null)
                playerProfileCaptureSignature.SetActive(signatureWasActive);

            if (fullScreen != null)
                Destroy(fullScreen);

            if (captureError != null || profileCard == null || string.IsNullOrWhiteSpace(imagePath))
            {
                if (profileCard != null)
                    Destroy(profileCard);
                Debug.LogWarning("Profile image capture failed: " + (captureError != null ? captureError.Message : "unknown error"));
                if (playerProfileStatusText != null)
                {
                    playerProfileStatusText.text = "SHARE IMAGE COULD NOT BE CREATED";
                    playerProfileStatusText.color = Hex("FF7A9E");
                }
                playerProfileShareInProgress = false;
                yield break;
            }

            // Ownership of the generated texture moves to the preview screen. Android sharing must only
            // begin after the player confirms with the second, green SHARE button. A short white camera
            // flash makes the capture action readable before the generated Polaroid preview appears.
            yield return PlayPlayerProfileCaptureAnimation();
            ShowPlayerProfileSharePreview(profileCard, imagePath);
            if (playerProfileStatusText != null)
            {
                playerProfileStatusText.text = "SHARE PROFILE";
                playerProfileStatusText.color = Hex("8DFF79");
            }
            playerProfileShareInProgress = false;
        }

        private void ShowPlayerProfileSharePreview(Texture2D profileCard, string imagePath)
        {
            if (playerProfileSharePreviewOverlay == null)
                BuildPlayerProfileSharePreview();

            ReleasePlayerProfileSharePreviewTexture();
            playerProfileSharePreviewTexture = profileCard;
            playerProfilePendingSharePath = imagePath ?? string.Empty;
            if (playerProfileSharePreviewImage != null)
            {
                playerProfileSharePreviewImage.texture = playerProfileSharePreviewTexture;
                playerProfileSharePreviewImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
            if (playerProfileSharePreviewAspect != null && profileCard.height > 0)
                playerProfileSharePreviewAspect.aspectRatio = (float)profileCard.width / profileCard.height;
            if (playerProfileSharePreviewStatusText != null)
                playerProfileSharePreviewStatusText.text = "Your screenshot will be saved to your gallery";

            playerProfileSharePreviewOverlay.transform.SetAsLastSibling();
            playerProfileSharePreviewOverlay.SetActive(true);
        }

        private void ClosePlayerProfileSharePreview()
        {
            if (playerProfileSharePreviewOverlay != null)
                playerProfileSharePreviewOverlay.SetActive(false);
            ReleasePlayerProfileSharePreviewTexture();
            playerProfilePendingSharePath = string.Empty;
        }

        private void ReleasePlayerProfileSharePreviewTexture()
        {
            if (playerProfileSharePreviewImage != null)
                playerProfileSharePreviewImage.texture = null;
            if (playerProfileSharePreviewTexture != null)
            {
                Destroy(playerProfileSharePreviewTexture);
                playerProfileSharePreviewTexture = null;
            }
        }

        private void ConfirmPlayerProfileShare()
        {
            if (string.IsNullOrWhiteSpace(playerProfilePendingSharePath)
                || !File.Exists(playerProfilePendingSharePath))
            {
                if (playerProfileSharePreviewStatusText != null)
                    playerProfileSharePreviewStatusText.text = "The profile image is no longer available";
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                ShareProfileImageAndroid(playerProfilePendingSharePath);
                if (playerProfileSharePreviewStatusText != null)
                    playerProfileSharePreviewStatusText.text = "Share options opened";
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Android profile image share failed: " + exception.Message);
                if (playerProfileSharePreviewStatusText != null)
                    playerProfileSharePreviewStatusText.text = "The profile image could not be shared";
            }
#else
            Debug.Log("Profile share image saved to: " + playerProfilePendingSharePath);
            GUIUtility.systemCopyBuffer = playerProfilePendingSharePath;
            if (playerProfileSharePreviewStatusText != null)
                playerProfileSharePreviewStatusText.text = "Preview ready — Android sharing opens on device";
#endif
        }

        private Texture2D CropProfileCardTexture(Texture2D source, RectTransform captureRect)
        {
            var corners = new Vector3[4];
            captureRect.GetWorldCorners(corners);
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);

            var x = Mathf.Clamp(Mathf.FloorToInt(bottomLeft.x), 0, source.width - 1);
            var y = Mathf.Clamp(Mathf.FloorToInt(bottomLeft.y), 0, source.height - 1);
            var right = Mathf.Clamp(Mathf.CeilToInt(topRight.x), x + 1, source.width);
            var top = Mathf.Clamp(Mathf.CeilToInt(topRight.y), y + 1, source.height);
            var width = Mathf.Max(1, right - x);
            var height = Mathf.Max(1, top - y);

            var result = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Gravity Half Dead Profile Share",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            result.SetPixels(source.GetPixels(x, y, width, height));
            result.Apply(false, false);
            return result;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void ShareProfileImageAndroid(string imagePath)
        {
            var imageBytes = File.ReadAllBytes(imagePath);
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
            using var values = new AndroidJavaObject("android.content.ContentValues");

            values.Call("put", "_display_name", Path.GetFileName(imagePath));
            values.Call("put", "mime_type", "image/png");
            values.Call("put", "title", "Gravity Half Dead Profile");

            using var buildVersion = new AndroidJavaClass("android.os.Build$VERSION");
            var sdkInt = buildVersion.GetStatic<int>("SDK_INT");
            if (sdkInt >= 29)
                values.Call("put", "relative_path", "Pictures/GravityHalfDead");

            using var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media");
            using var externalUri = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
            using var imageUri = resolver.Call<AndroidJavaObject>("insert", externalUri, values);
            if (imageUri == null)
                throw new InvalidOperationException("Android MediaStore did not return an image URI.");

            using (var outputStream = resolver.Call<AndroidJavaObject>("openOutputStream", imageUri))
            {
                if (outputStream == null)
                    throw new InvalidOperationException("Android could not open the profile image output stream.");
                outputStream.Call("write", imageBytes);
                outputStream.Call("flush");
                outputStream.Call("close");
            }

            using var intentClass = new AndroidJavaClass("android.content.Intent");
            using var intent = new AndroidJavaObject("android.content.Intent");
            intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
            intent.Call<AndroidJavaObject>("setType", "image/png");
            intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_STREAM"), imageUri);
            intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"),
                "Gravity: Half Dead - Player Profile");
            intent.Call<AndroidJavaObject>("addFlags",
                intentClass.GetStatic<int>("FLAG_GRANT_READ_URI_PERMISSION"));

            using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share player profile");
            activity.Call("startActivity", chooser);
        }
#endif

        private void BuildMeProfilePanel(Transform parent)
        {
            var cutoutShader = Resources.Load<Shader>("Shaders/UICharacterCutout");
            if (cutoutShader != null)
                characterCutoutMaterial = new Material(cutoutShader) { name = "Character background remover" };
            var novaShader = Resources.Load<Shader>("Shaders/UINovaCutout");
            if (novaShader != null)
                novaCutoutMaterial = new Material(novaShader) { name = "Nova repaired-art background remover" };

            gameMeContent = new GameObject("Me profile content");
            gameMeContent.transform.SetParent(parent, false);
            var meRect = gameMeContent.AddComponent<RectTransform>();
            Stretch(meRect);

            BuildMeCharactersPanel(gameMeContent.transform);

            BuildMeUpgradesPanel(gameMeContent.transform);
            BuildMeDiscsPanel(gameMeContent.transform);
            BuildMeUpgradeNavigation(gameMeContent.transform);

            gameMeContent.SetActive(false);
            meCharactersContent.SetActive(true);
            mePowerupsContent.SetActive(false);
            meDiscsContent.SetActive(false);
        }


        private void PopulateGameScreen(FirebaseUser user)
        {
            // Endless is the default home-screen mode. The casino lever toggles to Story.
            endlessMode = true;
            UpdateGameModeUI();
            RefreshMeProfileUI();
            RefreshTopPlayerAvatar(user);
        }

        private void RefreshTopPlayerAvatar(FirebaseUser user)
        {
            if (topPlayerAvatarImage == null || topPlayerSummaryRoot == null)
                return;

            var missionsCoveringHome = gameMissionsContent != null && gameMissionsContent.activeInHierarchy;
            var hasPlayer = user != null && !missionsCoveringHome;
            topPlayerSummaryRoot.SetActive(hasPlayer);
            if (!hasPlayer)
                return;

            // Always show the selected in-game avatar immediately. A linked account photo can
            // replace it asynchronously, but a missing or failed URL must never leave a blank hole.
            var selectedCharacter = string.IsNullOrWhiteSpace(bootstrapState.SelectedCharacter)
                ? "nova"
                : bootstrapState.SelectedCharacter;
            var localAvatar = Resources.Load<Texture2D>("UI/Avatars/" + selectedCharacter);
            if (localAvatar == null)
                localAvatar = Resources.Load<Texture2D>("UI/Characters/" + selectedCharacter);
            topPlayerAvatarImage.texture = localAvatar;
            topPlayerAvatarImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            topPlayerAvatarImage.gameObject.SetActive(localAvatar != null);

            var remotePhotoUrl = BestProfilePhotoUrl(user);
            if (!string.IsNullOrWhiteSpace(remotePhotoUrl))
                _ = LoadRemoteProfilePictureAsync(remotePhotoUrl, user.UserId);
        }

        private async Task LoadRemoteProfilePictureAsync(string url, string expectedUserId)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            using var request = UnityWebRequestTexture.GetTexture(url);
            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();
            if (request.result != UnityWebRequest.Result.Success || auth == null
                || auth.CurrentUser == null || auth.CurrentUser.UserId != expectedUserId)
                return;
            topPlayerAvatarImage.texture = DownloadHandlerTexture.GetContent(request);
            topPlayerAvatarImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            topPlayerAvatarImage.gameObject.SetActive(true);
        }


        private void ShowMeSection(int section)
        {
            if (meCharactersContent == null)
                return;

            var showingCharacters = section == 0;
            var showingUpgrades = section == 1;
            var showingDiscs = section == 2;
            meCharactersContent.SetActive(showingCharacters);
            if (mePowerupsContent != null)
                mePowerupsContent.SetActive(showingUpgrades);
            if (meDiscsContent != null)
                meDiscsContent.SetActive(showingDiscs);
            RefreshMeUpgradeNavigation(section);

            if (showingUpgrades)
            {
                RefreshPowerupUI();
                if (realtimePowerupPlayerReference == null)
                    _ = StartPowerupRealtimeSyncAsync();
            }
            else if (showingCharacters)
            {
                PreviewCharacter(bootstrapState.SelectedCharacter);
                RefreshMeProfileUI();
                if (realtimeCharacterPlayerReference == null)
                    _ = StartCharacterRealtimeSyncAsync();
            }
        }

        private static void SetButtonPalette(Button button, Color background, Color foreground)
        {
            if (button == null)
                return;
            button.GetComponent<Image>().color = background;
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.color = foreground;
        }
    }
}
