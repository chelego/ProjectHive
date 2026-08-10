using System;
using UnityEngine;

namespace ProjectHive.Core.Events
{
    public enum NoiseCategory
    {
        Unknown = 0,
        Footstep = 1,
        Gunshot = 2,
        MeleeImpact = 3,
        ThrownObject = 4,
        DoorOrMachine = 5,
        Creature = 6,
        Environment = 7,
        WildlifeAlarm = 8
    }

    public enum NoiseAffiliation
    {
        Unknown = 0,
        Player = 1,
        Monster = 2,
        Environment = 3
    }

    [Serializable]
    public readonly struct NoiseEvent
    {
        public NoiseEvent(
            Vector3 position,
            float loudness,
            float radius,
            NoiseCategory category,
            NoiseAffiliation affiliation,
            int sourceInstanceId,
            float occurredAt)
            : this(
                position,
                loudness,
                radius,
                0f,
                category,
                affiliation,
                sourceInstanceId,
                occurredAt)
        {
        }

        public NoiseEvent(
            Vector3 position,
            float loudness,
            float radius,
            float locationUncertaintyRadius,
            NoiseCategory category,
            NoiseAffiliation affiliation,
            int sourceInstanceId,
            float occurredAt)
        {
            Position = position;
            Loudness = Mathf.Max(0f, loudness);
            Radius = Mathf.Max(0f, radius);
            LocationUncertaintyRadius = Mathf.Max(0f, locationUncertaintyRadius);
            Category = category;
            Affiliation = affiliation;
            SourceInstanceId = sourceInstanceId;
            OccurredAt = occurredAt;
        }

        public Vector3 Position { get; }
        public float Loudness { get; }
        public float Radius { get; }
        public float LocationUncertaintyRadius { get; }
        public NoiseCategory Category { get; }
        public NoiseAffiliation Affiliation { get; }
        public int SourceInstanceId { get; }
        public float OccurredAt { get; }
    }
}
