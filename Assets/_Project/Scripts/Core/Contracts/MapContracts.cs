using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectHive.Core.Contracts
{
    public enum MapLocationKind
    {
        Bunker = 0,
        Merchant = 1,
        Landmark = 2
    }

    [Serializable]
    public readonly struct MapLocationSnapshot
    {
        public MapLocationSnapshot(
            string locationId,
            string displayName,
            MapLocationKind kind,
            Vector3 worldPosition)
        {
            LocationId = locationId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
            WorldPosition = worldPosition;
        }

        public string LocationId { get; }
        public string DisplayName { get; }
        public MapLocationKind Kind { get; }
        public Vector3 WorldPosition { get; }
    }

    public interface IMapLocationProvider
    {
        IReadOnlyList<MapLocationSnapshot> Locations { get; }
        event Action LocationsChanged;
    }
}
