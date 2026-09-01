using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// ==============================
// レアリティ定義（数値順で強さを比較できるよう宣言順に注意）
// ==============================
public enum EquipmentRarity
{
  Common = 0,
  Rare = 1,
  Epic = 2,
  Legendary = 3
}

// ==============================
// 付与可能なステータス種別
// ==============================
public enum EquipmentStatType
{
  AttackFlat,          // 攻撃力 加算
  HpFlat,              // 最大HP 加算
  AttackSpeedPercent,  // 攻撃速度（攻撃間隔short化） %
  LifestealPercent,    // 吸血率 加算 %
  DoubleAttackChance,  // 連撃確率 %
  Taunt                // 挑発（装備中は敵から優先的に狙われる）
}

// ==============================
// 個別のステータスボーナス
// ==============================
[System.Serializable]
public class EquipmentStatBonus
{
  public EquipmentStatType statType;
  public float value; // Flat系はそのままの数値、%系は 0.1f = 10% として扱う
}

// ==============================
// 装備インスタンス（1つのドロップ品 / 所持品）
// ==============================
[System.Serializable]
public class EquipmentInstance
{
  public string itemId;
  public string itemName;
  public EquipmentRarity rarity;
  public List<EquipmentStatBonus> bonuses = new List<EquipmentStatBonus>();
}

// ==============================
// ランダム装備生成ロジック
// ==============================
public static class EquipmentGenerator
{
  static readonly string[] commonNames = { "かけらの指輪", "古びた剣", "粗末な盾", "布のマント" };
  static readonly string[] rareNames = { "鋼の指輪", "魔法の剣", "鉄壁の盾", "知恵のマント" };
  static readonly string[] epicNames = { "竜鱗の指輪", "業火の剣", "不落の盾", "賢者のマント" };
  static readonly string[] legendaryNames = { "星辰の指輪", "破滅の剣", "絶対の盾", "神話のマント" };

  // qualityTier: 1=雑魚敵, 2=中堅敵, 3=強敵（レアリティ上振れしやすくなる）
  public static EquipmentInstance GenerateRandomEquipment(int qualityTier)
  {
    EquipmentRarity rarity = RollRarity(qualityTier);

    EquipmentInstance item = new EquipmentInstance();
    item.itemId = System.Guid.NewGuid().ToString();
    item.rarity = rarity;
    item.itemName = PickFlavorName(rarity);
    item.bonuses = GenerateBonuses(rarity);

    return item;
  }

  static EquipmentRarity RollRarity(int qualityTier)
  {
    float commonW, rareW, epicW, legendaryW;

    switch (qualityTier)
    {
      case 3:
        commonW = 0.20f; rareW = 0.40f; epicW = 0.30f; legendaryW = 0.10f;
        break;
      case 2:
        commonW = 0.45f; rareW = 0.35f; epicW = 0.17f; legendaryW = 0.03f;
        break;
      default:
        commonW = 0.70f; rareW = 0.25f; epicW = 0.05f; legendaryW = 0.00f;
        break;
    }

    float roll = Random.value;

    if (roll < commonW) return EquipmentRarity.Common;
    roll -= commonW;

    if (roll < rareW) return EquipmentRarity.Rare;
    roll -= rareW;

    if (roll < epicW) return EquipmentRarity.Epic;

    return EquipmentRarity.Legendary;
  }

  static string PickFlavorName(EquipmentRarity rarity)
  {
    string[] pool;
    switch (rarity)
    {
      case EquipmentRarity.Common: pool = commonNames; break;
      case EquipmentRarity.Rare: pool = rareNames; break;
      case EquipmentRarity.Epic: pool = epicNames; break;
      default: pool = legendaryNames; break;
    }
    return pool[Random.Range(0, pool.Length)];
  }

  static List<EquipmentStatBonus> GenerateBonuses(EquipmentRarity rarity)
  {
    int slotCount;
    switch (rarity)
    {
      case EquipmentRarity.Common: slotCount = 1; break;
      case EquipmentRarity.Rare: slotCount = 2; break;
      case EquipmentRarity.Epic: slotCount = 3; break;
      default: slotCount = 4; break; // Legendary
    }

    // レアリティが高いほど特殊効果（吸血・連撃・挑発）が候補に入る
    List<EquipmentStatType> pool = new List<EquipmentStatType>
    {
      EquipmentStatType.AttackFlat,
      EquipmentStatType.HpFlat,
      EquipmentStatType.AttackSpeedPercent
    };

    if (rarity >= EquipmentRarity.Rare) pool.Add(EquipmentStatType.LifestealPercent);
    if (rarity >= EquipmentRarity.Epic) pool.Add(EquipmentStatType.DoubleAttackChance);
    if (rarity >= EquipmentRarity.Legendary) pool.Add(EquipmentStatType.Taunt);

    // シャッフル
    for (int i = 0; i < pool.Count; i++)
    {
      int r = Random.Range(i, pool.Count);
      EquipmentStatType tmp = pool[i];
      pool[i] = pool[r];
      pool[r] = tmp;
    }

    int actualCount = Mathf.Min(slotCount, pool.Count);
    List<EquipmentStatBonus> bonuses = new List<EquipmentStatBonus>();

    for (int i = 0; i < actualCount; i++)
    {
      bonuses.Add(RollBonusValue(pool[i], rarity));
    }

    return bonuses;
  }

  static EquipmentStatBonus RollBonusValue(EquipmentStatType type, EquipmentRarity rarity)
  {
    int tier = (int)rarity;
    EquipmentStatBonus bonus = new EquipmentStatBonus { statType = type };

    switch (type)
    {
      case EquipmentStatType.AttackFlat:
        switch (tier)
        {
          case 0: bonus.value = Random.Range(50, 101); break;
          case 1: bonus.value = Random.Range(100, 171); break;
          case 2: bonus.value = Random.Range(170, 241); break;
          default: bonus.value = Random.Range(240, 301); break;
        }
        break;

      case EquipmentStatType.HpFlat:
        switch (tier)
        {
          case 0: bonus.value = Random.Range(200, 511); break;
          case 1: bonus.value = Random.Range(500, 911); break;
          case 2: bonus.value = Random.Range(900, 1211); break;
          default: bonus.value = Random.Range(1200, 1511); break;
        }
        break;

      case EquipmentStatType.AttackSpeedPercent:
        switch (tier)
        {
          case 0: bonus.value = Random.Range(0.05f, 0.08f); break;
          case 1: bonus.value = Random.Range(0.08f, 0.12f); break;
          case 2: bonus.value = Random.Range(0.12f, 0.16f); break;
          default: bonus.value = Random.Range(0.16f, 0.20f); break;
        }
        break;

      case EquipmentStatType.LifestealPercent:
        switch (tier)
        {
          case 1: bonus.value = Random.Range(0.05f, 0.08f); break;
          case 2: bonus.value = Random.Range(0.08f, 0.11f); break;
          default: bonus.value = Random.Range(0.11f, 0.15f); break;
        }
        break;

      case EquipmentStatType.DoubleAttackChance:
        switch (tier)
        {
          case 2: bonus.value = Random.Range(0.10f, 0.15f); break;
          default: bonus.value = Random.Range(0.15f, 0.20f); break;
        }
        break;

      case EquipmentStatType.Taunt:
        bonus.value = 1f; // フラグ的効果なので数値は使わない
        break;
    }

    return bonus;
  }

  // UI表示用: ボーナス1件を短いテキストに変換
  public static string FormatBonus(EquipmentStatBonus bonus)
  {
    switch (bonus.statType)
    {
      case EquipmentStatType.AttackFlat:
        return $"ATK+{Mathf.RoundToInt(bonus.value)}";
      case EquipmentStatType.HpFlat:
        return $"HP+{Mathf.RoundToInt(bonus.value)}";
      case EquipmentStatType.AttackSpeedPercent:
        return $"SPD+{bonus.value * 100f:F0}%";
      case EquipmentStatType.LifestealPercent:
        return $"吸血+{bonus.value * 100f:F0}%";
      case EquipmentStatType.DoubleAttackChance:
        return $"連撃+{bonus.value * 100f:F0}%";
      case EquipmentStatType.Taunt:
        return "挑発";
      default:
        return "";
    }
  }
}

// ==============================
// 盤上に落ちた装備ドロップ品（クリックで回収）
// ==============================
public class EquipmentDropPickup : MonoBehaviour
{
  public EquipmentInstance item;
  private Camera mainCamera;

  void Start()
  {
    mainCamera = Camera.main;
  }

  void Update()
  {
    if (Mouse.current == null) return;

    // ステップ7: カメラ参照の防御的再取得
    if (mainCamera == null) mainCamera = Camera.main;
    if (mainCamera == null) return;

    if (Mouse.current.leftButton.wasPressedThisFrame)
    {
      // ステップ11: UGUIボタン等の上でのクリックはドロップ拾得の対象にしない
      if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
      {
        return;
      }

      Vector2 mousePos = Mouse.current.position.ReadValue();
      Ray ray = mainCamera.ScreenPointToRay(mousePos);

      if (Physics.Raycast(ray, out RaycastHit hit))
      {
        if (hit.transform == transform)
        {
          if (DebugGameManager.Instance != null)
          {
            DebugGameManager.Instance.CollectDrop(this);
          }
        }
      }
    }
  }
}
