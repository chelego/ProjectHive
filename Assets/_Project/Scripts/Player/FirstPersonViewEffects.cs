using UnityEngine;

namespace ProjectHive.Player
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonViewEffects : MonoBehaviour
    {
        [Header("Walk Bob")]
        [SerializeField, Min(0f)] private float walkFrequency = 8.5f;
        [SerializeField, Min(0f)] private float walkVerticalAmplitude = 0.035f;
        [SerializeField, Min(0f)] private float walkHorizontalAmplitude = 0.022f;

        [Header("Run Bob")]
        [SerializeField, Min(0f)] private float runFrequency = 12.5f;
        [SerializeField, Min(0f)] private float runVerticalAmplitude = 0.055f;
        [SerializeField, Min(0f)] private float runHorizontalAmplitude = 0.035f;
        [SerializeField, Min(0f)] private float runSpeedThreshold = 5.2f;

        [Header("Smoothing")]
        [SerializeField, Min(0.01f)] private float returnSmoothTime = 0.09f;
        [SerializeField, Min(0f)] private float landingKick = 0.055f;
        [SerializeField, Min(0f)] private float landingRecovery = 8f;

        private CharacterController characterController;
        private Vector3 restLocalPosition;
        private Vector3 smoothVelocity;
        private float bobPhase;
        private float landingOffset;
        private bool wasGrounded;

        private void Awake()
        {
            characterController = GetComponentInParent<CharacterController>();
            restLocalPosition = transform.localPosition;
            wasGrounded = characterController != null && characterController.isGrounded;
        }

        private void LateUpdate()
        {
            if (characterController == null)
                return;

            Vector3 horizontalVelocity = characterController.velocity;
            horizontalVelocity.y = 0f;
            float speed = horizontalVelocity.magnitude;
            bool grounded = characterController.isGrounded;

            if (grounded && !wasGrounded)
                landingOffset = -landingKick;

            wasGrounded = grounded;
            landingOffset = Mathf.MoveTowards(landingOffset, 0f, landingRecovery * Time.deltaTime);

            Vector3 bobOffset = Vector3.zero;
            if (grounded && speed > 0.15f)
            {
                bool running = speed >= runSpeedThreshold;
                float frequency = running ? runFrequency : walkFrequency;
                float verticalAmplitude = running ? runVerticalAmplitude : walkVerticalAmplitude;
                float horizontalAmplitude = running ? runHorizontalAmplitude : walkHorizontalAmplitude;

                bobPhase += Time.deltaTime * frequency;
                bobOffset.x = Mathf.Cos(bobPhase * 0.5f) * horizontalAmplitude;
                bobOffset.y = Mathf.Sin(bobPhase) * verticalAmplitude;
            }
            else
            {
                bobPhase = Mathf.MoveTowards(bobPhase, 0f, Time.deltaTime * walkFrequency);
            }

            Vector3 target = restLocalPosition + bobOffset + Vector3.up * landingOffset;
            transform.localPosition = Vector3.SmoothDamp(
                transform.localPosition,
                target,
                ref smoothVelocity,
                returnSmoothTime);
        }
    }
}
