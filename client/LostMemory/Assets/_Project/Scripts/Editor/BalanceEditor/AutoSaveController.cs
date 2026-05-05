using System;
using UnityEditor;

namespace LostMemory.Editor.BalanceEditor
{
    /// <summary>
    /// Auto-save debounce 컨트롤러.
    /// 마지막 변경 후 DelaySeconds 동안 추가 변경 없으면 SaveAll 호출.
    /// 활성 편집 중에는 발화 안 함 — 데이터 손실 안전망 의미는 제한적.
    /// </summary>
    public class AutoSaveController
    {
        private const string EnabledKey = "BalanceEditor.AutoSave.Enabled";
        private const string DelayKey = "BalanceEditor.AutoSave.DelaySeconds";

        public const float MinDelay = 30f;
        public const float MaxDelay = 600f;
        public const float DefaultDelay = 60f;

        public bool Enabled { get; private set; }
        public float DelaySeconds { get; private set; }

        private double _nextSaveAt;
        private bool _hasPendingChange;
        private readonly Func<int> _getDirtyCount;
        private readonly Action _saveAction;

        public AutoSaveController(Func<int> getDirtyCount, Action saveAction)
        {
            _getDirtyCount = getDirtyCount;
            _saveAction = saveAction;
            Enabled = EditorPrefs.GetBool(EnabledKey, false);
            DelaySeconds = Math.Clamp(
                EditorPrefs.GetFloat(DelayKey, DefaultDelay), MinDelay, MaxDelay);
        }

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            EditorPrefs.SetBool(EnabledKey, enabled);
            if (enabled) ResetTimer();
            else _hasPendingChange = false;
        }

        public void SetDelay(float seconds)
        {
            DelaySeconds = Math.Clamp(seconds, MinDelay, MaxDelay);
            EditorPrefs.SetFloat(DelayKey, DelaySeconds);
        }

        /// <summary>SO 변경 시 호출. debounce 타이머 재시작.</summary>
        public void NotifyChange()
        {
            if (!Enabled) return;
            _hasPendingChange = true;
            ResetTimer();
        }

        /// <summary>SaveAll 후 호출 — 다음 변경까지 대기.</summary>
        public void NotifySaved()
        {
            _hasPendingChange = false;
        }

        private void ResetTimer()
        {
            _nextSaveAt = EditorApplication.timeSinceStartup + DelaySeconds;
        }

        /// <summary>EditorApplication.update 에서 매 프레임 호출.</summary>
        public void Tick()
        {
            if (!Enabled) return;
            if (!_hasPendingChange) return;
            if (EditorApplication.timeSinceStartup < _nextSaveAt) return;

            // Play 모드 / 컴파일 / asset 처리 중 가드
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling) return;
            if (EditorApplication.isUpdating) return;

            // dirty 없으면 호출 안 함 (이미 저장됐거나 dirty 가 외부에서 clear 됨)
            if (_getDirtyCount() == 0)
            {
                _hasPendingChange = false;
                return;
            }

            _saveAction();
            _hasPendingChange = false;
        }
    }
}
