using System;
using System.Collections;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

namespace GravityHalfDead
{
    /// <summary>
    /// Google Mobile Ads rewarded provider configured exclusively with Google's official test IDs.
    /// Replace both the app IDs asset and these ad-unit IDs before a production release.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoogleMobileAdsTestRewardedProvider : GravityRewardedAdProviderBehaviour
    {
#if UNITY_ANDROID
        private const string RewardedTestAdUnitId = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IPHONE
        private const string RewardedTestAdUnitId = "ca-app-pub-3940256099942544/1712485313";
#else
        private const string RewardedTestAdUnitId = "unused";
#endif

        private const string ShopPlacementPrefix = "shop_booster_";
        private const string MissionPlacementPrefix = "mission_remove_";
        private const float RetryDelaySeconds = 15f;

        private RewardedAd rewardedAd;
        private Coroutine retryCoroutine;
        private Action<bool> pendingCompletion;
        private bool sdkInitialized;
        private bool adLoading;
        private bool shuttingDown;
        private bool rewardEarned;

        protected override void OnEnable()
        {
            shuttingDown = false;
            base.OnEnable();

#if UNITY_ANDROID || UNITY_IPHONE
            // Version 11's next-generation Android SDK requires initialization on Unity's main thread.
            MobileAds.Initialize(initializationStatus =>
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (shuttingDown)
                        return;
                    if (initializationStatus == null)
                    {
                        Debug.LogError("Google Mobile Ads test SDK initialization failed.");
                        NotifyShopAvailabilityChanged();
                        return;
                    }

                    sdkInitialized = true;
                    LoadRewardedAd();
                });
            });
#else
            Debug.Log("Google rewarded test ads are enabled for Android and iOS device builds only.");
#endif
        }

        protected override void OnDisable()
        {
            shuttingDown = true;
            sdkInitialized = false;
            adLoading = false;
            if (retryCoroutine != null)
            {
                StopCoroutine(retryCoroutine);
                retryCoroutine = null;
            }

            CompletePending(false);
            DestroyRewardedAd();
            base.OnDisable();
        }

        public override bool IsRewardedAdReady(string placementId)
        {
            return IsSupportedPlacement(placementId) && rewardedAd != null && rewardedAd.CanShowAd();
        }

        public override void ShowRewardedAd(string placementId, Action<bool> completed)
        {
            if (!IsSupportedPlacement(placementId) || pendingCompletion != null || rewardedAd == null
                || !rewardedAd.CanShowAd())
            {
                completed?.Invoke(false);
                return;
            }

            var adToShow = rewardedAd;
            rewardedAd = null;
            pendingCompletion = completed;
            rewardEarned = false;
            NotifyShopAvailabilityChanged();

            try
            {
                adToShow.Show(reward =>
                {
                    // Google invokes this only after the user earns the reward. Never grant on close.
                    rewardEarned = true;
                    MobileAdsEventExecutor.ExecuteInUpdate(() => CompletePending(true));
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Google rewarded test ad could not open: " + exception.Message);
                CompletePending(false);
                adToShow.Destroy();
                LoadRewardedAd();
            }
        }

        private void LoadRewardedAd()
        {
#if UNITY_ANDROID || UNITY_IPHONE
            if (shuttingDown || !sdkInitialized || adLoading || rewardedAd != null)
                return;

            adLoading = true;
            var request = new AdRequest();
            RewardedAd.Load(RewardedTestAdUnitId, request, (ad, error) =>
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    adLoading = false;
                    if (shuttingDown)
                    {
                        ad?.Destroy();
                        return;
                    }

                    if (error != null || ad == null)
                    {
                        Debug.LogWarning("Google rewarded test ad failed to load: " +
                                         (error == null ? "empty ad response" : error.ToString()));
                        NotifyShopAvailabilityChanged();
                        ScheduleRetry();
                        return;
                    }

                    DestroyRewardedAd();
                    rewardedAd = ad;
                    RegisterAdEvents(ad);
                    NotifyShopAvailabilityChanged();
                });
            });
#endif
        }

        private void RegisterAdEvents(RewardedAd ad)
        {
            ad.OnAdFullScreenContentClosed += () =>
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() => FinishShownAd(ad, false));
            };
            ad.OnAdFullScreenContentFailed += error =>
            {
                Debug.LogWarning("Google rewarded test ad failed to present: " + error);
                MobileAdsEventExecutor.ExecuteInUpdate(() => FinishShownAd(ad, true));
            };
        }

        private void FinishShownAd(RewardedAd shownAd, bool failed)
        {
            // The reward callback is authoritative. Closing/skipping never grants an item.
            if (failed || !rewardEarned)
                CompletePending(false);
            shownAd?.Destroy();
            rewardEarned = false;
            LoadRewardedAd();
        }

        private void CompletePending(bool completed)
        {
            var callback = pendingCompletion;
            pendingCompletion = null;
            callback?.Invoke(completed);
        }

        private void DestroyRewardedAd()
        {
            if (rewardedAd == null)
                return;
            rewardedAd.Destroy();
            rewardedAd = null;
        }

        private void ScheduleRetry()
        {
            if (retryCoroutine == null && !shuttingDown)
                retryCoroutine = StartCoroutine(RetryAfterDelay());
        }

        private IEnumerator RetryAfterDelay()
        {
            yield return new WaitForSecondsRealtime(RetryDelaySeconds);
            retryCoroutine = null;
            LoadRewardedAd();
        }

        private void NotifyShopAvailabilityChanged()
        {
            FindAnyObjectByType<GravityHalfDeadApp>()?.RegisterRewardedAdProvider(this);
        }

        private static bool IsSupportedPlacement(string placementId)
        {
            return !string.IsNullOrEmpty(placementId)
                   && (placementId.StartsWith(ShopPlacementPrefix, StringComparison.Ordinal)
                       || placementId.StartsWith(MissionPlacementPrefix, StringComparison.Ordinal));
        }
    }
}
