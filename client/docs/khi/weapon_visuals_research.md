# 무기 비주얼 — 조사 결과 요약

> 2026-05-14. 활/스태프 추가에 앞서 현재 검 분석 + 동일 톤 후보 조사.
> 상세 플랜: [weapon_visuals_plan.md](weapon_visuals_plan.md)
> 시스템 디자인: [weapon_system_design.md](weapon_system_design.md)

---

## 1. 현재 검 — 어떻게 만들어졌나

| 항목 | 내용 |
|---|---|
| 패키지 | MoreMountains **TopDown Engine v4.5** — Koala2D 데모 |
| 스프라이트 경로 | `LostMemory/Assets/TopDownEngine/Demos/Koala2D/Sprites/Koala/KoalaSword*.png` |
| 픽셀 사양 | 128×128 프레임 / 16 PPU |
| 구조 | **캐릭터 애니에 통합** — 검만 따로 sprite 아님. 캐릭터 idle/slash 프레임 자체에 검이 그려짐 |
| 톤 | 귀여운 만화풍 픽셀. 코알라 캐릭터 기반. 채도 높은 원색 + 검은 외곽선 |
| 같은 시리즈 검 변종 | KoalaSword / KoalaChargeSword / KoalaNinjaSword (3종) |

---

## 2. 같은 패키지에서 이미 쓸 수 있는 무기 (신규 작업 0)

`LostMemory/Assets/TopDownEngine/Demos/Koala2D/Prefabs/Weapons/Weapons/`

| Prefab | 활용 아이디어 |
|---|---|
| KoalaGun | 활 강화 path "총" 변형 |
| KoalaRifle | 활 강화 path "총" 변형 |
| KoalaShotgun | 활 강화 path "산탄" 변형 |
| KoalaCannon | 활 강화 path "유탄발사기" 변형 |
| KoalaPlasma | 스태프 임시 대용 ("마법 발사체") |
| KoalaNinjaRifle | 보조 후보 |

→ **활의 강화 단계 무기는 신규 에셋 안 만들어도 됨.** 검 톤과 100% 일치.

---

## 3. 활·스태프 신규 조달 — 후보

### 1순위 (가장 cost-effective)
**[DanieruArt — [16x16] RPG Fantasy Icons (320+)](https://danieruart.itch.io/16x16rpgicons)** — $4

- **27 Bow** (애니메이션 포함)
- **49 Staff**
- 53 Spear + 53 Axe + 41 Sword + 9 Crossbow
- **활·스태프·봉 한 팩으로 다 해결**. 검까지 추가 변종 가능.

### 2순위 (톤 일치 최강)
**[Kenmi — Cute Fantasy RPG](https://kenmi-art.itch.io/cute-fantasy-rpg)**

- 16×16 top-down 픽셀. **bow shooting 애니** 포함
- Koala 톤과 가장 비슷한 cute fantasy 스타일
- **무료 샘플 있음** → 톤 검증용으로 먼저 받기

### 3순위 (가짓수 최다)
**[TheSquawkyRaven — Fantasy Pixel Art Weapon Icons](https://thesquawkyraven.itch.io/fantasy-weapon-icons)**

- 304종 × 2 (B&W 외곽선 양쪽 제공)
- Koala의 검은 외곽선 스타일과 매칭 좋음

### 무료 백업
- [OpenGameArt — 16x16 Weapon Sprites Free](https://opengameart.org/content/16x16-weapon-sprites-free) — shortBow, longBow, lightXbow 등 무료

---

## 4. 가장 큰 결정 — 캐릭터 통합 vs 별도 sprite

검은 캐릭터 애니에 통합되어 있음. 활/스태프는 어떻게 갈지가 작업 방식의 분기점.

| 옵션 | 작업량 | 톤 일치 | 비고 |
|---|---|---|---|
| **A. 통합형** (현재 검 방식) | 매우 큼 | 100% | 무기별 캐릭터 애니 전체 신규 |
| **B. 별도 sprite** | 작음 | 70% | itch.io 아이콘 그대로 활용 가능. 검과 시각 문법 안 맞음 |
| **C. 하이브리드 (추천)** | 중간 | 90% | 검만 통합형 유지. 활/스태프/봉은 별도 sprite. 코알라 "범용 무기 들기" idle 1개만 신규 |

**추천: 옵션 C**. 검·활·스태프가 동작 자체가 다르므로 시각 문법 차이가 디자인적으로 정당화됨.

---

## 5. 다음에 결정해야 할 것 (사용자 액션)

1. **옵션 A / B / C 중 택1** — 작업 방식이 여기서 갈림
2. **활 슬롯 컨셉** — Koala 화기를 활로 매핑할지 vs 진짜 활 외형을 쓸지
3. **DanieruArt 팩 $4 구매 여부** — 일단 사두면 봉까지 다 해결되는 cost-effective 선택

---

## 6. 권장 작업 순서

1. **Kenmi 무료 샘플 다운** → Koala 톤과 일치 검증 (10분)
2. **DanieruArt 팩 $4 구매** → 활·스태프·봉 sprite 확보 (즉시)
3. **옵션 C 결정** 후 코알라 "범용 무기 들기" idle 1프레임 신규 (AI 생성 또는 직접)
4. **활 prefab 1개 제작** → 별도 sprite 부착 방식 검증
5. 검증 OK → 스태프 / 봉 순차 진행

---

## 출처

- [TopDown Engine Weapons Docs](https://topdown-engine-docs.moremountains.com/weapons.html)
- [DanieruArt — 16x16 RPG Fantasy Icons](https://danieruart.itch.io/16x16rpgicons)
- [Kenmi — Cute Fantasy RPG](https://kenmi-art.itch.io/cute-fantasy-rpg)
- [TheSquawkyRaven — Fantasy Pixel Art Weapon Icons](https://thesquawkyraven.itch.io/fantasy-weapon-icons)
- [OpenGameArt — 16x16 Weapon Sprites Free](https://opengameart.org/content/16x16-weapon-sprites-free)
- [TopDownEngineExtensions (GitHub)](https://github.com/reunono/TopDownEngineExtensions)
