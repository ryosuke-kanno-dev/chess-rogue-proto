using System.Collections.Generic;
using UnityEngine; // Vector2Intのみ使用

namespace Chebyss.Battle
{
    /// <summary>
    /// 駒種ごとの移動・攻撃ロジック。実装クラス（KnightDashRule等）は
    /// MonoBehaviour/GameObject/ScriptableObjectに依存しないこと（単体テスト可能な設計を維持）。
    /// Vector2IntはUnityEngineの値型構造体で、シーンなしにNUnitで生成・比較できるため
    /// このインターフェースでは例外的に許容している。
    /// </summary>
    public interface IMovementAttackRule
    {
        List<Vector2Int> GetMovableTiles(PieceSnapshot piece, BoardState board, PieceMovementPatternSO pattern);

        List<Vector2Int> GetAttackableTiles(PieceSnapshot piece, BoardState board, PieceMovementPatternSO pattern);

        AttackOutcome ExecuteAttack(PieceSnapshot attacker, PieceSnapshot target, BoardState board, PieceMovementPatternSO pattern);
    }
}
