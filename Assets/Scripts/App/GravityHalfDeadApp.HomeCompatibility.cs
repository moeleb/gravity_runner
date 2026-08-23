using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        // Compatibility bridge for the CURRENT home-screen design.
        // GravityHalfDeadApp.Game.cs creates the coin label locally and names it
        // "Top coin amount", then calls RefreshTopCoinCounter().  Keep the refresh
        // logic here so Game.cs does not need to be replaced or redesigned.
        private Text homeTopCoinAmountText;

        private void RefreshTopCoinCounter()
        {
            ResolveHomeTopCoinAmountText();

            if (homeTopCoinAmountText == null)
                return;

            homeTopCoinAmountText.text = bootstrapState == null
                ? "0"
                : bootstrapState.Coins.ToString("N0");

            // Keep the header value synchronized after Firebase bootstrap and after
            // any later coin changes, without requiring changes to the approved Game UI.
            var binder = homeTopCoinAmountText.GetComponent<HomeCoinCounterBinder>();
            if (binder == null)
                binder = homeTopCoinAmountText.gameObject.AddComponent<HomeCoinCounterBinder>();
            binder.Configure(this, homeTopCoinAmountText);
        }

        private void ResolveHomeTopCoinAmountText()
        {
            if (homeTopCoinAmountText != null || gameHomeRoot == null)
                return;

            var labels = gameHomeRoot.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                if (label != null && label.gameObject.name == "Top coin amount")
                {
                    homeTopCoinAmountText = label;
                    return;
                }
            }
        }

        private sealed class HomeCoinCounterBinder : MonoBehaviour
        {
            private GravityHalfDeadApp owner;
            private Text label;
            private long lastCoins = long.MinValue;

            public void Configure(GravityHalfDeadApp app, Text target)
            {
                owner = app;
                label = target;
                RefreshNow();
            }

            private void LateUpdate()
            {
                RefreshNow();
            }

            private void RefreshNow()
            {
                if (owner == null || label == null || owner.bootstrapState == null)
                    return;

                var coins = owner.bootstrapState.Coins;
                if (coins == lastCoins)
                    return;

                lastCoins = coins;
                label.text = coins.ToString("N0");
            }
        }
    }
}
