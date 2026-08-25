using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private ListenerRegistration homePlayerFirestoreListener;
        private string homePlayerFirestoreUserId = string.Empty;
        private const long MissionsPerMultiplierLevel = 3L;
        private const long MaximumScoreMultiplier = 30L;

        private void StartHomePlayerFirestoreSync()
        {
            StopHomePlayerFirestoreSync();
            if (firestore == null || auth == null || auth.CurrentUser == null)
                return;

            homePlayerFirestoreUserId = auth.CurrentUser.UserId;
            homePlayerFirestoreListener = firestore.Collection("users")
                .Document(homePlayerFirestoreUserId).Listen(snapshot =>
                {
                    if (snapshot == null || !snapshot.Exists || auth == null || auth.CurrentUser == null
                        || auth.CurrentUser.UserId != homePlayerFirestoreUserId)
                        return;

                    var values = snapshot.ToDictionary();
                    values.TryGetValue("completed_missions", out var completedMissionsValue);
                    bootstrapState.CompletedMissions = Math.Max(0L,
                        DatabaseLong(completedMissionsValue, bootstrapState.CompletedMissions));
                    bootstrapState.ScoreMultiplier =
                        ScoreMultiplierForCompletedMissions(bootstrapState.CompletedMissions);
                    RefreshHomeHeaderDynamicValues();

                    values.TryGetValue("score_multiplier", out var storedMultiplierValue);
                    var storedMultiplier = DatabaseLong(storedMultiplierValue, 1L);
                    if (storedMultiplier != bootstrapState.ScoreMultiplier)
                        _ = PersistCanonicalScoreMultiplierAsync(homePlayerFirestoreUserId,
                            bootstrapState.ScoreMultiplier);
                });
        }

        private void StopHomePlayerFirestoreSync()
        {
            homePlayerFirestoreListener?.Stop();
            homePlayerFirestoreListener = null;
            homePlayerFirestoreUserId = string.Empty;
        }

        private void RefreshHomeHeaderDynamicValues()
        {
            if (gameHighScoreValueText != null)
                gameHighScoreValueText.text = Math.Max(0L, bootstrapState.EndlessHighScore).ToString("N0");
            if (gameMultiplierText != null)
                gameMultiplierText.text = "x" + Math.Min(MaximumScoreMultiplier,
                    Math.Max(1L, bootstrapState.ScoreMultiplier)).ToString("N0");
            RefreshGravityCoreHeader();
            RefreshMissionScoreText();
        }

        /// <summary>
        /// Mission controllers should call this exactly once after a mission is successfully completed.
        /// Firestore's atomic increment keeps the counter safe if completions arrive close together.
        /// </summary>
        public async Task RecordMissionCompletedAsync()
        {
            if (firestore == null || auth == null || auth.CurrentUser == null)
                throw new InvalidOperationException("A signed-in Firebase player is required to record a mission.");

            var userId = auth.CurrentUser.UserId;
            var playerReference = firestore.Collection("users").Document(userId);
            await playerReference.SetAsync(new Dictionary<string, object>
            {
                { "completed_missions", FieldValue.Increment(1L) },
                { "mission_progress_updated_at", FieldValue.ServerTimestamp }
            }, SetOptions.MergeAll);
        }

        public static long ScoreMultiplierForCompletedMissions(long completedMissions)
        {
            var safeMissionCount = Math.Max(0L, completedMissions);
            return Math.Min(MaximumScoreMultiplier,
                Math.Max(1L, 1L + safeMissionCount / MissionsPerMultiplierLevel));
        }

        private async Task PersistCanonicalScoreMultiplierAsync(string userId, long multiplier)
        {
            if (firestore == null || auth == null || auth.CurrentUser == null
                || auth.CurrentUser.UserId != userId)
                return;

            try
            {
                await firestore.Collection("users").Document(userId).SetAsync(
                    new Dictionary<string, object>
                    {
                        { "score_multiplier", Math.Min(MaximumScoreMultiplier, Math.Max(1L, multiplier)) }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Score multiplier could not be synchronized: " + exception.Message);
            }
        }
    }
}
