# BuildSets — CL-138 신규 폴더

`BuildSetData` 인스턴스 16개를 이 폴더에 생성합니다. `BuildManager`(CL-139) 가 이 SO들을 로드해 보유 아이템 듀얼 태그 카운트 → 도달 티어 효과를 적용합니다.

## Unity Editor 작업 (수동)

Project 창 → 우클릭 → **Create > LostMemory > Build Set Data** 로 16개 인스턴스 생성:

| 파일명 | SetTag | DisplayName |
|---|---|---|
| `BuildSet_공속.asset` | AttackSpeed | 공속 |
| `BuildSet_치명타.asset` | Critical | 치명타 |
| `BuildSet_일반뎀.asset` | AttackPower | 일반뎀 |
| `BuildSet_얼음.asset` | Ice | 얼음 |
| `BuildSet_전기.asset` | Lightning | 전기 |
| `BuildSet_쿨감.asset` | Cooldown | 쿨감 |
| `BuildSet_불.asset` | Fire | 불 |
| `BuildSet_바람.asset` | Wind | 바람 |
| `BuildSet_미소녀.asset` | MagicalGirl | 미소녀 |
| `BuildSet_체력.asset` | Health | 체력 |
| `BuildSet_방어력.asset` | Defense | 방어력 |
| `BuildSet_회피.asset` | Dodge | 회피 |
| `BuildSet_범위.asset` | Range | 범위 |
| `BuildSet_행운.asset` | Luck | 행운 |
| `BuildSet_탐욕.asset` | Greed | 탐욕 |
| `BuildSet_타로.asset` | Tarot | 타로 |

각 SO 의 `Tiers` 배열은 [items_draft.md §1](../../../../../docs/khi/items_draft.md) 세트 효과 표를 그대로 입력합니다. 본 ticket(CL-138) 의 책임은 *데이터 구조 정의* 까지이며, 카운트·적용 로직은 CL-139/CL-140.
