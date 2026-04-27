# AI-304 polling timeout 초안

## 관측한 성공 실행 시간

`comfyui-stderr.log`에서 최근 성공 실행 시간을 확인했다.

- `13.32s`
- `13.48s`
- `29.56s`
- `37.58s`
- `51.55s`

즉 같은 계열 workflow라도 warm/cold 상태에 따라 대략 `13 ~ 52초` 범위가 나왔다.

## 1차 MVP 초안

- polling interval:
  - `2초`
- soft warning threshold:
  - `60초`
- hard timeout threshold:
  - `120초`

## 초안 근거

- `2초` 간격이면 UX가 지나치게 답답하지 않으면서 ComfyUI를 과도하게 두드리지 않는다.
- `60초`는 최근 관측 최장 성공 시간 `51.55초`를 넘긴 뒤 주의 구간에 들어가는 보수적 기준이다.
- `120초`는 일시적인 cold start나 GPU 상태 흔들림을 감안한 1차 hard timeout 값이다.

## 구현 메모

- `AI-405-01` 상수 확정 전까지는 이 문서 값을 임시 기준으로 사용한다.
- `AI-406`에서 timeout 상태를 분리할 때는 `FAILED`와 섞지 않고 별도 `TIMEOUT` 상태값을 두는 편이 낫다.
- 실제 운영 데이터가 쌓이면 95 percentile 기준으로 재조정하는 것이 좋다.
