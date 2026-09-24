using System.Collections.Generic;

// 課題【駒レジストリによるFindObjectsOfType呼び出しの一元化】:
// これまで DebugGameManager.cs / PieceAI.cs / PieceData.cs / PieceDraggable.cs の各所で
// FindObjectsOfType<PieceData>() を呼んでシーン全体を走査していたが、特にキングオーラ関連の判定
// (IsOwnTeamKingAlive)のように攻撃・被弾のたびに毎回呼ばれる箇所では、駒数の増加に伴って
// 負荷が積み重なる状態になっていた。
// 本クラスは、PieceData.Awake()/OnDestroy()で自己登録・自己解除される「今シーン上に存在する
// 全PieceDataのリスト」を一元管理し、各所のFindObjectsOfType呼び出しをこのリスト参照へ
// 置き換えるための土台となる。
//
// ScoreManager.cs と同じ「static classによるユーティリティ」方式を踏襲し、
// MonoBehaviourのシングルトンにはしない（シーンにアタッチするGameObjectが不要なため）。
public static class PieceRegistry
{
  private static readonly List<PieceData> allPieces = new List<PieceData>();

  // 読み取り専用として公開（呼び出し側から誤って要素を追加/削除されないようにする）
  public static IReadOnlyList<PieceData> AllPieces => allPieces;

  public static void Register(PieceData piece)
  {
    if (!allPieces.Contains(piece)) allPieces.Add(piece);
  }

  public static void Unregister(PieceData piece)
  {
    allPieces.Remove(piece);
  }
}
