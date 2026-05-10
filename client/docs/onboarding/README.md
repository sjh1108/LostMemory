# Onboarding — 신규 합류 개발자용 가이드

신규 합류 개발자가 LostMemory 클라이언트의 적/보스 도메인을 빠르게 따라잡고, 새 몹을 양산할 수 있도록 돕는 진입점.

기존 [client/docs/](../) (CL-nnn 작업 단위 계획) 와 [docs/](../../../docs/) (게임 기획) 는 그대로 유지하고, 이 폴더는 **종합 명세 + 캡처 가이드** 용도로 그 위에 얹힌다.

## 문서 목록

### 표준 명세

- [mob-authoring-spec.md](mob-authoring-spec.md) — 일반 몹 양산 표준 명세 (8 카테고리 + 신규 추가 체크리스트 19 단계 + 6 종 적 충족도 격자)
- [boss-authoring-spec.md](boss-authoring-spec.md) — 보스 양산 표준 명세 (5 추가 카테고리 + 추가 체크리스트 17 단계 + Bertha 충족도 격자)

### 시각 자료

- [capture-shot-list.md](capture-shot-list.md) — 인게임 플레이 캡처 24 컷 가이드. 노션에 paste 하면 todo 블록으로 변환되어 진행 트래킹 가능

## 권장 학습 순서

신규 합류자가 처음부터 따라가는 경우.

1. [docs/01_game_overview.md](../../../docs/01_game_overview.md) — 게임 컨셉 한 페이지 요약
2. [mob-authoring-spec.md](mob-authoring-spec.md) — 적이 어떤 구성으로 만들어지는지 (8 카테고리)
3. [boss-authoring-spec.md](boss-authoring-spec.md) — 보스가 일반 몹과 어떻게 다른지 (5 추가)
4. 노션에 [capture-shot-list.md](capture-shot-list.md) 를 paste 한 페이지에서 인게임 캡처를 보며 1~3 의 개념을 시각적으로 매칭
5. [client/docs/cl037_cl038_melee_enemy_plan.md](../cl037_cl038_melee_enemy_plan.md) 부터 CL-037 ~ CL-055 순서로 실제 구현 계획 문서 읽기

## 향후 추가 예정

- 전투(Combat) 도메인 온보딩 — 플레이어 측 피해/패링/대시 로직
- 네트워킹 도메인 온보딩 — 호스트 권위 모델, Phase A/B 동기화 규칙
- UI / HUD 도메인 온보딩 — 보스 체력바, 결과 화면, 화폐 HUD
- 메모리 매커닉 온보딩 — 게임 핵심 매커닉

각 도메인 온보딩이 추가될 때 위 "문서 목록" 섹션에 인덱스를 1 줄씩 추가한다.

## 사용 시 주의

- 이 폴더의 명세서는 **사후 표준화** 결과물이다. 격자의 `?` 표기는 Unity 에디터에서 직접 검증해야 하는 항목으로, prefab 인스펙터를 열어 확인한 결과를 추후 반영한다.
- 명세와 현실이 어긋나면 우선 **현실을 명세에 맞춰 보정** 하고, 보정이 어려운 경우 명세를 갱신한다 (CL 티켓 발급 후).
- 노션 paste 는 마크다운 호환 (체크박스, 표, 헤더 모두 작동). mermaid 코드블록은 노션에서 자동 렌더되지 않으므로 필요 시 외부 mermaid 임베드 / 이미지 변환을 사용한다.
