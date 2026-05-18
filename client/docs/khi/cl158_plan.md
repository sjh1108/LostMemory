# CL-158 — 활 트리 구현 (노 / 총 / 화염방사 / 유탄 + Stage 2 강화)

## Context

Epic K 두 번째 콘텐츠 ticket. **검과 다른 원거리 무기 도입 — 신규 컨트롤러 + Projectile 시스템**.

5점 P2, 클라1, CL-156 의존.

### 검 vs 활 — 핵심 차이

| 항목 | 검 (CL-157) | 활 (본 CL) |
|---|---|---|
| 공격 방식 | 평타 hitbox (인접) | **Projectile spawn** (원거리) |
| 컨트롤러 | KhiMeleeComboController | **신규 KhiBowController** |
| 데이터 | WeaponData.Steps[] hitbox | **WeaponData + ProjectileSpawn 정보** |
| 조준 | 자동 (마우스 방향 또는 가까운 적) | **WeaponAim2D 또는 자체 조준** |
| 콤보 | 1/2/3타 콤보 | **단발 + 쿨타임** (또는 챠지) |

→ **WeaponData SO 확장 + KhiBowController 신설 + Projectile prefab** 신규 작업 다수.

### 기존 상태 (확인 완료)

- ✅ `WeaponData` (CL-090) — 검 가정 구조
- ✅ `WeaponElement` / `_upgrades` / 강화 시스템 (CL-155/156)
- ✅ TopDown Engine `ProjectileWeapon` / `Projectile` / `WeaponAim2D` 사용 가능
- ❌ 활 SO + Projectile prefab — **본 CL 신설**
- ❌ KhiBowController (또는 TDE ProjectileWeapon 어댑터) — **본 CL 신설**
- ❌ WeaponData 의 발사 무기 표현 — **본 CL 신설**

### 본 CL 책임 범위 (3축)

1. **WeaponData 확장**: 발사 무기 표현 (Projectile prefab 슬롯 + 발사 파라미터)
2. **KhiBowController** 신설 — 입력 받아 projectile 발사 + 쿨타임 + 조준
3. **활 트리 7개 SO + Projectile prefab 다수** (Stage 0 ×1 + Stage 1 ×3 + Stage 2 ×3)
4. PlayerWeaponLoadout 통합 — 검 ↔ 활 전환 시 컨트롤러 활성화 분기

→ 5점 표보다 **실제 7~9시간** 예상. 분할 권장 (인프라 vs 콘텐츠).

---

## 결정사항

### 1. 활 트리 구조 (확정)

```
노 (Bow_Default, Stage 0)              _element: None      / 단발 화살
 ├─ 총 (Stage 1)                       _element: None      / 빠른 단발 + 짧은 쿨
 │   └─ 머신건 (Stage 2)               _element: None      / 더 빠른 + 다발
 ├─ 화염방사 (Stage 1)                 _element: Fire      / 근거리 지속 데미지
 │   └─ 인페르노 (Stage 2)             _element: Fire      / 화염방사 ↑ + AOE
 └─ 유탄 (Stage 1)                     _element: None      / 폭발 (AOE)
     └─ 로켓 (Stage 2)                 _element: Fire      / 큰 폭발 + Burn
```

→ Stage 2 속성 부여: 인페르노/로켓 = Fire (화염방사 분기 + 폭발 분기 모두 화염 강조).

### 2. WeaponData 확장 — 발사 무기 표현

**옵션**:
- (a) **WeaponData 에 Projectile 필드 추가** ⭐ — 단일 SO 타입 유지
- (b) ProjectileWeaponData 별도 SO + 공통 부모
- (c) WeaponData 추상 + 상속

**채택: (a)**.
- 단순 (단일 타입, 검과 활 모두 같은 SO)
- Inspector 분기: _attackKind enum 으로 멜리/프로젝타일 구분
- 미사용 필드는 hidden (PropertyDrawer 옵션)

```csharp
public enum WeaponAttackKind { Melee, Projectile }

[Header("Attack Kind (CL-158)")]
[SerializeField] private WeaponAttackKind _attackKind = WeaponAttackKind.Melee;

[Header("Projectile (used when AttackKind == Projectile)")]
[SerializeField] private GameObject _projectilePrefab;
[SerializeField, Min(0f)] private float _projectileSpeed = 12f;
[SerializeField, Min(0f)] private float _fireCooldown = 0.5f;
[SerializeField, Min(1)] private int _projectilesPerShot = 1;        // 산탄 (총 = 1, 화염방사 = 3)
[SerializeField, Range(0f, 90f)] private float _spreadAngle = 0f;    // 산탄 각도
[SerializeField, Min(0f)] private float _projectileLifetime = 3f;
[SerializeField, Min(0f)] private float _projectileDamage = 5f;       // _baseDamage 와 별도 (발사 데미지)
[SerializeField, Min(0f)] private float _explosionRadius = 0f;        // 0 이면 폭발 X
[SerializeField, Min(0f)] private float _explosionDamage = 0f;

public WeaponAttackKind AttackKind => _attackKind;
public GameObject ProjectilePrefab => _projectilePrefab;
public float ProjectileSpeed => _projectileSpeed;
public float FireCooldown => _fireCooldown;
public int ProjectilesPerShot => _projectilesPerShot;
public float SpreadAngle => _spreadAngle;
public float ProjectileLifetime => _projectileLifetime;
public float ProjectileDamage => _projectileDamage;
public float ExplosionRadius => _explosionRadius;
public float ExplosionDamage => _explosionDamage;
```

### 3. KhiBowController 신설

```csharp
public class KhiBowController : MonoBehaviour
{
    [SerializeField] private WeaponData _weaponData;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private LayerMask _enemyLayers;

    private float _nextFireAt;

    public void SetWeaponData(WeaponData data) => _weaponData = data;

    private void Update()
    {
        if (Input.GetButton("Attack") && Time.time >= _nextFireAt)
            Fire();
    }

    private void Fire()
    {
        Vector2 aim = ResolveAimDirection();   // 마우스 또는 가장 가까운 적
        for (int i = 0; i < _weaponData.ProjectilesPerShot; i++)
        {
            float angleOffset = _weaponData.ProjectilesPerShot > 1
                ? Mathf.Lerp(-_weaponData.SpreadAngle / 2f, _weaponData.SpreadAngle / 2f,
                             (float)i / (_weaponData.ProjectilesPerShot - 1))
                : 0f;
            Vector2 dir = Quaternion.Euler(0, 0, angleOffset) * aim;
            SpawnProjectile(dir);
        }
        _nextFireAt = Time.time + _weaponData.FireCooldown;
    }

    private void SpawnProjectile(Vector2 dir)
    {
        var proj = Instantiate(_weaponData.ProjectilePrefab, _firePoint.position, Quaternion.identity);
        var p = proj.GetComponent<KhiProjectile>();
        p.Init(dir * _weaponData.ProjectileSpeed,
               _weaponData.ProjectileDamage,
               _weaponData.ProjectileLifetime,
               _weaponData.ExplosionRadius,
               _weaponData.ExplosionDamage,
               _weaponData.Element);
    }
}
```

→ TopDown Engine `ProjectileWeapon` 직접 사용도 검토했으나 **자체 컨트롤러** 채택 (커스텀 자유도, KhiMeleeComboController 와 일관 패턴).

### 4. KhiProjectile 컴포넌트

```csharp
public class KhiProjectile : MonoBehaviour
{
    private Vector2 _velocity;
    private float _damage, _lifetime, _explosionRadius, _explosionDamage;
    private WeaponElement _element;
    private float _spawnedAt;

    public void Init(Vector2 vel, float dmg, float life, float exR, float exD, WeaponElement el)
    {
        _velocity = vel; _damage = dmg; _lifetime = life;
        _explosionRadius = exR; _explosionDamage = exD; _element = el;
        _spawnedAt = Time.time;
    }

    private void Update()
    {
        transform.position += (Vector3)(_velocity * Time.deltaTime);
        if (Time.time - _spawnedAt > _lifetime) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var health = other.GetComponentInParent<Health>();
        if (health == null) return;

        if (_explosionRadius > 0)
        {
            // 폭발: 반경 내 모든 적
            var hits = Physics2D.OverlapCircleAll(transform.position, _explosionRadius);
            foreach (var h in hits)
            {
                var hh = h.GetComponentInParent<Health>();
                if (hh != null) hh.Damage(_explosionDamage, gameObject, ...);
            }
        }
        else
        {
            health.Damage(_damage, gameObject, ...);
        }

        // 무기 속성 OnHit 발동 (CL-155 패턴)
        if (_element != WeaponElement.None)
        {
            // OnHitEffectRegistry 호출 — 컨트롤러로 위임 또는 직접
        }

        Destroy(gameObject);
    }
}
```

### 5. PlayerWeaponLoadout 통합 — 컨트롤러 분기

CL-156 의 PlayerWeaponLoadout 수정:

```csharp
public bool TryUpgrade(WeaponData target, bool free = false)
{
    if (!CanUpgradeTo(target)) return false;
    if (!free) wallet.Spend(target.UpgradeCost);

    var old = _currentWeapon;
    _currentWeapon = target;

    // 컨트롤러 분기
    if (target.AttackKind == WeaponAttackKind.Melee)
    {
        meleeController.SetWeaponData(target);
        meleeController.enabled = true;
        bowController.enabled = false;
    }
    else
    {
        bowController.SetWeaponData(target);
        bowController.enabled = true;
        meleeController.enabled = false;
    }

    OnWeaponChanged?.Invoke(old, target);
    return true;
}
```

→ 두 컨트롤러 둘 다 플레이어에 부착. 활성화만 toggle.

### 6. 무기별 수치 (디폴트)

| 무기 | Stage | Element | ProjPerShot | Spread | Cooldown | ProjDmg | ExplosionR | ExplosionDmg | UpgradeCost |
|---|---|---|---|---|---|---|---|---|---|
| **노** | 0 | None | 1 | 0° | 0.6s | 8 | 0 | 0 | 0 |
| **총** | 1 | None | 1 | 0° | 0.25s | 6 | 0 | 0 | 100 |
| **화염방사** | 1 | Fire | 3 | 30° | 0.15s | 3 | 0 | 0 | 100 |
| **유탄** | 1 | None | 1 | 0° | 1.2s | 8 | 1.5 | 12 | 100 |
| **머신건** | 2 | None | 1 | 5° | 0.10s | 5 | 0 | 0 | 300 |
| **인페르노** | 2 | Fire | 5 | 45° | 0.10s | 4 | 0 | 0 | 300 |
| **로켓** | 2 | Fire | 1 | 0° | 1.5s | 12 | 2.5 | 20 | 300 |

→ DPS 균형:
- 노: 8 / 0.6 ≈ 13 DPS
- 총: 6 / 0.25 = 24 DPS (단발 ↑)
- 화염방사: 3×3 / 0.15 = 60 DPS 단, 적중률 + Spread 고려 시 절반 이하
- 유탄: 8+12(폭발) / 1.2 ≈ 17 DPS 단발 (광역 보너스)
- Stage 2 모두 DPS ↑

### 7. Projectile prefab — 4종 (시각 differentiation)

| Prefab | 사용 무기 | 특징 |
|---|---|---|
| `Projectile_Arrow.prefab` | 노 | 화살 sprite, 직선 |
| `Projectile_Bullet.prefab` | 총, 머신건 | 작은 점 sprite |
| `Projectile_FireBlast.prefab` | 화염방사, 인페르노 | 빨간 파티클, 짧은 사거리 |
| `Projectile_Grenade.prefab` | 유탄, 로켓 | 둥근 sprite, 폭발 시 ParticleSystem |

→ placeholder sprite. 정식 시각 별도 ticket.

### 8. 조준 — 마우스 방향

**옵션**:
- (a) **마우스 방향** ⭐ — 화면 마우스 좌표 → 플레이어 → projectile 방향
- (b) 가장 가까운 적 자동 조준 (auto-aim)
- (c) 입력 방향 (WASD)

**채택: (a)**. 가장 직관적.
- WASD 이동 + 마우스 조준 표준 (트윈스틱)
- (b) 는 보조 옵션 (별도 ticket)

```csharp
private Vector2 ResolveAimDirection()
{
    Vector3 mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
    return ((Vector2)(mouse - transform.position)).normalized;
}
```

### 9. 활 컨트롤러 in 검 모드 처리

플레이어가 검 보유 시 활 컨트롤러 비활성화. 입력 충돌 X.

```csharp
// Awake
bowController.enabled = (currentWeapon.AttackKind == WeaponAttackKind.Projectile);
meleeController.enabled = (currentWeapon.AttackKind == WeaponAttackKind.Melee);
```

→ TryUpgrade 시 동적 toggle (§5).

### 10. KhiAttackVisualPresenter / SlashRig — 활 모드

활은 slashFrames 안 씀. AttackVisualPresenter 가 검 모드에서만 작동하도록:
- `if (weapon.AttackKind == Melee) presenter.Play(...)` 가드

→ KhiAttackVisualPresenter 수정 또는 활성화 toggle.

---

## 핵심 파일

### 신규

| 경로 | 내용 |
|---|---|
| `Runtime/Combat/KhiBowController.cs` | 입력 + 발사 + 쿨타임 |
| `Runtime/Combat/KhiProjectile.cs` | 투사체 동작 + 충돌 |
| `Runtime/Data/WeaponAttackKind.cs` | enum (Melee/Projectile) |
| `ScriptableObjects/Weapons/Bow_Default.asset` | 노 (Stage 0) |
| `ScriptableObjects/Weapons/Bow_Gun.asset` | 총 (Stage 1) |
| `ScriptableObjects/Weapons/Bow_Flamethrower.asset` | 화염방사 (Stage 1, Fire) |
| `ScriptableObjects/Weapons/Bow_Grenade.asset` | 유탄 (Stage 1) |
| `ScriptableObjects/Weapons/Bow_MachineGun.asset` | 머신건 (Stage 2) |
| `ScriptableObjects/Weapons/Bow_Inferno.asset` | 인페르노 (Stage 2, Fire) |
| `ScriptableObjects/Weapons/Bow_Rocket.asset` | 로켓 (Stage 2, Fire) |
| `Prefabs/Projectiles/Projectile_Arrow.prefab` | 화살 |
| `Prefabs/Projectiles/Projectile_Bullet.prefab` | 총알 |
| `Prefabs/Projectiles/Projectile_FireBlast.prefab` | 화염 |
| `Prefabs/Projectiles/Projectile_Grenade.prefab` | 폭발물 |
| `docs/khi/cl158_bow_tree_balance.md` | 수치 표 + DPS 가이드 |

### 수정

| 경로 | 변경 |
|---|---|
| `WeaponData.cs` | `_attackKind` + `_projectile*` 필드 (10개) + 프로퍼티 |
| `PlayerWeaponLoadout.cs` (CL-156) | `bowController` 참조 + 컨트롤러 toggle 분기 |
| `KhiAttackVisualPresenter` (CL-090) | AttackKind == Melee 가드 추가 |

---

## 구현 단계

### 작업 단위 A — 인프라 (3시간)

#### 1단계: WeaponAttackKind enum + WeaponData 확장 (30분)

§2 코드. enum + 10개 필드.

#### 2단계: KhiProjectile (1시간)

§4 코드. Update 이동 + OnTriggerEnter2D 충돌 + 폭발 처리.

폭발 시각: ParticleSystem 활성화 또는 단순 Color flash.

#### 3단계: KhiBowController (1시간)

§3 코드. Input 처리 + 산탄 발사 + 쿨타임.

#### 4단계: PlayerWeaponLoadout 통합 (30분)

§5 코드. 컨트롤러 toggle.

플레이어 prefab 에 KhiBowController + firePoint 추가.

### 작업 단위 B — 콘텐츠 (3시간)

#### 5단계: Projectile prefab 4종 (45분)

각 prefab:
- SpriteRenderer + 작은 sprite
- Collider2D (Trigger)
- Rigidbody2D (Kinematic)
- KhiProjectile 컴포넌트

#### 6단계: 활 트리 SO 7개 (1시간 30분, 각 13분)

§6 표 그대로 입력. 신규 SO 6개 + Bow_Default 신규.

각 SO _attackKind = Projectile 명시. 검 트리 SO 들은 _attackKind = Melee 확인.

#### 7단계: _upgrades 트리 입력 (15분)

```
Bow_Default → [Bow_Gun, Bow_Flamethrower, Bow_Grenade]
Bow_Gun → [Bow_MachineGun]
Bow_Flamethrower → [Bow_Inferno]
Bow_Grenade → [Bow_Rocket]
```

#### 8단계: 수치 가이드 문서 (30분)

`cl158_bow_tree_balance.md`. CL-161 QA 인풋.

### 작업 단위 C — 검증 (1시간 30분)

```
시나리오 1: 시작 무기 검 → 평타 정상
시나리오 2: 활로 강화 (디버그)
- 검 → 활은 트리 분기 X (다른 무기 종류). 본 plan: 디버그 메뉴로 직접 weapon swap
시나리오 3: 노 (활) — 단발 화살 발사
- 마우스 방향으로 화살 발사
- 적 충돌 시 데미지 8
시나리오 4: 총 → 빠른 단발
시나리오 5: 화염방사 → 산탄 3 + Spread 30°
- BurnOnHit 발동 (Fire 속성)
시나리오 6: 유탄 → 폭발
- 적 충돌 시 폭발 (반경 1.5)
- 폭발 데미지 12 적용
시나리오 7: 머신건 → Spread 5° + 빠른 발사
시나리오 8: 인페르노 → 큰 산탄
시나리오 9: 로켓 → 큰 폭발 + Burn
시나리오 10: 컨트롤러 toggle
- 활 → 검 강화 (또는 swap) 시 검 컨트롤러 활성화 + 활 비활성
```

---

## 위험 / 결정 미정

### 위험

1. **검 → 활 강화 트리 X**: 본 CL 의 활 트리는 검 트리와 별도. PlayerWeaponLoadout._currentWeapon = 검 시작 정책 (CL-156) 면 활 사용 흐름 X. → **시작 무기 선택 메뉴 별도 ticket** 또는 **디버그 메뉴 weapon swap** 본 CL 포함.
2. **WeaponData 가 두 종류 책임**: 검 가정 필드 + 활 필드 한 SO 에 몰림. Inspector 복잡. → PropertyDrawer 옵션 (AttackKind 따라 hide), 후속 ticket.
3. **KhiBowController vs KhiMeleeComboController 입력 충돌**: 같은 "Attack" 버튼. 활성화 toggle 로 회피하지만 테스트 중 두 개 모두 enabled 면 양쪽 발동. → Awake 가드 + TryUpgrade 시 toggle 보장.
4. **Projectile 충돌 layer 설정**: 적 layer 만 충돌, 플레이어 layer 무시. Physics2D Layer Collision Matrix 점검.
5. **폭발 적 친구 데미지**: Friendly fire X 보장 — KhiProjectile 의 OnTriggerEnter2D 가 Health.gameObject.layer 검사.
6. **OnHitEffectRegistry 통합 — Projectile**: 활의 BurnOnHit 발동을 어떻게? KhiProjectile 이 OnHitEffectRegistry 참조 받아서 직접 호출, 또는 이벤트 발화 후 컨트롤러가 처리. → **MVP: KhiProjectile 이 직접 호출** (단순).
7. **시각: slashFrames 미사용 → AttackVisualPresenter NRE 위험**: 활 SO 의 slashFrames 가 빈 배열 → NRE 가능. AttackVisualPresenter 가 가드 (§10).
8. **TopDown Engine ProjectileWeapon 미사용 결정**: 자체 구현 선택. TDE 가 더 풍부하지만 학습 비용 + 통합 복잡도 ↑. → 자체 단순 구현 우선, 후속에서 필요 시 마이그레이션.
9. **5점 ticket 표 초과**: 인프라 + 콘텐츠 + 검증 = 7~8시간. **분할 권장** (작업 단위 A/B/C).

### 결정 미정

- [ ] WeaponData 단일 vs 분리 — 본 plan: **단일 + AttackKind enum**
- [ ] Projectile 컨트롤러 — 자체 / TDE — 본 plan: **자체** (커스텀)
- [ ] 조준 — 마우스 / auto-aim — 본 plan: **마우스**
- [ ] 검 → 활 swap 흐름 — 본 plan: **디버그 메뉴 + 시작 선택 별도 ticket**
- [ ] Stage 2 속성 부여 — 본 plan: **인페르노/로켓 = Fire, 머신건 = None**
- [ ] 폭발 friendly fire — 본 plan: **OFF**
- [ ] PropertyDrawer (AttackKind 분기 hide) — 본 plan: **별도 ticket**

---

## 후속 ticket 영향

| Ticket | CL-158 과의 관계 |
|---|---|
| **CL-159 (봉 트리)** | 검 패턴 (근접) 따라감. 본 CL 의 Projectile 인프라 영향 X |
| **CL-160 (스태프 트리)** | **본 CL Projectile 인프라 활용** + 스킬 2종 추가 |
| **CL-161 (무기 QA)** | 활 트리 7개 밸런스 검증 (DPS 계산) |
| **별도 ticket: 시작 무기 선택 메뉴** | 검 / 활 / 봉 / 스태프 4종 선택 |
| **별도 ticket: WeaponData PropertyDrawer** | AttackKind 따라 필드 hide |
| **별도 ticket: 정식 Projectile sprite** | placeholder → 정식 |
| **별도 ticket: TDE ProjectileWeapon 마이그레이션** | 자체 → TDE |
| **별도 ticket: auto-aim 옵션** | 마우스 + auto-aim 토글 |

---

## 예상 시간

### 작업 단위 A (인프라) — 약 3시간
| 단계 | 시간 |
|---|---|
| 1단계 (WeaponData 확장) | 30분 |
| 2단계 (KhiProjectile) | 1시간 |
| 3단계 (KhiBowController) | 1시간 |
| 4단계 (Loadout 통합) | 30분 |

### 작업 단위 B (콘텐츠) — 약 3시간
| 단계 | 시간 |
|---|---|
| 5단계 (Projectile prefab 4종) | 45분 |
| 6단계 (활 SO 7개) | 1시간 30분 |
| 7단계 (_upgrades 입력) | 15분 |
| 8단계 (수치 가이드 문서) | 30분 |

### 작업 단위 C (검증) — 약 1시간 30분
| 단계 | 시간 |
|---|---|
| 검증 10 시나리오 | 1시간 30분 |

| **합계** | **약 7시간 30분** |

→ **5점 ticket 표 초과**. 분할 권장 (A 단독 PR + B+C 단독 PR).

---

## 결정 요청

| # | 질문 | 옵션 | 추천 |
|---|---|---|---|
| 1 | WeaponData 분리 | **단일 + enum** / 별도 SO | **단일** |
| 2 | Projectile 컨트롤러 | **자체** / TDE | **자체** |
| 3 | 조준 | **마우스** / auto-aim | **마우스** |
| 4 | Stage 2 속성 | 인페르노/로켓 Fire / 모두 별도 | **본 plan §1** |
| 5 | 폭발 friendly fire | **OFF** / ON | **OFF** |
| 6 | 분할 PR | A 단독 + B+C / **단일** | **분할 권장** |
| 7 | 검 ↔ 활 swap | **디버그 메뉴 + 별도 ticket** / 본 CL 정식 | **디버그** |

전부 추천대로면 **단일 + 자체 + 마우스 + 본plan + OFF + 분할 + 디버그**.

---

## Epic K 진행률 (CL-158 후)

| Ticket | Plan |
|---|---|
| CL-090 무기 SO | ✅ |
| CL-155 4속성 + 결합 | ✅ |
| CL-156 단계 강화 | ✅ |
| CL-157 검 트리 | ✅ |
| **CL-158 활 트리** | ✅ ← 방금 |
| CL-159 봉 트리 | ⏳ |
| CL-160 스태프 트리 | ⏳ |
| CL-161 무기 QA | ⏳ |

**Epic K: 4/7**

---

## 다음 plan

| 옵션 | Ticket | 점수 | 비고 |
|---|---|---|---|
| **A** | CL-159 봉 트리 | 5점 | 검 패턴 반복. 가장 빠름 |
| B | CL-160 스태프 트리 | 5점 | 활 인프라 활용 + 스킬 2종 (가장 무거움) |
| C | CL-161 무기 QA | 2점 | CL-159/160 후 |
| D | Epic U 진입 | - | 디자이너 도구 (병렬) |

**추천: A (CL-159 봉 트리)** — 검 패턴 반복이라 가장 빠름. 봉/창/망치 3종 + Stage 2. 본 CL 패턴 익숙해진 상태에서 가장 효율적.

뭐로 갈까요?
