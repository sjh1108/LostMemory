package com.lostmemory.server.memory.controller;

import com.lostmemory.server.global.response.ApiResponse;
import com.lostmemory.server.memory.dto.MemoryFrameView;
import com.lostmemory.server.memory.dto.MemoryProgressSummaryView;
import com.lostmemory.server.memory.dto.MemoryProgressView;
import com.lostmemory.server.memory.dto.UnlockSlotRequest;
import com.lostmemory.server.memory.service.MemoryService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Content;
import io.swagger.v3.oas.annotations.media.ExampleObject;
import io.swagger.v3.oas.annotations.responses.ApiResponses;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@Tag(name = "Memory", description = "기억 액자 마스터 + 본인 진행도 + 칸 해금")
@RestController
@RequestMapping("/memory")
@RequiredArgsConstructor
public class MemoryController {

    private final MemoryService memoryService;

    @Operation(
            summary = "액자 마스터 조회",
            description = """
                    전체 액자 마스터 반환. `displayOrder` 오름차순. 클라는 이 순서대로 UI 배치.

                    칸 정보 (rows/cols/각 칸별 cost) 는 클라가 관리 — 백엔드는 식별자 + 정렬만.
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "조회 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": [
                                { "frameId": 1, "displayOrder": 1 },
                                { "frameId": 2, "displayOrder": 2 },
                                { "frameId": 3, "displayOrder": 3 },
                                { "frameId": 4, "displayOrder": 4 }
                              ]
                            }
                            """)))
    })
    @GetMapping("/frames")
    public ApiResponse<List<MemoryFrameView>> getAllFrames() {
        return ApiResponse.of(memoryService.getAllFrames());
    }

    @Operation(
            summary = "본인 메모리 진행도 종합",
            description = """
                    Bearer 토큰의 userId 기반. 보유 파편 수 + 모든 프레임 진행도 (mask + state) 반환.
                    마을 진입 시 1회 호출.

                    `unlockedMask`: 6칸 비트마스크 (0~63). bit n=1 이면 slot n 해금.
                    `state`: mask 로부터 derive — 0=Locked, 63=Done, 그 외=In Progress.
                    진행도 row 없는 프레임은 mask=0 + Locked 으로 채워서 반환.
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "조회 성공",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "memoryShards": 125,
                                "progresses": [
                                  { "frameId": 1, "unlockedMask": 63, "state": "Done" },
                                  { "frameId": 2, "unlockedMask": 42, "state": "In Progress" },
                                  { "frameId": 3, "unlockedMask": 0,  "state": "Locked" },
                                  { "frameId": 4, "unlockedMask": 0,  "state": "Locked" }
                                ]
                              }
                            }
                            """)))
    })
    @GetMapping("/progress")
    public ApiResponse<MemoryProgressSummaryView> getMyProgress(@AuthenticationPrincipal Long userId) {
        return ApiResponse.of(memoryService.getProgressSummary(userId));
    }

    @Operation(
            summary = "프레임 내 slot 해금 + 파편 차감",
            description = """
                    요청한 frame 의 slotIndex 비트를 set (mask |= 1<<slotIndex) + `consumedShards` 만큼
                    `user_currencies.memory_shards` 차감. 한 트랜잭션 — 검증 실패 시 모두 롤백.

                    cost (consumedShards) 는 클라가 계산해서 보냄 — 백엔드는 보유량 충분 여부만 검증.

                    이미 해금된 slot 인 경우 idempotent: mask 변화 없음 + shards 차감 없음.

                    실패:
                    - 400 MEMORY_SLOT_INDEX_OUT_OF_RANGE — slotIndex 가 0~5 밖
                    - 404 MEMORY_FRAME_NOT_FOUND — frameId 존재하지 않음
                    - 409 MEMORY_SHARDS_INSUFFICIENT — 보유 파편 부족
                    """
    )
    @ApiResponses({
            @io.swagger.v3.oas.annotations.responses.ApiResponse(
                    responseCode = "200", description = "해금 성공 (또는 idempotent — 이미 해금된 slot)",
                    content = @Content(examples = @ExampleObject(value = """
                            {
                              "success": true,
                              "data": {
                                "frameId": 1,
                                "unlockedMask": 1,
                                "state": "In Progress"
                              }
                            }
                            """)))
    })
    @PostMapping("/progress/unlock-slot")
    public ApiResponse<MemoryProgressView> unlockSlot(
            @AuthenticationPrincipal Long userId,
            @Valid @RequestBody UnlockSlotRequest request) {
        return ApiResponse.of(memoryService.unlockSlot(userId, request));
    }
}
