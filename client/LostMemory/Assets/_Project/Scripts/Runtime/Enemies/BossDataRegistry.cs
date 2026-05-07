// CL-178 draft (김회인) — 비활성 보관.
// 짝 클래스 BerthaBossDataInjector 와 함께 비활성. 활성화 시 둘 다 #if false → #if true.
// 자세한 사유: docs/khi/cl178_implementation.md
#if false
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Enemies
{
    /// <summary>
    /// CL-178: 게임에 적용할 BossData 등록 SO. Resources 폴더에 두고 BerthaBossDataInjector 가 로드.
    /// 일반 몹 추가 시 EnemyDataRegistry 분리 또는 본 SO 확장.
    /// </summary>
    [CreateAssetMenu(fileName = "BossDataRegistry",
                     menuName = "LostMemory/Enemies/Boss Data Registry",
                     order = 12)]
    public class BossDataRegistry : ScriptableObject
    {
        [Tooltip("게임에 적용할 BossData 인스턴스. Resources 폴더에 두고 Injector 가 자동 로드.")]
        [SerializeField] private BossData[] _entries = Array.Empty<BossData>();

        public IReadOnlyList<BossData> Entries => _entries;
    }
}
#endif
