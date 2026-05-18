# Ancient Ruins → Old Prison 룸 변환 규칙

`Modules/1F1R/Map_1F_1R_1_S_EP.prefab` 을 `Modules/testMap/Map_1F_1R_1_S_EP_OldPrison.prefab` 으로 변환했을 때 사용한 규칙. 다른 방에도 같은 방식 적용 가능.

> **적용 대상**: Tilemap_wall + Tilemap_floor 만 (시각적으로 확인됨)
> **제외**: Tilemap_deco, object_deco — 결과 부자연스러워서 사용 안 함

---

## 1. 핵심 아이디어

원본 prefab의 wall/floor는 Ancient Ruins 시트(`Tileset-Terrain2.png`)에서 슬라이스한 **개별 Tile asset 다수**로 모자이크되어 있음. 이를 Old Prison의 **RuleTile 1종씩**으로 일괄 교체.

- **벽**: 어떤 종류의 벽 타일이든 → `wall1-platform-full tile rnd` RuleTile 하나로
- **바닥**: 어떤 종류의 바닥 타일이든 → `full tile-pit-ground` RuleTile 하나로

RuleTile이 인접 셀을 보고 코너·가장자리를 자동으로 골라 그려주므로 결과가 깔끔.

---

## 2. 사용한 상수

### 대체 타일 (모든 방에서 동일)

| 용도 | Asset 경로 | Tile asset GUID | 기본 sprite fileID | 시트 GUID |
|---|---|---|---|---|
| **벽** | `Assets/RafaelMatos/ERW - Old Prison/Tilesets/Rule tile/wall1-platform-full tile rnd.asset` | `6be5c88d6b5d686489c71c4d3a3e53db` | `549190229` | `60abcdcb36e579e48980c724133e13db` |
| **바닥** | `Assets/RafaelMatos/ERW - Old Prison/Tilesets/Rule tile/full tile-pit-ground.asset` | `62f6b94d8d967a245acc6a77dbbfe5de` | `-35823755` | `21a9874326d7b3d4ab2657258de66a61` |

### 원본 시트

- Ancient Ruins `Tileset-Terrain2.png`: GUID `a4deed4a3628fc24b9e06bc1b3cd7876`

---

## 3. 변환 절차

### 3-1. 새 prefab 만들기

```bash
# testMap 폴더 옆에 새 폴더 생성 (예: testRoom2)
mkdir -p "Assets/_Project/Map/Modules/<새폴더>"
cp "Assets/_Project/Map/Modules/<원본경로>/<원본>.prefab" \
   "Assets/_Project/Map/Modules/<새폴더>/<새이름>.prefab"
```

`.prefab.meta` 도 만들어야 함 — 새 GUID로:

```bash
# PowerShell에서 새 GUID 생성:  [System.Guid]::NewGuid().ToString('N')
```

그리고 새 GUID로 `.prefab.meta` 작성:
```yaml
fileFormatVersion: 2
guid: <새 GUID>
PrefabImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

> ⚠️ **CRLF 주의**: Windows에서 PowerShell이나 Write tool로 파일 만들면 CRLF 들어감. Unity가 GUID 못 읽음. 항상 `tr -d '\r'` 로 LF로 통일.

### 3-2. 원본 prefab의 wall/floor 타일 정보 추출

각 prefab은 자기만의 unique Tile GUID 집합을 씀. 직접 추출 필요.

```bash
PREFAB="<원본 prefab 경로>"

# Tilemap_wall, Tilemap_floor 의 m_TileAssetArray 라인 번호 찾기
grep -n "m_Name: Tilemap_wall\|m_Name: Tilemap_floor\|m_TileAssetArray" "$PREFAB"
```

각 Tilemap 섹션의 `m_TileAssetArray` 에서 `m_RefCount: N > 0` 인 entry의 GUID를 추출.
`m_TileSpriteArray` 에서는 `fileID` 와 `guid` (시트 GUID = `a4deed4a3628fc24b9e06bc1b3cd7876`) 추출.

### 3-3. sed 치환 스크립트 생성

각 unique GUID/fileID에 대해 두 종류 치환:

**Tile asset GUID 치환** (wall 들 → wall RuleTile, floor 들 → floor RuleTile):
```
s|guid: <원본_타일_GUID>, type: 2|guid: <대체_RuleTile_GUID>, type: 2|g
```

**Sprite 참조 치환**:
```
s|fileID: <원본_sprite_fileID>, guid: a4deed4a3628fc24b9e06bc1b3cd7876, type: 3|fileID: <RuleTile_기본_sprite_fileID>, guid: <RuleTile_시트_GUID>, type: 3|g
```

### 3-4. 적용 + CRLF 제거

```bash
sed -i -f patch.sed "$PREFAB"
tr -d '\r' < "$PREFAB" > "$PREFAB.tmp" && mv "$PREFAB.tmp" "$PREFAB"
```

### 3-5. 검증

```bash
# 원본 Ancient Ruins 타일 GUID가 다 사라졌는지 확인 (0이어야 함)
grep -c "guid: a4deed4a3628fc24b9e06bc1b3cd7876" "$PREFAB"  # 0

# 대체 RuleTile 참조가 들어갔는지
grep -c "guid: 6be5c88d6b5d686489c71c4d3a3e53db, type: 2" "$PREFAB"  # wall 슬롯 수
grep -c "guid: 62f6b94d8d967a245acc6a77dbbfe5de, type: 2" "$PREFAB"  # floor 슬롯 수 (보통 1)
```

---

## 4. 자동화 스크립트 (참고)

아래 스크립트를 `apply_oldprison.sh` 같은 이름으로 저장 후 실행하면 한 번에 처리됨.

```bash
#!/bin/bash
set -euo pipefail

# === 설정 ===
PREFAB="$1"   # 변환할 prefab 경로

SRC_SHEET="a4deed4a3628fc24b9e06bc1b3cd7876"
WALL_RT_GUID="6be5c88d6b5d686489c71c4d3a3e53db"
WALL_RT_SPRITE_FID="549190229"
WALL_RT_SHEET="60abcdcb36e579e48980c724133e13db"
FLOOR_RT_GUID="62f6b94d8d967a245acc6a77dbbfe5de"
FLOOR_RT_SPRITE_FID="-35823755"
FLOOR_RT_SHEET="21a9874326d7b3d4ab2657258de66a61"

# === Tilemap_wall, Tilemap_floor 의 m_TileAssetArray 라인 범위 찾기 ===
# (수동으로 라인 번호 입력하거나 awk로 자동 추출)
WALL_START=$(grep -n "m_Name: Tilemap_wall$" "$PREFAB" | cut -d: -f1)
FLOOR_START=$(grep -n "m_Name: Tilemap_floor" "$PREFAB" | cut -d: -f1)

# Tilemap_wall 의 m_TileAssetArray, m_TileSpriteArray 라인
WALL_ARR=$(awk -v s="$WALL_START" 'NR>s && /^  m_TileAssetArray:/ {print NR; exit}' "$PREFAB")
WALL_SPR=$(awk -v s="$WALL_START" 'NR>s && /^  m_TileSpriteArray:/ {print NR; exit}' "$PREFAB")
WALL_MTX=$(awk -v s="$WALL_START" 'NR>s && /^  m_TileMatrixArray:/ {print NR; exit}' "$PREFAB")

FLOOR_ARR=$(awk -v s="$FLOOR_START" 'NR>s && /^  m_TileAssetArray:/ {print NR; exit}' "$PREFAB")
FLOOR_SPR=$(awk -v s="$FLOOR_START" 'NR>s && /^  m_TileSpriteArray:/ {print NR; exit}' "$PREFAB")
FLOOR_MTX=$(awk -v s="$FLOOR_START" 'NR>s && /^  m_TileMatrixArray:/ {print NR; exit}' "$PREFAB")

# === unique 타일 GUID, sprite fileID 추출 ===
WALL_TILE_GUIDS=$(awk -v s="$WALL_ARR" -v e="$WALL_SPR" 'NR>s && NR<e && /m_Data:.*guid:/ {match($0, /guid: ([a-f0-9]{32})/, a); print a[1]}' "$PREFAB" | sort -u)
WALL_SPRITE_FIDS=$(awk -v s="$WALL_SPR" -v e="$WALL_MTX" -v sg="$SRC_SHEET" 'NR>s && NR<e && /m_Data:.*fileID:/ && index($0, sg) {match($0, /fileID: (-?[0-9]+)/, a); print a[1]}' "$PREFAB" | sort -u)

FLOOR_TILE_GUIDS=$(awk -v s="$FLOOR_ARR" -v e="$FLOOR_SPR" 'NR>s && NR<e && /m_Data:.*guid:/ {match($0, /guid: ([a-f0-9]{32})/, a); print a[1]}' "$PREFAB" | sort -u)
FLOOR_SPRITE_FIDS=$(awk -v s="$FLOOR_SPR" -v e="$FLOOR_MTX" -v sg="$SRC_SHEET" 'NR>s && NR<e && /m_Data:.*fileID:/ && index($0, sg) {match($0, /fileID: (-?[0-9]+)/, a); print a[1]}' "$PREFAB" | sort -u)

# === sed 스크립트 생성 ===
SED=$(mktemp)
for g in $WALL_TILE_GUIDS;    do echo "s|guid: ${g}, type: 2|guid: ${WALL_RT_GUID}, type: 2|g" >> "$SED"; done
for f in $WALL_SPRITE_FIDS;   do echo "s|fileID: ${f}, guid: ${SRC_SHEET}, type: 3|fileID: ${WALL_RT_SPRITE_FID}, guid: ${WALL_RT_SHEET}, type: 3|g" >> "$SED"; done
for g in $FLOOR_TILE_GUIDS;   do echo "s|guid: ${g}, type: 2|guid: ${FLOOR_RT_GUID}, type: 2|g" >> "$SED"; done
for f in $FLOOR_SPRITE_FIDS;  do echo "s|fileID: ${f}, guid: ${SRC_SHEET}, type: 3|fileID: ${FLOOR_RT_SPRITE_FID}, guid: ${FLOOR_RT_SHEET}, type: 3|g" >> "$SED"; done

# === 적용 + CRLF 제거 ===
sed -i -f "$SED" "$PREFAB"
tr -d '\r' < "$PREFAB" > "$PREFAB.tmp" && mv "$PREFAB.tmp" "$PREFAB"

# === 검증 ===
echo "원본 시트 GUID 잔존: $(grep -c "guid: $SRC_SHEET" "$PREFAB") (0이어야 함)"
echo "Wall RuleTile 참조: $(grep -c "guid: $WALL_RT_GUID, type: 2" "$PREFAB")"
echo "Floor RuleTile 참조: $(grep -c "guid: $FLOOR_RT_GUID, type: 2" "$PREFAB")"

rm -f "$SED"
echo "완료: $PREFAB"
```

**사용법**:
```bash
# 1. 원본 prefab을 새 경로에 복사 (.meta 도 새 GUID로 직접 만들기)
cp Assets/_Project/Map/Modules/1F1R/Map_1F_1R_3_NE.prefab \
   Assets/_Project/Map/Modules/testMap_NE/Map_1F_1R_3_NE_OldPrison.prefab
# (.meta는 별도로 새 GUID로 작성 — 위 3-1 참고)

# 2. 변환 스크립트 실행
bash apply_oldprison.sh "Assets/_Project/Map/Modules/testMap_NE/Map_1F_1R_3_NE_OldPrison.prefab"

# 3. Unity Refresh (Ctrl+R)
```

---

## 5. 한계 / 주의 사항

| 항목 | 비고 |
|---|---|
| **원본이 다른 시트를 쓰는 경우** | 이 규칙은 Ancient Ruins `Tileset-Terrain2.png` 기반 prefab에만 적용. 다른 시트 쓰는 prefab은 별도 매핑 필요 |
| **`Tilemap_wall_invisible`** | 충돌 전용 (sprite 무관). 건드릴 필요 없음 |
| **`Tilemap_deco`, `object_deco`** | 자동 변환이 시각적으로 부자연스러워서 제외. 필요하면 Unity에서 Tile Palette로 직접 |
| **CRLF** | Windows 환경에서 가장 흔한 함정. 반드시 `tr -d '\r'` 처리 |
| **Connection / Door SpriteRenderer** | 별도 sheet(`26c496b3a85fead4aaa41bd7ea271ffa`) 사용. 건드리지 않아도 됨 |
| **wall_for_object Tilemap** | 별도 sheet 사용. 건드리지 않음 |

---

## 6. 적용 사례

- ✅ `Map_1F_1R_1_S_EP.prefab` → `testMap/Map_1F_1R_1_S_EP_OldPrison.prefab` — wall/floor 변환 성공
