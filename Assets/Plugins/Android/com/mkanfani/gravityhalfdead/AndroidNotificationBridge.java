package com.mkanfani.gravityhalfdead;

import android.Manifest;
import android.app.Activity;
import android.content.pm.PackageManager;
import android.os.Build;

import com.google.firebase.messaging.FirebaseMessaging;
import com.unity3d.player.UnityPlayer;

/** Android-only opt-in bridge for Gravity's FCM campaign topic. */
public final class AndroidNotificationBridge {
    public static final String TOPIC = "gravity_android";
    private static final String UNITY_OBJECT = "Gravity Half Dead \u00b7 App Flow";

    private AndroidNotificationBridge() {}

    public static void enableNotifications() {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            send("OnFcmRegistrationError", "Android activity is unavailable.");
            return;
        }

        activity.runOnUiThread(() -> {
            if (Build.VERSION.SDK_INT >= 33
                    && activity.checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)
                    != PackageManager.PERMISSION_GRANTED) {
                activity.requestPermissions(new String[] { Manifest.permission.POST_NOTIFICATIONS }, 4901);
            }

            FirebaseMessaging messaging = FirebaseMessaging.getInstance();
            messaging.setAutoInitEnabled(true);
            messaging.subscribeToTopic(TOPIC)
                    .addOnFailureListener(error -> send("OnFcmRegistrationError", message(error)));
            messaging.getToken()
                    .addOnSuccessListener(token -> send("OnFcmTokenReceived", token))
                    .addOnFailureListener(error -> send("OnFcmRegistrationError", message(error)));
        });
    }

    public static void disableNotifications() {
        FirebaseMessaging messaging = FirebaseMessaging.getInstance();
        messaging.unsubscribeFromTopic(TOPIC);
        messaging.setAutoInitEnabled(false);
    }

    static void send(String method, String message) {
        UnityPlayer.UnitySendMessage(UNITY_OBJECT, method, message == null ? "" : message);
    }

    private static String message(Exception error) {
        return error == null || error.getMessage() == null
                ? "FCM registration failed." : error.getMessage();
    }
}
