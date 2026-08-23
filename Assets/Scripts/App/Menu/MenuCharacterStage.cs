using System;
using UnityEngine;
using UnityEngine.UI;

namespace GravityHalfDead
{
    internal sealed class MenuCharacterStage : IDisposable
    {
        private const int StageLayer = 30;
        private readonly RawImage display;
        private RenderTexture texture;
        private GameObject root;

        public MenuCharacterStage(RawImage targetDisplay) => display = targetDisplay;

        public void Ensure()
        {
            if (root != null)
                return;

            texture = new RenderTexture(720, 1280, 24, RenderTextureFormat.ARGB32)
            {
                name = "Menu Character Stage",
                antiAliasing = 2
            };
            texture.Create();
            if (display != null)
                display.texture = texture;

            root = new GameObject("Menu Character 3D Stage");
            root.transform.position = new Vector3(5000f, 5000f, 5000f);
            UnityEngine.Object.DontDestroyOnLoad(root);

            BuildCamera();
            CreateLight("Cyan rim", new Vector3(-2.5f, 3f, -2f), new Color(0.05f, 0.75f, 1f), 3.2f);
            CreateLight("Orange rim", new Vector3(2.6f, 2.4f, -1f), new Color(1f, 0.28f, 0.06f), 2.7f);
            CreateLight("Soft key", new Vector3(0f, 4f, -3f), Color.white, 1.5f);
        }

        public GameObject Spawn(GameObject prefab, string characterId)
        {
            Ensure();
            var character = UnityEngine.Object.Instantiate(prefab, root.transform);
            character.name = characterId + " · Menu Performer";
            character.transform.localPosition = Vector3.zero;
            character.transform.localRotation = Quaternion.identity;
            SetLayerRecursively(character, StageLayer);
            return character;
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("Menu Character Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.35f, -5.2f);
            cameraObject.transform.LookAt(root.transform.position + Vector3.up * 1.25f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.cullingMask = 1 << StageLayer;
            camera.fieldOfView = 31f;
            camera.targetTexture = texture;
        }

        private void CreateLight(string name, Vector3 position, Color color, float intensity)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = position;
            lightObject.transform.LookAt(root.transform.position + Vector3.up);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = 12f;
            light.spotAngle = 70f;
            light.cullingMask = 1 << StageLayer;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        public void Dispose()
        {
            if (texture != null)
            {
                texture.Release();
                UnityEngine.Object.Destroy(texture);
                texture = null;
            }
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
                root = null;
            }
        }
    }
}
