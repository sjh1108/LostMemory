# 트러블슈팅 로그

시연 / 데모 / 멀티플레이 발견 버그의 root cause + fix 기록.
"왜 안 됐는가" 를 중점으로 기록 — 같은 패턴 재발 방지가 목표.

## 인덱스

| 날짜 | 문제 | 영역 |
|---|---|---|
| 2026-05-26 | [Rena 보스 cast telegraph (빨간 장판) host-only 표시](2026-05-26-rena-cast-telegraph-remove.md) | NGO sync — telegraph |
| 2026-05-26 | [Rena 보스 IceSweep row warning guest 측 visual 부재](2026-05-26-rena-icesweep-rowwarning-sync.md) | NGO sync — visual hierarchy |
| 2026-05-28 | [유물 stat 효과 미적용 (3중 버그)](2026-05-28-relic-stat-not-applied.md) | 유물 — `_effects[]` array + 원거리 무기 AttackPower + InventoryTestWindow 솔로 |
| 2026-05-28 | [씬 전환 시 미소녀 ghost / stuck 중복 (NGO 멀티)](2026-05-28-magical-girl-scene-transition-ghost.md) | MagicalGirl — scene-placed + DDoL persistent 2 Spawner race |

## 기록 원칙

1. **증상** — 사용자가 본 것 그대로 (host/guest 양쪽 시점 명시)
2. **Root cause** — 코드 file:line 인용, "왜 그렇게 설계됐는가" 의 가정도 추적
3. **Fix** — diff 또는 핵심 코드 발췌
4. **영향 범위** — 어떤 시스템이 바뀌고 어떤 게 안 바뀌었는지
5. **NGO 안정성** — Join risk / prefab 변경 / ClientRpc 시그니처 영향 평가
6. **교훈** — 같은 패턴 재발 방지를 위한 가이드라인
