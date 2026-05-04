# Generated RelicData (CL-141)

이 폴더의 RelicData asset 들은 **자동 생성**됩니다. **직접 수정하지 마세요**.

## 생성 방법

Unity Editor 메뉴:

1. **`LostMemory > Relics > Validate items.csv (Dry Run)`** — 파싱·검증만 실행 (SO 생성 X)
2. **`LostMemory > Relics > Generate RelicData from CSV`** — 실제 SO 일괄 생성

## 데이터 소스

`../_source/items.csv` (77 행). 데이터 수정 시:
1. CSV 파일만 수정
2. 위 메뉴 재실행

## 명명 충돌

기존 SO (`Relics/` 직하 18개, 한국어 명) 와 동명인 행은 **스킵 + 경고**. 기존 SO 보호.

## 생성되는 필드

각 RelicData 에 다음이 채워집니다:
- DisplayName / Rarity / TagPrimary / TagSecondary / Size
- EffectDescription (CSV 의 Description 컬럼)
- Effects[] (Effect1/2 Type+Magnitude 자동 채움. 둘 다 None/0 이면 빈 배열)

채우지 않는 필드:
- IsConsumable = false (소모품 SO 는 별도)
- IsInstantUse = false
- Icon = null (별도 ticket 에서 매핑)

## 후속 ticket 영향

- CL-142~147: 본 폴더의 SO 들로 효과 시스템 검증
- CL-148: 인벤토리 UI 표시
- CL-152: 보상 풀 등록
