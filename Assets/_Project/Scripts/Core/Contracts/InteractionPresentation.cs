using UnityEngine;

namespace ProjectHive.Core.Contracts
{
    public enum InteractionKind
    {
        Generic = 0,
        Loot = 1,
        Door = 2,
        Extraction = 3,
        Trade = 4,
        Assassination = 5
    }

    public interface IInteractionPresentation
    {
        InteractionKind InteractionKind { get; }
        Transform InteractionAnchor { get; }
    }
}
