using System.Collections.Generic;
using UnityEngine;

// 課題【異種合成「精鋭騎兵」】: 異なる駒種を消費して新しい駒種へ融合するレシピを表すSO。
// 既存の同種合成（★1×3→★2、★2×3→★3）とは別系統の、汎用的なリスト構造として実装する
// （将来「ルーク×ビショップ→○○」のような追加レシピにもそのまま対応できるようにするため）。
[System.Serializable]
public class FusionRecipeEntry
{
  [Tooltip("表示名（例:「精鋭騎兵に融合」）")]
  public string recipeName;
  public PieceType materialType1;
  public int materialCount1 = 1;
  public PieceType materialType2;
  public int materialCount2 = 1;
  [Tooltip("融合結果として生成される駒種")]
  public PieceType resultType;
}

[CreateAssetMenu(fileName = "FusionRecipeData", menuName = "Game/Fusion Recipe Data")]
public class FusionRecipeDataSO : ScriptableObject
{
  public List<FusionRecipeEntry> recipes = new List<FusionRecipeEntry>
  {
    new FusionRecipeEntry {
      recipeName = "精鋭騎兵に融合",
      materialType1 = PieceType.Pawn, materialCount1 = 1,
      materialType2 = PieceType.Knight, materialCount2 = 1,
      resultType = PieceType.EliteCavalier
    }
  };
}
