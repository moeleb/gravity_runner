using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private Text gameGravityCoreAmountText;
        private int gravityCoreRevivesThisRun;
        private bool gravityCoreReviveInFlight;

        /// <summary>
        /// The number of Gravity Cores required for the next revive in the current run.
        /// The sequence resets when a new run begins: 1, 2, 4, 8, 16, 32, ...
        /// </summary>
        public long NextGravityCoreReviveCost => GravityCoreCostForRevive(gravityCoreRevivesThisRun);

        public static long GravityCoreCostForRevive(int successfulRevivesThisRun)
        {
            var safeRevives = Math.Max(0, successfulRevivesThisRun);
            // A signed 64-bit balance cannot represent 2^63. Saturating prevents overflow
            // if a test session somehow reaches an extreme number of revives.
            return safeRevives >= 62 ? long.MaxValue : 1L << safeRevives;
        }

        /// <summary>
        /// Starts the revive-cost sequence for a fresh run. Gameplay may also call this
        /// explicitly if a run starts somewhere other than the home Tap To Play button.
        /// </summary>
        public void ResetGravityCoreReviveEscalation()
        {
            gravityCoreRevivesThisRun = 0;
        }

        private void BeginGravityCoreRun()
        {
            ResetGravityCoreReviveEscalation();
        }

        public bool CanUseGravityCoreRevive()
        {
            return !gravityCoreReviveInFlight
                && realtimePowerupPlayerReference != null
                && auth != null
                && auth.CurrentUser != null
                && bootstrapState.GravityCores >= NextGravityCoreReviveCost;
        }

        /// <summary>
        /// Atomically consumes the correct number of owned Gravity Cores for a revive.
        /// Gameplay should resume the player only when this method returns true.
        /// A failed/insufficient transaction does not advance the 1, 2, 4, 8 sequence.
        /// </summary>
        public async Task<bool> TryUseGravityCoreReviveAsync()
        {
            if (gravityCoreReviveInFlight || realtimePowerupPlayerReference == null
                || auth == null || auth.CurrentUser == null)
                return false;

            var expectedUserId = auth.CurrentUser.UserId;
            var cost = NextGravityCoreReviveCost;
            var transactionAccepted = false;
            gravityCoreReviveInFlight = true;
            try
            {
                var result = await realtimePowerupPlayerReference.Child("gravityCores")
                    .RunTransaction(mutableCores =>
                {
                    transactionAccepted = false;
                    var owned = Math.Max(0L, DatabaseLong(mutableCores.Value, bootstrapState.GravityCores));
                    if (owned < cost)
                        return TransactionResult.Abort();

                    mutableCores.Value = owned - cost;
                    transactionAccepted = true;
                    return TransactionResult.Success(mutableCores);
                }, true);

                if (!transactionAccepted || auth == null || auth.CurrentUser == null
                    || auth.CurrentUser.UserId != expectedUserId)
                    return false;

                bootstrapState.GravityCores = result != null && result.Exists
                    ? Math.Max(0L, DatabaseLong(result.Value, bootstrapState.GravityCores - cost))
                    : Math.Max(0L, bootstrapState.GravityCores - cost);
                gravityCoreRevivesThisRun++;
                RefreshGravityCoreHeader();
                RecordAchievementCrystalRevive();
                _ = MirrorGravityCoresToFirestoreAsync(expectedUserId);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Gravity Core revive could not be synchronized: " + exception.Message);
                return false;
            }
            finally
            {
                gravityCoreReviveInFlight = false;
            }
        }

        private void RefreshGravityCoreHeader()
        {
            if (gameGravityCoreAmountText != null)
                gameGravityCoreAmountText.text = Math.Max(0L, bootstrapState.GravityCores).ToString("N0");
        }

        private async Task MirrorGravityCoresToFirestoreAsync(string expectedUserId)
        {
            if (firestore == null || auth == null || auth.CurrentUser == null
                || auth.CurrentUser.UserId != expectedUserId)
                return;

            try
            {
                await firestore.Collection("users").Document(expectedUserId).SetAsync(
                    new Dictionary<string, object>
                    {
                        { "gravity_cores", Math.Max(0L, bootstrapState.GravityCores) },
                        { "gravity_cores_updated_at", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                // Realtime Database is authoritative for spending. A delayed Firestore mirror
                // must never block a successful revive.
                Debug.LogWarning("Gravity Core Firestore mirror delayed: " + exception.Message);
            }
        }
    }
}
