using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    public sealed partial class GravityHalfDeadApp
    {
        private void BuildBlackHoleHomeScene(Transform parent)
        {
            CreateImage("Game deep-space backdrop", parent, Hex("020611"), FullStretch());

            var fieldObject = new GameObject("Living black hole field");
            fieldObject.transform.SetParent(parent, false);
            var fieldRect = fieldObject.AddComponent<RectTransform>();
            SetRect(fieldRect, new Vector2(0, 125), new Vector2(1060, 1210));

            // Large faint gravity well. The rocks live on the layer above this and below the actual hole.
            CreateCard("Outer cyan gravity haze", fieldObject.transform, new Vector2(0, 45),
                new Vector2(900, 470), new Color(Cyan.r, Cyan.g, Cyan.b, 0.16f), 235)
                .AddComponent<Spinner>().degreesPerSecond = 3.5f;
            CreateCard("Outer magenta gravity haze", fieldObject.transform, new Vector2(0, 45),
                new Vector2(810, 400), new Color(NeonPink.r, NeonPink.g, NeonPink.b, 0.18f), 200)
                .AddComponent<Spinner>().degreesPerSecond = -5.5f;

            var rockLayerObject = new GameObject("Rock capture layer");
            rockLayerObject.transform.SetParent(fieldObject.transform, false);
            var rockLayer = rockLayerObject.AddComponent<RectTransform>();
            Stretch(rockLayer);

            // Everything below is created AFTER the rocks so it physically covers them as they cross the horizon.
            var rimGlow = CreateCard("Event horizon glow", fieldObject.transform, new Vector2(0, 45),
                new Vector2(430, 430), new Color(NeonPink.r, NeonPink.g, NeonPink.b, 0.58f), 215);
            var cyanRim = CreateCard("Event horizon cyan rim", rimGlow.transform, Vector2.zero,
                new Vector2(370, 370), new Color(Cyan.r, Cyan.g, Cyan.b, 0.92f), 185);
            var darkRim = CreateCard("Event horizon dark ring", cyanRim.transform, Vector2.zero,
                new Vector2(320, 320), Hex("01040B"), 160);
            var singularity = CreateCard("Black hole singularity", darkRim.transform, Vector2.zero,
                new Vector2(276, 276), Color.black, 138);

            CreateImage("Inner lens flare", singularity.transform,
                new Color(0.38f, 0.05f, 0.68f, 0.28f),
                Centered(Vector2.zero, new Vector2(230, 88)), RoundedSprite(44));
            MakeText(singularity.transform, "∞", 104, FontStyle.Bold, Cream,
                new Vector2(0, 14), new Vector2(210, 128), TextAnchor.MiddleCenter, 5);
            gameModeHintText = MakeText(singularity.transform, "STORY BREACH", 17, FontStyle.Bold, Cyan,
                new Vector2(0, -62), new Vector2(220, 34), TextAnchor.MiddleCenter, 3);

            var controller = fieldObject.AddComponent<BlackHoleFieldController>();
            controller.Configure(
                rockLayer,
                new Vector2(0f, 45f),
                145f,
                new Vector2(500f, 555f),
                rimGlow.GetComponent<RectTransform>(),
                rimGlow.GetComponent<Image>(),
                cyanRim.GetComponent<Image>());

            CreateImage("Game cinematic shade", parent, new Color(0f, 0f, 0f, 0.08f), FullStretch());
            CreateImage("Lower cinematic fade", parent, new Color(0.002f, 0.008f, 0.025f, 0.88f),
                Centered(new Vector2(0, -690), new Vector2(1080, 590)), RoundedSprite(2));
        }
    }
}
