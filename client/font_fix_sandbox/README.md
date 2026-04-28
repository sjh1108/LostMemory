# 픽셀 폰트(Galmuri9) 깨짐 수정 — Sandbox 비교

## 폴더 구조

```
font_fix_sandbox/
├── original/   # 손대지 않은 원본 (대조군)
└── fixed/      # 수정본 (변경 적용)
```

`Assets/` 바깥에 둬서 Unity가 자동 import 하지 않음.

## 비교 방법

VSCode/Cursor에서 각 파일 우클릭 → **Compare Selected** 로 두 파일 선택해 diff 확인.

또는 PowerShell에서:
```powershell
git diff --no-index original\RewardPanel.prefab fixed\RewardPanel.prefab
git diff --no-index original\PlayerHUD.prefab fixed\PlayerHUD.prefab
git diff --no-index original\TalentPanel.prefab fixed\TalentPanel.prefab
```

## 변경 요약

### 1. RewardPanel.prefab

이미 Galmuri9 사용 중. RectTransform 비정수 사이즈 + Center 정렬이 문제였음.

| 항목 | 원본 | 수정 | 개수 |
|---|---|---|---|
| RectTransform Height | `43.2` | `45` | 3 |
| RectTransform Height | `29.71` | `36` | 6 |
| TMP m_VerticalAlignment | `512` (Middle) | `256` (Top) | 9 |

**왜:**
- 비정수 Height (43.2) → 텍스트 Y 위치가 `(43.2 - 27) / 2 = 8.1` 같은 소수가 되어 픽셀 보간으로 흐려짐
- Top 정렬로 바꾸면 Y=0에서 시작해서 항상 정수 위치

### 2. PlayerHUD.prefab

이미 Galmuri9 사용 중. SizeDelta는 모두 정수였고 정렬만 수정.

| 항목 | 원본 | 수정 | 개수 |
|---|---|---|---|
| TMP m_VerticalAlignment | `512` (Middle) | `256` (Top) | 2 |

### 3. TalentPanel.prefab

가장 변경 많음. 폰트가 malgun SDF였고 사이즈도 9의 배수가 아니었음.

| 항목 | 원본 | 수정 | 개수 |
|---|---|---|---|
| Font Asset GUID | `9df3a6a0...` (malgun SDF) | `24e78672...` (Galmuri9) | 24 |
| SharedMaterial fileID + GUID | `4056146775108264783, malgun` | `2084286004769984998, Galmuri9` | 24 |
| TMP m_fontSize | `20` | `18` | 22 |
| TMP m_fontSizeBase | `20` | `18` | 22 |
| TMP m_fontSize | `24` | `27` | 1 |
| TMP m_fontSizeBase | `24` | `27` | 1 |
| TMP m_VerticalAlignment | `512` (Middle) | `256` (Top) | 18 |

**왜 사이즈 변경:**
- Galmuri9는 9px 베이스 → Font Size는 **9의 배수** (9, 18, 27, 36...) 만 픽셀 정렬됨
- 20pt = 9 × 2.22 = 비정수 스케일 → 픽셀 일그러짐
- 24pt = 9 × 2.66 = 비정수 스케일 → 픽셀 일그러짐
- 18pt(2x), 27pt(3x), 36pt(4x)로 정렬하면 정수배

### 4. Galmuri9.asset

본 sandbox에서는 변경 없음 (Unity가 처리해야 하므로). 사용자가 Unity에서 다음 작업 필요:

1. Project 창에서 `Assets/_Project/Art/Fonts/Galmuri9.asset` 선택
2. Inspector 상단 우측 **`Update Atlas Texture`** 버튼 클릭

이미 Line Height 11.7→9, Population Mode→Static, Render Mode→RASTER_HINTED, Sampling Point Size→9 설정 완료된 상태. Atlas 재생성만 누르면 됨.

## 적용 방법 (사용자가 직접 판단)

수정본 검토 후 만족하면:

### 옵션 A. 파일 통째로 덮어쓰기 (빠름, 위험)

다른 팀원이 같은 파일 안 만지고 있을 때만:
```powershell
Copy-Item fixed\RewardPanel.prefab ..\LostMemory\Assets\_Project\Prefabs\UI\ -Force
Copy-Item fixed\PlayerHUD.prefab ..\LostMemory\Assets\_Project\Prefabs\UI\ -Force
Copy-Item fixed\TalentPanel.prefab ..\LostMemory\Assets\_Project\Prefabs\UI\ -Force
```

### 옵션 B. Unity Inspector에서 수동 입력 (안전)

각 prefab을 Unity에서 열어서:
- 변경된 RectTransform Width/Height 값 직접 입력
- TMP_Text의 Vertical Alignment 클릭 (Middle → Top)
- TalentPanel의 모든 TMP_Text:
  - Font Asset 슬롯에 Galmuri9 드래그
  - Font Size 20 → 18, 24 → 27 변경
  
실수 가능성 있지만 다른 팀원 변경과 충돌 안 남.

## 적용 후 검증 (Unity에서)

1. **Galmuri9.asset → Update Atlas Texture** 버튼 누르기
2. **Windows 디스플레이 배율 100%** 인지 확인 (시스템 설정)
3. Test_Reward 씬 열어서 Play 모드 진입
4. **Game 뷰 Scale = 1x** 로 두기 (Maximize on Play 켜는 게 가장 정확)
5. 다음 항목 모두 또렷하게 픽셀 폰트로 보이는지:
   - "보상을 선택하세요" 헤더
   - "유물 이름" (노란색)
   - "[등급 태그]"
   - "유물 설명이 들어가는 곳입니다" (멀티라인)
6. 영문 "New", "abc" 등 들어간 곳에서 글자 식별 가능한지

## 그래도 깨질 때

만약 위 작업 후에도 깨지면 원인이 다른 곳:

1. **Windows 배율 != 100%** → 가장 흔한 원인. 변경 후 Unity 재시작.
2. **Game 뷰 Scale != 1x** → 슬라이더 정확히 1.0으로
3. **Canvas Scaler가 다름** → Constant Pixel Size + Scale Factor 1 인지
4. **Camera Anti-aliasing** → 끄기 (URP Asset에서 MSAA Disabled)
5. **부모 RectTransform이 비정수 Position** → VerticalLayoutGroup의 Spacing/Padding 정수인지

## 파일별 라인 매핑 (참고)

원본 RewardPanel.prefab의 변경 라인:
- Line 38, 176, 686: SizeDelta y: 43.2 → 45
- Line 548, 1015, 1611, 1749, 1887, 2025: SizeDelta y: 29.71 → 36
- Line 104, 242, 614, 752, 1081, 1677, 1815, 1953, 2091: VerticalAlignment 512 → 256

원본 PlayerHUD.prefab의 변경 라인:
- Line 104, 1276: VerticalAlignment 512 → 256

원본 TalentPanel.prefab의 변경 라인:
- 24개 fontAsset/sharedMaterial 블록 (line 70-71, 208-209, 등)
- 22개 fontSize 20 → 18, 22개 fontSizeBase 20 → 18
- 1개 fontSize 24 → 27, 1개 fontSizeBase 24 → 27
- 18개 VerticalAlignment 512 → 256
