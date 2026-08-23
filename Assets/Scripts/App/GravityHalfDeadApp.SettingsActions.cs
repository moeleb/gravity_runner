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
        private string settingsAvatarLoadedUrl = string.Empty;

        private void OpenGameSettings()
        {
            if (gameSettingsPanel == null)
                return;

            LoadLocalSettings();
            ApplyAudioSettings();
            ApplyGraphicsSettings();
            if (gameSettingsMainPage != null)
                gameSettingsMainPage.SetActive(true);
            if (gameSettingsSupportPage != null)
                gameSettingsSupportPage.SetActive(false);

            RefreshSettingsUI();
            gameSettingsPanel.transform.SetAsLastSibling();
            gameSettingsPanel.gameObject.SetActive(true);
            gameSettingsPanel.alpha = 1f;
            gameSettingsPanel.blocksRaycasts = true;
            gameSettingsPanel.interactable = true;
        }

        private void CloseGameSettings()
        {
            if (gameSettingsPanel == null)
                return;

            if (gameSettingsSupportPage != null)
                gameSettingsSupportPage.SetActive(false);
            if (gameSettingsMainPage != null)
                gameSettingsMainPage.SetActive(true);
            gameSettingsPanel.alpha = 0f;
            gameSettingsPanel.blocksRaycasts = false;
            gameSettingsPanel.interactable = false;
            gameSettingsPanel.gameObject.SetActive(false);
        }

        private void OpenSettingsSupportPage()
        {
            if (gameSettingsMainPage != null)
                gameSettingsMainPage.SetActive(false);
            if (gameSettingsSupportPage != null)
                gameSettingsSupportPage.SetActive(true);
            supportSubjectInput?.Select();
        }

        private void CloseSettingsSupportPage()
        {
            if (gameSettingsSupportPage != null)
                gameSettingsSupportPage.SetActive(false);
            if (gameSettingsMainPage != null)
                gameSettingsMainPage.SetActive(true);
        }

        private void LoadLocalSettings()
        {
            musicEnabled = PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
            sfxEnabled = PlayerPrefs.GetInt(SfxEnabledKey, 1) == 1;
            vibrationEnabled = PlayerPrefs.GetInt(VibrationEnabledKey, 1) == 1;
            notificationsEnabled = PlayerPrefs.GetInt(NotificationsEnabledKey, 0) == 1;
            resolutionQualityIndex = Mathf.Clamp(PlayerPrefs.GetInt(ResolutionQualityKey, 1), 0, 2);
            targetFps = NormalizeFps(PlayerPrefs.GetInt(TargetFpsKey, 60));
        }

        private void ApplyCloudSettings(IDictionary<string, object> playerData)
        {
            if (playerData == null || !playerData.TryGetValue("settings", out var rawSettings)
                || rawSettings is not IDictionary<string, object> settings)
            {
                LoadLocalSettings();
                return;
            }

            musicEnabled = ReadBool(settings, "music", PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1);
            sfxEnabled = ReadBool(settings, "sfx", PlayerPrefs.GetInt(SfxEnabledKey, 1) == 1);
            vibrationEnabled = ReadBool(settings, "vibration", PlayerPrefs.GetInt(VibrationEnabledKey, 1) == 1);
            notificationsEnabled = ReadBool(settings, "notifications", PlayerPrefs.GetInt(NotificationsEnabledKey, 0) == 1);
            resolutionQualityIndex = Mathf.Clamp(ReadInt(settings, "resolution_quality",
                PlayerPrefs.GetInt(ResolutionQualityKey, 1)), 0, 2);
            targetFps = NormalizeFps(ReadInt(settings, "fps", PlayerPrefs.GetInt(TargetFpsKey, 60)));

            PersistLocalSettings();
            ApplyAudioSettings();
            ApplyGraphicsSettings();
        }

        private static bool ReadBool(IDictionary<string, object> map, string key, bool fallback)
        {
            if (!map.TryGetValue(key, out var value) || value == null)
                return fallback;
            if (value is bool flag)
                return flag;
            return bool.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
        }

        private static int ReadInt(IDictionary<string, object> map, string key, int fallback)
        {
            if (!map.TryGetValue(key, out var value) || value == null)
                return fallback;
            if (value is long longValue)
                return (int)longValue;
            if (value is int intValue)
                return intValue;
            if (value is double doubleValue)
                return Mathf.RoundToInt((float)doubleValue);
            return int.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
        }

        private void ToggleMusic()
        {
            musicEnabled = !musicEnabled;
            SaveAndRefreshSettings();
        }

        private void ToggleSfx()
        {
            sfxEnabled = !sfxEnabled;
            SaveAndRefreshSettings();
        }

        private void ToggleVibration()
        {
            vibrationEnabled = !vibrationEnabled;
            SaveAndRefreshSettings();
        }

        private void ToggleNotifications()
        {
            notificationsEnabled = !notificationsEnabled;
            SaveAndRefreshSettings();
            ApplyAndroidNotificationPreference();
        }

        private void CycleResolutionQuality()
        {
            StepResolutionQuality(1);
        }

        private void StepResolutionQuality(int direction)
        {
            var next = Mathf.Clamp(resolutionQualityIndex + Math.Sign(direction), 0, ResolutionQualityNames.Length - 1);
            if (next == resolutionQualityIndex)
                return;
            resolutionQualityIndex = next;
            SaveAndRefreshSettings();
        }

        private void CycleTargetFps()
        {
            StepTargetFps(1);
        }

        private void StepTargetFps(int direction)
        {
            var currentIndex = Array.IndexOf(FpsChoices, targetFps);
            if (currentIndex < 0)
                currentIndex = 1;
            var nextIndex = Mathf.Clamp(currentIndex + Math.Sign(direction), 0, FpsChoices.Length - 1);
            if (nextIndex == currentIndex)
                return;
            targetFps = FpsChoices[nextIndex];
            SaveAndRefreshSettings();
        }

        private static int NormalizeFps(int value)
        {
            var best = FpsChoices[0];
            var bestDistance = Mathf.Abs(value - best);
            for (var i = 1; i < FpsChoices.Length; i++)
            {
                var distance = Mathf.Abs(value - FpsChoices[i]);
                if (distance >= bestDistance)
                    continue;
                best = FpsChoices[i];
                bestDistance = distance;
            }
            return best;
        }

        private void SaveAndRefreshSettings()
        {
            PersistLocalSettings();
            ApplyAudioSettings();
            ApplyGraphicsSettings();
            RefreshSettingsUI();
            _ = SyncSettingsToFirebaseAsync();
        }

        private void PersistLocalSettings()
        {
            PlayerPrefs.SetInt(MusicEnabledKey, musicEnabled ? 1 : 0);
            PlayerPrefs.SetInt(SfxEnabledKey, sfxEnabled ? 1 : 0);
            PlayerPrefs.SetInt(VibrationEnabledKey, vibrationEnabled ? 1 : 0);
            PlayerPrefs.SetInt(NotificationsEnabledKey, notificationsEnabled ? 1 : 0);
            PlayerPrefs.SetInt(ResolutionQualityKey, resolutionQualityIndex);
            PlayerPrefs.SetInt(TargetFpsKey, targetFps);
            PlayerPrefs.Save();
        }

        private void ApplyAudioSettings()
        {
            AudioListener.pause = false;
            AudioListener.volume = 1f;
            foreach (var source in FindObjectsByType<AudioSource>())
            {
                var isMusic = source.gameObject.name.IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0;
                source.mute = isMusic ? !musicEnabled : !sfxEnabled;
            }
        }

        private void ApplyGraphicsSettings()
        {
            // Keep the UI at the native logical resolution. Only the game render buffers are scaled,
            // which avoids breaking the responsive layout on small/tall phones.
            var scale = resolutionQualityIndex switch
            {
                0 => 0.70f,
                1 => 0.85f,
                _ => 1.00f
            };

            try
            {
                QualitySettings.resolutionScalingFixedDPIFactor = scale;
                ScalableBufferManager.ResizeBuffers(scale, scale);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Resolution scale could not be applied on this device: " + exception.Message);
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFps;
        }

        private void RefreshSettingsUI()
        {
            ConfigureSettingsToggle(gameMusicButton, musicEnabled);
            ConfigureSettingsToggle(gameSfxButton, sfxEnabled);
            ConfigureSettingsToggle(gameVibrationButton, vibrationEnabled);
            ConfigureSettingsToggle(gameNotificationsButton, notificationsEnabled);
            RefreshGraphicsSettingSelectors();
            RefreshSettingsAccountUI();
        }

        private static void ConfigureSettingsToggle(Button button, bool enabled)
        {
            if (button == null)
                return;

            var track = button.transform.Find("Toggle Track");
            if (track == null)
                return;
            var trackImage = track.GetComponent<Image>();
            if (trackImage != null)
                trackImage.color = enabled ? SettingsGreen : Hex("384159");

            var knob = track.Find("Toggle Knob") as RectTransform;
            if (knob != null)
                knob.anchoredPosition = new Vector2(enabled ? 19f : -19f, 0f);
        }

        private void RefreshGraphicsSettingSelectors()
        {
            if (gameResolutionValueText != null)
                gameResolutionValueText.text = ResolutionQualityNames[resolutionQualityIndex];
            var canResolutionGoBack = resolutionQualityIndex > 0;
            var canResolutionGoForward = resolutionQualityIndex < ResolutionQualityNames.Length - 1;
            SetSelectorDirectionState(gameResolutionPreviousButton, gameResolutionPreviousText, canResolutionGoBack);
            SetSelectorDirectionState(gameResolutionNextButton, gameResolutionNextText, canResolutionGoForward);
            if (gameResolutionCycleButton != null)
                gameResolutionCycleButton.interactable = canResolutionGoForward;

            var fpsIndex = Array.IndexOf(FpsChoices, targetFps);
            if (fpsIndex < 0)
                fpsIndex = 1;
            if (gameFpsValueText != null)
                gameFpsValueText.text = FpsChoices[fpsIndex].ToString();
            var canFpsGoBack = fpsIndex > 0;
            var canFpsGoForward = fpsIndex < FpsChoices.Length - 1;
            SetSelectorDirectionState(gameFpsPreviousButton, gameFpsPreviousText, canFpsGoBack);
            SetSelectorDirectionState(gameFpsNextButton, gameFpsNextText, canFpsGoForward);
            if (gameFpsCycleButton != null)
                gameFpsCycleButton.interactable = canFpsGoForward;
        }

        private static void SetSelectorDirectionState(Button button, Text arrow, bool visible)
        {
            if (button != null)
            {
                button.interactable = visible;
                button.gameObject.SetActive(visible);
            }
            if (arrow != null)
                arrow.gameObject.SetActive(visible);
        }

        private void RefreshSettingsAccountUI()
        {
            if (auth == null || gameSettingsEmailText == null)
                return;

            var user = auth.CurrentUser;
            var isGuest = user == null || user.IsAnonymous;
            var playerId = string.IsNullOrWhiteSpace(bootstrapState.PlayerId) ? "GHD-GUEST" : bootstrapState.PlayerId;

            if (isGuest)
            {
                gameSettingsEmailText.text = "Guest player";
                gameSettingsProviderText.text = "Connect an account to protect progress";
                gameSettingsProviderText.color = SettingsYellow;
                gameSettingsPlayerIdText.text = "PLAYER ID  ·  " + playerId;
                if (gameSettingsProviderBadgeText != null)
                {
                    gameSettingsProviderBadgeText.text = "?";
                    gameSettingsProviderBadgeText.color = Hex("1C2A48");
                }
                if (gameSettingsLogoutButton != null)
                    gameSettingsLogoutButton.gameObject.SetActive(false);
            }
            else
            {
                var displayIdentity = !string.IsNullOrWhiteSpace(user.Email)
                    ? user.Email
                    : !string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName : playerId;
                gameSettingsEmailText.text = displayIdentity;
                gameSettingsProviderText.text = LinkedProviderStatus(user);
                gameSettingsProviderText.color = SettingsGreen;
                gameSettingsPlayerIdText.text = "PLAYER ID  ·  " + playerId;
                if (gameSettingsProviderBadgeText != null)
                {
                    gameSettingsProviderBadgeText.text = ProviderBadge(user);
                    gameSettingsProviderBadgeText.color = ProviderBadgeColor(user);
                }
                if (gameSettingsLogoutButton != null)
                    gameSettingsLogoutButton.gameObject.SetActive(true);
            }

            RefreshSettingsProviderButtons(user);
            RefreshSettingsAccountAvatar(user);
        }

        private void RefreshSettingsProviderButtons(FirebaseUser user)
        {
            var isGuest = user == null || user.IsAnonymous;
            var facebookLinked = !isGuest && HasLinkedProvider(user, FacebookAuthProvider.ProviderId);
            ConfigureProviderButton(gameSettingsFacebookButton, gameSettingsFacebookButtonText,
                gameSettingsFacebookIconText, facebookLinked, "FACEBOOK", "f", Hex("1877F2"));

#if UNITY_IOS
            var platformLinked = !isGuest && HasLinkedProvider(user, "apple.com");
            ConfigureProviderButton(gameSettingsPlatformButton, gameSettingsPlatformButtonText,
                gameSettingsPlatformIconText, platformLinked, "APPLE", "A", Hex("101218"));
#else
            var platformLinked = !isGuest && HasLinkedProvider(user, GoogleAuthProvider.ProviderId);
            ConfigureProviderButton(gameSettingsPlatformButton, gameSettingsPlatformButtonText,
                gameSettingsPlatformIconText, platformLinked, "GOOGLE", "G", Hex("FFFFFF"));
#endif

            if (settingsProviderLinkInProgress)
            {
                if (gameSettingsFacebookButton != null)
                    gameSettingsFacebookButton.interactable = false;
                if (gameSettingsPlatformButton != null)
                    gameSettingsPlatformButton.interactable = false;
            }
        }

        private static void ConfigureProviderButton(Button button, Text labelText, Text iconText, bool linked,
            string providerName, string icon, Color iconColor)
        {
            if (button == null)
                return;

            button.interactable = !linked;
            if (labelText != null)
            {
                labelText.text = linked ? "✓  " + providerName + " LINKED" : "CONNECT " + providerName;
                labelText.color = linked ? SettingsGreen : Cream;
            }
            if (iconText != null)
            {
                iconText.text = icon;
                iconText.color = iconColor.grayscale > 0.75f ? Hex("2756C7") : Color.white;
            }
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = linked ? Hex("101A2C") : SettingsCardInner;
        }

        private void RefreshSettingsAccountAvatar(FirebaseUser user)
        {
            if (gameSettingsAvatarImage == null || gameSettingsAvatarFallback == null)
                return;

            var isGuest = user == null || user.IsAnonymous;
            gameSettingsAvatarFallback.SetActive(isGuest);
            gameSettingsAvatarImage.gameObject.SetActive(!isGuest);
            if (isGuest)
            {
                settingsAvatarLoadedUrl = string.Empty;
                return;
            }

            var localTexture = Resources.Load<Texture2D>("UI/Avatars/" + bootstrapState.SelectedCharacter);
            if (localTexture == null)
                localTexture = Resources.Load<Texture2D>("UI/Characters/" + bootstrapState.SelectedCharacter);
            if (localTexture != null && string.IsNullOrWhiteSpace(settingsAvatarLoadedUrl))
            {
                gameSettingsAvatarImage.texture = localTexture;
                gameSettingsAvatarImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            }

            var url = BestProfilePhotoUrl(user);
            if (string.IsNullOrWhiteSpace(url) || string.Equals(settingsAvatarLoadedUrl, url, StringComparison.Ordinal))
                return;

            settingsAvatarRequestVersion++;
            _ = LoadSettingsProfilePictureAsync(url, user.UserId, settingsAvatarRequestVersion);
        }

        private async Task LoadSettingsProfilePictureAsync(string url, string expectedUserId, int requestVersion)
        {
            try
            {
                using var request = UnityWebRequestTexture.GetTexture(url);
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success || auth == null || auth.CurrentUser == null
                    || auth.CurrentUser.UserId != expectedUserId || requestVersion != settingsAvatarRequestVersion
                    || gameSettingsAvatarImage == null)
                    return;

                gameSettingsAvatarImage.texture = DownloadHandlerTexture.GetContent(request);
                gameSettingsAvatarImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                settingsAvatarLoadedUrl = url;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Settings profile image could not be loaded: " + exception.Message);
            }
        }

        private void BeginSettingsProviderLink(string provider)
        {
            if (settingsProviderLinkInProgress)
                return;
            settingsProviderLinkInProgress = true;
            pendingSettingsProvider = provider;
            if (gameSettingsLinkStatusText != null)
            {
                gameSettingsLinkStatusText.text = "Connecting " + provider.ToUpperInvariant() + "…";
                gameSettingsLinkStatusText.color = SettingsCyan;
            }
            RefreshSettingsProviderButtons(auth != null ? auth.CurrentUser : null);
        }

        private void EndSettingsProviderLink(bool success, string message)
        {
            settingsProviderLinkInProgress = false;
            pendingSettingsProvider = string.Empty;
            if (gameSettingsLinkStatusText != null)
            {
                gameSettingsLinkStatusText.text = message ?? string.Empty;
                gameSettingsLinkStatusText.color = success ? SettingsGreen : Coral;
            }
            RefreshSettingsUI();
        }

        private async void LogoutFromGame()
        {
            CloseGameSettings();
            settingsAvatarLoadedUrl = string.Empty;
            if (auth != null)
                auth.SignOut();
            SetAuthMessage("Signed out safely. Choose how you want to return.", Cyan);
            await ShowScreenAsync("Auth");
        }

        private void OnSettingsFacebookPressed()
        {
            BeginSettingsProviderLink("facebook");
            OnFacebookPressed();
        }

        private void OnSettingsGooglePressed()
        {
            BeginSettingsProviderLink("google");
            OnGooglePressed();
        }

        private void OnSettingsApplePressed()
        {
            BeginSettingsProviderLink("apple");
            OnApplePressed();
        }

        private static string ProviderBadge(FirebaseUser user)
        {
            if (HasLinkedProvider(user, GoogleAuthProvider.ProviderId)) return "G";
            if (HasLinkedProvider(user, FacebookAuthProvider.ProviderId)) return "f";
            if (HasLinkedProvider(user, "apple.com")) return "A";
            return "✓";
        }

        private static Color ProviderBadgeColor(FirebaseUser user)
        {
            if (HasLinkedProvider(user, GoogleAuthProvider.ProviderId)) return Hex("2D6CDF");
            if (HasLinkedProvider(user, FacebookAuthProvider.ProviderId)) return Hex("1877F2");
            if (HasLinkedProvider(user, "apple.com")) return Hex("111111");
            return Hex("1C2A48");
        }
    }
}
