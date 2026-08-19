using ProjectHive.Core.Contracts;
using UnityEngine;

namespace ProjectHive.Gameplay.Raid
{
    [DisallowMultipleComponent]
    public sealed class PrototypeExtractionGate : MonoBehaviour, IInteractable, IInteractionPresentation, IExtractionGate
    {
        [SerializeField] private string gateId = "Bunker";
        [SerializeField, Min(0.1f)] private float openingDurationSeconds = 8f;
        [SerializeField] private Transform interactionAnchor;

        private VerticalSliceRaidController raidController;
        private ExtractionGateState state = ExtractionGateState.Locked;
        private bool isEntryOnly;
        private float openingElapsed;

        public string GateId => gateId;
        public bool IsEntryOnly => isEntryOnly;
        public ExtractionGateState State => state;
        public float OpeningDurationSeconds => openingDurationSeconds;
        public float OpeningProgress => state == ExtractionGateState.Open
            ? 1f
            : Mathf.Clamp01(openingElapsed / Mathf.Max(0.1f, openingDurationSeconds));
        public InteractionKind InteractionKind => InteractionKind.Extraction;
        public Transform InteractionAnchor => interactionAnchor != null ? interactionAnchor : transform;

        public string InteractionPrompt => state switch
        {
            ExtractionGateState.Available => "[E] 벙커 문 개방",
            ExtractionGateState.Opening => $"개방 중 {OpeningProgress * 100f:0}%",
            ExtractionGateState.Open => "개방된 벙커",
            _ => string.Empty
        };

        public void Configure(
            VerticalSliceRaidController controller,
            string id,
            float openingSeconds)
        {
            raidController = controller;
            gateId = string.IsNullOrWhiteSpace(id) ? name : id;
            openingDurationSeconds = Mathf.Max(0.1f, openingSeconds);
            interactionAnchor = transform;
        }

        public void ResetForRaid(bool entryOnly, bool available)
        {
            isEntryOnly = entryOnly;
            openingElapsed = 0f;
            state = entryOnly || !available
                ? ExtractionGateState.Locked
                : ExtractionGateState.Available;
            raidController?.NotifyGateChanged(this);
        }

        public bool CanInteract(in InteractionContext context)
        {
            return state == ExtractionGateState.Available && !isEntryOnly;
        }

        public void Interact(in InteractionContext context)
        {
            TryBeginOpening(in context);
        }

        public bool TryBeginOpening(in InteractionContext context)
        {
            if (!CanInteract(in context))
                return false;

            state = ExtractionGateState.Opening;
            openingElapsed = 0f;
            raidController?.NotifyGateChanged(this);
            return true;
        }

        private void Update()
        {
            if (state != ExtractionGateState.Opening)
                return;

            openingElapsed += Time.deltaTime;
            if (openingElapsed < openingDurationSeconds)
                return;

            openingElapsed = openingDurationSeconds;
            state = ExtractionGateState.Open;
            raidController?.NotifyGateChanged(this);
        }

        private void OnValidate()
        {
            openingDurationSeconds = Mathf.Max(0.1f, openingDurationSeconds);
        }
    }
}
