package com.lostmemory.website.global.security;

import com.lostmemory.website.admin.entity.AdminUser;
import com.lostmemory.website.admin.repository.AdminUserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.userdetails.User;
import org.springframework.security.core.userdetails.UserDetails;
import org.springframework.security.core.userdetails.UserDetailsService;
import org.springframework.security.core.userdetails.UsernameNotFoundException;
import org.springframework.stereotype.Service;

@Service
@RequiredArgsConstructor
public class AdminUserDetailsService implements UserDetailsService {

    private final AdminUserRepository adminUserRepository;

    @Override
    public UserDetails loadUserByUsername(String username) throws UsernameNotFoundException {
        AdminUser admin = adminUserRepository.findByUsername(username)
            .orElseThrow(() -> new UsernameNotFoundException("admin not found: " + username));
        return User.withUsername(admin.getUsername())
            .password(admin.getPasswordHash())
            .authorities("ROLE_ADMIN")
            .build();
    }
}
