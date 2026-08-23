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
        private void RefreshPowerupUI()
        {
            if (mePowerupWalletText == null)
                return;
            mePowerupWalletText.text = "COINS " + bootstrapState.Coins.ToString("N0")
                + "  ·  SIX PERMANENT LEVELS";
            for (var i = 0; i < PowerupIds.Length; i++)
            {
                var level = bootstrapState.PowerupLevels[i];
                mePowerupLevelTexts[i].text = level >= 6 ? "LEVEL 6 / 6 · MAX" : "LEVEL " + level + " / 6";
                mePowerupDurationTexts[i].text = PowerupDurations[Mathf.Clamp(level, 0, 6)] + "s";
                for (var segment = 0; segment < 6; segment++)
                    mePowerupSegments[i, segment].color = segment < level
                        ? (i % 2 == 0 ? NeonLime : NeonPink) : Hex("263553");

                var maxed = level >= 6;
                mePowerupCostTexts[i].text = maxed ? "MAXED" : PowerupUpgradeCosts[level].ToString("N0") + " COINS";
                mePowerupCostTexts[i].color = maxed ? Cream : Ink;
                mePowerupButtons[i].interactable = !maxed;
                mePowerupButtons[i].GetComponent<Image>().color = maxed ? Hex("635A89")
                    : bootstrapState.Coins >= PowerupUpgradeCosts[level] ? NeonLime : Hex("FF5B8E");
            }
        }

        private async void UpgradePowerup(string powerupId)
        {
            var index = Array.IndexOf(PowerupIds, powerupId);
            if (index < 0)
                return;
            var level = bootstrapState.PowerupLevels[index];
            if (level >= 6)
                return;
            var cost = PowerupUpgradeCosts[level];
            if (!SpendCoins(cost))
            {
                mePowerupWalletText.text = "NOT ENOUGH COINS · NEED " + cost.ToString("N0");
                mePowerupWalletText.color = Hex("FF82C8");
                return;
            }

            bootstrapState.PowerupLevels[index]++;
            mePowerupWalletText.color = NeonLime;
            RefreshPowerupUI();
            if (firestore == null || auth == null || auth.CurrentUser == null)
                return;
            try
            {
                await firestore.Collection("users").Document(auth.CurrentUser.UserId).SetAsync(
                    new Dictionary<string, object>
                    {
                    { "coins", bootstrapState.Coins },
                    { "powerups", PowerupDictionary() },
                    { "powerups_updated_at", FieldValue.ServerTimestamp }
                    }, SetOptions.MergeAll);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Power-up upgrade sync delayed: " + exception.Message);
            }
        }

        private Dictionary<string, object> PowerupDictionary()
        {
            var values = new Dictionary<string, object>();
            for (var i = 0; i < PowerupIds.Length; i++)
                values[PowerupIds[i]] = (long)bootstrapState.PowerupLevels[i];
            return values;
        }
    }
}
