using TMPro;
using UnityEngine;

namespace LostMemory.Networking.Session
{
    /// <summary>
    /// 멀티 로비 (Test_MultiLobby) 의 입장 코드를 TMP_Text 에 표시.
    /// 호스트는 <see cref="RelaySessionHost.CreateAsync"/> 직후, 게스트는 <see cref="RelaySessionClient.JoinByCodeAsync"/>
    /// 직후 <see cref="RelaySession.ActiveJoinCode"/> 에 코드가 set 되므로, 양쪽 모두 본인 static 값을 그대로 표시.
    ///
    /// 부착:
    ///   - Test_MultiLobby 의 LobbyHUD 하위 scene-placed GameObject (NetworkObject 불필요 — sync 없이 각자 static)
    ///   - `label` 슬롯에 TMP_Text 드래그
    ///
    /// 표시 형식: `format` (기본 "입장 코드: {0}") 에 {0}=ActiveJoinCode 치환. 코드가 비어있으면 `fallbackText` 표시.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Networking/Multi Lobby Join Code Display")]
    public sealed class MultiLobbyJoinCodeDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string format = "입장 코드: {0}";
        [SerializeField] private string fallbackText = "입장 코드: -";

        private void OnEnable()
        {
            RelaySession.Joined += HandleJoined;
            RelaySession.Left += HandleLeft;
            Refresh();
        }

        private void OnDisable()
        {
            RelaySession.Joined -= HandleJoined;
            RelaySession.Left -= HandleLeft;
        }

        private void HandleJoined(bool asHost) => Refresh();
        private void HandleLeft() => Refresh();

        private void Refresh()
        {
            if (label == null) return;

            string code = RelaySession.ActiveJoinCode;
            label.text = string.IsNullOrWhiteSpace(code)
                ? fallbackText
                : string.Format(format, code);
        }
    }
}
