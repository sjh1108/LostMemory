package com.lostmemory.server.global.validation;

import jakarta.validation.ConstraintValidator;
import jakarta.validation.ConstraintValidatorContext;

/**
 * {@link PasswordPolicy} 어노테이션의 실제 검증 로직.
 *
 *  - null / 길이 미달·초과 → 길이 위반 메시지
 *  - 영문·숫자·특수문자 중 하나라도 결여 → 종류 위반 메시지
 *
 * 메시지는 ConstraintViolationContext 로 동적으로 교체해 정확한 사유를 노출한다.
 */
public class PasswordPolicyValidator implements ConstraintValidator<PasswordPolicy, String> {

    private int min;
    private int max;

    @Override
    public void initialize(PasswordPolicy annotation) {
        this.min = annotation.min();
        this.max = annotation.max();
    }

    @Override
    public boolean isValid(String password, ConstraintValidatorContext context) {
        if (password == null || password.length() < min || password.length() > max) {
            replaceMessage(context, "비밀번호는 " + min + "~" + max + "자여야 합니다");
            return false;
        }
        boolean hasLetter  = password.chars().anyMatch(Character::isLetter);
        boolean hasDigit   = password.chars().anyMatch(Character::isDigit);
        boolean hasSpecial = password.chars().anyMatch(PasswordPolicyValidator::isSpecial);

        if (!(hasLetter && hasDigit && hasSpecial)) {
            replaceMessage(context, "비밀번호는 영문·숫자·특수문자를 모두 포함해야 합니다");
            return false;
        }
        return true;
    }

    /** 영문/숫자/공백이 아닌 ASCII 가시 문자만 특수문자로 인정. 한글·이모지 등은 특수로 치지 않는다. */
    private static boolean isSpecial(int ch) {
        if (Character.isLetterOrDigit(ch)) return false;
        if (Character.isWhitespace(ch)) return false;
        return ch >= 0x21 && ch <= 0x7E;
    }

    private static void replaceMessage(ConstraintValidatorContext context, String message) {
        context.disableDefaultConstraintViolation();
        context.buildConstraintViolationWithTemplate(message).addConstraintViolation();
    }
}
