package com.mkanfani.gravityhalfdead;

import android.app.Activity;
import android.content.ClipData;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.util.ArrayList;

public final class SupportImagePickerActivity extends Activity {
    private static final int PICK_IMAGES = 4812;
    private static final long MAX_TOTAL_BYTES = 10L * 1024L * 1024L;
    private static final String UNITY_OBJECT = "Gravity Half Dead \u00b7 App Flow";

    @Override
    protected void onCreate(Bundle state) {
        super.onCreate(state);
        if (state == null) {
            Intent picker = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            picker.addCategory(Intent.CATEGORY_OPENABLE);
            picker.setType("image/*");
            picker.putExtra(Intent.EXTRA_ALLOW_MULTIPLE, true);
            startActivityForResult(picker, PICK_IMAGES);
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != PICK_IMAGES || resultCode != RESULT_OK || data == null) {
            send("OnSupportImagePickerError", "Image selection was cancelled.");
            finish();
            return;
        }

        try {
            ArrayList<Uri> uris = new ArrayList<>();
            ClipData clips = data.getClipData();
            if (clips != null) {
                for (int i = 0; i < clips.getItemCount(); i++)
                    uris.add(clips.getItemAt(i).getUri());
            } else if (data.getData() != null) {
                uris.add(data.getData());
            }

            File directory = new File(getCacheDir(), "support-images");
            if (!directory.exists() && !directory.mkdirs())
                throw new IllegalStateException("Could not create image cache.");

            long total = 0L;
            ArrayList<String> paths = new ArrayList<>();
            byte[] buffer = new byte[64 * 1024];
            for (int i = 0; i < uris.size(); i++) {
                Uri uri = uris.get(i);
                String mime = getContentResolver().getType(uri);
                String extension = "image/png".equals(mime) ? ".png"
                        : "image/webp".equals(mime) ? ".webp" : ".jpg";
                File output = new File(directory, "support-" + System.currentTimeMillis() + "-" + i + extension);
                try (InputStream input = getContentResolver().openInputStream(uri);
                     FileOutputStream stream = new FileOutputStream(output)) {
                    if (input == null) throw new IllegalStateException("An image could not be opened.");
                    int read;
                    while ((read = input.read(buffer)) >= 0) {
                        total += read;
                        if (total > MAX_TOTAL_BYTES)
                            throw new IllegalArgumentException("Selected images exceed the 10 MB combined limit.");
                        stream.write(buffer, 0, read);
                    }
                }
                paths.add(output.getAbsolutePath());
            }

            send("OnSupportImagesPicked", android.text.TextUtils.join("\n", paths));
        } catch (Exception error) {
            send("OnSupportImagePickerError", error.getMessage() == null
                    ? "Could not read the selected images." : error.getMessage());
        }
        finish();
    }

    private static void send(String method, String message) {
        UnityPlayer.UnitySendMessage(UNITY_OBJECT, method, message == null ? "" : message);
    }
}
