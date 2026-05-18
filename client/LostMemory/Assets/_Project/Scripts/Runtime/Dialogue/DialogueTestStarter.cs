using UnityEngine;

namespace LostMemory.Dialogue
{
    /// <summary>
    /// 테스트 씬에서 Play 시 자동으로 한 그룹의 대화를 발화시키는 헬퍼.
    /// 빈 GameObject 에 부착하고 groupId 만 지정하면 됨.
    /// 운영 빌드에 들어갈 일은 없는 디버그용 컴포넌트.
    /// </summary>
    [AddComponentMenu("Lost Memory/Dialogue/Dialogue Test Starter")]
    public sealed class DialogueTestStarter : MonoBehaviour
    {
        [Tooltip("dialogues.csv 의 prefix. 예: boss_intro, shopkeeper_greeting, tutorial_intro")]
        [SerializeField] private string groupId = "boss_intro";

        [Tooltip("Play 후 몇 초 뒤 발화할지")]
        [SerializeField, Min(0f)] private float delay = 0.5f;

        [Tooltip("자동 발화 비활성. 키 입력 대기로만 동작.")]
        [SerializeField] private bool autoStart = true;

        [Tooltip("이 키 누르면 강제로 다시 발화 (반복 검증용)")]
        [SerializeField] private KeyCode replayKey = KeyCode.R;

        private void Start()
        {
            if (autoStart) Invoke(nameof(TriggerDialogue), delay);
        }

        private void Update()
        {
            if (Input.GetKeyDown(replayKey)) TriggerDialogue();
        }

        [ContextMenu("Trigger Dialogue")]
        private void TriggerDialogue()
        {
            DialogueController controller = DialogueController.Instance;
            if (controller == null)
            {
                Debug.LogError("[DialogueTestStarter] DialogueController 가 씬에 없음. DialoguePanel.prefab 을 Canvas 자식으로 배치했는지 확인.", this);
                return;
            }
            if (string.IsNullOrEmpty(groupId))
            {
                Debug.LogWarning("[DialogueTestStarter] groupId 비어있음.", this);
                return;
            }
            controller.Show(groupId);
        }
    }
}
