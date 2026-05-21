using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// CL-234 (A-1/A-2): UI 패널 GameObject 가 활성/비활성될 때 <see cref="UIInputBlocker"/> 카운터를
    /// 자동 증가/감소.
    ///
    /// 인벤토리·상점·보상창·일시정지·재능 등 게임플레이 입력을 차단해야 하는 패널 prefab 의
    /// 루트에 부착. OnEnable 시 Acquire, OnDisable 시 Release.
    ///
    /// gameObject.SetActive(true/false) 만으로 자동 동작 — 추가 와이어링 불필요.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/UI Input Blocker Source")]
    public sealed class UIInputBlockerSource : MonoBehaviour
    {
        private bool _acquired;

        private void OnEnable()
        {
            if (_acquired) return;
            UIInputBlocker.Acquire();
            _acquired = true;
        }

        private void OnDisable()
        {
            if (!_acquired) return;
            UIInputBlocker.Release();
            _acquired = false;
        }
    }
}
