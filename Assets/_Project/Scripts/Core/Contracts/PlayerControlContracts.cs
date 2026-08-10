using System;
using UnityEngine;

namespace ProjectHive.Core.Contracts
{
    [Flags]
    public enum PlayerControlChannel
    {
        None = 0,
        Move = 1 << 0,
        Look = 1 << 1,
        Jump = 1 << 2,
        Crouch = 1 << 3,
        Sprint = 1 << 4,
        Parkour = 1 << 5,
        Combat = 1 << 6,
        Reload = 1 << 7,
        Interact = 1 << 8,
        Inventory = 1 << 9,
        Map = 1 << 10,
        Menu = 1 << 11,
        All = Move | Look | Jump | Crouch | Sprint | Parkour |
              Combat | Reload | Interact | Inventory | Map | Menu
    }

    public enum PlayerControlContext
    {
        Gameplay = 0,
        Inventory = 1,
        Map = 2,
        Menu = 3,
        Cutscene = 4,
        Dead = 5
    }

    [Serializable]
    public readonly struct PlayerControlMode
    {
        public PlayerControlMode(
            PlayerControlContext context,
            PlayerControlChannel allowedChannels,
            bool cursorVisible,
            CursorLockMode cursorLockMode)
        {
            Context = context;
            AllowedChannels = allowedChannels;
            CursorVisible = cursorVisible;
            CursorLockMode = cursorLockMode;
        }

        public PlayerControlContext Context { get; }
        public PlayerControlChannel AllowedChannels { get; }
        public bool CursorVisible { get; }
        public CursorLockMode CursorLockMode { get; }

        public bool Allows(PlayerControlChannel channel)
        {
            return (AllowedChannels & channel) == channel;
        }

        public static PlayerControlMode Gameplay => new PlayerControlMode(
            PlayerControlContext.Gameplay,
            PlayerControlChannel.All,
            false,
            UnityEngine.CursorLockMode.Locked);

        public static PlayerControlMode Inventory => new PlayerControlMode(
            PlayerControlContext.Inventory,
            PlayerControlChannel.Move |
            PlayerControlChannel.Jump |
            PlayerControlChannel.Crouch |
            PlayerControlChannel.Inventory,
            true,
            UnityEngine.CursorLockMode.None);

        public static PlayerControlMode Map => new PlayerControlMode(
            PlayerControlContext.Map,
            PlayerControlChannel.Move | PlayerControlChannel.Map,
            false,
            UnityEngine.CursorLockMode.Locked);

        public static PlayerControlMode Menu => new PlayerControlMode(
            PlayerControlContext.Menu,
            PlayerControlChannel.Menu,
            true,
            UnityEngine.CursorLockMode.None);

        public static PlayerControlMode Cutscene => new PlayerControlMode(
            PlayerControlContext.Cutscene,
            PlayerControlChannel.None,
            false,
            UnityEngine.CursorLockMode.Locked);

        public static PlayerControlMode Dead => new PlayerControlMode(
            PlayerControlContext.Dead,
            PlayerControlChannel.None,
            false,
            UnityEngine.CursorLockMode.Locked);
    }

    public interface IPlayerControlModeService
    {
        PlayerControlMode CurrentMode { get; }
        event Action<PlayerControlMode> ModeChanged;
        IDisposable PushMode(in PlayerControlMode mode, UnityEngine.Object owner);
    }
}
