# CL-227 V2 카드 뽑기 — 사용법 (요약)

작성일: 2026-05-16

> 카드 1장 뽑기 = 100G 입장료. 결과는 *유지 / 증가 / 감소* 셋 중 하나 (1/3 보장).

---

## 1. 셋업 (한 번만)

### 메뉴 실행

씬 (예: `Dungeon_1F_Shop_testkhi.unity`) 열고 상단 메뉴:

```
LostMemory → Card Draw → Sync In Active Scene
```

자동 생성 (씬 안):
- `Canvas/CardDrawPanel` — 패널 (3 카드 슬롯 + Skip / X 버튼 + 골드/info/result 텍스트)
- `CardDrawController` — 매니저 (씬 싱글톤)
- `CardDrawNpc` — F 키 trigger NPC (Player 좌측 3 unit 자동 배치)

기존 `ShopController` 의 player ref (Aim/Movement/Combo/Dash/Parry/Weapon/Wallet/Inventory) 자동 복사.

### NPC 위치 조정

`CardDrawNpc` 를 원하는 위치로 드래그.

### Prefab 화 (선택, 권장)

같은 메뉴 안:
```
LostMemory → Card Draw → Save As Prefabs
```

→ `Assets/_Project/Prefabs/NPC/` 에 `CardDrawNpc.prefab` + `CardDrawPanel.prefab` 저장 + 씬 인스턴스가 prefab 에 연결됨.

---

## 2. Play 흐름

1. Player 가 `CardDrawNpc` trigger 안 진입 → 콘솔 "Player in range"
2. **F** 누름 → 패널 등장
   - 카드 3장이 **좁은 측면에서 정면으로 펼쳐지며 등장** (entrance flip ~0.2s)
3. 카드 1장 클릭 → -100G + **flip 애니메이션** (정면 → 좁음 → 다른 카드 정면)
   - 다른 2 카드도 동시에 reveal (선택지 공개 — 도박 긴장감)
   - 결과 토스트 표시 ("증가! (Net +150G)" 등)
   - 골드 변동 즉시 적용 (회수액 = 0 / 100 / 250)
4. **패널 유지** — 결과 확인 후 X 또는 ESC 로 닫기
5. 패널 닫힘 → 출구 해제 → 다음 방으로

---

## 3. 결과 분포

| 결과 | 회수액 | Net | 확률 |
|---|---|---|---|
| 유지 | 100G | 0 | 1/3 |
| 증가 | 250G | **+150** | 1/3 |
| 감소 | 0G | **-100** | 1/3 |

기댓값 ≈ +16.67G/픽 (살짝 player favorable).

**조정 방법**: `CardDrawConfig_Default.asset` 의 keepOutcome / increaseOutcome / decreaseOutcome 의 ReturnGold 값 수정.

---

## 4. 닫기 옵션

| 입력 | 동작 |
|---|---|
| **F** (다시) | controller.IsOpen 이면 Close, 아니면 Open |
| **ESC** | Close + 방 클리어 (픽 안 했어도 OK) |
| **X 버튼** (우측 상단) | ESC 동일 |
| **건너뛰기** (하단, 픽 전만) | Close + 방 클리어 (골드 변화 X) |

> 한 번 픽하면 NPC `_consumed=true` — 같은 방에서 재진입 불가. *1회성 이벤트*.

---

## 5. 다른 자판기 만들기 (config 변경)

NPC prefab 은 하나면 충분. **config SO 만 새로 만들면 다른 카드 뽑기**:

1. Project 창 → **Create → LostMemory → Events → Card Draw Config**
2. 이름 예: `CardDrawConfig_HighStakes.asset`
3. Inspector:
   - **Entry Cost**: 500
   - **Keep**: ReturnGold = 500 (본전)
   - **Increase**: ReturnGold = 1500 (×3)
   - **Decrease**: ReturnGold = 0 (전액 손실)
4. 새 NPC 인스턴스의 `Config` 슬롯에 wire

→ 같은 NPC prefab, 다른 config = 다른 자판기

---

## 6. 카드 디자인 변경

### 다른 카드 뒷면 (Card B/C/D)

1. `CardDrawUIBuilder.cs` 의 `BackSpriteBase` 상수 변경:
   ```csharp
   private const string BackSpriteBase = "Assets/2D Pixel Quest Vol.3 - The UI-GUI/Sprites PNG/Skill Cards- Flip Animations/Back Face Flip/Back Face Flip B/F_U_CardB_Back_Flip";
   ```
2. Sync 메뉴 재실행

### 다른 앞면 색 (Blue/Green/Red 등)

1. `FrontSpriteBase` 상수 변경:
   ```csharp
   private const string FrontSpriteBase = "Assets/.../F_U_Blank Red Card_Flip";
   ```
2. Sync 메뉴 재실행

### Sprite 직접 wire

코드 안 건드리고 prefab 에서 직접 sprite 슬롯 (`backFrames[]`, `frontFrames[]`) 에 다른 sprite 드래그도 가능.

---

## 7. 트러블슈팅

| 증상 | 원인 / 해결 |
|---|---|
| 메뉴 안 보임 | Console 컴파일 에러 확인 → 해결 → Refresh (Ctrl+R) |
| F 무반응 | Player tag 매칭 실패 / BoxCollider2D isTrigger 꺼짐 |
| Console "씬에 CardDrawController 가 없음" | Sync 메뉴 실행 안 함 → 다시 실행 |
| 카드가 Flip04 (좁은) 로 시작 | 옛 prefab 캐시. Sync 메뉴 재실행 |
| 카드 클릭해도 골드 안 변함 | `CardDrawConfig_Default.asset` 의 ReturnGold 값 확인 |
| Skip 버튼 안 보임 | 이미 픽 후엔 정상 (Skip 은 픽 전 전용 UI). X 버튼으로 닫기 |
| 골드 부족인데 카드 클릭됨 | PanelView affordability check 확인 |

---

## 8. 워크플로우 요약

```
디자인 단계 (1회):
  Sync 메뉴 → 위치 조정 → Save As Prefabs

이후 운영:
  Prefab 더블클릭 → 시각 수정 → Ctrl+S
  (시각 외 코드/로직 변경은 자동 반영)

새 자판기 추가:
  새 Config SO → NPC prefab 인스턴스의 config 슬롯에 wire
```
