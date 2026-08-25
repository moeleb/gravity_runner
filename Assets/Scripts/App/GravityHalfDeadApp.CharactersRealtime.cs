using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Firestore;
using UnityEngine;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private DatabaseReference realtimeCharacterPlayerReference;
        private string realtimeCharacterUserId = string.Empty;
        private bool characterPurchaseInFlight;

        private async Task StartCharacterRealtimeSyncAsync()
        {
            StopCharacterRealtimeSync();
            if (realtimeDatabase == null || auth == null || auth.CurrentUser == null)
                return;

            realtimeCharacterUserId = auth.CurrentUser.UserId;
            realtimeCharacterPlayerReference = realtimeDatabase.RootReference
                .Child("players").Child(realtimeCharacterUserId);
            realtimeCharacterPlayerReference.ValueChanged += HandleCharacterRealtimeValueChanged;

            try
            {
                var snapshot = await realtimeCharacterPlayerReference.GetValueAsync();
                if (!snapshot.Child("characters").Exists)
                    await SeedCharacterRealtimeStateAsync();
                else
                    ApplyCharacterRealtimeSnapshot(snapshot);
            }
            catch (Exception exception)
            {
                SetCharacterStatus("OFFLINE CHARACTER STATE · RETRYING SYNC", Muted);
                Debug.LogWarning("Realtime character initialization delayed: " + exception.Message);
            }
        }

        private void StopCharacterRealtimeSync()
        {
            if (realtimeCharacterPlayerReference != null)
                realtimeCharacterPlayerReference.ValueChanged -= HandleCharacterRealtimeValueChanged;
            realtimeCharacterPlayerReference = null;
            realtimeCharacterUserId = string.Empty;
        }

        private async Task SeedCharacterRealtimeStateAsync()
        {
            if (realtimeCharacterPlayerReference == null)
                return;

            var selectedIndex = Array.IndexOf(CharacterIds, bootstrapState.SelectedCharacter);
            var seedSelectedCharacter = selectedIndex >= 0 && selectedIndex < CharacterScreenCount
                && IsCharacterUnlocked(bootstrapState.SelectedCharacter)
                ? bootstrapState.SelectedCharacter : "nova";
            var updates = new Dictionary<string, object>
            {
                { "selectedCharacter", seedSelectedCharacter },
                { "updatedAt", ServerValue.Timestamp }
            };
            for (var i = 0; i < CharacterScreenCount; i++)
            {
                var id = CharacterIds[i];
                var unlocked = IsCharacterUnlocked(id);
                updates["characters/" + id + "/name"] = CharacterNames[i];
                updates["characters/" + id + "/unlocked"] = unlocked;
                updates["characters/" + id + "/unlockCost"] = id == "nova" ? 0L : CharacterUnlockCoinCost;
                updates["characters/" + id + "/updatedAt"] = ServerValue.Timestamp;
            }
            await realtimeCharacterPlayerReference.UpdateChildrenAsync(updates);
        }

        private void HandleCharacterRealtimeValueChanged(object sender, ValueChangedEventArgs args)
        {
            if (args.DatabaseError != null)
            {
                SetCharacterStatus("CHARACTER SYNC ERROR · USING LOCAL STATE", Hex("FF82C8"));
                Debug.LogWarning("Realtime character listener: " + args.DatabaseError.Message);
                return;
            }
            if (args.Snapshot == null || !args.Snapshot.Exists || auth == null || auth.CurrentUser == null
                || auth.CurrentUser.UserId != realtimeCharacterUserId)
                return;
            ApplyCharacterRealtimeSnapshot(args.Snapshot);
        }

        private void ApplyCharacterRealtimeSnapshot(DataSnapshot snapshot)
        {
            var selectedCharacterChanged = false;
            var coinSnapshot = snapshot.Child("coins");
            if (coinSnapshot.Exists)
                bootstrapState.Coins = Math.Max(0L, DatabaseLong(coinSnapshot.Value, bootstrapState.Coins));

            var charactersSnapshot = snapshot.Child("characters");
            if (charactersSnapshot.Exists)
            {
                bootstrapState.UnlockedCharacters.Clear();
                bootstrapState.UnlockedCharacters.Add("nova");
                for (var i = 0; i < CharacterScreenCount; i++)
                {
                    var unlockedSnapshot = charactersSnapshot.Child(CharacterIds[i] + "/unlocked");
                    if (unlockedSnapshot.Exists && DatabaseBool(unlockedSnapshot.Value))
                        bootstrapState.UnlockedCharacters.Add(CharacterIds[i]);
                }
            }

            var selectedSnapshot = snapshot.Child("selectedCharacter");
            var selected = selectedSnapshot.Exists ? selectedSnapshot.Value?.ToString() : string.Empty;
            var selectedIndex = Array.IndexOf(CharacterIds, selected);
            if (!string.IsNullOrEmpty(selected) && IsCharacterUnlocked(selected)
                && selectedIndex >= 0 && selectedIndex < CharacterScreenCount)
            {
                selectedCharacterChanged = bootstrapState.SelectedCharacter != selected;
                bootstrapState.SelectedCharacter = selected;
                if (IsCharacterUnlocked(previewCharacterId))
                    previewCharacterId = selected;
            }

            RefreshMeProfileUI();
            RefreshPowerupUI();
            RefreshHomeHeaderDynamicValues();
            if (selectedCharacterChanged)
                RefreshTopPlayerAvatar(auth != null ? auth.CurrentUser : null);
        }

        private async Task UnlockCharacterWithCoinsAsync(string characterId)
        {
            var index = Array.IndexOf(CharacterIds, characterId);
            if (index <= 0 || index >= CharacterScreenCount || characterPurchaseInFlight)
                return;
            if (realtimeCharacterPlayerReference == null || auth == null || auth.CurrentUser == null)
            {
                SetCharacterStatus("REALTIME SYNC IS NOT READY", Hex("FF82C8"));
                return;
            }

            characterPurchaseInFlight = true;
            RefreshCharacterShowcase();
            var purchaseResult = "SYNC_ERROR";
            try
            {
                await realtimeCharacterPlayerReference.RunTransaction(mutablePlayer =>
                {
                    var mutableCoins = mutablePlayer.Child("coins");
                    var mutableCharacter = mutablePlayer.Child("characters/" + characterId);
                    var currentCoins = DatabaseLong(mutableCoins.Value, bootstrapState.Coins);
                    if (DatabaseBool(mutableCharacter.Child("unlocked").Value))
                    {
                        purchaseResult = "UNLOCKED";
                        return TransactionResult.Abort();
                    }
                    if (currentCoins < CharacterUnlockCoinCost)
                    {
                        purchaseResult = "NOT_ENOUGH";
                        return TransactionResult.Abort();
                    }

                    mutableCoins.Value = currentCoins - CharacterUnlockCoinCost;
                    mutableCharacter.Child("name").Value = CharacterNames[index];
                    mutableCharacter.Child("unlocked").Value = true;
                    mutableCharacter.Child("unlockCost").Value = CharacterUnlockCoinCost;
                    mutableCharacter.Child("unlockedAt").Value = ServerValue.Timestamp;
                    mutableCharacter.Child("updatedAt").Value = ServerValue.Timestamp;
                    mutablePlayer.Child("selectedCharacter").Value = characterId;
                    mutablePlayer.Child("updatedAt").Value = ServerValue.Timestamp;
                    purchaseResult = "SUCCESS";
                    return TransactionResult.Success(mutablePlayer);
                }, true);

                if (purchaseResult == "NOT_ENOUGH")
                    RefreshCharacterShowcase();
                else if (purchaseResult == "SUCCESS" || purchaseResult == "UNLOCKED")
                {
                    bootstrapState.UnlockedCharacters.Add(characterId);
                    bootstrapState.SelectedCharacter = characterId;
                    previewCharacterId = characterId;
                    RefreshMeProfileUI();
                    RefreshTopPlayerAvatar(auth.CurrentUser);
                    _ = SaveProfileCosmeticsAsync();
                    _ = SyncCharacterStateToLegacyFirestoreAsync();
                }
            }
            catch (Exception exception)
            {
                SetCharacterStatus("CHARACTER COULD NOT SYNC · TRY AGAIN", Hex("FF82C8"));
                Debug.LogWarning("Realtime character purchase failed: " + exception.Message);
            }
            finally
            {
                characterPurchaseInFlight = false;
                RefreshCharacterShowcase();
            }
        }

        private async Task SetSelectedCharacterRealtimeAsync(string characterId)
        {
            if (realtimeCharacterPlayerReference == null || !IsCharacterUnlocked(characterId))
                return;
            try
            {
                await realtimeCharacterPlayerReference.Child("selectedCharacter").SetValueAsync(characterId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Realtime selected character sync delayed: " + exception.Message);
            }
        }

        private async Task SyncCharacterStateToLegacyFirestoreAsync()
        {
            if (firestore == null || auth == null || auth.CurrentUser == null)
                return;
            try
            {
                var unlocked = new List<object>();
                foreach (var id in bootstrapState.UnlockedCharacters)
                    unlocked.Add(id);
                await firestore.Collection("users").Document(auth.CurrentUser.UserId).SetAsync(
                    new Dictionary<string, object>
                    {
                        { "coins", bootstrapState.Coins },
                        { "selected_character", bootstrapState.SelectedCharacter },
                        { "unlocked_characters", unlocked },
                        { "characters_updated_at", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Legacy Firestore character mirror delayed: " + exception.Message);
            }
        }

        private void SetCharacterStatus(string message, Color color)
        {
            _ = color;
            Debug.LogWarning(message);
            RefreshCharacterShowcase();
        }

        private static bool DatabaseBool(object value)
        {
            if (value is bool flag)
                return flag;
            return value != null && bool.TryParse(value.ToString(), out var parsed) && parsed;
        }
    }
}
