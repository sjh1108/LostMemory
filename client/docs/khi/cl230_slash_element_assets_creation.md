# CL230 — Material + WeaponElementCatalog SO 자동 생성

## Context

Element 셰이더 시스템 인프라(SlashElement.shader, WeaponElementCatalog.cs, WeaponData/KhiSlashAnimator 수정)는 이미 완료. 그러나 적용에 필요한 두 asset (Material, ScriptableObject 인스턴스) 이 미생성 상태라 단검(F9) 슬래시가 셰이더 적용 안 되고 기존 slashTint(시안) 만 보임. 사용자가 Unity Editor 수동 작업 대신 yaml 직접 작성 요청. 두 asset 을 코드에서 yaml 로 만들고, .meta 파일에 고정 GUID 부여해 다른 환경에서도 일관되게 import.

## 생성할 파일

### 1. SlashElement.shader.meta (필요시 — 이미 있으면 그대로)

Unity 가 자동 생성했을 수 있음. 없으면 고정 GUID 로 신규.

### 2. WeaponElementCatalog.cs.meta (필요시)

새로 만든 .cs 파일이라 import 안 됐을 수 있음. 고정 GUID 부여로 SO 의 m_Script 참조 안정화.

### 3. SlashElement.mat (Material)

경로: `Assets/_Project/Art/Shaders/SlashElement.mat`
참조: SlashElement.shader (GUID)
기본 property 값:
- _InnerColor: white
- _OuterColor: orange (런타임에 PropertyBlock override)
- _GradientThreshold: 0.5
- _GradientSoftness: 0.3
- _GlowIntensity: 0
- _ScrollSpeed: (0, 0)

### 4. WeaponElementCatalog.asset (ScriptableObject)

경로: `Assets/_Project/ScriptableObjects/Combat/WeaponElementCatalog.asset`
참조: WeaponElementCatalog.cs (class GUID)
Entries:
- Fire (element: 1)
  - innerColor: (1, 0.9, 0.5, 1) — 노란 hot core
  - outerColor: (1, 0.25, 0, 1) — 빨간 화염
  - gradientThreshold: 0.5
  - gradientSoftness: 0.3
  - glowIntensity: 1.5
  - scrollSpeed: (0.5, 0)

## 사용자 액션 (5초)

자동 생성 후 사용자가 Unity Editor 에서:
1. Hierarchy → KhiSlashAnimator 컴포넌트 찾기
2. Inspector → Element System 헤더:
   - Element Catalog 슬롯 ← WeaponElementCatalog.asset 드래그
   - Element Material 슬롯 ← SlashElement.mat 드래그

(이 두 슬롯 연결은 prefab/scene yaml 수정 자동화가 가능하지만 어느 prefab/scene 인지 모호하므로 사용자 액션으로 둠.)

## 검증

1. Unity 컴파일 + import
2. KhiSlashAnimator Inspector 두 슬롯 드래그
3. Play → F9 단검 → 좌클릭 → 노랑/빨강 화염 슬래시
4. F10 검 복귀 → 기존 흰 슬래시
5. F8 닌자 (element None) → 기존 슬래시 그대로

## 추가 속성 (Ice/Lightning 등)

WeaponElementCatalog Inspector → Entries `+` → 새 항목 등록 → 새 WeaponData SO 의 `_currentElement` 에 enum 값 설정.
