package com.lostmemory.server.global.security;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lostmemory.server.global.exception.ErrorCode;
import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.global.response.ErrorDetail;
import jakarta.servlet.http.HttpServletResponse;
import lombok.RequiredArgsConstructor;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;

import java.io.IOException;

@Component
@RequiredArgsConstructor
public class ErrorResponseWriter {

    private final ObjectMapper objectMapper;

    /** ErrorCode를 ApiResponse.error JSON 으로 직렬화해 HttpServletResponse 에 직접 기록 (Security 필터단 등 MVC 밖 응답용) */
    public void write(HttpServletResponse response, ErrorCode code) throws IOException {
        ErrorDetail detail = new ErrorDetail(code.name(), code.message());
        ApiResponse<Void> body = ApiResponse.error(detail);

        response.setStatus(code.status().value());
        response.setContentType(MediaType.APPLICATION_JSON_VALUE);
        response.setCharacterEncoding("UTF-8");
        objectMapper.writeValue(response.getWriter(), body);
    }
}
