using UnityEngine;

namespace ProjectHive.Gameplay.Raid
{
    [DisallowMultipleComponent]
    public sealed class PrototypeSpawnDistrict : MonoBehaviour
    {
        [SerializeField] private string districtName;
        [SerializeField, Min(1)] private int population = 10;
        [SerializeField] private Vector3[] localSpawnCandidates = new Vector3[0];

        public string DistrictName => districtName;
        public int Population => population;
        public int CandidateCount => localSpawnCandidates.Length;
        public Vector3 CandidateAt(int index) => transform.TransformPoint(localSpawnCandidates[index]);

        public void Configure(string label, int count, Vector3[] worldCandidates)
        {
            districtName = label;
            population = Mathf.Max(1, count);
            localSpawnCandidates = new Vector3[worldCandidates.Length];
            for (int i = 0; i < worldCandidates.Length; i++)
                localSpawnCandidates[i] = transform.InverseTransformPoint(worldCandidates[i]);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 0.55f, 0.8f);
            for (int i = 0; i < localSpawnCandidates.Length; i++)
                Gizmos.DrawWireSphere(CandidateAt(i) + Vector3.up, 0.6f);
        }
    }
}
