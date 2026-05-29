using UnityEngine;

namespace LostMemory.UI
{
    /// <summary>
    /// 데미지 popup 표시 스타일. 카테고리별 색/크기/라벨을 인스펙터에서 튜닝.
    /// 나중에 "CRITICAL!" 텍스트를 이미지/다국어로 바꾸려면 prefix만 갈아끼우면 됨.
    /// </summary>
    [CreateAssetMenu(menuName = "Lost Memory/UI/Damage Popup Style", fileName = "DamagePopupStyle")]
    public class DamagePopupStyle : ScriptableObject
    {
        [Header("MMFloatingText Channel (Int mode)")]
        [Tooltip("씬에 배치된 MMFloatingTextSpawner 와 동일 채널이어야 trigger 가 수신됨.")]
        public int channel = 0;

        [Header("Normal")]
        public Color normalColor = Color.white;
        [Min(0.1f)] public float normalIntensity = 1f;

        [Header("Critical")]
        public Color criticalColor = new Color(1f, 0.85f, 0.2f, 1f);
        [Min(0.1f)] public float criticalIntensity = 1.7f;
        [Tooltip("크리티컬 수치 앞에 붙는 라벨. 줄바꿈(\\n) 사용 시 2줄 표시. 이미지 prefab으로 바꾸려면 빈 문자열로.")]
        public string criticalPrefix = "CRITICAL!\n";

        [Header("Sub Effect (체인/풍속/화상/도트)")]
        public Color subEffectColor = new Color(0.65f, 0.85f, 1f, 1f);
        [Min(0.1f)] public float subEffectIntensity = 0.7f;

        [Header("Spawn")]
        [Tooltip("타겟 transform 위치 + 이 오프셋에 popup spawn.")]
        public Vector3 spawnOffset = new Vector3(0f, 1.0f, 0f);
        [Tooltip("popup 이동 방향. (0,1,0) 위쪽 권장.")]
        public Vector3 direction = Vector3.up;
        [Min(0.1f)] public float lifetime = 1.0f;

        [Header("Format")]
        [Tooltip("ToString 포맷. '0' = 정수, '0.0' = 소수 1자리.")]
        public string numberFormat = "0";

        public bool useUnscaledTime = false;

        [Header("Outline (4방향 픽셀 outline 트릭)")]
        [Tooltip("DamagePopupOutlineMirror 가 LateUpdate 에서 이 값을 읽어 4개 outline TMP 에 적용. Play 중 변경 시 즉시 반영.")]
        public bool outlineEnabled = true;
        public Color outlineColor = Color.black;
        [Tooltip("메인 글자 기준 4방향 (상/하/좌/우) offset 거리. 폰트 size 4 기준 0.03~0.08 권장. Play 중 슬라이더로 튜닝.")]
        [Range(0f, 0.3f)] public float outlineOffset = 0.05f;

        [Header("Layout (메인 + outline 4개에 동일 적용)")]
        [Tooltip("폰트 크기. 4 정도가 1unit 캐릭터 기준 적정.")]
        [Range(1f, 30f)] public float fontSize = 4f;
        [Tooltip("자간 (글자 사이 간격). 음수면 글자 붙이기, 양수면 벌리기. 픽셀 폰트는 -10~10 정도가 자연스러움.")]
        [Range(-20f, 20f)] public float characterSpacing = 0f;
        [Tooltip("줄 간격 (CRITICAL!\\n 숫자 두 줄 사이 간격).")]
        [Range(-50f, 50f)] public float lineSpacing = 0f;

        [Header("Motion (MMFloatingTextSpawner 에 반영. 다음 popup 부터 적용)")]
        [Tooltip("popup 이 위로 떠오르는 총 거리. 0.5~2.0 권장. (Spawner.RemapYOne 으로 전파)")]
        [Range(0f, 10f)] public float riseDistance = 1.5f;
    }
}
