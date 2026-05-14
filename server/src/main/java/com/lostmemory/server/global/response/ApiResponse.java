package com.lostmemory.server.global.response;

import com.fasterxml.jackson.annotation.JsonInclude;
import io.swagger.v3.oas.annotations.media.Schema;

@Schema(description = "모든 API 의 공통 응답 envelope. success=true 시 data 사용, false 시 error 사용.")
@JsonInclude(JsonInclude.Include.NON_NULL)
public record ApiResponse<T>(
        @Schema(description = "요청 성공 여부", example = "true")
        boolean success,

        @Schema(description = "응답 페이로드 (success=true 시 존재, 그 외 null)")
        T data,

        @Schema(description = "에러 상세 (success=false 시 존재, 그 외 null)")
        ErrorDetail error
) {

    /** 성공 응답 생성 (데이터 포함) */
    public static <T> ApiResponse<T> of(T data) {
        return new ApiResponse<>(true, data, null);
    }

    /** 성공 응답 생성 (데이터 없음 — 저장/삭제 등 반환값이 필요 없는 경우) */
    public static ApiResponse<Void> ok() {
        return new ApiResponse<>(true, null, null);
    }

    /** 실패 응답 생성 */
    public static <T> ApiResponse<T> error(ErrorDetail error) {
        return new ApiResponse<>(false, null, error);
    }
}
