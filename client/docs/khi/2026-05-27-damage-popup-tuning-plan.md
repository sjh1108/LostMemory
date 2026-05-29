# 데미지 Popup 튜닝 계획

## Context

[2026-05-27-damage-popup-ui-plan.md](2026-05-27-damage-popup-ui-plan.md)으로 popup 시스템을 구축한 뒤 실제 화면에서 보니 5가지 튜닝 필요:

1. 텍스트가 너무 큼
2. 상승 거리가 너무 김
3. spawn 위치가 몬스터 몸 안에서 시작 — 체력바 위(머리 위)에서 시작해야 함
4. 상승 속도가 빠름
5. 폰트가 Lato (영문 전용) → **Galmuri9** 로 교체. Bold variant 사용

목표: 코드 변경 없이 **prefab + SO + Spawner GameObject 인스펙터 값**만 조정해서 해결.

## 핵심 발견

- **Galmuri9 Bold SDF asset이 이미 프로젝트에 존재**: [`Galmuri9 1.asset`](LostMemory/Assets/_Project/Art/Fonts/Galmuri9%201.asset) (GUID `1f760a28ffcf16a4e97d6841a9343642`) — 그대로 할당하면 됨. 별도 SDF 생성 작업 불필요
- **prefab root에 Y=6.93 offset이 박혀 있음** ([DamageFloatingText.prefab:152](LostMemory/Assets/_Project/Prefabs/UI/DamageFloatingText.prefab#L152)) — TDE 데모용 위치. spawn 시 `spawnPosition`으로 덮어써지지만 디자인 노이즈라 0으로 정리 권장
- 이동 거리는 **MMFloatingTextSpawner의 `RemapYZero/RemapYOne`** 으로 결정 (현재 기본 0~5)
- 이동 속도는 **lifetime이 짧을수록 빠름** — 우리 SO에서 `forceLifetime=true`로 덮어쓰므로 [DamagePopupStyle.asset](LostMemory/Assets/_Project/ScriptableObjects/UI/DamagePopupStyle.asset)의 `lifetime` 필드가 진짜 제어 지점

## 변경 사항

### 1. [DamageFloatingText.prefab](LostMemory/Assets/_Project/Prefabs/UI/DamageFloatingText.prefab) — Prefab Mode에서 편집

`Text (TMP)` 자식 GameObject의 **TextMeshPro** 컴포넌트:

| 필드 | 현재 | 변경 | 비고 |
|------|------|------|------|
| **Font Asset** | Lato SDF | **Galmuri9 1** (Bold) | Project 창에서 드래그. `Assets/_Project/Art/Fonts/Galmuri9 1.asset` |
| **Material Preset** | Lato SDF Material | Galmuri9 Bold의 main material | Font Asset 바꾸면 자동으로 따라감 |
| **Font Size** | 10 | **4** | 월드 단위 — 캐릭터 1unit 기준 적절한 비례 |
| **Font Style** | Bold (toggle) | **Normal** (toggle 끔) | Bold variant asset을 직접 쓰므로 faux bold 불필요. 안 끄면 이중 굵게 처리되어 뭉개짐 |

Prefab **루트 GameObject** (`DamageFloatingText`):
- Transform Position Y: `6.93` → **`0`** (데모 잔재 제거)

### 2. [DamagePopupStyle.asset](LostMemory/Assets/_Project/ScriptableObjects/UI/DamagePopupStyle.asset) — Inspector에서

| 필드 | 현재 | 변경 | 효과 |
|------|------|------|------|
| **Spawn Offset Y** | 1.0 | **1.3** | 적 sprite(보통 ~1unit) 머리 위, 체력바 위쪽 정도 |
| **Lifetime** | 1.0 | **1.5** | 같은 거리를 더 천천히 — 상승 속도 ↓ |
| **Normal Intensity** | 1.0 | **0.8** | 일반 데미지 약간 작게 |
| **Critical Intensity** | 1.7 | **1.3** | 크리도 너무 안 크게 |

### 3. Spawner GameObject (Test_Title_Copy 씬의 `MMFloatingTextSpawner`)

**Remap Y Zero / One**: 기본 0 ~ 5 → **0 ~ 1.5**

이게 popup이 spawn 위치에서 위로 얼마나 멀리 이동하는지 결정. 5는 너무 길어서 "너무 많이 올라간다" 원인. 1.5면 적당히 떠올랐다가 사라지는 자연스러운 거리.

> 인스펙터에서 못 찾으면: Spawner GameObject 선택 → MMFloatingTextSpawner 컴포넌트 → "Movement" 또는 "Animate Y" 섹션 펼치기

## 튜닝 가이드 (이후 미세 조정)

위 값은 일반적 출발점. 실제 보고 더 조정:

- **여전히 크면**: Prefab의 Font Size 4 → 3 또는 prefab root Scale 1 → 0.8
- **여전히 빠르면**: SO Lifetime 1.5 → 2.0
- **올라가는 게 부족하면**: Spawner RemapYOne 1.5 → 2.5
- **체력바와 겹치면**: SO Spawn Offset Y 1.3 → 1.6
- **글자가 흐릿하면**: Material Preset에서 outline 추가 또는 별도 Galmuri9-Outline material 생성

## 검증

Play 모드 진입 → 던전 적 평타:

1. 텍스트가 체력바 살짝 위에서 시작하는지
2. 위로 1.5unit 정도 살짝 떠올랐다 사라지는지 (1초가 아니라 1.5초)
3. 글자가 한글 폰트로 Bold하게 보이는지 (Galmuri9 픽셀 폰트 특유의 모양)
4. CRITICAL 발동 시 살짝 더 큰 노란 글자
5. Scene 뷰에서도 같이 확인 — Game 뷰에서 안 보이면 sorting layer 문제 (이전 가이드 참고)

## 의도적으로 안 하는 것

- **코드 변경 없음** — 모든 튜닝은 prefab/SO/Spawner 인스펙터 값으로만
- **Outline / Glow 효과** — 우선 기본 가독성 확보 후 별개 작업
- **카테고리별(평타/화살/보조효과) 폰트 차별화** — 색/크기만으로 충분, 폰트는 통일
