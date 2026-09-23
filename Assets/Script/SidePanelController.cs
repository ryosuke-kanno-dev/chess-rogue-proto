using UnityEngine;

// 課題【神クラス分割 第1弾】: DebugGameManager.csから「墓地/スキルツリー/設定パネルの
// 排他制御」を切り出したコンポーネント。DebugGameManagerと同じGameObjectにアタッチする想定。
// ブロック中判定（成長ボーナス選択中/合成選択モード中）はDebugGameManager側が
// 引き続き保持しているため、その判定だけはgm.UI_IsBlockingModalOpen()を通じて参照する。
public class SidePanelController : MonoBehaviour
{
  private DebugGameManager gm;

  private bool showCemeteryModal = false;
  private bool showSkillTreeModal = false;

  public bool IsCemeteryModalOpen => showCemeteryModal;
  public bool IsSkillTreeModalOpen => showSkillTreeModal;

  void Awake()
  {
    gm = GetComponent<DebugGameManager>();
  }

  // 課題【UIの排他制御】: 墓地/スキルツリー/AIパターン選択の3つは、
  // 同時に1つしか開けないようにする（新しく開く前に、必ず他を閉じる）
  public void CloseAllSidePanels()
  {
    showCemeteryModal = false;
    showSkillTreeModal = false;
    if (PieceAIBehaviorSelectorModal.Instance != null) PieceAIBehaviorSelectorModal.Instance.Hide();
    if (SettingsPanelUI.Instance != null) SettingsPanelUI.Instance.Hide();
  }

  public bool CanOpenSidePanel() => gm != null && !gm.UI_IsBlockingModalOpen();

  public void ToggleSkillTree()
  {
    if (gm == null || gm.UI_IsBlockingModalOpen()) return;
    bool opening = !showSkillTreeModal;
    CloseAllSidePanels();
    showSkillTreeModal = opening;
  }

  public void ToggleCemetery()
  {
    if (gm == null || gm.UI_IsBlockingModalOpen()) return;
    bool opening = !showCemeteryModal;
    CloseAllSidePanels();
    showCemeteryModal = opening;
  }

  // 課題【設定画面】: 既存のUI_ToggleSettings()と同じ考え方（開く前は他を閉じる・
  // ブロック中は開けない）。既に開いている場合はブロック判定を経由せず、
  // いつでも閉じられるようにする（設定画面は成長ボーナス選択中等でも「閉じる」操作自体は妨げない）。
  public void ToggleSettings()
  {
    if (SettingsPanelUI.Instance == null) return;

    if (SettingsPanelUI.Instance.IsOpen)
    {
      SettingsPanelUI.Instance.Hide();
      return;
    }

    if (gm != null && gm.UI_IsBlockingModalOpen()) return;
    CloseAllSidePanels();
    SettingsPanelUI.Instance.Show();
  }
}
