package com.lostmemory.server.run.entity;

import jakarta.persistence.AttributeConverter;
import jakarta.persistence.Converter;

/**
 * RunStatus enum ↔ DB 컬럼 변환기.
 * Java 측은 대문자(PROGRESS/END), DB 측은 schema.sql 의 CHECK (status IN ('progress','end')) 와 일치하도록 소문자.
 * autoApply=true — @Enumerated 와 같이 쓰지 않는다.
 */
@Converter(autoApply = true)
public class RunStatusConverter implements AttributeConverter<RunStatus, String> {

    @Override
    public String convertToDatabaseColumn(RunStatus attribute) {
        return attribute == null ? null : attribute.name().toLowerCase();
    }

    @Override
    public RunStatus convertToEntityAttribute(String dbData) {
        return dbData == null ? null : RunStatus.valueOf(dbData.toUpperCase());
    }
}
