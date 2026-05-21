using System;
using LostMemory.Networking.Session;
using LostMemory.SceneFlow;
using TMPro;
using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// Q-3: 4인 포탈 게이트 "X/Y 명 대기 중" UI 라벨.
    /// 포탈 GameObject 자식 Canvas/Text 에 부착. HostOnlyLoadSceneTrigger 또는 SceneLoadPortalController 의 이벤트 구독.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Portal Readiness Label")]
    public sealed class PortalReadinessLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private HostOnlyLoadSceneTrigger portalTrigger;
        [SerializeField] private SceneLoadPortalController portalController;
        [SerializeField] private string format = "{0}/{1}";
        [SerializeField] private bool hideWhenSingle = true;
        [SerializeField] private bool hideWhenReady = true;

        private Action<int, int> _handler;

        private void Awake()
        {
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            if (portalTrigger == null) portalTrigger = GetComponentInParent<HostOnlyLoadSceneTrigger>();
            if (portalController == null) portalController = GetComponentInParent<SceneLoadPortalController>();
        }

        private void OnEnable()
        {
            _handler = HandleReadinessChanged;
            if (portalTrigger != null)
            {
                portalTrigger.PortalReadinessChanged += _handler;
                HandleReadinessChanged(portalTrigger.PlayersInsideCount, portalTrigger.RequiredPlayerCount);
            }
            else if (portalController != null)
            {
                portalController.PortalReadinessChanged += _handler;
                HandleReadinessChanged(portalController.ValidPlayersInsideCount, portalController.RequiredPlayerCount);
            }
            else
            {
                Debug.LogWarning("[PortalReadinessLabel] portalTrigger / portalController 둘 다 null.", this);
                if (label != null) label.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (portalTrigger != null) portalTrigger.PortalReadinessChanged -= _handler;
            if (portalController != null) portalController.PortalReadinessChanged -= _handler;
            _handler = null;
        }

        private void HandleReadinessChanged(int current, int required)
        {
            if (label == null) return;
            bool hideSolo = hideWhenSingle && required <= 1;
            bool hideReady = hideWhenReady && current >= required;
            if (hideSolo || hideReady) { label.gameObject.SetActive(false); return; }
            label.gameObject.SetActive(true);
            label.text = string.Format(format, current, required);
        }
    }
}
