# 세트효과 패널 — 노소연 UI 톤 맞추기 플랜

## Context

직전에 만든 [SetEffectPanel.prefab](../../LostMemory/Assets/_Project/Prefabs/UI/SetEffectPanel.prefab) / [SetEffectRow.prefab](../../LostMemory/Assets/_Project/Prefabs/UI/SetEffectRow.prefab) 의 색/폰트 사이즈 가 노소연 작 [InventoryPanel.prefab](../../LostMemory/Assets/_Project/Prefabs/UI/InventoryPanel.prefab) / [ShopPanel.prefab](../../LostMemory/Assets/_Project/Prefabs/UI/ShopPanel.prefab) 톤과 맞지 않아 인벤토리 옆에 같이 떴을 때 "임시 패널" 느낌이 남는다. 두 패널이 항상 같이 표시되므로 톤 통일이 중요. 폰트는 이미 같은 GUID 를 쓰고 있어 갭은 **배경 명도, Title/텍스트 사이즈, 골드 악센트 hex** 정도이며 prefab YAML 의 `m_Color` / `m_fontSize` 만 바꾸면 끝. Unity 에디터 작업 불필요.

## 노소연 UI 디자인 토큰 (확정값)

InventoryPanel.prefab + ShopPanel.prefab 에서 추출:

| 항목 | 값 |
| --- | --- |
| Panel 배경 | `{r: 0.17, g: 0.17, b: 0.17, a: 1}`, sprite 없음, Image Type=Simple |
| 슬롯/Row 배경 | `{r: 0.25, g: 0.25, b: 0.25, a: 1}`, sprite 없음 |
| 폰트 GUID | `24e7867240b94b4458d8a28b7e2e0a93` (단일) |
| 제목 사이즈 | 18 |
| 본문(보조) 사이즈 | 14 |
| 본문 색 (기본) | `{r: 1, g: 1, b: 1, a: 1}` |
| 본문 색 (보조 회색) | `{r: 0.7, g: 0.7, b: 0.7, a: 1}` |
| 골드 악센트 | `{r: 0.96, g: 0.77, b: 0.26, a: 1}` |
| LayoutGroup spacing | 6 (Inventory 슬롯), 2~10 (Shop) |

## 현재 vs 목표 갭

### SetEffectPanel.prefab

| 토큰 | 현재 | 목표 |
| --- | --- | --- |
| 루트 Image `m_Color` | `(0.08, 0.08, 0.08, 0.92)` | `(0.17, 0.17, 0.17, 1)` |
| ScrollArea Image `m_Color` | `(0, 0, 0, 0.3)` | `(0, 0, 0, 0)` 투명 (노소연 패턴엔 별도 ScrollArea 띠 없음) |
| Title `m_fontSize` & `m_fontSizeBase` | 22 | 18 |

### SetEffectRow.prefab

| 토큰 | 현재 | 목표 |
| --- | --- | --- |
| 루트 Image `m_Color` | `(0.15, 0.15, 0.15, 1)` | `(0.25, 0.25, 0.25, 1)` |
| NameText `m_fontSize`/`m_fontSizeBase` | 20 | 16 |
| CountText `m_fontSize`/`m_fontSizeBase` | 18 | 16 |
| CountText `m_fontColor` | `(0.9, 0.9, 0.5, 1)` | `(0.96, 0.77, 0.26, 1)` 골드 악센트 통일 |
| EffectText `m_fontSize`/`m_fontSizeBase` | 16 | 14 |
| EffectText `m_fontColor` | `(0.85, 0.85, 0.85, 1)` | `(0.7, 0.7, 0.7, 1)` 보조 회색 통일 |

폰트 GUID 와 LayoutGroup padding 은 변경 없음 (이미 일치하거나 기능적으로 충분).

## 변경 파일

- [LostMemory/Assets/_Project/Prefabs/UI/SetEffectPanel.prefab](../../LostMemory/Assets/_Project/Prefabs/UI/SetEffectPanel.prefab)
  - 라인 64 부근: 루트 Image `m_Color` → `(0.17, 0.17, 0.17, 1)`
  - 라인 188 부근: Title `m_fontSize`, `m_fontSizeBase` → 18 (둘 다)
  - ScrollArea Image `m_Color` → `(0, 0, 0, 0)`
- [LostMemory/Assets/_Project/Prefabs/UI/SetEffectRow.prefab](../../LostMemory/Assets/_Project/Prefabs/UI/SetEffectRow.prefab)
  - 루트 Image `m_Color` → `(0.25, 0.25, 0.25, 1)`
  - NameText TMP `m_fontSize`/`m_fontSizeBase` → 16
  - CountText TMP `m_fontSize`/`m_fontSizeBase` → 16, `m_fontColor` → `(0.96, 0.77, 0.26, 1)`
  - EffectText TMP `m_fontSize`/`m_fontSizeBase` → 14, `m_fontColor` → `(0.7, 0.7, 0.7, 1)`

총 prefab 2개, Edit 약 8회.

## 변경하지 않는 것

- 레이아웃 구조 (HorizontalLayoutGroup / VerticalLayoutGroup / ScrollRect / Mask) — 기능 그대로 둠
- LayoutGroup padding 값 (현 값으로 가독성 충분)
- Row 높이 44px → 폰트가 작아져 살짝 여유 생기지만 압축감 없이 자연스러움
- SetEffectRowView 의 `_inactiveAlpha = 0.4` 디밍 로직 — 노소연 패턴에 비활성/활성 구분 사례 없으나 기능적으로 의미 있음

## 검증

1. Unity 에디터 reimport 후 Project 창에서 두 prefab 더블클릭 → 색/사이즈 갱신 시각 확인
2. `Dungeon_1F_Shop_testkhi` 씬 Play → `I` 키 → 인벤토리 패널 옆에 SetEffectPanel 띄움
3. 두 패널 배경 명도가 같아 보이는지 (눈으로 비교 — 톤 끊김 없으면 OK)
4. row 텍스트가 InventoryPanel 슬롯 라벨과 비슷한 크기·색감인지
5. 활성/비활성 row 의 디밍은 그대로 동작하는지 (`_inactiveAlpha=0.4` — Bind 시점에 알파 곱)

## 범위 외

- ScrollRect Scrollbar 추가 (현재는 마우스 휠만)
- 9-slice sprite 적용 (노소연도 안 쓰므로 일치)
- 활성 row 의 골드 테두리/액센트 — 추가하면 더 멋지지만 노소연 패턴엔 없음 → 통일감 유지 위해 보류
