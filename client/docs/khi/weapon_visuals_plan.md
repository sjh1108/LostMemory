# 무기 비주얼 플랜 — 활 / 스태프

> 2026-05-14 작성. 기능 디자인은 [weapon_system_design.md](weapon_system_design.md) 참고.
> 이 문서는 **moodboard + 체크리스트**. "결정"이 아니라 "탐색·정리". 강화 path별 비주얼·속성별 비주얼은 본 문서 범위 밖.

---

## 0. 현재 검 분석 (조사 완료)

### 출처
- **패키지**: MoreMountains **TopDown Engine v4.5** — Koala2D 데모
- **스프라이트 위치**: `LostMemory/Assets/TopDownEngine/Demos/Koala2D/Sprites/Koala/`
  - `KoalaSwordIdle.png` / `KoalaSwordSlash1~3.png`
- **Prefab**: `LostMemory/Assets/Prepabs/Weapons/TestKhi_Sword.prefab` (추정 경로, 확정 필요)
- **같은 패키지 검 변종**: `KoalaSword` / `KoalaChargeSword` / `KoalaNinjaSword` 3종

### 스타일·기술 사양
- **픽셀 사이즈**: 스프라이트 시트 프레임 128×128px (캐릭터 + 무기 통합)
- **PPU**: 16 pixels per unit
- **톤**: 귀여운 만화풍 픽셀, 밝고 친근. 코알라 캐릭터에 맞춰진 둥근 실루엣
- **중요**: 검 스프라이트는 **캐릭터 애니메이션에 통합**됨 (캐릭터 손에 별도 sprite parent가 아니라, 캐릭터 idle/slash 프레임 자체에 검이 그려진 형태)

### 같은 패키지에 이미 있는 무기 (재활용 가능)
- **원거리 화기 6종**: KoalaGun / KoalaRifle / KoalaShotgun / KoalaCannon / KoalaPlasma / KoalaNinjaRifle
- 모두 캐릭터 통합형, 같은 톤 100% 일치
- **활용 아이디어**: 활 무기의 강화 path "총 / 화염방사·유탄" 변형에 KoalaGun·KoalaCannon·KoalaPlasma 직접 매핑 가능 → **에셋 신규 제작 안 해도 됨**
- 단, "활" 그 자체 (시위 당기는 형태)는 패키지에 없음

### 결정된 톤
- **만화풍 픽셀, 16 PPU, 128px 캐릭터 통합 스프라이트**
- **컬러**: 채도 높은 원색 + 검은 외곽선 + 회색·흰색 디테일 (Koala 톤)
- **피해야 할 스타일**: 다크 판타지 (RafaelMatos 패키지 톤 — 32px 작은 픽셀, 어두운 색조). 프로젝트에 들어있긴 한데 톤 안 맞음.

### 체크리스트 (남은 작업)
- [ ] 검 인게임 스크린샷 3~5장 캡처 (정면 / 측면 / Slash 1~3)
- [ ] Koala 컬러 팔레트 추출 (포토샵 색상 추출 또는 직접 픽셀 픽)
- [ ] 검 prefab 정확한 경로 확정 — `Prepabs/Weapons/` 또는 `TestKhi/` 내부
- [ ] 활/스태프도 캐릭터 통합형으로 갈지, 별도 sprite로 갈지 결정 (아래 섹션 7 참고)

---

## 1. 활 — 기본형

### 디자인 메모
- 강화 전 기본 활 1종만 (노/총/화염방사기는 강화 단계, 본 문서 범위 밖)
- "평타 기반 무기" 정체성 — 손에 익숙해 보이는 자연스러운 형태
- 차지샷 동작이 있으므로 **시위를 당기는 자세**가 멋있게 나와야 함
- **현재 검과 동일하게 캐릭터 통합형 스프라이트로 가는 게 톤 일치 베스트** (별도 sprite parent 방식은 검과 시각 문법이 달라짐)

### 후보 (조사 완료)

**1순위 — 신규 픽셀 에셋 구매·조달**
- [[16x16] RPG Fantasy Icons (320+) — DanieruArt](https://danieruart.itch.io/16x16rpgicons) ($4)
  - **27 Bow 스프라이트 (애니메이션 포함)** + 49 Staff + 53 Spear + 53 Axe + 41 Sword
  - 한 팩으로 활·스태프·봉(창/도끼/망치) 다 커버 → **가장 cost-effective**
  - 다만 16×16 아이콘 단위 — 인벤토리·UI 용. 캐릭터 통합형 애니로 쓰려면 추가 작업 필요
- [Fantasy Pixel Art Weapon Icons — TheSquawkyRaven](https://thesquawkyraven.itch.io/fantasy-weapon-icons)
  - 304종, B&W 외곽선 양쪽 제공 (Koala 외곽선 스타일과 매칭 좋음)
- [Cute Fantasy RPG — Kenmi](https://kenmi-art.itch.io/cute-fantasy-rpg)
  - 16x16 top-down 픽셀, **bow shooting 애니메이션 포함**. 톤이 Koala와 가장 가까움
  - 무료 샘플 있음 → 톤 검증용으로 먼저 받아보기 추천

**2순위 — Koala 패키지 화기 그대로 사용**
- KoalaGun / KoalaRifle / KoalaShotgun 중 1종을 **"활" 슬롯에 매핑**
- 톤 100% 일치, 즉시 사용 가능
- 단, "활"의 판타지 정체성 (시위 당기기·화살)이 사라짐 → 게임 컨셉 어울리면 좋고, 아니면 신규 조달

**3순위 — AI 생성**
- Koala 스타일 reference 3~5장 입력 → 활 들고 있는 코알라 idle/draw/release 4프레임 생성
- 시간 빠르지만 일관성 검증 필요. 보조 수단으로만.

### 체크리스트
- [ ] 1순위 itch.io 팩 3개 중 1~2개 구매·다운로드 → 톤 검증
- [ ] 캐릭터 통합형 vs 별도 sprite 방식 결정 (섹션 7 참고)
- [ ] 화살 디자인 (별도 sprite) — 위 팩들에 보통 포함
- [ ] 차지 상태 시각 효과 (활 빛남 / 화살에 오라) — VFX 단계
- [ ] **결정**: 신규 에셋 vs Koala 화기 재매핑

### 메모
- 선호 방향:
- 피하고 싶은 방향:

---

## 2. 스태프 — 기본형

### 디자인 메모
- 강화 전 기본 스태프 1종만 (트페/마법사/비숍은 강화 단계)
- "스킬 기반 무기" 정체성 — 위엄·신비 강조
- 스킬 시전 시 **스태프 끝에서 마법이 발사되는 동선**이 명확해야 함 (시각적 anchor)
- 봉과 헷갈리지 않는 실루엣 필수 (봉=무기, 스태프=촉매)
- Koala 톤(귀여운 만화 픽셀)과 마법사 정체성(위엄·신비)이 충돌 가능 → **"귀여운 마법사 코알라"** 컨셉으로 풀어야 자연스러움 (위엄 톤은 톤 자체가 안 맞음)

### 후보 (조사 완료)

**1순위 — itch.io 팩에서 조달**
- [[16x16] RPG Fantasy Icons (320+) — DanieruArt](https://danieruart.itch.io/16x16rpgicons): **49 Staff 스프라이트** + 4 Tome 보유
- [Fantasy Pixel Art Weapon Icons — TheSquawkyRaven](https://thesquawkyraven.itch.io/fantasy-weapon-icons): 마법 무기 포함 304종
- **활과 같은 팩에서 같이 조달**하는 게 톤 통일에 베스트 — DanieruArt 팩 하나로 활·스태프·봉 다 해결

**2순위 — Koala 패키지 KoalaPlasma 재활용**
- KoalaPlasma 무기는 청록색 에너지 발사 형태 → **"마법" 임시 대용** 가능
- 스태프 외형은 없지만, 시각 컨셉 ("마법 같은 발사체")은 매칭됨
- MVP·프로토타입 단계에 추천. 정식 스태프는 추후 교체.

**3순위 — AI 생성**
- "픽셀 아트 코알라 마법사가 짧은 지팡이 든 idle 4프레임" 식으로 reference 입력해 생성
- 톤 검증 필요

### 체크리스트
- [ ] Reference 3~5장 모으기 (Koala 톤 + 마법사 컨셉 교차점)
- [ ] 실루엣 결정 — 짧은 완드 / 긴 지팡이 / 양손 스태프?
- [ ] 끝부분 디자인 — 보석 / 수정 / 별 모티프 / 룬?
- [ ] 재질 — 목재 / 결정 (Koala 색감에 맞는 채도 높은 단색 추천)
- [ ] 봉과의 실루엣 구별 규칙 명문화 (예: 봉=일자 막대 / 스태프=끝에 장식)
- [ ] **MVP 단계 결정**: KoalaPlasma 임시 사용 vs 신규 에셋 즉시 조달

### 메모
- 선호 방향:
- 피하고 싶은 방향:
- 봉 vs 스태프 구별 규칙:

---

## 3. 스태프 스킬 이펙트 톤

기본형 스태프 디자인 결정 후 진행. 스킬별 색·파티클 밀도·시간감 reference.

### 대상 스킬 (기본형 1단계만)
- [ ] **주력기 후보** — 파이어볼 류 (단발 투사체)
- [ ] **한 방기 후보** — 메테오 류 (광역 폭발)
- [ ] **유틸기 후보** — 블링크 / 배리어 (필요 시)

### 체크리스트 (스킬별)
- [ ] Reference 영상·gif 2~3개 모으기
- [ ] 메인 색 (스킬 정체성 컬러)
- [ ] 파티클 밀도 (가벼움 / 화려함)
- [ ] 지속 시간 (순간 / 1초 / 지속형)
- [ ] 사운드 톤 (참고용 메모)
- [ ] 시전 → 발사 → 적중의 3 페이즈 비주얼 분리

### Reference 슬롯
```
주력기:
[ref 1] [ref 2]

한 방기:
[ref 1] [ref 2]

유틸기:
[ref 1] [ref 2]
```

### 메모
- 마나 시스템과 시각 연동 (예: 풀충전 시 스태프 빛남):
- 속성 시스템 대비 (불·물·전기·바람) — 같은 스킬이 색만 바뀌어도 OK인지:

---

## 4. UI 아이콘 톤

인벤토리·HUD·무기 슬롯에 들어갈 작은 아이콘.

### 체크리스트
- [ ] 기존 검 아이콘 캡처 (크기·테두리·배경 스타일)
- [ ] 아이콘 사이즈 통일 규격 (px)
- [ ] 배경 — 투명 / 단색 / 그라데이션?
- [ ] 테두리 — 있음 / 없음 / 등급별 색?
- [ ] 활 아이콘 시안 슬롯
- [ ] 스태프 아이콘 시안 슬롯

### Reference 슬롯
```
[검 아이콘 캡처]
[아이콘 톤 reference 1]
[아이콘 톤 reference 2]
```

---

## 5. (기술 결정) 캐릭터 통합 vs 별도 sprite

이게 활/스태프 작업의 진짜 분기점. 검은 캐릭터 애니에 통합돼 있어서, 활/스태프도 같은 방식으로 가면 톤은 완벽하지만 작업량이 폭발.

### 옵션 A — 캐릭터 통합형 (현재 검 방식)
- 활 드는 코알라 idle/draw/release, 스태프 드는 코알라 idle/cast 등 **무기별 캐릭터 애니 전체를 새로 그림**
- 검 = 4 애니메이션(idle/slash1/2/3) × 무기 변종 = 작업량 큼
- 톤 일치 100%. 무기 교체 시 캐릭터 손 위치 안 맞는 문제 없음.
- **AI 생성·외주 비용 큼**

### 옵션 B — 별도 sprite 부착형
- 코알라는 "무기 들 준비 자세"만 있고, **무기 sprite는 손 위치에 parent**
- 무기 1개 = sprite 1장 + 회전·rotate 애니. 작업량 1/10
- itch.io 16×16 아이콘 그대로 활용 가능
- 단점: 코알라 손 위치 정확도 필요, 검의 통합형과 시각 문법 안 맞음 (혼재 시 어색)
- 화살 발사 같은 동작은 자연스러움

### 옵션 C — 하이브리드 (추천)
- **검**: 기존 통합형 유지 (이미 만들어진 거)
- **활/스태프/봉**: 별도 sprite 부착형 신규
- 캐릭터에 "범용 무기 들기" 자세 1개만 추가 → 활·스태프·봉 sprite 다 거기 부착
- 검만 따로 노는 게 어색해질 위험 있지만, 활/스태프는 어차피 검과 동작 자체가 다르니 시각 문법 차이가 디자인적으로 정당화됨
- **작업량 / 톤 일치 / 확장성 균형**

### 결정 사항
- [ ] **옵션 A / B / C 중 택1**
- [ ] (C 선택 시) "범용 무기 들기" 자세 코알라 idle 신규 그려야 함 — 1프레임이지만 일관성 있어야 함
- [ ] 무기 sprite의 손 부착 좌표 규약 명문화 (모든 무기 같은 anchor)

---

## 6. 에셋 조달 방법

각 카테고리별로 어떻게 만들지 미리 정해두면 막힘 ↓

### 결정 사항
- [ ] 활 외형 — AI / 외주 / 구매 (에셋스토어) / 직접
- [ ] 스태프 외형 — AI / 외주 / 구매 / 직접
- [ ] 화살 — AI / 구매 / 직접
- [ ] 스킬 이펙트 — VFX Graph 자작 / 에셋스토어 구매 / 외주
- [ ] UI 아이콘 — AI 생성 / 직접 / 외주

### 후보 에셋스토어 / 도구 (조사 완료)

**픽셀 무기 팩 (활·스태프·봉 한 번에)**
- [DanieruArt — [16x16] RPG Fantasy Icons (320+)](https://danieruart.itch.io/16x16rpgicons) — $4. 27 Bow(애니) + 49 Staff + 53 Spear + 53 Axe + 41 Sword. **최우선 후보**
- [TheSquawkyRaven — Fantasy Pixel Art Weapon Icons](https://thesquawkyraven.itch.io/fantasy-weapon-icons) — 304종, B&W 외곽선
- [Kenmi — Cute Fantasy RPG](https://kenmi-art.itch.io/cute-fantasy-rpg) — bow 애니 포함, 무료 샘플 있음. Koala 톤과 가장 비슷

**Koala 패키지 내 재활용 가능 무기 (이미 있음)**
- `LostMemory/Assets/TopDownEngine/Demos/Koala2D/Prefabs/Weapons/Weapons/`
  - KoalaGun / KoalaRifle / KoalaShotgun / KoalaCannon / KoalaPlasma / KoalaNinjaRifle
  - 활 강화 path "총·화염방사" 변형에 그대로 매핑 가능 → **신규 작업 0**

**참고 자료**
- [16x16 무료 무기 - OpenGameArt](https://opengameart.org/content/16x16-weapon-sprites-free) — shortBow, longBow, lightXbow 등 무료
- [TopDown Engine 무기 문서](https://topdown-engine-docs.moremountains.com/weapons.html) — MeleeWeapon / ProjectileWeapon 스크립트 확장 방법
- [TopDown Engine Extensions (GitHub)](https://github.com/reunono/TopDownEngineExtensions) — 커뮤니티 확장 (활/스태프 확장은 확인 필요)

### 예산·리드타임 메모
- itch.io 픽셀 팩: $4~$10, 즉시 다운로드 (리드타임 0)
- AI 생성: 시간 단위, 일관성 검증 필요
- 외주: 주 단위, 통일성·품질 최고
- **권장 우선순위**: 1) Koala 화기 즉시 활용 (활 임시) → 2) DanieruArt 팩 구매 (활·스태프 정식) → 3) 필요 시 AI/외주로 캐릭터 통합 애니

---

## 다음 단계 (이 문서 채워지면)

1. 톤 가이드 확정 → 기존 검과 통일성 검증
2. 활·스태프 기본형 reference 모음 → 실루엣 1안 결정
3. 스킬 이펙트 reference → 기본형 스킬 3종 비주얼 톤 결정
4. UI 아이콘 시안 → 검 옆에 나란히 놓고 검증
5. 조달 방법 확정 → 실제 에셋 제작 발주

---

## 본 문서 범위 밖 (나중에 별도 문서)

- 강화 path 3개별 비주얼 (단궁/총/화염방사 등) — 강화 디자인 확정 후
- 속성 4종별 비주얼 (불/물/전기/바람) — 속성 시스템 도입 시
- 검·봉 비주얼 — 검은 이미 있고, 봉은 후순위
- 캐릭터 무기 휴대 자세·등에 메는 모션
- 컷신·과장 이펙트
