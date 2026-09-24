using System.Collections.Generic;
using UnityEngine; // Vector2Intのみ使用

namespace Chebyss.Battle
{
    /// <summary>
    /// ナイトのL字移動・突進攻撃ルール。
    /// 通常のチェスのナイトと同様、間の駒を飛び越える（射線判定なし）。
    /// ★2以降解放される2倍L字も同じ前提で飛び越える。
    /// </summary>
    public class KnightDashRule : IMovementAttackRule
    {
        private static readonly Vector2Int[] BaseOffsets =
        {
            new(1, 2), new(2, 1), new(2, -1), new(1, -2),
            new(-1, -2), new(-2, -1), new(-2, 1), new(-1, 2)
        };

        public List<Vector2Int> GetMovableTiles(PieceSnapshot piece, BoardState board, PieceMovementPatternSO pattern)
        {
            var knightPattern = (KnightMovementPatternSO)pattern;
            var result = new List<Vector2Int>();

            for (int tier = 1; tier <= Mathf.Max(1, knightPattern.maxLMultiplier); tier++)
            {
                foreach (var offset in BaseOffsets)
                {
                    var dest = piece.position + offset * tier;
                    if (!board.IsInBounds(dest)) continue;
                    if (board.IsOccupied(dest)) continue; // 移動先は完全に空いているマスのみ
                    result.Add(dest);
                }
            }

            return result;
        }

        public List<Vector2Int> GetAttackableTiles(PieceSnapshot piece, BoardState board, PieceMovementPatternSO pattern)
        {
            var knightPattern = (KnightMovementPatternSO)pattern;
            var result = new List<Vector2Int>();

            for (int tier = 1; tier <= Mathf.Max(1, knightPattern.maxLMultiplier); tier++)
            {
                foreach (var offset in BaseOffsets)
                {
                    var dest = piece.position + offset * tier;
                    if (!board.IsInBounds(dest)) continue;

                    bool hasOpponent = piece.isPlayerSide
                        ? board.TryGetEnemyPiece(dest, out _)
                        : board.TryGetPlayerPiece(dest, out _);
                    if (hasOpponent)
                        result.Add(dest);
                }
            }

            return result;
        }

        public AttackOutcome ExecuteAttack(PieceSnapshot attacker, PieceSnapshot target, BoardState board, PieceMovementPatternSO pattern)
        {
            var knightPattern = (KnightMovementPatternSO)pattern;

            int tierUsed = DetermineTierUsed(attacker.position, target.position, knightPattern.maxLMultiplier);

            float bonusRate = attacker.statModifiers.TryGetValue("DistanceBonusRate", out var itemRate)
                ? itemRate
                : knightPattern.distanceBonusRate;

            // TODO: 距離ボーナスの正式な計算式は未確定（design doc「未決定事項」参照）。
            // 現状は 基礎攻撃力 × (1 + bonusRate × tierUsed) の仮実装。
            int damage = Mathf.RoundToInt(attacker.attack * (1f + bonusRate * tierUsed));

            bool defeated = target.currentHp - damage <= 0;

            // スタン付与はpattern.onHitEffectsの定義をそのまま使う
            var appliedEffects = new List<OnHitStatusEffect>(pattern.onHitEffects);

            // 撃破時のみ、倒した敵のマスへの追撃移動を候補として提示する。
            // 実際の移動はバトル側がプレイヤーの選択を受けてBoardState.ApplyMoveを呼ぶ。
            var pursuitCandidates = defeated
                ? new List<Vector2Int> { target.position }
                : new List<Vector2Int>();

            return new AttackOutcome(
                damage: damage,
                targetDefeated: defeated,
                appliedEffects: appliedEffects,
                knockbackDestination: null,
                pursuitMoveCandidates: pursuitCandidates,
                chainCollisionResult: null);
        }

        private static int DetermineTierUsed(Vector2Int from, Vector2Int to, int maxTier)
        {
            var delta = to - from;
            for (int tier = 1; tier <= Mathf.Max(1, maxTier); tier++)
            {
                foreach (var offset in BaseOffsets)
                {
                    if (offset * tier == delta) return tier;
                }
            }

            // TODO: 想定外の座標（L字パターンに一致しない）が渡された場合の
            // エラー処理は未確定。現状はtier=1を仮に返す。
            return 1;
        }
    }
}
