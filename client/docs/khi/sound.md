# Sound Catalog (LostMemory)

> 프로젝트(`LostMemory`)에는 자체 사운드 파일이 없음. 모든 사운드는 외부 패키지(TopDownEngine, MMInterface, MMFeedbacks, InventoryEngine, DungeonArchitect_Samples, Unity Timeline 데모)에 분산되어 있다. 이 문서는 사용 가능한 사운드의 인덱스다.

## 사용법
1. 작업 중인 프리팹/상황을 **카테고리**(아래 목차)에서 찾는다.
2. 표에서 **분위기/스타일**과 **추천 사용처**를 보고 후보를 추린다.
3. **에셋 경로**를 Unity Inspector에 드래그하거나 `AssetDatabase.LoadAssetAtPath`로 참조한다.
4. 적합한 게 없으면 비슷한 음을 직접 청취해서 매칭 후 이 문서에 메모를 추가한다.

### 분위기 약어
- **2D-Pixel**: Koala2D 픽셀 게임용. 짧고 카툰풍, 톤 높음
- **3D-Real**: Loft3D / Colonel용. 사실적 톤, 길이 다양
- **Tank**: 굵고 무거운 산업/메카닉음
- **UI**: 합성 톤, 짧음, 인터페이스 피드백
- **Demo**: 데모 전용 — 게임 본 작업엔 비추천이지만 임시론 OK

---

## 1. 무기 / 공격

### 1-1. 근접 (검·휘두름)
| 파일명 | 분위기/스타일 | 추천 사용처 | 에셋 경로 |
|---|---|---|---|
| KoalaSword1.wav | 2D-Pixel, 짧음 | Khi 검사 캐릭터 공격 (1타) | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaSword1.wav` |
| KoalaSword2.wav | 2D-Pixel, 짧음 | Khi 검사 공격 (2타) | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaSword2.wav` |
| KoalaSword3.wav | 2D-Pixel, 짧음 | Khi 검사 공격 (3타/마무리) | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaSword3.wav` |
| LoftSwoosh.wav | 3D-Real, 휙 | 무기 휘두름 공통, 회피/회전 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftSwoosh.wav` |
| TankWoosh.wav | Tank, 굵은 휙 | 큰 무기/보스 휘두름 | `Assets/TopDownEngine/Demos/Tanks/Audio/TankWoosh.wav` |
| KoalaGrassCut.wav | 2D-Pixel, 사각 | 풀/약한 오브젝트 베기 | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaGrassCut.wav` |

### 1-2. 사격 / 원거리 (Demo)
대부분 데모용 — 본 작업 시 비추천이지만 임시 테스트에는 OK.

| 파일명 | 분위기/스타일 | 추천 사용처 | 에셋 경로 |
|---|---|---|---|
| KoalaLaser.wav / KoalaLaser2.wav | 2D-Pixel | 마법/투사체 (픽셀톤) | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaLaser.wav` 외 |
| KoalaMachineGun1~3.wav | 2D-Pixel | 연발 사격 (Demo) | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaMachineGun1.wav` 외 |
| KoalaMagnum1~2.wav | 2D-Pixel | 단발 사격 (Demo) | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaMagnum1.wav` 외 |
| KoalaShotgun.wav | 2D-Pixel | 산탄총 (Demo) | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaShotgun.wav` |
| LoftGun1~3 / LoftAssaultRifle / LoftShotgun / LoftSilentGun / LoftSniper | 3D-Real | 3D 슈터용 (Demo) | `Assets/TopDownEngine/Demos/Loft3D/Sounds/` |
| LoftToyGun.wav | 3D-Real, 가벼움 | 장난감 총/카툰 무기 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftToyGun.wav` |
| TankCannonShot.wav | Tank, 굵음 | 보스 포격/대형 공격 | `Assets/TopDownEngine/Demos/Tanks/Audio/TankCannonShot.wav` |
| TankMachineGun.wav | Tank | 보스 연발 (Demo) | `Assets/TopDownEngine/Demos/Tanks/Audio/TankMachineGun.wav` |

### 1-3. 재장전
| 파일명 | 분위기/스타일 | 추천 사용처 | 에셋 경로 |
|---|---|---|---|
| ColonelReload.wav | 3D-Real | 무기 교체/재장전 (Demo) | `Assets/TopDownEngine/Demos/Colonel/Sounds/ColonelReload.wav` |
| ColonelReloadNeeded.wav | 3D-Real, 경고 | 탄약 부족 경고음 | `Assets/TopDownEngine/Demos/Colonel/Sounds/ColonelReloadNeeded.wav` |
| LoftReload.wav | 3D-Real | 무기 교체/재장전 (Demo) | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftReload.wav` |

---

## 2. 발소리 / 이동

| 파일명 | 분위기/스타일 | 추천 사용처 | 에셋 경로 |
|---|---|---|---|
| ColonelFootstep1~4.wav | 3D-Real | 3D 캐릭터 걷기 (4종 랜덤) | `Assets/TopDownEngine/Demos/Colonel/Sounds/ColonelFootstep1.wav` 외 |
| KoalaSteps1~2.wav | 2D-Pixel | Khi/픽셀 캐릭터 걷기 | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaSteps1.wav` 외 |
| LoftWalk.wav | 3D-Real | 3D 걷기 (Demo) | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftWalk.wav` |
| LoftRun.wav | 3D-Real | 3D 달리기 (Demo) | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftRun.wav` |
| KoalaDash.wav | 2D-Pixel | **Khi 대시** | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaDash.wav` |
| KoalaJump.wav | 2D-Pixel | 점프 | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaJump.wav` |
| LoftJump1~2.wav | 3D-Real | 3D 점프 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftJump1.wav` 외 |
| KoalaFalling.wav | 2D-Pixel | 낙하 | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaFalling.wav` |
| TankDrive.wav | Tank, 루프 | 보스 이동/탑승물 | `Assets/TopDownEngine/Demos/Tanks/Audio/TankDrive.wav` |

---

## 3. 피격 / 사망

| 파일명 | 분위기/스타일 | 추천 사용처 | 에셋 경로 |
|---|---|---|---|
| KoalaHurt1~3.wav | 2D-Pixel | Khi 피격 / 픽셀 적 피격 | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaHurt1.wav` 외 |
| ColonelHurt1~3.wav | 3D-Real | 3D 캐릭터 피격 | `Assets/TopDownEngine/Demos/Colonel/Sounds/ColonelHurt1.wav` 외 |
| LoftHit1~3.wav | 3D-Real, 타격감 | **공격 명중 시 임팩트음** | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftHit1.wav` 외 |
| LoftImpact.wav | 3D-Real | 충돌/임팩트 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftImpact.wav` |
| KoalaImpact.wav | 2D-Pixel | 픽셀 임팩트 | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaImpact.wav` |
| LoftDeath.wav | 3D-Real | 플레이어 사망 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftDeath.wav` |
| LoftAIDeath.wav | 3D-Real | 적/AI 사망 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftAIDeath.wav` |
| LoftGunMiss.wav | 3D-Real | **공격 빗나감/Miss** | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftGunMiss.wav` |
| Player Hurt.wav | 3D-Real | 플레이어 피격 (Survival 데모) | `Assets/CodeRespawn/DungeonArchitect_Samples/Game3D_SurvivalShooter/Audio/Effects/Player Hurt.wav` |
| Player Death.wav | 3D-Real | 플레이어 사망 (Demo) | `Assets/CodeRespawn/DungeonArchitect_Samples/Game3D_SurvivalShooter/Audio/Effects/Player Death.wav` |
| Hellephant Hurt / Death.wav | 3D-Real, 굵음 | **대형/보스 적 피격·사망** | `Assets/CodeRespawn/DungeonArchitect_Samples/Game3D_SurvivalShooter/Audio/Effects/Hellephant *.wav` |
| ZomBear Death.wav | 3D-Real, 굵음 | 중형 적 사망 | `Assets/CodeRespawn/DungeonArchitect_Samples/Game3D_SurvivalShooter/Audio/Effects/ZomBear Death.wav` |
| ZomBunny Hurt / Death.wav | 3D-Real, 작음 | 소형 적 피격·사망 | `Assets/CodeRespawn/DungeonArchitect_Samples/Game3D_SurvivalShooter/Audio/Effects/ZomBunny *.wav` |

---

## 4. 패링 / 방어
> **전용 패링 사운드 없음.** 대용 후보:
> - `LoftFingerSnap.wav` — 짧고 날카로움, **타이밍 패링 성공 큐**로 적합
> - `LoftTinySelect.wav` — 가볍고 작음, 패링 시도/판정 효과
> - `LoftGlassyTap.wav` — 유리 톤, 차가운 카운터 느낌
> - `LoftBump.wav` — 부딪힘, 일반 방어
> - MMFeedbacks의 `Snap.wav` — 단단한 스냅

| 파일명 | 분위기/스타일 | 추천 사용처 | 에셋 경로 |
|---|---|---|---|
| LoftFingerSnap.wav | 3D-Real, 짧고 날카로움 | **패링 성공 큐** | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftFingerSnap.wav` |
| LoftTinySelect.wav | 3D-Real, 가벼움 | 패링 판정/시도 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftTinySelect.wav` |
| LoftGlassyTap.wav | 3D-Real, 유리 | 차가운 카운터/완벽 방어 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftGlassyTap.wav` |
| LoftBump.wav | 3D-Real, 둔탁 | 일반 방어/블록 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftBump.wav` |

---

## 5. 아이템 / 획득 / 인벤토리

### 5-1. 월드 획득 (TopDownEngine)
| 파일명 | 분위기/스타일 | 추천 사용처 | 에셋 경로 |
|---|---|---|---|
| KoalaLoot.wav | 2D-Pixel, 밝음 | **아이템 줍기** | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaLoot.wav` |
| LoftCoin.wav | 3D-Real | 동전/통화 획득 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftCoin.wav` |
| KoalaPot.wav | 2D-Pixel | 항아리/오브젝트 파괴 | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaPot.wav` |
| LoftSteal.wav | 3D-Real | 아이템 훔치기/획득 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftSteal.wav` |

### 5-2. 인벤토리 UI (InventoryEngine)
| 파일명 | 분위기/스타일 | 추천 사용처 | 에셋 경로 |
|---|---|---|---|
| ClickWav.wav | UI, 짧음 | 인벤토리 클릭 | `Assets/TopDownEngine/ThirdParty/MoreMountains/InventoryEngine/InventoryEngine/Sounds/DefaultSounds/ClickWav.wav` |
| OpenWav.wav / CloseWav.wav | UI | 인벤토리 열기/닫기 | 같은 경로 |
| DropWav.wav | UI | 아이템 버리기 | 같은 경로 |
| EquipWav.wav | UI | 장비 착용 | 같은 경로 |
| MoveWav.wav | UI | 아이템 이동/슬롯 이동 | 같은 경로 |
| SelectWav.wav | UI | 슬롯 선택 | 같은 경로 |
| UseWav.wav | UI | 아이템 사용 | 같은 경로 |
| ErrorWav.wav | UI, 경고 | 사용 불가/오류 | 같은 경로 |
| AppleUse.wav | UI | 음식/회복 아이템 사용 | `Assets/TopDownEngine/ThirdParty/MoreMountains/InventoryEngine/Demos/PixelRogue/Sounds/AppleUse.wav` |

---

## 6. UI / 인터페이스 (MMInterface, 36개)

> 메뉴/HUD/시스템 UI 피드백. 36개 전부 나열은 과함. 대표만 표시 — 디렉토리 직접 탐색 권장:
> `Assets/TopDownEngine/ThirdParty/MoreMountains/MMInterface/Demos/UISoundSamples/` (정확한 경로는 패키지 버전마다 다를 수 있음)

| 카테고리 | 대표 파일 | 추천 사용처 |
|---|---|---|
| 클릭 | Click, Tweep, Pleep | 버튼 클릭, 메뉴 항목 선택 |
| 확인 | Confirm, Ding, Success | 확인/성공 피드백 |
| 거절 | Nope, Error, Danger | 잘못된 입력/실패 |
| 호버 | BeepShort, Thud | 메뉴 호버, 강조 |
| 길게 누름/대기 | BeepLong, Flute | 차징/대기 UI |

> **Tip**: MMFeedbacks에 동일 패키지군의 `Snap`, `Tick`, `Note`, `Hat`, `Tom` 등 짧은 효과음도 있음 (`Assets/TopDownEngine/ThirdParty/MoreMountains/MMFeedbacks/Demos/Sounds/` 류 경로). UI/게임플레이 피드백 양쪽에 활용 가능.

---

## 7. 환경 / 앰비언트 / BGM

### 7-1. BGM
| 파일명 | 분위기/스타일 | 추천 사용처 | 에셋 경로 |
|---|---|---|---|
| KoalaBackgroundMusic2.wav | 2D-Pixel, 루프 | 픽셀 스테이지 BGM | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaBackgroundMusic2.wav` |
| BackgroundMusic1.wav | 3D-Real, 루프 | 3D 스테이지 BGM | `Assets/TopDownEngine/Demos/Loft3D/Sounds/BackgroundMusic1.wav` |
| ExplodudesSong.wav | 발랄 | 보너스/미니게임 BGM | `Assets/TopDownEngine/Demos/Explodudes/Sounds/ExplodudesSong.wav` |

### 7-2. 환경음
| 파일명 | 분위기/스타일 | 추천 사용처 | 에셋 경로 |
|---|---|---|---|
| LoftWind.wav | 3D-Real, 루프 | 야외/광장 앰비언트 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftWind.wav` |
| LoftSand.wav | 3D-Real | 모래/사막 효과 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftSand.wav` |
| LoftBubble.wav | 3D-Real | 물/거품 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftBubble.wav` |
| crickets (Timeline 데모) | 야간 | 밤 앰비언트 | `Assets/Unity Timeline/...` |

### 7-3. 폭발 / 임팩트 / 메커니즘
| 파일명 | 분위기/스타일 | 추천 사용처 | 에셋 경로 |
|---|---|---|---|
| LoftExplosion.wav | 3D-Real, 굵음 | 폭발/스킬 임팩트 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftExplosion.wav` |
| TankCannonExplosion.wav | Tank | 대형 폭발 | `Assets/TopDownEngine/Demos/Tanks/Audio/TankCannonExplosion.wav` |
| LoftMechanism.wav | 3D-Real | 문/장치 작동 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftMechanism.wav` |
| LoftSpring.wav | 3D-Real, 통통 | 스프링/트랩 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftSpring.wav` |
| LoftThump.wav | 3D-Real, 둔탁 | 우둑/낙하 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftThump.wav` |
| LoftDrop.wav | 3D-Real | 떨어짐 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftDrop.wav` |
| KoalaTeleport.wav | 2D-Pixel | 워프/순간이동 | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaTeleport.wav` |
| KoalaLocked.wav | 2D-Pixel, 경고 | **잠긴 문/접근 불가** | `Assets/TopDownEngine/Demos/Koala2D/Sounds/KoalaLocked.wav` |
| LoftSlowdown.wav | 3D-Real | 시간/속도 감소 | `Assets/TopDownEngine/Demos/Loft3D/Sounds/LoftSlowdown.wav` |

---

## 8. 기타 (Demo / Timeline)

### Unity Timeline 데모 (12개)
대부분 짧은 효과음. 단발 큐로 활용 가능:
- `breath` — 호흡, 캐릭터 시동
- `bump` — 부딪힘
- `jog`, `jump` — 이동 효과
- `hey` — 보이스 큐
- `neon` — 전자/네온
- `crickets` — 야간 환경
- `can`, `canBounce`, `canFall`, `canRoll`, `table` — 오브젝트 상호작용

경로: `Assets/Unity Timeline/...` (Timeline 패키지 내부 데모 폴더)

### MMFeedbacks 데모 (20개)
짧은 타악기/효과음. 게임플레이 피드백에 1회성 큐로 사용.
- 타악기: Bass, Crash, Cymbal, Hat, Tom, Tick
- 효과: Jump, Key, Snap, Track, Note

경로: `Assets/TopDownEngine/ThirdParty/MoreMountains/MMFeedbacks/Demos/...` (정확한 경로는 패키지 버전 확인)

---

## 상황별 빠른 매칭 치트시트

| 상황 | 1순위 후보 | 2순위 |
|---|---|---|
| Khi 검 휘두름 (1~3타) | KoalaSword1~3 | LoftSwoosh |
| 회피/대시 | KoalaDash | LoftSwoosh |
| 점프 | KoalaJump | LoftJump1~2 |
| 발소리 (Khi) | KoalaSteps1~2 | ColonelFootstep1~4 |
| 공격 명중 (적 피격) | LoftHit1~3 | KoalaImpact |
| 공격 빗나감 | LoftGunMiss | LoftSwoosh |
| Khi 피격 | KoalaHurt1~3 | ColonelHurt1~3 |
| Khi 사망 | LoftDeath | Player Death |
| 소형 적 사망 | ZomBunny Death | KoalaHurt 류 |
| 중·대형 적/보스 사망 | Hellephant Death | ZomBear Death |
| 보스 등장/포효 | TankCannonExplosion + Hellephant Hurt 합성 | LoftExplosion |
| 보스 공격(포격) | TankCannonShot | LoftExplosion |
| 패링 성공 | LoftFingerSnap | LoftGlassyTap |
| 일반 방어/블록 | LoftBump | MMFeedbacks/Snap |
| 아이템 줍기 | KoalaLoot | LoftCoin |
| 항아리/오브젝트 파괴 | KoalaPot | LoftBump |
| 잠긴 문 | KoalaLocked | LoftMechanism |
| 워프/포탈 | KoalaTeleport | LoftSlowdown |
| 인벤토리 열기 | InventoryEngine/OpenWav | MMInterface/Click |
| UI 버튼 클릭 | MMInterface/Click | InventoryEngine/ClickWav |
| 회복 아이템 사용 | AppleUse | KoalaLoot |
| BGM (스테이지) | BackgroundMusic1 (3D) / KoalaBackgroundMusic2 (2D) | — |
| 야외 앰비언트 | LoftWind | crickets |

---

## 갱신 메모
- 새 사운드를 추가하면 이 문서에도 항목 추가
- 적용한 사운드는 별도로 코드/프리팹 주석에 출처 명시 권장 (예: `// SFX: KoalaSword1`)
- 분위기/스타일과 추천 사용처는 파일명 기반 추정이므로, 실제 적용 전 청취 권장
