package com.lostmemory.website.feedback.entity;

public enum FeedbackCategory {
    BUG("버그 리포트"),
    SUGGESTION("건의사항"),
    ETC("기타");

    private final String displayName;

    FeedbackCategory(String displayName) {
        this.displayName = displayName;
    }

    public String getDisplayName() {
        return displayName;
    }
}
