using System.Collections;
using UnityEngine;

namespace ProjectHive.Combat
{
    [DisallowMultipleComponent]
    public sealed class AssassinationExecutionMotion : MonoBehaviour
    {
        [SerializeField] private float knifePullDistance = 0.35f;
        [SerializeField] private float knifeDropDistance = 0.45f;
        [SerializeField] private float hammerHeadDrop = 0.55f;
        [SerializeField] private float hammerFallDistance = 0.55f;
        [SerializeField] private float hammerRaiseSeconds = 0.46f;
        [SerializeField] private float hammerWindupDropSeconds = 0.14f;
        [SerializeField] private float hammerStrikeSeconds = 0.18f;
        [SerializeField] private float hammerImpactHoldSeconds = 0.26f;

        private Coroutine motionRoutine;

        public void Play(MeleeWeaponKind kind, Transform attacker)
        {
            if (motionRoutine != null)
                StopCoroutine(motionRoutine);

            motionRoutine = StartCoroutine(kind == MeleeWeaponKind.Warhammer
                ? HammerExecution(attacker)
                : KnifeExecution(attacker));
        }

        private IEnumerator KnifeExecution(Transform attacker)
        {
            Pose start = CapturePose();
            Vector3 pullDirection = attacker != null
                ? (attacker.position - transform.position)
                : -transform.forward;
            pullDirection.y = 0f;
            if (pullDirection.sqrMagnitude <= 0.0001f)
                pullDirection = -transform.forward;
            pullDirection.Normalize();

            Vector3 pulledPosition = start.position + pullDirection * knifePullDistance;
            Quaternion pulledRotation = Quaternion.LookRotation(-pullDirection, Vector3.up) *
                                        Quaternion.Euler(-8f, 0f, 7f);
            yield return AnimateRoot(start.position, start.rotation, pulledPosition, pulledRotation, 0.16f);

            Vector3 sagPosition = pulledPosition + Vector3.down * 0.12f;
            Quaternion sagRotation = Quaternion.LookRotation(-pullDirection, Vector3.up) *
                                     Quaternion.Euler(-18f, 0f, 12f);
            yield return AnimateRoot(pulledPosition, pulledRotation, sagPosition, sagRotation, 0.18f);

            Vector3 dropPosition = sagPosition + Vector3.down * knifeDropDistance + pullDirection * 0.12f;
            Quaternion dropRotation = Quaternion.LookRotation(-pullDirection, Vector3.up) *
                                      Quaternion.Euler(-68f, 0f, 28f);
            yield return AnimateRoot(sagPosition, sagRotation, dropPosition, dropRotation, 0.24f);
            motionRoutine = null;
        }

        private IEnumerator HammerExecution(Transform attacker)
        {
            Pose start = CapturePose();
            Transform head = FindGeneratedHead();
            Vector3 headStartLocalPosition = head != null ? head.localPosition : Vector3.zero;
            Quaternion headStartLocalRotation = head != null ? head.localRotation : Quaternion.identity;
            Vector3 headStartLocalScale = head != null ? head.localScale : Vector3.one;
            Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>();
            bool[] wasKinematic = CacheAndFreezeBodies(bodies);

            Vector3 impactDirection = attacker != null
                ? (transform.position - attacker.position)
                : transform.forward;
            impactDirection.y = 0f;
            if (impactDirection.sqrMagnitude <= 0.0001f)
                impactDirection = transform.forward;
            impactDirection.Normalize();

            float attackerLocalX = attacker != null
                ? transform.InverseTransformPoint(attacker.position).x
                : 0f;
            bool fallRight = attackerLocalX >= 0f;
            Vector3 fallSide = fallRight ? transform.right : -transform.right;
            fallSide.y = 0f;
            if (fallSide.sqrMagnitude <= 0.0001f)
                fallSide = fallRight ? Vector3.right : Vector3.left;
            fallSide.Normalize();

            yield return new WaitForSeconds(hammerRaiseSeconds + hammerWindupDropSeconds);

            if (head != null)
            {
                head.localPosition = headStartLocalPosition;
                head.localRotation = headStartLocalRotation;
                head.localScale = headStartLocalScale;
            }

            Quaternion pinRotation = start.rotation * Quaternion.Euler(64f, 0f, fallRight ? -12f : 12f);
            Vector3 pinPosition = start.position + impactDirection * 0.32f + Vector3.down * 0.26f;
            if (head != null)
            {
                Vector3 headLocalToRoot = transform.InverseTransformPoint(head.position);
                Vector3 targetHeadPosition = head.position + impactDirection * 0.36f + Vector3.down * 0.72f;
                pinPosition = GetRootPositionForHeadTarget(headLocalToRoot, pinRotation, targetHeadPosition);
            }

            yield return AnimateRoot(start.position, start.rotation, pinPosition, pinRotation, hammerStrikeSeconds);

            Quaternion settleRotation = start.rotation * Quaternion.Euler(72f, 0f, fallRight ? -74f : 74f);
            Vector3 settlePosition = pinPosition + fallSide * (hammerFallDistance * 0.72f) + impactDirection * 0.04f + Vector3.down * 0.04f;
            if (head != null)
            {
                Vector3 headLocalToRoot = transform.InverseTransformPoint(head.position);
                Vector3 targetHeadPosition = head.position + impactDirection * 0.18f + Vector3.down * 0.1f;
                settlePosition = GetRootPositionForHeadTarget(headLocalToRoot, settleRotation, targetHeadPosition);
            }
            settlePosition.y = Mathf.Min(settlePosition.y, start.position.y - 0.42f);

            yield return AnimateRoot(pinPosition, pinRotation, settlePosition, settleRotation, hammerImpactHoldSeconds);

            RestoreBodies(bodies, wasKinematic);
            motionRoutine = null;
        }

        private IEnumerator AnimateRoot(
            Vector3 startPosition,
            Quaternion startRotation,
            Vector3 targetPosition,
            Quaternion targetRotation,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector3.Lerp(startPosition, targetPosition, eased);
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);
                yield return null;
            }

            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }

        private static Vector3 GetRootPositionForHeadTarget(
            Vector3 headLocalToRoot,
            Quaternion targetRootRotation,
            Vector3 targetHeadPosition)
        {
            return targetHeadPosition - targetRootRotation * headLocalToRoot;
        }

        private static IEnumerator AnimateHead(
            Transform head,
            Vector3 startPosition,
            Quaternion startRotation,
            Vector3 targetPosition,
            Quaternion targetRotation,
            float duration)
        {
            if (head == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                head.localPosition = Vector3.Lerp(startPosition, targetPosition, eased);
                head.localRotation = Quaternion.Slerp(startRotation, targetRotation, eased);
                yield return null;
            }

            head.localPosition = targetPosition;
            head.localRotation = targetRotation;
        }

        private IEnumerator AnimateHammerPin(
            Transform head,
            Vector3 startHeadPosition,
            Quaternion startHeadRotation,
            Vector3 startHeadScale,
            Vector3 targetHeadPosition,
            Quaternion targetHeadRotation,
            Vector3 targetHeadScale,
            Vector3 startRootPosition,
            Quaternion startRootRotation,
            Vector3 targetRootPosition,
            Quaternion targetRootRotation,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startRootPosition, targetRootPosition, eased);
                transform.rotation = Quaternion.Slerp(startRootRotation, targetRootRotation, eased);

                if (head != null)
                {
                    head.localPosition = Vector3.Lerp(startHeadPosition, targetHeadPosition, eased);
                    head.localRotation = Quaternion.Slerp(startHeadRotation, targetHeadRotation, eased);
                    head.localScale = Vector3.Lerp(startHeadScale, targetHeadScale, eased);
                }

                yield return null;
            }

            transform.position = targetRootPosition;
            transform.rotation = targetRootRotation;
            if (head != null)
            {
                head.localPosition = targetHeadPosition;
                head.localRotation = targetHeadRotation;
                head.localScale = targetHeadScale;
            }
        }

        private IEnumerator AnimateRootAndAttachedHead(
            Transform head,
            Vector3 startRootPosition,
            Quaternion startRootRotation,
            Vector3 targetRootPosition,
            Quaternion targetRootRotation,
            Vector3 startHeadPosition,
            Quaternion startHeadRotation,
            Vector3 headScale,
            Vector3 targetHeadPosition,
            Quaternion targetHeadRotation,
            Vector3 targetHeadScale,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startRootPosition, targetRootPosition, eased);
                transform.rotation = Quaternion.Slerp(startRootRotation, targetRootRotation, eased);

                if (head != null)
                {
                    head.localPosition = Vector3.Lerp(startHeadPosition, targetHeadPosition, eased);
                    head.localRotation = Quaternion.Slerp(startHeadRotation, targetHeadRotation, eased);
                    head.localScale = Vector3.Lerp(headScale, targetHeadScale, eased);
                }

                yield return null;
            }

            transform.position = targetRootPosition;
            transform.rotation = targetRootRotation;
            if (head != null)
            {
                head.localPosition = targetHeadPosition;
                head.localRotation = targetHeadRotation;
                head.localScale = targetHeadScale;
            }
        }

        private static bool[] CacheAndFreezeBodies(Rigidbody[] bodies)
        {
            bool[] wasKinematic = new bool[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (body == null)
                    continue;

                wasKinematic[i] = body.isKinematic;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            return wasKinematic;
        }

        private static void RestoreBodies(Rigidbody[] bodies, bool[] wasKinematic)
        {
            for (int i = 0; i < bodies.Length && i < wasKinematic.Length; i++)
            {
                Rigidbody body = bodies[i];
                if (body != null)
                    body.isKinematic = wasKinematic[i];
            }
        }

        private Transform FindGeneratedHead()
        {
            Transform generated = transform.Find("Generated Humanoid Target");
            return generated != null ? generated.Find("Head Hit Zone") : null;
        }

        private Pose CapturePose()
        {
            return new Pose(transform.position, transform.rotation);
        }

        private void OnValidate()
        {
            knifePullDistance = Mathf.Max(0f, knifePullDistance);
            knifeDropDistance = Mathf.Max(0f, knifeDropDistance);
            hammerHeadDrop = Mathf.Max(0f, hammerHeadDrop);
            hammerFallDistance = Mathf.Max(0f, hammerFallDistance);
            hammerRaiseSeconds = Mathf.Max(0f, hammerRaiseSeconds);
            hammerWindupDropSeconds = Mathf.Max(0f, hammerWindupDropSeconds);
            hammerStrikeSeconds = Mathf.Max(0.01f, hammerStrikeSeconds);
            hammerImpactHoldSeconds = Mathf.Max(0.01f, hammerImpactHoldSeconds);
        }
    }
}
