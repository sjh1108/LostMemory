package com.lostmemory.server.session.entity;

import jakarta.persistence.AttributeConverter;
import jakarta.persistence.Converter;

/**
 * SessionRole enum ↔ DB 컬럼 변환기.
 * Java 측은 관례대로 대문자(HOST/GUEST), DB 측은 schema.sql 의 CHECK (role IN ('host','guest')) 와 일치하도록 소문자로 영속화.
 * autoApply=true 라서 @Enumerated 는 같이 쓰지 않는다. (UserStatusConverter 와 동일 패턴)
 */
@Converter(autoApply = true)
public class SessionRoleConverter implements AttributeConverter<SessionRole, String> {

    @Override
    public String convertToDatabaseColumn(SessionRole attribute) {
        return attribute == null ? null : attribute.name().toLowerCase();
    }

    @Override
    public SessionRole convertToEntityAttribute(String dbData) {
        return dbData == null ? null : SessionRole.valueOf(dbData.toUpperCase());
    }
}
