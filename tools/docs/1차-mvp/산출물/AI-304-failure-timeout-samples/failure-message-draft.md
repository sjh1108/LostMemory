# AI-304 실패 메시지 표준 문구 초안

## 목적

- 사용자에게 보여줄 문구와 내부 로그에 남길 원문을 분리한다.
- `AI-406-03` 구현 전 문서 기준을 먼저 맞춘다.

## 메시지 표

| 케이스 | 사용자 노출 문구 | 내부 로그 문구 |
| --- | --- | --- |
| 모델 없음 | `생성에 필요한 모델 구성이 올바르지 않아 요청을 처리하지 못했습니다.` | `ComfyUI prompt validation failed: invalid model filename in loader node` |
| output node 없음 | `워크플로 설정이 올바르지 않아 결과 이미지를 생성할 수 없습니다.` | `ComfyUI prompt validation failed: prompt has no outputs` |
| 잘못된 node reference | `워크플로 연결 정보가 올바르지 않아 요청을 처리하지 못했습니다.` | `ComfyUI prompt validation failed: broken node reference in workflow graph` |
| malformed JSON | `생성 요청 형식이 올바르지 않습니다.` | `ComfyUI prompt request parse failed: malformed JSON body` |
| polling timeout | `생성 시간이 예상보다 오래 걸려 요청을 종료했습니다.` | `ComfyUI history polling timed out before success state` |
| output 없음 | `생성은 완료되었지만 결과 파일을 찾지 못했습니다.` | `ComfyUI history success without discoverable output file` |

## 메시지 작성 원칙

- 사용자 문구에는 node id, 파일명, stack trace 같은 내부 정보를 넣지 않는다.
- 내부 로그 문구에는 실패 단계와 원인을 가능한 짧게 남긴다.
- backend 상태값은 별도로 관리하고, 문구는 그 상태값을 사람이 읽기 좋은 형태로 변환한 결과로 본다.
