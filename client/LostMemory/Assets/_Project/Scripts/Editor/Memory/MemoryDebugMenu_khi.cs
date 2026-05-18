using LostMemory.Memory;
using UnityEditor;
using UnityEngine;

namespace LostMemory.Editor.Memory
{
    /// <summary>
    /// 퍼즐 UI 회귀 테스트용 디버그 메뉴.
    /// 원본 MemoryProgressTracker 를 손대지 않고, MemoryMetaService 를 직접 조작.
    ///
    /// 메뉴:
    ///   - LostMemory/Memory/Debug/Grant 10000 Shards (khi)
    ///   - LostMemory/Memory/Debug/Delete Save Data (khi)
    ///   - LostMemory/Memory/Debug/Log Save Data (khi)
    /// </summary>
    public static class MemoryDebugMenu_khi
    {
        [MenuItem("LostMemory/Memory/Debug/Grant 10000 Shards (khi)")]
        public static void Grant10000()
        {
            MemorySaveData save = MemoryMetaService.Load();
            save.AccumulatedShards += 10000;
            MemoryMetaService.Save(save);
            Debug.Log($"[MemoryDebugMenu_khi] +10000 → AccumulatedShards={save.AccumulatedShards}");
        }

        [MenuItem("LostMemory/Memory/Debug/Delete Save Data (khi)")]
        public static void DeleteAll()
        {
            if (!EditorUtility.DisplayDialog(
                "기억 저장 데이터 삭제",
                "PlayerPrefs 의 MemorySaveData 를 완전히 삭제합니다. 계속할까요?",
                "삭제", "취소"))
            {
                return;
            }

            MemoryMetaService.DeleteAll();
            Debug.Log("[MemoryDebugMenu_khi] Save data deleted.");
        }

        [MenuItem("LostMemory/Memory/Debug/Log Save Data (khi)")]
        public static void LogSaveData()
        {
            MemorySaveData save = MemoryMetaService.Load();
            string ids = save.UnlockedPieceIds.Count > 0
                ? string.Join(", ", save.UnlockedPieceIds)
                : "(none)";
            Debug.Log(
                $"[MemoryDebugMenu_khi] AccumulatedShards={save.AccumulatedShards} " +
                $"UnlockedPieces=[{ids}]");
        }
    }
}
