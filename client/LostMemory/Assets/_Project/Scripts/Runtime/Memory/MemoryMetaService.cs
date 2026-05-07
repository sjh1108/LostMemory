using UnityEngine;

namespace LostMemory.Memory
{
    /// <summary>
    /// 기억·조각 수집 상태를 PlayerPrefs(JSON) 에 저장·로드하는 static 서비스.
    /// TalentSaveService 와 동일한 static + PlayerPrefs 패턴.
    ///
    /// 호출 지점:
    ///   - MemoryProgressTracker.Awake() → Load()
    ///   - MemoryProgressTracker.SaveRunFragments() → Save()
    ///   - MemoryCompletionRewardApplicator (StatBoost 기록 시) → Load → 수정 → Save
    ///   - MemoryCollectionPanelView.Open() → Load() (읽기 전용)
    /// </summary>
    public static class MemoryMetaService
    {
        private const string SaveKey = "MemorySystem_SaveData";

        /// <summary>현재 저장 데이터를 PlayerPrefs 에 JSON 으로 기록.</summary>
        public static void Save(MemorySaveData data)
        {
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }

        /// <summary>저장된 데이터를 불러온다. 저장값이 없으면 빈 데이터를 반환.</summary>
        public static MemorySaveData Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
                return new MemorySaveData();

            string json = PlayerPrefs.GetString(SaveKey);
            return JsonUtility.FromJson<MemorySaveData>(json) ?? new MemorySaveData();
        }

        /// <summary>저장 데이터를 완전히 삭제 (개발·디버그용).</summary>
        public static void DeleteAll()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        public static bool HasSaveData() => PlayerPrefs.HasKey(SaveKey);
    }
}
