# DemoSoundPanel — 사용 가이드 & 메모

> **자동 부트스트랩이라 prefab 부착 작업이 없음** — 빌드 실행 + F10 누르면 바로 패널 뜸.

## 구현 상태

- [x] 신규 컴포넌트: [LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/DemoSoundPanel.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/DemoSoundPanel.cs)
  - F10 토글 IMGUI 패널
  - 글로벌 Master/Music/SFX 슬라이더 (`GameAudioSettings` 재사용)
  - 씬 안 모든 AudioSource 자동 스캔 → 클립 단위 그룹화 → 개별 슬라이더 + Mute
  - PlayerPrefs 자동 저장 (다음 실행에도 유지)
  - `[RuntimeInitializeOnLoadMethod]` 자동 부트스트랩 (부착 작업 0)

## 단축키

| 키 | 기능 |
|---|---|
| **F10** | 패널 토글 (열기/닫기) |
| **F11** | 글로벌 mute 토글 (모든 사운드 차단 / 재개). `AudioListener.pause` 사용 — 재생 위치 유지. mute 중에는 화면 우상단에 `🔇 MUTED [F11]` 표시 |

## 사용법

### 시연 전 (튜닝)
1. 빌드 실행 → 아무 씬 (예: town_preview) 진입
2. **F10** → 패널 뜸
3. 글로벌 슬라이더로 전체 톤 잡기 (Master / Music / SFX)
4. 개별 클립 리스트에서 너무 시끄러운 / 작은 클립 슬라이더 조정 / 검색창으로 클립 이름 필터
5. 설정은 PlayerPrefs 에 즉시 저장 → 다음 실행에도 유지

### 시연 중
- **F10** 으로 패널 여닫기 (드래그로 이동 가능)
- 즉석 조정 가능 — 너무 시끄럽거나 잘못된 BGM 나오면 Mute 토글
- **F11** 한 번 → 모든 소리 즉시 차단 (예: 누가 말걸 때, 외부 소음 들어올 때). 다시 F11 → 재개. 재생 위치 유지됨

## UI 요소

| 위치 | 컨트롤 | 기능 |
|---|---|---|
| 상단 Global 박스 | 3개 슬라이더 (Master/Music/SFX) | 기존 옵션 메뉴와 동일. `GameAudioSettings` 직접 호출 |
| 검색창 | 텍스트 | 클립명 일부 입력 → 필터링 |
| Reset All 버튼 | - | 모든 클립 multiplier=1, mute=false 로 복원 (PlayerPrefs 도 갱신) |
| Per-Clip 리스트 | 각 행: 클립명 / Mute / 슬라이더 / 값 | 클립별 개별 조절. ⚠ 표시는 증폭 한계 (아래 참고) |

## ⚠ 증폭 (>1.0) 한계 — 알아둘 것

슬라이더는 **0 ~ 2.0** 까지 노출하지만 Unity `AudioSource.volume` 자체가 **[0, 1] clamp** 라 1.0 초과는 시각적 표시(`⚠`) 만 됩니다. 실효는 1.0 까지.

**진짜 증폭이 필요한 클립이 있다면** mixer asset 분리 작업이 필요해요:
1. Unity 에디터에서 `LostMemory/Assets/_Project/Audio/Resources/MainMixer.mixer` 열기
2. SFX 그룹 아래에 자식 그룹 추가 (예: "Boss SFX", "Character SFX")
3. 그 그룹을 expose (우클릭 → Expose to Script)
4. 해당 보스/캐릭터 AudioSource 의 `outputAudioMixerGroup` 을 새 그룹으로 라우팅
5. DemoSoundPanel 에 mixer group exposed parameter 조절 추가 → +dB 진짜 증폭 가능

## 동작 원리 요약

```
[F10] 토글
  ↓
ScanAudioSources (0.5초 마다)
  ↓ FindObjectsOfType<AudioSource>
  ↓ clip.name 기반 그룹화 → _entries Dictionary
  ↓
LateUpdate 매 프레임:
  src.volume = effective(multiplier, muted) * baseVolume
  ↓
OnGUI: IMGUI 패널 렌더
```

- 새 AudioSource 가 동적으로 생기면 (예: 보스 등장 시 새 SFX) **다음 스캔(0.5s 안)** 에 자동 등록
- baseVolume 은 처음 만났을 때 캐싱 (인스펙터에 설정된 원본 볼륨 보존)

## 멀티플레이 주의

`AudioSource` 는 클라이언트 독립. **호스트/게스트 각자 자기 인스턴스에서 F10 → 자기 화면 사운드만 조정**. PlayerPrefs 도 클라이언트 로컬. 화면 공유로 시연하면 한쪽만 세팅하면 됨.

## Verification (체크리스트)

1. **자동 부트스트랩**: 빌드 실행 → F10 → 패널 뜸 (부착 없이)
2. **글로벌 슬라이더**: Master/Music/SFX 조정 → 기존 옵션 메뉴와 동일 효과
3. **개별 클립 스캔**: town_preview 진입 → BGM + 캐릭터 SFX 클립명이 리스트에 등장
4. **개별 볼륨**: 특정 클립 슬라이더 0.5 → 그 클립을 쓰는 모든 AudioSource volume 감소
5. **Mute**: Mute 토글 → 즉시 무음
6. **증폭 한계**: 슬라이더 1.5 → `1.50 ⚠` 표시. 실효는 1.0 까지
7. **PlayerPrefs 저장**: 슬라이더 조정 후 게임 재시작 → 같은 값 복원
8. **검색 필터**: 클립명 일부 타이핑 → 필터링
9. **Reset All**: 모든 entry multiplier=1, mute=false 복원
10. **새 AudioSource**: 보스 등장 같은 동적 SFX → 0.5s 안에 자동 등록

## 정식 빌드 분리 전략

시연 끝나면:
- **옵션 A**: DemoSoundPanel.cs 파일 자체 삭제
- **옵션 B**: `[RuntimeInitializeOnLoadMethod]` 메서드를 `#if UNITY_EDITOR || DEMO_BUILD` 가드로 감싸기 → 출시 빌드에서 자동 부트스트랩 안 됨
- **옵션 C**: 그냥 두기 (F10 안 누르면 화면에 아무것도 안 뜸. 성능 영향 거의 없음)

F10 키가 다른 기능과 충돌하지 않는지만 확인 (현재 코드에서 F10 사용처 없음).

## PlayerPrefs 키 형식

```
DemoSoundPanel.Clip.{clipName}.Volume  (float)
DemoSoundPanel.Clip.{clipName}.Mute    (int 0/1)
```

지우려면 `PlayerPrefs.DeleteAll()` 또는 키 prefix 로 일괄 삭제.

## 향후 follow-up

- **진짜 증폭** — mixer 클립별 group 분리 (위 ⚠ 섹션 참고)
- **카테고리 그룹화** — 클립명 prefix (예: "Rena_", "Bertha_", "BGM_") 로 폴딩
- **세션 복사/공유** — 튜닝한 PlayerPrefs 값을 JSON 으로 내보내기/가져오기 → 시연 PC 간 동기화
- **출시 빌드에서도 노출** — 옵션 메뉴에 "고급 사운드" 탭으로 통합

## 관련 파일

- [AutoPotionController.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/AutoPotionController.cs) — F8 자동 포션
- [DemoItemGranter.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/TestKhi/DemoItemGranter.cs) — F9 아이템 지급
- [auto-potion-demo-todo.md](auto-potion-demo-todo.md) / [demo-item-granter-todo.md](demo-item-granter-todo.md) — 다른 시연 기능 메모
- [GameAudioSettings.cs](../../LostMemory/Assets/_Project/Scripts/Runtime/Audio/GameAudioSettings.cs) — 글로벌 볼륨 진입점 (재사용)
- [MainMixer.mixer](../../LostMemory/Assets/_Project/Audio/Resources/MainMixer.mixer) — Unity Audio Mixer asset
