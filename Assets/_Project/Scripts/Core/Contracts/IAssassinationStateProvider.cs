using UnityEngine;

namespace ProjectHive.Core.Contracts
{
    /// <summary>
    /// 암살 대상(몹)이 지금 암살당할 수 있는 상태 여부를 전달
    /// </summary>
    public interface IAssassinationStateProvider
    {
        bool IsAssassinable { get; }
    }
}
