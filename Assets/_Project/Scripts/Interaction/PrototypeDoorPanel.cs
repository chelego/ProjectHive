using System.Collections;
using UnityEngine;

namespace ProjectHive.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeDoorPanel : PrototypeInteractable
    {
        [SerializeField] private Transform door;
        [SerializeField] private Renderer indicatorRenderer;
        [SerializeField] private Light indicatorLight;
        [SerializeField, Min(0.1f)] private float effectDuration = 0.85f;

        private MaterialPropertyBlock propertyBlock;
        private Vector3 doorRestPosition;
        private Quaternion doorRestRotation;
        private bool isBusy;
        private bool isActivated;

        public override string Prompt => isActivated ? "[E] 문 제어 장치 재작동" : "[E] 문 제어 장치 작동";
        public override bool CanInteract => !isBusy;
        public bool IsActivated => isActivated;

        public void Configure(Transform controlledDoor, Renderer indicator, Light light)
        {
            door = controlledDoor;
            indicatorRenderer = indicator;
            indicatorLight = light;
        }

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();

            if (door != null)
            {
                doorRestPosition = door.localPosition;
                doorRestRotation = door.localRotation;
            }

            SetIndicator(false, 0f);
        }

        public override void Interact()
        {
            if (!isBusy)
                StartCoroutine(PlayActivationEffect());
        }

        private IEnumerator PlayActivationEffect()
        {
            isBusy = true;
            isActivated = !isActivated;

            float elapsed = 0f;
            while (elapsed < effectDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / effectDuration);
                float pulse = Mathf.Sin(normalized * Mathf.PI * 6f) * (1f - normalized);

                if (door != null)
                {
                    door.localPosition = doorRestPosition + new Vector3(pulse * 0.025f, 0f, 0f);
                    door.localRotation = doorRestRotation * Quaternion.Euler(0f, pulse * 1.8f, 0f);
                }

                SetIndicator(true, 2.2f + Mathf.Abs(pulse) * 2f);
                yield return null;
            }

            if (door != null)
            {
                door.localPosition = doorRestPosition;
                door.localRotation = doorRestRotation;
            }

            SetIndicator(isActivated, isActivated ? 2.2f : 0f);
            isBusy = false;
        }

        private void SetIndicator(bool active, float emissionStrength)
        {
            Color color = active ? new Color(0.1f, 1f, 0.35f) : new Color(1f, 0.12f, 0.05f);

            if (indicatorRenderer != null)
            {
                indicatorRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_EmissionColor", color * emissionStrength);
                indicatorRenderer.SetPropertyBlock(propertyBlock);
            }

            if (indicatorLight != null)
            {
                indicatorLight.color = color;
                indicatorLight.intensity = active ? 3f : 0.8f;
            }
        }
    }
}
