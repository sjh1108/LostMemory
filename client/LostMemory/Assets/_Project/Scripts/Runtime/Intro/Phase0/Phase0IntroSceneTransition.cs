using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.Intro.Phase0
{
    public class Phase0IntroSceneTransition : MonoBehaviour
    {
        [SerializeField] private Phase0IntroSequenceController controller;
        [SerializeField] private string nextSceneName = "Phase1_Tutorial";

        private void Start()
        {
            controller.IntroCompleted += OnIntroCompleted;
        }

        private void OnDestroy()
        {
            controller.IntroCompleted -= OnIntroCompleted;
        }

        private void OnIntroCompleted()
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
