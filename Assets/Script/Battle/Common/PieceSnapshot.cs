using System.Collections.Generic;
using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>
    /// IMovementAttackRuleが参照する、駒1体分の状態スナップショット。
    /// 必ずPieceSnapshotFactory.Create()経由でのみ構築すること。
    /// statusEffectsは常にList&lt;ActiveStatusEffect&gt;の実体で構築される前提で、
    /// BoardStateが内部でList&lt;ActiveStatusEffect&gt;へキャストして書き換える。
    ///
    /// 注記：isPlayerSideは実装時に追加したフィールド。ExecuteAttack/GetAttackableTiles等が
    /// 「どちらが味方でどちらが敵か」を判断するために必須だが、これまでの確定済み設計には
    /// 含まれていなかった（BoardState側のメソッドは呼び出し元が明示的にisPlayerSideを渡す
    /// 形だったため気づきにくかった）。IMovementAttackRuleのシグネチャ自体は変更していない。
    /// </summary>
    public struct PieceSnapshot
    {
        public PieceType pieceType; // 既存コード（PieceData.cs）のPieceType enumをそのまま使用
        public bool isPlayerSide;
        public Vector2Int position;
        public int currentHp;
        public int attack; // UnitStatusDataSO基礎値 + 装備補正の合算済み（全駒共通）

        // 駒種固有の「SO基礎値 + アイテム補正」の合算値。例: "DistanceBonusRate"（ナイト用）。
        // 未設定キーは各Rule実装側でpatternの基礎値へフォールバックする。
        public IReadOnlyDictionary<string, float> statModifiers;

        public IReadOnlyList<ActiveStatusEffect> statusEffects;

        // このターン残っている行動回数。敵駒（isPlayerSide=false）では未使用（常に0）。
        public int actionsLeft;
    }
}
