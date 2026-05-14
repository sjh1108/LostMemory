package com.lostmemory.server.global.config;

import io.swagger.v3.oas.models.Components;
import io.swagger.v3.oas.models.OpenAPI;
import io.swagger.v3.oas.models.info.Contact;
import io.swagger.v3.oas.models.info.Info;
import io.swagger.v3.oas.models.security.SecurityRequirement;
import io.swagger.v3.oas.models.security.SecurityScheme;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
public class OpenApiConfig {

    private static final String SECURITY_SCHEME_NAME = "bearerAuth";

    /** Swagger UI 용 OpenAPI 정의: 프로젝트 메타 + JWT Bearer 인증 스키마 + 글로벌 보안 요구 */
    @Bean
    public OpenAPI openAPI() {
        return new OpenAPI()
                .info(new Info()
                        .title("Lost Memory Server API")
                        .version("0.1.0")
                        .description("""
                                Lost Memory Unity 게임 백엔드 API.

                                **공통 응답 envelope**: 모든 API 응답은 `ApiResponse<T>` 형태 — `{ success, data, error }`.
                                - 성공: `{ "success": true, "data": <T> }`
                                - 실패: `{ "success": false, "error": { "code": "<ErrorCode>", "message": "..." } }`

                                **인증**: 회원가입 / 로그인 / refresh 외 모든 endpoint 는 `Authorization: Bearer <accessToken>` 헤더 필수.
                                Swagger UI 의 우상단 [Authorize] 버튼으로 토큰 입력 → 자동 헤더 부착.

                                **도메인 그룹**:
                                - Auth — 회원가입, 로그인, 토큰 갱신, 로그아웃
                                - Session — 매칭룸 생성 / 입장 / 종료 (자체 Relay 핸드셰이크용 sessionToken 발급)
                                - Run — 던전 런 시작 / 종료 / 조회 (결과 저장 + 파편 적립 + 전적 갱신)
                                - User — 본인 정보 / 재화 / 전적
                                - Weapon — 무기 마스터 데이터 + 본인 해금 상태

                                **에러 코드**: `ErrorCode` enum 참조. HTTP status 와 매핑됨 — 400 / 401 / 403 / 404 / 409 / 500.
                                """)
                        .contact(new Contact()
                                .name("LostMemory Backend")
                                .email("sonhm48021@gmail.com")))
                .addSecurityItem(new SecurityRequirement().addList(SECURITY_SCHEME_NAME))
                .components(new Components()
                        .addSecuritySchemes(SECURITY_SCHEME_NAME,
                                new SecurityScheme()
                                        .type(SecurityScheme.Type.HTTP)
                                        .scheme("bearer")
                                        .bearerFormat("JWT")
                                        .description("로그인 응답의 `accessToken` 을 입력. `Bearer ` prefix 자동 부착됨.")));
    }
}
