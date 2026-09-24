using UnityEngine;

namespace Chebyss.Battle
{
    [CreateAssetMenu(menuName = "Chebyss/Knight Movement Pattern", fileName = "KnightMovementPattern")]
    public class KnightMovementPatternSO : PieceMovementPatternSO
    {
        [Tooltip("1 = 標準L字のみ、2 = 2倍L字まで解放（★2以降）")]
        public int maxLMultiplier = 1;

        [Tooltip("基礎の距離ボーナス係数。アイテムによる補正はPieceSnapshot.statModifiers[\"DistanceBonusRate\"]側で扱い、この値自体は書き換えない")]
        public float distanceBonusRate;
    }
}
