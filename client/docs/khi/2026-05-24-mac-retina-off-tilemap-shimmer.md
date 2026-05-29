# Mac 빌드 타일맵 일렁임 해결 — Retina Support OFF

> 적용일: 2026-05-24
> 이미 repo 에 적용됨 (`ProjectSettings/ProjectSettings.asset` 의 `macRetinaSupport: 1 → 0`)
> 이 문서는 Mac 빌드 돌리는 팀원이 **자기 로컬에서 추가로 해야 할 일** 정리.

## 증상

Mac 빌드에서 캐릭터가 **움직일 때만 타일맵이 미세하게 일렁임** (가장자리 픽셀 깜빡깜빡). 정지하면 멈춤. Windows 빌드에선 안 보임.

## 원인

Mac Retina 디스플레이는 2x DPI (예: 2880×1800 같은 고밀도 화면). Unity 가 2x 해상도로 렌더링 → **카메라 sub-pixel 진동이 0.5픽셀 단위까지 정밀하게 sampling 됨** → 타일 경계 일렁임 가시화.

`KhiPlayerCamera` 가 `Vector3.SmoothDamp` 으로 부드럽게 캐릭터를 추적 → 카메라 위치가 매 프레임 `x=3.14159 → 3.18234` 처럼 sub-pixel 진동 → Windows 1x DPI 에선 사람 눈에 안 보이지만 Mac Retina 2x DPI 에선 보임.

## 해결 — Mac Retina Support OFF

Mac 도 Windows 와 동일하게 1x 해상도로 렌더. sub-pixel 진동은 그대로지만 사람 눈에 안 보이는 수준이 됨. **카메라 부드러움 보존**, snap stepping 없음.

### 적용 방법 (선택 — 둘 중 아무거나)

#### A. Unity Editor GUI (권장)
1. Unity 프로젝트 열기 (`client/LostMemory`)
2. 메뉴 `Edit → Project Settings...`
3. 좌측 `Player` 클릭
4. 상단 **사과 아이콘 탭** (Mac Standalone) 선택
5. `Resolution and Presentation` 섹션 펼치기
6. **`Mac Retina Support`** 체크박스 해제
7. Project 저장 (Ctrl/Cmd + S)

#### B. 파일 직접 편집 (이미 repo 에 commit 됨)
`client/LostMemory/ProjectSettings/ProjectSettings.asset` line 87:
```yaml
macRetinaSupport: 0   # 1 → 0 변경
```
Repo pull 받았으면 이미 반영됨. 추가 작업 X.

## Mac 빌드 재빌드

설정 변경 후 **반드시 Mac 빌드 다시 생성**해야 적용됨. Editor 의 Play mode 와 빌드는 별도 설정.

1. `File → Build Profiles` (또는 `File → Build Settings`)
2. Platform → Mac
3. `Build` 클릭 → 출력 위치 지정
4. 새로 빌드된 .app 실행

## 검증

빌드 실행 후:
- [ ] 캐릭터 이동 시 타일맵 일렁임 사라짐 (또는 매우 약함, Windows 수준)
- [ ] 카메라 추적 부드러움 유지 (SmoothDamp 효과 그대로)
- [ ] 화면이 살짝 흐릿/뭉개진 느낌 — **정상**. 1x 해상도 stretch 때문. 픽셀 아트 미감과 잘 맞음.

## Trade-off (알아둘 것)

| | Retina ON (이전) | Retina OFF (현재) |
|---|---|---|
| 타일 일렁임 | 보임 | 거의 안 보임 |
| 화질 또렷함 | 매우 또렷 (2x) | 살짝 흐릿 (1x stretch) |
| 카메라 부드러움 | ✓ | ✓ |

**Mac 노트북 사용자 입장**: Retina 또렷함 살짝 포기하는 대신 일렁임 해소. 픽셀 아트 게임은 1x 가 디폴트 미감이라 위화감 적음. Windows 빌드 화질이 곧 Mac OFF 화질이라 보면 됨.

## 그래도 일렁임 남으면

옵션 A 만으론 부족할 경우 (드물지만 가능):

### 옵션 B — 카메라 snap (코드 1줄 추가)
`KhiPlayerCamera.cs` 의 `LateUpdate` 마지막에:
```csharp
const float PPU = 32f; // 프로젝트 표준 (타일 sprite Inspector 확인)
pos.x = Mathf.Round(pos.x * PPU) / PPU;
pos.y = Mathf.Round(pos.y * PPU) / PPU;
transform.position = pos;
```
일렁임 100% 사라지지만 카메라가 1픽셀 step 으로 살짝 끊기듯 움직임. 부드러움 trade-off.

### 옵션 C — PixelPerfectCamera + Upscale Render Texture (시연 후 정석)
Main Camera 에 `Add Component → Pixel Perfect Camera`, `Upscale Render Texture` 옵션 ON.
저해상도 RT 에 렌더 후 화면으로 stretch → 카메라는 픽셀 격자 snap 되지만 화면엔 부드럽게 보임 (Stardew Valley 방식).
**셋팅 신중히** — PPU, Reference Resolution 잘못 잡으면 시야 변함.

## 안 보이는 곳에서 발생할 수 있는 부작용

`macRetinaSupport: 0` 변경 영향 범위:
- ✅ 코드 0
- ✅ Scene/Prefab 0
- ✅ NGO join risk 0
- ⚠️ Mac 빌드 화질만 (위에 설명)
- ⚠️ Windows/Linux/모바일 빌드 영향 0 (Mac 한정 설정)

## 참고

- 같은 폴더 `2026-05-24-guest-revive-serverrpc-plan.md` — 게스트 부활 시스템 (관련 X, 같은 날 작업)
- `feedback_unity_scene_player_audit.md` (memory) — Scene 변경 시 검증 절차
