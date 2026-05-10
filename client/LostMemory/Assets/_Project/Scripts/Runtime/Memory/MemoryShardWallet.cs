using System;
using UnityEngine;

namespace LostMemory.Memory
{
    /// <summary>
    /// 이전 런들에서 누적된 영구 기억 파편 지갑.
    /// 던전 중 소비 가능한 파편과 마을 HUD는 이 값을 기준으로 한다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Lost Memory/Memory/Memory Shard Wallet")]
    public sealed class MemoryShardWallet : MonoBehaviour
    {
        public static MemoryShardWallet Instance { get; private set; }

        private MemorySaveData _saveData;

        public event Action<int> ShardsChanged;

        public MemorySaveData SaveData
        {
            get
            {
                EnsureLoaded();
                return _saveData;
            }
        }

        public int CurrentShards => SaveData.AccumulatedShards;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static MemoryShardWallet EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            GameObject walletObject = new GameObject("MemoryShardWallet");
            return walletObject.AddComponent<MemoryShardWallet>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ReloadFromDisk(notify: false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ReloadFromDisk(bool notify = true)
        {
            _saveData = MemoryMetaService.Load();
            if (notify)
            {
                ShardsChanged?.Invoke(CurrentShards);
            }
        }

        public void SaveAndNotify()
        {
            EnsureLoaded();
            MemoryMetaService.Save(_saveData);
            ShardsChanged?.Invoke(CurrentShards);
        }

        public void AddShards(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            SaveData.AccumulatedShards += amount;
            SaveAndNotify();
        }

        public bool CanSpendShards(int amount)
        {
            return amount <= 0 || CurrentShards >= amount;
        }

        public bool TrySpendShards(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (!CanSpendShards(amount))
            {
                return false;
            }

            SaveData.AccumulatedShards -= amount;
            SaveAndNotify();
            return true;
        }

        private void EnsureLoaded()
        {
            if (_saveData == null)
            {
                _saveData = MemoryMetaService.Load();
            }
        }
    }
}
