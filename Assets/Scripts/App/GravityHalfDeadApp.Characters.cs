using System;
using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private void RefreshMeProfileUI()
        {
            if (meStatisticValues[0] != null)
            {
                meStatisticValues[0].text = FormatPlayTime(bootstrapState.TotalPlaySeconds);
                meStatisticValues[1].text = bootstrapState.EndlessHighScore.ToString("N0");
                meStatisticValues[2].text = bootstrapState.MaxCoinsSingleRun.ToString("N0");
                meStatisticValues[3].text = bootstrapState.LifetimeCoinsCollected.ToString("N0");
                meStatisticValues[4].text = FormatDistance(bootstrapState.TotalDistanceMeters);
            }

            if (gameCoinAmountText != null)
                gameCoinAmountText.text = bootstrapState.Coins.ToString("N0");
            if (meCharacterWalletText != null)
                meCharacterWalletText.text = "COINS  " + bootstrapState.Coins.ToString("N0");

            for (var i = 0; i < CharacterScreenCount; i++)
            {
                if (meAvatarBorders[i] == null)
                    continue;

                var unlocked = IsCharacterUnlocked(CharacterIds[i]);
                var selected = bootstrapState.SelectedCharacter == CharacterIds[i];
                meAvatarBorders[i].gameObject.SetActive(selected);
                if (meCharacterCheckmarks[i] != null)
                    meCharacterCheckmarks[i].SetActive(selected);
                if (meCharacterLockOverlays[i] != null)
                    meCharacterLockOverlays[i].SetActive(!unlocked);
                if (meCharacterCostGroups[i] != null)
                    meCharacterCostGroups[i].SetActive(!unlocked);
                if (meAvatarStatusTexts[i] != null)
                    meAvatarStatusTexts[i].color = unlocked ? Muted : new Color(Muted.r, Muted.g, Muted.b, 0.62f);
                if (meAvatarPortraits[i] != null)
                    meAvatarPortraits[i].color = unlocked ? Color.white : new Color(0.76f, 0.82f, 0.91f, 0.80f);
            }

            RefreshCharacterShowcase();
        }

        private void PreviewCharacter(string avatarId)
        {
            var index = Array.IndexOf(CharacterIds, avatarId);
            if (index < 0 || index >= CharacterScreenCount)
                return;

            previewCharacterId = avatarId;
            if (IsCharacterUnlocked(avatarId) && bootstrapState.SelectedCharacter != avatarId)
                SelectAvatar(avatarId);
            else
                RefreshMeProfileUI();
        }

        private void RefreshCharacterShowcase()
        {
            if (meCharacterShowcase == null)
                return;

            var index = Array.IndexOf(CharacterIds, previewCharacterId);
            if (index < 0 || index >= CharacterScreenCount)
            {
                index = Mathf.Clamp(Array.IndexOf(CharacterIds, bootstrapState.SelectedCharacter), 0,
                    CharacterScreenCount - 1);
                previewCharacterId = CharacterIds[index];
            }

            meCharacterShowcase.texture = Resources.Load<Texture2D>(CharacterArtworkResource(previewCharacterId));
            meCharacterShowcase.material = CharacterArtworkMaterial(index);
            meCharacterShowcase.uvRect = new Rect(0f, 0f, 1f, 1f);
            var showcaseAspect = meCharacterShowcase.GetComponent<AspectRatioFitter>();
            if (showcaseAspect != null)
                showcaseAspect.enabled = false;
            SetFeaturedCharacterRect(meCharacterShowcase.texture);
            meCharacterName.text = CharacterNames[index];
            if (meCharacterRarityText != null)
                meCharacterRarityText.text = CharacterRarities[index];
            if (meCharacterCollectionText != null)
                meCharacterCollectionText.text = (index + 1) + " / " + CharacterScreenCount;
            for (var dotIndex = 0; dotIndex < meCharacterPositionDots.Length; dotIndex++)
            {
                if (meCharacterPositionDots[dotIndex] != null)
                    meCharacterPositionDots[dotIndex].color = dotIndex == index ? Cyan : Hex("193C67");
            }

            var unlocked = IsCharacterUnlocked(previewCharacterId);
            if (meCharacterActionLock != null)
                meCharacterActionLock.SetActive(!unlocked);
            if (meCharacterActionCoin != null)
                meCharacterActionCoin.SetActive(!unlocked);
            if (meCharacterActionLabelText != null)
            {
                meCharacterActionLabelText.text = unlocked
                    ? "SELECTED"
                    : CharacterUnlockCoinCost.ToString("N0") + "  ·  UNLOCK";
                meCharacterActionLabelText.color = unlocked ? NeonLime : Hex("FFD35C");
                meCharacterActionLabelText.rectTransform.anchoredPosition = unlocked
                    ? Vector2.zero : new Vector2(24f, 0f);
            }
            if (meCharacterActionButton != null)
            {
                var canPurchase = bootstrapState.Coins >= CharacterUnlockCoinCost;
                meCharacterActionButton.interactable = !characterPurchaseInFlight && !unlocked && canPurchase;
                var actionImage = meCharacterActionButton.GetComponent<Image>();
                if (actionImage != null)
                    actionImage.color = unlocked ? Hex("0B5C82")
                        : canPurchase ? Hex("0B4C78") : Hex("111D31");
            }
        }

        private void TryUnlockOrSelectPreviewCharacter()
        {
            if (IsCharacterUnlocked(previewCharacterId))
            {
                SelectAvatar(previewCharacterId);
                return;
            }

            _ = UnlockCharacterWithCoinsAsync(previewCharacterId);
        }

        private void SelectAvatar(string avatarId)
        {
            if (!IsCharacterUnlocked(avatarId))
                return;
            bootstrapState.SelectedCharacter = avatarId;
            previewCharacterId = avatarId;
            RefreshMeProfileUI();
            RefreshTopPlayerAvatar(auth != null ? auth.CurrentUser : null);
            _ = SetSelectedCharacterRealtimeAsync(avatarId);
            _ = SaveProfileCosmeticsAsync();
        }

        private bool SpendCoins(long amount)
        {
            if (bootstrapState.Coins < amount)
                return false;
            bootstrapState.Coins -= amount;
            RefreshPowerupUI();
            RefreshMeProfileUI();
            _ = SetRealtimeCoinBalanceAsync(bootstrapState.Coins);
            return true;
        }

        private bool IsCharacterUnlocked(string avatarId)
            => avatarId == "nova" || bootstrapState.UnlockedCharacters.Contains(avatarId);

        private static string CharacterUnlockPrice(string id)
            => id == "nova" ? string.Empty : CharacterUnlockCoinCost.ToString("N0") + " COINS";

        private static string CharacterActionLabel(string id)
            => id == "nova" ? "SELECTED" : "UNLOCK · " + CharacterUnlockCoinCost.ToString("N0");
    }
}
