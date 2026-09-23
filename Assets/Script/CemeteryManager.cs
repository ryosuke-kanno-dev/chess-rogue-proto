using System.Collections.Generic;
using UnityEngine;

// 課題【神クラス分割 第2弾】: DebugGameManager.csから「墓地記録」「Goldでの駒回復」を
// 切り出したコンポーネント。DebugGameManagerと同じGameObjectにアタッチする想定。
// Gold増減(AddGold)・装備追加(AddItemToInventory)・現在ウェーブ(currentWave)は
// 引き続きDebugGameManager側が保持しているため、gm参照を通じて呼び出す。
public class CemeteryManager : MonoBehaviour
{
  private DebugGameManager gm;

  public List<CemeteryRecord> CemeteryList { get; } = new List<CemeteryRecord>();

  [Tooltip("gameConfigが未設定の場合に使う、負傷した味方駒をGoldで全回復させる際の" +
           "フォールバックコスト。UI側は必ずGetHealCost()経由でこの値を参照すること" +
           "（値の二重管理を防止するための単一の真実）")]
  [SerializeField] private int healCostFallback = 2000;

  private int HealCost => gm != null && gm.GameConfig != null ? gm.GameConfig.healCost : healCostFallback;

  public int GetHealCost() => HealCost;

  void Awake()
  {
    gm = GetComponent<DebugGameManager>();
  }

  // ステップ29【要件2】/ステップ31【改善】: 負傷した味方駒をGoldで全回復させる。
  // コストは引数で受け取らず、必ず上記のHealCostプロパティ（単一の真実）を参照する。
  public bool HealPieceWithGold(PieceData piece)
  {
    if (piece == null) return false;
    if (piece.isEnemy) return false;
    if (piece.currentHp <= 0) return false;
    if (piece.currentHp >= piece.maxHp) return false;

    if (gm.gold < HealCost)
    {
      Debug.LogWarning($"⚠️ Goldが足りません（必要: {HealCost}G / 所持: {gm.gold}G）。");
      return false;
    }

    gm.AddGold(-HealCost, GoldSourceType.ManualHeal);
    piece.Heal(piece.maxHp - piece.currentHp);
    return true;
  }

  // ステップ7: 戦死した味方駒を墓地リストへ記録し、装備を1つずつ50%抽選で回収 or ロスト
  public void SendPieceToCemetery(PieceData piece)
  {
    CemeteryRecord record = new CemeteryRecord
    {
      pieceName = piece.pieceName,
      type = piece.type,
      rank = piece.rank,
      deathWave = gm.currentWave
    };

    int recovered = 0;
    int lost = 0;

    if (piece.equippedItems.Count > 0)
    {
      List<EquipmentInstance> items = new List<EquipmentInstance>(piece.equippedItems);

      foreach (var item in items)
      {
        bool isRecovered = Random.value < 0.5f;

        if (isRecovered)
        {
          gm.AddItemToInventory(item);
          recovered++;
        }
        else
        {
          lost++;
        }

        record.equipmentLog.Add(new CemeteryEquipmentEntry
        {
          itemName = item.itemName,
          rarity = item.rarity,
          wasRecovered = isRecovered
        });
      }
    }

    CemeteryList.Add(record);

    Debug.Log($"【墓地】{piece.pieceName} が戦死。装備 {recovered}個 回収 / {lost}個 ロスト。");
  }
}
