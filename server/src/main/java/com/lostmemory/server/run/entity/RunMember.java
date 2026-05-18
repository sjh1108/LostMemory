package com.lostmemory.server.run.entity;

import com.lostmemory.server.user.entity.User;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.OnDelete;
import org.hibernate.annotations.OnDeleteAction;

/**
 * 런에 참여한 유저별 스냅샷. 런 시작 시 RunService 가 세션의 모든 멤버를 등록.
 *
 * MVP 단계엔 selected_weapon_id / final_hp / final_gold / *_json 컬럼 모두 NULL 허용.
 * 추후 richer stats 합의 후 채워짐.
 */
@Entity
@Table(
        name = "run_member",
        uniqueConstraints = {
                @UniqueConstraint(name = "uq_run_user", columnNames = {"run_id", "user_id"})
        }
)
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class RunMember {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @Column(name = "run_member_id")
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "run_id", nullable = false)
    @OnDelete(action = OnDeleteAction.CASCADE)
    private Run run;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "user_id", nullable = false)
    @OnDelete(action = OnDeleteAction.CASCADE)
    private User user;

    @Column(name = "selected_weapon_id")
    private Long selectedWeaponId;

    @Column(name = "final_hp")
    private Integer finalHp;

    @Column(name = "final_gold")
    private Integer finalGold;

    private RunMember(Run run, User user) {
        this.run = run;
        this.user = user;
    }

    /** 런 시작 시 멤버 row 생성. selected_weapon_id 등은 후속 합의 시 setter 추가. */
    public static RunMember of(Run run, User user) {
        return new RunMember(run, user);
    }
}
