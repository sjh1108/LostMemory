using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Talents
{
    /// <summary>
    /// 재능 투자 포인트를 PlayerPrefs에 저장하고 불러오는 서비스.
    /// 런 사이에도 유지되는 메타 성장 데이터를 담당한다.
    /// </summary>
    public static class TalentSaveService
    {
        private const string KeyPrefix = "Talent_";

        /// <summary>
        /// 재능 저장이 완료된 직후 발화. 마을에서 즉시 stat 적용에 사용 (TalentStartupApplier 구독).
        /// </summary>
        public static event Action Saved;

        /// <summary>현재 모델의 투자값을 PlayerPrefs에 저장한다.</summary>
        public static void Save(TalentModel model)
        {
            foreach (TalentType type in Enum.GetValues(typeof(TalentType)))
            {
                PlayerPrefs.SetInt(KeyPrefix + type, model.GetInvested(type));
            }
            PlayerPrefs.Save();
            Debug.Log("[TalentSaveService] 저장 완료");
            Saved?.Invoke();
        }

        /// <summary>PlayerPrefs에서 투자값을 불러온다. 저장값이 없으면 빈 딕셔너리를 반환한다.</summary>
        public static Dictionary<TalentType, int> Load()
        {
            var result = new Dictionary<TalentType, int>();
            foreach (TalentType type in Enum.GetValues(typeof(TalentType)))
            {
                var key = KeyPrefix + type;
                if (PlayerPrefs.HasKey(key))
                    result[type] = PlayerPrefs.GetInt(key);
            }
            return result;
        }

        /// <summary>저장된 모든 재능 데이터를 초기화한다.</summary>
        public static void DeleteAll()
        {
            foreach (TalentType type in Enum.GetValues(typeof(TalentType)))
            {
                PlayerPrefs.DeleteKey(KeyPrefix + type);
            }
            PlayerPrefs.Save();
            Debug.Log("[TalentSaveService] 저장 데이터 삭제 완료");
        }
    }
}
