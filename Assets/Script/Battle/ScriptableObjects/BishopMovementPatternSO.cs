using UnityEngine;

namespace Chebyss.Battle
{
    [CreateAssetMenu(menuName = "Chebyss/Bishop Movement Pattern", fileName = "BishopMovementPattern")]
    public class BishopMovementPatternSO : PieceMovementPatternSO
    {
        // ビショップ固有の追加フィールドは現状なし。
        // スロウの定義は基底クラスのonHitEffectsに{Slow, duration, magnitude}として設定する。
    }
}
