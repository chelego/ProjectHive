using UnityEngine;

namespace ProjectHive.Player
{
    [DisallowMultipleComponent]
    public sealed class ParkourPrototypeObstacle : MonoBehaviour
    {
        [SerializeField] private ParkourPrototypeObstacleKind kind;
        [SerializeField] private Vector3 testDimensions = Vector3.one;
        [SerializeField] private string testPurpose = string.Empty;

        public ParkourPrototypeObstacleKind Kind => kind;
        public Vector3 TestDimensions => testDimensions;
        public string TestPurpose => testPurpose;

#if UNITY_EDITOR
        public void Configure(ParkourPrototypeObstacleKind obstacleKind, Vector3 dimensions, string purpose)
        {
            kind = obstacleKind;
            testDimensions = dimensions;
            testPurpose = purpose;
        }
#endif
    }

    public enum ParkourPrototypeObstacleKind
    {
        VaultBarrier,
        MantleWall,
        WindowPassage
    }
}
