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
            _ = RefreshPlayerProfileAvatarAsync();
        }

        private void ClosePlayerProfileOverlay()
        {
            if (playerProfileOverlay != null)
                playerProfileOverlay.SetActive(false);
        }

        private void OpenPlayerProfileEditor()
        {
            ClosePlayerProfileOverlay();
            // Keep the existing character browser available, but only behind the explicit EDIT button.
            ShowGameTab("ME");
        }

        private void BuildPlayerProfileOverlay()
        {
            var host = safeRoot != null ? safeRoot : transform as RectTransform;
            playerProfileOverlay = new GameObject("Player Profile Overlay");
            playerProfileOverlay.transform.SetParent(host != null ? host : transform, false);
            var overlayRect = playerProfileOverlay.AddComponent<RectTransform>();
            Stretch(overlayRect);

            var blocker = playerProfileOverlay.AddComponent<Image>();
            blocker.color = new Color(0.005f, 0.012f, 0.028f, 0.94f);
            blocker.raycastTarget = true;

            // Main panel fills almost the entire safe screen, like a premium mobile profile sheet.
            var panel = new GameObject("Profile Main Panel");
            panel.transform.SetParent(playerProfileOverlay.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.035f, 0.045f);
            panelRect.anchorMax = new Vector2(0.965f, 0.955f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = RoundedSprite(44);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Hex("08152B");
            AddProfileOutline(panelImage, new Color(Cyan.r, Cyan.g, Cyan.b, 0.58f), 2f);

            // Top close button.
            var close = MakeButton(panel.transform, "X", new Vector2(432f, 745f),
                new Vector2(94f, 94f), Hex("C94D5A"), Cream, 56, ClosePlayerProfileOverlay);
            AddProfileOutline(close.GetComponent<Image>(), new Color(1f, 0.68f, 0.72f, 0.62f), 2f);
            close.gameObject.AddComponent<ButtonGlow>();

            MakeText(panel.transform, "PLAYER PROFILE", 48, FontStyle.Bold, Cream,
                new Vector2(0f, 748f), new Vector2(640f, 76f), TextAnchor.MiddleCenter, 4);
            CreateImage("Profile title line", panel.transform, Cyan,
                Centered(new Vector2(0f, 700f), new Vector2(390f, 5f)), RoundedSprite(3)).raycastTarget = false;

            // Identity card — square avatar, name/UID, country, edit button. No circular avatar carousel.
            var identity = CreateCard("Profile Identity", panel.transform, new Vector2(0f, 540f),
                new Vector2(900f, 270f), Hex("101D37"), 34);
            AddProfileOutline(identity.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.34f), 1.4f);

            var avatarFrame = CreateCard("Square Avatar Frame", identity.transform, new Vector2(-326f, 20f),
                new Vector2(190f, 190f), Hex("1C3760"), 28);
            AddProfileOutline(avatarFrame.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f), 2f);
            var avatarMask = CreateCard("Square Avatar Mask", avatarFrame.transform, Vector2.zero,
                new Vector2(166f, 166f), Hex("07182C"), 22);
            avatarMask.GetComponent<Image>().type = Image.Type.Simple;
            avatarMask.AddComponent<Mask>().showMaskGraphic = true;
            playerProfileAvatarImage = MakeTextureImage("Profile Avatar", avatarMask.transform, null,
                Vector2.zero, new Vector2(166f, 166f));
            playerProfileAvatarImage.raycastTarget = false;
            playerProfileAvatarFallback = MakeText(avatarMask.transform, "PILOT", 25, FontStyle.Bold, Cyan,
                Vector2.zero, new Vector2(150f, 60f), TextAnchor.MiddleCenter, 2);

            playerProfileNameText = MakeText(identity.transform, "PILOT", 42, FontStyle.Bold, Cream,
                new Vector2(62f, 62f), new Vector2(540f, 60f), TextAnchor.MiddleLeft, 2);
            playerProfileNameText.resizeTextForBestFit = true;
            playerProfileNameText.resizeTextMinSize = 20;
            playerProfileNameText.resizeTextMaxSize = 42;

            var countryPill = CreateCard("Profile Country", identity.transform, new Vector2(42f, 2f),
                new Vector2(500f, 64f), Hex("0A1830"), 22);
            AddProfileOutline(countryPill.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f), 1f);
            var countryFlagObject = new GameObject("Country Flag");
            countryFlagObject.transform.SetParent(countryPill.transform, false);
            var countryFlagRect = countryFlagObject.AddComponent<RectTransform>();
            SetRect(countryFlagRect, new Vector2(-200f, 0f), new Vector2(62f, 42f));
            playerProfileCountryFlagImage = countryFlagObject.AddComponent<RawImage>();
            playerProfileCountryFlagImage.raycastTarget = false;
            var countryAspect = countryFlagObject.AddComponent<AspectRatioFitter>();
            countryAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            countryAspect.aspectRatio = 1.5f;
            playerProfileCountryFlagFallback = MakeText(countryPill.transform, "--", 20, FontStyle.Bold, Cyan,
                new Vector2(-200f, 0f), new Vector2(64f, 42f), TextAnchor.MiddleCenter);
            playerProfileCountryText = MakeText(countryPill.transform, "COUNTRY", 24, FontStyle.Bold, Cream,
                new Vector2(42f, 0f), new Vector2(370f, 44f), TextAnchor.MiddleLeft, 2);

            var edit = MakeButton(identity.transform, "EDIT", new Vector2(-326f, -94f),
                new Vector2(190f, 58f), Hex("87D94C"), Hex("08121E"), 22, OpenPlayerProfileEditor);
            AddProfileOutline(edit.GetComponent<Image>(), new Color(0.78f, 1f, 0.58f, 0.55f), 1.6f);

            var share = MakeButton(panel.transform, "SHARE PROFILE", new Vector2(-320f, 355f),
                new Vector2(250f, 70f), Hex("153B66"), Cyan, 19, SharePlayerProfile);
            AddProfileOutline(share.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f), 1.5f);

            // Main showcase — selected Firebase hero on the left, three big career stats on the right.
            var showcase = CreateCard("Profile Showcase", panel.transform, new Vector2(0f, -70f),
                new Vector2(900f, 760f), Hex("0B1932"), 34);
            AddProfileOutline(showcase.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.30f), 1.4f);

            var heroLabel = CreateCard("Selected Hero Label", showcase.transform, new Vector2(-235f, 290f),
                new Vector2(330f, 58f), Hex("14395C"), 18);
            MakeText(heroLabel.transform, "SELECTED HERO", 17, FontStyle.Bold, Cyan,
                new Vector2(0f, 0f), new Vector2(300f, 40f), TextAnchor.MiddleCenter, 2);

            playerProfileHeroNameText = MakeText(showcase.transform, "NOVA", 36, FontStyle.Bold, Cream,
                new Vector2(-235f, 238f), new Vector2(360f, 54f), TextAnchor.MiddleCenter, 2);

            var heroStage = new GameObject("Profile Hero Stage");
            heroStage.transform.SetParent(showcase.transform, false);
            var heroStageRect = heroStage.AddComponent<RectTransform>();
            SetRect(heroStageRect, new Vector2(-235f, -55f), new Vector2(420f, 555f));
            playerProfileHeroImage = MakeTextureImage("Firebase Selected Hero", heroStage.transform, null,
                Vector2.zero, new Vector2(420f, 555f));
            playerProfileHeroImage.raycastTarget = false;
            var heroAspect = playerProfileHeroImage.GetComponent<AspectRatioFitter>();
            if (heroAspect != null)
                heroAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            var heroShadow = CreateCard("Hero Ground Shadow", showcase.transform, new Vector2(-235f, -325f),
                new Vector2(310f, 44f), new Color(0f, 0f, 0f, 0.40f), 22);
            heroShadow.GetComponent<Image>().raycastTarget = false;

            playerProfileHighScoreText = BuildProfileStatCard(showcase.transform, "S", "HIGH SCORE",
                new Vector2(240f, 175f));
            playerProfileCoinsText = BuildProfileStatCard(showcase.transform, "C", "TOTAL COINS COLLECTED",
                new Vector2(240f, -15f));
            playerProfilePlayTimeText = BuildProfileStatCard(showcase.transform, "T", "TOTAL TIME PLAYED",
                new Vector2(240f, -205f));

            playerProfileStatusText = MakeText(panel.transform, "FIREBASE PROFILE · LIVE", 17, FontStyle.Bold,
                Hex("72E6C7"), new Vector2(0f, -540f), new Vector2(760f, 38f), TextAnchor.MiddleCenter, 2);
            MakeText(panel.transform, "Gravity: Half Dead", 18, FontStyle.Normal, Hex("7F8AA6"),
                new Vector2(0f, -680f), new Vector2(600f, 38f), TextAnchor.MiddleCenter);

            playerProfileOverlay.SetActive(false);
        }

        private Text BuildProfileStatCard(Transform parent, string icon, string label, Vector2 position)
        {
            var card = CreateCard("Profile stat " + label, parent, position, new Vector2(360f, 154f),
                Hex("12335B"), 28);
            AddProfileOutline(card.GetComponent<Image>(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f), 1.2f);
            MakeText(card.transform, icon, 34, FontStyle.Bold, Cyan,
                new Vector2(-134f, 20f), new Vector2(58f, 58f), TextAnchor.MiddleCenter);
            MakeText(card.transform, label, 19, FontStyle.Bold, Hex("A9B8D4"),
                new Vector2(25f, 38f), new Vector2(270f, 34f), TextAnchor.MiddleLeft, 2);
            return MakeText(card.transform, "0", 38, FontStyle.Bold, Cream,
                new Vector2(25f, -22f), new Vector2(270f, 58f), TextAnchor.MiddleLeft, 2);
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

            // Immediate local country value; Firestore refresh below can replace it.
            var localCode = PlayerPrefs.GetString("ghd.country_code", string.Empty).Trim().ToUpperInvariant();
            var localName = PlayerPrefs.GetString("ghd.country_name", string.Empty).Trim();
            SetPlayerProfileCountryText(localCode, localName);

            // Use selected hero as the avatar fallback until an account photo is downloaded.
            if (playerProfileAvatarImage != null)
            {
                var fallbackTexture = Resources.Load<Texture2D>("UI/Characters/" + heroId);
                if (fallbackTexture == null)
                    fallbackTexture = Resources.Load<Texture2D>("UI/Avatars/" + heroId);
                playerProfileAvatarImage.texture = fallbackTexture;
                playerProfileAvatarImage.uvRect = fallbackTexture != null
                    ? new Rect(0.08f, 0.50f, 0.84f, 0.47f)
                    : new Rect(0f, 0f, 1f, 1f);
                playerProfileAvatarImage.gameObject.SetActive(fallbackTexture != null);
                if (playerProfileAvatarFallback != null)
                    playerProfileAvatarFallback.gameObject.SetActive(fallbackTexture == null);
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

            if (playerProfileCountryTexture != null && string.Equals(playerProfileLoadedCountryCode, code, StringComparison.OrdinalIgnoreCase))
            {
                ShowPlayerProfileCountryFlag(playerProfileCountryTexture);
                return;
            }

            try
            {
                if (firestore == null)
                    return;
                var countryDoc = await firestore.Collection("countries").Document(code).GetSnapshotAsync();
                if (!countryDoc.Exists)
                    return;
                var countryValues = countryDoc.ToDictionary();
                if (!countryValues.TryGetValue("flag_url", out var flagValue) || flagValue == null)
                    return;
                var flagUrl = flagValue.ToString();
                if (string.IsNullOrWhiteSpace(flagUrl))
                    return;

                using var request = UnityWebRequestTexture.GetTexture(flagUrl);
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();
                if (request.result != UnityWebRequest.Result.Success || playerProfileCountryFlagImage == null)
                    return;

                playerProfileCountryTexture = DownloadHandlerTexture.GetContent(request);
                playerProfileLoadedCountryCode = code;
                ShowPlayerProfileCountryFlag(playerProfileCountryTexture);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Profile country flag could not be loaded: " + exception.Message);
            }
        }

        private void SetPlayerProfileCountryText(string code, string name)
        {
            var normalizedCode = string.IsNullOrWhiteSpace(code) ? "--" : code.ToUpperInvariant();
            var displayName = string.IsNullOrWhiteSpace(name) ? "Country not selected" : name;
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
            var user = auth != null ? auth.CurrentUser : null;
            var name = user != null && !string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.DisplayName.Trim()
                : bootstrapState.PlayerId;
            var heroId = string.IsNullOrWhiteSpace(bootstrapState.SelectedCharacter) ? "nova" : bootstrapState.SelectedCharacter;
            var heroIndex = Array.IndexOf(CharacterIds, heroId);
            var heroName = heroIndex >= 0 && heroIndex < CharacterNames.Length ? CharacterNames[heroIndex] : heroId;
            var country = PlayerPrefs.GetString("ghd.country_name", string.Empty);
            var summary = name + " · Gravity: Half Dead\n"
                + (string.IsNullOrWhiteSpace(country) ? string.Empty : country + "\n")
                + "Hero: " + heroName + "\n"
                + "High Score: " + bootstrapState.EndlessHighScore.ToString("N0") + "\n"
                + "Coins Collected: " + bootstrapState.LifetimeCoinsCollected.ToString("N0") + "\n"
                + "Time Played: " + FormatPlayTime(bootstrapState.TotalPlaySeconds);

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var intentClass = new AndroidJavaClass("android.content.Intent");
                using var intent = new AndroidJavaObject("android.content.Intent");
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), summary);
                using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share profile");
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("startActivity", chooser);
                if (playerProfileStatusText != null)
                    playerProfileStatusText.text = "PROFILE READY TO SHARE";
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Android profile share failed: " + exception.Message);
            }
#endif
            GUIUtility.systemCopyBuffer = summary;
            if (playerProfileStatusText != null)
                playerProfileStatusText.text = "PROFILE SUMMARY COPIED";
        }

        private void BuildMeProfilePanel(Transform parent)
        {
            var cutoutShader = Resources.Load<Shader>("Shaders/UICharacterCutout");
            if (cutoutShader != null)
                characterCutoutMaterial = new Material(cutoutShader) { name = "Character background remover" };

            gameMeContent = new GameObject("Me profile content");
            gameMeContent.transform.SetParent(parent, false);
            var meRect = gameMeContent.AddComponent<RectTransform>();
            Stretch(meRect);
            meCharactersContent = new GameObject("Characters content");
            meCharactersContent.transform.SetParent(gameMeContent.transform, false);
            var charactersRect = meCharactersContent.AddComponent<RectTransform>();
            Stretch(charactersRect);

            CreateCard("Character page horizon", meCharactersContent.transform, new Vector2(0, 235),
                new Vector2(1080, 690), new Color(0.08f, 0.25f, 0.36f, 0.30f), 0);
            CreateCard("Character showcase card", meCharactersContent.transform, new Vector2(0, 350),
                new Vector2(650, 570), new Color(0.03f, 0.43f, 0.52f, 0.68f), 56);

            meCharacterName = MakeText(meCharactersContent.transform, "NOVA", 54, FontStyle.Bold, Cream,
                new Vector2(0, 700), new Vector2(850, 76), TextAnchor.MiddleCenter, 5);
            meCharacterUnlockText = MakeText(meCharactersContent.transform, string.Empty, 25,
                FontStyle.Bold, Hex("FFD35C"), new Vector2(0, 640), new Vector2(760, 52), TextAnchor.MiddleCenter, 2);
            meCharacterActionButton = MakeButton(meCharactersContent.transform, "", new Vector2(0, 640),
                new Vector2(470, 62), new Color(1f, 1f, 1f, 0.001f), Hex("FFD35C"), 24,
                TryUnlockOrSelectPreviewCharacter);

            var stageShadowObject = CreateCard("Character ground shadow", meCharactersContent.transform,
                new Vector2(0, 92), new Vector2(280, 38), new Color(0f, 0f, 0f, 0.30f), 19);
            stageShadowObject.GetComponent<Image>().raycastTarget = false;

            var performerStage = new GameObject("Character performer stage");
            performerStage.transform.SetParent(meCharactersContent.transform, false);
            var performerStageRect = performerStage.AddComponent<RectTransform>();
            SetRect(performerStageRect, new Vector2(0, 365), new Vector2(370, 500));
            meCharacterShowcase = MakeTextureImage("Selected character showcase", performerStage.transform,
                Resources.Load<Texture2D>("UI/Characters/nova"), Vector2.zero, new Vector2(370, 500));
            var showcaseAspect = meCharacterShowcase.GetComponent<AspectRatioFitter>();
            if (showcaseAspect != null)
                showcaseAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            for (var i = 0; i < CharacterIds.Length; i++)
            {
                var index = i;
                var row = i / 5;
                var column = i % 5;
                var rowCount = row < 2 ? 5 : 3;
                var startX = -(rowCount - 1) * 90f;
                var position = new Vector2(startX + column * 180f, -190 - row * 158f);
                var cardColor = column % 3 == 0 ? new Color(0.12f, 0.20f, 0.28f, 0.92f)
                    : column % 3 == 1 ? new Color(0.20f, 0.18f, 0.28f, 0.92f)
                    : new Color(0.12f, 0.24f, 0.26f, 0.92f);

                var hitArea = CreateCard(CharacterNames[i] + " roster hit area", meCharactersContent.transform,
                    position, new Vector2(158, 150), cardColor, 12);
                var hitGraphic = hitArea.GetComponent<Image>();
                var choose = hitArea.AddComponent<Button>();
                choose.targetGraphic = hitGraphic;
                choose.onClick.AddListener(() => PreviewCharacter(CharacterIds[index]));
                meAvatarPortraits[i] = MakeTextureImage(CharacterNames[i] + " roster art", hitArea.transform,
                    Resources.Load<Texture2D>("UI/Characters/" + CharacterIds[i]), Vector2.zero, new Vector2(140, 140));
                if (i >= 3 && characterCutoutMaterial != null)
                    meAvatarPortraits[i].material = characterCutoutMaterial;
                var rosterAspect = meAvatarPortraits[i].GetComponent<AspectRatioFitter>();
                if (rosterAspect != null)
                    rosterAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                meAvatarBorders[i] = CreateCard(CharacterNames[i] + " selected frame", hitArea.transform,
                    Vector2.zero, new Vector2(168, 160), Cyan, 14).GetComponent<Image>();
                meAvatarBorders[i].transform.SetAsFirstSibling();
                meAvatarBorders[i].raycastTarget = false;
                var selectedFrameInterior = CreateCard(CharacterNames[i] + " selected frame interior",
                    meAvatarBorders[i].transform, Vector2.zero, new Vector2(154, 146), cardColor, 11);
                selectedFrameInterior.GetComponent<Image>().raycastTarget = false;
                meAvatarStatusTexts[i] = MakeText(hitArea.transform, string.Empty, 13, FontStyle.Bold, Cream,
                    Vector2.zero, new Vector2(1, 1), TextAnchor.MiddleCenter);
            }

            gameMeContent.SetActive(false);
            meCharactersContent.SetActive(true);
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
            if (topPlayerAvatarImage == null)
                return;

            var isGuest = user == null || user.IsAnonymous;
            topPlayerAvatarFallback.SetActive(isGuest);
            topPlayerAvatarImage.gameObject.SetActive(!isGuest);
            if (!isGuest)
            {
                topPlayerAvatarImage.texture = Resources.Load<Texture2D>(
                    "UI/Characters/" + bootstrapState.SelectedCharacter);
                if (topPlayerAvatarImage.texture == null)
                    topPlayerAvatarImage.texture = Resources.Load<Texture2D>(
                        "UI/Avatars/" + bootstrapState.SelectedCharacter);
                topPlayerAvatarImage.uvRect = new Rect(0.08f, 0.50f, 0.84f, 0.47f);
                if (user.PhotoUrl != null)
                    _ = LoadRemoteProfilePictureAsync(user.PhotoUrl.ToString(), user.UserId);
            }

            var frame = Resources.Load<Texture2D>("UI/AvatarFrames/" + bootstrapState.SelectedFrame);
            if (frame == null)
                frame = Resources.Load<Texture2D>("UI/AvatarFrames/neon_recruit");
            topPlayerAvatarFrameImage.texture = frame;
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
        }


        private void ShowMeSection(int section)
        {
            if (meCharactersContent == null)
                return;

            // The profile destination is now a focused character browser; the old Statistics,
            // Characters and Power-ups sub-navigation does not belong on this page.
            meCharactersContent.SetActive(true);
            PreviewCharacter(bootstrapState.SelectedCharacter);
            RefreshMeProfileUI();
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
