# KHI 작업 문서

이 폴더는 KHI 테스트 씬과 TopDown Engine 도입 관련 작업을 정리한다.

## 문서 목록

| 문서 | 목적 |
|---|---|
| `test-khi-prototype-status.md` | `test_khi.unity` 현재 구조, 완료된 작업, 보류 이슈 정리 |
| `character-replacement-guide.md` | 임시 TDE 캐릭터를 나중에 프로젝트 캐릭터로 바꾸기 위한 조건과 절차 |
| `topdown-engine-adoption-plan.md` | TopDown Engine 채택 방향 검토 |
| `topdown-engine-feature-inventory.md` | TopDown Engine에서 가져올 수 있는 기능 목록 |
| `game-design-draft.md` | KHI 쪽 게임 기획 초안 |

## 작업 원칙

- TopDown Engine 원본은 직접 수정하지 않는다.
- 테스트 코드는 `LostMemory/Assets/_Project` 아래에 둔다.
- 씬/프리팹 YAML 직접 수정은 필요한 경우에만 최소 범위로 진행한다.
- `test_khi.unity`는 기능 검증용 테스트 씬으로 보고, 실제 게임 씬과 분리한다.
- 자동 생성/자동 삭제 로직은 보수적으로 둔다. 특히 런타임에서 기존 씬 오브젝트를 삭제하는 방식은 피한다.

