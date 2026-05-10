using UnityEngine;
using UnityEngine.SceneManagement;

namespace LostMemory.UI
{
    public class TitleInputHandler : MonoBehaviour
    {
        [SerializeField] private string _nextSceneName = "Lobby";

        void Update()
        {
            if (Input.anyKeyDown)
            {
                SceneManager.LoadScene(_nextSceneName);
            }
        }
    }
}