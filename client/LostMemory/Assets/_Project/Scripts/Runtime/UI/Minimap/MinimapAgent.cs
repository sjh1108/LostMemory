using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.UI.Minimap
{
    /// <summary>
    /// 미니맵에 표시될 객체에 부착하는 컴포넌트. Player/Enemy/Boss prefab 등.
    /// OnEnable/OnDisable로 등록되어 풀링(MMSimpleObjectPooler) 재사용에도 안전.
    /// MinimapMarkerOverlay 가 매 프레임 활성 agent 들을 순회한다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Minimap/Minimap Agent")]
    public class MinimapAgent : MonoBehaviour
    {
        public enum AgentKind
        {
            PlayerLocal,
            PlayerRemote,
            Enemy,
            Boss,
            Pickup,
            Portal
        }

        [Header("Identity")]
        [SerializeField] private AgentKind kind = AgentKind.Enemy;

        [Header("Visual")]
        [SerializeField] private Sprite icon;
        [SerializeField] private Color tint = Color.white;
        [SerializeField, Min(0.1f)] private float iconScale = 1f;
        [SerializeField, Tooltip("true: transform.eulerAngles.z 로 마커 회전 (이동 방향 화살표용). false: 정적 아이콘.")]
        private bool rotateWithTransform = false;

        [Header("Sorting")]
        [SerializeField, Tooltip("높을수록 위에 그려짐. PlayerLocal=100, Boss=80, Enemy=10 권장.")]
        private int priority;

        private static readonly List<MinimapAgent> ActiveAgents = new List<MinimapAgent>();

        /// <summary>현재 활성화된 모든 agent. 마커 오버레이가 매 프레임 순회.</summary>
        public static IReadOnlyList<MinimapAgent> All => ActiveAgents;

        public AgentKind Kind => kind;
        public Sprite Icon => icon;
        public Color Tint => tint;
        public float IconScale => iconScale;
        public int Priority => priority;
        public Vector3 WorldPosition => transform.position;

        public void SetKind(AgentKind newKind) => kind = newKind;
        public void SetTint(Color newTint) => tint = newTint;
        public void SetIcon(Sprite newIcon) => icon = newIcon;

        protected virtual void OnEnable()
        {
            if (!ActiveAgents.Contains(this))
            {
                ActiveAgents.Add(this);
            }
        }

        protected virtual void OnDisable()
        {
            ActiveAgents.Remove(this);
        }

        /// <summary>
        /// 마커 회전값. 2D 프로젝트(XY 평면) 기준 transform.eulerAngles.z 그대로 사용.
        /// 캐릭터가 이동 방향대로 회전하지 않는 sprite 라면 서브클래스에서 override.
        /// </summary>
        public virtual Quaternion GetMarkerRotation()
        {
            if (!rotateWithTransform)
            {
                return Quaternion.identity;
            }

            return Quaternion.Euler(0f, 0f, transform.eulerAngles.z);
        }
    }
}
