using System.Collections.Generic;
using UnityEngine;

// 課題【CADモデル対応】: 駒種ごとに、キューブの代わりに表示するCADモデルのプレハブを管理するSO。
// UnitStatusDataSO（ステータス/コスト/特性説明等）とは完全に独立した、見た目専用のデータとして分離する。
[CreateAssetMenu(fileName = "PieceModelData", menuName = "Game/Piece Model Data")]
public class PieceModelDataSO : ScriptableObject
{
  [System.Serializable]
  public class PieceModelEntry
  {
    public PieceType type;
    [Tooltip("この駒種に使用するCADモデルのプレハブ。未設定(null)の場合は、" +
             "従来通りキューブ+color上書きのまま表示される（後方互換）")]
    public GameObject modelPrefab;
    [Tooltip("CADモデルの直立補正角度（オイラー角、度数）。\n" +
             "CADソフトはZ軸を上として作成されることが多く、Unity（Y軸が上）に" +
             "そのまま持ち込むと横倒しになるため、この値で補正する。\n" +
             "まずは (-90, 0, 0) または (90, 0, 0) あたりから試して調整するとよい")]
    public Vector3 rotationOffset = Vector3.zero;
    [Tooltip("CADモデルの位置補正（ローカル座標、駒の中心を基準としたオフセット）。\n" +
             "CADモデルの原点(ピボット)が底面ではなく中心付近にある場合、駒がタイルから" +
             "浮いて/めり込んで見えることがあるため、この値でY座標を中心に微調整する。\n" +
             "まずは (0, -0.2, 0) 前後から試すとよい（マイナスで沈める、プラスで浮かせる）")]
    public Vector3 positionOffset = Vector3.zero;
    [Tooltip("CADモデルの大きさ補正。デフォルト(1,1,1)はプレハブ本来のスケールのまま。\n" +
             "モデルが盤面のマスに対して大きすぎる/小さすぎる場合に調整する")]
    public Vector3 scaleOffset = Vector3.one;
    [Tooltip("体力バー（HPゲージ）の表示高さ（駒本体のローカルY座標）。\n" +
             "キューブ表示時の既定値(1.2)を初期値としてあるため、モデル未調整の駒種は" +
             "従来と同じ高さのまま表示される")]
    public float healthBarHeight = 1.2f;
    [Tooltip("HP数値テキスト（現在HP/最大HP）の表示高さ")]
    public float healthBarTextHeight = 1.45f;
  }

  public List<PieceModelEntry> models = new List<PieceModelEntry>();

  public GameObject GetModelPrefab(PieceType type)
  {
    var entry = models.Find(m => m.type == type);
    return entry != null ? entry.modelPrefab : null;
  }

  // 課題【CADモデルの直立補正】: modelPrefabだけでなくrotationOffsetも一緒に取得できるよう、
  // entry自体を返すメソッドを新設する（既存のGetModelPrefabは後方互換のため削除せず残す）。
  public PieceModelEntry GetEntry(PieceType type)
  {
    return models.Find(m => m.type == type);
  }
}
