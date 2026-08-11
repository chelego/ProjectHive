using System.Collections;
using UnityEngine;

namespace ProjectHive.Combat
{
    [DisallowMultipleComponent]
    public sealed class PlayerMeleeViewModel : MonoBehaviour
    {
        [SerializeField] private Transform viewRoot;
        [SerializeField] private MeleeWeaponKind equippedKind = MeleeWeaponKind.Knife;
        [SerializeField] private Vector3 restLocalPosition = new Vector3(0.36f, -0.34f, 0.68f);
        [SerializeField] private Vector3 restLocalEuler = new Vector3(8f, -18f, 0f);
        [SerializeField] private float executionZoomFieldOfView = 34f;
        [SerializeField] private float executionImpactShake = 0.035f;

        private GameObject knifeObject;
        private GameObject warhammerObject;
        private GameObject supportArmObject;
        private Coroutine motionRoutine;
        private bool visible = true;

        private void Awake()
        {
            if (viewRoot == null && Camera.main != null)
                viewRoot = Camera.main.transform;

            EnsureModels();
            Equip(equippedKind);
        }

        public void Equip(MeleeWeaponKind kind)
        {
            equippedKind = kind;
            EnsureModels();

            ApplyVisibility();

            Transform model = CurrentModel;
            if (model != null)
            {
                model.localPosition = restLocalPosition;
                model.localRotation = Quaternion.Euler(GetRestEuler(kind));
            }
        }

        public void SetVisible(bool isVisible)
        {
            visible = isVisible;
            ApplyVisibility();
        }

        public void PlayAttack(MeleeWeaponKind kind)
        {
            Equip(kind);
            StartMotion(kind == MeleeWeaponKind.Warhammer ? WarhammerSwing() : KnifeSlash());
        }

        public void PlayAssassination(MeleeWeaponKind kind)
        {
            Equip(kind);
            StartMotion(kind == MeleeWeaponKind.Warhammer ? WarhammerExecution() : KnifeNeckStab());
        }

        private Transform CurrentModel
        {
            get
            {
                GameObject current = equippedKind == MeleeWeaponKind.Warhammer ? warhammerObject : knifeObject;
                return current != null ? current.transform : null;
            }
        }

        private Vector3 GetRestEuler(MeleeWeaponKind kind)
        {
            return restLocalEuler;
        }

        private void StartMotion(IEnumerator routine)
        {
            if (motionRoutine != null)
                StopCoroutine(motionRoutine);

            motionRoutine = StartCoroutine(routine);
        }

        private IEnumerator KnifeSlash()
        {
            yield return Animate(
                restLocalPosition + new Vector3(-0.08f, 0.04f, -0.08f),
                restLocalEuler + new Vector3(18f, 28f, -28f),
                0.08f);
            yield return Animate(
                restLocalPosition + new Vector3(0.08f, 0f, 0.2f),
                restLocalEuler + new Vector3(-10f, -16f, 18f),
                0.1f);
            yield return Animate(restLocalPosition, restLocalEuler, 0.14f);
        }

        private IEnumerator WarhammerSwing()
        {
            Vector3 hammerRestEuler = GetRestEuler(MeleeWeaponKind.Warhammer);

            yield return Animate(
                restLocalPosition + new Vector3(0.05f, 0.28f, -0.12f),
                hammerRestEuler + new Vector3(-64f, 12f, 18f),
                0.22f);
            yield return Animate(
                restLocalPosition + new Vector3(-0.04f, -0.08f, 0.16f),
                hammerRestEuler + new Vector3(44f, -8f, -12f),
                0.16f);
            yield return Animate(restLocalPosition, hammerRestEuler, 0.26f);
        }

        private IEnumerator KnifeNeckStab()
        {
            Vector3 armRestPosition = new Vector3(-0.34f, -0.28f, 0.58f);
            Vector3 armRestEuler = new Vector3(18f, 16f, -18f);
            if (supportArmObject != null)
            {
                supportArmObject.SetActive(visible);
                supportArmObject.transform.localPosition = armRestPosition;
                supportArmObject.transform.localRotation = Quaternion.Euler(armRestEuler);
            }

            yield return AnimateKnifeExecution(
                restLocalPosition + new Vector3(-0.18f, 0.1f, -0.14f),
                restLocalEuler + new Vector3(28f, 46f, -54f),
                armRestPosition + new Vector3(-0.04f, 0.18f, 0.28f),
                armRestEuler + new Vector3(-22f, -28f, 54f),
                0.16f);
            yield return AnimateKnifeExecution(
                restLocalPosition + new Vector3(-0.04f, 0.08f, 0.42f),
                restLocalEuler + new Vector3(-26f, -10f, 14f),
                armRestPosition + new Vector3(0.08f, 0.22f, 0.4f),
                armRestEuler + new Vector3(-34f, -44f, 68f),
                0.1f);
            yield return AnimateKnifeExecution(
                restLocalPosition + new Vector3(0f, 0.02f, 0.24f),
                restLocalEuler + new Vector3(-8f, -2f, 24f),
                armRestPosition + new Vector3(0.02f, 0.18f, 0.32f),
                armRestEuler + new Vector3(-20f, -34f, 58f),
                0.22f);
            yield return AnimateKnifeExecution(restLocalPosition, restLocalEuler, armRestPosition, armRestEuler, 0.18f);

            if (supportArmObject != null)
                supportArmObject.SetActive(false);
        }

        private IEnumerator WarhammerExecution()
        {
            Vector3 hammerRestEuler = GetRestEuler(MeleeWeaponKind.Warhammer);
            Camera viewCamera = viewRoot != null ? viewRoot.GetComponent<Camera>() : null;
            Vector3 cameraRestPosition = viewRoot != null ? viewRoot.localPosition : Vector3.zero;
            Quaternion cameraRestRotation = viewRoot != null ? viewRoot.localRotation : Quaternion.identity;
            float cameraRestFov = viewCamera != null ? viewCamera.fieldOfView : 60f;

            yield return AnimateHammerExecutionStep(
                restLocalPosition + new Vector3(0.02f, 0.88f, -0.38f),
                hammerRestEuler + new Vector3(-154f, 2f, 6f),
                cameraRestPosition + new Vector3(0f, -0.055f, -0.025f),
                cameraRestRotation * Quaternion.Euler(-75f, 0f, 0f),
                cameraRestFov + 5f,
                0.46f);

            yield return AnimateHammerExecutionStep(
                restLocalPosition + new Vector3(0.01f, 0.62f, -0.12f),
                hammerRestEuler + new Vector3(-112f, 0f, 2f),
                cameraRestPosition + new Vector3(0f, -0.025f, 0.02f),
                cameraRestRotation * Quaternion.Euler(-36f, 0f, 0f),
                cameraRestFov - 2f,
                0.14f);

            yield return AnimateHammerExecutionStep(
                restLocalPosition + new Vector3(0f, -0.08f, 0.56f),
                hammerRestEuler + new Vector3(64f, 0f, -4f),
                cameraRestPosition + new Vector3(0f, -0.09f, 0.12f),
                cameraRestRotation * Quaternion.Euler(34f, 0f, 0f),
                executionZoomFieldOfView,
                0.18f);

            yield return ShakeExecutionCamera(cameraRestPosition, cameraRestRotation, executionZoomFieldOfView, 0.26f);

            yield return AnimateHammerExecutionStep(
                restLocalPosition + new Vector3(0.06f, -0.02f, 0.22f),
                hammerRestEuler + new Vector3(34f, -10f, -12f),
                cameraRestPosition,
                cameraRestRotation,
                cameraRestFov,
                0.36f);
            yield return Animate(restLocalPosition, hammerRestEuler, 0.3f);

            if (viewRoot != null)
            {
                viewRoot.localPosition = cameraRestPosition;
                viewRoot.localRotation = cameraRestRotation;
            }

            if (viewCamera != null)
                viewCamera.fieldOfView = cameraRestFov;
        }

        private IEnumerator Animate(Vector3 targetPosition, Vector3 targetEuler, float duration)
        {
            Transform model = CurrentModel;
            if (model == null)
                yield break;

            Vector3 startPosition = model.localPosition;
            Quaternion startRotation = model.localRotation;
            Quaternion targetRotation = Quaternion.Euler(targetEuler);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                model.localPosition = Vector3.Lerp(startPosition, targetPosition, eased);
                model.localRotation = Quaternion.Slerp(startRotation, targetRotation, eased);
                yield return null;
            }

            model.localPosition = targetPosition;
            model.localRotation = targetRotation;
        }

        private IEnumerator AnimateKnifeExecution(
            Vector3 targetKnifePosition,
            Vector3 targetKnifeEuler,
            Vector3 targetArmPosition,
            Vector3 targetArmEuler,
            float duration)
        {
            Transform knife = CurrentModel;
            Transform arm = supportArmObject != null ? supportArmObject.transform : null;
            if (knife == null)
                yield break;

            Vector3 startKnifePosition = knife.localPosition;
            Quaternion startKnifeRotation = knife.localRotation;
            Quaternion targetKnifeRotation = Quaternion.Euler(targetKnifeEuler);
            Vector3 startArmPosition = arm != null ? arm.localPosition : Vector3.zero;
            Quaternion startArmRotation = arm != null ? arm.localRotation : Quaternion.identity;
            Quaternion targetArmRotation = Quaternion.Euler(targetArmEuler);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                knife.localPosition = Vector3.Lerp(startKnifePosition, targetKnifePosition, eased);
                knife.localRotation = Quaternion.Slerp(startKnifeRotation, targetKnifeRotation, eased);

                if (arm != null)
                {
                    arm.localPosition = Vector3.Lerp(startArmPosition, targetArmPosition, eased);
                    arm.localRotation = Quaternion.Slerp(startArmRotation, targetArmRotation, eased);
                }

                yield return null;
            }

            knife.localPosition = targetKnifePosition;
            knife.localRotation = targetKnifeRotation;
            if (arm != null)
            {
                arm.localPosition = targetArmPosition;
                arm.localRotation = targetArmRotation;
            }
        }

        private IEnumerator AnimateHammerExecutionStep(
            Vector3 targetHammerPosition,
            Vector3 targetHammerEuler,
            Vector3 targetCameraPosition,
            Quaternion targetCameraRotation,
            float targetFieldOfView,
            float duration)
        {
            Transform hammer = CurrentModel;
            if (hammer == null)
                yield break;

            Camera viewCamera = viewRoot != null ? viewRoot.GetComponent<Camera>() : null;
            Vector3 startHammerPosition = hammer.localPosition;
            Quaternion startHammerRotation = hammer.localRotation;
            Quaternion targetHammerRotation = Quaternion.Euler(targetHammerEuler);
            Vector3 startCameraPosition = viewRoot != null ? viewRoot.localPosition : Vector3.zero;
            Quaternion startCameraRotation = viewRoot != null ? viewRoot.localRotation : Quaternion.identity;
            float startFieldOfView = viewCamera != null ? viewCamera.fieldOfView : targetFieldOfView;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                hammer.localPosition = Vector3.Lerp(startHammerPosition, targetHammerPosition, eased);
                hammer.localRotation = Quaternion.Slerp(startHammerRotation, targetHammerRotation, eased);

                if (viewRoot != null)
                {
                    viewRoot.localPosition = Vector3.Lerp(startCameraPosition, targetCameraPosition, eased);
                    viewRoot.localRotation = Quaternion.Slerp(startCameraRotation, targetCameraRotation, eased);
                }

                if (viewCamera != null)
                    viewCamera.fieldOfView = Mathf.Lerp(startFieldOfView, targetFieldOfView, eased);

                yield return null;
            }
        }

        private IEnumerator ShakeExecutionCamera(Vector3 restPosition, Quaternion restRotation, float fieldOfView, float duration)
        {
            Camera viewCamera = viewRoot != null ? viewRoot.GetComponent<Camera>() : null;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float strength = (1f - normalized) * executionImpactShake;
                float x = Mathf.Sin(Time.time * 95f) * strength;
                float y = Mathf.Cos(Time.time * 131f) * strength;

                if (viewRoot != null)
                {
                    viewRoot.localPosition = restPosition + new Vector3(x, -0.09f + y, 0.12f);
                    viewRoot.localRotation = restRotation * Quaternion.Euler(34f + y * 80f, x * 80f, x * 120f);
                }

                if (viewCamera != null)
                    viewCamera.fieldOfView = fieldOfView;

                yield return null;
            }
        }

        private void EnsureModels()
        {
            if (viewRoot == null)
                return;

            if (knifeObject == null)
                knifeObject = CreateKnife();
            if (warhammerObject == null)
                warhammerObject = CreateWarhammer();
            if (supportArmObject == null)
                supportArmObject = CreateSupportArm();

            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (knifeObject != null)
                knifeObject.SetActive(visible && equippedKind == MeleeWeaponKind.Knife);
            if (warhammerObject != null)
                warhammerObject.SetActive(visible && equippedKind == MeleeWeaponKind.Warhammer);
            if (supportArmObject != null)
                supportArmObject.SetActive(false);
        }

        private GameObject CreateKnife()
        {
            GameObject root = new GameObject("View Knife");
            root.transform.SetParent(viewRoot, false);
            root.transform.localPosition = restLocalPosition;
            root.transform.localRotation = Quaternion.Euler(restLocalEuler);

            AddPrimitive(root.transform, "Grip", PrimitiveType.Cylinder, new Vector3(0f, -0.12f, 0f), new Vector3(0.055f, 0.18f, 0.055f), new Color(0.08f, 0.07f, 0.06f, 1f));
            AddPrimitive(root.transform, "Blade", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0.02f), new Vector3(0.045f, 0.34f, 0.018f), new Color(0.78f, 0.82f, 0.86f, 1f));
            AddPrimitive(root.transform, "Guard", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0f), new Vector3(0.18f, 0.026f, 0.035f), new Color(0.18f, 0.17f, 0.15f, 1f));
            return root;
        }

        private GameObject CreateSupportArm()
        {
            GameObject root = new GameObject("View Left Grapple Arm");
            root.transform.SetParent(viewRoot, false);
            root.transform.localPosition = new Vector3(-0.34f, -0.28f, 0.58f);
            root.transform.localRotation = Quaternion.Euler(18f, 16f, -18f);

            Color sleeveColor = new Color(0.12f, 0.14f, 0.17f, 1f);
            Color gloveColor = new Color(0.055f, 0.05f, 0.045f, 1f);
            Color skinColor = new Color(0.82f, 0.58f, 0.42f, 1f);

            Transform upperArm = AddPrimitive(root.transform, "Upper Arm Sleeve", PrimitiveType.Capsule, new Vector3(-0.08f, -0.08f, 0.02f), new Vector3(0.13f, 0.24f, 0.13f), sleeveColor);
            upperArm.localRotation = Quaternion.Euler(0f, 0f, -38f);

            Transform forearm = AddPrimitive(root.transform, "Forearm Sleeve", PrimitiveType.Capsule, new Vector3(0.04f, 0.1f, 0.17f), new Vector3(0.115f, 0.32f, 0.115f), sleeveColor);
            forearm.localRotation = Quaternion.Euler(70f, 0f, -18f);

            Transform wrist = AddPrimitive(root.transform, "Wrist", PrimitiveType.Sphere, new Vector3(0.02f, 0.23f, 0.34f), new Vector3(0.09f, 0.07f, 0.08f), skinColor);
            wrist.localRotation = Quaternion.Euler(12f, 0f, 0f);

            Transform palm = AddPrimitive(root.transform, "Open Palm", PrimitiveType.Cube, new Vector3(0.02f, 0.28f, 0.43f), new Vector3(0.16f, 0.065f, 0.12f), gloveColor);
            palm.localRotation = Quaternion.Euler(-12f, 0f, 0f);

            for (int i = 0; i < 4; i++)
            {
                float x = -0.06f + i * 0.04f;
                Transform finger = AddPrimitive(root.transform, "Curled Finger " + (i + 1), PrimitiveType.Capsule, new Vector3(x, 0.31f, 0.52f), new Vector3(0.025f, 0.075f, 0.025f), gloveColor);
                finger.localRotation = Quaternion.Euler(58f, 0f, 0f);
            }

            Transform thumb = AddPrimitive(root.transform, "Thumb", PrimitiveType.Capsule, new Vector3(-0.095f, 0.255f, 0.45f), new Vector3(0.028f, 0.075f, 0.028f), gloveColor);
            thumb.localRotation = Quaternion.Euler(28f, 0f, 58f);

            root.SetActive(false);
            return root;
        }

        private GameObject CreateWarhammer()
        {
            GameObject root = new GameObject("View Warhammer");
            root.transform.SetParent(viewRoot, false);
            root.transform.localPosition = restLocalPosition;
            root.transform.localRotation = Quaternion.Euler(GetRestEuler(MeleeWeaponKind.Warhammer));

            AddPrimitive(root.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0f, -0.16f, 0f), new Vector3(0.06f, 0.42f, 0.06f), new Color(0.18f, 0.11f, 0.06f, 1f));
            AddPrimitive(root.transform, "Head", PrimitiveType.Cube, new Vector3(0f, 0.28f, 0f), new Vector3(0.16f, 0.16f, 0.36f), new Color(0.33f, 0.34f, 0.36f, 1f));
            AddPrimitive(root.transform, "Striking Face", PrimitiveType.Cube, new Vector3(0f, 0.28f, 0.19f), new Vector3(0.13f, 0.13f, 0.025f), new Color(0.5f, 0.5f, 0.52f, 1f));
            return root;
        }

        private static Transform AddPrimitive(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                renderer.sharedMaterial = new Material(shader) { color = color };
            }

            return part.transform;
        }
    }
}
