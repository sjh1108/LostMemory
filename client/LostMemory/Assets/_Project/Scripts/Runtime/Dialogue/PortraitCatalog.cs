using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Dialogue
{
    /// <summary>
    /// portraitId(string) → Sprite 매핑.
    ///
    /// CSV 의 portraitId 컬럼이 여기서 정의된 키와 매칭되어야 한다.
    /// 예: dialogues.csv 의 "khi_neutral" → 이 카탈로그의 (id="khi_neutral", sprite=Khi_Neutral.png)
    ///
    /// inspector 에서 entries 리스트에 (id, sprite) 페어를 등록한다.
    /// 빠진 id 가 조회되면 fallback sprite(null) 반환 + 경고 1회 로그.
    /// </summary>
    [CreateAssetMenu(fileName = "PortraitCatalog", menuName = "LostMemory/Dialogue/Portrait Catalog")]
    public sealed class PortraitCatalog : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string id;
            public Sprite sprite;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [Tooltip("매칭 실패 시 사용할 대체 sprite (null 허용 — 그러면 portrait 가 비어 보임)")]
        [SerializeField] private Sprite fallbackSprite;

        private Dictionary<string, Sprite> _lookup;
        private readonly HashSet<string> _warnedMissing = new HashSet<string>();

        private void OnEnable()
        {
            BuildLookup();
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, Sprite>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (string.IsNullOrEmpty(e.id)) continue;
                if (_lookup.ContainsKey(e.id))
                {
                    Debug.LogWarning($"[PortraitCatalog] 중복 id '{e.id}' (index {i}) — 첫 번째만 사용됨.", this);
                    continue;
                }
                _lookup[e.id] = e.sprite;
            }
        }

        public Sprite Get(string portraitId)
        {
            if (_lookup == null) BuildLookup();
            if (string.IsNullOrEmpty(portraitId)) return fallbackSprite;

            if (_lookup.TryGetValue(portraitId, out Sprite sprite))
            {
                return sprite;
            }

            if (_warnedMissing.Add(portraitId))
            {
                Debug.LogWarning($"[PortraitCatalog] portraitId '{portraitId}' 매칭 실패 — fallback 사용.", this);
            }
            return fallbackSprite;
        }
    }
}
