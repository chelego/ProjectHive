using UnityEngine;

namespace ProjectHive.AI.Mob
{
    [CreateAssetMenu(menuName = "ProjectHive/Raid Encounter Layout")]
    public sealed class RaidEncounterLayout : ScriptableObject
    {
        public GameObject birdPrefab;
        public GameObject breckenPrefab;
        public Material spotterMaterial;
        public Material beamMaterial;
        public AudioClip[] birdCalls;
        public Vector3[] outdoorPoints;
        public Vector3[] patrolCenters;
        public UnityEngine.AI.NavMeshData mapNavMesh;
        public Vector3 navMeshPosition;
        public Quaternion navMeshRotation = Quaternion.identity;
        [Min(1)] public int birdsPerFlock = 5;
    }
}
