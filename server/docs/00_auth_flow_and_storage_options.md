# 인증 절차 및 클라이언트 토큰 저장 전략 정리 문서

- **문서 목적**: 현재 프로젝트의 인증 절차를 정리하고, Unity 클라이언트에서 토큰을 어떤 방식으로 저장/관리할지 3가지 안으로 비교한다.
- **기준 방향**:
  - **1단계 구현**: 이메일 인증 기반 로그인
  - **2단계 구현**: Steam 확장 및 Steam 중심 인증 전환

---

## 1. 전체 인증 방향

현재 프로젝트의 인증 절차는 아래 2단계로 진행한다.

1. **1단계**: 이메일 인증 기반 로그인
2. **2단계**: Steam 확장

핵심 원칙은 다음과 같다.

- 처음부터 **저장 가능한 실제 계정 구조**를 사용한다.
- 내부 기준 키는 항상 **`user_id`** 로 유지한다.
- 인증 수단은 바뀌더라도 저장 데이터의 기준은 바꾸지 않는다.
- 인증 수단(provider)과 내부 유저 식별자는 분리한다.
- Unity 클라이언트는 **access token** 과 **refresh token** 을 역할에 맞게 다르게 관리한다.

---

## 2. 핵심 개념

### 2.1 `user_id`
- 서버 내부 기준 유저 식별자
- 진행 저장, 런 결과 저장, 메타 성장 저장의 기준 키
- 이메일 로그인과 Steam 로그인 모두 동일한 내부 키를 사용

### 2.2 access token
- 일반 API 요청 인증용 토큰
- 예: 유저 정보 조회, 진행 저장, 런 결과 저장
- 짧은 만료 시간을 가짐
- 클라이언트에서는 **메모리 저장**을 원칙으로 함

### 2.3 refresh token
- access token 만료 시 재발급을 위한 토큰
- 로그인 상태 유지를 위한 장기 토큰
- 클라이언트에서는 **영구 저장소 보관**이 필요
- 서버는 발급/검증/만료/무효화/rotation을 담당

### 2.4 provider
- 유저가 어떤 인증 수단으로 로그인하는지 나타내는 개념
- 예:
  - `EMAIL`
  - `STEAM`

---

## 3. 1단계 구현 - 이메일 인증 기반 로그인

### 3.1 목적
- MVP부터 저장 가능한 실계정 기반 구조 확보
- 다른 기기에서도 동일 계정으로 로그인 가능
- 진행 저장, 런 결과 저장, 메타 성장 저장을 안정적으로 계정에 귀속

### 3.2 최초 가입/로그인 흐름

#### 가입
1. 클라이언트가 이메일 가입/인증 요청
2. 서버가 이메일 유효성 검증
3. 새 `users` 생성
4. `user_auth_identities`에 `provider_type = EMAIL` 저장
5. access token / refresh token 발급
6. 클라이언트 저장
   - `access_token`: 메모리 저장
   - `refresh_token`: 영구 저장

#### 로그인
1. 클라이언트가 이메일 로그인 요청
2. 서버가 계정 검증
3. 기존 `user_id` 조회
4. access token / refresh token 발급
5. 클라이언트 저장

### 3.3 앱 재실행 흐름

1. 클라이언트 실행
2. 로컬 저장소에서 `refresh_token` 확인
3. 있으면 `POST /api/auth/refresh` 호출
4. 성공 시 새 `access_token` 발급
5. 필요 시 refresh token rotation 수행

#### refresh 실패 시
- refresh token 만료 또는 무효화
- 다시 이메일 로그인 필요

### 3.4 로그아웃 정책

#### 로그아웃
- 서버: refresh token 무효화
- 클라이언트: access token / refresh token 삭제
- 계정은 유지

#### 게임 종료
- refresh token 유지
- 다음 실행 시 refresh로 자동 복구 시도

#### 데이터 초기화 요청
- 유저 데이터 삭제 또는 비활성화 정책 적용
- 해당 유저의 모든 refresh token 만료
- 로컬 토큰 삭제
- 계정 유지 여부는 서비스 정책에 따라 결정

---

## 4. 2단계 구현 - Steam 확장

### 4.1 목적
- Steam 플랫폼에 맞춘 로그인/인증 절차 적용
- Steam 계정 기반으로 유저 인증 표준화
- 기존 이메일 계정과 저장 데이터를 유지한 채 Steam 로그인 추가 또는 전환

### 4.2 기본 방향
- Steam 인증 절차를 기준으로 로그인 수행
- 내부 `user_id`는 계속 유지
- Steam 계정은 새로운 provider로 연결

예:
- `provider_type = STEAM`
- `provider_account_id = steam id`

### 4.3 적용 방식

#### 안 1. 이메일 계정에 Steam 연동 추가
1. 기존 이메일 계정 로그인
2. Steam 연동 수행
3. 동일 `user_id`에 `STEAM` provider 추가
4. 이후 이메일 또는 Steam 중 하나로 로그인 가능

#### 안 2. Steam 중심 로그인으로 전환
1. Steam 로그인 성공
2. 기존 계정과 매핑
3. 이후 Steam 로그인을 기본 인증 수단으로 사용

---

## 5. access token / refresh token 관리 원칙

### 5.1 access token
#### 클라이언트
- 메모리에만 저장
- 앱 종료 시 사라져도 무방
- API 요청 시 `Authorization` 헤더로 전송

#### 서버
- 저장보다 검증 위주
- JWT 서명 검증
- 만료 시간 검증
- 필요 시 클레임(`user_id`, 권한 등) 추출

### 5.2 refresh token
#### 클라이언트
- 앱 재실행 후에도 유지되어야 하므로 영구 저장 필요
- 저장 매체 선택이 중요함

#### 서버
- 발급
- 유효성 검증
- 만료 시간 결정
- revoke 처리
- rotation 수행

즉,
- **클라이언트 = 보관/전달 주체**
- **서버 = 발급/검증/수명 관리 주체**

---

## 6. Unity 클라이언트 토큰 저장 전략 3가지 안

현재 논의 기준은 **Windows 환경의 Unity 클라이언트**를 전제로 한다.

### 안 1. 쉬움
#### 저장 방식
- `access_token`: 메모리
- `refresh_token`: `PlayerPrefs`

#### 장점
- 구현이 가장 쉬움
- Unity 기본 기능만으로 바로 적용 가능
- 빠른 프로토타이핑 가능

#### 단점
- `PlayerPrefs`는 민감 정보 저장에 부적절
- 보안 수준이 낮음
- refresh token 저장소로 비권장

#### 적합 상황
- 아주 짧은 기간의 실험용 프로토타입
- 기능 동작 확인만 필요한 경우

#### 평가
- **가장 쉬우나, 최종안으로는 비추천**

---

### 안 2. 권장
#### 저장 방식
- `access_token`: 메모리
- `refresh_token`: `Application.persistentDataPath` 아래 파일 저장
- 파일 내용은 **암호화 후 저장**

#### 장점
- Unity 개발자가 상대적으로 쉽게 구현 가능
- `PlayerPrefs`보다 안전함
- Windows 전용 네이티브 연동 없이도 MVP 수준 구현 가능
- 실무적으로 가장 현실적인 절충안

#### 단점
- 암호화 키/방식에 대한 고민 필요
- OS 보안 저장소보다는 보안 수준이 낮음

#### 적합 상황
- 현재 프로젝트의 MVP
- 일정 제약이 큰 상태
- Unity 클라이언트 측에서 무리 없이 구현해야 하는 경우

#### 평가
- **현재 프로젝트 기준 최우선 추천안**

---

### 안 3. 강보안
#### 저장 방식
- `access_token`: 메모리
- `refresh_token`: Windows Credential Locker / PasswordVault

#### 장점
- OS 보안 저장소 사용
- 민감 정보 저장 방식으로 가장 정석적
- refresh token 보관 관점에서 가장 안전한 편

#### 단점
- Unity에서 바로 쓰기 어려움
- Windows 전용 연동 필요
- 네이티브 플러그인/플랫폼 연동 작업이 필요할 수 있음
- 클라이언트 개발 난이도 상승

#### 적합 상황
- 장기 운영
- 보안 요구 수준이 높음
- 일정 여유가 충분한 경우

#### 평가
- **보안은 가장 좋지만 MVP 구현 난이도가 높음**

---

## 7. 추천안

현재 프로젝트 기준 추천은 다음과 같다.

### 최종 추천
- `access_token` → **메모리 저장**
- `refresh_token` → **`persistentDataPath`의 암호화 파일 저장**
- 서버 → **refresh token rotation / revoke / 만료 관리**

### 추천 이유
- Unity 클라이언트 팀이 비교적 쉽게 구현 가능
- `PlayerPrefs` 단독 저장보다 안전함
- OS 보안 저장소 직접 연동보다 부담이 적음
- 추후 강보안 구조로 전환도 가능

즉,
**지금은 안 2(권장)로 가고, 이후 일정 여유가 생기면 안 3(강보안)로 올리는 구조**가 가장 현실적이다.

---

## 8. DB 저장 방향

### 8.1 `users`
- 내부 기준 유저
- 진행 저장, 런 결과 저장, 메타 성장 저장의 기준 키

### 8.2 `user_auth_identities`
- 유저가 어떤 인증 수단을 가지는지 저장

예:
- `provider_type = EMAIL`
- `provider_account_id = email or normalized email key`

향후:
- `provider_type = STEAM`
- `provider_account_id = steam id`

### 8.3 `auth_refresh_tokens`
- refresh token 세션 관리
- 어떤 refresh token이 유효한지 저장
- 로그아웃/만료/회전 상태 관리

예시 저장 정보:
- `user_id`
- `token_hash`
- `expires_at`
- `revoked_at`
- `last_used_at`
- `created_at`

---

## 9. 토큰 정책 권장안

### access token
- 용도: API 인증
- 권장 만료: **30분 ~ 1시간**

### refresh token
- 용도: access token 재발급
- 권장 만료: **14일 ~ 30일**
- 권장 정책: **rotation 적용**

---

## 10. 단계별 요약

### 1단계 구현
- 이메일 인증 기반 로그인
- JWT(access + refresh)
- 다른 기기 로그인 가능
- 진행 저장 가능
- refresh token 기반 로그인 상태 복구

### 2단계 구현
- Steam 인증 추가
- 내부 `user_id` 유지
- 이메일 계정과 Steam 계정 연동 또는 전환 가능

### 클라이언트 저장 전략
- 쉬움: `PlayerPrefs`
- 권장: `persistentDataPath` + 암호화 파일
- 강보안: Windows Credential Locker / PasswordVault

---

## 11. 한 줄 정리

현재 프로젝트의 인증 절차는 **초기부터 이메일 인증 기반 계정 구조로 시작하고, 이후 Steam 확장 시에도 내부 `user_id`를 유지한 채 provider만 확장한다. 클라이언트 토큰 저장은 MVP 기준으로 `access token`은 메모리, `refresh token`은 암호화 파일 저장 방식을 우선 추천한다.**
