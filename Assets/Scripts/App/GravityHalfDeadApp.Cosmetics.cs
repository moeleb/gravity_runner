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
        public async void CollectCharacterCurrency(string currencyId, long amount)
        {
            amount = Math.Max(0L, amount);
            if (currencyId == "robot_shard")
                bootstrapState.RobotShards += amount;
            else if (currencyId == "ice_shard")
                bootstrapState.IceShards += amount;
            else
                return;
            RefreshMeProfileUI();
            if (firestore == null || auth == null || auth.CurrentUser == null)
                return;
            await firestore.Collection("users").Document(auth.CurrentUser.UserId).SetAsync(
                new Dictionary<string, object>
                {
                { "robot_shards", bootstrapState.RobotShards },
                { "ice_shards", bootstrapState.IceShards },
                { "currency_updated_at", FieldValue.ServerTimestamp }
                }, SetOptions.MergeAll);
        }

        private void SelectFrame(string frameId)
        {
            if (!IsFrameUnlocked(frameId))
                return;
            bootstrapState.SelectedFrame = frameId;
            RefreshMeProfileUI();
            RefreshTopPlayerAvatar(auth != null ? auth.CurrentUser : null);
            _ = SaveProfileCosmeticsAsync();
        }

        private bool IsFrameUnlocked(string frameId)
        {
            if (frameId == "neon_recruit")
                return true;
            if (frameId == "void_runner")
                return bootstrapState.TotalDistanceMeters >= 25000L;
            return bootstrapState.TotalPlaySeconds >= 18000L || bootstrapState.TotalDistanceMeters >= 100000L;
        }

        private async Task SaveProfileCosmeticsAsync()
        {
            if (firestore == null || auth == null || auth.CurrentUser == null)
                return;
            try
            {
                await firestore.Collection("users").Document(auth.CurrentUser.UserId).SetAsync(
                    new Dictionary<string, object>
                    {
                    { "selected_character", bootstrapState.SelectedCharacter },
                    { "selected_frame", bootstrapState.SelectedFrame },
                    { "coins", bootstrapState.Coins },
                    { "robot_shards", bootstrapState.RobotShards },
                    { "ice_shards", bootstrapState.IceShards },
                    { "has_made_purchase", bootstrapState.HasMadePurchase },
                    { "unlocked_characters", UnlockedCharacterValues() },
                    { "profile_updated_at", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not save profile cosmetics: " + exception.Message);
            }
        }

        private List<object> UnlockedCharacterValues()
        {
            var values = new List<object>();
            foreach (var characterId in CharacterIds)
            {
                if (IsCharacterUnlocked(characterId))
                    values.Add(characterId);
            }
            return values;
        }

        // The store controller calls this only after any purchase has been confirmed.
        public async void RegisterSuccessfulPurchase()
        {
            bootstrapState.HasMadePurchase = true;
            bootstrapState.UnlockedCharacters.Add("raze");
            RefreshMeProfileUI();
            await SaveProfileCosmeticsAsync();
        }
    }
}
