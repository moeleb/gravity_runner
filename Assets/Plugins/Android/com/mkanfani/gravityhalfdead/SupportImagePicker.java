package com.mkanfani.gravityhalfdead;

import android.app.Activity;
import android.content.Intent;

import com.unity3d.player.UnityPlayer;

/** Opens Android's document picker through a small result-forwarding activity. */
public final class SupportImagePicker {
    private SupportImagePicker() {}

    public static void pickImages() {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            UnityPlayer.UnitySendMessage("Gravity Half Dead \u00b7 App Flow",
                    "OnSupportImagePickerError", "Android activity is unavailable.");
            return;
        }
        activity.runOnUiThread(() -> {
            Intent intent = new Intent(activity, SupportImagePickerActivity.class);
            activity.startActivity(intent);
        });
    }
}
