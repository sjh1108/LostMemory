package com.lostmemory.server.run.entity;

import jakarta.persistence.AttributeConverter;
import jakarta.persistence.Converter;

/**
 * RunResultStatus enum ↔ DB 컬럼 변환기.
 * Java 측은 대문자, DB 측은 schema.sql 의 CHECK (result IN ('clear','death','surrender')) 와 일치하도록 소문자.
 */
@Converter(autoApply = true)
public class RunResultStatusConverter implements AttributeConverter<RunResultStatus, String> {

    @Override
    public String convertToDatabaseColumn(RunResultStatus attribute) {
        return attribute == null ? null : attribute.name().toLowerCase();
    }

    @Override
    public RunResultStatus convertToEntityAttribute(String dbData) {
        return dbData == null ? null : RunResultStatus.valueOf(dbData.toUpperCase());
    }
}
