# cl188 — RelicData 빈 아이콘 일괄 할당

## Context

현재 `RelicData` ScriptableObject 중 **78개가 아이콘 미할당** 상태 (`_icon: {fileID: 0}`). 실제 SO 위치는 `Assets/_Project/ScriptableObjects/Relics/Generated/` 폴더 (CSV로 일괄 생성된 .asset 파일들). `Relics/` 직하의 18개 .asset 파일은 수동 작업되어 이미 아이콘 할당 완료. `Assets/_Project/Art/Icons/` 폴더에는 221개의 PNG가 있고 그 중 약 **203개가 미사용**. 인벤토리·상점 등 UI에서 정상적인 아이콘이 표시되도록 매칭해서 채워 넣는 작업.

> 참고: `Relics/Buildset/Relics/` 하위의 .json 파일들은 JsonUtility 형식이라 Sprite 참조가 저장되지 않음 — 별도 자료로 보이고 이번 작업 대상 아님.

- 상점 UI에서 아이콘이 빠지면 빈 칸으로 보임 (`ShopItemView._iconImage.sprite = data.Relic.Icon;` — null 대입)
- 게임 디자인상 ([items_draft.md](docs/khi/items_draft.md)) 75~77개 아이템 풀이 목표라 이 78개가 전부 실제로 노출됨

## Approach

### Phase 1 — 추천 매칭표 작성 (Claude)

파일: **`client/docs/khi/cl188_icon_matching.md`** (신규 작성)

형식 — Markdown 표:

| # | RelicData 파일 | displayName | 추천 PNG | 사유 |
|---|---|---|---|---|
| 1 | RelicData_적중의망원경.asset | 적중의 망원경 | 전자기기-망원경.png | 직접 일치 |
| 2 | RelicData_얼음결정.asset | 얼음 결정 | 식품-얼음.png | 키워드 일치 |
| ... | ... | ... | ... | ... |

매칭 우선순위:
1. **키워드 직접 일치** — "망원경" → 망원경, "얼음" → 얼음
2. **카테고리 매칭** — "마법사/마법서" → 서적-, "룬/부적" → 문구-쪽지/편지, "악세사리류" → 악세-
3. **컨셉 매칭** — "빙결/서리" → 얼음/식품-아이스크림, "불/화염" → 식품-마라/불닭, "전기/번개" → 전자기기-
4. **등급 고려** — 전설/유니크는 더 화려한 아이콘 (보석류, 황금)

매칭 후보 없으면 PNG 컬럼에 `미정` 기재.

### Phase 2 — 사용자 검토

사용자가 `cl188_icon_matching.md`를 직접 열어 PNG 컬럼만 수정:
- 마음에 안 드는 추천 → 다른 PNG로 교체
- `미정` 항목 → 적절한 PNG 입력하거나 그대로 두기 (그대로면 스킵)
- 행 자체 삭제 — 해당 RelicData는 변경 안 함

완료 후 사용자가 알려주면 Phase 3 진행.

### Phase 3 — Editor 스크립트 작성 & 실행

신규 파일: **`LostMemory/Assets/_Project/Scripts/Editor/RelicIconAssigner.cs`**

```csharp
[MenuItem("Tools/Relics/Assign Icons From Markdown")]
public static void AssignIcons()
{
    // 1. cl188_icon_matching.md 파싱 (| 구분, 헤더 스킵, "미정" 행 스킵)
    // 2. 각 행마다:
    //    var relic = AssetDatabase.LoadAssetAtPath<RelicData>(relicPath);
    //    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
    //    var so = new SerializedObject(relic);
    //    so.FindProperty("_icon").objectReferenceValue = sprite;
    //    so.ApplyModifiedProperties();
    //    EditorUtility.SetDirty(relic);
    // 3. AssetDatabase.SaveAssets()
    // 4. 결과 요약 Debug.Log (성공/실패/스킵 카운트, 실패 사유)
}
```

- Undo 지원: `Undo.RecordObject` 사용
- 안전성: 이미 아이콘 있는 RelicData는 건드리지 않음 (`_icon == null` 체크 후에만 적용)
- 검증: PNG 파일 존재 여부, Sprite 로드 성공 여부 확인 후 적용

실행 절차:
1. Unity 에디터에서 메뉴 `Tools > Relics > Assign Icons From Markdown` 클릭
2. Console 로그로 결과 확인 (예: "성공 70 / 실패 0 / 스킵 8")

## Critical Files

**조사한 기존 파일 (수정 안 함)**:
- [RelicData.cs:51](LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicData.cs:51) — `_icon` 필드 (`Sprite`)
- [RelicData.cs:81](LostMemory/Assets/_Project/Scripts/Runtime/Relics/RelicData.cs:81) — `Icon` 프로퍼티
- [ShopItemView.cs:67](LostMemory/Assets/_Project/Scripts/Runtime/UI/Shop/ShopItemView.cs:67) — UI 적용 지점
- `LostMemory/Assets/_Project/ScriptableObjects/Relics/Generated/` — 78개 .asset 파일 수정 대상 (실제 SO)
- `LostMemory/Assets/_Project/Art/Icons/` — 아이콘 PNG 풀

**Claude가 작성**:
- `client/docs/khi/cl188_icon_matching.md` — 매칭표 (사용자 검토 대상)
- `LostMemory/Assets/_Project/Scripts/Editor/RelicIconAssigner.cs` — 적용 스크립트

## Verification

1. **스크립트 실행 로그**: Unity Console에 성공/실패/스킵 카운트 출력. 실패가 있으면 사유 함께.
2. **인스펙터 확인**: Project 창에서 임의의 변경된 RelicData 5~10개 선택 → 인스펙터 우측에 아이콘 미리보기 표시되는지.
3. **Play 모드 검증**: 게임 실행 → 상점 진입 → 78개에 포함된 아이템이 등장할 때 빈 칸 없이 아이콘 표시되는지 확인.
4. **Git diff**: `git diff` 로 변경된 `.asset` 파일들이 `_icon: {fileID: 0}` → `_icon: {fileID: 21300000, guid: ..., type: 3}` 라인만 변경됐는지 확인 (다른 필드 보존).
5. **롤백 가능성**: 문제 발견 시 `git checkout` 으로 `.asset` 파일들 일괄 되돌리거나 Unity에서 Undo (`Ctrl+Z`).

## Notes

- 매칭이 어색한 항목은 Phase 2에서 사용자가 자유롭게 교체 가능. Claude의 추천은 시작점일 뿐.
- 78개 전부 채우지 않아도 됨 — `미정`이거나 행 삭제하면 해당 RelicData는 그대로 유지.
- 적용 후에도 인스펙터에서 개별 변경 가능 (스크립트는 일괄 채우기 도구일 뿐).
