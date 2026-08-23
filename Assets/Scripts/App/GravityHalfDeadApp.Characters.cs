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

            for (var i = 0; i < meAvatarBorders.Length; i++)
            {
                if (meAvatarBorders[i] == null)
                    continue;
                var unlocked = IsCharacterUnlocked(CharacterIds[i]);
                var selected = bootstrapState.SelectedCharacter == CharacterIds[i];
                meAvatarBorders[i].gameObject.SetActive(selected);
                meAvatarStatusTexts[i].text = string.Empty;
                meAvatarStatusTexts[i].color = selected ? NeonLime : Cream;
                if (meAvatarPortraits[i] != null)
                    meAvatarPortraits[i].color = unlocked ? Color.white : new Color(0.72f, 0.72f, 0.78f, 0.62f);
            }

            RefreshCharacterShowcase();
        }

        private void PreviewCharacter(string avatarId)
        {
            previewCharacterId = avatarId;
            if (IsCharacterUnlocked(avatarId) && bootstrapState.SelectedCharacter != avatarId)
            {
                bootstrapState.SelectedCharacter = avatarId;
                _ = SaveProfileCosmeticsAsync();
                RefreshTopPlayerAvatar(auth != null ? auth.CurrentUser : null);
            }
            RefreshMeProfileUI();
        }

        private void RefreshCharacterShowcase()
        {
            if (meCharacterShowcase == null)
                return;

            var index = Array.IndexOf(CharacterIds, previewCharacterId);
            if (index < 0)
            {
                index = 0;
                previewCharacterId = CharacterIds[0];
            }
            meCharacterShowcase.texture = Resources.Load<Texture2D>("UI/Characters/" + previewCharacterId);
            meCharacterShowcase.material = index >= 3 ? characterCutoutMaterial : null;
            meCharacterName.text = CharacterNames[index];
            var unlocked = IsCharacterUnlocked(previewCharacterId);
            meCharacterUnlockText.text = unlocked ? string.Empty : CharacterUnlockPrice(previewCharacterId);
            meCharacterActionButton.gameObject.SetActive(!unlocked);
            meCharacterActionButton.interactable = !unlocked;
        }

        private void TryUnlockOrSelectPreviewCharacter()
        {
            if (IsCharacterUnlocked(previewCharacterId))
            {
                SelectAvatar(previewCharacterId);
                return;
            }

            var unlocked = false;
            switch (previewCharacterId)
            {
                case "orbit": unlocked = bootstrapState.RobotShards >= 250L; break;
                case "jax": unlocked = SpendCoins(30000L); break;
                case "raze": unlocked = bootstrapState.HasMadePurchase; break;
                case "echo": unlocked = bootstrapState.IceShards >= 200L; break;
                case "regalia":
                    meCharacterUnlockText.text = CharacterUnlockPrice(previewCharacterId);
                    return;
                case "kairo": unlocked = SpendCoins(10000L); break;
                case "luna": unlocked = bootstrapState.ConsecutiveLoginDays >= 7L; break;
                case "volt": unlocked = bootstrapState.TotalDistanceMeters >= 50000L; break;
                case "mako": unlocked = bootstrapState.CompletedRuns >= 25L; break;
                case "glitch": unlocked = SpendCoins(75000L); break;
                case "ember": unlocked = bootstrapState.TotalPlaySeconds >= 36000L; break;
                case "frost": unlocked = bootstrapState.IceShards >= 400L; break;
            }

            if (!unlocked)
            {
                meCharacterUnlockText.text = CharacterUnlockPrice(previewCharacterId);
                return;
            }

            bootstrapState.UnlockedCharacters.Add(previewCharacterId);
            SelectAvatar(previewCharacterId);
        }

        private void SelectAvatar(string avatarId)
        {
            if (!IsCharacterUnlocked(avatarId))
                return;
            bootstrapState.SelectedCharacter = avatarId;
            RefreshMeProfileUI();
            _ = SaveProfileCosmeticsAsync();
            RefreshTopPlayerAvatar(auth != null ? auth.CurrentUser : null);
        }

        private bool SpendCoins(long amount)
        {
            if (bootstrapState.Coins < amount)
                return false;
            bootstrapState.Coins -= amount;
            return true;
        }

        private bool IsCharacterUnlocked(string avatarId)
            => avatarId == "nova" || bootstrapState.UnlockedCharacters.Contains(avatarId);

        private string CharacterUnlockDescription(string id)
        {
            return id switch
            {
                "orbit" => "COLLECT 250 ROBOT SHARDS · " + bootstrapState.RobotShards + " / 250",
                "jax" => "30,000 COINS",
                "raze" => "FREE WITH YOUR FIRST PURCHASE",
                "echo" => "COLLECT 200 ICE SHARDS · " + bootstrapState.IceShards + " / 200",
                "regalia" => "PREMIUM CHARACTER · USD $3.99",
                "kairo" => "10,000 COINS",
                "luna" => "REACH A 7-DAY LOGIN STREAK · " + bootstrapState.ConsecutiveLoginDays + " / 7",
                "volt" => "TRAVEL 50 KM · " + FormatDistance(bootstrapState.TotalDistanceMeters),
                "mako" => "COMPLETE 25 RUNS · " + bootstrapState.CompletedRuns + " / 25",
                "glitch" => "75,000 COINS",
                "ember" => "PLAY FOR 10 HOURS · " + FormatPlayTime(bootstrapState.TotalPlaySeconds),
                "frost" => "COLLECT 400 ICE SHARDS · " + bootstrapState.IceShards + " / 400",
                _ => "STARTER CHARACTER · FREE"
            };
        }

        private static string CharacterUnlockPrice(string id)
        {
            return id switch
            {
                "orbit" => "250 ROBOT SHARDS",
                "jax" => "30,000 COINS",
                "raze" => "FIRST PURCHASE",
                "echo" => "200 ICE SHARDS",
                "regalia" => "USD $3.99",
                "kairo" => "10,000 COINS",
                "luna" => "7-DAY LOGIN",
                "volt" => "50 KM",
                "mako" => "25 RUNS",
                "glitch" => "75,000 COINS",
                "ember" => "10 HOURS",
                "frost" => "400 ICE SHARDS",
                _ => string.Empty
            };
        }

        private static string CharacterActionLabel(string id)
        {
            return id switch
            {
                "jax" => "UNLOCK · 30,000",
                "kairo" => "UNLOCK · 10,000",
                "glitch" => "UNLOCK · 75,000",
                "regalia" => "USD $3.99",
                _ => "CHECK UNLOCK"
            };
        }
    }
}
