using UnityEngine;
using System.Collections.Generic;

public class PieceAI : MonoBehaviour
{
  private PieceData myData;
  private float timer = 0f;

  void Start()
  {
    myData = GetComponent<PieceData>();
  }

  void Update()
  {
    if (DebugGameManager.Instance != null && !DebugGameManager.Instance.isBattleStarted) return;
    if (DebugGameManager.Instance != null && DebugGameManager.Instance.isGameOver) return;
    if (myData.currentHp <= 0) return;

    // ベンチ（X > 4.5）にいる駒は行動しない
    if (DebugGameManager.Instance != null && DebugGameManager.Instance.IsWorldPositionInBenchArea(transform.position)) return;

    timer += Time.deltaTime;
    if (timer >= myData.attackInterval)
    {
      timer = 0f;
      ExecuteAction();

      // ステップ4: 連撃装備の効果（確率でもう1回行動）
      if (myData.doubleAttackChance > 0f && Random.value < myData.doubleAttackChance)
      {
        Debug.Log($"🌀【連撃発動！】{gameObject.name} が追加行動！");
        ExecuteAction();
      }
    }
  }

  // 課題【SOへのデータ統合・重複解消】: myData.typeに対応するUnitStatusDataSOのエントリを取得する共通ヘルパー。
  // DebugGameManager.GetAttackRange 等の既存Get系メソッドと同じパターン（null時はnullを返し、呼び出し側でフォールバック）を踏襲する。
  UnitStatusDataSO.UnitStatusEntry GetMyStatusEntry()
  {
    DebugGameManager gm = DebugGameManager.Instance;
    if (gm == null || gm.UnitStatusData == null) return null;
    return gm.UnitStatusData.GetStats(myData.type);
  }

  // ダメージ適用 ＆ 吸血判定
  void ApplyDamage(PieceData target, int damage, bool isCritical = false)
  {
    // 課題6【キング常時オーラ・攻撃力バフ】: 自チームのKingが生存している間、全ての攻撃ダメージに
    // kingAuraAttackBonus分のボーナスを乗せる（自分自身がKingの場合を除外する必要は無いため、一律に適用する）。
    // HPそのものを動的に増減させる実装は複雑になるため避け、あくまでダメージ計算時の倍率として扱う。
    DebugGameManager gm = DebugGameManager.Instance;
    if (gm != null && gm.UnitStatusData != null && IsOwnTeamKingAlive())
    {
      UnitStatusDataSO.UnitStatusEntry kingEntry = gm.UnitStatusData.GetStats(PieceType.King);
      if (kingEntry != null && kingEntry.kingAuraAttackBonus != 0f)
      {
        damage = Mathf.RoundToInt(damage * (1f + kingEntry.kingAuraAttackBonus));
      }
    }

    target.TakeDamage(damage, isCritical);

    if (myData.lifestealRate > 0f)
    {
      int healAmount = Mathf.RoundToInt(damage * myData.lifestealRate);
      if (healAmount > 0)
      {
        myData.Heal(healAmount);
        Debug.Log($"【吸血】{gameObject.name} が {healAmount} HP回復！");
      }
    }
  }

  // 課題6【キング常時オーラ】: 自チーム(isEnemyが同じ)のKingが生存しているかどうかを判定する共通ヘルパー。
  // 被ダメージ軽減側（PieceData.TakeDamage内の同名ロジック）とも同じ考え方（type==King, isEnemy一致, currentHp>0）で揃えている。
  bool IsOwnTeamKingAlive()
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え。
    // ループ内のロジック（King判定・isEnemy一致・currentHp>0の生存フィルタ）は既存のまま一切変更しない。
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p.type == PieceType.King && p.isEnemy == myData.isEnemy && p.currentHp > 0) return true;
    }
    return false;
  }

  void ExecuteAction()
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え。
    // 以降のforeachループ内のロジック（挑発判定・射程判定等）は一切変更しない。
    IReadOnlyList<PieceData> allPieces = PieceRegistry.AllPieces;
    PieceData target = null;
    float minDistance = float.MaxValue;

    // ステップ4: 挑発装備の効果（挑発中の敵がいれば最優先でそちらを狙う）
    PieceData tauntTarget = null;
    float tauntDistance = float.MaxValue;

    foreach (var p in allPieces)
    {
      if (DebugGameManager.Instance != null && DebugGameManager.Instance.IsWorldPositionInBenchArea(p.transform.position)) continue; // ベンチ除外

      if (p.isEnemy != myData.isEnemy && p.currentHp > 0)
      {
        float dist = Vector3.Distance(transform.position, p.transform.position);
        if (dist < minDistance)
        {
          minDistance = dist;
          target = p;
        }

        if (p.isTaunting && dist < tauntDistance)
        {
          tauntDistance = dist;
          tauntTarget = p;
        }
      }
    }

    if (tauntTarget != null)
    {
      target = tauntTarget;
      minDistance = tauntDistance;
    }
    else
    {
      // 課題【AIパターンのSO管理化】: 挑発中の敵がいない場合のみ、myData.aiBehavior（nullならBalanced扱い）に
      // 応じてターゲット選定方法を分岐する。isEnemyによる除外は行わない（敵駒・プレイヤー駒の両方に適用するため）。
      // Balancedの場合は、既に上のループで求めた「最も近い敵」（target/minDistance）をそのまま使えばよいので何もしない。
      EnemyTargetingMode mode = myData.aiBehavior != null ? myData.aiBehavior.targetingMode : EnemyTargetingMode.Balanced;

      if (mode == EnemyTargetingMode.WeakestFirst)
      {
        PieceData weakest = SelectWeakestFirstTarget(allPieces, out float weakestDist);
        if (weakest != null)
        {
          target = weakest;
          minDistance = weakestDist;
        }
      }
      else if (mode == EnemyTargetingMode.HighestValueFirst)
      {
        PieceData highestValue = SelectHighestValueFirstTarget(allPieces, out float highestValueDist);
        if (highestValue != null)
        {
          target = highestValue;
          minDistance = highestValueDist;
        }
      }
      // EnemyTargetingMode.Balanced（またはWeakestFirst/HighestValueFirstで対象が見つからなかった場合）は
      // target/minDistanceを変更せず、既存通り「最も近い敵」のままにする。
    }

    if (target == null) return;

    switch (myData.type)
    {
      case PieceType.Queen:
        ExecuteQueenAction(target, minDistance);
        break;

      case PieceType.Rook:
        // 課題3【ルーク目標優先度】: 新規にFindObjectsOfTypeを呼び直さず、既に取得済みのallPiecesと
        // 挑発ターゲット(tauntTarget、無ければnull)を渡す。ExecuteRookAction内で独自のターゲット選定を行う。
        ExecuteRookAction(target, minDistance, allPieces, tauntTarget);
        break;

      case PieceType.Bishop:
        ExecuteBishopAction(target, minDistance);
        break;

      case PieceType.Paladin:
        ExecutePaladinAction(target, minDistance);
        break;

      case PieceType.Knight:
        ExecuteKnightAction(target, minDistance);
        break;

      case PieceType.EliteCavalier:
        ExecuteEliteCavalierAction(target, minDistance);
        break;

      case PieceType.Pawn:
      default:
        ExecutePawnAction(target, minDistance);
        break;
    }
  }

  // 課題【AIパターンのSO管理化】: 弱者優先型（WeakestFirst）のターゲット選定。
  // 対象（isEnemyが逆の生存駒、ベンチ除く）の中から、currentHp / maxHp が最も低い敵を選ぶ。
  // 同点の場合は距離が近い方を優先するフォールバックを入れる。
  PieceData SelectWeakestFirstTarget(IReadOnlyList<PieceData> pieces, out float outDist)
  {
    PieceData best = null;
    float lowestRatio = float.MaxValue;
    float bestDist = float.MaxValue;

    foreach (var p in pieces)
    {
      if (DebugGameManager.Instance != null && DebugGameManager.Instance.IsWorldPositionInBenchArea(p.transform.position)) continue;
      if (p.isEnemy == myData.isEnemy || p.currentHp <= 0) continue;

      float ratio = p.maxHp > 0 ? (float)p.currentHp / p.maxHp : 0f;
      float dist = Vector3.Distance(transform.position, p.transform.position);

      if (ratio < lowestRatio || (Mathf.Approximately(ratio, lowestRatio) && dist < bestDist))
      {
        lowestRatio = ratio;
        bestDist = dist;
        best = p;
      }
    }

    outDist = bestDist;
    return best;
  }

  // 課題【AIパターンのSO管理化】: 本命特攻型（HighestValueFirst）のターゲット選定。
  // 対象の中から、UnitStatusDataSO.GetStats(p.type).shopCost が最も高い敵を選ぶ。
  // 同点（shopCostが同じ、SO未設定で全員0扱いの場合等）は距離が近い方を優先するフォールバックを入れる。
  PieceData SelectHighestValueFirstTarget(IReadOnlyList<PieceData> pieces, out float outDist)
  {
    DebugGameManager gm = DebugGameManager.Instance;
    PieceData best = null;
    int highestCost = -1;
    float bestDist = float.MaxValue;

    foreach (var p in pieces)
    {
      if (gm != null && gm.IsWorldPositionInBenchArea(p.transform.position)) continue;
      if (p.isEnemy == myData.isEnemy || p.currentHp <= 0) continue;

      int cost = 0;
      if (gm != null && gm.UnitStatusData != null)
      {
        UnitStatusDataSO.UnitStatusEntry entry = gm.UnitStatusData.GetStats(p.type);
        if (entry != null) cost = entry.shopCost;
      }

      float dist = Vector3.Distance(transform.position, p.transform.position);

      if (cost > highestCost || (cost == highestCost && dist < bestDist))
      {
        highestCost = cost;
        bestDist = dist;
        best = p;
      }
    }

    outDist = bestDist;
    return best;
  }

  void ExecuteQueenAction(PieceData target, float dist)
  {
    // 課題【二重管理バグの解消】: 以前はここに 3.5f がハードコードされており、
    // UnitStatusDataSO.attackRange（UI表示にのみ使用）とは無関係に動作していたため、
    // 「インスペクタで攻撃範囲を変更してもUI表示だけ変わり、実際の攻撃判定は変わらない」不整合があった。
    // SOから取得し、未設定時のみ従来のハードコード値へフォールバックする。
    UnitStatusDataSO.UnitStatusEntry entry = GetMyStatusEntry();
    float attackRange = entry != null ? entry.attackRange : 3.5f;
    float splashRadius = entry != null ? entry.queenSplashRadius : 1.8f;
    float kingProximityRadius = entry != null ? entry.queenKingProximityRadius : 3.0f;
    float kingProximityBonus = entry != null ? entry.queenKingProximityBonus : 1.3f;

    if (dist <= attackRange)
    {
      // 課題5【クイーン王への近接ボーナス】: 自チームのKingが生存しており、自分からqueenKingProximityRadius以内にいる間、
      // 攻撃ダメージにqueenKingProximityBonusを乗算する（範囲攻撃の巻き込み半径queenSplashRadiusには影響させない）。
      bool nearOwnKing = IsNearOwnKing(kingProximityRadius);
      int finalDamage = nearOwnKing ? Mathf.RoundToInt(myData.attack * kingProximityBonus) : myData.attack;

      int hitCount = 0;
      // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え

      foreach (var p in PieceRegistry.AllPieces)
      {
        if (DebugGameManager.Instance != null && DebugGameManager.Instance.IsWorldPositionInBenchArea(p.transform.position)) continue;

        if (p.isEnemy != myData.isEnemy && p.currentHp > 0)
        {
          float d = Vector3.Distance(target.transform.position, p.transform.position);
          // 課題: 「着弾点からの巻き込み半径」はattackRange（発動できる射程）とは別概念のため、
          // queenSplashRadiusという別フィールドをSOに追加して分離した。
          if (d <= splashRadius)
          {
            ApplyDamage(p, finalDamage);
            hitCount++;
          }
        }
      }

      string bonusText = nearOwnKing ? "（王佐ボーナス発動）" : "";
      Debug.Log($"👑【クイーン範囲攻撃！】{gameObject.name} の魔導弾が着弾！{hitCount} 体の敵に {finalDamage} ダメージ{bonusText}！");
    }
    else
    {
      SmartMoveTowards(target.transform.position);
    }
  }

  // 課題5【クイーン王への近接ボーナス】: 自チーム(isEnemyが同じ)のKingが生存しており、
  // 自分からradius以内にいるかどうかを判定する
  bool IsNearOwnKing(float radius)
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p.type != PieceType.King || p.isEnemy != myData.isEnemy || p.currentHp <= 0) continue;

      float d = Vector3.Distance(transform.position, p.transform.position);
      if (d <= radius) return true;
    }
    return false;
  }

  void ExecuteRookAction(PieceData target, float dist, IReadOnlyList<PieceData> allPieces, PieceData tauntTarget)
  {
    UnitStatusDataSO.UnitStatusEntry entry = GetMyStatusEntry();
    float attackRange = entry != null ? entry.attackRange : 1.8f;
    float straightTolerance = entry != null ? entry.rookStraightLineTolerance : 0.3f;
    float chargeMultiplier = entry != null ? entry.rookChargeMultiplier : 1.5f;

    // 課題3【ルーク目標優先度の変更（最も奥の敵を狙う）】:
    // ①挑発中の敵がいれば従来通りそちらを最優先（ExecuteAction側の共通ロジックで既にtarget/distへ反映済みのため、
    //   ここでは何もせずtarget/distをそのまま使う）。
    // ②挑発中の敵がいなければ、直線上（rookStraightLineTolerance以内）にいる敵の中から最も遠い敵を選ぶ。
    // ③直線上に敵が1体もいなければ、共通ロジックで渡された最も近い敵（target/dist）への通常攻撃にフォールバックする。
    PieceData actualTarget = target;
    float actualDist = dist;

    if (tauntTarget == null)
    {
      PieceData farthestInLine = null;
      float farthestDist = -1f;

      foreach (var p in allPieces)
      {
        if (DebugGameManager.Instance != null && DebugGameManager.Instance.IsWorldPositionInBenchArea(p.transform.position)) continue;
        if (p.isEnemy == myData.isEnemy || p.currentHp <= 0) continue;

        Vector3 diffToCandidate = p.transform.position - transform.position;
        bool isStraightLine = Mathf.Abs(diffToCandidate.x) < straightTolerance || Mathf.Abs(diffToCandidate.z) < straightTolerance;
        if (!isStraightLine) continue;

        float d = Vector3.Distance(transform.position, p.transform.position);
        if (d > farthestDist)
        {
          farthestDist = d;
          farthestInLine = p;
        }
      }

      if (farthestInLine != null)
      {
        actualTarget = farthestInLine;
        actualDist = farthestDist;
      }
      // 直線上に敵が1体もいなければ、actualTarget/actualDistはtarget/distのまま（③のフォールバック）
    }

    if (actualDist <= attackRange)
    {
      ApplyDamage(actualTarget, myData.attack);
      Debug.Log($"🏰【ルーク粉砕】{gameObject.name} が {actualTarget.gameObject.name} に {myData.attack} ダメージ！");
    }
    else
    {
      Vector3 diff = actualTarget.transform.position - transform.position;
      bool isStraight = Mathf.Abs(diff.x) < straightTolerance || Mathf.Abs(diff.z) < straightTolerance;

      if (isStraight)
      {
        SmartMoveTowards(actualTarget.transform.position);
        int chargeDamage = Mathf.RoundToInt(myData.attack * chargeMultiplier);
        ApplyDamage(actualTarget, chargeDamage, true);
        Debug.Log($"🚀【ルーク直線突進！】{gameObject.name} が {actualTarget.gameObject.name} へ強攻！ ({chargeDamage} ダメージ)");
      }
      else
      {
        SmartMoveTowards(actualTarget.transform.position);
      }
    }
  }

  void ExecuteBishopAction(PieceData target, float dist)
  {
    UnitStatusDataSO.UnitStatusEntry entry = GetMyStatusEntry();
    float attackRange = entry != null ? entry.attackRange : 3.2f;
    float meleeThreatRange = entry != null ? entry.bishopMeleeThreatRange : 3.0f;
    float meleeThreatPenaltyRate = entry != null ? entry.bishopMeleeThreatPenaltyRate : 0.5f;

    if (dist <= attackRange)
    {
      // 課題4【ビショップ近接ペナルティ】: 自分からbishopMeleeThreatRange以内に生存中の敵が1体でもいる場合、
      // 攻撃ダメージ・回復量ともにbishopMeleeThreatPenaltyRate倍に低下する（前線に晒されると本来の力を発揮できない）。
      bool underMeleeThreat = IsAnyLivingEnemyWithinRange(meleeThreatRange);
      float performanceMultiplier = underMeleeThreat ? meleeThreatPenaltyRate : 1f;

      int finalDamage = Mathf.RoundToInt(myData.attack * performanceMultiplier);
      ApplyDamage(target, finalDamage);

      if (underMeleeThreat)
      {
        Debug.Log($"🪄【ビショップ聖弾（近接ペナルティ）】{gameObject.name} が {target.gameObject.name} に {finalDamage} 遠距離ダメージ（性能低下中）！");
      }
      else
      {
        Debug.Log($"🪄【ビショップ聖弾】{gameObject.name} が {target.gameObject.name} に {finalDamage} 遠距離ダメージ！");
      }

      HealWeakestAlly(performanceMultiplier);
    }
    else
    {
      SmartMoveTowards(target.transform.position);
    }
  }

  // 課題4【ビショップ近接ペナルティ】: 自分からrange以内に、生存中の敵駒（ベンチ除く）が1体でもいるかどうかを判定する
  bool IsAnyLivingEnemyWithinRange(float range)
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (DebugGameManager.Instance != null && DebugGameManager.Instance.IsWorldPositionInBenchArea(p.transform.position)) continue;
      if (p.isEnemy == myData.isEnemy || p.currentHp <= 0) continue;

      float d = Vector3.Distance(transform.position, p.transform.position);
      if (d <= range) return true;
    }
    return false;
  }

  void HealWeakestAlly(float performanceMultiplier = 1f)
  {
    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え
    PieceData weakestAlly = null;
    float lowestHpRatio = 1.0f;

    foreach (var p in PieceRegistry.AllPieces)
    {
      if (DebugGameManager.Instance != null && DebugGameManager.Instance.IsWorldPositionInBenchArea(p.transform.position)) continue;

      if (p.isEnemy == myData.isEnemy && p.currentHp > 0 && p.currentHp < p.maxHp)
      {
        float ratio = (float)p.currentHp / p.maxHp;
        if (ratio < lowestHpRatio)
        {
          lowestHpRatio = ratio;
          weakestAlly = p;
        }
      }
    }

    if (weakestAlly != null)
    {
      UnitStatusDataSO.UnitStatusEntry entry = GetMyStatusEntry();
      int baseHealAmount = entry != null ? entry.bishopHealAmount : 20;
      // 課題4【ビショップ近接ペナルティ】: performanceMultiplier(近接ペナルティ発動中はbishopMeleeThreatPenaltyRate)を回復量にも適用する
      int healAmount = Mathf.RoundToInt(baseHealAmount * performanceMultiplier);
      weakestAlly.Heal(healAmount);
      Debug.Log($"【ビショップヒール】{gameObject.name} が {weakestAlly.gameObject.name} のHPを {healAmount} 回復！");
    }
  }

  void ExecutePaladinAction(PieceData target, float dist)
  {
    UnitStatusDataSO.UnitStatusEntry entry = GetMyStatusEntry();
    float attackRange = entry != null ? entry.attackRange : 1.8f;

    if (dist <= attackRange)
    {
      int hitCount = 0;
      // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え

      foreach (var p in PieceRegistry.AllPieces)
      {
        if (DebugGameManager.Instance != null && DebugGameManager.Instance.IsWorldPositionInBenchArea(p.transform.position)) continue;

        if (p.isEnemy != myData.isEnemy && p.currentHp > 0)
        {
          float d = Vector3.Distance(transform.position, p.transform.position);
          if (d <= attackRange)
          {
            ApplyDamage(p, myData.attack);
            hitCount++;
          }
        }
      }

      if (hitCount > 0)
      {
        Debug.Log($"【パラディン薙ぎ払い】{gameObject.name} が周囲 {hitCount} 体の敵に {myData.attack} ダメージ！");
      }
    }
    else
    {
      SmartMoveTowards(target.transform.position);
    }
  }

  void ExecutePawnAction(PieceData target, float dist)
  {
    UnitStatusDataSO.UnitStatusEntry entry = GetMyStatusEntry();
    float attackRange = entry != null ? entry.attackRange : 1.8f;
    float diagonalTolerance = entry != null ? entry.pawnDiagonalDetectionTolerance : 0.5f;
    float diagonalMultiplier = entry != null ? entry.pawnDiagonalAttackMultiplier : 1.5f;

    if (dist <= attackRange)
    {
      Vector3 diff = target.transform.position - transform.position;
      bool isDiagonal = Mathf.Abs(diff.x) > diagonalTolerance && Mathf.Abs(diff.z) > diagonalTolerance;

      // 課題1【ポーン渡河システム】: 盤面中央（BoardDepth/2）より奥まで前進した後は、
      // 横方向（同じ行、Z成分がほぼ同一）の敵に対しても、斜め攻撃と同じボーナス倍率が乗るようにする。
      // 新規SOフィールドは不要で、既存のpawnDiagonalAttackMultiplierをそのまま条件分岐で使い回す。
      bool isHorizontal = !isDiagonal && HasCrossedRiver() &&
                           Mathf.Abs(diff.z) <= diagonalTolerance && Mathf.Abs(diff.x) > diagonalTolerance;
      bool getsBonus = isDiagonal || isHorizontal;

      int finalDamage = myData.attack;
      if (getsBonus)
      {
        finalDamage = Mathf.RoundToInt(myData.attack * diagonalMultiplier);
        string style = isDiagonal ? "斜めから" : "渡河後の横撃で";
        Debug.Log($"【ポーン強襲】{gameObject.name} が{style} {target.gameObject.name} に {finalDamage} ダメージ！");
      }
      else
      {
        Debug.Log($"【ポーン攻撃】{gameObject.name} が {target.gameObject.name} に {finalDamage} ダメージ！");
      }

      ApplyDamage(target, finalDamage, getsBonus);
    }
    else
    {
      SmartMoveTowards(target.transform.position);
    }
  }

  // 課題1【ポーン渡河システム】: 自分の現在位置を盤面グリッド座標に変換し、
  // Z成分がBoardDepth/2を超えていれば「渡河済み」と判定する。
  // プレイヤー駒は前進方向（Z増加方向）、敵駒は逆方向（Z減少方向）に越えたら発動するよう、
  // isEnemyに応じて判定の向きを反転させる。
  bool HasCrossedRiver()
  {
    DebugGameManager gm = DebugGameManager.Instance;
    if (gm == null) return false;

    Vector2Int gridIndex = gm.WorldToNearestGridIndex(transform.position);
    float midline = gm.BoardDepth / 2f;

    return myData.isEnemy ? (gridIndex.y < midline) : (gridIndex.y > midline);
  }

  void ExecuteKnightAction(PieceData target, float dist)
  {
    UnitStatusDataSO.UnitStatusEntry entry = GetMyStatusEntry();
    float attackRange = entry != null ? entry.attackRange : 1.8f;
    float jumpDistance = entry != null ? entry.knightJumpDistance : 1.0f;
    float criticalMultiplier = entry != null ? entry.knightCriticalMultiplier : 2f;

    if (dist <= attackRange)
    {
      ApplyDamage(target, myData.attack);
      Debug.Log($"【ナイト通常攻撃】{gameObject.name} が {target.gameObject.name} に {myData.attack} ダメージ！");
      return;
    }

    // 課題2【ナイト蹩馬腿（足封じ）】: L字ジャンプで背後に回り込む際、ジャンプ方向（自分→ターゲット方向）の
    // 自分に隣接するマスに、生存中の駒（味方・敵問わず）が存在する場合、ジャンプそのものを封じる。
    DebugGameManager gmForJump = DebugGameManager.Instance;
    float cellStep = gmForJump != null ? gmForJump.WorldCellSize : 1f;

    Vector3 jumpDirection = target.transform.position - transform.position;
    jumpDirection.y = 0f;
    if (jumpDirection.sqrMagnitude > 0.0001f) jumpDirection = jumpDirection.normalized;
    Vector3 legCheckPos = transform.position + jumpDirection * cellStep;

    if (IsSquareOccupiedByAnyone(legCheckPos))
    {
      // 足を封じられた場合のフォールバック: 射程内なら通常ダメージ（=既にdist<=attackRangeで処理済みのためここには来ない）、
      // 射程外なら何もしない。
      Debug.Log($"🦵【蹩馬腿！】{gameObject.name} の跳躍ルートを足で塞がれ、ジャンプできなかった。");
      return;
    }

    Vector3 targetPos = target.transform.position;
    Vector3 jumpPos = Vector3.MoveTowards(targetPos, transform.position, jumpDistance);

    // ステップ16: BoardParent基準のグリッドインデックスへスナップする
    Vector3 nextGrid = jumpPos;
    DebugGameManager gm = DebugGameManager.Instance;
    if (gm != null)
    {
      Vector2Int nearest = gm.WorldToNearestGridIndex(jumpPos);
      nextGrid = gm.GridToWorldPosition(nearest.x, nearest.y, transform.position.y);
    }

    if (IsCellEmpty(nextGrid))
    {
      transform.position = nextGrid;
      int criticalDamage = Mathf.RoundToInt(myData.attack * criticalMultiplier);
      ApplyDamage(target, criticalDamage, true);
      Debug.Log($"【ナイトL字ジャンプ！】{gameObject.name} が {target.gameObject.name} の背後へ強襲！（{criticalDamage} ダメージ）");
    }
    else
    {
      SmartMoveTowards(target.transform.position);
    }
  }

  // 課題【異種合成「精鋭騎兵」】: ポーン×ナイトの融合進化専用の行動ロジック。
  // 射程内はExecutePawnActionと同じ「斜め/渡河後の横方向なら1.5倍、それ以外は通常ダメージ」を、
  // 射程外はExecuteKnightActionと同じ「足封じ判定→L字ジャンプ→クリティカルダメージ」をそのまま適用する。
  // 共通化はせず、要件通り中身をコピーして流用する形にしている（今回は動くことを優先）。
  void ExecuteEliteCavalierAction(PieceData target, float dist)
  {
    UnitStatusDataSO.UnitStatusEntry entry = GetMyStatusEntry();
    float attackRange = entry != null ? entry.attackRange : 1.8f;

    if (dist <= attackRange)
    {
      // ─── ExecutePawnActionと同じロジック（射程内） ───
      float diagonalTolerance = entry != null ? entry.pawnDiagonalDetectionTolerance : 0.5f;
      float diagonalMultiplier = entry != null ? entry.pawnDiagonalAttackMultiplier : 1.5f;

      Vector3 diff = target.transform.position - transform.position;
      bool isDiagonal = Mathf.Abs(diff.x) > diagonalTolerance && Mathf.Abs(diff.z) > diagonalTolerance;

      bool isHorizontal = !isDiagonal && HasCrossedRiver() &&
                           Mathf.Abs(diff.z) <= diagonalTolerance && Mathf.Abs(diff.x) > diagonalTolerance;
      bool getsBonus = isDiagonal || isHorizontal;

      int finalDamage = myData.attack;
      if (getsBonus)
      {
        finalDamage = Mathf.RoundToInt(myData.attack * diagonalMultiplier);
        string style = isDiagonal ? "斜めから" : "渡河後の横撃で";
        Debug.Log($"【精鋭騎兵強襲】{gameObject.name} が{style} {target.gameObject.name} に {finalDamage} ダメージ！");
      }
      else
      {
        Debug.Log($"【精鋭騎兵攻撃】{gameObject.name} が {target.gameObject.name} に {finalDamage} ダメージ！");
      }

      ApplyDamage(target, finalDamage, getsBonus);
      return;
    }

    // ─── ExecuteKnightActionと同じロジック（射程外）。通常攻撃へのフォールバックは発生しない ───
    float jumpDistance = entry != null ? entry.knightJumpDistance : 1.0f;
    float criticalMultiplier = entry != null ? entry.knightCriticalMultiplier : 2f;

    DebugGameManager gmForJump = DebugGameManager.Instance;
    float cellStep = gmForJump != null ? gmForJump.WorldCellSize : 1f;

    Vector3 jumpDirection = target.transform.position - transform.position;
    jumpDirection.y = 0f;
    if (jumpDirection.sqrMagnitude > 0.0001f) jumpDirection = jumpDirection.normalized;
    Vector3 legCheckPos = transform.position + jumpDirection * cellStep;

    if (IsSquareOccupiedByAnyone(legCheckPos))
    {
      // 足を封じられた場合: Knightの既存の挙動と同様に何もしない（SmartMoveTowardsで接近するのみ）
      Debug.Log($"🦵【蹩馬腿！】{gameObject.name} の跳躍ルートを足で塞がれ、ジャンプできなかった。");
      SmartMoveTowards(target.transform.position);
      return;
    }

    Vector3 targetPos = target.transform.position;
    Vector3 jumpPos = Vector3.MoveTowards(targetPos, transform.position, jumpDistance);

    Vector3 nextGrid = jumpPos;
    DebugGameManager gm = DebugGameManager.Instance;
    if (gm != null)
    {
      Vector2Int nearest = gm.WorldToNearestGridIndex(jumpPos);
      nextGrid = gm.GridToWorldPosition(nearest.x, nearest.y, transform.position.y);
    }

    if (IsCellEmpty(nextGrid))
    {
      transform.position = nextGrid;
      int criticalDamage = Mathf.RoundToInt(myData.attack * criticalMultiplier);
      ApplyDamage(target, criticalDamage, true);
      Debug.Log($"【精鋭騎兵L字ジャンプ！】{gameObject.name} が {target.gameObject.name} の背後へ強襲！（{criticalDamage} ダメージ）");
    }
    else
    {
      SmartMoveTowards(target.transform.position);
    }
  }

  // 課題2【ナイト蹩馬腿】: 座標(worldPos)に、自分以外の生存中の駒（味方・敵問わず）が存在するかどうかを判定する。
  // IsCellEmpty()と似ているが、盤面境界チェックは行わず「占有されているかどうか」のみを見る点が異なる
  // （足封じの判定は盤面外かどうかとは無関係なため）。
  bool IsSquareOccupiedByAnyone(Vector3 worldPos)
  {
    DebugGameManager gm = DebugGameManager.Instance;
    float threshold = 0.6f * (gm != null ? gm.WorldCellSize : 1f);

    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え
    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p == myData || p.currentHp <= 0) continue;

      float dist = Vector3.Distance(new Vector3(worldPos.x, 0, worldPos.z),
                                    new Vector3(p.transform.position.x, 0, p.transform.position.z));
      if (dist < threshold) return true;
    }
    return false;
  }

  void SmartMoveTowards(Vector3 targetPosition)
  {
    // ステップ17: 移動の1歩幅をWorldCellSize（BoardParentの実スケール）に連動させる。
    // これによりBoardParentを拡大縮小しても、AIは正しく「1マス分」だけ移動する。
    float step = DebugGameManager.Instance != null ? DebugGameManager.Instance.WorldCellSize : 1f;

    Vector3 diff = targetPosition - transform.position;

    float primaryX = Mathf.Abs(diff.x) > 0.3f * step ? Mathf.Sign(diff.x) : 0;
    float primaryZ = Mathf.Abs(diff.z) > 0.3f * step ? Mathf.Sign(diff.z) : 0;

    Vector3 posDirect = transform.position + new Vector3(primaryX * step, 0, primaryZ * step);

    if (IsCellEmpty(posDirect))
    {
      transform.position = posDirect;
      return;
    }

    Vector3 posSideX = transform.position + new Vector3((primaryX != 0 ? primaryX : 1.0f) * step, 0, 0);
    Vector3 posSideXAlt = transform.position + new Vector3((primaryX != 0 ? -primaryX : -1.0f) * step, 0, 0);
    Vector3 posSideZ = transform.position + new Vector3(0, 0, primaryZ * step);

    if (primaryX != 0 && IsCellEmpty(posSideX))
    {
      transform.position = posSideX;
    }
    else if (IsCellEmpty(posSideXAlt))
    {
      transform.position = posSideXAlt;
    }
    else if (primaryZ != 0 && IsCellEmpty(posSideZ))
    {
      transform.position = posSideZ;
    }
  }

  bool IsCellEmpty(Vector3 targetCell)
  {
    DebugGameManager gm = DebugGameManager.Instance;

    // ステップ16/17: BoardParent基準のローカル座標に変換してから境界判定する（ローカル空間は常に1マス=1ユニット、倍率なし）。
    // これによりBoardParentの位置・回転・スケールを変更しても、正しい盤面外判定ができる。
    //
    // 課題6【陣地進入・移動制限の適正化】: 以前はここに8x8盤面時代の非対称な固定値
    // （X: -3.5〜3.5, Z: -2.5〜4.5）がハードコードされており、盤面の中心合わせ修正後の
    // 対称な座標系（Z: -3.5〜3.5相当）とズレてしまっていた。これにより、
    // 「敵が自陣2列に侵入できない」「味方が敵陣2列に侵入できない」という、
    // 戦闘フェーズでは本来存在しないはずの陣地制限が誤って発生していた。
    // 配置フェーズの自陣制限（PlayerFrontRowDepth）はPieceDraggable.SnapToGrid側のみに存在し、
    // かつ戦闘中はそもそもドラッグ移動自体が無効化されているため、ここ（戦闘フェーズのAI移動判定）は
    // BoardWidth/BoardDepthから動的に算出した「盤面全体」の境界のみをチェックし、
    // 陣地による制限は一切行わない（盤面全体のどこへでも移動できる）。
    if (gm != null)
    {
      Vector3 local = gm.WorldToBoardLocal(targetCell);
      float halfW = gm.BoardWidth / 2f;
      float halfD = gm.BoardDepth / 2f;
      if (local.x < -halfW || local.x > halfW ||
          local.z < -halfD || local.z > halfD)
      {
        return false;
      }
    }

    // 課題【駒レジストリ】: FindObjectsOfType<PieceData>()をPieceRegistry.AllPiecesへ置き換え

    // 占有判定はワールド空間の実距離で行うため、BoardParentの現在の実スケール（WorldCellSize）を使う
    float threshold = 0.6f * (gm != null ? gm.WorldCellSize : 1f);

    foreach (var p in PieceRegistry.AllPieces)
    {
      if (p == myData || p.currentHp <= 0) continue;

      float dist = Vector3.Distance(new Vector3(targetCell.x, 0, targetCell.z),
                                    new Vector3(p.transform.position.x, 0, p.transform.position.z));
      if (dist < threshold) return false;
    }
    return true;
  }
}
