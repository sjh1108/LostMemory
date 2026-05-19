package com.lostmemory.server.user.entity;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class UserRecordTest {

    @Test
    @DisplayName("upgradeIfDeeper — 더 깊은 chapter 가 들어오면 갱신")
    void upgradeIfDeeper_higherChapter_updates() {
        UserRecord record = UserRecord.create(null, 1, 5);

        boolean changed = record.upgradeIfDeeper(2, 0);

        assertThat(changed).isTrue();
        assertThat(record.getClearedChapter()).isEqualTo(2);
        assertThat(record.getClearedStage()).isEqualTo(0);
    }

    @Test
    @DisplayName("upgradeIfDeeper — 동일 chapter + 더 깊은 stage 면 갱신")
    void upgradeIfDeeper_sameChapterHigherStage_updates() {
        UserRecord record = UserRecord.create(null, 2, 3);

        boolean changed = record.upgradeIfDeeper(2, 5);

        assertThat(changed).isTrue();
        assertThat(record.getClearedChapter()).isEqualTo(2);
        assertThat(record.getClearedStage()).isEqualTo(5);
    }

    @Test
    @DisplayName("upgradeIfDeeper — 더 낮은 chapter 면 stage 가 높아도 갱신 X (chapter 우선)")
    void upgradeIfDeeper_lowerChapterHigherStage_noChange() {
        UserRecord record = UserRecord.create(null, 2, 3);

        boolean changed = record.upgradeIfDeeper(1, 99);

        assertThat(changed).isFalse();
        assertThat(record.getClearedChapter()).isEqualTo(2);
        assertThat(record.getClearedStage()).isEqualTo(3);
    }

    @Test
    @DisplayName("upgradeIfDeeper — 동일 chapter + 동일 stage 면 갱신 X")
    void upgradeIfDeeper_sameChapterSameStage_noChange() {
        UserRecord record = UserRecord.create(null, 2, 3);

        boolean changed = record.upgradeIfDeeper(2, 3);

        assertThat(changed).isFalse();
        assertThat(record.getClearedChapter()).isEqualTo(2);
        assertThat(record.getClearedStage()).isEqualTo(3);
    }

    @Test
    @DisplayName("upgradeIfDeeper — 동일 chapter + 더 낮은 stage 면 갱신 X")
    void upgradeIfDeeper_sameChapterLowerStage_noChange() {
        UserRecord record = UserRecord.create(null, 2, 5);

        boolean changed = record.upgradeIfDeeper(2, 3);

        assertThat(changed).isFalse();
        assertThat(record.getClearedStage()).isEqualTo(5);
    }
}
