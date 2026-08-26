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

        private static readonly Vector3 KnifeTipLocalPosition = new Vector3(0.004f, 0.395f, 0.022f);

        private GameObject knifeObject;
        private GameObject warhammerObject;
        private GameObject supportArmObject;
        private Coroutine motionRoutine;
        private bool visible = true;

        public bool IsMotionPlaying => motionRoutine != null;

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

        public void PlayAssassination(MeleeWeaponKind kind, GameObject target = null)
        {
            Equip(kind);
            StartMotion(kind == MeleeWeaponKind.Warhammer ? WarhammerExecution() : KnifeNeckStab(target));
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

            motionRoutine = StartCoroutine(RunMotion(routine));
        }

        private IEnumerator RunMotion(IEnumerator routine)
        {
            yield return routine;
            motionRoutine = null;
        }

        private IEnumerator KnifeSlash()
        {
            Camera viewCamera = viewRoot != null ? viewRoot.GetComponent<Camera>() : null;
            Vector3 cameraRestPosition = viewRoot != null ? viewRoot.localPosition : Vector3.zero;
            Quaternion cameraRestRotation = viewRoot != null ? viewRoot.localRotation : Quaternion.identity;
            float cameraRestFov = viewCamera != null ? viewCamera.fieldOfView : 60f;

            yield return AnimateKnifeStep(
                restLocalPosition + new Vector3(0.08f, -0.04f, -0.12f),
                restLocalEuler + new Vector3(34f, -56f, 42f),
                cameraRestPosition,
                cameraRestRotation,
                cameraRestFov,
                0.07f);

            yield return AnimateKnifeStep(
                restLocalPosition + new Vector3(-0.1f, 0.02f, 0.38f),
                restLocalEuler + new Vector3(-24f, 18f, -46f),
                cameraRestPosition,
                cameraRestRotation,
                cameraRestFov,
                0.09f);

            yield return AnimateKnifeStep(
                restLocalPosition + new Vector3(0.02f, -0.02f, 0.16f),
                restLocalEuler + new Vector3(2f, -32f, 18f),
                cameraRestPosition,
                cameraRestRotation,
                cameraRestFov,
                0.14f);

            yield return Animate(restLocalPosition, restLocalEuler, 0.08f);

            if (viewRoot != null)
            {
                viewRoot.localPosition = cameraRestPosition;
                viewRoot.localRotation = cameraRestRotation;
            }
            if (viewCamera != null)
                viewCamera.fieldOfView = cameraRestFov;
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

        private IEnumerator KnifeNeckStab(GameObject target)
        {
            Camera viewCamera = viewRoot != null ? viewRoot.GetComponent<Camera>() : null;
            Vector3 cameraRestPosition = viewRoot != null ? viewRoot.localPosition : Vector3.zero;
            Quaternion cameraRestRotation = viewRoot != null ? viewRoot.localRotation : Quaternion.identity;
            float cameraRestFov = viewCamera != null ? viewCamera.fieldOfView : 60f;
            float knifeReadyFov = Mathf.Max(cameraRestFov - 2f, 56f);
            float knifePunctureFov = Mathf.Max(cameraRestFov - 8f, 46f);
            float knifeImpactFov = Mathf.Max(cameraRestFov - 10f, 44f);
            float knifeRecoverFov = Mathf.Max(cameraRestFov - 3f, 55f);
            Vector3 neckWorld = ResolveTargetNeckPosition(target);
            Quaternion executionFrameRotation = cameraRestRotation;
            Quaternion readyFrameRotation = cameraRestRotation;
            Vector3 neckLocal = viewRoot != null
                ? viewRoot.InverseTransformPoint(neckWorld)
                : new Vector3(0f, 0f, 1.1f);
            neckLocal = new Vector3(
                Mathf.Clamp(neckLocal.x, -0.18f, 0.18f),
                Mathf.Clamp(neckLocal.y - 0.04f, -0.08f, 0.16f),
                Mathf.Clamp(neckLocal.z, 0.72f, 1.05f));
            Vector3 targetRight = ResolveTargetRight(target);
            Vector3 targetForward = ResolveTargetForward(target);
            Vector3 readyTipLocal = ResolveExecutionTipLocal(neckWorld + targetRight * 0.24f - targetForward * 0.08f + Vector3.down * 0.045f, neckLocal);
            Vector3 punctureTipLocal = ResolveExecutionTipLocal(neckWorld + targetRight * 0.08f + targetForward * 0.005f + Vector3.down * 0.035f, neckLocal);
            Vector3 buriedTipLocal = ResolveExecutionTipLocal(neckWorld - targetRight * 0.12f + targetForward * 0.075f + Vector3.down * 0.01f, neckLocal);
            Vector3 pulloutTipLocal = ResolveExecutionTipLocal(neckWorld + targetRight * 0.16f - targetForward * 0.08f + Vector3.down * 0.035f, neckLocal);

            Quaternion reverseGripReadyRotation = GetKnifeReverseGripRotation(new Vector3(-0.16f, -0.98f, 0.08f), 18f);
            Quaternion punctureRotation = GetKnifeReverseGripRotation(new Vector3(-0.28f, -0.94f, 0.2f), 8f);
            Quaternion buriedRotation = GetKnifeReverseGripRotation(new Vector3(-0.42f, -0.88f, 0.2f), -6f);
            Quaternion pulloutRotation = GetKnifeReverseGripRotation(new Vector3(-0.22f, -0.96f, 0.1f), 14f);
            Vector3 reverseGripReadyPosition = GetKnifeRootPositionForTip(readyTipLocal, reverseGripReadyRotation);
            Vector3 puncturePosition = GetKnifeRootPositionForTip(punctureTipLocal, punctureRotation);
            Vector3 buriedPosition = GetKnifeRootPositionForTip(buriedTipLocal, buriedRotation);

            if (supportArmObject != null)
                supportArmObject.SetActive(false);

            yield return AnimateKnifeStep(
                reverseGripReadyPosition,
                reverseGripReadyRotation,
                cameraRestPosition,
                readyFrameRotation,
                knifeReadyFov,
                0.24f);

            yield return AnimateKnifeStep(
                puncturePosition,
                punctureRotation,
                cameraRestPosition,
                executionFrameRotation,
                knifePunctureFov,
                0.14f);

            yield return AnimateKnifeStep(
                buriedPosition,
                buriedRotation,
                cameraRestPosition,
                executionFrameRotation,
                knifeImpactFov,
                0.24f);

            yield return ShakeKnifeCamera(cameraRestPosition, executionFrameRotation, knifeImpactFov, 0.16f);

            yield return AnimateKnifeStep(
                GetKnifeRootPositionForTip(pulloutTipLocal, pulloutRotation),
                pulloutRotation,
                cameraRestPosition,
                readyFrameRotation,
                knifeRecoverFov,
                0.18f);

            yield return AnimateKnifeStep(
                restLocalPosition + new Vector3(0.04f, -0.02f, 0.08f),
                Quaternion.Euler(restLocalEuler + new Vector3(10f, -34f, 26f)),
                cameraRestPosition,
                cameraRestRotation,
                cameraRestFov,
                0.3f);
            yield return Animate(restLocalPosition, restLocalEuler, 0.1f);

            if (viewRoot != null)
            {
                viewRoot.localPosition = cameraRestPosition;
                viewRoot.localRotation = cameraRestRotation;
            }
            if (viewCamera != null)
                viewCamera.fieldOfView = cameraRestFov;
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

        private IEnumerator AnimateKnifeStep(
            Vector3 targetKnifePosition,
            Vector3 targetKnifeEuler,
            Vector3 targetCameraPosition,
            Quaternion targetCameraRotation,
            float targetFieldOfView,
            float duration)
        {
            return AnimateKnifeStep(
                targetKnifePosition,
                Quaternion.Euler(targetKnifeEuler),
                targetCameraPosition,
                targetCameraRotation,
                targetFieldOfView,
                duration);
        }

        private IEnumerator AnimateKnifeStep(
            Vector3 targetKnifePosition,
            Quaternion targetKnifeRotation,
            Vector3 targetCameraPosition,
            Quaternion targetCameraRotation,
            float targetFieldOfView,
            float duration)
        {
            Transform knife = CurrentModel;
            if (knife == null)
                yield break;

            Vector3 startKnifePosition = knife.localPosition;
            Quaternion startKnifeRotation = knife.localRotation;
            Camera viewCamera = viewRoot != null ? viewRoot.GetComponent<Camera>() : null;
            Vector3 startCameraPosition = viewRoot != null ? viewRoot.localPosition : Vector3.zero;
            Quaternion startCameraRotation = viewRoot != null ? viewRoot.localRotation : Quaternion.identity;
            float startFieldOfView = viewCamera != null ? viewCamera.fieldOfView : targetFieldOfView;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                knife.localPosition = Vector3.Lerp(startKnifePosition, targetKnifePosition, eased);
                knife.localRotation = Quaternion.Slerp(startKnifeRotation, targetKnifeRotation, eased);

                if (viewRoot != null)
                {
                    viewRoot.localPosition = Vector3.Lerp(startCameraPosition, targetCameraPosition, eased);
                    viewRoot.localRotation = Quaternion.Slerp(startCameraRotation, targetCameraRotation, eased);
                }

                if (viewCamera != null)
                    viewCamera.fieldOfView = Mathf.Lerp(startFieldOfView, targetFieldOfView, eased);

                yield return null;
            }

            knife.localPosition = targetKnifePosition;
            knife.localRotation = targetKnifeRotation;
            if (viewRoot != null)
            {
                viewRoot.localPosition = targetCameraPosition;
                viewRoot.localRotation = targetCameraRotation;
            }
            if (viewCamera != null)
                viewCamera.fieldOfView = targetFieldOfView;
        }

        private IEnumerator AnimateKnifeExecutionStep(
            Vector3 targetKnifePosition,
            Quaternion targetKnifeRotation,
            Vector3 targetArmPosition,
            Quaternion targetArmRotation,
            Vector3 targetCameraPosition,
            Quaternion targetCameraRotation,
            float targetFieldOfView,
            float duration)
        {
            Transform knife = CurrentModel;
            Transform arm = supportArmObject != null && supportArmObject.activeSelf
                ? supportArmObject.transform
                : null;
            if (knife == null)
                yield break;

            Vector3 startKnifePosition = knife.localPosition;
            Quaternion startKnifeRotation = knife.localRotation;
            Vector3 startArmPosition = arm != null ? arm.localPosition : targetArmPosition;
            Quaternion startArmRotation = arm != null ? arm.localRotation : targetArmRotation;
            Camera viewCamera = viewRoot != null ? viewRoot.GetComponent<Camera>() : null;
            Vector3 startCameraPosition = viewRoot != null ? viewRoot.localPosition : Vector3.zero;
            Quaternion startCameraRotation = viewRoot != null ? viewRoot.localRotation : Quaternion.identity;
            float startFieldOfView = viewCamera != null ? viewCamera.fieldOfView : targetFieldOfView;
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

                if (viewRoot != null)
                {
                    viewRoot.localPosition = Vector3.Lerp(startCameraPosition, targetCameraPosition, eased);
                    viewRoot.localRotation = Quaternion.Slerp(startCameraRotation, targetCameraRotation, eased);
                }

                if (viewCamera != null)
                    viewCamera.fieldOfView = Mathf.Lerp(startFieldOfView, targetFieldOfView, eased);

                yield return null;
            }

            knife.localPosition = targetKnifePosition;
            knife.localRotation = targetKnifeRotation;
            if (arm != null)
            {
                arm.localPosition = targetArmPosition;
                arm.localRotation = targetArmRotation;
            }
            if (viewRoot != null)
            {
                viewRoot.localPosition = targetCameraPosition;
                viewRoot.localRotation = targetCameraRotation;
            }
            if (viewCamera != null)
                viewCamera.fieldOfView = targetFieldOfView;
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

        private IEnumerator ShakeKnifeCamera(Vector3 restPosition, Quaternion restRotation, float fieldOfView, float duration)
        {
            Camera viewCamera = viewRoot != null ? viewRoot.GetComponent<Camera>() : null;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float strength = (1f - normalized) * executionImpactShake * 0.25f;
                float x = Mathf.Sin(Time.time * 123f) * strength;
                float y = Mathf.Cos(Time.time * 97f) * strength;

                if (viewRoot != null)
                {
                    viewRoot.localPosition = restPosition + new Vector3(x, y, 0f);
                    viewRoot.localRotation = restRotation * Quaternion.Euler(y * 28f, x * 28f, x * 34f);
                }

                if (viewCamera != null)
                    viewCamera.fieldOfView = fieldOfView;

                yield return null;
            }
        }

        private Vector3 ResolveTargetNeckPosition(GameObject target)
        {
            if (target == null)
            {
                if (viewRoot != null)
                    return viewRoot.position + viewRoot.forward * 1.1f;

                return transform.position + transform.forward * 1.1f + Vector3.up * 1.35f;
            }

            Transform generated = target.transform.Find("Generated Humanoid Target");
            Transform head = generated != null ? generated.Find("Head Hit Zone") : null;
            if (head == null)
                head = FindChildByName(target.transform, "Head Hit Zone");

            if (head != null)
                return head.position + Vector3.down * 0.08f;

            return target.transform.position + Vector3.up * 1.35f;
        }

        private Vector3 ResolveExecutionTipLocal(Vector3 worldTipPosition, Vector3 fallbackLocalPosition)
        {
            if (viewRoot == null)
                return fallbackLocalPosition;

            Vector3 local = viewRoot.InverseTransformPoint(worldTipPosition);
            return new Vector3(
                Mathf.Clamp(local.x, -0.28f, 0.28f),
                Mathf.Clamp(local.y - 0.04f, -0.11f, 0.16f),
                Mathf.Clamp(local.z, 0.68f, 1.12f));
        }

        private Vector3 ResolveTargetRight(GameObject target)
        {
            if (target == null)
                return viewRoot != null ? viewRoot.right : transform.right;

            Vector3 right = target.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude <= 0.0001f)
                right = viewRoot != null ? viewRoot.right : transform.right;

            right.y = 0f;
            return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        }

        private Vector3 ResolveTargetForward(GameObject target)
        {
            if (target == null)
                return viewRoot != null ? viewRoot.forward : transform.forward;

            Vector3 forward = target.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = viewRoot != null ? viewRoot.forward : transform.forward;

            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private static Vector3 GetKnifeRootPositionForTip(Vector3 targetTipPosition, Quaternion knifeRotation)
        {
            return targetTipPosition - knifeRotation * KnifeTipLocalPosition;
        }

        private static Vector3 GetKnifeRootPositionForTip(Vector3 targetTipPosition, Vector3 knifeEuler)
        {
            return targetTipPosition - Quaternion.Euler(knifeEuler) * KnifeTipLocalPosition;
        }

        private static Quaternion GetKnifeReverseGripRotation(Vector3 bladeDirection, float rollDegrees)
        {
            if (bladeDirection.sqrMagnitude <= 0.0001f)
                bladeDirection = Vector3.down;

            bladeDirection.Normalize();
            return Quaternion.AngleAxis(rollDegrees, bladeDirection) *
                   Quaternion.FromToRotation(Vector3.up, bladeDirection);
        }

        private static Quaternion GetKnifeExecutionFrameRotation(Quaternion currentLocalRotation)
        {
            Vector3 currentEuler = currentLocalRotation.eulerAngles;
            float currentPitch = NormalizeAngle(currentEuler.x);
            float framedPitch = Mathf.Lerp(currentPitch, 12f, 0.78f);
            return Quaternion.Euler(framedPitch, 0f, 0f);
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f)
                angle -= 360f;
            while (angle < -180f)
                angle += 360f;

            return angle;
        }

        private Quaternion ResolveLocalLookRotation(Vector3 worldTarget, Quaternion fallbackLocalRotation)
        {
            if (viewRoot == null)
                return fallbackLocalRotation;

            Vector3 direction = worldTarget - viewRoot.position;
            if (direction.sqrMagnitude <= 0.0001f)
                return fallbackLocalRotation;

            Quaternion worldLook = Quaternion.LookRotation(direction.normalized, Vector3.up);
            return viewRoot.parent != null
                ? Quaternion.Inverse(viewRoot.parent.rotation) * worldLook
                : worldLook;
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                    return child;

                Transform match = FindChildByName(child, childName);
                if (match != null)
                    return match;
            }

            return null;
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

            Color blackenedSteel = new Color(0.23f, 0.27f, 0.29f, 1f);
            Color sharpenedEdge = new Color(0.78f, 0.84f, 0.88f, 1f);
            Color brass = new Color(0.62f, 0.44f, 0.18f, 1f);
            Color leather = new Color(0.06f, 0.045f, 0.035f, 1f);

            AddPrimitive(root.transform, "Wrapped Grip", PrimitiveType.Cylinder, new Vector3(0f, -0.15f, 0f), new Vector3(0.052f, 0.2f, 0.052f), leather);
            Transform pommel = AddPrimitive(root.transform, "Heavy Pommel", PrimitiveType.Sphere, new Vector3(0f, -0.29f, 0f), new Vector3(0.085f, 0.06f, 0.085f), brass);
            pommel.localRotation = Quaternion.Euler(0f, 0f, 45f);
            AddPrimitive(root.transform, "Offset Guard", PrimitiveType.Cube, new Vector3(0.018f, -0.035f, 0f), new Vector3(0.22f, 0.026f, 0.045f), brass);
            Transform spine = AddPrimitive(root.transform, "Dark Blade Spine", PrimitiveType.Cube, new Vector3(-0.012f, 0.12f, 0.018f), new Vector3(0.052f, 0.42f, 0.022f), blackenedSteel);
            spine.localRotation = Quaternion.Euler(0f, 0f, -4f);
            Transform edge = AddPrimitive(root.transform, "Bright Cutting Edge", PrimitiveType.Cube, new Vector3(0.026f, 0.13f, 0.028f), new Vector3(0.018f, 0.39f, 0.012f), sharpenedEdge);
            edge.localRotation = Quaternion.Euler(0f, 0f, -9f);
            AddPrimitive(root.transform, "Needle Tip", PrimitiveType.Cube, new Vector3(0.004f, 0.35f, 0.022f), new Vector3(0.034f, 0.09f, 0.014f), sharpenedEdge);
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
