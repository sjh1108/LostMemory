using UnityEngine;

namespace LostMemory.Friend
{
    /// <summary>
    /// 소꿉친구 NPC 캐릭터 설정을 담는 ScriptableObject.
    ///
    /// [초심자 설명]
    ///   ScriptableObject 는 Unity 의 "데이터 전용 파일"입니다.
    ///   게임 로직 코드가 아닌 "설정값"을 저장할 때 사용합니다.
    ///   여기에 소꿉친구의 이름, 성격, 말투를 정의합니다.
    ///
    ///   Inspector 에서 systemPrompt 를 직접 편집하면 코드 수정 없이
    ///   캐릭터의 말투나 성격을 바꿀 수 있습니다.
    ///
    /// 생성 방법:
    ///   Project 창 우클릭 → Create → Lost Memory → Friend → Character Context
    ///   생성된 asset 을 FriendChatPanelView 의 characterContext 슬롯에 드래그.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FriendCharacterContext",
        menuName = "Lost Memory/Friend/Character Context")]
    public sealed class FriendCharacterContext : ScriptableObject
    {
        [Header("캐릭터 기본 정보")]
        [SerializeField] private string npcName = "루아나";

        [Header("시스템 프롬프트 (LLM 에게 전달되는 캐릭터 지침)")]
        [TextArea(15, 40)]
        [SerializeField] private string systemPrompt =
@"당신은 ""루아나""입니다.
플레이어의 오랜 소꿉친구이자 이 기억의 미로를 함께 탐험하는 동반자입니다.

플레이어는 기억을 잃어가는 소녀이며, 이 던전을 탐험하며 자신의 기억을 되찾으려 합니다.
루아나는 플레이어가 기억을 잃기 전부터의 친구이며, 플레이어를 온전히 기억합니다.

[성격]
- 따뜻하고 씩씩한 소녀. 혼자 힘든 척을 잘 못함.
- 플레이어의 안위를 항상 걱정하지만, 들키기 싫어서 가끔 짓궂게 구는 척함.
- 기억에 관한 이야기를 꺼낼 때는 조심스럽게 접근함.
- 무섭거나 걱정될 때도 긍정적인 말로 플레이어를 밀어붙이려 함.

[말투]
- 반말 사용 (""~야"", ""~어"", ""~지?"", ""~해"")
- 한 번에 2~4문장 이내의 짧고 자연스러운 일상 대화 톤
- 이모티콘 사용 금지
- 예시: ""야 또 다쳤어? 조심 좀 하지."" / ""이번 층은 좀 힘들었지? 수고했어.""
- 예시: ""그 기억... 아직 못 찾았어? 괜찮아, 같이 찾으면 되지.""

[게임 세계관]
- 이 공간은 기억의 미로(던전)임
- 기억 조각, 유물, 층(floor) 개념을 자연스럽게 사용할 수 있음
- 던전이 위험하다는 것을 알지만 플레이어를 응원함

[절대 규칙]
1. 항상 루아나로서만 대답한다.
2. 자신이 AI, 언어모델, 프로그램이라고 절대 밝히지 않는다.
3. 모든 답변은 반드시 한국어로 한다.
4. 롤플레이를 깨는 발언을 하지 않는다.
5. 폭력적이거나 부적절한 내용은 자연스럽게 화제를 돌린다.";

        public string NpcName => npcName;

        /// <summary>기본 시스템 프롬프트 반환.</summary>
        public string BuildSystemPrompt() => systemPrompt;

        /// <summary>
        /// 현재 게임 상황을 포함한 시스템 프롬프트 생성.
        /// FriendChatPanelView.InjectGameContext() 를 통해 대화 시작 시 주입.
        ///
        /// 예: Open() 전에 호출하여 LLM 이 현재 층과 체력을 참고하게 할 수 있음.
        /// </summary>
        public string BuildContextMessage(int currentFloor, float hpPercent)
        {
            return $"[현재 게임 상황]\n" +
                   $"- 플레이어가 현재 {currentFloor}층에 있음\n" +
                   $"- 플레이어 체력: {Mathf.RoundToInt(hpPercent * 100)}%\n" +
                   $"이 정보를 대화에 자연스럽게 반영할 수 있음 (굳이 매 메시지마다 언급하지 않아도 됨)";
        }
    }
}
