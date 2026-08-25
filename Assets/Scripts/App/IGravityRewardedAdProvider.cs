using System;

namespace GravityHalfDead
{
    /// <summary>
    /// Adapter boundary for the rewarded-ad SDK selected by the project.
    /// The shop never grants inventory until the provider invokes completed with true.
    /// </summary>
    public interface IGravityRewardedAdProvider
    {
        bool IsRewardedAdReady(string placementId);
        void ShowRewardedAd(string placementId, Action<bool> completed);
    }
}
