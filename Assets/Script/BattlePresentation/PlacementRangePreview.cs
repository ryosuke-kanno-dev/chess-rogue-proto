using System.Collections.Generic;
using UnityEngine;
using Chebyss.Battle;

// 配置フェーズ中に、駒を置いた場合の移動範囲（青）と攻撃できる敵（赤）をプレビュー表示する。
// 盤面（BoardState）は一切変更せず、ルールの結果をタイルのハイライトとして示すだけ。
// このクラスが付けたハイライトだけを、付ける前の状態へ戻す（自陣エリアの強調などを壊さない）。
public class PlacementRangePreview : MonoBehaviour
{
    private BattleBootstrapper bootstrapper;
    private BoardGenerator boardGenerator;

    // このクラスが上書きしたタイルと、上書き前のハイライト（Clearで元へ戻す）
    private readonly Dictionary<Vector2Int, HighlightType> previousHighlights = new();

    // 同じ条件での再表示を避けるためのキャッシュ（ドラッグ中は毎フレーム呼ばれるため）
    private bool hasLast;
    private PieceType lastType;
    private Vector2Int lastPosition;

    public void Initialize(BattleBootstrapper bootstrapper, BoardGenerator generator)
    {
        this.bootstrapper = bootstrapper;
        this.boardGenerator = generator;
    }

    // typeの駒がcandidatePositionにいるとして、現在の盤面上の移動可能マスと攻撃できる敵のマスを表示する
    public void ShowFor(PieceType type, Vector2Int candidatePosition)
    {
        if (bootstrapper == null || boardGenerator == null || !bootstrapper.IsPlacementPhase)
        {
            Clear();
            return;
        }

        if (hasLast && lastType == type && lastPosition == candidatePosition) return;

        Clear();

        if (!bootstrapper.TryGetPlacementPreviewData(type, out var snapshot, out var rule, out var pattern) ||
            rule == null || pattern == null)
        {
            return;
        }

        // 盤面上に未配置の駒でも使えるよう、名簿のスナップショットを複製して位置だけ差し替える
        snapshot.position = candidatePosition;
        snapshot.isPlayerSide = true;

        var board = bootstrapper.Board;

        foreach (var pos in rule.GetMovableTiles(snapshot, board, pattern))
            Apply(pos, HighlightType.PreviewMovable);

        foreach (var pos in rule.GetAttackableTiles(snapshot, board, pattern))
            Apply(pos, HighlightType.PreviewAttackable);

        hasLast = true;
        lastType = type;
        lastPosition = candidatePosition;
    }

    // このクラスが付けたハイライトだけを、付ける前の状態へ戻す
    public void Clear()
    {
        if (boardGenerator != null)
        {
            foreach (var kvp in previousHighlights)
                boardGenerator.GetTile(kvp.Key)?.SetHighlight(kvp.Value);
        }

        previousHighlights.Clear();
        hasLast = false;
    }

    private void Apply(Vector2Int pos, HighlightType type)
    {
        var tile = boardGenerator.GetTile(pos);
        if (tile == null) return;

        // 最初に上書きするときだけ、元のハイライトを記録する
        if (!previousHighlights.ContainsKey(pos))
            previousHighlights[pos] = tile.CurrentHighlight;

        tile.SetHighlight(type);
    }
}
