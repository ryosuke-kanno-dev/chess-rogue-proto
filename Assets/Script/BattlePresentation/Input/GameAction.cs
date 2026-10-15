// ゲーム内でキー割り当ての対象になる操作。
public enum GameAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Confirm,
    Cancel,
    NextPiece,
    PrevPiece,
    EndTurn,
    OpenSettings
}

public static class GameActionInfo
{
    public static string GetDisplayName(GameAction action)
    {
        switch (action)
        {
            case GameAction.MoveUp:       return "上に移動";
            case GameAction.MoveDown:     return "下に移動";
            case GameAction.MoveLeft:     return "左に移動";
            case GameAction.MoveRight:    return "右に移動";
            case GameAction.Confirm:      return "決定";
            case GameAction.Cancel:       return "キャンセル";
            case GameAction.NextPiece:    return "次の駒";
            case GameAction.PrevPiece:    return "前の駒";
            case GameAction.EndTurn:      return "ターン終了・配置完了・次へ・スキル取得";
            case GameAction.OpenSettings: return "設定を開く";
            default:                      return action.ToString();
        }
    }
}
