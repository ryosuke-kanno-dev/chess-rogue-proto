using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>
    /// 配置フェーズ（自陣3列への駒配置）を管理する。MonoBehaviourに依存しない。
    /// Phase1限定の割り切り：ナイト・ルーク・ビショップは1体ずつしか存在しないため、
    /// PieceTypeをそのまま識別子として使う。将来ポーンが複数体になったときは
    /// PieceTypeではなく駒ごとの一意なIDでの管理に作り直す必要がある（今回のスコープ外）。
    /// キングはここでは扱わない。呼び出し元がバトル開始時にboard.PlacePieceで
    /// 固定位置へあらかじめ配置しておく。
    /// </summary>
    public class PlacementController
    {
        private readonly BoardState board;
        private readonly int deploymentZoneHeight;
        private readonly Dictionary<PieceType, PieceSnapshot> pendingPieces;

        public IReadOnlyCollection<PieceType> PendingPieceTypes => pendingPieces.Keys;
        public bool IsComplete => pendingPieces.Count == 0;

        public PlacementController(BoardState board, IEnumerable<PieceSnapshot> rosterToPlace, int deploymentZoneHeight)
        {
            this.board = board;
            this.deploymentZoneHeight = deploymentZoneHeight;
            pendingPieces = rosterToPlace.ToDictionary(p => p.pieceType);
        }

        // y < deploymentZoneHeight が自陣（配置可能範囲）
        public bool IsInDeploymentZone(Vector2Int pos) =>
            board.IsInBounds(pos) && pos.y < deploymentZoneHeight;

        public PlacementResult PlacePendingPiece(PieceType type, Vector2Int pos)
        {
            if (!pendingPieces.TryGetValue(type, out var piece)) return PlacementResult.PieceNotPending;
            if (!board.IsInBounds(pos)) return PlacementResult.OutOfBounds;
            if (!IsInDeploymentZone(pos)) return PlacementResult.OutsideDeploymentZone;
            if (board.IsOccupied(pos)) return PlacementResult.TileOccupied;

            piece.position = pos;
            board.PlacePiece(piece);
            pendingPieces.Remove(type);
            return PlacementResult.Success;
        }

        // ドラッグ中の再配置・配置取り消し用。既に置いた駒を配置待ちへ戻す。
        public PlacementResult RemovePlacedPiece(PieceType type)
        {
            // キングはこのクラスの管理対象外（クラス冒頭のコメント通り）。
            // ガードがないと、盤面上の全プレイヤー駒を検索するこのメソッドが
            // 固定配置されているキングを見つけて削除・pendingPieces化してしまい、
            // 「最後列中央に固定配置」という前提が崩れる。
            if (type == PieceType.King) return PlacementResult.PieceNotPending;

            Vector2Int? foundPos = null;
            foreach (var pos in board.GetAllPlayerPiecePositionsOrdered())
            {
                if (board.TryGetPlayerPiece(pos, out var p) && p.pieceType == type)
                {
                    foundPos = pos;
                    break;
                }
            }

            if (foundPos is null) return PlacementResult.PieceNotPending;

            board.TryGetPlayerPiece(foundPos.Value, out var piece);
            board.RemovePiece(true, foundPos.Value);
            pendingPieces[type] = piece;
            return PlacementResult.Success;
        }
    }
}
