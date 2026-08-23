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
        private Slider BuildSlider(Transform parent, Vector2 position, Vector2 size)
        {
            var root = new GameObject("Age Slider");
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            SetRect(rect, position, size);
            var slider = root.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;

            var background = CreateImage("Track", root.transform, CardSoft,
                Centered(Vector2.zero, new Vector2(size.x, 18)), RoundedSprite(10));

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(root.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1, 0.5f);
            fillAreaRect.offsetMin = new Vector2(0, -9);
            fillAreaRect.offsetMax = new Vector2(0, 9);

            var fill = CreateImage("Fill", fillArea.transform, Cyan, FullStretch(), RoundedSprite(10));
            slider.fillRect = fill.rectTransform;

            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(root.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(25, 0);
            handleAreaRect.offsetMax = new Vector2(-25, 0);

            var handle = CreateImage("Handle", handleArea.transform, Cream,
                Centered(Vector2.zero, new Vector2(64, 64)), RoundedSprite(32));
            handle.rectTransform.anchorMin = new Vector2(0, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0, 0.5f);
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            return slider;
        }

        private Button MakeButton(Transform parent, string label, Vector2 position, Vector2 size,
            Color background, Color foreground, int fontSize, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(label + " Button");
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.AddComponent<RectTransform>();
            SetRect(rect, position, size);
            var image = buttonObject.AddComponent<Image>();
            image.sprite = RoundedSprite(28);
            image.type = Image.Type.Sliced;
            image.color = background;

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.07f, 1.07f, 1.07f, 1f);
            colors.pressedColor = new Color(0.82f, 0.88f, 0.91f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.1f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(action);

            if (!string.IsNullOrEmpty(label))
            {
                MakeText(buttonObject.transform, label, fontSize, FontStyle.Bold, foreground,
                    Vector2.zero, size - new Vector2(40, 18), TextAnchor.MiddleCenter, 2);
            }
            return button;
        }

        private GameObject CreateCard(string name, Transform parent, Vector2 position, Vector2 size, Color color, int radius)
        {
            var cardObject = new GameObject(name);
            cardObject.transform.SetParent(parent, false);
            var rect = cardObject.AddComponent<RectTransform>();
            SetRect(rect, position, size);
            var image = cardObject.AddComponent<Image>();
            image.sprite = RoundedSprite(radius);
            image.type = Image.Type.Sliced;
            image.color = color;
            return cardObject;
        }

        private Text MakeText(Transform parent, string content, int size, FontStyle style, Color color,
            Vector2 position, Vector2 dimensions, TextAnchor alignment, int spacing = 0)
        {
            var textObject = new GameObject("Text · " + (content.Length > 18 ? content[..18] : content));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            SetRect(rect, position, dimensions);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(13, size - 9);
            text.resizeTextMaxSize = size;
            text.raycastTarget = false;
            if (spacing != 0)
                text.gameObject.AddComponent<LetterSpacing>().spacing = spacing;
            return text;
        }
    }
}
