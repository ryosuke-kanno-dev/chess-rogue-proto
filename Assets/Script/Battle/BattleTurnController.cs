using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>
    /// プレイヤーターン⇔敵ターンの進行と、駒1体分の行動（移動・攻撃・待機・追撃移動）を
    /// 管理するステートマシン。MonoBehaviourに依存しない（バトル全体をシーンなしで
    /// シミュレート・テストできる状態を保つため）。
    /// 移動・攻撃対象マスの妥当性チェックはこのクラスの責務（BoardStateは許可判断をしない）。
    /// ターン終了は「いつでも可能」（未行動の駒はそのまま何もしない。次ターンでactionsLeftがリセットされる）。
    /// </summary>
    public class BattleTurnController
    {
        private readonly BoardState board;
        private readonly EnemyTurnProcessor enemyTurnProcessor;
        private readonly IReadOnlyDictionary<PieceType, IMovementAttackRule> playerRules;
        private readonly IReadOnlyDictionary<PieceType, PieceMovementPatternSO> playerPatterns;

        public BattlePhase CurrentPhase { get; private set; } = BattlePhase.PlayerTurn;
        public BattleDamageLog DamageLog { get; } = new BattleDamageLog();
        public BattleExperienceLedger ExperienceLedger { get; } = new BattleExperienceLedger();

        public EnemyTurnProcessor EnemyTurnProcessor { get; }

        private readonly PawnGrowthRules growthRules;

        public BattleTurnController(
            BoardState board,
            EnemyTurnProcessor enemyTurnProcessor,
            IReadOnlyDictionary<PieceType, IMovementAttackRule> playerRules,
            IReadOnlyDictionary<PieceType, PieceMovementPatternSO> playerPatterns,
            PawnGrowthRules growthRules)
        {
            this.board = board;
            this.enemyTurnProcessor = enemyTurnProcessor;
            EnemyTurnProcessor = enemyTurnProcessor;
            this.growthRules = growthRules;
            this.playerRules = playerRules;
            this.playerPatterns = playerPatterns;
            // 1ターン目はAdvanceTurnを呼ばない（tickするものがまだ何もないため）
        }

        public List<Vector2Int> GetMovableTiles(Vector2Int piecePos)
        {
            if (!TryGetActingPlayerPiece(piecePos, out var piece, out var rule, out var pattern))
                return new List<Vector2Int>();
            return rule.GetMovableTiles(piece, board, pattern);
        }

        public List<Vector2Int> GetAttackableTiles(Vector2Int piecePos)
        {
            if (!TryGetActingPlayerPiece(piecePos, out var piece, out var rule, out var pattern))
                return new List<Vector2Int>();
            return rule.GetAttackableTiles(piece, board, pattern);
        }

        public ActionResult ExecuteMove(Vector2Int from, Vector2Int to)
        {
            if (CurrentPhase != BattlePhase.PlayerTurn) return ActionResult.WrongPhase;
            if (!board.TryGetPlayerPiece(from, out var piece)) return ActionResult.InvalidTarget;
            if (piece.actionsLeft <= 0) return ActionResult.NoActionsLeft;
            if (!GetMovableTiles(from).Contains(to)) return ActionResult.InvalidTarget;

            board.ApplyMove(true, from, to);
            ConsumeAction(to);
            return ActionResult.Success;
        }

        public ActionResult ExecuteAttack(Vector2Int attackerPos, Vector2Int targetPos, out AttackOutcome outcome)
        {
            outcome = default;
            if (CurrentPhase != BattlePhase.PlayerTurn) return ActionResult.WrongPhase;
            if (!board.TryGetPlayerPiece(attackerPos, out var attacker)) return ActionResult.InvalidTarget;
            if (attacker.actionsLeft <= 0) return ActionResult.NoActionsLeft;
            if (!GetAttackableTiles(attackerPos).Contains(targetPos)) return ActionResult.InvalidTarget;
            if (!board.TryGetEnemyPiece(targetPos, out var target)) return ActionResult.InvalidTarget;

            if (!playerRules.TryGetValue(attacker.pieceType, out var rule) ||
                !playerPatterns.TryGetValue(attacker.pieceType, out var pattern))
            {
                return ActionResult.InvalidTarget;
            }
            outcome = rule.ExecuteAttack(attacker, target, board, pattern);

            board.ApplyAttack(false, targetPos, outcome);
            DamageLog.Record(attacker.instanceId, target.instanceId, outcome.damage, outcome.targetDefeated);
            ConsumeAction(attackerPos);

            if (outcome.targetDefeated)
            {
                AwardExperienceForKill(target);
                if (!board.GetAllEnemyPiecePositionsOrdered().Any())
                    CurrentPhase = BattlePhase.Victory;
            }

            return ActionResult.Success;
        }

        // 追撃移動：actionsLeftを消費しない別経路（確定済み方針）
        public ActionResult ExecutePursuitMove(Vector2Int knightPos, Vector2Int destination)
        {
            if (CurrentPhase != BattlePhase.PlayerTurn) return ActionResult.WrongPhase;
            board.ApplyMove(true, knightPos, destination);
            return ActionResult.Success;
        }

        public ActionResult Wait(Vector2Int piecePos)
        {
            if (CurrentPhase != BattlePhase.PlayerTurn) return ActionResult.WrongPhase;
            if (!board.TryGetPlayerPiece(piecePos, out _)) return ActionResult.InvalidTarget;
            ConsumeAction(piecePos, consumeAll: true);
            return ActionResult.Success;
        }

        public void EndPlayerTurn()
        {
            if (CurrentPhase != BattlePhase.PlayerTurn) return;

            CurrentPhase = BattlePhase.EnemyTurn;
            var result = enemyTurnProcessor.Process(board);

            if (result.kingDefeated)
            {
                CurrentPhase = BattlePhase.Defeat;
                return;
            }

            board.AdvanceTurn(); // ターン数+1・状態異常tick・プレイヤー駒のactionsLeftリセット
            CurrentPhase = BattlePhase.PlayerTurn;
        }

        // 撃破した敵のmaxHpを基準に、ダメージ貢献度で按分した経験値＋トドメボーナスを配る。
        // 今は固定1戦しかなく「バトルをまたいで持ち越す」概念が無いため、
        // 閾値に達した時点でその場で即座に進化させる（Phase3でラン構造ができたら、
        // バトル終了時にboard.WasDefeatedThisBattle()で生存者だけに確定させる形へ見直す）。
        private void AwardExperienceForKill(PieceSnapshot target)
        {
            int totalDamage = 0;
            foreach (var r in DamageLog.Records)
                if (r.targetInstanceId == target.instanceId) totalDamage += r.damage;
            if (totalDamage <= 0) return;

            int basePool = Mathf.RoundToInt(target.maxHp * growthRules.damageShareXpRate);
            int killerBonus = Mathf.RoundToInt(target.maxHp * growthRules.killingBlowXpRate);

            var contributors = new HashSet<int>();
            foreach (var r in DamageLog.Records)
            {
                if (r.targetInstanceId != target.instanceId) continue;

                int share = Mathf.RoundToInt(basePool * (r.damage / (float)totalDamage));
                int bonus = r.wasKillingBlow ? killerBonus : 0;
                ExperienceLedger.AddXp(r.attackerInstanceId, share + bonus);
                contributors.Add(r.attackerInstanceId);

                Debug.Log($"[Growth] instanceId={r.attackerInstanceId}: +{share}xp" +
                    (bonus > 0 ? $" +{bonus}xp(トドメ)" : "") +
                    $" → 累計{ExperienceLedger.GetXp(r.attackerInstanceId)}xp");
            }

            foreach (var instanceId in contributors)
                TryEvolvePawn(instanceId);
        }

        private void TryEvolvePawn(int instanceId)
        {
            if (!board.TryFindPieceById(instanceId, out var piece, out var isPlayerSide, out _)) return;
            if (!isPlayerSide || piece.pieceType != PieceType.Pawn || piece.rank != 1) return;
            if (ExperienceLedger.GetXp(instanceId) < growthRules.xpToEvolveStar2) return;

            Debug.Log($"[Growth] instanceId={instanceId}: ★2に進化！ " +
                $"HP {piece.maxHp}→{Mathf.RoundToInt(piece.maxHp * growthRules.star2MaxHpMultiplier)}, " +
                $"攻撃力 {piece.attack}→{Mathf.RoundToInt(piece.attack * growthRules.star2AttackMultiplier)}");

            board.EvolvePieceToStar2(instanceId, growthRules.star2MaxHpMultiplier, growthRules.star2AttackMultiplier);
        }

        private void ConsumeAction(Vector2Int pos, bool consumeAll = false)
        {
            if (!board.TryGetPlayerPiece(pos, out var piece)) return;
            piece.actionsLeft = consumeAll ? 0 : Mathf.Max(0, piece.actionsLeft - 1);
            board.SetPlayerPiece(pos, piece);
        }

        private bool TryGetActingPlayerPiece(
            Vector2Int pos,
            out PieceSnapshot piece,
            out IMovementAttackRule rule,
            out PieceMovementPatternSO pattern)
        {
            rule = null;
            pattern = null;
            if (CurrentPhase != BattlePhase.PlayerTurn || !board.TryGetPlayerPiece(pos, out piece))
            {
                piece = default;
                return false;
            }

            // キング（「王の移動権」使用時以外は非対応）や未実装のポーンなど、
            // playerRules/playerPatternsに登録されていない駒種が渡ると
            // インデクサではKeyNotFoundExceptionで落ちるため、TryGetValueで安全に弾く。
            if (!playerRules.TryGetValue(piece.pieceType, out rule) ||
                !playerPatterns.TryGetValue(piece.pieceType, out pattern))
            {
                return false;
            }

            return true;
        }
    }
}
