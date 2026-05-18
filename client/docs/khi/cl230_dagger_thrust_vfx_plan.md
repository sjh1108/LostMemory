# CL230 — 단검 찌르기 VFX 외부 자산 적용

## Context

현재 단검(Sword_Dagger / Sword_DaggerNinja)의 슬래시 sprite가 검과 동일한 호 베기 형태(KhiSwordSlash1~3). 단검 컨셉(빠른 찌르기)에 시각이 안 맞아 사용자에게 단검 느낌이 약함. 프로젝트 내 자산 탐색 결과 찌르기 전용 sprite 없음 → 외부 무료 자산 다운로드로 가는 게 가장 효과적. 이 plan은 추천 자산 + 다운로드/임포트/적용 워크플로우.

## 추천 자산 사이트 (라이센스 안전 순)

### 1. Kenney.nl (CC0, 무조건 안전, 톤 일치) ⭐ 최우선
- URL: https://kenney.nl/assets
- 이유: 이미 프로젝트에 Kenney 자산 사용 중 (`Assets/_Project/Art/Effects/Kenney/`) → 톤 일치
- 추천 팩:
  - **Particle Pack** (https://kenney.nl/assets/particle-pack) — 작은 임팩트/스파크 sprite. 찌르기 끝점 표현용
  - **Pixel Platformer effects** — 2D pixel VFX
- 라이센스: CC0 (상업 사용 가능, 출처 표기 불필요)

### 2. itch.io (CC0 또는 무료 라이센스)
- URL: https://itch.io/game-assets/free/tag-effects
- 추천 검색 키워드:
  - "pixel art slash effects"
  - "2D thrust VFX"
  - "pierce attack pixel"
  - "dagger attack animation"
- 추천 작가: pixelfrog, ansimuz, brullov 등 (무료 + CC 라이센스)
- 라이센스: 자산마다 다름 → 다운로드 페이지에서 확인 필수

### 3. OpenGameArt.org (CC0/CC-BY)
- URL: https://opengameart.org/art-search-advanced
- 카테고리 필터: 2D > Effects, License: CC0
- 검색: "thrust", "stab", "slash", "pierce"

### 4. Unity Asset Store (무료 섹션)
- URL: https://assetstore.unity.com/2d/textures-materials?free=true
- 추천 검색: "pixel VFX free", "2D combat effects"
- 라이센스: Standard Unity Asset Store EULA

### 권장 형식

단검 찌르기에 어울리는 자산 형식:

1. **PNG sprite sheet 또는 sequence** (가장 호환성 좋음)
   - 4~9 프레임 정도. 짧은 직선 라인 + 끝 임팩트
   - 흰색/단색 → 코드의 slashTint로 색 조정 가능
2. **개별 PNG sprite (한 장 임팩트)**
   - 찌르기 끝점 star/cross 모양
3. **ParticleSystem prefab** (.prefab)
   - 더 화려하지만 코드 변경 필요 (KhiAttackVisualPresenter에 prefab spawn 슬롯 추가)

→ **권장**: PNG sprite sequence (4프레임 정도)가 가장 단순. 기존 slashFrames 배열에 그대로 끼움.

## 다운로드 후 임포트 절차

### 1. 폴더 배치
- 경로: `Assets/_Project/Art/Effects/Daggers/` 신규 폴더 (또는 사용자 선호)
- 다운로드한 PNG 파일들 복사
- 폴더명 컨벤션: `DaggerThrust_01.png ~ DaggerThrust_04.png` 같은 sequence

### 2. Unity Sprite Import Settings
각 .png 선택 → Inspector:
- **Texture Type**: Sprite (2D and UI)
- **Sprite Mode**: Single (한 장씩 따로 import 시) 또는 Multiple (sprite sheet)
- **Pixels Per Unit**: 32 (기존 단검 Dagger_2와 일치) 또는 16 (큰 표시)
- **Filter Mode**: Point (no filter) — pixel art 필수
- **Compression**: None
- **Pivot**: Center (0.5, 0.5) 또는 Left (0.0, 0.5)
  - Left 권장: 슬래시 시작점이 캐릭터 손에 위치 → 찌르기 자연스러움
- Apply

### 3. SO에 sprite 등록 (Sword_Dagger.asset / Sword_DaggerNinja.asset)
- Unity Project 창에서 Sword_Dagger.asset 클릭
- Inspector → Steps[0] (1타) → Slash Frames 배열
- 기존 KhiSwordSlash 프레임을 새 DaggerThrust sprite 로 드래그 교체
- 2타, 3타도 동일하게 교체 (또는 같은 sequence 사용)
- slashTint는 그대로 유지 (시안색)

### 4. (옵션) AttackStepData 콤보별 다른 sprite
- 1타: 짧은 찌르기 (3프레임)
- 2타: 두 번째 찌르기 (3프레임)
- 3타: 회전 마무리 (5프레임)
- 각 step에 다른 sprite sequence

## 코드 변경 필요 여부

**일반 PNG sprite sequence 사용 시**: 코드 변경 0건.
- WeaponData.AttackStepData.slashFrames 가 이미 Sprite[] 배열
- KhiSlashAnimator 가 그 배열을 PlayFrames 로 재생 중
- Sprite asset 교체만으로 작동

**ParticleSystem prefab 사용 시**: 코드 변경 필요.
- KhiAttackVisualPresenter 또는 KhiSlashAnimator 에 prefab 슬롯 추가
- AttackActiveStarted 이벤트에 Instantiate 호출
- 본 plan 범위 외 (후속 작업)

## 추천 — 가장 빠른 시작점

1. **Kenney Particle Pack 다운로드** (CC0, 5분)
   - https://kenney.nl/assets/particle-pack
   - 흰색 임팩트/스파크 sprite 다수
2. 그 중 "muzzle_01~04" 또는 "smoke_01" 같은 짧은 sequence 선택
3. 단검 1타 slashFrames 에 적용 → Play 로 확인
4. 마음에 들면 2/3타도 적용. 아쉬우면 itch.io 에서 다른 자산 찾기

## 사용자 액션 후 후속 작업 (선택)

다운로드 + 적용 후:
- Sprite 크기가 맞지 않으면 → AttackStepData 의 hitboxSize 또는 SlashSlot transform.localScale 조정
- 색 톤이 안 맞으면 → SO 의 slashTint 미세 조정
- ParticleSystem 으로 가고 싶으면 → KhiAttackVisualPresenter 에 prefab 슬롯 추가 (별도 plan)

## 검증

1. Play → F9 단검 모드 진입
2. 좌클릭 콤보 → 단검 sprite + **새 찌르기 VFX** 확인
3. 1타 → 2타 → 3타 차이 (각자 다른 sprite 라면)
4. 화면 깨짐 / sprite 크기 이상 / 색 안 맞음 → SO 값 조정
5. F8 닌자 단검도 같은 자산 사용 → 두 자루 모두 찌르기 VFX 발사

## 참고 — 코드 차원 작업은 없음

이번 단계 = "외부 자산 다운로드 + Inspector 드래그". 코드 / SO schema 변경 없음.
사용자가 자산 다운로드 후 마음에 드는 거 결정하면 그때 SO 에 적용 또는 추가 작업 (ParticleSystem 으로 가면 그때 코드 추가).
