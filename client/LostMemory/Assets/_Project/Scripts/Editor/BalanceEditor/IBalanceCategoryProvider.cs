using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor
{
    public interface IBalanceCategoryProvider
    {
        string CategoryName { get; }

        /// <summary>
        /// AssetDatabase.FindAssets 의 "t:" 필터로 사용. 예: "RelicData".
        /// 문자열 기반 — Editor asmdef 가 Assembly-CSharp 의 SO 타입을 직접 참조하지 않기 위함.
        /// </summary>
        string AssetTypeFilter { get; }

        IEnumerable<ScriptableObject> LoadAll();
    }
}
