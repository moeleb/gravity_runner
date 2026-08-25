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
        private sealed class BootstrapState
        {
            public string PlayerId = "GHD-NEW";
            public long GravityCores;
            public long Coins;
            public long CurrentChapter = 1;
            public long CurrentLevel = 1;
            public long EndlessHighScore;
            public long ScoreMultiplier = 1L;
            public long CompletedMissions;
            public long TotalPlaySeconds;
            public long MaxCoinsSingleRun;
            public long LifetimeCoinsCollected;
            public long TotalDistanceMeters;
            public long CompletedRuns;
            public long ConsecutiveLoginDays = 1;
            public long RobotShards;
            public long IceShards;
            public bool RemoveAds;
            public bool HasMadePurchase;
            public string SelectedCharacter = "nova";
            public string SelectedFrame = "neon_recruit";
            public readonly HashSet<string> UnlockedCharacters = new() { "nova" };
            public readonly int[] PowerupLevels = new int[PowerupIds.Length];
            public string CountryCode = "";
            public string CountryName = "";
            public string MinimumVersion = "0.1.0";
            public bool Maintenance;
            public string AppVersion;
            public DateTime ServerReadyAtUtc;

            public static BootstrapState FromDictionary(IDictionary<string, object> values, FirebaseUser user)
            {
                var state = new BootstrapState
                {
                    PlayerId = StringValue(values, "player_id", "GHD-" + user.UserId[..Mathf.Min(6, user.UserId.Length)].ToUpperInvariant()),
                    GravityCores = Math.Max(0L, LongValue(values, "gravity_cores", 0L)),
                    Coins = LongValue(values, "coins", 0),
                    CurrentChapter = LongValue(values, "current_chapter", 1),
                    CurrentLevel = LongValue(values, "current_level", 1),
                    EndlessHighScore = LongValue(values, "endless_high_score", 0),
                    CompletedMissions = Math.Max(0L, LongValue(values, "completed_missions", 0L)),
                    TotalPlaySeconds = LongValue(values, "total_play_seconds", 0),
                    MaxCoinsSingleRun = LongValue(values, "max_coins_single_run", 0),
                    LifetimeCoinsCollected = LongValue(values, "lifetime_coins_collected", 0),
                    TotalDistanceMeters = LongValue(values, "total_distance_meters", 0),
                    CompletedRuns = LongValue(values, "completed_runs", 0),
                    ConsecutiveLoginDays = LongValue(values, "consecutive_login_days", 1),
                    RobotShards = LongValue(values, "robot_shards", 0),
                    IceShards = LongValue(values, "ice_shards", 0),
                    RemoveAds = BoolValue(values, "remove_ads", false),
                    HasMadePurchase = BoolValue(values, "has_made_purchase", false),
                    SelectedCharacter = StringValue(values, "selected_character", "nova"),
                    SelectedFrame = StringValue(values, "selected_frame", "neon_recruit"),
                    CountryCode = StringValue(values, "country_code", ""),
                    CountryName = StringValue(values, "country_name", "")
                };
                state.ScoreMultiplier = ScoreMultiplierForCompletedMissions(state.CompletedMissions);
                ApplyUnlockedCharacters(state, values);
                ApplyPowerupLevels(state, values);
                if (!state.UnlockedCharacters.Contains(state.SelectedCharacter))
                    state.SelectedCharacter = "nova";
                return state;
            }

            public void ApplyGameConfig(IDictionary<string, object> values)
            {
                MinimumVersion = StringValue(values, "minimum_version", MinimumVersion);
                Maintenance = BoolValue(values, "maintenance", false);
            }

            private static string StringValue(IDictionary<string, object> values, string key, string fallback)
                => values.TryGetValue(key, out var value) && value != null ? value.ToString() : fallback;

            private static long LongValue(IDictionary<string, object> values, string key, long fallback)
                => values.TryGetValue(key, out var value) && value is long number ? number : fallback;

            private static bool BoolValue(IDictionary<string, object> values, string key, bool fallback)
                => values.TryGetValue(key, out var value) && value is bool flag ? flag : fallback;

            private static void ApplyUnlockedCharacters(BootstrapState state, IDictionary<string, object> values)
            {
                if (!values.TryGetValue("unlocked_characters", out var raw) || raw is string
                    || raw is not System.Collections.IEnumerable collection)
                    return;
                foreach (var value in collection)
                {
                    var id = value?.ToString();
                    if (!string.IsNullOrWhiteSpace(id) && Array.IndexOf(CharacterIds, id) >= 0)
                        state.UnlockedCharacters.Add(id);
                }
            }

            private static void ApplyPowerupLevels(BootstrapState state, IDictionary<string, object> values)
            {
                if (!values.TryGetValue("powerups", out var raw)
                    || raw is not IDictionary<string, object> powerups)
                    return;
                for (var i = 0; i < PowerupIds.Length; i++)
                {
                    var level = LongValue(powerups, PowerupIds[i], 0L);
                    state.PowerupLevels[i] = Mathf.Clamp((int)level, 0, PowerupMaxLevel(i));
                }
            }
        }
    }
}
