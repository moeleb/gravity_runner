using UnityEngine;

namespace GravityHalfDead
{
    /// <summary>
    /// Optional base class for a Unity Ads, LevelPlay, AdMob, or AppLovin adapter.
    /// A concrete provider only needs to implement the two SDK-specific interface methods.
    /// </summary>
    public abstract class GravityRewardedAdProviderBehaviour : MonoBehaviour, IGravityRewardedAdProvider
    {
        private GravityHalfDeadApp app;

        protected virtual void OnEnable()
        {
            app = FindAnyObjectByType<GravityHalfDeadApp>();
            app?.RegisterRewardedAdProvider(this);
        }

        protected virtual void OnDisable()
        {
            app?.UnregisterRewardedAdProvider(this);
            app = null;
        }

        public abstract bool IsRewardedAdReady(string placementId);
        public abstract void ShowRewardedAd(string placementId, System.Action<bool> completed);
    }
}
