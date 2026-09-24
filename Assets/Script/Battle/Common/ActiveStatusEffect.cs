namespace Chebyss.Battle
{
    /// <summary>
    /// 駒が実際に受けている状態異常の1件。参照型（class）。
    /// BoardState.AdvanceTurnがremainingTurnsを直接書き換える。
    /// PieceSnapshot.statusEffectsを介して外部にはIReadOnlyListとして公開されるが、
    /// 個々のインスタンス自体はclassのため中身は書き換え可能（BoardState内部の想定挙動）。
    /// </summary>
    public class ActiveStatusEffect
    {
        public StatusEffectType type;
        public int remainingTurns;
        public float magnitude;
    }
}
