using TMPro;
using UnityEngine;

namespace LostMemory.Friend
{
    /// <summary>
    /// 게임 채팅 로그의 "한 줄"을 담당하는 뷰 컴포넌트.
    ///
    /// 롤(LoL) / MMO 스타일 채팅 로그:
    ///   <color=#xxx>발신자이름</color>: 메시지
    /// 형식의 한 줄 텍스트를 렌더링한다.
    ///
    /// [초심자 설명]
    ///   - SetText(): 한 번에 전체 텍스트 세팅
    ///   - AppendText(): 스트리밍 응답 시 글자를 뒤에 덧붙임 (타이핑 효과)
    ///
    ///   발신자 이름의 색은 호출하는 쪽(FriendChatPanelView)에서
    ///   리치 텍스트 태그(<color=#xxxxxx>)로 직접 조립해 넘긴다.
    ///
    /// Inspector 연결:
    ///   messageText — 줄에 표시할 TMP_Text (Rich Text 옵션 ON 필수)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Friend/Chat Message View")]
    public sealed class ChatMessageView : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;

        public void SetText(string text)
        {
            if (messageText != null) messageText.text = text;
        }

        /// <summary>스트리밍 응답: 기존 텍스트 뒤에 청크를 실시간으로 덧붙임.</summary>
        public void AppendText(string chunk)
        {
            if (messageText != null) messageText.text += chunk;
        }
    }
}
