using TMPro;
using UnityEngine;

namespace LostMemory.UI.Status
{
    /// <summary>
    /// 상태창 패널의 한 행. 이름 + 값 + 설명 텍스트 슬롯을 채운다.
    /// Stat 행과 OnHit 효과 행 모두 같은 prefab 으로 재사용 — OnHit 은 value 가 빈 문자열.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/UI/Player Status Row View")]
    public sealed class PlayerStatusRowView : MonoBehaviour
    {
        [Tooltip("스탯/효과 이름 표시. 예: \"공격력\", \"적중 시 화상\"")]
        [SerializeField] private TMP_Text _nameText;

        [Tooltip("합산값 표시. 예: \"+27%\", \"+12\". OnHit 행은 빈 문자열.")]
        [SerializeField] private TMP_Text _valueText;

        [Tooltip("한 줄 설명. 예: \"평타 데미지 배율\".")]
        [SerializeField] private TMP_Text _descriptionText;

        public void Bind(string name, string value, string description)
        {
            if (_nameText != null) _nameText.text = name;
            if (_valueText != null) _valueText.text = value;
            if (_descriptionText != null) _descriptionText.text = description;
        }
    }
}
