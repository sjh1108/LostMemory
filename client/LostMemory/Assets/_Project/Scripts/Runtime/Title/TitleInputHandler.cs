using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.UI
{
    public class TitleInputHandler : MonoBehaviour
    {
        [SerializeField] private string _nextSceneName = "Lobby";
        [SerializeField, Tooltip("인트로 미시청 유저에게 보여줄 인트로 씬 이름. 비어있으면 nextScene 으로 바로 이동.")]
        private string _introSceneName = "Phase0_Intro";
        [SerializeField, Tooltip("개발용: 켜면 PlayerPrefs 무시하고 항상 인트로부터 재생.")]
        private bool _forcePlayIntro = false;

        private const string IntroSeenPrefKey = "LostMemory.IntroSeen";

        void Update()
        {
            if (Input.anyKeyDown)
            {
                LoadNext();
            }
        }

        private void LoadNext()
        {
            bool introSeen = !_forcePlayIntro && PlayerPrefs.GetInt(IntroSeenPrefKey, 0) == 1;
            string target = (!introSeen && !string.IsNullOrEmpty(_introSceneName))
                ? _introSceneName
                : _nextSceneName;
            Debug.Log($"[TitleInputHandler] introSeen={introSeen} (force={_forcePlayIntro}, pref={PlayerPrefs.GetInt(IntroSeenPrefKey, 0)}) → LoadScene({target})");
            SceneManager.LoadScene(target);
        }
    }
}
