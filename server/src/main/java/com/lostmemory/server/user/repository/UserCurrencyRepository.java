package com.lostmemory.server.user.repository;

import com.lostmemory.server.user.entity.UserCurrency;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface UserCurrencyRepository extends JpaRepository<UserCurrency, Long> {
    // PK = user_id 이므로 findById(userId) 가 곧 findByUserId
}
