using System;

namespace LostMemory.Relics
{
    /// <summary>
    /// CL-140: SetEffectApplicator 가 PlayerStatModifierContainer 등에 modifier 등록 시
    /// "출처" 식별자로 사용. RemoveBySource 매칭이 정확하려면 Equals/GetHashCode 정확 구현 필수.
    ///
    /// PlayerStatModifierContainer.RemoveBySource 는 <c>Equals(m.Source, source)</c> 로 비교 →
    /// boxing 시에도 매칭되도록 Equals(object) override + IEquatable&lt;T&gt; 둘 다 구현.
    /// </summary>
    public readonly struct SetEffectSource : IEquatable<SetEffectSource>
    {
        public readonly BuildSetData Set;
        public readonly int TierIndex;

        public SetEffectSource(BuildSetData set, int tierIndex)
        {
            Set = set;
            TierIndex = tierIndex;
        }

        public bool Equals(SetEffectSource other) =>
            ReferenceEquals(Set, other.Set) && TierIndex == other.TierIndex;

        public override bool Equals(object obj) =>
            obj is SetEffectSource s && Equals(s);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (Set != null ? Set.GetInstanceID() : 0);
                hash = hash * 31 + TierIndex;
                return hash;
            }
        }

        public override string ToString() =>
            Set != null ? $"SetEffect({Set.SetTag}, t{TierIndex})" : "SetEffect(null)";
    }
}
