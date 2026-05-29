# LLM 호스팅 옵션 비교 + 권장안

## Context
현재 LLM 채팅은 **RunPod 위 vLLM** (`bllossom-8b`) 단일 운영자 pod 로 서비스 중. 운영 안정성/비용/모델 다양성 측면의 대안을 정리해 발표 후 의사결정 자료로 둠.

코드 변경은 없음 — 본 문서는 의사결정용. 실제 호스팅 swap 시에는 `LlmProperties.baseUrl`/`apiKey` + 운영 `.env` + `LlmApiClient.ModelName` 만 바꾸면 됨 (백엔드 프록시 추상화 덕분).

---

## 현 상태 (2026-05-23)

| 항목 | 값 |
|---|---|
| 호스팅 | RunPod (운영자 개인 pod) |
| 모델 | `bllossom-8b` (한국어 Llama-3 8B bllossom 파인튜닝) |
| URL | `https://<POD_ID>-8000.proxy.runpod.net/v1` |
| 인증 | `Authorization: Bearer <VLLM_API_KEY>` (단일 키) |
| 비용 | RunPod 시간당 GPU 과금 (운영자 부담) |
| 가용성 | 운영자가 pod 띄워둘 때만. SLA 없음 |
| Context window | 8K 토큰 |

## RunPod 의 한계
1. **운영자 의존**: 운영자가 pod 끄면 즉시 다운. SSAFY 졸업 후 유지보수 부담
2. **POD_ID 변경 위험**: pod 재기동 시 ID 바뀜 → `.env` 갱신 필요 ([reference_runpod_vllm](../../.claude/projects/C--Users-SSAFY-IdeaProjects-S14P31C201/memory/reference_runpod_vllm.md))
3. **단일 키**: 키 회전 시 vLLM 재시작 (downtime) — atomic swap 불가
4. **scale 0**: 동시 사용자 N+ 시 응답 지연 가능 (vLLM batch 처리 한계)

---

## 후보 옵션

### A. Together AI (관리형 vLLM)
- **장점**: vLLM 기반 + 한국어 모델 일부 지원 + OpenAI 호환 API + 관리형 (운영 부담 X)
- **단점**: `bllossom-8b` 직접 호스팅 안 함 → 다른 한국어 모델 (Qwen2.5 한국어 fine-tune 등) 으로 교체 필요
- **비용**: per-token 과금 (8B 모델 ~$0.20/1M tokens 수준)
- **migration 비용**: 백엔드 `VLLM_BASE_URL` + `VLLM_API_KEY` 만 교체. 클라 `ModelName` 변경

### B. AWS Bedrock
- **장점**: 엔터프라이즈급 SLA + Claude/Llama/Mistral 등 다수 모델 + Korea region (ap-northeast-2) + IAM 통합
- **단점**: OpenAI 호환 API 가 아님 → 백엔드 `LlmProxyService` body/응답 변환 layer 필요. SDK 의존성 추가
- **비용**: per-token. 모델별 다름 (Claude Haiku ~$0.25/$1.25 per 1M in/out)
- **migration 비용**: 큼 — proxy 재작성 필요 (단 추상화 잘 해두면 후속 호스팅 swap 도 쉬워짐)

### C. OpenRouter (멀티 routing)
- **장점**: 단일 API 로 수십 개 모델 (OpenAI / Anthropic / Llama 변형 등) routing + OpenAI 호환 + fallback 자동
- **단점**: 중간 단계가 하나 더 — latency 약간 증가, 가용성 의존
- **비용**: provider 별 마진 + per-token
- **migration 비용**: 작음 — OpenAI 호환 → URL/Key 만 교체

### D. Groq (속도 특화)
- **장점**: Llama-3 8B 추론 속도가 매우 빠름 (500+ tok/s) — 게임 채팅 UX 에 적합 + OpenAI 호환
- **단점**: 한국어 모델 옵션 좁음 (영문 위주). 무료 tier 제한
- **비용**: per-token 저렴. 무료 tier 있음
- **migration 비용**: 작음 — OpenAI 호환

### E. 자가 호스팅 (SSAFY 외부 GPU 임대)
- **장점**: 모델 자유 선택 + bllossom-8b 그대로 유지 가능
- **단점**: 운영 부담 큼 (vLLM 띄우기 + 모니터링 + 자동 재시작 + 보안). 현재 RunPod 과 같은 risk 그대로
- **비용**: GPU 시간당 (Vast.ai / Lambda / 직접 GPU 서버 등)

---

## 비교 표

| 옵션 | 운영 부담 | OpenAI 호환 | 한국어 모델 | 발표 후 유지 적합도 |
|---|---|---|---|---|
| RunPod (현재) | 🔴 운영자 의존 | ✅ vLLM 호환 | ✅ bllossom-8b | 🟡 운영자 활동 의존 |
| **A. Together AI** | 🟢 관리형 | ✅ | 🟡 일부 (Qwen 등) | ✅ |
| B. AWS Bedrock | 🟢 관리형 + SLA | ❌ SDK 필요 | 🟡 Claude/Llama (한국어 일반 강함) | ✅ |
| **C. OpenRouter** | 🟢 routing 관리형 | ✅ | ✅ 라우팅 통해 다양 | ✅ |
| D. Groq | 🟢 관리형 | ✅ | ❌ 영문 위주 | 🟡 한국어 한계 |
| E. 자가 호스팅 | 🔴 운영자 부담 큼 | ✅ vLLM 호환 | ✅ 자유 | ❌ |

---

## 권장안

**1순위 — C. OpenRouter** (마이그레이션 가장 가벼움 + 모델 자유도 + fallback 자동)
- 한국어는 Qwen2.5-72B-Instruct / Claude Haiku / GPT-4o-mini 등 routing
- 백엔드 변경: `VLLM_BASE_URL=https://openrouter.ai/api/v1` + `VLLM_API_KEY=<key>` 만 교체. `ModelName` 만 클라 측 변경
- 비용 vs 안정성 trade-off — provider 골라가며 latency/품질 비교 가능

**2순위 — A. Together AI** (vLLM 기반이라 가장 호환성 안전)
- bllossom 직접 호스팅 안 한다는 게 결정적 단점. 클라 측 model id 변경 + 응답 품질 재검증 비용

**3순위 — B. AWS Bedrock** (장기 운영 안정성 최고, 단 마이그레이션 비용 큼)
- SSAFY 졸업 후 학생 본인이 운영 안 할 거면 적합 X. 회사/팀 인수 시 검토

**비권장 — D, E**: 한국어 한계 또는 운영 부담 그대로

---

## 결정 시점 / 검증 절차
- 발표 직후 RunPod 운영 부담 평가 → 운영자 인수자 없을 경우 1순위 (OpenRouter) 마이그레이션
- 검증: `trial/llm-openrouter-202Xxxxx` 브랜치 + EC2 가빌드 (`.env` 만 교체) + curl + Unity Editor 채팅 → 응답 품질/latency 비교 → develop merge

## 비범위 (이 문서가 답 안 하는 것)
- 정확한 가격 수치 (변동성 큼 — 결정 시점에 각 사 가격표 직접 확인)
- 모델 품질 비교 (정성적 — 한국어 채팅 응답 quality 는 실제 호출 평가 필요)
- legal/compliance (GDPR / 게임 약관 / data retention 정책 — 별도 검토)
