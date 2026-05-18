# AI-304 실패 및 timeout 시나리오 샘플 수집

`AI-304`에서는 ComfyUI `/prompt` 호출 단계에서 재현 가능한 실패 케이스를 최소 1건 이상 수집하고, 이후 `AI-405`, `AI-406` 구현에 사용할 polling timeout 기준과 실패 메시지 초안을 정리한다.

## 수집 범위

- 모델 없음 validation 실패
- output node 없음 workflow 실패
- 잘못된 node reference validation 실패
- malformed JSON body 파싱 실패
- polling timeout 기준값 초안
- 실패 메시지 표준 문구 초안

## 관련 파일

- `requests/invalid-unet-name-request.json`
- `requests/missing-output-node-request.json`
- `requests/invalid-node-reference-request.json`
- `requests/malformed-json-request.txt`
- `failure-sample-results.md`
- `timeout-draft.md`
- `failure-message-draft.md`

## 핵심 결론

- ComfyUI `/prompt` 단계의 validation 실패는 이번 로컬 환경에서 HTTP `400`으로 떨어졌고 응답 body는 비어 있었다.
- 대신 실제 실패 타입과 세부 원인은 `comfyui-stderr.log`에 남는다.
- malformed JSON body는 HTTP `500`으로 떨어졌고 `JSONDecodeError`가 stderr에 기록됐다.
- 1차 MVP backend는 HTTP status만으로 실패를 분기하지 말고, 가능한 경우 request 조립 전 사전 검증과 ComfyUI stderr/응답 구조를 함께 보조 신호로 사용해야 한다.
