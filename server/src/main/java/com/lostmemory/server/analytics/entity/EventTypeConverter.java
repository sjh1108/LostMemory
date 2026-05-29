package com.lostmemory.server.analytics.entity;

import jakarta.persistence.AttributeConverter;
import jakarta.persistence.Converter;

/**
 * EventType enum ↔ DB 컬럼 변환기.
 * Java 측은 대문자 (SESSION_START 등), DB 측은 소문자 (session_start 등).
 * autoApply=true — @Enumerated 와 같이 쓰지 않는다.
 */
@Converter(autoApply = true)
public class EventTypeConverter implements AttributeConverter<EventType, String> {

    @Override
    public String convertToDatabaseColumn(EventType attribute) {
        return attribute == null ? null : attribute.name().toLowerCase();
    }

    @Override
    public EventType convertToEntityAttribute(String dbData) {
        return dbData == null ? null : EventType.valueOf(dbData.toUpperCase());
    }
}
