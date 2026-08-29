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
using Firebase.Database;
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
        private async Task InitializeFirebaseAsync()
        {
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus != DependencyStatus.Available)
                throw new InvalidOperationException("Firebase dependencies are unavailable: " + dependencyStatus);

            var firebaseApp = FirebaseApp.DefaultInstance;
            auth = FirebaseAuth.DefaultInstance;
            // The project id is fixed by google-services.json. If the Realtime Database is
            // created in a non-default region, only this URL needs to be changed.
            realtimeDatabase = FirebaseDatabase.GetInstance(firebaseApp,
                "https://gravity-half-dead-default-rtdb.firebaseio.com");
            realtimeDatabase.SetPersistenceEnabled(true);
            firestore = FirebaseFirestore.DefaultInstance;
        }

        private async Task BootstrapAndEnterGameAsync()
        {
            await firebaseReadyTask;
            var user = auth.CurrentUser;
            if (user == null)
            {
                await ShowScreenAsync("Auth");
                return;
            }

            try
            {
                await user.ReloadAsync();
                if (auth.CurrentUser != null)
                    user = auth.CurrentUser;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Firebase profile refresh delayed: " + exception.Message);
            }

            SetSplashProgress(0.12f, "Authenticating pilot");
            await Delay(260);

            var playerReference = firestore.Collection("users").Document(user.UserId);
            var snapshot = await playerReference.GetSnapshotAsync();
            SetSplashProgress(0.38f, "Syncing progress and inventory");

            IDictionary<string, object> playerData;
            if (!snapshot.Exists)
            {
                var newPlayer = CreateDefaultPlayer(user);
                await playerReference.SetAsync(newPlayer);
                playerData = newPlayer;
                bootstrapState = BootstrapState.FromDictionary(newPlayer, user);
            }
            else
            {
                playerData = snapshot.ToDictionary();
                bootstrapState = BootstrapState.FromDictionary(playerData, user);
            }

            // A signed-in account carries these preferences across devices. Missing values fall
            // back to the local defaults: NORMAL render quality and 60 FPS.
            ApplyCloudSettings(playerData);

            var loginUpdate = new Dictionary<string, object>
            {
                { "last_login", FieldValue.ServerTimestamp },
                { "auth_type", user.IsAnonymous ? "guest" : ProviderName(user) },
                { "is_guest", user.IsAnonymous },
                { "device_id_hash", user.IsAnonymous ? HashedDeviceId() : string.Empty },
                { "email", SafeString(user.Email) },
                { "display_name", SafeString(user.DisplayName) },
                { "photo_url", BestProfilePhotoUrl(user) },
                { "linked_providers", LinkedProviderFirestoreValues(user) },
                { "settings", BuildSettingsMap() }
            };

            if (!playerData.ContainsKey("total_play_seconds")) loginUpdate["total_play_seconds"] = 0L;
            if (!playerData.ContainsKey("max_coins_single_run")) loginUpdate["max_coins_single_run"] = 0L;
            if (!playerData.ContainsKey("lifetime_coins_collected")) loginUpdate["lifetime_coins_collected"] = 0L;
            if (!playerData.ContainsKey("total_distance_meters")) loginUpdate["total_distance_meters"] = 0L;
            if (!playerData.ContainsKey("selected_frame")) loginUpdate["selected_frame"] = "neon_recruit";
            if (!playerData.ContainsKey("robot_shards")) loginUpdate["robot_shards"] = 0L;
            if (!playerData.ContainsKey("ice_shards")) loginUpdate["ice_shards"] = 0L;
            if (!playerData.ContainsKey("disc_shards")) loginUpdate["disc_shards"] = 0L;
            if (!playerData.ContainsKey("completed_runs")) loginUpdate["completed_runs"] = 0L;
            if (!playerData.ContainsKey("completed_missions")) loginUpdate["completed_missions"] = 0L;
            if (!playerData.ContainsKey("gravity_cores")) loginUpdate["gravity_cores"] = 0L;
            var canonicalMultiplier = ScoreMultiplierForCompletedMissions(bootstrapState.CompletedMissions);
            bootstrapState.ScoreMultiplier = canonicalMultiplier;
            if (!playerData.ContainsKey("score_multiplier")
                || DatabaseLong(playerData["score_multiplier"], 1L) != canonicalMultiplier)
                loginUpdate["score_multiplier"] = canonicalMultiplier;
            if (!playerData.ContainsKey("consecutive_login_days")) loginUpdate["consecutive_login_days"] = 1L;
            if (!playerData.ContainsKey("has_made_purchase")) loginUpdate["has_made_purchase"] = false;
            if (!playerData.ContainsKey("unlocked_characters"))
                loginUpdate["unlocked_characters"] = new List<object> { "nova" };
            if (!playerData.ContainsKey("selected_disc")) loginUpdate["selected_disc"] = "core_runner";
            if (!playerData.ContainsKey("unlocked_discs"))
                loginUpdate["unlocked_discs"] = new List<object> { "core_runner" };
            if (!playerData.ContainsKey("powerups")) loginUpdate["powerups"] = PowerupDictionary();
            if (!playerData.ContainsKey("booster_inventory"))
                loginUpdate["booster_inventory"] = DefaultBoosterInventoryFirestoreMap();
            if (!playerData.ContainsKey("daily_rewarded_ads"))
                loginUpdate["daily_rewarded_ads"] = DefaultDailyAdsFirestoreMap();

            // Persist the country chosen during onboarding so it appears in the
            // Firestore player document even if the user navigates away before
            // the async sync completes.
            var localCountryCode = PlayerPrefs.GetString("ghd.country_code", "");
            var localCountryName = PlayerPrefs.GetString("ghd.country_name", "");
            if (!string.IsNullOrEmpty(localCountryCode) && !playerData.ContainsKey("country_code"))
            {
                loginUpdate["country_code"] = localCountryCode;
                loginUpdate["country_name"] = localCountryName;
            }

            await playerReference.SetAsync(loginUpdate, SetOptions.MergeAll);

            SetSplashProgress(0.62f, "Checking purchases and ad access");
            await Delay(320);

            try
            {
                var configSnapshot = await firestore.Collection("gameConfig").Document("bootstrap").GetSnapshotAsync();
                if (configSnapshot.Exists)
                    bootstrapState.ApplyGameConfig(configSnapshot.ToDictionary());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Remote bootstrap config unavailable; using safe defaults. " + exception.Message);
            }

            SetSplashProgress(0.83f, "Aligning chapters and daily rewards");
            await Delay(380);

            bootstrapState.AppVersion = Application.version;
            bootstrapState.ServerReadyAtUtc = DateTime.UtcNow;
            ApplyAndroidNotificationPreference();
            SetSplashProgress(1f, "The breach is open");
            await Delay(420);

            PopulateGameScreen(user);
            await StartPowerupRealtimeSyncAsync();
            await StartCharacterRealtimeSyncAsync();
            await StartDiscRealtimeSyncAsync();
            await StartShopRealtimeSyncAsync();
            await StartMissionRealtimeSyncAsync();
            await StartAchievementRealtimeSyncAsync();
            StartHomePlayerFirestoreSync();
            await ShowScreenAsync("Game");
        }

        private Dictionary<string, object> CreateDefaultPlayer(FirebaseUser user)
        {
            return new Dictionary<string, object>
            {
                { "player_id", "GHD-" + user.UserId[..Mathf.Min(6, user.UserId.Length)].ToUpperInvariant() },
                { "auth_type", user.IsAnonymous ? "guest" : ProviderName(user) },
                { "is_guest", user.IsAnonymous },
                { "device_id_hash", user.IsAnonymous ? HashedDeviceId() : string.Empty },
                { "email", SafeString(user.Email) },
                { "display_name", SafeString(user.DisplayName) },
                { "photo_url", BestProfilePhotoUrl(user) },
                { "linked_providers", LinkedProviderFirestoreValues(user) },
                { "age_confirmed", true },
                { "age_group", PlayerPrefs.GetString(AgeGroupKey, "18_plus") },
                { "coins", 0L },
                { "gravity_cores", 0L },
                { "current_chapter", 1L },
                { "current_level", 1L },
                { "endless_high_score", 0L },
                { "score_multiplier", 1L },
                { "completed_missions", 0L },
                { "total_play_seconds", 0L },
                { "max_coins_single_run", 0L },
                { "lifetime_coins_collected", 0L },
                { "total_distance_meters", 0L },
                { "completed_runs", 0L },
                { "consecutive_login_days", 1L },
                { "robot_shards", 0L },
                { "ice_shards", 0L },
                { "disc_shards", 0L },
                { "has_made_purchase", false },
                { "selected_character", "nova" },
                { "selected_frame", "neon_recruit" },
                { "unlocked_characters", new List<object> { "nova" } },
                { "selected_disc", "core_runner" },
                { "unlocked_discs", new List<object> { "core_runner" } },
                { "powerups", new Dictionary<string, object>
                    {
                        { "magnet", 0L },
                        { "speed_boost", 0L },
                        { "shield", 0L },
                        { "invulnerability", 0L },
                        { "wall_walk", 0L },
                        { "timezone", 0L }
                    }
                },
                { "booster_inventory", DefaultBoosterInventoryFirestoreMap() },
                { "daily_rewarded_ads", DefaultDailyAdsFirestoreMap() },
                { "remove_ads", false },
                { "country_code", PlayerPrefs.GetString("ghd.country_code", "") },
                { "country_name", PlayerPrefs.GetString("ghd.country_name", "") },
                { "settings", BuildSettingsMap() },
                { "created_at", FieldValue.ServerTimestamp },
                { "last_login", FieldValue.ServerTimestamp }
            };
        }

        private Dictionary<string, object> BuildSettingsMap()
        {
            return new Dictionary<string, object>
            {
                { "music", musicEnabled },
                { "sfx", sfxEnabled },
                { "vibration", vibrationEnabled },
                { "notifications", notificationsEnabled },
                { "resolution_quality", resolutionQualityIndex },
                { "resolution_label", ResolutionQualityNames[Mathf.Clamp(resolutionQualityIndex, 0, 2)].ToLowerInvariant() },
                { "fps", targetFps }
            };
        }

        private async Task SyncSettingsToFirebaseAsync()
        {
            if (firestore == null || auth == null || auth.CurrentUser == null)
                return;

            try
            {
                await firestore.Collection("users").Document(auth.CurrentUser.UserId).SetAsync(
                    new Dictionary<string, object>
                    {
                        { "settings", BuildSettingsMap() },
                        { "settings_updated_at", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Settings sync delayed: " + exception.Message);
            }
        }

        private async Task SyncAuthProfileToFirebaseAsync(FirebaseUser user)
        {
            if (user == null)
                return;
            if (firestore == null)
            {
                try
                {
                    await firebaseReadyTask;
                }
                catch
                {
                    return;
                }
            }
            if (firestore == null)
                return;

            try
            {
                await firestore.Collection("users").Document(user.UserId).SetAsync(
                    new Dictionary<string, object>
                    {
                        { "auth_type", user.IsAnonymous ? "guest" : ProviderName(user) },
                        { "is_guest", user.IsAnonymous },
                        { "email", SafeString(user.Email) },
                        { "display_name", SafeString(user.DisplayName) },
                        { "photo_url", BestProfilePhotoUrl(user) },
                        { "linked_providers", LinkedProviderFirestoreValues(user) },
                        { "auth_profile_updated_at", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Auth profile sync delayed: " + exception.Message);
            }
        }

        private static string SafeString(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value;

        private IEnumerator TrackPlaytime()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(1f);
                if (!screens.TryGetValue("Game", out var gameScreen) || !gameScreen.gameObject.activeInHierarchy
                    || auth == null || auth.CurrentUser == null)
                    continue;

                bootstrapState.TotalPlaySeconds++;
                pendingPlaySeconds++;
                if (gameMeContent != null && gameMeContent.activeInHierarchy)
                    RefreshMeProfileUI();
                if (pendingPlaySeconds >= 60L)
                    _ = SyncPendingPlaytimeAsync();
            }
        }

        private async Task SyncPendingPlaytimeAsync()
        {
            if (playtimeSyncInFlight || pendingPlaySeconds <= 0L || firestore == null
                || auth == null || auth.CurrentUser == null)
                return;

            playtimeSyncInFlight = true;
            var seconds = pendingPlaySeconds;
            pendingPlaySeconds = 0L;
            try
            {
                await firestore.Collection("users").Document(auth.CurrentUser.UserId).SetAsync(
                    new Dictionary<string, object>
                    {
                        { "total_play_seconds", FieldValue.Increment(seconds) },
                        { "statistics_updated_at", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                pendingPlaySeconds += seconds;
                Debug.LogWarning("Playtime sync delayed: " + exception.Message);
            }
            finally
            {
                playtimeSyncInFlight = false;
            }
        }

        public async void RecordRunStatistics(long score, long coinsCollected, float distanceMeters)
        {
            var walletCoins = Math.Max(0L, coinsCollected);
            bootstrapState.EndlessHighScore = Math.Max(bootstrapState.EndlessHighScore, score);
            bootstrapState.MaxCoinsSingleRun = Math.Max(bootstrapState.MaxCoinsSingleRun, coinsCollected);
            bootstrapState.LifetimeCoinsCollected += walletCoins;
            bootstrapState.Coins += walletCoins;
            bootstrapState.TotalDistanceMeters += Math.Max(0L, (long)Math.Round(distanceMeters));
            bootstrapState.CompletedRuns++;
            RefreshMeProfileUI();
            RefreshPowerupUI();
            RefreshHomeHeaderDynamicValues();
            _ = AddRealtimeCoinsAsync(walletCoins);
            _ = SetRealtimeHighScoreAsync(bootstrapState.EndlessHighScore);
            ReportBasicRunMissionProgress(score, walletCoins,
                Math.Max(0L, (long)Math.Round(distanceMeters)));
            RecordAchievementRunTotals(walletCoins, Math.Max(0L, (long)Math.Round(distanceMeters)));

            if (firestore == null || auth == null || auth.CurrentUser == null)
                return;
            try
            {
                await firestore.Collection("users").Document(auth.CurrentUser.UserId).SetAsync(
                    new Dictionary<string, object>
                    {
                        { "endless_high_score", bootstrapState.EndlessHighScore },
                        { "coins", bootstrapState.Coins },
                        { "max_coins_single_run", bootstrapState.MaxCoinsSingleRun },
                        { "lifetime_coins_collected", bootstrapState.LifetimeCoinsCollected },
                        { "total_distance_meters", bootstrapState.TotalDistanceMeters },
                        { "completed_runs", bootstrapState.CompletedRuns },
                        { "statistics_updated_at", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Run statistics sync delayed: " + exception.Message);
            }
        }

        private static string FormatPlayTime(long seconds)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(0L, seconds));
            return duration.TotalHours >= 1d
                ? ((int)duration.TotalHours) + "h " + duration.Minutes + "m"
                : duration.Minutes + "m " + duration.Seconds + "s";
        }

        private static string FormatDistance(long meters)
        {
            return meters >= 1000L ? (meters / 1000f).ToString("0.0") + " km" : meters + " m";
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _ = SyncPendingPlaytimeAsync();
                _ = SyncSettingsToFirebaseAsync();
            }
        }

        private void OnApplicationQuit()
        {
            StopPowerupRealtimeSync();
            StopCharacterRealtimeSync();
            StopDiscRealtimeSync();
            StopShopRealtimeSync();
            StopMissionRealtimeSync();
            StopAchievementRealtimeSync();
            StopHomePlayerFirestoreSync();
            StopStoreCatalogSync();
            _ = SyncPendingPlaytimeAsync();
            _ = SyncSettingsToFirebaseAsync();
        }
    }
}
