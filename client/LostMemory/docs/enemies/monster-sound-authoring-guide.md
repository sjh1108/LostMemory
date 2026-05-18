# 몬스터 사운드 작업 가이드

## 문서 목적

이 문서는 몬스터 SFX를 추가하거나 수정할 때 참고하는 작업 기준이다.

- 작성일: 2026-05-12
- 대상: 일반 몬스터, 보스 몬스터, 투사체, 패턴 전조, 피격/사망 사운드
- 목표: 사운드 트리거 위치는 상황에 맞게 고르고, 실제 출력은 가능하면
  `MMSoundManager`의 `Sfx` 트랙으로 통일한다.

## 현재 기준 구조

프로젝트의 사운드 설정 UI는 `MMSoundManager` 트랙 볼륨을 조절한다. 따라서 새로
추가하는 몬스터 SFX는 기본적으로 `Sfx` 트랙으로 보내야 한다.

```text
MMSoundManager
-> MMSoundManager.MMSoundManagerTracks.Sfx
-> SettingsPanelView SFX slider
```

현재 참고 구현은 `Orc_CL037` 공격 휘두르기 사운드다.

```text
Assets/_Project/Prefabs/Enemies/Orc_CL037.prefab
Assets/_Project/Art/Animations/Enemies/Orc/Orc_CL037.controller
Assets/_Project/Art/Animations/Enemies/Orc/Orc_CL037_Attack.anim
Assets/_Project/Scripts/Runtime/Combat/AnimationEventSfxRelay.cs
Assets/_Project/Prefabs/Weapons/OrcMeleeWeapon4Dir_OrcCL037.prefab
```

현재 Orc 공격 사운드 흐름:

```text
Attack 애니메이션 프레임
-> LM_AttackSwingSfx
-> AnimationEventSfxRelay
-> MMSoundManager Sfx 트랙
```

`OrcMeleeWeapon4Dir_OrcCL037.prefab`는 `Orc_CL037`만 공유 무기 프리팹에서
분리하기 위해 존재한다. 실제 휘두르기 사운드는 현재 `WeaponUsedMMFeedback`이
아니라 애니메이션 이벤트에서 재생한다.

## 트리거 선택 기준

모든 사운드를 한 방식으로 처리하지 않는다. 사운드의 의미에 따라 트리거 위치를
나눈다.

| 사운드 종류 | 권장 트리거 | 이유 |
|---|---|---|
| 무기 휘두르기 | Animation Event | 특정 모션 프레임과 맞아야 함 |
| 발소리 | Animation Event | 발이 닿는 프레임과 맞아야 함 |
| 착지음 | Animation Event 또는 이동 Ability 이벤트 | 착지 타이밍과 맞아야 함 |
| 실제 피격음 | 데미지/히트 판정 이벤트 | 맞았을 때만 나야 함 |
| 사망음 | `Health.OnDeath`, Death 상태, Death Feedback | 확정 사망 시 1회만 나야 함 |
| 투사체 발사음 | 공격 코드 또는 투사체 생성 코드 | 실제 발사 시점과 맞아야 함 |
| 투사체 충돌음 | 투사체 충돌 코드 | 실제 충돌 시점과 맞아야 함 |
| 패턴 전조음 | AI 패턴 상태 진입 | 경고가 시작될 때 나야 함 |

정리하면, 모션 타이밍 사운드는 애니메이션 쪽에 두고, 게임플레이 결과 사운드는
코드 이벤트 쪽에 둔다.

## 출력 API 기준

신규 몬스터 SFX는 기본적으로 `MMSoundManager`를 사용한다.

간단한 호출:

```text
MMSoundManagerSoundPlayEvent.Trigger(clip, MMSoundManagerTracks.Sfx, position)
```

옵션이 필요한 호출:

```text
MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
options.Location = transform.position;
options.Volume = volume;
options.Pitch = pitch;

MMSoundManagerSoundPlayEvent.Trigger(clip, options);
```

새 몬스터 작업에서 `AudioSource.PlayClipAtPoint`를 직접 쓰는 것은 피한다. 이
방식은 프로젝트의 SFX 볼륨 흐름을 우회할 수 있다. 단, `MMSoundManager`가 없는
예외 상황의 fallback이나 아직 정리되지 않은 레거시 코드는 별도로 판단한다.

## 공유 에셋 안전 규칙

몬스터 사운드를 수정하기 전에 해당 에셋이 공유인지 먼저 확인한다.

공유 가능성이 높은 에셋:

```text
AnimatorController
AnimationClip
Weapon prefab
MMFeedbacks object
Audio mixer / sound manager setting
```

특정 몬스터 1종에만 적용해야 한다면 다음 순서를 지킨다.

1. 공유 컨트롤러, 애니메이션 클립, 무기 프리팹인지 확인한다.
2. 몬스터 전용 변경이 필요하면 `_Project` 아래에 복제한다.
3. 복제본 이름에 몬스터 이름이나 CL 번호를 넣는다.
4. 대상 몬스터 프리팹만 복제본을 참조하게 한다.
5. 원본 공유 에셋에 git diff가 없는지 확인한다.

외부 에셋 원본은 직접 수정하지 않는다. 외부 사운드팩의 `.wav`는 참조만 하고,
프로젝트별 연결은 `Assets/_Project` 아래 프리팹, 애니메이션, 스크립트에서 처리한다.

## 네이밍 기준

소유자와 이벤트 의미가 드러나게 이름을 짓는다.

권장 예시:

```text
Orc_CL037_Attack.anim
Orc_CL037.controller
OrcMeleeWeapon4Dir_OrcCL037.prefab
SkeletonArcher_CL041_FireSfx
Bertha_LightProjectileTelegraphSfx
```

애니메이션 이벤트 함수명은 에셋 패키지 함수와 충돌하지 않게 프로젝트 접두어를
붙인다.

```text
LM_AttackSwingSfx
LM_FootstepSfx
LM_ProjectileFireSfx
```

## Animation Event 방식

무기 휘두르기, 발소리, 착지음처럼 프레임 타이밍이 중요한 사운드에 사용한다.

작업 순서:

1. 몬스터가 실제로 사용하는 애니메이션 클립을 찾는다.
2. 해당 클립이 다른 몬스터와 공유되는지 확인한다.
3. 몬스터 전용 사운드라면 클립을 복제한다.
4. 원하는 프레임에 Animation Event를 추가한다.
5. 이벤트를 받을 컴포넌트를 애니메이션 이벤트가 도달하는 GameObject에 붙인다.
6. 프리팹에서 `AudioClip`을 할당한다.
7. 공격 반복, 공격 중단, 사망 중단 상황을 Play Mode에서 확인한다.

공격 휘두르기 사운드는 보이는 스윙 시작 지점 근처에 둔다. 이벤트가 너무 뒤에
있으면 공격 모션은 보였는데 이벤트 프레임까지 도달하지 못해 소리가 빠질 수 있다.

현재 Orc 기준:

```text
Orc_CL037_Attack.anim
event time: 0.083333336
function: LM_AttackSwingSfx
```

## 코드 이벤트 방식

피격, 사망, 투사체, 보스 패턴처럼 실제 게임플레이 결과와 연결된 사운드에 사용한다.

권장 이벤트 소스:

```text
Health.OnHit
Health.OnDeath
DamageOnTouch hit callback 또는 프로젝트 hit confirm 이벤트
Projectile spawn code
Projectile impact code
AIBrain state entry
Boss pattern controller event
```

실제 피격음은 공격 애니메이션에서 재생하지 않는다. 빗나간 공격에서는 피격음이
나면 안 되므로, 데미지 적용 또는 히트 확정 흐름에서 재생한다.

## MMFeedbacks 방식

사운드가 카메라 흔들림, 히트 플래시, 무기 사용 피드백, 사망 연출과 같이 묶여야
한다면 `MMFeedbacks`를 사용할 수 있다.

사용하기 좋은 경우:

- 사운드가 여러 피드백 중 하나다.
- 정확한 애니메이션 프레임 타이밍이 중요하지 않다.
- Feedback object가 대상 프리팹 소유이거나 안전한 복제본이다.

피해야 하는 경우:

- 특정 프레임에 반드시 맞아야 한다.
- Feedback object가 여러 몬스터에서 공유된다.
- SFX 볼륨 슬라이더를 따르는지 확실하지 않다.

## 오디오 클립 선택 기준

사운드 선택 시 우선순위:

1. 이미 프로젝트에 들어온 클립을 먼저 찾는다.
2. 파일이 실제로 존재하고 Unity에서 import 되었는지 확인한다.
3. `.meta` GUID를 확인하고 serialized reference를 연결한다.
4. 새 `.wav`를 추가한다면 Git LFS 대상인지 확인한다.

현재 저장소 기준:

```text
*.wav is tracked by Git LFS from the repository .gitattributes.
```

외부 사운드팩 파일은 원본 `.wav`나 import setting을 직접 바꾸지 않는다. 필요한
경우 프로젝트 소유 프리팹이나 스크립트에서 참조만 연결한다.

## 특정 몬스터 1종만 적용하는 체크리스트

요청이 "이 몬스터만 적용"이라면 아래를 확인한다.

1. 대상 몬스터 프리팹을 찾는다.
2. 대상 프리팹의 Animator Controller를 찾는다.
3. 공격/피격/사망 상태에서 실제로 쓰는 AnimationClip을 찾는다.
4. `CharacterHandleWeapon`을 쓰는 적이면 무기 프리팹도 확인한다.
5. 각 파일이 다른 몬스터에서도 참조되는지 확인한다.
6. 공유 파일이면 복제본을 만든다.
7. 대상 몬스터만 복제본을 참조하게 한다.
8. 사운드 연결을 추가한다.
9. 원본 공유 파일에 의도치 않은 변경이 없는지 확인한다.

확인에 자주 쓰는 명령:

```text
git status --short
grep -RIn "<guid>" Assets/_Project
git diff --name-only
```

`rg`가 있으면 `grep`보다 우선 사용한다. 현재 작업 환경에는 `rg`가 없을 수 있다.

## Unity 테스트 체크리스트

몬스터 SFX를 추가한 뒤 Unity에서 확인한다.

1. 프리팹을 열고 Missing Script가 없는지 확인한다.
2. 대상 `AudioClip` 필드가 할당되어 있는지 확인한다.
3. Animation Event 함수명이 receiver method와 정확히 일치하는지 확인한다.
4. 몬스터가 스폰되는 씬에서 Play Mode로 진입한다.
5. 사운드가 나는 행동을 5회 이상 반복한다.
6. 공격 중단, 피격 중단, 사망 중단 상황도 확인한다.
7. SFX 슬라이더를 움직여 볼륨이 같이 변하는지 확인한다.
8. Console에 Animation Event warning이 없는지 확인한다.
9. 같은 원본을 쓰던 다른 몬스터의 동작이 바뀌지 않았는지 확인한다.

## 문제 해결

### 공격 모션은 보였는데 소리가 가끔 안 난다

가능한 원인:

- Animation Event가 너무 뒤에 있다.
- 공격 상태가 이벤트 프레임 전에 끊긴다.
- 런타임 컨트롤러가 수정한 클립을 사용하지 않는다.
- receiver component가 이벤트를 받을 수 없는 오브젝트에 붙어 있다.
- Animation Event 함수명에 오타가 있다.

해결:

- 이벤트를 더 앞 프레임으로 옮긴다.
- 실제 스윙이 여러 번 있는 클립이면 필요한 프레임에만 이벤트를 추가한다.
- 컨트롤러 상태가 수정한 클립을 참조하는지 확인한다.
- 대상 프리팹에 receiver component가 활성화되어 있는지 확인한다.

### 다른 몬스터에서도 소리가 난다

가능한 원인:

- 공유 controller, animation clip, weapon prefab, feedback object를 수정했다.

해결:

- 본인이 만든 의도치 않은 공유 변경만 되돌린다.
- 공유 에셋을 복제한다.
- 대상 몬스터만 복제본을 참조하게 한다.

### SFX 슬라이더가 먹지 않는다

가능한 원인:

- `AudioSource`로 직접 재생했다.
- `MMSoundManager` 바깥 경로로 재생했다.
- 예상과 다른 mixer 또는 track을 사용했다.

해결:

- 신규 SFX는 `MMSoundManagerTracks.Sfx`로 보낸다.
- 직접 `AudioSource` 재생은 fallback 용도로만 둔다.

### Console에 Animation Event warning이 나온다

가능한 원인:

- receiver method가 없다.
- receiver method가 `public`이 아니다.
- Animation Event parameter와 method signature가 맞지 않는다.
- Unity가 이벤트를 보내는 오브젝트에 receiver component가 없다.

해결:

- 단순한 `public` method를 사용한다.
- 함수명을 안정적으로 유지한다.
- receiver component 위치를 애니메이션 오브젝트 기준으로 다시 확인한다.

## 커밋 전 확인

커밋 전 확인할 것:

1. `git status --short`로 변경 파일을 확인한다.
2. 사운드 작업과 무관한 dirty file을 분리해서 인지한다.
3. 외부 에셋 원본을 수정하지 않았는지 확인한다.
4. 의도치 않은 공유 `.controller`, `.anim`, `.prefab`, `.asset`, `.meta` 변경이
   없는지 확인한다.
5. 새 `.wav`가 있다면 LFS 추적 대상인지 확인한다.
6. Unity serialized file을 수정했다면 최종 전달 시 파일과 리스크를 언급한다.

## 권장 방향

앞으로 몬스터 사운드는 아래 구조를 기본 방향으로 잡는다.

```text
모션 타이밍 사운드
-> Animation Event
-> 작은 receiver component
-> MMSoundManager Sfx track

게임플레이 결과 사운드
-> Health / Damage / Projectile / AI Pattern event
-> 전용 presenter 또는 feedback
-> MMSoundManager Sfx track
```

트리거 위치는 사운드 종류에 따라 달라질 수 있다. 하지만 실제 출력 경로는
`MMSoundManager`로 통일해야 볼륨, 음소거, 씬 전환 이후 동작을 예측하기 쉽다.
