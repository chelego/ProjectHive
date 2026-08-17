using ProjectHive.Player;
using ProjectHive.Core.Contracts;
using UnityEngine;

namespace ProjectHive.Gameplay.Raid
{
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class PrototypeExtractionZone : MonoBehaviour
    {
        [SerializeField] private PrototypeExtractionGate gate;

        public void Configure(PrototypeExtractionGate extractionGate)
        {
            gate = extractionGate;
        }

        private void Reset()
        {
            Collider zoneCollider = GetComponent<Collider>();
            zoneCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (gate == null || gate.State != ExtractionGateState.Open)
                return;

            FirstPersonMotor player = other.GetComponentInParent<FirstPersonMotor>();
            if (player == null)
                return;

            VerticalSliceRaidController controller = FindFirstObjectByType<VerticalSliceRaidController>();
            controller?.CompleteExtraction(gate, player.gameObject);
        }
    }
}
