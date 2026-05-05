package com.lostmemory.server.user.entity;

import jakarta.persistence.AttributeConverter;
import jakarta.persistence.Converter;

/**
 * UserStatus enum ↔ DB 컬럼 변환기.
 * Java 측은 관례대로 대문자(ACTIVE/SUSPENDED/DELETED) enum 이름을 유지하고,
 * DB 측은 schema.sql 의 CHECK (status IN ('active','suspended','deleted')) 와 일치하도록 소문자로 영속화한다.
 * autoApply=true 라서 UserStatus 타입 필드는 자동으로 이 컨버터를 사용하며 @Enumerated 는 함께 쓰지 않는다.
 */
@Converter(autoApply = true)
public class UserStatusConverter implements AttributeConverter<UserStatus, String> {

    @Override
    public String convertToDatabaseColumn(UserStatus attribute) {
        return attribute == null ? null : attribute.name().toLowerCase();
    }

    @Override
    public UserStatus convertToEntityAttribute(String dbData) {
        return dbData == null ? null : UserStatus.valueOf(dbData.toUpperCase());
    }
}
