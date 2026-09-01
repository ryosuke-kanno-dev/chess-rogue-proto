using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum PieceType
{
  Pawn,
  Knight,
  Rook,
  Bishop,
  Queen,
  Paladin,
  King,
  EliteCavalier, // 課題【異種合成「精鋭騎兵」】: ポーン×ナイトの融合進化専用。ショップ購入・通常スポーン対象外
}

// 課題3【所有者チェック】: 駒がプレイヤー側か敵側かを表す。
// 既存の isEnemy (bool) は他ファイルからの参照が非常に多いため互換性のためそのまま残し、
// PlayerType は isEnemy から算出される読み取り専用プロパティとして追加する。
// 「PlayerType.Player のみ許可」といった所有者チェックを書くコードは、
// 新規に isEnemy を直接見るのではなく、こちらの Owner プロパティを参照することを推奨する。
public enum PlayerType
{
  Player,
  Enemy
}

public class PieceData : MonoBehaviour
{
  [Header("基本設定")]
  public string pieceName = "ポーン";
  public PieceType type = PieceType.Pawn;
  public int rank = 1; // ★ランク（1 = ★1, 2 = ★2）
  public bool isEnemy = false;

  // 課題3: isEnemy を PlayerType として読み取るための算出プロパティ
  public PlayerType Owner => isEnemy ? PlayerType.Enemy : PlayerType.Player;

  [Header("課題【AIパターンのSO管理化】")]
  [Tooltip("この駒が使用するAI行動パターン。null＝バランス型。敵駒・プレイヤー駒の両方で使用する")]
  public AIBehaviorDataSO aiBehavior;

  [Header("課題【★2→★3合成の育成履歴分岐システム】")]
  [Tooltip("★2進化時に選んだGrowthTypeの履歴。★2→★3の進化分岐判定に使用する")]
  public List<GrowthType> growthHistory = new List<GrowthType>();
  [Tooltip("★3進化後のフレーバー名（例:「吸血馬」）。未進化の場合は空文字列")]
  public string evolvedVariantName = "";
  [Tooltip("★3進化後の追加説明文。ツールチップに追記する")]
  public string evolvedVariantDescription = "";
  [Tooltip("EvolutionRuleDataSO.grantsExtraEquipSlotがtrueの進化を遂げた場合に加算される、装備スロットの追加数")]
  public int bonusEquipSlots = 0;

  [Header("ステータス")]
  public int maxHp = 1200;
  public int currentHp = 1200;
  public int attack = 200;
  public float attackInterval = 2.0f;

  [Header("特殊能力バフ")]
  public float lifestealRate = 0f; // 吸血率（例: 0.2f で20%吸収）

  [Header("ステップ2・3: バフ・スキル状態")]
  public bool isOnBuffTile = false;
  public bool hasAdjacentBuff = false;
  public int kingBonusAttack = 0; // キングスキルによる全体攻撃バフ

  [Header("ステップ4: ハクスラ装備システム")]
  public const int MaxEquipSlots = 3;
  public List<EquipmentInstance> equippedItems = new List<EquipmentInstance>();
  public bool isTaunting = false;        // 挑発装備の効果
  public float doubleAttackChance = 0f;  // 連撃装備の効果

  [Header("ステップ6: 戦闘前配置の記憶")]
  public Vector3 savedPosition; // 戦闘開始時にDebugGameManagerが記録し、勝敗後にここへワープして戻す

  [Header("課題1: 復活(リバース)システム")]
  [Tooltip("このバトル中に既に一度リバース（その場での復活）を使用したかどうか。\n同じ命で連続してリバースし続けることが無いよう、1バトルにつき1回までに制限するためのガード")]
  public bool hasRebirthedThisBattle = false;
  private float invincibleUntilTime = -1f; // 復活直後の無敵時間の終了時刻（Time.time基準）

  private Renderer meshRenderer;
  private Color originalColor;
  private PieceHealthBar healthBar;
  private bool statsAppliedFromSO = false; // ステップ26: UnitStatusDataSOから正常に反映できたかどうかの追跡フラグ

  // 課題【駒レジストリ】: Start()ではなくAwake()で登録することで、他コンポーネントのStart()より
  // 確実に先に登録を終わらせる（他コンポーネントがStart()時点でPieceRegistry.AllPiecesを
  // 参照しても、自分自身が既にリストに含まれている状態を保証するため）。
  void Awake()
  {
    PieceRegistry.Register(this);
  }

  // 課題【駒レジストリ】: 撃破時はDieRoutine()内でSetActive(false)されるだけでDestroy()はされないため、
  // OnDestroy()はこのタイミングでは発火しない（＝非アクティブな撃破済み駒はリストに残り続ける。
  // CleanUpBattlefield()側が非アクティブな駒も含めて処理する必要があるための意図的な仕様）。
  // 一方、SceneManager.LoadScene()によるシーン再読み込み時はUnityが全オブジェクトを実際に破棄するため、
  // その際にOnDestroy()が発火し、staticなリストから自動的に除外される。
  void OnDestroy()
  {
    PieceRegistry.Unregister(this);
  }

  void Start()
  {
    meshRenderer = GetComponent<Renderer>();
    if (meshRenderer != null)
    {
      originalColor = meshRenderer.material.color;
    }

    healthBar = GetComponent<PieceHealthBar>();
    if (healthBar == null)
    {
      healthBar = gameObject.AddComponent<PieceHealthBar>();
    }

    SetupInitialStats();
  }

  void Update()
  {
    // ステップ26【不具合調査対応】: 生成タイミングの都合でDebugGameManager.Instanceがまだ
    // 準備できていなかった場合の保険。SOを参照できる状態になり次第、1度だけ自動的に取り直す。
    if (!statsAppliedFromSO && DebugGameManager.Instance != null && DebugGameManager.Instance.UnitStatusData != null)
    {
      SetupInitialStats();
    }
  }

  public void SetupInitialStats()
  {
    // ステップ24/26: DebugGameManagerにUnitStatusDataSOが設定されていれば、そちらの数値を優先して使用する。
    // 未設定、またはInstanceが取得できない/該当駒種のエントリが無い場合は既存のハードコード値へフォールバックする。
    DebugGameManager gm = DebugGameManager.Instance;
    UnitStatusDataSO data = gm != null ? gm.UnitStatusData : null;
    UnitStatusDataSO.UnitStatusEntry entry = data != null ? data.GetStats(type) : null;

    if (entry != null)
    {
      pieceName = entry.pieceName;
      maxHp = entry.maxHp;
      attack = entry.attack;
      attackInterval = entry.attackInterval;
      currentHp = maxHp;
      statsAppliedFromSO = true;

      Debug.Log($"【SO反映】{gameObject.name}（{type}）: HP={maxHp}, ATK={attack}, Interval={attackInterval} を UnitStatusDataSO から取得しました。");
      return;
    }

    // ---- フォールバック: 既存のハードコードロジック ----
    // 診断用: なぜフォールバックになったのかを明示しておく（gm==null / data==null / entry==null のいずれか）
    if (!statsAppliedFromSO)
    {
      if (gm == null)
      {
        Debug.LogWarning($"⚠️【SO未反映】{gameObject.name}（{type}）: DebugGameManager.Instance がまだ取得できないため、ハードコード値を使用します（次フレーム以降に自動リトライします）。");
      }
      else if (data == null)
      {
        Debug.LogWarning($"⚠️【SO未反映】{gameObject.name}（{type}）: DebugGameManager の Unit Status Data がInspectorで未アサインです。ハードコード値を使用します。");
      }
      else
      {
        Debug.LogWarning($"⚠️【SO未反映】{gameObject.name}（{type}）: UnitStatusDataSO 内に {type} のエントリが見つかりません。ハードコード値を使用します。");
      }
    }

    switch (type)
    {
      case PieceType.Pawn:
        pieceName = "ポーン";
        maxHp = 1200;
        attack = 200;
        attackInterval = 2.0f;
        break;

      case PieceType.Knight:
        pieceName = "ナイト";
        maxHp = 1500;
        attack = 250;
        attackInterval = 2.0f;
        break;

      case PieceType.Rook:
        pieceName = "ルーク";
        maxHp = 2000;
        attack = 300;
        attackInterval = 2.5f;
        break;

      case PieceType.Bishop:
        pieceName = "ビショップ";
        maxHp = 800;
        attack = 150;
        attackInterval = 1.8f;
        break;

      case PieceType.Queen:
        pieceName = "クイーン";
        maxHp = 1800;
        attack = 350;
        attackInterval = 2.2f;
        break;

      case PieceType.King:
        pieceName = "キング";
        maxHp = 3000;
        attack = 150;
        attackInterval = 2.0f;
        break;
    }
    currentHp = maxHp;
  }

  // ★2 への手動合成・進化処理
  public void EvolveToStar2()
  {
    int oldMaxHp = maxHp;
    int oldAttack = attack;

    rank = 2;
    maxHp = Mathf.RoundToInt(maxHp * 1.6f);
    currentHp = maxHp;
    attack = Mathf.RoundToInt(attack * 1.5f);

    // 見た目を少し大きくして★2感を演出
    transform.localScale *= 1.15f;

    if (healthBar != null)
    {
      healthBar.UpdateHealthBar();
    }

    // 進化によるステータス上昇を画面上に浮遊テキストで表示（ステップ20）
    ShowGrowthPopup(attack - oldAttack, maxHp - oldMaxHp);

    Debug.Log($"{pieceName} が ★2 に進化！（HP: {maxHp}, ATK: {attack}）");
  }

  public void RankUp()
  {
    int oldMaxHp = maxHp;
    int oldAttack = attack;

    rank++;
    maxHp = Mathf.RoundToInt(maxHp * 1.8f);
    currentHp = maxHp;
    attack = Mathf.RoundToInt(attack * 1.5f);

    if (rank >= 3 && type == PieceType.Pawn)
    {
      EvolveToPaladin(oldAttack, oldMaxHp);
    }
    else
    {
      transform.localScale *= 1.05f;

      // ランクアップによるステータス上昇を画面上に浮遊テキストで表示（ステップ20）
      ShowGrowthPopup(attack - oldAttack, maxHp - oldMaxHp);

      Debug.Log($"{pieceName} が ★{rank} にランクアップ！（HP: {maxHp}, ATK: {attack}）");
    }

    if (healthBar != null)
    {
      healthBar.UpdateHealthBar();
    }
  }

  void EvolveToPaladin(int oldAttack, int oldMaxHp)
  {
    type = PieceType.Paladin;
    pieceName = "パラディン";
    maxHp = 4000;
    currentHp = maxHp;
    attack = 450;

    transform.localScale = Vector3.one * 1.1f;

    if (meshRenderer != null)
    {
      meshRenderer.material.color = new Color(0.9f, 0.8f, 0.2f);
      originalColor = meshRenderer.material.color;
    }

    // プロモーションによるステータス上昇を画面上に浮遊テキストで表示（ステップ20）
    ShowGrowthPopup(attack - oldAttack, maxHp - oldMaxHp);

    Debug.Log($"【プロモーション！】ポーンが 『パラディン』 に進化しました！");
  }

  // ステップ20: 合成・ランクアップ・進化などによるステータス上昇量を浮遊テキストで表示する共通処理。
  // 既存のダメージポップアップ（DamagePopup）システムをそのまま流用し、新しい表示機構は追加しない。
  void ShowGrowthPopup(int attackGain, int hpGain)
  {
    Vector3 basePos = transform.position + Vector3.up * 0.9f;

    if (attackGain != 0)
    {
      DamagePopup.Create(basePos + Vector3.left * 0.25f, $"ATK +{attackGain}", DamagePopupType.Critical);
    }

    if (hpGain != 0)
    {
      DamagePopup.Create(basePos + Vector3.right * 0.25f, $"HP +{hpGain}", DamagePopupType.Heal);
    }
  }

  // ==============================
  // ステップ4: 装備の着脱処理
  // ==============================

  // 課題【★2→★3合成の育成履歴分岐システム】: 定数MaxEquipSlotsはそのまま「基礎値」として残し、
  // grantsExtraEquipSlotによる上乗せ分(bonusEquipSlots)を加算した「実際の上限」をこちらで公開する。
  // 既存コードでPieceData.MaxEquipSlots（静的定数）を直接参照している箇所は、
  // 装備スロットボーナスを持たない駒に対しては従来通りの値のまま動作するため、影響は最小限に留まる。
  public int EffectiveMaxEquipSlots => MaxEquipSlots + bonusEquipSlots;

  // ステップ20: 装備可否をここに一元化する（呼び出し側が個別に条件を再実装しないようにするため）
  public bool CanEquip(EquipmentInstance item)
  {
    if (item == null) return false;
    if (currentHp <= 0) return false; // 戦闘不能の駒には装備できない
    if (equippedItems.Count >= EffectiveMaxEquipSlots) return false;
    return true;
  }

  // インベントリ側から呼ばれる装着処理（枠数チェックは呼び出し側でも行うが念のためここでも確認）
  public void EquipItem(EquipmentInstance item)
  {
    if (!CanEquip(item))
    {
      if (item == null) return;

      if (currentHp <= 0)
      {
        Debug.LogWarning($"⚠️ {pieceName} は戦闘不能のため装備できません。");
      }
      else
      {
        Debug.LogWarning($"⚠️ {pieceName} の装備枠は満杯です！");
      }
      return;
    }

    equippedItems.Add(item);
    ApplyEquipmentBonus(item, 1f);

    if (healthBar != null)
    {
      healthBar.UpdateHealthBar();
    }

    Debug.Log($"{pieceName} に【{item.itemName}】({item.rarity}) を装着！");
  }

  // 装備を外してインベントリへ返す（呼び出し側でインベントリへの追加を行う）
  public void UnequipItem(EquipmentInstance item)
  {
    if (item == null || !equippedItems.Contains(item)) return;

    ApplyEquipmentBonus(item, -1f);
    equippedItems.Remove(item);

    if (healthBar != null)
    {
      healthBar.UpdateHealthBar();
    }

    Debug.Log($"{pieceName} から【{item.itemName}】を外した。");
  }

  // 合成（★2進化）時の装備保護: 全装備を強制的に外し、外れたリストを返す
  public List<EquipmentInstance> UnequipAll()
  {
    List<EquipmentInstance> removed = new List<EquipmentInstance>(equippedItems);

    foreach (var item in removed)
    {
      ApplyEquipmentBonus(item, -1f);
    }
    equippedItems.Clear();

    if (healthBar != null)
    {
      healthBar.UpdateHealthBar();
    }

    return removed;
  }

  // sign: +1f で装着適用, -1f で装着解除（打ち消し）
  void ApplyEquipmentBonus(EquipmentInstance item, float sign)
  {
    foreach (var bonus in item.bonuses)
    {
      switch (bonus.statType)
      {
        case EquipmentStatType.AttackFlat:
          attack += Mathf.RoundToInt(bonus.value * sign);
          if (attack < 0) attack = 0;
          break;

        case EquipmentStatType.HpFlat:
          int hpChange = Mathf.RoundToInt(bonus.value * sign);
          maxHp += hpChange;
          if (maxHp < 1) maxHp = 1;
          currentHp = Mathf.Clamp(currentHp + hpChange, 0, maxHp);
          break;

        case EquipmentStatType.AttackSpeedPercent:
          if (sign > 0)
          {
            attackInterval *= (1f - bonus.value);
          }
          else
          {
            attackInterval /= (1f - bonus.value);
          }
          attackInterval = Mathf.Max(attackInterval, 0.3f);
          break;

        case EquipmentStatType.LifestealPercent:
          lifestealRate += bonus.value * sign;
          if (lifestealRate < 0f) lifestealRate = 0f;
          break;

        case EquipmentStatType.DoubleAttackChance:
          doubleAttackChance += bonus.value * sign;
          if (doubleAttackChance < 0f) doubleAttackChance = 0f;
          break;

        case EquipmentStatType.Taunt:
          isTaunting = sign > 0;
          break;
      }
    }
  }

  public void TakeDamage(int damage, bool isCritical = false)
  {
    // 課題1【復活後の無敵時間】: 復活直後の一定時間はダメージを一切受け付けない
    if (Time.time < invincibleUntilTime) return;

    // 課題6【キング常時オーラ・被ダメージ軽減】: 自チームのKingが生存している間、受けるダメージを
    // kingAuraDamageReduction分だけ軽減する。
    // 【重要】maxHpそのものを動的に増減させる実装は、Kingが撃破された際に元へ戻す処理が複雑になり
    // 不具合の温床になりやすいため避け、あくまで「被ダメージ計算時の軽減率」として実装している。
    if (DebugGameManager.Instance != null && DebugGameManager.Instance.UnitStatusData != null && IsOwnTeamKingAlive())
    {
      UnitStatusDataSO.UnitStatusEntry kingEntry = DebugGameManager.Instance.UnitStatusData.GetStats(PieceType.King);
      if (kingEntry != null && kingEntry.kingAuraDamageReduction > 0f)
      {
        float reduction = Mathf.Clamp01(kingEntry.kingAuraDamageReduction);
        damage = Mathf.RoundToInt(damage * (1f - reduction));
        if (damage < 0) damage = 0;
      }
    }

    currentHp -= damage;
    if (currentHp < 0) currentHp = 0;

    if (healthBar != null)
    {
      healthBar.UpdateHealthBar();
    }

    // ステップ9: ダメージポップアップ生成
    Vector3 popupPos = transform.position + Vector3.up * 0.6f;
    if (isCritical)
    {
      DamagePopup.Create(popupPos, $"CRITICAL! -{damage}", DamagePopupType.Critical);
    }
    else
    {
      DamagePopup.Create(popupPos, $"-{damage}", DamagePopupType.Normal);
    }

    StopAllCoroutines();
    StartCoroutine(FlashRed());

    if (currentHp <= 0)
    {
      // 課題1【復活(リバース)システム】: Kingは既存の「ウェーブ毎に必ず全回復」の仕組みが別途あるため対象外。
      // King以外の駒は、UnitStatusDataSOで設定されたrebirthRateの確率で「その場で復活」できる。
      // 1バトルにつき1回までのガード(hasRebirthedThisBattle)により、無限リバースは発生しない。
      if (type != PieceType.King && !hasRebirthedThisBattle && TryRebirth())
      {
        return; // 復活成功。以降の撃破処理（ドロップ判定・死亡演出）はスキップする
      }

      // 敵駒が倒された場合のみ: 撃破数カウント＋装備ドロップ判定（ステップ5でスコア記録用に統合）
      if (isEnemy && DebugGameManager.Instance != null)
      {
        DebugGameManager.Instance.OnEnemyDefeated(type, transform.position);
      }

      StartCoroutine(DieRoutine());
    }
  }

  // 課題6【キング常時オーラ】: 自チーム(isEnemyが同じ)のKingが生存しているかどうかを判定する。
  // PieceAI.ApplyDamage側の攻撃力ボーナス判定（IsOwnTeamKingAlive、同名・同ロジック）と考え方を揃えている。
  bool IsOwnTeamKingAlive()
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()（シーン走査）をPieceRegistry.AllPieces
    // （事前に登録済みのリスト参照）へ置き換え。ループ内のロジック（King判定・isEnemy一致・currentHp>0の
    // 生存フィルタ）は既存のまま一切変更しない。
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p.type == PieceType.King && p.isEnemy == isEnemy && p.currentHp > 0) return true;
    }
    return false;
  }

  // 課題1【復活(リバース)システム】: rebirthRateの確率判定に成功した場合、その場でHPを一部回復させて復活する。
  // 成功した場合はtrueを返す（呼び出し側で以降の死亡処理をスキップするために使う）。
  bool TryRebirth()
  {
    if (DebugGameManager.Instance == null) return false;

    float rate = DebugGameManager.Instance.GetRebirthRate(type);
    if (rate <= 0f) return false;
    if (Random.value >= rate) return false;

    float hpRatio = DebugGameManager.Instance.GetRebirthHpRatio(type);
    float invincibleSeconds = DebugGameManager.Instance.GetRebirthInvincibleSeconds(type);

    currentHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * hpRatio));
    hasRebirthedThisBattle = true;
    invincibleUntilTime = Time.time + Mathf.Max(0f, invincibleSeconds);

    // 課題5【HPバー非表示バグ対策】: 復活によってHPが変化した直後は、必ず明示的にHPバーの表示更新を呼ぶ。
    // （UpdateHealthBar側でも生存中は常に再表示するよう保険をかけてあるが、二重の安全策としてここでも呼ぶ）
    if (healthBar != null)
    {
      healthBar.UpdateHealthBar();
    }

    Vector3 popupPos = transform.position + Vector3.up * 0.6f;
    DamagePopup.Create(popupPos, "REBIRTH!", DamagePopupType.Heal);

    StopAllCoroutines();
    StartCoroutine(FlashGreen());

    Debug.Log($"✨ {pieceName} が復活した！ (HP {currentHp}/{maxHp}, 無敵 {invincibleSeconds:F1}秒)");
    return true;
  }

  public void Heal(int amount)
  {
    currentHp += amount;
    if (currentHp > maxHp) currentHp = maxHp;

    if (healthBar != null)
    {
      healthBar.UpdateHealthBar();
    }

    // ステップ9: 回復ポップアップ生成
    Vector3 popupPos = transform.position + Vector3.up * 0.6f;
    DamagePopup.Create(popupPos, $"+{amount}", DamagePopupType.Heal);

    StopAllCoroutines();
    StartCoroutine(FlashGreen());
  }

  IEnumerator FlashRed()
  {
    if (meshRenderer != null)
    {
      meshRenderer.material.color = Color.white;
      yield return new WaitForSeconds(0.1f);
      meshRenderer.material.color = originalColor;
    }
  }

  IEnumerator FlashGreen()
  {
    if (meshRenderer != null)
    {
      meshRenderer.material.color = Color.green;
      yield return new WaitForSeconds(0.15f);
      meshRenderer.material.color = originalColor;
    }
  }

  IEnumerator DieRoutine()
  {
    yield return new WaitForSeconds(0.2f);
    gameObject.SetActive(false);
  }

  // 課題【合成/融合の手動選択モード】: FlashRed/FlashGreenは「一瞬だけ色を変えて戻す」演出用コルーチンだが、
  // こちらは「選択状態が続く限り持続する」表示のため別メソッドとして新設する。
  // コルーチンを止めない（StopAllCoroutines()を呼ばない）ことで、ダメージ演出等のフラッシュと
  // 同時に走っても、フラッシュが終わった後は自然にこのハイライト色へ戻る形にはならない点に注意
  // （meshRenderer.material.colorを直接上書きするため、フラッシュ側のyield明けの代入で
  // originalColorに戻ってしまう可能性がある。選択モード中はダメージ演出が発生しない前提のため、
  // 今回はこの単純な実装で問題ない）。
  public void SetSelectionHighlight(SelectionHighlightState state)
  {
    if (meshRenderer == null) return;

    switch (state)
    {
      case SelectionHighlightState.Selected:
        meshRenderer.material.color = new Color(0.3f, 1f, 0.4f); // 緑系
        break;
      case SelectionHighlightState.Eligible:
        meshRenderer.material.color = new Color(1f, 0.95f, 0.5f); // 薄い黄色系
        break;
      case SelectionHighlightState.None:
      default:
        meshRenderer.material.color = originalColor;
        break;
    }
  }
}

// 課題【合成/融合の手動選択モード】: 盤面上の駒に持続的なハイライトをかけるための状態。
// FlashRed/FlashGreenは「一瞬だけ色を変えて戻す」演出用コルーチンだが、こちらは
// 「選択状態が続く限り持続する」表示のため、PieceDataとは別にトップレベルのenumとして定義する
// （PieceType/PlayerType等、他の共有enumと同じくグローバルスコープに置くスタイルを踏襲）。
public enum SelectionHighlightState { None, Eligible, Selected }
