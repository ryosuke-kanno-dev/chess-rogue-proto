using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ステップ18: プレイヤー向けインベントリUIのUGUI化。
// slotPrefab（InventorySlotUIをアタッチしたUI要素）を DebugGameManager.UI_MaxInventorySlots 個ぶんだけ
// 起動時に1度だけ生成し（動的Instantiateはこの初期化タイミングのみ）、以後は同じスロット群の
// 表示内容だけを毎フレーム更新する。ロジック本体（EquipmentInstance / inventory リスト等）は
// DebugGameManager側にそのまま残し、本スクリプトは表示・入力の橋渡しのみを行う。
public class InventoryUI : MonoBehaviour
{
  [Header("スロット構成")]
  [Tooltip("InventorySlotUIをアタッチしたスロットのプレハブ")]
  [SerializeField] private GameObject slotPrefab;
  [Tooltip("スロットをまとめて配置する親（Grid Layout Group等を付けておくと綺麗に並ぶ）")]
  [SerializeField] private Transform slotContainer;

  [Header("表示（任意）")]
  [SerializeField] private TextMeshProUGUI headerText; // 例:「インベントリ (3/8)」

  [Header("課題【インベントリ開閉トグル】")]
  [Tooltip("開閉ボタン（InventoryToggleButton）")]
  [SerializeField] private Button toggleButton;
  [Tooltip("開閉対象のグリッド本体（Editor上で手動的にチェックを外して非表示にしていた、\n" +
           "スロットが並んでいるコンテナ。slotContainerの親、またはslotContainer自体を想定）")]
  [SerializeField] private GameObject contentRoot;
  private bool isExpanded = false;

  private DebugGameManager gm;
  private readonly List<InventorySlotUI> slots = new List<InventorySlotUI>();

  void Start()
  {
    gm = DebugGameManager.Instance;
    BuildSlots();

    if (toggleButton != null)
    {
      toggleButton.onClick.AddListener(OnToggleClicked);
    }
    // 課題【インベントリ開閉トグル】: 起動時は閉じた状態から始める
    if (contentRoot != null) contentRoot.SetActive(isExpanded);
  }

  void Update()
  {
    if (gm == null) gm = DebugGameManager.Instance;
    if (gm == null) return;

    RefreshSlots();
  }

  // MaxInventorySlots個ぶんのスロットを1度だけ生成する
  void BuildSlots()
  {
    if (slotPrefab == null || slotContainer == null)
    {
      Debug.LogWarning("⚠️ InventoryUI: slotPrefab / slotContainer がInspectorで未設定です。");
      return;
    }

    if (gm == null) return;

    int slotCount = gm.UI_MaxInventorySlots;
    for (int i = 0; i < slotCount; i++)
    {
      GameObject obj = Instantiate(slotPrefab, slotContainer);
      obj.name = $"InventorySlot_{i}";

      InventorySlotUI slot = obj.GetComponent<InventorySlotUI>();
      if (slot == null)
      {
        Debug.LogWarning("⚠️ InventoryUI: slotPrefabにInventorySlotUIがアタッチされていません。");
        continue;
      }

      slot.Initialize(OnLeftClick_ShowDetail, OnSlotClicked);
      slot.SetEmpty();
      slots.Add(slot);
    }
  }

  // 現在のinventoryの中身を各スロットへ反映する
  void RefreshSlots()
  {
    if (headerText != null)
    {
      headerText.text = $"インベントリ ({gm.inventory.Count}/{gm.UI_MaxInventorySlots})";
    }

    for (int i = 0; i < slots.Count; i++)
    {
      if (i < gm.inventory.Count)
      {
        EquipmentInstance item = gm.inventory[i];
        slots[i].SetData(item, gm.UI_GetRarityColor(item.rarity));
      }
      else
      {
        slots[i].SetEmpty();
      }
    }
  }

  // スロット右クリック時: 選択中の駒（DebugGameManager.selectedPiece）へ装着を試みる
  void OnSlotClicked(EquipmentInstance item)
  {
    if (gm == null) return;
    gm.UI_TryEquipToSelectedPiece(item);
  }

  // 課題【左右クリック分岐】: スロット左クリック時、アイテムの詳細（ステータス内訳）をポップアップ表示する
  void OnLeftClick_ShowDetail(EquipmentInstance item)
  {
    if (ItemDetailPopup.Instance == null) return;
    ItemDetailPopup.Instance.Show(item);
  }

  // 課題【インベントリ開閉トグル】: 開閉ボタン押下時、グリッド本体（contentRoot）の表示/非表示を切り替える。
  // BuildSlots()/RefreshSlots()の中身は一切変更しないため、contentRootが非アクティブの間も
  // スロットの中身自体は裏側で正しく更新され続ける（単に見た目上非表示になるだけ）。
  void OnToggleClicked()
  {
    isExpanded = !isExpanded;
    if (contentRoot != null) contentRoot.SetActive(isExpanded);
  }
}
