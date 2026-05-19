using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Audio
{
    /// <summary>
    /// SFX 클립별 볼륨 multiplier (0..1) 보관 ScriptableObject. Resources 로 자동 로드.
    ///
    /// 용도:
    /// - 원본 wav 녹음 레벨이 제각각이라 어떤 SFX 가 비정상적으로 크거나 작을 때
    ///   개별 클립에 0..1 multiplier 를 곱해서 균일하게 맞춤.
    /// - per-event volume (예: `attackSwingVolume: 0.9`) 와는 별개. 그건 이벤트 그룹 전체에 적용,
    ///   이쪽은 **클립 1개** 단위.
    ///
    /// 사용:
    /// - 편집: `LostMemory → SFX Balance` 메뉴 → `SfxBalanceWindow` 에서 슬라이더 + audition.
    /// - 런타임: 각 SFX play 지점에서 `SfxClipVolumeBalance.GetGain(clip)` 을 곱해 PlayOneShot.
    ///
    /// 영구 저장: ScriptableObject asset (`Assets/_Project/Audio/Resources/SfxClipVolumeBalance.asset`).
    /// 자산 commit 시 모든 개발자 / 빌드 사용자에게 동일하게 적용됨.
    /// 항목이 없는 클립은 기본 gain = 1.0 (변경 없음).
    /// </summary>
    [CreateAssetMenu(fileName = "SfxClipVolumeBalance", menuName = "Lost Memory/Audio/SFX Clip Volume Balance")]
    public class SfxClipVolumeBalance : ScriptableObject
    {
        /// <summary>gain 의 상한. 0..1 은 감쇠, 1..MaxGain 은 증폭. 2.0 = 약 +6dB = 청각상 2배.
        /// 1.0 초과 시 원본 wav 가 peak 근처면 clipping 위험 있음.</summary>
        public const float MaxGain = 2f;

        [System.Serializable]
        public struct Entry
        {
            public AudioClip clip;
            [Range(0f, MaxGain)] public float gain;
        }

        [Tooltip("클립별 gain (0..1). SfxBalanceWindow 가 자동 관리. 수동 편집 가능하지만 권장 X.")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        // 런타임 빠른 조회용 캐시.
        private Dictionary<AudioClip, float> _cache;

        private void OnEnable()
        {
            RebuildCache();
        }

        public void RebuildCache()
        {
            _cache = new Dictionary<AudioClip, float>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e.clip != null) _cache[e.clip] = Mathf.Clamp(e.gain, 0f, MaxGain);
            }
        }

        /// <summary>클립의 multiplier (0..1) 반환. 등록 없으면 1.0.</summary>
        public float GetGainFor(AudioClip clip)
        {
            if (clip == null) return 1f;
            if (_cache == null) RebuildCache();
            return _cache.TryGetValue(clip, out float g) ? g : 1f;
        }

        // ─── Editor 편집용 ───────────────────────────────────────────────────

        public IReadOnlyList<Entry> Entries => entries;

        public void SetGain(AudioClip clip, float gain)
        {
            if (clip == null) return;
            gain = Mathf.Clamp(gain, 0f, MaxGain);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].clip == clip)
                {
                    Entry e = entries[i];
                    e.gain = gain;
                    entries[i] = e;
                    RebuildCache();
                    return;
                }
            }
            entries.Add(new Entry { clip = clip, gain = gain });
            RebuildCache();
        }

        public void RemoveEntry(AudioClip clip)
        {
            if (clip == null) return;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].clip == clip) entries.RemoveAt(i);
            }
            RebuildCache();
        }

        public void ResetAll()
        {
            entries.Clear();
            RebuildCache();
        }

        // ─── Static 접근 (런타임 SFX play 지점에서 호출) ─────────────────────

        private static SfxClipVolumeBalance _instance;
        private static bool _attemptedLoad;

        /// <summary>SO 가 존재하면 그 안의 gain 을, 없거나 항목 없으면 1.0 반환.</summary>
        public static float GetGain(AudioClip clip)
        {
            if (clip == null) return 1f;
            if (_instance == null && !_attemptedLoad)
            {
                _instance = Resources.Load<SfxClipVolumeBalance>("SfxClipVolumeBalance");
                _attemptedLoad = true;
            }
            return _instance != null ? _instance.GetGainFor(clip) : 1f;
        }

        /// <summary>Editor 가 SO 를 수정한 직후 캐시 무효화 + 강제 재로드 트리거.</summary>
        public static void InvalidateStaticCache()
        {
            _instance = null;
            _attemptedLoad = false;
        }
    }
}
