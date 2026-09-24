using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>
    /// バトル中の唯一の真実（案A確定）。MonoBehaviourに依存しない。
    /// バトル開始時にPieceData.CreateSnapshot()（PieceSnapshotFactory経由）の結果を
    /// PlacePieceで流し込んだ後は、PieceData（MonoBehaviour）は表示専用となり、
    /// 実際のHP・座標・状態異常は全てこのクラスが保持・変更する。
    /// 読み取りは公開メソッド経由、書き換えは専用メソッド（PlacePiece/ApplyMove/
    /// ApplyDefeat/ApplyAttack/AdvanceTurn）経由のみとする。
    /// </summary>
    public class BoardState
    {
        private readonly int width;
        private readonly int height;

        private readonly Dictionary<Vector2Int, PieceSnapshot> playerPieces = new();
        private readonly Dictionary<Vector2Int, PieceSnapshot> enemyPieces = new();
        private readonly List<GraveyardEntry> graveyard = new();

        public Vector2Int KingPosition { get; private set; }
        public int CurrentTurn { get; private set; }
        public IReadOnlyList<GraveyardEntry> Graveyard => graveyard;

        public BoardState(int width, int height, Vector2Int kingStartPosition)
        {
            this.width = width;
            this.height = height;
            KingPosition = kingStartPosition;
            CurrentTurn = 0;
        }

        // ---- 初期配置 ----
        // 注記：これまでの確定済み設計には明記されていなかったメソッド。
        // バトル開始時にPieceSnapshotをBoardStateへ登録する手段が存在しなかったため、
        // 実装時に必要になり追加した。許可判断（配置可能かどうか）は行わない
        // （追加確定1「移動の許可判断はBoardStateの責務外」と同じ方針）。
        public void PlacePiece(PieceSnapshot piece)
        {
            GetDict(piece.isPlayerSide)[piece.position] = piece;

            if (piece.isPlayerSide && piece.pieceType == PieceType.King)
                KingPosition = piece.position;
        }

        // ---- 読み取り ----
        public bool IsInBounds(Vector2Int pos) =>
            pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;

        public bool TryGetPlayerPiece(Vector2Int pos, out PieceSnapshot piece) =>
            playerPieces.TryGetValue(pos, out piece);

        public bool TryGetEnemyPiece(Vector2Int pos, out PieceSnapshot piece) =>
            enemyPieces.TryGetValue(pos, out piece);

        public bool IsOccupied(Vector2Int pos) =>
            playerPieces.ContainsKey(pos) || enemyPieces.ContainsKey(pos);

        // 注記：Dictionaryの列挙順は非決定的なため、座標順（x→y）にソートして返す。
        // EnemyTurnProcessorなど「毎回同じ順序で処理したい」呼び出し元向け。
        public IEnumerable<Vector2Int> GetAllPlayerPiecePositionsOrdered() =>
            playerPieces.Keys.OrderBy(p => p.x).ThenBy(p => p.y);

        public IEnumerable<Vector2Int> GetAllEnemyPiecePositionsOrdered() =>
            enemyPieces.Keys.OrderBy(p => p.x).ThenBy(p => p.y);

        // ---- 書き換え（専用メソッド経由のみ） ----
        // actionsLeftなど、駒の一部フィールドのみを更新するための単純な書き込み。
        // ApplyMove/ApplyAttackのような意味のある操作とは区別する。
        public void SetPlayerPiece(Vector2Int pos, PieceSnapshot piece) => playerPieces[pos] = piece;

        // 配置フェーズでの配置取り消し（ドラッグ中の再配置）専用。ApplyDefeatと違い墓地には送らない。
        public void RemovePiece(bool isPlayerSide, Vector2Int pos) => GetDict(isPlayerSide).Remove(pos);

        public void ApplyMove(bool isPlayerSide, Vector2Int from, Vector2Int to)
        {
            var dict = GetDict(isPlayerSide);
            var piece = dict[from];
            dict.Remove(from);
            piece.position = to;
            dict[to] = piece;

            if (isPlayerSide && piece.pieceType == PieceType.King)
                KingPosition = to;
        }

        public void ApplyDefeat(bool isPlayerSide, Vector2Int pos)
        {
            var dict = GetDict(isPlayerSide);
            if (dict.TryGetValue(pos, out var piece))
            {
                dict.Remove(pos);
                graveyard.Add(new GraveyardEntry(piece.pieceType, CurrentTurn));
            }
        }

        public void ApplyAttack(bool targetIsPlayerSide, Vector2Int targetPos, AttackOutcome outcome)
        {
            if (outcome.targetDefeated)
            {
                // 死ぬ駒にHP減算・状態異常付与をしても意味がないため、
                // 既存のApplyDefeatに委譲するだけで完結させる（削除ロジックの二重化を避ける）。
                ApplyDefeat(targetIsPlayerSide, targetPos);
                return;
            }

            var dict = GetDict(targetIsPlayerSide);
            var piece = dict[targetPos];

            piece.currentHp -= outcome.damage;

            if (outcome.appliedEffects.Count > 0)
            {
                if (piece.statusEffects is not List<ActiveStatusEffect> mutableEffects)
                    throw new InvalidOperationException(
                        "PieceSnapshot.statusEffects must be backed by List<ActiveStatusEffect>. " +
                        "Construct PieceSnapshot only via PieceSnapshotFactory.");

                foreach (var onHit in outcome.appliedEffects)
                {
                    mutableEffects.Add(new ActiveStatusEffect
                    {
                        type = onHit.type,
                        remainingTurns = onHit.duration,
                        magnitude = onHit.magnitude
                    });
                }
            }

            // ノックバックより先に書き戻す。
            // 先にApplyMoveを呼ぶと、dictに残っている「ダメージ反映前」の値を移動させてしまう。
            dict[targetPos] = piece;

            if (outcome.KnockbackOccurred)
                ApplyMove(targetIsPlayerSide, targetPos, outcome.knockbackDestination.Value);
        }

        public void AdvanceTurn()
        {
            CurrentTurn++;
            TickStatusEffects(playerPieces);
            TickStatusEffects(enemyPieces);
            ResetPlayerActionsLeft();
        }

        private void ResetPlayerActionsLeft()
        {
            foreach (var pos in new List<Vector2Int>(playerPieces.Keys))
            {
                var piece = playerPieces[pos];
                piece.actionsLeft = PieceSnapshotFactory.ComputeMaxActions(piece.statModifiers);
                playerPieces[pos] = piece;
            }
        }

        private void TickStatusEffects(Dictionary<Vector2Int, PieceSnapshot> dict)
        {
            foreach (var key in new List<Vector2Int>(dict.Keys))
            {
                var piece = dict[key];
                if (piece.statusEffects is not List<ActiveStatusEffect> mutableEffects)
                    continue;

                // 後ろから走査して、期限切れ（remainingTurns<=0）の効果を除去する。
                // 注記：これも確定済み設計にはなかった追加分。除去しないと
                // 期限切れの効果がリストに残り続け、際限なく増えてしまう。
                for (int i = mutableEffects.Count - 1; i >= 0; i--)
                {
                    mutableEffects[i].remainingTurns--;
                    if (mutableEffects[i].remainingTurns <= 0)
                        mutableEffects.RemoveAt(i);
                }
            }
        }

        private Dictionary<Vector2Int, PieceSnapshot> GetDict(bool isPlayerSide) =>
            isPlayerSide ? playerPieces : enemyPieces;
    }
}
