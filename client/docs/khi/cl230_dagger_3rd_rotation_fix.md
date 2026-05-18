# CL230 — 단검 3타 회전 -60도 제거

## Context

`TestKhi_MinimalCharacter2D.prefab` 의 SlashSlot_3 Transform 에 Z 회전 -60도가 박혀있음 (검 3타 큰 회전 마무리 디자인용). 옵션 B (SO override) 적용 후 단검 SO 3타의 `overrideSlashSlotTransform = 0` 이라 prefab transform 그대로 사용 → 단검 3타에도 -60도 회전이 적용되어 어색.

**해결**: 단검 SO 3타에서 override 켜서 회전 0도로 명시. prefab 안 건드림 → 검 동작 100% 보존.

## 변경 파일 (1개)

### Sword_Dagger.asset — 3타 step만 수정

`Assets/_Project/ScriptableObjects/Weapon/Sword_Dagger.asset`

3타 step (label "3타: 마무리 기습", comboStep: 3) 의 SlashSlot Transform Override 필드:

| 필드 | 현재 | 변경 |
|---|---|---|
| `overrideSlashSlotTransform` | 0 | **1** (ON) |
| `slashSlotLocalPosition` | (0, 0, 0) | **(0.75, 0, 0)** (prefab 값 동일) |
| `slashSlotRotationZ` | 0 | **0** (변경 없음. 명시) |
| `slashSlotLocalScale` | (1, 1, 1) | **(1.5, 1.5, 1)** (prefab 값 동일) |

핵심:
- prefab 의 위치 (0.75, 0, 0) 와 스케일 (1.5, 1.5, 1) 은 유지 — 단검에도 적합한 크기/위치
- **회전만 -60도 → 0도로 reset** (단검은 직선적, 큰 호 회전 안 어울림)

## 코드 변경 없음

`KhiSlashAnimator.cs` 의 override 처리 로직은 이전 옵션 B 작업에서 이미 완료. 본 작업은 SO 값만 변경.

## 검증

1. Play 진입
2. **검 (F10)** → 좌클릭 콤보 → **3타 -60도 큰 회전 그대로** (prefab transform 유지)
3. **F9 단검** → 좌클릭 콤보 → **3타 0도 직선 모션** (SO override 적용)
4. 1/2타는 둘 다 prefab 그대로 (overrideSlashSlotTransform=0)

## 후속 확인 사항 (선택)

SlashSlot_1, SlashSlot_2 prefab transform 에도 의도 안 한 회전/위치 있는지 확인. 있으면 단검 1/2타도 같은 방식으로 override 설정.

## 비고

본 패턴 = 무기별 자세 차이를 SO override 로 처리. prefab 은 검 기준 default 자세 유지. 향후 새 무기 추가 시 SO 만 override 설정.
