# 오디오 인벤토리 & Mixer 통합 계획

> 관련 작업: [SFX Balance 도구 + GameAudioSettings 통합](./cl230_scene_transition_weapon_mana_carryover.md) (별개 작업, 동시에 진행 중)
> 작성 시점: 출시 임박, 사용자 옵션 메뉴의 SFX/Music 슬라이더 작동 보장이 목표.

## Context

현재 상태:
- `GameAudioSettings` 싱글톤 + `MainMixer.mixer` (Master/SFX/Music 그룹) + `AudioVolumeSliderBinder` UI 헬퍼 완성
- `SfxClipVolumeBalance.asset` + `SfxBalanceWindow` 클립별 multiplier 도구 완성
- 플레이어 전투 SFX 4개 컴포넌트 (KhiSfxBinder / UltimateReadyHud / UltimateCooldown / KhiParryFeedbackPresenter) 만 mixer SFX 그룹 통과

문제: 게임에 사용 중인 오디오 시스템이 4가지로 분산. 대부분이 우리 mixer 안 거침 → 사용자 슬라이더 영향 못 받음.

목표: 사용자 옵션 메뉴의 SFX / Music 슬라이더가 게임 모든 오디오를 제어하도록 정리.

---

## 사용 중인 오디오 시스템 (현황 스냅샷)

| 시스템 | 어디서 사용 | mixer 통과? |
|---|---|---|
| `AudioSource.PlayOneShot` (KhiSfxBinder 등) | 플레이어 prefab | ✅ SFX 그룹 |
| `AudioSource.PlayClipAtPoint` (KhiParryFeedbackPresenter) | 패링 성공 | ✅ SFX 그룹 (PlayClipAtPoint 자체는 temp source, but 우리 코드가 라우팅 처리) |
| 직접 `AudioSource.Play` (Phase0TimedBgm) | 인트로 BGM | ❌ |
| 직접 `AudioSource.PlayOneShot` (Phase0NarrationTypewriter) | 인트로 타이프음 | ❌ |
| `MMSoundManagerSoundPlayEvent.Trigger` (TDE BackgroundMusic) | Title / Town | ❌ MMSoundManager 자체 시스템 |
| `MMSoundManagerSoundPlayEvent.Trigger` (Stage1BgmController) | Dungeon 1F 전체 | ❌ MMSoundManager |
| `AnimationEventSfxRelay` | Orc / OrcRider / StoneGolem / Moose | 미확인 (그룹 3 작업 시 조사) |
| `SkeletonArcherSfxRelay` | SkeletonArcher | 미확인 |
| Bertha 보스 컨트롤러들 (impactSfx / burstSfx / fullComboProjectileBurstSfx) | BerthaRoot/2/3 | 미확인 |

---

## BGM 인벤토리

| 파일 | 사용 씬 | 컴포넌트 | mixer 통과? | 조정 방법 |
|---|---|---|---|---|
| `title.ogg` | Title.unity | TDE `BackgroundMusic` → `MMSoundManager` | ❌ | wav amplitude / 그룹 2 통합 후 슬라이더 |
| `town.ogg` | Town.unity | TDE `BackgroundMusic` → `MMSoundManager` | ❌ | wav / 그룹 2 |
| `stage1_dungeon.ogg` | Dungeon_1F_1R/2R/3R/4R/Shop | `Stage1BgmController` → `MMSoundManager` | ❌ | wav / 그룹 2 |
| `stage1_boss.ogg` | Dungeon_1F_Boss | `Stage1BgmController` → `MMSoundManager` | ❌ | wav / 그룹 2 |
| `maou_bgm_piano39.ogg` | Phase0_Intro | `Phase0TimedBgm` (direct AudioSource) | ❌ | Inspector `targetVolume` / 그룹 1 통합 후 슬라이더 |

### 미사용 (orphan) BGM — 빌드 정리 시 삭제 후보

- `intro_bgm.mp3`
- `maou_bgm_fantasy10.ogg`
- `maou_bgm_piano32.ogg`
- `maou_bgm_piano37.ogg`

---

## SFX 인벤토리

### 플레이어 SFX — SfxBalanceWindow 에서 관리 중 (mixer ✅)

| 파일 | 용도 | 컴포넌트 |
|---|---|---|
| `KoalaSword1/2/3.wav` | 검 콤보 1/2/3 | KhiSfxBinder.attackSwingClips |
| `LoftImpact.wav` | 적 명중 | KhiSfxBinder.hitImpactClips |
| `KoalaLoot.wav` | 패링 시작 | KhiSfxBinder.parryStartClip |
| `LoftBump.wav` | 패링 실패 | KhiSfxBinder.parryFailClip |
| `KoalaDash.wav` | 대시 | KhiSfxBinder.dashStartClip |
| `KoalaHurt3.wav` | 피격 / Down | KhiSfxBinder.hurtClips / downEnterClip |
| `LoftMechanism.wav` | 부활 시작 | KhiSfxBinder.reviveStartClip |
| `LoftDeath.wav` | 사망 | KhiSfxBinder.defeatClip |
| (패링 성공 ding) | 패링 성공 | KhiParryFeedbackPresenter.successSfx |

### 적 SFX (mixer ❌)

| 파일 | 사용 prefab | 컴포넌트 |
|---|---|---|
| `Audio/Bertha/midle_blade.wav` | BerthaRoot/2/3 | 여러 Bertha* 컨트롤러의 SFX 필드 |
| `Audio/Bertha/light_blade_swing_sfx.wav` | BerthaRoot/2/3 | (동일) |
| `Audio/Bertha/heavy_weapon_sfx.wav` | BerthaRoot/2/3 | (동일) |
| `Audio/Bertha/heavier_weapon_sfx_v2.wav` | BerthaRoot/2/3 | (동일) |
| `Audio/Enemies/skeleton/bowShoot.wav` | SkeletonArcher_CL041 | `SkeletonArcherSfxRelay` |
| `Audio/Enemies/skeleton/bowPull.wav` | SkeletonArcher_CL041 | `SkeletonArcherSfxRelay` |
| `Audio/Enemies/OrcRider/orcRiderAtk.wav` | OrcRider_CL039 | `AnimationEventSfxRelay` |
| `Audio/Enemies/StoneGolem/stoneGolemAttackSound.wav` | StoneGolem_Test | `AnimationEventSfxRelay` |
| `Casual Game Sounds U6/DM-CGS-08.wav` | Chobomb_CL212 | (CGS 외부) |
| `Casual Game Sounds U6/DM-CGS-21.wav` | Chobomb_CL212 | (CGS 외부) |
| `Casual Game Sounds U6/DM-CGS-47.wav` | Orc_CL037 | `AnimationEventSfxRelay` |
| `Casual Game Sounds U6/DM-CGS-48.wav` | Moose1_Test | `AnimationEventSfxRelay` |

### 인트로 SFX (mixer ❌)

| 파일 | 사용처 | 컴포넌트 |
|---|---|---|
| `freesound_community-medium-text-blip-14855.mp3` | Phase0_Intro 타이프 소리 | `Phase0NarrationTypewriter` (via `Phase0IntroSequenceData_SCN01.asset` SO) |

---

## Mixer 통과 현황 요약

| 영역 | mixer? | 사용자 슬라이더 영향? |
|---|---|---|
| 플레이어 전투 SFX (Khi*) | ✅ | ✅ SFX 슬라이더 |
| 모든 BGM (Title / Town / Dungeon / Phase0) | ❌ | ❌ |
| 모든 적 SFX (Bertha / Skeleton / Orc / StoneGolem / Moose / Chobomb) | ❌ | ❌ |
| Phase0 타이프 SFX | ❌ | ❌ |

→ **현 시점: 사용자 SFX 슬라이더 = 플레이어 SFX 만 영향, Music 슬라이더 = 무영향.**
→ **불완전한 UX. 통합 필요.**

---

## 통합 작업 계획 — 3단계 분류

### Group 1: Phase0 audio 라우팅 (5~10분, 우리 코드)

3개 컴포넌트 Awake/Start 에 1줄씩:

```csharp
// Phase0TimedBgm
audioSource.outputAudioMixerGroup =
    LostMemory.Audio.GameAudioSettings.Instance?.MusicGroup;

// Phase0IntroSequenceController (bgmSource)
bgmSource.outputAudioMixerGroup =
    LostMemory.Audio.GameAudioSettings.Instance?.MusicGroup;

// Phase0NarrationTypewriter (sfxSource)
sfxSource.outputAudioMixerGroup =
    LostMemory.Audio.GameAudioSettings.Instance?.SfxGroup;
```

위험도: 낮음 (우리 코드, 사이드 이펙트 없음).

### Group 2: MMSoundManager 양방향 sync (15~20분)

TDE BackgroundMusic + Stage1BgmController 모두 MMSoundManager 경유. MMSoundManager 자체 볼륨 API 활용:

```csharp
// GameAudioSettings.SetMusicVolume 안에 추가
MMSoundManager.Current?.SetVolumeTrack(
    MMSoundManager.MMSoundManagerTracks.Music,
    musicLinear01);
// SfxVolume 도 동일 패턴
MMSoundManager.Current?.SetVolumeTrack(
    MMSoundManager.MMSoundManagerTracks.Sfx,
    sfxLinear01);
```

확인 사항: `MMSoundManager.SetVolumeTrack` 의 단위 (linear 0..1 vs dB). 단위에 맞게 변환 후 호출.

이후: Title / Town / Dungeon BGM 자동으로 Music 슬라이더 따라옴.

### Group 3: 적 SFX + Bertha 라우팅 (20~30분, 조사 + 라우팅)

조사 대상:
- `LostMemory.Combat.AnimationEventSfxRelay` — AudioSource 사용? PlayClipAtPoint 사용?
- `LostMemory.Enemies.SkeletonArcherSfxRelay` — 동일
- Bertha 컨트롤러들 (impactSfx / burstSfx / fullComboProjectileBurstSfx 등이 어떻게 재생되는지)

패턴별 작업:
- AudioSource 사용 → Awake 에서 `outputAudioMixerGroup = SfxGroup` 1줄
- PlayClipAtPoint → temp source 가 mixer 안 거치므로 자체 AudioSource 라우팅으로 변환 필요

---

## 추천 진행 순서

1. **Group 1 + Group 2 먼저** (30분 합산)
   - 결과: 모든 BGM + 인트로 audio 슬라이더 작동
   - 검증: 옵션 메뉴 슬라이더 드래그 → BGM 볼륨 즉시 변화 확인
   - 위험 격리: MMSoundManager API 단위 / 라우팅 패턴 첫 검증

2. **Group 3 이후 진행** (시간 여유 있을 때)
   - 결과: 적 SFX 도 슬라이더 영향
   - 시간 부족 시 → 적 SFX 는 wav amplitude 로 미리 fix 하는 우회책

순서 근거:
- Group 1+2 끝나면 사용자 UX 거의 완성
- Group 3 은 코드 조사 비용 큼, 후순위가 합리적
- Group 2 가 패턴 검증 역할 → 잘 되면 Group 3 자신감

## 위험 / 주의

- **MMSoundManager.SetVolumeTrack 단위 확인 필요** — Group 2 시작 시 첫 검증.
- **MMSoundManager.Current 가 게임 초기에 null 가능** — null check 필수 (`?.` 연산자 또는 명시적 가드).
- **Bertha 의 SFX 필드들이 분산** — 각 컨트롤러 별도 라우팅 필요할 수 있음. Group 3 시 패턴화.
- **PlayClipAtPoint 의 한계** — temp source 는 mixer 라우팅 X. 우리 시스템으로 대체 시 새 헬퍼 (`RoutedPlayClipAtPoint`) 추가 고려.

## 빌드 정리 (출시 직전, 별개 작업)

미사용 BGM 4개 (`intro_bgm.mp3`, `maou_bgm_fantasy10/32/37.ogg`) 삭제 → 빌드 사이즈 감소.

## 진행 상태 체크리스트

- [x] 오디오 인벤토리 작성 (이 문서)
- [x] Group 1: Phase0 라우팅 (3줄)
- [x] Group 2: MMSoundManager sync
- [x] Group 2 검증 — 옵션 메뉴 슬라이더로 BGM 조정 확인
- [x] Group 3 조사 결과: **불필요** — 모든 적 SFX 가 이미 `MMSoundManager.Sfx` 통과 → Group 2 sync 로 자동 적용됨
- [ ] 미사용 BGM 삭제 (빌드 정리)
- [ ] SfxBalanceWindow / GameAudioSettings 의 진단 로그 정리

## Group 3 조사 후기 (작업 안 함)

원래 적 SFX 와 Bertha 보스 컨트롤러들의 mixer 라우팅을 추가하려 했음. 코드 조사 결과:

| 컴포넌트 | SFX 재생 경로 |
|---|---|
| `AnimationEventSfxRelay` (Orc / OrcRider / StoneGolem / Moose1) | `MMSoundManagerTracks.Sfx` |
| `SkeletonArcherSfxRelay` | `MMSoundManagerTracks.Sfx` |
| `BerthaAreaAttackController` (impactSfx) | `MMSoundManagerTracks.Sfx` |
| `BerthaDashAttackController` (impactSfx) | `MMSoundManagerTracks.Sfx` |
| `BerthaProjectilePatternDriver` (burstSfx) | `MMSoundManagerTracks.Sfx` |
| `BerthaLightAttack1Bootstrap` | config 저장만 — 실제 재생은 ProjectilePatternDriver |
| `ChobombSelfDestructController` | `MMSoundManagerTracks.Sfx` |

**전부 MMSoundManager.Sfx 트랙 사용**. Group 2 의 `GameAudioSettings.SetSfxVolume → MMSoundManager.SetTrackVolume(Sfx, ...)` 가 자동으로 모든 적 SFX 의 볼륨 통제. 추가 코드 작업 0.

남은 edge case: 각 컨트롤러의 `PlayClipAtPoint` fallback (MMSoundManager 없을 때만 트리거 — 실제 게임 씬에는 항상 MM 부트스트랩되므로 무시 가능).

## 최종 mixer 통과 현황

| 영역 | mixer / MM 경유? | 사용자 슬라이더 영향? |
|---|---|---|
| 플레이어 전투 SFX (Khi*) | ✅ MainMixer.SFX | ✅ SFX 슬라이더 |
| Phase0 인트로 BGM | ✅ MainMixer.Music (Group 1) | ✅ Music 슬라이더 |
| Phase0 타이프 SFX | ✅ MainMixer.SFX (Group 1) | ✅ SFX |
| Title / Town BGM (TDE BackgroundMusic) | ✅ MMSoundManager.Music (Group 2) | ✅ Music |
| Dungeon BGM (Stage1BgmController) | ✅ MMSoundManager.Music (Group 2) | ✅ Music |
| 모든 적 SFX (Bertha / Skeleton / Orc / OrcRider / StoneGolem / Moose / Chobomb) | ✅ MMSoundManager.Sfx (Group 2) | ✅ SFX |

→ **모든 게임 audio 가 사용자 옵션 메뉴 슬라이더 영향 받음.**

---

## 실제 구현 결과 (Implementation Details)

### 수정된 파일 목록

| 파일 | 변경 내용 |
|---|---|
| `Scripts/Runtime/Audio/GameAudioSettings.cs` | `MoreMountains.Tools` + `UnityEngine.Audio` + `UnityEngine.SceneManagement` import 추가. `ApplyToMMSoundManager(track, linear01)` 헬퍼 추가. `SetCategory` 시그니처에 `MMSoundManager.MMSoundManagerTracks` 인자 추가. `ApplyAllToMixer` 가 mixer + MMSoundManager 양쪽 적용. `OnEnable`/`OnDisable` 에 `SceneManager.sceneLoaded` 구독/해제 추가. `HandleSceneLoaded` 가 `ApplyAllToMixer()` 호출 — 안전망. |
| `Scripts/Runtime/Intro/Phase0/Phase0TimedBgm.cs` | `Start()` 에서 `audioSource.outputAudioMixerGroup = GameAudioSettings.Instance?.MusicGroup` 라우팅 추가 |
| `Scripts/Runtime/Intro/Phase0/Phase0IntroSequenceController.cs` | `OnEnable()` 에서 `bgmSource.outputAudioMixerGroup = MusicGroup` 라우팅 추가 |
| `Scripts/Runtime/Intro/Phase0/Phase0NarrationTypewriter.cs` | 신규 `Awake()` 메서드에서 `sfxSource.outputAudioMixerGroup = SfxGroup` 라우팅 추가 |

### 핵심 API 단위 확인 결과

- `MMSoundManager.SetTrackVolume(track, volume)` 는 **linear 0..1** 받음. 내부에서 `NormalizedToMixerVolume` 으로 dB 변환 후 mixer 의 exposed parameter 에 `SetFloat`. → 우리 GameAudioSettings 에서 그대로 linear 값 전달하면 됨.
- `MainMixer.SetFloat(paramName, dB)` 는 **dB** 받음. → GameAudioSettings 가 `LinearToDb(20·log10)` 변환 후 전달.
- 두 시스템은 **독립적 mixer asset** 사용 (우리 MainMixer vs MM 의 MMSoundManagerAudioMixer). 양쪽 sync 필수.

### 추가 안전망 — Audition mute leak 대비

`SfxBalanceWindow` 의 audition 기능이 MainMixer 의 `MasterVol` 을 임시로 -80dB 로 mute. 정상 흐름에서는 `StopAudition()` 이 복원하지만 **Editor 재컴파일 / 비정상 종료** 시 leak 가능성.

`GameAudioSettings.HandleSceneLoaded` 가 매 씬 전환 시 `ApplyAllToMixer()` 호출 → mixer + MMSoundManager 둘 다 PlayerPrefs 값으로 재적용. audition mute leak 있어도 다음 씬 진입 시 자동 복원.

### 디버깅 과정 메모

작업 중 한 번 "player 공격 소리 안 나옴" 의심 발생 → 진단 로그 추가 → 사용자 확인 결과 false alarm (확인 잘못). 진단 로그 제거 완료. 시스템 동작 정상.

### 검증 결과

**Group 2 검증 (사용자 확인)**:
- ✅ 옵션 메뉴 Music 슬라이더 드래그 → Title/Town/Dungeon BGM 모두 실시간 반영
- ✅ 게임 재시작 후 슬라이더 값 유지 (PlayerPrefs)
- ✅ 씬 전환 시에도 BGM 볼륨 유지

**Group 3 작업 안 함**: 코드 조사 결과 모든 적 SFX 가 이미 `MMSoundManager.MMSoundManagerTracks.Sfx` 통과 → Group 2 sync 가 자동으로 처리. 추가 작업 불필요.

### 진단 로그 정리 (선택, 출시 직전)

다음은 dev 편의용으로 남아있는 로그 — 출시 직전 제거 고려:

- `SfxBalanceWindow.cs` 의 `[SfxBalance][진단] RefreshDiscovery 종료` 로그 (Refresh 누를 때마다 1회)
- `GameAudioSettings.cs` 의 `logVolumeChanges` SerializeField (Inspector 토글, 기본 false)
- `KhiSfxBinder.cs` 의 `logSfx` SerializeField (Inspector 토글)

진단 로그는 디버그 빌드용 — Release 빌드에서는 자동 stripping 되지 않으므로 명시적 제거가 깔끔.

### 미사용 자산 정리 (빌드 사이즈)

`Assets/_Project/Audio/BGM/` 안의 4개 wav 자산이 어디서도 참조 안 됨:
- `intro_bgm.mp3`
- `maou_bgm_fantasy10.ogg`
- `maou_bgm_piano32.ogg`
- `maou_bgm_piano37.ogg`

빌드 정리 시 삭제 가능. SfxBalanceWindow 의 `(orphan)` 그룹에 표시되지 않음 — Player prefab 의 SFX 와 동일 GUID 가 아니라 별개 자산.
