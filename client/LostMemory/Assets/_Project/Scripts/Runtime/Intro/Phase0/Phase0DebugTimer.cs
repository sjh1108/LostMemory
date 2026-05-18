using UnityEngine;

namespace LostMemory.Intro.Phase0
{
    /// <summary>
    /// 인트로 재생 중 경과 시간을 화면 좌상단에 표시.
    /// 배경 Fade In Delay 타이밍을 찾을 때 사용하고, 완성 후 오브젝트 비활성화하면 됨.
    /// </summary>
    [AddComponentMenu("Lost Memory/Intro/Phase0 Debug Timer (개발용)")]
    public sealed class Phase0DebugTimer : MonoBehaviour
    {
        [SerializeField] private Phase0IntroSequenceController controller;

        private float _elapsed;

        private GUIStyle _style;

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    fontSize  = 28,
                    alignment = TextAnchor.MiddleCenter
                };
                _style.normal.textColor = Color.yellow;
            }

            GUI.Box(new Rect(10, 10, 160, 45), $"⏱ {_elapsed:F1}초", _style);
        }
    }
}
