using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Data
{
    /// <summary>
    /// CL-230: 무기 속성 종류. 디자이너가 새 속성 추가 시 이 enum 에 항목 추가 + Catalog 에 entry 등록.
    /// </summary>
    public enum WeaponElement
    {
        None = 0,
        Fire = 1,
        Ice = 2,
        Lightning = 3,
        Dark = 4,
        Holy = 5,
        Poison = 6,
        // 신규 속성 추가 시 여기에 enum 항목 추가.
    }

    /// <summary>
    /// CL-230: 한 속성의 시각 데이터.
    /// SlashElement 셰이더의 material property 와 매핑됨.
    /// </summary>
    [Serializable]
    public class WeaponElementVisual
    {
        public WeaponElement element = WeaponElement.None;

        [Header("Colors")]
        [Tooltip("sprite 안쪽 (알파 1.0 근처) 색. 보통 흰/노랑 같은 hot core.")]
        public Color innerColor = Color.white;
        [Tooltip("sprite 가장자리 (알파 0.x) 색. 속성의 정체성 색 (Fire=빨강, Ice=파랑 등).")]
        public Color outerColor = Color.red;

        [Header("Gradient Shape")]
        [Range(0f, 1f)]
        [Tooltip("inner ↔ outer 전환점. 0.5 = 알파 0.5 기준으로 분리.")]
        public float gradientThreshold = 0.5f;
        [Range(0.01f, 1f)]
        [Tooltip("전환 부드러움. 작을수록 sharp outline, 클수록 부드러운 그라디언트.")]
        public float gradientSoftness = 0.3f;

        [Header("Glow")]
        [Range(0f, 5f)]
        [Tooltip("안쪽 영역 밝기 boost. 0 = off, 1 = 두 배, 3+ = 강한 발광.")]
        public float glowIntensity = 0f;

        [Header("Animation")]
        [Tooltip("UV scroll 속도. (x,y) = uv per second. 화염 양수, 얼음 0, 번개는 깜빡임 대신 강한 glow.")]
        public Vector2 scrollSpeed = Vector2.zero;
    }

    /// <summary>
    /// CL-230: 속성별 시각 카탈로그. ScriptableObject 로 만들어 씬 또는 KhiSlashAnimator Inspector 에 연결.
    /// 새 속성 추가 시: enum 항목 추가 → Inspector 의 entries 에 새 WeaponElementVisual 등록.
    ///
    /// 사용:
    ///   KhiSlashAnimator 가 WeaponData.CurrentElement 조회 → 본 카탈로그에서 visual 찾음
    ///   → MaterialPropertyBlock 으로 SpriteRenderer 에 값 적용.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponElementCatalog", menuName = "LostMemory/Combat/Weapon Element Catalog")]
    public class WeaponElementCatalog : ScriptableObject
    {
        [SerializeField] private WeaponElementVisual[] entries = Array.Empty<WeaponElementVisual>();

        // enum → visual 빠른 조회 캐시 (런타임)
        private Dictionary<WeaponElement, WeaponElementVisual> _lookup;

        /// <summary>
        /// 주어진 element 에 해당하는 visual 반환. 없으면 null.
        /// </summary>
        public WeaponElementVisual Get(WeaponElement element)
        {
            EnsureLookup();
            return _lookup.TryGetValue(element, out var v) ? v : null;
        }

        /// <summary>
        /// element 가 등록되어 있는지.
        /// </summary>
        public bool Has(WeaponElement element)
        {
            EnsureLookup();
            return _lookup.ContainsKey(element);
        }

        private void EnsureLookup()
        {
            if (_lookup != null && _lookup.Count == entries.Length)
            {
                return;
            }
            _lookup = new Dictionary<WeaponElement, WeaponElementVisual>(entries.Length);
            foreach (var e in entries)
            {
                if (e == null) continue;
                _lookup[e.element] = e; // 중복 시 마지막 항목 우선
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Inspector 에서 entries 변경 시 캐시 무효화 → 다음 Get 시 재구성.
            _lookup = null;
        }
#endif
    }
}
