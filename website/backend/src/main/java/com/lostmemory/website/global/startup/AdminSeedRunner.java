package com.lostmemory.website.global.startup;

import com.lostmemory.website.admin.entity.AdminUser;
import com.lostmemory.website.admin.repository.AdminUserRepository;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.ApplicationArguments;
import org.springframework.boot.ApplicationRunner;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

/**
 * 부팅 시 admin 계정 seed.
 * - admin.seed.username 이 존재하면 skip (재부팅 시 idempotent).
 * - 없으면 admin.seed.password 를 BCrypt 로 hash 해서 insert.
 * - 운영자가 첫 부팅 후 .env 의 ADMIN_USERNAME / ADMIN_PASSWORD 로 로그인 가능.
 * - 비밀번호 변경은 추후 admin UI 의 비밀번호 변경 기능 또는 SQL 직접 update.
 */
@Component
@RequiredArgsConstructor
@Slf4j
public class AdminSeedRunner implements ApplicationRunner {

    private final AdminUserRepository adminUserRepository;
    private final PasswordEncoder passwordEncoder;

    @Value("${admin.seed.username}")
    private String seedUsername;

    @Value("${admin.seed.password}")
    private String seedPassword;

    @Override
    @Transactional
    public void run(ApplicationArguments args) {
        if (adminUserRepository.existsByUsername(seedUsername)) {
            log.info("[AdminSeed] admin '{}' already exists — skip", seedUsername);
            return;
        }
        String hash = passwordEncoder.encode(seedPassword);
        adminUserRepository.save(new AdminUser(seedUsername, hash));
        log.info("[AdminSeed] admin '{}' seeded", seedUsername);
    }
}
