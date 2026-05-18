# Git Merge 충돌 해결 Plan

## Context

현재 `무기조정` 브랜치에서 머지가 진행 중이며 충돌(unmerged paths) 3건이 남아있다.
나머지 변경분(Map prefab, RoomData 리네이밍 등)은 이미 자동 머지/스테이지된 상태이므로,
이 plan은 남은 3개의 충돌만 정확히 해결하여 머지 커밋을 만들기 위함이다.

스테이지 상태에서 확인된 충돌:

| 상태 | 경로 |
|------|------|
| both added | `LostMemory/Assets/_Project/Scenes/Test/weapontest_khi.unity` |
| both added | `LostMemory/Assets/_Project/ScriptableObjects/Weapon/Sword_Dagger.asset` |
| deleted by us | `LostMemory/Assets/_Project/ScriptableObjects/Rooms/1F/RoomData_Combat_1F_1R_2.asset.meta` |

## 사용자 결정 사항

| 파일 | 해결 방향 |
|------|----------|
| `weapontest_khi.unity` | **ours** (현재 `무기조정` 브랜치 버전 유지) |
| `Sword_Dagger.asset` | **ours** (현재 `무기조정` 브랜치 버전 유지 — 단검 수치 작업이 들어 있음) |
| `RoomData_Combat_1F_1R_2.asset.meta` | **theirs 복원** (상대 브랜치가 수정한 `.meta` 를 받아들여 `git add`) |

## 실행 단계

### 1) ours 채택: weapontest_khi.unity

```powershell
git checkout --ours LostMemory/Assets/_Project/Scenes/Test/weapontest_khi.unity
git add LostMemory/Assets/_Project/Scenes/Test/weapontest_khi.unity
```

### 2) ours 채택: Sword_Dagger.asset

```powershell
git checkout --ours LostMemory/Assets/_Project/ScriptableObjects/Weapon/Sword_Dagger.asset
git add LostMemory/Assets/_Project/ScriptableObjects/Weapon/Sword_Dagger.asset
```

### 3) theirs 복원: RoomData_Combat_1F_1R_2.asset.meta

`deleted by us` 상태이므로 working tree 에 해당 파일이 없다. 상대 브랜치 버전을 꺼내서 스테이지한다.

```powershell
git checkout --theirs -- LostMemory/Assets/_Project/ScriptableObjects/Rooms/1F/RoomData_Combat_1F_1R_2.asset.meta
git add LostMemory/Assets/_Project/ScriptableObjects/Rooms/1F/RoomData_Combat_1F_1R_2.asset.meta
```

> 만약 `git checkout --theirs` 가 실패하면 (양쪽 stage 가 모두 없는 경우):
> `git show MERGE_HEAD:LostMemory/Assets/_Project/ScriptableObjects/Rooms/1F/RoomData_Combat_1F_1R_2.asset.meta > <경로>` 로 내용을 복원 후 `git add`.

### 4) 머지 커밋 생성

> **주의**: 메모리 정책에 따라 커밋은 **사용자가 직접 수행**한다. 자동으로 `git commit` 을 실행하지 않는다.

스테이지 상태가 깨끗하면 사용자가 직접:

```powershell
git commit         # 머지 메시지 기본값 사용
# 또는
git commit -m "Merge ... 충돌 해결 (weapontest/Sword_Dagger ours, RoomData meta theirs)"
```

## Verification

각 단계 후 확인 명령:

1. **충돌 0건 검증**

   ```powershell
   git status
   ```
   - "Unmerged paths" 섹션이 사라져야 한다.
   - 모든 변경이 "Changes to be committed" 아래로 이동해야 한다.

2. **각 결정이 정확히 반영됐는지 검증**

   ```powershell
   git diff --staged --name-only | Select-String "weapontest_khi|Sword_Dagger|RoomData_Combat_1F_1R_2"
   git ls-files --stage | Select-String "weapontest_khi|Sword_Dagger|RoomData_Combat_1F_1R_2"
   ```
   - `ls-files --stage` 결과에 stage 1/2/3 마커(충돌)가 더 이상 없어야 한다 (stage 0 만 존재).

3. **Unity 측 무결성 (커밋 전 권장)**
   - Unity Editor 를 열어 `weapontest_khi` 씬을 로드해 missing reference / 컴파일 에러 없는지 확인.
   - `Sword_Dagger.asset` Inspector 에서 단검 수치(공격력/속도)가 의도한 값인지 확인.
   - 1F Room 데이터: `RoomData_Combat_1F_1R_2.asset` 가 Inspector 에서 정상 표시되는지 확인 (`.meta` GUID 가 asset 과 연결).

4. **충돌 해결이 잘못됐다고 판단되면 롤백**

   ```powershell
   git merge --abort
   ```

## Critical Files

| 파일 | 동작 |
|------|------|
| `LostMemory/Assets/_Project/Scenes/Test/weapontest_khi.unity` | ours 채택 |
| `LostMemory/Assets/_Project/ScriptableObjects/Weapon/Sword_Dagger.asset` | ours 채택 |
| `LostMemory/Assets/_Project/ScriptableObjects/Rooms/1F/RoomData_Combat_1F_1R_2.asset.meta` | theirs 채택 |
