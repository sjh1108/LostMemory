using System;
using System.Collections.Generic;
using UnityEngine;
using LostMemory.Talents;

namespace LostMemory.Save
{
    /// <summary>
    /// 재능 투자 세팅을 PlayerPrefs에 저장하고 불러오는 서비스.
    /// PlayerPrefs는 게임을 껐다 켜도 유지되는 Unity의 로컬 키-값 저장소이다.
    /// </summary>
    public static class TalentSaveService
    {
        // PlayerPrefs 키 접두사 — 예: "Talent_CriticalRate" = 5
        private const string KeyPrefix = "Talent_";

        /// <summary>TalentModel의 현재 투자 상태를 로컬에 저장</summary>
        public static void Save(TalentModel model)
        {
            foreach (TalentType type in Enum.GetValues(typeof(TalentType)))
            {
                PlayerPrefs.SetInt(KeyPrefix + type, model.GetInvested(type));
            }
            PlayerPrefs.Save();
        }

        /// <summary>로컬에 저장된 투자 값을 딕셔너리로 반환. 저장값 없으면 0으로 채움</summary>
        public static Dictionary<TalentType, int> LoadInvested()
        {
            var result = new Dictionary<TalentType, int>();
            foreach (TalentType type in Enum.GetValues(typeof(TalentType)))
            {
                result[type] = PlayerPrefs.GetInt(KeyPrefix + type, 0);
            }
            return result;
        }

        /// <summary>저장된 재능 데이터가 존재하는지 확인</summary>
        public static bool HasSaveData()
        {
            return PlayerPrefs.HasKey(KeyPrefix + TalentType.CriticalRate);
        }

        /// <summary>저장된 재능 데이터를 전부 삭제</summary>
        public static void DeleteSaveData()
        {
            foreach (TalentType type in Enum.GetValues(typeof(TalentType)))
            {
                PlayerPrefs.DeleteKey(KeyPrefix + type);
            }
            PlayerPrefs.Save();
        }
    }
}
