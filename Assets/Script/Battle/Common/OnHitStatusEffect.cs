using System;

namespace Chebyss.Battle
{
    /// <summary>
    /// PieceMovementPatternSO（および派生クラス）が持つ、
    /// 「攻撃が命中したときに何を付与するか」の定義データ。
    /// ランタイムの実インスタンスはActiveStatusEffectが担う。
    /// </summary>
    [Serializable]
    public class OnHitStatusEffect
    {
        public StatusEffectType type;
        public int duration;    // 付与ターン数
        public float magnitude; // Stunでは未使用。Slowでは移動可能マス削減量など
    }
}
