package com.lostmemory.server.memory.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 기억 액자 마스터. 시드로 frame_id 1~4 가 들어가며 런타임에 추가되지 않음.
 *
 * 각 프레임의 칸 정보(rows/cols/cost) 는 클라가 관리. 백엔드는 식별자 + 정렬만.
 */
@Entity
@Table(name = "memory_frames")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class MemoryFrame {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "frame_id")
    private Long id;

    @Column(name = "display_order", nullable = false)
    private Integer displayOrder;
}
