using System;
using ProjectHive.Data.Items;
using UnityEngine;

namespace ProjectHive.Core.Contracts
{
    public interface ILootSource : IInteractable, IInteractionPresentation
    {
        string LootContainerId { get; }
        bool IsDepleted { get; }
    }

    [Serializable]
    public readonly struct WorldItemDropRequest
    {
        public WorldItemDropRequest(
            in ItemStack item,
            Vector3 position,
            Quaternion rotation,
            GameObject owner)
        {
            Item = item;
            Position = position;
            Rotation = rotation;
            Owner = owner;
        }

        public ItemStack Item { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public GameObject Owner { get; }
    }

    public enum WorldItemDropResult
    {
        Spawned = 0,
        InvalidItem = 1,
        MissingPrefab = 2,
        InvalidPosition = 3,
        PoolUnavailable = 4
    }

    public interface IWorldItemDropService
    {
        WorldItemDropResult TrySpawnDrop(
            in WorldItemDropRequest request,
            out GameObject spawnedObject);
    }
}
