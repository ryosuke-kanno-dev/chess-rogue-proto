using System.Collections.Generic;
using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>
    /// 駒種ごとの移動・攻撃パターンの抽象基底クラス。
    /// 駒種固有のフィールドはサブクラス（KnightMovementPatternSO等）に持たせ、
    /// このクラスには全駒共通のフィールドのみを置く。
    /// フィールドは全て「基礎値・不変」として扱い、ランタイムで書き換えないこと。
    /// アイテムによる補正はPieceData/PieceSnapshot.statModifiers側で管理する。
    /// </summary>
    public abstract class PieceMovementPatternSO : ScriptableObject
    {
        public PieceType pieceType; // 既存コード（PieceData.cs）のPieceType enumをそのまま使用

        [Tooltip("射程の上限マス数。0 = 無制限（ルーク・ビショップ用。ナイトは未使用）")]
        public int moveRange;

        [Tooltip("この駒の攻撃が命中したときに付与する状態異常の定義")]
        public List<OnHitStatusEffect> onHitEffects = new();
    }
}
