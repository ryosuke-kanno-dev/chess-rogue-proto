using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Chebyss.Battle;

namespace Chebyss.Tests
{
    /// <summary>
    /// 戦闘のコアの軽量な自動テスト（Editor専用）。メニュー「Chebyss/コアのテストを実行」から実行する。
    /// Unity Test Framework・asmdefは使わない。ゲームのコードは変更しない。
    /// </summary>
    public static class CoreTests
    {
        private const string KeyBindingsPrefsKey = "Chebyss.KeyBindings.v1";

        private class TestFailure : Exception
        {
            public TestFailure(string message) : base(message) { }
        }

        // テスト中に作成したScriptableObject（各テストの最後にDestroyImmediateする）
        private static readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        // ---------------------------------------------------------------
        // 実行
        // ---------------------------------------------------------------

        [MenuItem("Chebyss/コアのテストを実行")]
        public static void RunAllMenu()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[CoreTests] 再生中は実行できません（RunStateなどの実行中の状態を壊さないため）。再生を止めてから実行してください。");
                return;
            }

            RunAll();
        }

        public static int RunAll()
        {
            var tests = BuildTests();
            int passed = 0;
            int failed = 0;

            foreach (var test in tests)
            {
                RunState.Reset();
                try
                {
                    test.run();
                    passed++;
                }
                catch (Exception exception)
                {
                    failed++;
                    string reason = exception is TestFailure
                        ? exception.Message
                        : exception.GetType().Name + ": " + exception.Message + "\n" + exception.StackTrace;
                    Debug.LogError($"[CoreTests] 失敗: {test.name}\n{reason}");
                }
                finally
                {
                    CleanupCreated();
                    RunState.Reset();
                }
            }

            string summary = $"[CoreTests] {tests.Count} 件中 {passed} 件成功、{failed} 件失敗";
            if (failed == 0) Debug.Log(summary);
            else Debug.LogError(summary);

            return failed;
        }

        private static void CleanupCreated()
        {
            foreach (var obj in created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        // ---------------------------------------------------------------
        // テスト一覧
        // ---------------------------------------------------------------

        private static List<(string name, Action run)> BuildTests()
        {
            var list = new List<(string name, Action run)>();
            void Add(string name, Action run) => list.Add((name, run));

            // A. BoardState
            Add("BoardState: 駒の配置・移動・IsOccupied・キングの位置", A_PlaceAndMove);
            Add("BoardState: ドロップはIsOccupiedに含まれない", A_DropNotOccupied);
            Add("BoardState: TryPlaceDrop（空き・埋まっている・空き無し）", A_TryPlaceDrop);
            Add("BoardState: TryTakeDropの後にドロップが消える", A_TryTakeDrop);
            Add("BoardState: Cloneはドロップを複製せず、駒は独立している", A_CloneNoDrops);
            Add("BoardState: 致死のApplyAttackで味方駒が墓地に入り盤面から消える", A_LethalAttackGraveyard);
            Add("BoardState: 非致死ダメージ・吹き飛ばし・状態異常の付与と更新", A_NonLethalAttack);
            Add("BoardState: AdvanceTurnで行動回数が戻り状態異常が減る", A_AdvanceTurn);

            // B. 各ルール
            Add("KnightDashRule: 突進段数と移動・攻撃範囲", B_KnightDashRange);
            Add("KnightDashRule: 2段目のダメージが1段目より大きい", B_KnightTierDamage);
            Add("KnightDashRule: 距離ボーナスは基礎値+statModifiersの加算", B_KnightDistanceBonusAdditive);
            Add("KnightDashRule: 撃破時のみ追撃移動の候補が出る", B_KnightPursuit);
            Add("RookLineRule: 吹き飛ばし確率0で発生しない", B_RookKnockbackZero);
            Add("RookLineRule: 吹き飛ばし確率1で非撃破時に発生・撃破時は発生しない", B_RookKnockbackOne);
            Add("RookLineRule: 縦横の射線（自駒で遮られ、最初の敵が攻撃対象）", B_RookLines);
            Add("PawnAdvanceRule: rank1の移動と斜め前攻撃", B_PawnRank1);
            Add("PawnAdvanceRule: rank2の移動（前・左右に最大2マス、後退なし、遮られる）", B_PawnRank2Move);
            Add("PawnAdvanceRule: rank2の攻撃（3方向・最大2マス・遮られる）", B_PawnRank2Attack);

            // C. EnemyTurnProcessor
            Add("EnemyTurnProcessor: キングに隣接した敵がキングを攻撃する", C_AttackKing);
            Add("EnemyTurnProcessor: キングに隣接しないとき最もHPの低い隣接駒を攻撃する", C_AttackWeakest);
            Add("EnemyTurnProcessor: キングが隣接していればキングを優先する", C_KingPriority);
            Add("EnemyTurnProcessor: スタンされた敵は何もしない", C_Stunned);
            Add("EnemyTurnProcessor: スロウは移動だけを遅らせる", C_Slow);
            Add("EnemyTurnProcessor: 味方駒が全滅するとallPiecesDefeated", C_AllPiecesDefeated);
            Add("EnemyTurnProcessor: PreviewNextTurnは本物の盤面を変えない", C_Preview);

            // D. EncounterGenerator
            Add("EncounterGenerator: 同じシード・同じ階で同じ計画", D_Deterministic);
            Add("EncounterGenerator: ボス階は間隔の倍数と最終階", D_BossFloors);
            Add("EncounterGenerator: ラスボスのHP倍率・攻撃力加算", D_FinalBossScaling);
            Add("EncounterGenerator: 階が進んでも敵の数とHPが減らない", D_Monotonic);
            Add("EncounterGenerator: 敵の位置が盤面内で重複しない", D_Positions);
            Add("EncounterGenerator: 逃げる敵は通常階のみ・許可時のみ", D_Retreater);

            // E. BattleEquipmentGenerator
            Add("BattleEquipmentGenerator: ボーナスの数・重複・候補プール・値の範囲", E_GenerateRules);
            Add("BattleEquipmentGenerator.CreateUpgraded: 手作りの装備での確認", E_UpgradedHandmade);
            Add("BattleEquipmentGenerator.CreateUpgraded: 多数回の生成での性質", E_UpgradedSweep);
            Add("BattleEquipmentGenerator.FormatBonus: 全種類で空でない", E_FormatBonus);

            // F. RunState
            Add("RunState: 装備・外す・入れ替え", F_EquipUnequip);
            Add("RunState: MarkPieceDeadで装備が所持品に戻る", F_MarkDead);
            Add("RunState: TryReviveが装備を1つ消費する", F_Revive);
            Add("RunState: 墓地の駒に装備できない", F_DeadCannotEquip);
            Add("RunState: スキルの取得条件", F_SkillAcquire);
            Add("RunState: スキルから導出される値", F_SkillDerived);
            Add("RunState: SetPawnGrowthの丸めとRecordVictory", F_PawnGrowthAndStats);
            Add("RunState: 配置の記憶", F_Placement);
            Add("RunState: Resetの初期化", F_Reset);

            // G. KingSkillCatalog
            Add("KingSkillCatalog: id重複なし・前提の存在・コスト・循環なし・全て取得可能", G_Catalog);

            // H. KeyBindings
            Add("KeyBindings: 初期設定の割り当て", () => WithCleanKeyBindings(H_Defaults));
            Add("KeyBindings: Setで同じ内容の他の割り当てが解除される", () => WithCleanKeyBindings(H_SetConflict));
            Add("KeyBindings: Clear・ResetToDefault・Changedイベント", () => WithCleanKeyBindings(H_ClearReset));
            Add("KeyBindings: GetLabel・GetMoveLabelの文字列", () => WithCleanKeyBindings(H_Labels));
            Add("KeyBindings: 保存と再読み込み", () => WithCleanKeyBindings(H_Persistence));

            // I. DropRules
            Add("DropRules: 品質Tierの上限は3", I_DropRules);

            // J. PieceSnapshotFactory
            Add("PieceSnapshotFactory: ComputeMaxActionsがBonusActionsを加味する", J_MaxActions);

            return list;
        }

        // ---------------------------------------------------------------
        // 検証用ヘルパー
        // ---------------------------------------------------------------

        private static void Fail(string message)
        {
            throw new TestFailure(message);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) Fail(message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                Fail($"{message}（期待: {expected} / 実際: {actual}）");
        }

        private static void AssertApprox(float expected, float actual, string message, float tolerance = 0.0001f)
        {
            if (Mathf.Abs(expected - actual) > tolerance)
                Fail($"{message}（期待: {expected} / 実際: {actual}）");
        }

        private static void AssertSetEqual(IEnumerable<Vector2Int> expected, IEnumerable<Vector2Int> actual, string message)
        {
            var e = new HashSet<Vector2Int>(expected);
            var a = new List<Vector2Int>(actual);
            var aSet = new HashSet<Vector2Int>(a);
            if (a.Count != aSet.Count) Fail($"{message}（重複した要素がある: {string.Join(",", a)}）");
            if (!e.SetEquals(aSet))
                Fail($"{message}（期待: {string.Join(",", e)} / 実際: {string.Join(",", aSet)}）");
        }

        private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        private static int Cheb(Vector2Int a, Vector2Int b) =>
            Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        private static T NewSO<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            created.Add(so);
            return so;
        }

        private static PieceSnapshot Player(PieceType type, int x, int y, int hp, int attack,
            Dictionary<string, float> modifiers = null)
        {
            return PieceSnapshotFactory.Create(type, true, V(x, y), hp, hp, attack, modifiers, null);
        }

        private static PieceSnapshot Enemy(EnemyArchetype archetype, int x, int y, int hp, int attack,
            IEnumerable<ActiveStatusEffect> effects = null)
        {
            return PieceSnapshotFactory.Create(PieceType.King, false, V(x, y), hp, hp, attack, null, effects, archetype);
        }

        private static Dictionary<string, float> Mods(params (string key, float value)[] pairs)
        {
            var d = new Dictionary<string, float>();
            foreach (var p in pairs) d[p.key] = p.value;
            return d;
        }

        private static EquipmentStatBonus B(EquipmentStatType type, float value)
        {
            return new EquipmentStatBonus { statType = type, value = value };
        }

        private static EquipmentInstance Item(string name, EquipmentRarity rarity, params EquipmentStatBonus[] bonuses)
        {
            return new EquipmentInstance
            {
                itemId = Guid.NewGuid().ToString(),
                itemName = name,
                rarity = rarity,
                bonuses = new List<EquipmentStatBonus>(bonuses)
            };
        }

        // ---------------------------------------------------------------
        // A. BoardState
        // ---------------------------------------------------------------

        private static void A_PlaceAndMove()
        {
            var board = new BoardState(8, 8, V(4, 0));

            var knight = Player(PieceType.Knight, 2, 1, 10, 3);
            board.PlacePiece(knight);
            AssertTrue(board.TryGetPlayerPiece(V(2, 1), out var got), "配置した味方駒を取得できる");
            AssertEqual(knight.instanceId, got.instanceId, "取得した駒のinstanceId");
            AssertTrue(board.IsOccupied(V(2, 1)), "駒のいるマスはIsOccupied");
            AssertTrue(!board.IsOccupied(V(3, 3)), "空きマスはIsOccupiedでない");

            var scout = Enemy(EnemyArchetype.Scout, 3, 3, 5, 2);
            board.PlacePiece(scout);
            AssertTrue(board.IsOccupied(V(3, 3)), "敵のいるマスもIsOccupied");
            AssertTrue(board.TryGetEnemyPiece(V(3, 3), out _), "敵駒を取得できる");
            AssertTrue(!board.TryGetPlayerPiece(V(3, 3), out _), "敵のマスに味方駒は無い");

            board.ApplyMove(true, V(2, 1), V(2, 2));
            AssertTrue(!board.TryGetPlayerPiece(V(2, 1), out _), "移動元から駒が消える");
            AssertTrue(board.TryGetPlayerPiece(V(2, 2), out var moved), "移動先に駒がいる");
            AssertEqual(V(2, 2), moved.position, "移動後のposition");
            AssertEqual(knight.instanceId, moved.instanceId, "移動してもinstanceIdは同じ");

            board.ApplyMove(false, V(3, 3), V(3, 4));
            AssertTrue(!board.IsOccupied(V(3, 3)) && board.IsOccupied(V(3, 4)), "敵駒の移動");

            var king = Player(PieceType.King, 5, 0, 20, 0);
            board.PlacePiece(king);
            AssertEqual(V(5, 0), board.KingPosition, "キングを配置するとKingPositionが更新される");
            board.ApplyMove(true, V(5, 0), V(5, 1));
            AssertEqual(V(5, 1), board.KingPosition, "キングを移動するとKingPositionが更新される");

            AssertTrue(board.IsInBounds(V(0, 0)) && board.IsInBounds(V(7, 7)), "盤面内");
            AssertTrue(!board.IsInBounds(V(-1, 0)) && !board.IsInBounds(V(8, 0)) && !board.IsInBounds(V(0, 8)), "盤面外");

            board.RemovePiece(true, V(2, 2));
            AssertTrue(!board.IsOccupied(V(2, 2)), "RemovePieceで駒が消える");
            AssertEqual(0, board.Graveyard.Count, "RemovePieceは墓地に送らない");

            // TryFindPieceById
            AssertTrue(board.TryFindPieceById(scout.instanceId, out var found, out bool isPlayer, out var pos), "instanceIdで駒を探せる");
            AssertTrue(!isPlayer, "敵側として見つかる");
            AssertEqual(V(3, 4), pos, "見つかった位置");
            AssertEqual(scout.instanceId, found.instanceId, "見つかった駒のid");
            AssertTrue(!board.TryFindPieceById(-12345, out _, out _, out _), "存在しないidは見つからない");
        }

        private static void A_DropNotOccupied()
        {
            var board = new BoardState(8, 8, V(4, 0));
            var item = Item("古びた剣", EquipmentRarity.Common, B(EquipmentStatType.AttackFlat, 1));

            AssertTrue(board.TryPlaceDrop(item, null, V(3, 3), out var entry), "空きマスにドロップを置ける");
            AssertEqual(V(3, 3), entry.position, "ドロップの位置");
            AssertTrue(board.HasDropAt(V(3, 3)), "HasDropAt");
            AssertTrue(!board.IsOccupied(V(3, 3)), "ドロップはIsOccupiedに含まれない");

            // ドロップのあるマスへ駒が移動できる（妨げない）
            board.PlacePiece(Player(PieceType.Rook, 3, 2, 10, 3));
            board.ApplyMove(true, V(3, 2), V(3, 3));
            AssertTrue(board.IsOccupied(V(3, 3)), "ドロップのあるマスに駒が入れる");
            AssertTrue(board.HasDropAt(V(3, 3)), "駒が入ってもドロップは残る（拾得は呼び出し側の責務）");
        }

        private static void A_TryPlaceDrop()
        {
            var item = Item("古びた剣", EquipmentRarity.Common, B(EquipmentStatType.AttackFlat, 1));

            // 空きマス：originにそのまま置く
            var board = new BoardState(8, 8, V(4, 0));
            AssertTrue(board.TryPlaceDrop(item, null, V(3, 3), out var first), "空きマスに置ける");
            AssertEqual(V(3, 3), first.position, "空きならoriginに置く");

            // 既にドロップがあるマス：最寄り（距離1で、x→y順で先の(2,2)）
            AssertTrue(board.TryPlaceDrop(item, null, V(3, 3), out var second), "既にドロップがあるマスを指定しても置ける");
            AssertEqual(V(2, 2), second.position, "最寄りの空きマス（同距離はx→y順）");
            AssertTrue(second.dropId > first.dropId, "dropIdは増える");

            // 駒がいるマス：最寄りの空きマス
            var board2 = new BoardState(8, 8, V(4, 0));
            board2.PlacePiece(Player(PieceType.Knight, 3, 3, 10, 3));
            AssertTrue(board2.TryPlaceDrop(item, null, V(3, 3), out var third), "駒のいるマスが指定でも置ける");
            AssertEqual(V(2, 2), third.position, "駒のいるマスの代わりに最寄りの空きマス");
            AssertTrue(!board2.HasDropAt(V(3, 3)), "駒のいるマスにはドロップを置かない");

            // 空きが無い：false
            var tiny = new BoardState(2, 2, V(0, 0));
            tiny.PlacePiece(Player(PieceType.King, 0, 0, 10, 0));
            tiny.PlacePiece(Player(PieceType.Knight, 1, 0, 10, 3));
            tiny.PlacePiece(Player(PieceType.Rook, 0, 1, 10, 3));
            tiny.PlacePiece(Enemy(EnemyArchetype.Scout, 1, 1, 5, 2));
            AssertTrue(!tiny.TryPlaceDrop(item, null, V(0, 0), out _), "空きマスが無いときはfalse");
            AssertEqual(0, tiny.GetAllDropsOrdered().Count, "失敗したらドロップは増えない");

            // 盤面外のoriginでも盤面内の空きへ置ける／null装備はfalse
            AssertTrue(board.TryPlaceDrop(item, null, V(20, 20), out var outside), "盤面外のoriginでも最寄りの空きへ");
            AssertTrue(board.IsInBounds(outside.position), "置かれた位置は盤面内");
            AssertTrue(!board.TryPlaceDrop(null, null, V(1, 1), out _), "装備がnullならfalse");

            // +1版が保持される
            var boosted = Item("魔法の剣", EquipmentRarity.Rare, B(EquipmentStatType.AttackFlat, 2));
            AssertTrue(board.TryPlaceDrop(item, boosted, V(6, 6), out var withBoost), "+1版付きで置ける");
            AssertTrue(ReferenceEquals(withBoost.boostedItem, boosted), "+1版が保持される");
        }

        private static void A_TryTakeDrop()
        {
            var board = new BoardState(8, 8, V(4, 0));
            var item = Item("古びた剣", EquipmentRarity.Common, B(EquipmentStatType.AttackFlat, 1));
            board.TryPlaceDrop(item, null, V(6, 6), out _);
            board.TryPlaceDrop(item, null, V(1, 5), out _);
            board.TryPlaceDrop(item, null, V(1, 2), out _);

            var ordered = board.GetAllDropsOrdered();
            AssertEqual(3, ordered.Count, "ドロップ3件");
            AssertEqual(V(1, 2), ordered[0].position, "x→y順の1番目");
            AssertEqual(V(1, 5), ordered[1].position, "x→y順の2番目");
            AssertEqual(V(6, 6), ordered[2].position, "x→y順の3番目");

            // 回収中に取り出しても安全（コピーを返している）
            foreach (var drop in ordered)
                AssertTrue(board.TryTakeDrop(drop.position, out _), "回収中にTryTakeDropしても安全");
            AssertEqual(0, board.GetAllDropsOrdered().Count, "全て回収したら空");

            board.TryPlaceDrop(item, null, V(3, 3), out var placed);
            AssertTrue(board.TryTakeDrop(V(3, 3), out var taken), "TryTakeDropで取り出せる");
            AssertEqual(placed.dropId, taken.dropId, "取り出したのは置いたドロップ");
            AssertTrue(!board.HasDropAt(V(3, 3)), "取り出した後はドロップが消える");
            AssertTrue(!board.TryGetDropAt(V(3, 3), out _), "TryGetDropAtも失敗する");
            AssertTrue(!board.TryTakeDrop(V(3, 3), out _), "2回目は取り出せない");
        }

        private static void A_CloneNoDrops()
        {
            var board = new BoardState(8, 8, V(4, 0));
            board.PlacePiece(Player(PieceType.King, 4, 0, 20, 0));
            board.PlacePiece(Player(PieceType.Knight, 2, 1, 10, 3));
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 5, 5, 6, 2));
            board.TryPlaceDrop(Item("古びた剣", EquipmentRarity.Common, B(EquipmentStatType.AttackFlat, 1)), null, V(3, 3), out _);

            var clone = board.Clone();
            AssertTrue(!clone.HasDropAt(V(3, 3)), "Cloneにドロップは複製されない");
            AssertEqual(0, clone.GetAllDropsOrdered().Count, "Cloneのドロップは空");
            AssertTrue(board.HasDropAt(V(3, 3)), "元のドロップは残っている");
            AssertTrue(clone.TryGetPlayerPiece(V(2, 1), out _), "駒は複製される");
            AssertEqual(board.KingPosition, clone.KingPosition, "キングの位置も複製される");
            AssertEqual(board.CurrentTurn, clone.CurrentTurn, "ターン数も複製される");

            // 複製側の変更が本物に影響しない
            clone.ApplyAttack(false, V(5, 5), new AttackOutcome(2, false, null, null, null, null));
            board.TryGetEnemyPiece(V(5, 5), out var original);
            AssertEqual(6, original.currentHp, "複製側のダメージは本物に影響しない");
            clone.TryGetEnemyPiece(V(5, 5), out var cloned);
            AssertEqual(4, cloned.currentHp, "複製側にはダメージが入る");

            // 状態異常のリストも独立している（ディープコピー）
            var effects = new List<OnHitStatusEffect> { new OnHitStatusEffect { type = StatusEffectType.Stun, duration = 2 } };
            clone.ApplyAttack(false, V(5, 5), new AttackOutcome(0, false, effects, null, null, null));
            board.TryGetEnemyPiece(V(5, 5), out original);
            AssertEqual(0, original.statusEffects.Count, "複製側の状態異常は本物に影響しない");

            clone.ApplyMove(true, V(2, 1), V(2, 2));
            AssertTrue(board.TryGetPlayerPiece(V(2, 1), out _), "複製側の移動は本物に影響しない");
        }

        private static void A_LethalAttackGraveyard()
        {
            var board = new BoardState(8, 8, V(4, 0));
            var knight = Player(PieceType.Knight, 2, 1, 3, 3);
            board.PlacePiece(knight);

            AssertTrue(!board.WasDefeatedThisBattle(knight.instanceId), "倒される前は墓地に無い");

            board.ApplyAttack(true, V(2, 1), new AttackOutcome(5, true, null, null, null, null));

            AssertTrue(!board.IsOccupied(V(2, 1)), "致死の攻撃で盤面から消える");
            AssertTrue(!board.TryGetPlayerPiece(V(2, 1), out _), "味方駒として取得できない");
            AssertEqual(1, board.Graveyard.Count, "墓地に1件入る");
            AssertEqual(knight.instanceId, board.Graveyard[0].instanceId, "墓地のinstanceId");
            AssertEqual(PieceType.Knight, board.Graveyard[0].pieceType, "墓地のpieceType");
            AssertEqual(board.CurrentTurn, board.Graveyard[0].turnDefeated, "墓地のturnDefeated");
            AssertTrue(board.WasDefeatedThisBattle(knight.instanceId), "WasDefeatedThisBattle");
            AssertTrue(!board.TryFindPieceById(knight.instanceId, out _, out _, out _), "TryFindPieceByIdで見つからない");

            // 敵が倒れた場合も墓地リストに入る（実装どおり）。存在しないマスへの致死攻撃は何もしない
            board.ApplyAttack(true, V(7, 7), new AttackOutcome(5, true, null, null, null, null));
            AssertEqual(1, board.Graveyard.Count, "空きマスへの致死攻撃で墓地は増えない");
        }

        private static void A_NonLethalAttack()
        {
            var board = new BoardState(8, 8, V(4, 0));
            var scout = Enemy(EnemyArchetype.Scout, 5, 5, 6, 2);
            board.PlacePiece(scout);

            var stun = new List<OnHitStatusEffect>
            {
                new OnHitStatusEffect { type = StatusEffectType.Stun, duration = 2, magnitude = 0f }
            };
            board.ApplyAttack(false, V(5, 5), new AttackOutcome(2, false, stun, V(5, 6), null, null));

            AssertTrue(!board.IsOccupied(V(5, 5)), "吹き飛ばしで元のマスが空く");
            AssertTrue(board.TryGetEnemyPiece(V(5, 6), out var moved), "吹き飛ばし先に敵がいる");
            AssertEqual(4, moved.currentHp, "ダメージが反映されたまま移動する");
            AssertEqual(1, moved.statusEffects.Count, "状態異常が付与される");
            AssertEqual(StatusEffectType.Stun, moved.statusEffects[0].type, "付与された状態異常の種類");
            AssertEqual(2, moved.statusEffects[0].remainingTurns, "付与された残りターン");
            AssertEqual(0, moved.statusEffects[0].appliedTurn, "付与ターン");

            // 同種の再付与：重複させず、残りターン・強さを更新し、付与ターンは据え置く
            var board2 = new BoardState(8, 8, V(4, 0));
            board2.PlacePiece(Enemy(EnemyArchetype.Scout, 5, 5, 20, 2));
            var slow1 = new List<OnHitStatusEffect>
            {
                new OnHitStatusEffect { type = StatusEffectType.Slow, duration = 3, magnitude = 1f }
            };
            board2.ApplyAttack(false, V(5, 5), new AttackOutcome(1, false, slow1, null, null, null));
            board2.AdvanceTurn();
            board2.TryGetEnemyPiece(V(5, 5), out var afterTick);
            AssertEqual(2, afterTick.statusEffects[0].remainingTurns, "1ターン経過で残りが減る");

            var slow2 = new List<OnHitStatusEffect>
            {
                new OnHitStatusEffect { type = StatusEffectType.Slow, duration = 5, magnitude = 2f }
            };
            board2.ApplyAttack(false, V(5, 5), new AttackOutcome(1, false, slow2, null, null, null));
            board2.TryGetEnemyPiece(V(5, 5), out var refreshed);
            AssertEqual(1, refreshed.statusEffects.Count, "同種の状態異常は重複しない");
            AssertEqual(5, refreshed.statusEffects[0].remainingTurns, "残りターンは大きい方に更新");
            AssertApprox(2f, refreshed.statusEffects[0].magnitude, "強さは大きい方に更新");
            AssertEqual(0, refreshed.statusEffects[0].appliedTurn, "付与ターンは据え置き");

            // 異なる種類は別枠で付与される
            board2.ApplyAttack(false, V(5, 5), new AttackOutcome(0, false, stun, null, null, null));
            board2.TryGetEnemyPiece(V(5, 5), out var both);
            AssertEqual(2, both.statusEffects.Count, "別の種類は別に付与される");
        }

        private static void A_AdvanceTurn()
        {
            var board = new BoardState(8, 8, V(4, 0));

            var knight = Player(PieceType.Knight, 2, 1, 10, 3, Mods(("BonusActions", 1f)));
            board.PlacePiece(knight);
            var rook = Player(PieceType.Rook, 3, 1, 10, 3);
            board.PlacePiece(rook);

            // 行動を使い切った状態にする
            knight.actionsLeft = 0;
            board.SetPlayerPiece(V(2, 1), knight);
            rook.actionsLeft = 0;
            board.SetPlayerPiece(V(3, 1), rook);

            var shortEffect = new[] { new ActiveStatusEffect { type = StatusEffectType.Stun, remainingTurns = 1 } };
            var longEffect = new[] { new ActiveStatusEffect { type = StatusEffectType.Slow, remainingTurns = 3, magnitude = 1f } };
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 6, 6, 5, 2, shortEffect));
            board.PlacePiece(Enemy(EnemyArchetype.Boss, 5, 6, 5, 2, longEffect));

            board.AdvanceTurn();

            AssertEqual(1, board.CurrentTurn, "ターン数が+1");
            board.TryGetPlayerPiece(V(2, 1), out var k);
            board.TryGetPlayerPiece(V(3, 1), out var r);
            AssertEqual(2, k.actionsLeft, "BonusActions+1の駒は行動回数2に戻る");
            AssertEqual(1, r.actionsLeft, "通常の駒は行動回数1に戻る");

            board.TryGetEnemyPiece(V(6, 6), out var expired);
            AssertEqual(0, expired.statusEffects.Count, "残り1ターンの状態異常は期限切れで除去される");
            board.TryGetEnemyPiece(V(5, 6), out var ticking);
            AssertEqual(1, ticking.statusEffects.Count, "残りがある状態異常は残る");
            AssertEqual(2, ticking.statusEffects[0].remainingTurns, "残りターンが1減る");
        }

        // ---------------------------------------------------------------
        // B. 各ルール
        // ---------------------------------------------------------------

        private static KnightMovementPatternSO NewKnightPattern(int maxL, float rate)
        {
            var p = NewSO<KnightMovementPatternSO>();
            p.maxLMultiplier = maxL;
            p.distanceBonusRate = rate;
            return p;
        }

        private static void B_KnightDashRange()
        {
            var rule = new KnightDashRule();
            var board = new BoardState(8, 8, V(4, 0));
            var knight = Player(PieceType.Knight, 3, 3, 10, 4);

            var pattern = NewKnightPattern(1, 0f);
            var tier1 = new List<Vector2Int>
            {
                V(4, 5), V(5, 4), V(5, 2), V(4, 1), V(2, 1), V(1, 2), V(1, 4), V(2, 5)
            };
            AssertSetEqual(tier1, rule.GetMovableTiles(knight, board, pattern), "最大段1：標準L字の8マスだけ");

            // 突進距離+1（statModifiers）で2倍L字が解放される。盤面内の2倍L字は4マス
            var boosted = Player(PieceType.Knight, 3, 3, 10, 4, Mods(("DashRangeBonus", 1f)));
            var tier12 = new List<Vector2Int>(tier1) { V(5, 7), V(7, 5), V(7, 1), V(1, 7) };
            AssertSetEqual(tier12, rule.GetMovableTiles(boosted, board, pattern), "DashRangeBonus=1：2倍L字も列挙される");

            // 0.9は整数に切り捨て（0）
            var fractional = Player(PieceType.Knight, 3, 3, 10, 4, Mods(("DashRangeBonus", 0.9f)));
            AssertSetEqual(tier1, rule.GetMovableTiles(fractional, board, pattern), "DashRangeBonus=0.9は切り捨てで2倍L字なし");

            // パターン側のmaxLMultiplier=2でも2倍L字が出る
            var pattern2 = NewKnightPattern(2, 0f);
            AssertSetEqual(tier12, rule.GetMovableTiles(knight, board, pattern2), "maxLMultiplier=2でも2倍L字が出る");

            // 移動先に駒がいれば除外、攻撃対象は敵のみ
            board.PlacePiece(Player(PieceType.Rook, 4, 5, 10, 3));      // 味方：移動も攻撃も不可
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 5, 4, 5, 2));  // 敵：移動不可・攻撃可（段1）
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 5, 7, 5, 2));  // 敵：段2
            var movable = rule.GetMovableTiles(boosted, board, pattern);
            AssertTrue(!movable.Contains(V(4, 5)), "味方のいるマスへは移動できない");
            AssertTrue(!movable.Contains(V(5, 4)), "敵のいるマスへは移動できない");
            AssertTrue(!movable.Contains(V(5, 7)), "敵のいる2段目のマスへは移動できない");

            var attackable1 = rule.GetAttackableTiles(knight, board, pattern);
            AssertSetEqual(new[] { V(5, 4) }, attackable1, "最大段1：攻撃できるのは1段目の敵だけ（味方・2段目は不可）");
            var attackable2 = rule.GetAttackableTiles(boosted, board, pattern);
            AssertSetEqual(new[] { V(5, 4), V(5, 7) }, attackable2, "DashRangeBonus=1：2段目の敵も攻撃できる");
        }

        private static void B_KnightTierDamage()
        {
            var rule = new KnightDashRule();
            var board = new BoardState(8, 8, V(4, 0));
            var pattern = NewKnightPattern(1, 0.5f);
            var attacker = Player(PieceType.Knight, 3, 3, 10, 4, Mods(("DashRangeBonus", 1f)));

            var near = Enemy(EnemyArchetype.Scout, 4, 5, 100, 2);   // 1段目 (+1,+2)
            var far = Enemy(EnemyArchetype.Scout, 5, 7, 100, 2);    // 2段目 (+2,+4)

            int damage1 = rule.ExecuteAttack(attacker, near, board, pattern).damage;
            int damage2 = rule.ExecuteAttack(attacker, far, board, pattern).damage;

            AssertEqual(6, damage1, "1段目のダメージ = round(4 × (1 + 0.5 × 1))");
            AssertEqual(8, damage2, "2段目のダメージ = round(4 × (1 + 0.5 × 2))");
            AssertTrue(damage2 > damage1, "2段目のダメージが1段目より大きい");

            // 距離ボーナスが0なら段数によらず同じ
            var flat = NewKnightPattern(1, 0f);
            AssertEqual(4, rule.ExecuteAttack(attacker, near, board, flat).damage, "距離ボーナス0：1段目は基礎攻撃力");
            AssertEqual(4, rule.ExecuteAttack(attacker, far, board, flat).damage, "距離ボーナス0：2段目も基礎攻撃力");
        }

        private static void B_KnightDistanceBonusAdditive()
        {
            var rule = new KnightDashRule();
            var board = new BoardState(8, 8, V(4, 0));
            var target = Enemy(EnemyArchetype.Scout, 4, 5, 100, 2); // 1段目

            var patternBase = NewKnightPattern(1, 0.25f);
            var patternZero = NewKnightPattern(1, 0f);

            var noMod = Player(PieceType.Knight, 3, 3, 10, 4);
            var withMod = Player(PieceType.Knight, 3, 3, 10, 4, Mods(("DistanceBonusRate", 0.25f)));
            var bigMod = Player(PieceType.Knight, 3, 3, 10, 4, Mods(("DistanceBonusRate", 0.5f)));

            AssertEqual(5, rule.ExecuteAttack(noMod, target, board, patternBase).damage, "基礎0.25のみ：round(4×1.25)=5");
            AssertEqual(6, rule.ExecuteAttack(withMod, target, board, patternBase).damage, "基礎0.25＋装備0.25＝0.5：round(4×1.5)=6（加算）");
            AssertEqual(6, rule.ExecuteAttack(bigMod, target, board, patternZero).damage, "基礎0＋装備0.5＝0.5：6");
            AssertEqual(4, rule.ExecuteAttack(noMod, target, board, patternZero).damage, "基礎0・装備なし：4");

            // 装備の補正は基礎値（SO）を書き換えない
            AssertApprox(0.25f, patternBase.distanceBonusRate, "SOの基礎値は変わらない");
        }

        private static void B_KnightPursuit()
        {
            var rule = new KnightDashRule();
            var board = new BoardState(8, 8, V(4, 0));
            var pattern = NewKnightPattern(1, 0f);
            var attacker = Player(PieceType.Knight, 3, 3, 10, 4);

            var survives = Enemy(EnemyArchetype.Scout, 4, 5, 5, 2);
            var outcomeLive = rule.ExecuteAttack(attacker, survives, board, pattern);
            AssertTrue(!outcomeLive.targetDefeated, "HP5に4ダメージでは倒れない");
            AssertTrue(!outcomeLive.PursuitMoveAvailable, "倒れなければ追撃移動はない");

            var dies = Enemy(EnemyArchetype.Scout, 4, 5, 4, 2);
            var outcomeKill = rule.ExecuteAttack(attacker, dies, board, pattern);
            AssertTrue(outcomeKill.targetDefeated, "HP4に4ダメージで倒れる");
            AssertTrue(outcomeKill.PursuitMoveAvailable, "撃破で追撃移動が可能");
            AssertEqual(1, outcomeKill.pursuitMoveCandidates.Count, "追撃先は1マス");
            AssertEqual(V(4, 5), outcomeKill.pursuitMoveCandidates[0], "追撃先は撃破したマス");

            // On Hit Effectsの定義が付与される
            pattern.onHitEffects.Add(new OnHitStatusEffect { type = StatusEffectType.Stun, duration = 1 });
            var withStun = rule.ExecuteAttack(attacker, survives, board, pattern);
            AssertEqual(1, withStun.appliedEffects.Count, "スタン付与が結果に含まれる");
            AssertEqual(StatusEffectType.Stun, withStun.appliedEffects[0].type, "付与される種類");
        }

        private static RookMovementPatternSO NewRookPattern(float chance, int distance = 1)
        {
            var p = NewSO<RookMovementPatternSO>();
            p.knockbackChance = chance;
            p.knockbackDistance = distance;
            p.moveRange = 0;
            return p;
        }

        private static void B_RookKnockbackZero()
        {
            var rule = new RookLineRule();
            var board = new BoardState(8, 8, V(4, 0));
            var rook = Player(PieceType.Rook, 3, 3, 10, 3);
            var target = Enemy(EnemyArchetype.Scout, 3, 5, 10, 2);
            board.PlacePiece(target);
            var pattern = NewRookPattern(0f);

            for (int i = 0; i < 200; i++)
            {
                var outcome = rule.ExecuteAttack(rook, target, board, pattern);
                AssertTrue(!outcome.KnockbackOccurred, "吹き飛ばし確率0では発生しない");
                AssertEqual(3, outcome.damage, "ダメージは与える");
                AssertTrue(!outcome.targetDefeated, "HP10に3ダメージでは倒れない");
            }
        }

        private static void B_RookKnockbackOne()
        {
            var rule = new RookLineRule();
            var board = new BoardState(8, 8, V(4, 0));
            var rook = Player(PieceType.Rook, 3, 3, 10, 3);
            var target = Enemy(EnemyArchetype.Scout, 3, 5, 10, 2);
            board.PlacePiece(target);
            var pattern = NewRookPattern(1f);

            // Random.valueは[0,1]なので、確率1でも理論上ごくまれに外れうる。回数を抑えて確認する
            for (int i = 0; i < 50; i++)
            {
                var outcome = rule.ExecuteAttack(rook, target, board, pattern);
                AssertTrue(outcome.KnockbackOccurred, "吹き飛ばし確率1：倒さなかった攻撃で発生する");
                AssertEqual(V(3, 6), outcome.knockbackDestination.Value, "攻撃方向に1マス");
            }

            // 吹き飛ばし先が塞がっていれば発生しない
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 3, 6, 10, 2));
            var blocked = rule.ExecuteAttack(rook, target, board, pattern);
            AssertTrue(!blocked.KnockbackOccurred, "吹き飛ばし先が塞がっていれば発生しない");

            // 盤面の端
            var edgeBoard = new BoardState(8, 8, V(4, 0));
            var edgeTarget = Enemy(EnemyArchetype.Scout, 3, 7, 10, 2);
            edgeBoard.PlacePiece(edgeTarget);
            var edge = rule.ExecuteAttack(Player(PieceType.Rook, 3, 3, 10, 3), edgeTarget, edgeBoard, pattern);
            AssertTrue(!edge.KnockbackOccurred, "盤面の端では吹き飛ばせない");

            // 敵を倒した攻撃では発生しない
            var weak = Enemy(EnemyArchetype.Scout, 3, 5, 3, 2);
            for (int i = 0; i < 50; i++)
            {
                var outcome = rule.ExecuteAttack(rook, weak, new BoardState(8, 8, V(4, 0)), pattern);
                AssertTrue(outcome.targetDefeated, "HP3に3ダメージで倒れる");
                AssertTrue(!outcome.KnockbackOccurred, "倒した攻撃では吹き飛ばしは発生しない");
            }

            // 横方向の吹き飛ばし
            var sideBoard = new BoardState(8, 8, V(4, 0));
            var sideTarget = Enemy(EnemyArchetype.Scout, 1, 3, 10, 2);
            sideBoard.PlacePiece(sideTarget);
            var side = rule.ExecuteAttack(rook, sideTarget, sideBoard, pattern);
            AssertEqual(V(0, 3), side.knockbackDestination.Value, "左方向の吹き飛ばし");
        }

        private static void B_RookLines()
        {
            var rule = new RookLineRule();
            var board = new BoardState(8, 8, V(4, 0));
            var rook = Player(PieceType.Rook, 3, 3, 10, 3);
            board.PlacePiece(Player(PieceType.Bishop, 3, 6, 10, 3));      // 上方向の自駒：(3,6)で遮る
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 6, 3, 5, 2));    // 右方向の敵
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 7, 3, 5, 2));    // 手前の敵に遮られる
            var pattern = NewRookPattern(0f);

            var movable = rule.GetMovableTiles(rook, board, pattern);
            var expected = new List<Vector2Int>
            {
                V(3, 4), V(3, 5),                      // 上（自駒で止まる）
                V(3, 2), V(3, 1), V(3, 0),             // 下
                V(2, 3), V(1, 3), V(0, 3),             // 左
                V(4, 3), V(5, 3)                       // 右（敵の手前まで）
            };
            AssertSetEqual(expected, movable, "ルークの移動可能マス");
            AssertSetEqual(new[] { V(6, 3) }, rule.GetAttackableTiles(rook, board, pattern), "攻撃対象は射線上で最初の敵だけ");

            // 射程制限
            pattern.moveRange = 2;
            var limited = rule.GetMovableTiles(rook, board, pattern);
            AssertTrue(!limited.Contains(V(3, 0)) && limited.Contains(V(3, 1)), "moveRange=2：2マスまで");
            AssertEqual(0, rule.GetAttackableTiles(rook, board, pattern).Count, "射程外の敵は攻撃できない");
        }

        private static PawnMovementPatternSO NewPawnPattern()
        {
            var p = NewSO<PawnMovementPatternSO>();
            p.star2MoveRange = 2;
            p.star2AttackRange = 2;
            return p;
        }

        private static void B_PawnRank1()
        {
            var rule = new PawnAdvanceRule();
            var pattern = NewPawnPattern();
            var pawn = Player(PieceType.Pawn, 3, 3, 10, 3);
            AssertEqual(1, pawn.rank, "初期のrankは1");

            var board = new BoardState(8, 8, V(4, 0));
            AssertSetEqual(new[] { V(3, 4) }, rule.GetMovableTiles(pawn, board, pattern), "前に1マス");
            AssertEqual(0, rule.GetAttackableTiles(pawn, board, pattern).Count, "敵がいなければ攻撃対象なし");

            board.PlacePiece(Enemy(EnemyArchetype.Scout, 3, 4, 5, 2));   // 真正面：移動を塞ぎ、攻撃対象にはならない
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 2, 4, 5, 2));   // 斜め前左
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 4, 4, 5, 2));   // 斜め前右
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 2, 5, 5, 2));   // 2マス先：対象外
            AssertEqual(0, rule.GetMovableTiles(pawn, board, pattern).Count, "前が塞がっていれば移動できない");
            AssertSetEqual(new[] { V(2, 4), V(4, 4) }, rule.GetAttackableTiles(pawn, board, pattern), "斜め前の2マス");

            // 盤面の端：前が盤面外
            var top = Player(PieceType.Pawn, 3, 7, 10, 3);
            AssertEqual(0, rule.GetMovableTiles(top, board, pattern).Count, "盤面の端では前に進めない");

            // 斜め前に味方がいても攻撃対象にならない
            var board2 = new BoardState(8, 8, V(4, 0));
            board2.PlacePiece(Player(PieceType.Rook, 2, 4, 10, 3));
            AssertEqual(0, rule.GetAttackableTiles(pawn, board2, pattern).Count, "味方は攻撃対象でない");

            // ダメージ
            var outcome = rule.ExecuteAttack(pawn, Enemy(EnemyArchetype.Scout, 2, 4, 3, 2), board, pattern);
            AssertEqual(3, outcome.damage, "ダメージ＝攻撃力");
            AssertTrue(outcome.targetDefeated, "HP3に3ダメージで倒れる");
        }

        private static void B_PawnRank2Move()
        {
            var rule = new PawnAdvanceRule();
            var pattern = NewPawnPattern();
            var pawn = Player(PieceType.Pawn, 3, 3, 10, 3);
            pawn.rank = 2;

            var board = new BoardState(8, 8, V(4, 0));
            var expected = new List<Vector2Int>
            {
                V(3, 4), V(3, 5),   // 前
                V(2, 3), V(1, 3),   // 左
                V(4, 3), V(5, 3)    // 右
            };
            var movable = rule.GetMovableTiles(pawn, board, pattern);
            AssertSetEqual(expected, movable, "★2：前・左・右に最大2マス");
            AssertTrue(!movable.Contains(V(3, 2)) && !movable.Contains(V(3, 1)), "後退はしない");

            // 距離はパターンの値に従う
            pattern.star2MoveRange = 1;
            AssertSetEqual(new[] { V(3, 4), V(2, 3), V(4, 3) }, rule.GetMovableTiles(pawn, board, pattern), "star2MoveRange=1なら1マス");
            pattern.star2MoveRange = 2;

            // 途中に駒があれば止まる
            board.PlacePiece(Player(PieceType.Rook, 3, 5, 10, 3));       // 前の2マス目に味方
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 1, 3, 5, 2));   // 左の2マス目に敵
            board.PlacePiece(Player(PieceType.Bishop, 4, 3, 10, 3));     // 右の1マス目に味方
            var blocked = rule.GetMovableTiles(pawn, board, pattern);
            AssertSetEqual(new[] { V(3, 4), V(2, 3) }, blocked, "前は味方の手前まで、左は敵の手前まで、右は塞がれて0");

            // 盤面の端
            var edge = Player(PieceType.Pawn, 0, 6, 10, 3);
            edge.rank = 2;
            var edgeMovable = rule.GetMovableTiles(edge, new BoardState(8, 8, V(4, 0)), pattern);
            AssertSetEqual(new[] { V(0, 7), V(1, 6), V(2, 6) }, edgeMovable, "盤面の端では範囲内だけ");
        }

        private static void B_PawnRank2Attack()
        {
            var rule = new PawnAdvanceRule();
            var pattern = NewPawnPattern();
            var pawn = Player(PieceType.Pawn, 3, 3, 10, 3);
            pawn.rank = 2;

            var board = new BoardState(8, 8, V(4, 0));
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 3, 5, 5, 2));   // 前2マス
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 2, 4, 5, 2));   // 斜め前左1マス
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 5, 5, 5, 2));   // 斜め前右2マス
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 3, 2, 5, 2));   // 後ろ：対象外
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 4, 3, 5, 2));   // 真横：対象外
            AssertSetEqual(new[] { V(3, 5), V(2, 4), V(5, 5) }, rule.GetAttackableTiles(pawn, board, pattern),
                "前・斜め前左・斜め前右の3方向に最大2マス先まで");

            // 3マス先は届かない
            var far = new BoardState(8, 8, V(4, 0));
            far.PlacePiece(Enemy(EnemyArchetype.Scout, 3, 6, 5, 2));
            far.PlacePiece(Enemy(EnemyArchetype.Scout, 6, 6, 5, 2));
            AssertEqual(0, rule.GetAttackableTiles(pawn, far, pattern).Count, "3マス先は届かない");

            // 射線上で最初に当たる敵だけ
            var first = new BoardState(8, 8, V(4, 0));
            first.PlacePiece(Enemy(EnemyArchetype.Scout, 3, 4, 5, 2));
            first.PlacePiece(Enemy(EnemyArchetype.Scout, 3, 5, 5, 2));
            AssertSetEqual(new[] { V(3, 4) }, rule.GetAttackableTiles(pawn, first, pattern), "手前の敵だけが対象");

            // 途中に別の駒（味方）があれば遮られる
            var blocked = new BoardState(8, 8, V(4, 0));
            blocked.PlacePiece(Player(PieceType.Rook, 4, 4, 10, 3));
            blocked.PlacePiece(Enemy(EnemyArchetype.Scout, 5, 5, 5, 2));
            blocked.PlacePiece(Player(PieceType.Bishop, 3, 4, 10, 3));
            blocked.PlacePiece(Enemy(EnemyArchetype.Scout, 3, 5, 5, 2));
            AssertEqual(0, rule.GetAttackableTiles(pawn, blocked, pattern).Count, "味方に遮られた射線の敵は攻撃できない");

            // 距離はパターンの値に従う
            pattern.star2AttackRange = 1;
            AssertSetEqual(new[] { V(2, 4) }, rule.GetAttackableTiles(pawn, board, pattern), "star2AttackRange=1なら1マス先だけ");
        }

        // ---------------------------------------------------------------
        // C. EnemyTurnProcessor
        // ---------------------------------------------------------------

        private static EnemyTurnProcessor NewProcessor(int kingDamage)
        {
            var pattern = NewSO<ChaseNearestMovementPatternSO>();
            pattern.kingDamage = kingDamage;
            var rules = new Dictionary<EnemyArchetype, IEnemyMoveRule>
            {
                { EnemyArchetype.Scout, new ChaseNearestMoveRule() }
            };
            var patterns = new Dictionary<EnemyArchetype, EnemyMovementPatternSO>
            {
                { EnemyArchetype.Scout, pattern }
            };
            return new EnemyTurnProcessor(rules, patterns);
        }

        private static BoardState NewBoardWithKing(int kingHp)
        {
            var board = new BoardState(8, 8, V(4, 0));
            board.PlacePiece(Player(PieceType.King, 4, 0, kingHp, 0));
            return board;
        }

        private static void C_AttackKing()
        {
            // 既に隣接している
            var board = NewBoardWithKing(20);
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 5, 1, 5, 2));
            var result = NewProcessor(3).Process(board);

            board.TryGetPlayerPiece(V(4, 0), out var king);
            AssertEqual(17, king.currentHp, "キングが kingDamage(3) を受ける");
            AssertEqual(1, result.actions.Count, "行動は1件");
            var action = result.actions[0];
            AssertTrue(action.attackedKing, "キングを攻撃した");
            AssertEqual(V(4, 0), action.targetPosition.Value, "対象はキングのマス");
            AssertEqual(3, action.damage, "ダメージはkingDamage");
            AssertTrue(!result.kingDefeated, "キングは倒れていない");
            AssertTrue(!result.allPiecesDefeated, "駒がいなかったのでallPiecesDefeatedはfalse");

            // 敵の攻撃力ではなくkingDamageが使われる
            AssertTrue(action.damage != 5, "敵の攻撃力(2)ではなくkingDamageが使われる");

            // 距離2から移動して隣接→攻撃
            var board2 = NewBoardWithKing(20);
            board2.PlacePiece(Enemy(EnemyArchetype.Scout, 4, 2, 5, 2));
            var result2 = NewProcessor(3).Process(board2);
            AssertTrue(result2.actions[0].moveDestination.HasValue, "移動する");
            AssertEqual(1, Cheb(result2.actions[0].moveDestination.Value, V(4, 0)), "移動後はキングに隣接");
            AssertTrue(result2.actions[0].attackedKing, "移動後にキングを攻撃する");
            board2.TryGetPlayerPiece(V(4, 0), out var king2);
            AssertEqual(17, king2.currentHp, "移動後の攻撃でHPが減る");

            // キングが倒れる
            var board3 = NewBoardWithKing(3);
            board3.PlacePiece(Enemy(EnemyArchetype.Scout, 5, 1, 5, 2));
            var result3 = NewProcessor(3).Process(board3);
            AssertTrue(result3.kingDefeated, "HP3のキングがkingDamage3で倒れる");
            AssertTrue(result3.actions[0].targetDefeated, "記録にも倒れたと残る");
        }

        private static void C_AttackWeakest()
        {
            // キングから遠い(4,4)。隣接するのは(3,4)のナイトと(5,4)のルーク
            var board = NewBoardWithKing(20);
            board.PlacePiece(Player(PieceType.Knight, 3, 4, 8, 3));
            board.PlacePiece(Player(PieceType.Rook, 5, 4, 5, 3));
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 4, 4, 5, 2));

            var result = NewProcessor(3).Process(board);
            var action = result.actions[0];
            AssertTrue(!action.attackedKing, "キングは攻撃しない");
            AssertEqual(V(5, 4), action.targetPosition.Value, "HPが最も低いルークを攻撃");
            AssertEqual(2, action.damage, "駒へのダメージは敵の攻撃力");
            board.TryGetPlayerPiece(V(5, 4), out var rook);
            board.TryGetPlayerPiece(V(3, 4), out var knight);
            AssertEqual(3, rook.currentHp, "ルークのHPが減る");
            AssertEqual(8, knight.currentHp, "ナイトのHPは変わらない");
            board.TryGetPlayerPiece(V(4, 0), out var king);
            AssertEqual(20, king.currentHp, "キングのHPは変わらない");

            // HPが同値なら座標順（x→y）で先の駒
            var tie = NewBoardWithKing(20);
            tie.PlacePiece(Player(PieceType.Knight, 3, 4, 5, 3));
            tie.PlacePiece(Player(PieceType.Rook, 5, 4, 5, 3));
            tie.PlacePiece(Enemy(EnemyArchetype.Scout, 4, 4, 5, 2));
            var tieResult = NewProcessor(3).Process(tie);
            AssertEqual(V(3, 4), tieResult.actions[0].targetPosition.Value, "同値は座標順で先の駒");

            // 隣接する駒が無ければ攻撃しない
            var none = NewBoardWithKing(20);
            none.PlacePiece(Player(PieceType.Knight, 1, 1, 8, 3));
            none.PlacePiece(Enemy(EnemyArchetype.Scout, 6, 6, 5, 2));
            var noneResult = NewProcessor(3).Process(none);
            AssertTrue(!noneResult.actions[0].targetPosition.HasValue, "隣接する駒がなければ攻撃しない");
            AssertTrue(noneResult.actions[0].moveDestination.HasValue, "最寄りの駒へ向かって移動する");
        }

        private static void C_KingPriority()
        {
            // (4,1)はキングに隣接し、(3,1)のナイトにも隣接する。キングが優先される
            var board = NewBoardWithKing(20);
            board.PlacePiece(Player(PieceType.Knight, 3, 1, 1, 3));
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 4, 1, 5, 2));
            var result = NewProcessor(3).Process(board);

            AssertTrue(result.actions[0].attackedKing, "キングに隣接していればキングを優先");
            board.TryGetPlayerPiece(V(3, 1), out var knight);
            AssertEqual(1, knight.currentHp, "ナイトは攻撃されない");
        }

        private static void C_Stunned()
        {
            var stun = new[] { new ActiveStatusEffect { type = StatusEffectType.Stun, remainingTurns = 1 } };
            var board = NewBoardWithKing(20);
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 5, 1, 5, 2, stun));   // キングに隣接
            var result = NewProcessor(3).Process(board);

            AssertEqual(1, result.actions.Count, "行動記録は1件");
            AssertTrue(result.actions[0].wasStunned, "スタンされている");
            AssertTrue(!result.actions[0].attackedKing, "キングを攻撃しない");
            AssertTrue(!result.actions[0].moveDestination.HasValue, "移動しない");
            board.TryGetPlayerPiece(V(4, 0), out var king);
            AssertEqual(20, king.currentHp, "キングのHPは変わらない");

            // 遠くのスタン敵は動かない
            var board2 = NewBoardWithKing(20);
            board2.PlacePiece(Enemy(EnemyArchetype.Scout, 6, 6, 5, 2, stun));
            NewProcessor(3).Process(board2);
            AssertTrue(board2.TryGetEnemyPiece(V(6, 6), out _), "スタン中の敵は位置を変えない");

            // 残りターン0のスタンは効かない
            var expired = new[] { new ActiveStatusEffect { type = StatusEffectType.Stun, remainingTurns = 0 } };
            var board3 = NewBoardWithKing(20);
            board3.PlacePiece(Enemy(EnemyArchetype.Scout, 5, 1, 5, 2, expired));
            var result3 = NewProcessor(3).Process(board3);
            AssertTrue(!result3.actions[0].wasStunned, "残り0のスタンは無効");
        }

        private static void C_Slow()
        {
            var slow = new[] { new ActiveStatusEffect { type = StatusEffectType.Slow, remainingTurns = 5, magnitude = 1f, appliedTurn = 0 } };
            var board = NewBoardWithKing(20);
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 4, 4, 5, 2, slow));
            var processor = NewProcessor(3);

            // ターン0：付与ターンから数えて(1+1)ターンに1回しか移動できない。0%2==0なので移動を飛ばす
            var turn0 = processor.Process(board);
            AssertTrue(!turn0.actions[0].moveDestination.HasValue, "スロウ：移動を飛ばすターンは動かない");
            AssertTrue(board.TryGetEnemyPiece(V(4, 4), out _), "位置は変わらない");

            // ターン1：1%2!=0なので移動する
            board.AdvanceTurn();
            var turn1 = processor.Process(board);
            AssertTrue(turn1.actions[0].moveDestination.HasValue, "スロウ：次のターンは移動する");

            // キングに隣接していれば、スロウでも攻撃は行う
            var near = NewBoardWithKing(20);
            near.PlacePiece(Enemy(EnemyArchetype.Scout, 5, 1, 5, 2, slow));
            var nearResult = processor.Process(near);
            AssertTrue(nearResult.actions[0].attackedKing, "スロウでもキングへの攻撃は行う");
        }

        private static void C_AllPiecesDefeated()
        {
            var processor = NewProcessor(3);

            // 唯一の味方駒が倒れる
            var board = NewBoardWithKing(20);
            board.PlacePiece(Player(PieceType.Rook, 5, 4, 2, 3));
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 4, 4, 5, 5));
            var result = processor.Process(board);
            AssertTrue(result.allPiecesDefeated, "味方駒が全滅したらtrue");
            AssertTrue(!result.kingDefeated, "キングは無事");
            AssertTrue(result.actions[0].targetDefeated, "記録でも倒したことになっている");
            AssertTrue(!board.TryGetPlayerPiece(V(5, 4), out _), "倒された駒は盤面から消える");
            AssertEqual(1, board.Graveyard.Count, "墓地に入る");

            // 2体のうち1体だけが倒れる
            var board2 = NewBoardWithKing(20);
            board2.PlacePiece(Player(PieceType.Rook, 5, 4, 2, 3));
            board2.PlacePiece(Player(PieceType.Knight, 1, 1, 10, 3));
            board2.PlacePiece(Enemy(EnemyArchetype.Scout, 4, 4, 5, 5));
            AssertTrue(!processor.Process(board2).allPiecesDefeated, "1体残っていればfalse");

            // 開始時に駒がいない
            var board3 = NewBoardWithKing(20);
            board3.PlacePiece(Enemy(EnemyArchetype.Scout, 4, 6, 5, 5));
            var result3 = processor.Process(board3);
            AssertTrue(!result3.allPiecesDefeated, "開始時に駒がいなければfalse");
            AssertTrue(!result3.kingDefeated, "キングは倒れていない");
        }

        private static void C_Preview()
        {
            var board = NewBoardWithKing(20);
            board.PlacePiece(Enemy(EnemyArchetype.Scout, 5, 1, 5, 2));
            var preview = NewProcessor(3).PreviewNextTurn(board);

            AssertTrue(preview.actions[0].attackedKing, "予告ではキングを攻撃する");
            board.TryGetPlayerPiece(V(4, 0), out var king);
            AssertEqual(20, king.currentHp, "本物のキングのHPは変わらない");

            var board2 = NewBoardWithKing(20);
            board2.PlacePiece(Enemy(EnemyArchetype.Scout, 4, 4, 5, 2));
            NewProcessor(3).PreviewNextTurn(board2);
            AssertTrue(board2.TryGetEnemyPiece(V(4, 4), out _), "予告では本物の敵は動かない");
        }

        // ---------------------------------------------------------------
        // D. EncounterGenerator
        // ---------------------------------------------------------------

        private static EncounterPlan Gen(int floor, EncounterGeneratorSettings settings, bool retreater, int seed,
            int width = 8, int height = 8)
        {
            return EncounterGenerator.Generate(floor, width, height, settings, 10, 4, 6, 3, retreater, seed);
        }

        private static int CountOf(EncounterPlan plan, EnemyArchetype archetype)
        {
            int n = 0;
            foreach (var e in plan.enemies) if (e.archetype == archetype) n++;
            return n;
        }

        private static EnemySpawn FirstOf(EncounterPlan plan, EnemyArchetype archetype)
        {
            foreach (var e in plan.enemies) if (e.archetype == archetype) return e;
            Fail($"{archetype} の敵が計画に含まれていない");
            return default;
        }

        private static void D_Deterministic()
        {
            var settings = new EncounterGeneratorSettings();
            foreach (int floor in new[] { 1, 2, 5, 7, 10, 15 })
            {
                var a = Gen(floor, settings, true, 12345);
                var b = Gen(floor, settings, true, 12345);
                AssertEqual(a.enemies.Count, b.enemies.Count, $"{floor}F：敵の数");
                for (int i = 0; i < a.enemies.Count; i++)
                {
                    AssertEqual(a.enemies[i].archetype, b.enemies[i].archetype, $"{floor}F[{i}]：種類");
                    AssertEqual(a.enemies[i].position, b.enemies[i].position, $"{floor}F[{i}]：位置");
                    AssertEqual(a.enemies[i].hp, b.enemies[i].hp, $"{floor}F[{i}]：HP");
                    AssertEqual(a.enemies[i].attack, b.enemies[i].attack, $"{floor}F[{i}]：攻撃力");
                }
            }

            // 階が0以下なら1階として扱う
            AssertEqual(1, Gen(0, settings, false, 1).floorNumber, "階0は1階に丸められる");
            AssertEqual(7, Gen(7, settings, false, 1).floorNumber, "floorNumberが保持される");
        }

        private static void D_BossFloors()
        {
            var settings = new EncounterGeneratorSettings(); // 間隔5・最終階15

            for (int floor = 1; floor <= 20; floor++)
            {
                bool expected = floor % 5 == 0 || floor == 15;
                var plan = Gen(floor, settings, false, 777 + floor);
                AssertEqual(expected, CountOf(plan, EnemyArchetype.Boss) == 1, $"{floor}F：ボス階の判定");
                AssertTrue(CountOf(plan, EnemyArchetype.Boss) <= 1, $"{floor}F：ボスは最大1体");
                AssertEqual(expected, EnemyNames.IsBossFloor(floor, 5, 15), $"{floor}F：EnemyNames.IsBossFloor");
            }

            // 間隔が無効でも最終階はボス階
            var noInterval = new EncounterGeneratorSettings { bossFloorInterval = 0 };
            AssertEqual(0, CountOf(Gen(5, noInterval, false, 1), EnemyArchetype.Boss), "間隔0：5階はボス階でない");
            AssertEqual(1, CountOf(Gen(15, noInterval, false, 1), EnemyArchetype.Boss), "間隔0でも最終階はボス階");

            // ボスは最上段中央
            var boss = FirstOf(Gen(5, settings, false, 1), EnemyArchetype.Boss);
            AssertEqual(V(4, 7), boss.position, "ボスは最上段中央");

            // 呼び名
            AssertEqual("", EnemyNames.GetBossRankName(3, 5, 15), "通常階は空");
            AssertEqual("小ボス", EnemyNames.GetBossRankName(5, 5, 15), "5階は小ボス");
            AssertEqual("中ボス", EnemyNames.GetBossRankName(10, 5, 15), "10階は中ボス");
            AssertEqual("ラスボス", EnemyNames.GetBossRankName(15, 5, 15), "最終階はラスボス");
            AssertEqual("ボス", EnemyNames.GetBossRankName(20, 5, 15), "それ以降はボス");
            AssertEqual("ボス", EnemyNames.GetDisplayName(EnemyArchetype.Boss, 3, 5, 15), "通常階のボスの表示名");
            AssertEqual("スカウト", EnemyNames.GetDisplayName(EnemyArchetype.Scout, 3, 5, 15), "スカウトの表示名");
        }

        private static void D_FinalBossScaling()
        {
            var settings = new EncounterGeneratorSettings();

            // 15階：HP=10+14×2=38→×1.5=57。攻撃力=4+(14/3)×1=8→+2=10
            var final = FirstOf(Gen(15, settings, false, 1), EnemyArchetype.Boss);
            AssertEqual(57, final.hp, "ラスボスのHP（スケーリング後に×1.5）");
            AssertEqual(10, final.attack, "ラスボスの攻撃力（スケーリング後に+2）");

            // 10階（最終階でないボス）：HP=10+9×2=28、攻撃力=4+3=7。倍率・加算なし
            var mid = FirstOf(Gen(10, settings, false, 1), EnemyArchetype.Boss);
            AssertEqual(28, mid.hp, "中ボスのHPは倍率なし");
            AssertEqual(7, mid.attack, "中ボスの攻撃力は加算なし");

            // 5階：HP=18、攻撃力=4+1=5
            var small = FirstOf(Gen(5, settings, false, 1), EnemyArchetype.Boss);
            AssertEqual(18, small.hp, "小ボスのHP");
            AssertEqual(5, small.attack, "小ボスの攻撃力");

            // 設定を変えると反映される
            var custom = new EncounterGeneratorSettings { finalBossHpMultiplier = 2f, finalBossAttackBonus = 5 };
            var customFinal = FirstOf(Gen(15, custom, false, 1), EnemyArchetype.Boss);
            AssertEqual(76, customFinal.hp, "倍率2.0：38×2=76");
            AssertEqual(13, customFinal.attack, "加算+5：8+5=13");

            // 最終階を12階にすると、12階でスケーリングされ、15階（間隔の倍数）は通常のボス
            var moved = new EncounterGeneratorSettings { finalFloor = 12 };
            var boss12 = FirstOf(Gen(12, moved, false, 1), EnemyArchetype.Boss);
            AssertEqual(Mathf.RoundToInt((10 + 11 * 2) * 1.5f), boss12.hp, "最終階=12：12階でHP倍率が掛かる");
            var boss15 = FirstOf(Gen(15, moved, false, 1), EnemyArchetype.Boss);
            AssertEqual(38, boss15.hp, "最終階でない15階のボスは倍率なし");

            // スカウトの数：ボス階は通常より1体少ない（15階は通常5→4）
            AssertEqual(4, CountOf(Gen(15, settings, false, 1), EnemyArchetype.Scout), "ボス階のスカウトは1体少ない");
        }

        private static void D_Monotonic()
        {
            var settings = new EncounterGeneratorSettings();

            int prevScoutHp = int.MinValue;
            int prevNormalScouts = int.MinValue;
            int prevBossHp = int.MinValue;
            int prevScoutAttack = int.MinValue;

            // ゲームで実際に遊ぶ階（最終階まで）だけを検証する。最終階を変えても追従する
            for (int floor = 1; floor <= settings.finalFloor; floor++)
            {
                var plan = Gen(floor, settings, false, 4242);
                bool isBoss = CountOf(plan, EnemyArchetype.Boss) == 1;
                var scout = FirstOf(plan, EnemyArchetype.Scout);

                AssertTrue(scout.hp >= prevScoutHp, $"{floor}F：スカウトのHPが減らない");
                prevScoutHp = scout.hp;
                AssertTrue(scout.attack >= prevScoutAttack, $"{floor}F：スカウトの攻撃力が減らない");
                prevScoutAttack = scout.attack;

                if (isBoss)
                {
                    var boss = FirstOf(plan, EnemyArchetype.Boss);
                    AssertTrue(boss.hp >= prevBossHp, $"{floor}F：ボスのHPが減らない");
                    prevBossHp = boss.hp;
                }
                else
                {
                    int scouts = CountOf(plan, EnemyArchetype.Scout);
                    AssertTrue(scouts >= prevNormalScouts, $"{floor}F：通常階のスカウトの数が減らない");
                    AssertTrue(scouts <= settings.maxScoutCount, $"{floor}F：スカウトの数は上限以内");
                    prevNormalScouts = scouts;
                }
            }

            AssertEqual(2, CountOf(Gen(1, settings, false, 1), EnemyArchetype.Scout), "1階のスカウトは2体");
            AssertEqual(3, CountOf(Gen(3, settings, false, 1), EnemyArchetype.Scout), "3階のスカウトは3体");
            AssertEqual(5, CountOf(Gen(9, settings, false, 1), EnemyArchetype.Scout), "9階のスカウトは上限の5体");
            AssertEqual(6, FirstOf(Gen(1, settings, false, 1), EnemyArchetype.Scout).hp, "1階のスカウトHP");
            AssertEqual(12, FirstOf(Gen(7, settings, false, 1), EnemyArchetype.Scout).hp, "7階のスカウトHP=6+6");
            AssertEqual(3, FirstOf(Gen(1, settings, false, 1), EnemyArchetype.Scout).attack, "1階のスカウト攻撃力");
            AssertEqual(5, FirstOf(Gen(7, settings, false, 1), EnemyArchetype.Scout).attack, "7階のスカウト攻撃力=3+2");
        }

        private static void D_Positions()
        {
            var settings = new EncounterGeneratorSettings();

            for (int floor = 1; floor <= 30; floor++)
            {
                for (int seed = 1; seed <= 5; seed++)
                {
                    var plan = Gen(floor, settings, true, seed * 1000 + floor);
                    var seen = new HashSet<Vector2Int>();
                    foreach (var enemy in plan.enemies)
                    {
                        AssertTrue(enemy.position.x >= 0 && enemy.position.x < 8 &&
                                   enemy.position.y >= 0 && enemy.position.y < 8,
                                   $"{floor}F(seed{seed})：位置{enemy.position}が盤面内");
                        AssertTrue(seen.Add(enemy.position), $"{floor}F(seed{seed})：位置{enemy.position}が重複");
                        AssertTrue(enemy.hp > 0, $"{floor}F：HPは正");
                    }
                }
            }

            // 狭い盤面でも盤面内・重複なし（埋まったら打ち切る）
            var small = Gen(9, settings, true, 5, 3, 3);
            var seenSmall = new HashSet<Vector2Int>();
            foreach (var enemy in small.enemies)
            {
                AssertTrue(enemy.position.x >= 0 && enemy.position.x < 3 && enemy.position.y >= 0 && enemy.position.y < 3, "3×3盤面内");
                AssertTrue(seenSmall.Add(enemy.position), "3×3で重複なし");
            }
            AssertTrue(small.enemies.Count <= 9, "敵の数は盤面のマス数以内");
        }

        private static void D_Retreater()
        {
            var settings = new EncounterGeneratorSettings();

            // 許可しなければ出ない
            for (int seed = 0; seed < 100; seed++)
                AssertEqual(0, CountOf(Gen(3, settings, false, seed), EnemyArchetype.Retreater), "allowRetreater=falseでは出ない");

            // ボス階では出ない
            for (int seed = 0; seed < 100; seed++)
            {
                foreach (int floor in new[] { 5, 10, 15 })
                    AssertEqual(0, CountOf(Gen(floor, settings, true, seed), EnemyArchetype.Retreater), $"{floor}F（ボス階）では出ない");
            }

            // 通常階では、一定確率で1体だけ出る
            int appeared = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var plan = Gen(3, settings, true, seed);
                int n = CountOf(plan, EnemyArchetype.Retreater);
                AssertTrue(n <= 1, "逃げる敵は最大1体");
                if (n == 1)
                {
                    appeared++;
                    var r = FirstOf(plan, EnemyArchetype.Retreater);
                    AssertEqual(4, r.hp, "逃げる敵のHP");
                    AssertEqual(1, r.attack, "逃げる敵の攻撃力は階によらない");
                }
            }
            AssertTrue(appeared > 0 && appeared < 200, $"逃げる敵が一部のシードだけで出る（出現 {appeared}/200）");

            // 確率1なら通常階で必ず1体、確率0なら出ない
            var always = new EncounterGeneratorSettings { retreaterChance = 1f };
            var never = new EncounterGeneratorSettings { retreaterChance = 0f };
            for (int seed = 0; seed < 30; seed++)
            {
                AssertEqual(1, CountOf(Gen(4, always, true, seed), EnemyArchetype.Retreater), "確率1：必ず1体");
                AssertEqual(0, CountOf(Gen(4, never, true, seed), EnemyArchetype.Retreater), "確率0：出ない");
            }
        }

        // ---------------------------------------------------------------
        // E. BattleEquipmentGenerator
        // ---------------------------------------------------------------

        private static readonly string[] CommonNames = { "かけらの指輪", "古びた剣", "粗末な盾", "布のマント" };
        private static readonly string[] RareNames = { "鋼の指輪", "魔法の剣", "鉄壁の盾", "知恵のマント" };
        private static readonly string[] EpicNames = { "竜鱗の指輪", "業火の剣", "不落の盾", "賢者のマント" };
        private static readonly string[] LegendaryNames = { "星辰の指輪", "破滅の剣", "絶対の盾", "神話のマント" };

        private static string[] NamesFor(EquipmentRarity rarity)
        {
            switch (rarity)
            {
                case EquipmentRarity.Common: return CommonNames;
                case EquipmentRarity.Rare:   return RareNames;
                case EquipmentRarity.Epic:   return EpicNames;
                default:                     return LegendaryNames;
            }
        }

        private static int ExpectedBonusCount(EquipmentRarity rarity)
        {
            switch (rarity)
            {
                case EquipmentRarity.Common: return 1;
                case EquipmentRarity.Rare:   return 1;
                case EquipmentRarity.Epic:   return 2;
                default:                     return 3;
            }
        }

        private static HashSet<EquipmentStatType> AllowedTypes(EquipmentRarity rarity)
        {
            var set = new HashSet<EquipmentStatType> { EquipmentStatType.AttackFlat, EquipmentStatType.HpFlat };
            if (rarity >= EquipmentRarity.Rare)
            {
                set.Add(EquipmentStatType.DistanceBonusRate);
                set.Add(EquipmentStatType.DashRangeBonus);
            }
            if (rarity >= EquipmentRarity.Epic) set.Add(EquipmentStatType.BonusActions);
            return set;
        }

        private static void CheckBonusValue(EquipmentStatBonus bonus, EquipmentRarity rarity)
        {
            float v = bonus.value;
            string label = $"[{rarity}] {bonus.statType}={v}";

            switch (bonus.statType)
            {
                case EquipmentStatType.AttackFlat:
                    {
                        int lo = rarity == EquipmentRarity.Common ? 1 : rarity == EquipmentRarity.Rare ? 1 : rarity == EquipmentRarity.Epic ? 2 : 3;
                        int hi = rarity == EquipmentRarity.Common ? 1 : rarity == EquipmentRarity.Rare ? 2 : rarity == EquipmentRarity.Epic ? 3 : 3;
                        AssertTrue(v >= lo && v <= hi && Mathf.Approximately(v, Mathf.Round(v)), $"攻撃力+の範囲外 {label}");
                        break;
                    }
                case EquipmentStatType.HpFlat:
                    {
                        int lo = rarity == EquipmentRarity.Common ? 2 : rarity == EquipmentRarity.Rare ? 3 : rarity == EquipmentRarity.Epic ? 5 : 7;
                        int hi = rarity == EquipmentRarity.Common ? 3 : rarity == EquipmentRarity.Rare ? 5 : rarity == EquipmentRarity.Epic ? 7 : 9;
                        AssertTrue(v >= lo && v <= hi && Mathf.Approximately(v, Mathf.Round(v)), $"最大HP+の範囲外 {label}");
                        break;
                    }
                case EquipmentStatType.DistanceBonusRate:
                    {
                        float lo = rarity == EquipmentRarity.Rare ? 0.25f : rarity == EquipmentRarity.Epic ? 0.35f : 0.50f;
                        float hi = rarity == EquipmentRarity.Rare ? 0.35f : rarity == EquipmentRarity.Epic ? 0.50f : 0.75f;
                        AssertTrue(v >= lo - 0.0051f && v <= hi + 0.0051f, $"距離ボーナス+の範囲外 {label}");
                        break;
                    }
                case EquipmentStatType.BonusActions:
                    AssertApprox(1f, v, $"行動回数+は1固定 {label}");
                    break;
                case EquipmentStatType.DashRangeBonus:
                    AssertApprox(1f, v, $"突進距離+は1固定 {label}");
                    break;
                default:
                    Fail($"生成されないはずの種類 {label}");
                    break;
            }
        }

        private static void E_GenerateRules()
        {
            var ids = new HashSet<string>();
            var seen = new Dictionary<EquipmentRarity, HashSet<EquipmentStatType>>();
            var rarityCounts = new Dictionary<EquipmentRarity, int>();
            foreach (EquipmentRarity r in Enum.GetValues(typeof(EquipmentRarity)))
            {
                seen[r] = new HashSet<EquipmentStatType>();
                rarityCounts[r] = 0;
            }

            for (int tier = 1; tier <= 3; tier++)
            {
                for (int i = 0; i < 1500; i++)
                {
                    var item = BattleEquipmentGenerator.GenerateRandomEquipment(tier);
                    AssertTrue(item != null, "装備が生成される");
                    AssertTrue(!string.IsNullOrEmpty(item.itemId) && ids.Add(item.itemId), "itemIdが空でなく重複しない");
                    AssertTrue(Array.IndexOf(NamesFor(item.rarity), item.itemName) >= 0,
                        $"名前が[{item.rarity}]の名前プールにある（{item.itemName}）");

                    rarityCounts[item.rarity]++;

                    AssertEqual(ExpectedBonusCount(item.rarity), item.bonuses.Count, $"[{item.rarity}]のボーナスの数");

                    var types = new HashSet<EquipmentStatType>();
                    var allowed = AllowedTypes(item.rarity);
                    foreach (var bonus in item.bonuses)
                    {
                        AssertTrue(types.Add(bonus.statType), $"[{item.rarity}]同じ種類の重複: {bonus.statType}");
                        AssertTrue(bonus.statType != EquipmentStatType.SenseLevel, "SenseLevelは生成されない");
                        AssertTrue(allowed.Contains(bonus.statType), $"[{item.rarity}]に出ない種類: {bonus.statType}");
                        CheckBonusValue(bonus, item.rarity);
                        seen[item.rarity].Add(bonus.statType);
                    }
                }
            }

            // 4段階のレアリティが全て生成される
            foreach (var kv in rarityCounts)
                AssertTrue(kv.Value > 0, $"[{kv.Key}]が1回も生成されなかった");

            // 出うる種類が実際に出る
            AssertTrue(seen[EquipmentRarity.Common].Contains(EquipmentStatType.AttackFlat), "Common：攻撃力+が出る");
            AssertTrue(seen[EquipmentRarity.Common].Contains(EquipmentStatType.HpFlat), "Common：最大HP+が出うる");
            AssertTrue(seen[EquipmentRarity.Rare].Contains(EquipmentStatType.HpFlat), "Rare：最大HP+が出うる");
            AssertTrue(seen[EquipmentRarity.Rare].Contains(EquipmentStatType.DistanceBonusRate), "Rare：距離ボーナス+が出る");
            AssertTrue(seen[EquipmentRarity.Rare].Contains(EquipmentStatType.DashRangeBonus), "Rare：突進距離+が出る");
            AssertTrue(seen[EquipmentRarity.Epic].Contains(EquipmentStatType.BonusActions), "Epic：行動回数+が出る");
            AssertTrue(seen[EquipmentRarity.Legendary].Contains(EquipmentStatType.BonusActions), "Legendary：行動回数+が出る");
            AssertTrue(seen[EquipmentRarity.Epic].Contains(EquipmentStatType.HpFlat), "Epic：最大HP+が出うる");
            AssertTrue(seen[EquipmentRarity.Legendary].Contains(EquipmentStatType.HpFlat), "Legendary：最大HP+が出うる");
        }

        private static void E_UpgradedHandmade()
        {
            // Common → Rare
            var common = Item("古びた剣", EquipmentRarity.Common, B(EquipmentStatType.AttackFlat, 1));
            for (int i = 0; i < 100; i++)
            {
                var up = BattleEquipmentGenerator.CreateUpgraded(common);
                AssertTrue(up != null, "Commonは上位版を作れる");
                AssertEqual(EquipmentRarity.Rare, up.rarity, "レアリティ+1");
                AssertEqual("魔法の剣", up.itemName, "同じ種類（剣）の上位の名前");
                AssertTrue(up.itemId != common.itemId, "新しいitemId");
                AssertEqual(1, up.bonuses.Count, "Rareのボーナスは1つ（増えない）");
                AssertEqual(EquipmentStatType.AttackFlat, up.bonuses[0].statType, "効果の種類は維持");
                AssertTrue(up.bonuses[0].value >= 1f && up.bonuses[0].value <= 2f, "Rareの範囲で引き直す");
            }
            AssertEqual(EquipmentRarity.Common, common.rarity, "元の装備は書き換えない（レアリティ）");
            AssertEqual(1, common.bonuses.Count, "元の装備は書き換えない（ボーナス数）");

            // Rare → Epic（効果の数が1→2：不足分が1つ追加される）。値は元より下がらない
            var rare = Item("鋼の指輪", EquipmentRarity.Rare, B(EquipmentStatType.AttackFlat, 2));
            var allowedAdded = new HashSet<EquipmentStatType>
            {
                EquipmentStatType.DistanceBonusRate, EquipmentStatType.BonusActions,
                EquipmentStatType.DashRangeBonus, EquipmentStatType.HpFlat
            };
            for (int i = 0; i < 100; i++)
            {
                var up = BattleEquipmentGenerator.CreateUpgraded(rare);
                AssertEqual(EquipmentRarity.Epic, up.rarity, "Rare→Epic");
                AssertEqual("竜鱗の指輪", up.itemName, "指輪の上位の名前");
                AssertEqual(2, up.bonuses.Count, "Epicのボーナスは2つ（1つ追加）");
                AssertEqual(EquipmentStatType.AttackFlat, up.bonuses[0].statType, "元の効果が先頭に維持される");
                AssertTrue(up.bonuses[0].value >= 2f, "値は元より下がらない");
                AssertTrue(allowedAdded.Contains(up.bonuses[1].statType), $"追加された効果は未所持の候補から（{up.bonuses[1].statType}）");
                CheckBonusValue(up.bonuses[1], EquipmentRarity.Epic);
            }

            // Epic → Legendary（2→3）
            var epic = Item("業火の剣", EquipmentRarity.Epic,
                B(EquipmentStatType.AttackFlat, 3), B(EquipmentStatType.DistanceBonusRate, 0.4f));
            for (int i = 0; i < 100; i++)
            {
                var up = BattleEquipmentGenerator.CreateUpgraded(epic);
                AssertEqual(EquipmentRarity.Legendary, up.rarity, "Epic→Legendary");
                AssertEqual("破滅の剣", up.itemName, "剣の最上位の名前");
                AssertEqual(3, up.bonuses.Count, "Legendaryのボーナスは3つ");
                var types = new HashSet<EquipmentStatType>();
                foreach (var b in up.bonuses) AssertTrue(types.Add(b.statType), "種類の重複なし");
                AssertTrue(types.Contains(EquipmentStatType.AttackFlat) && types.Contains(EquipmentStatType.DistanceBonusRate), "元の種類は維持");
                foreach (var b in up.bonuses)
                {
                    if (b.statType == EquipmentStatType.AttackFlat) AssertTrue(b.value >= 3f, "攻撃力+は下がらない");
                    if (b.statType == EquipmentStatType.DistanceBonusRate) AssertTrue(b.value >= 0.4f, "距離ボーナスは下がらない");
                }
            }

            // Legendaryはnull
            var legendary = Item("破滅の剣", EquipmentRarity.Legendary, B(EquipmentStatType.AttackFlat, 3));
            AssertTrue(BattleEquipmentGenerator.CreateUpgraded(legendary) == null, "Legendaryは上位がなくnull");
            AssertTrue(BattleEquipmentGenerator.CreateUpgraded(null) == null, "nullならnull");

            // 名前プールに無い名前なら、上位の名前プールから選ぶ
            var oddName = Item("謎の品", EquipmentRarity.Common, B(EquipmentStatType.AttackFlat, 1));
            for (int i = 0; i < 50; i++)
            {
                var up = BattleEquipmentGenerator.CreateUpgraded(oddName);
                AssertTrue(Array.IndexOf(RareNames, up.itemName) >= 0, "不明な名前は上位の名前プールからのフォールバック");
            }

            // 最大HP+の値も下がらない
            var hpItem = Item("粗末な盾", EquipmentRarity.Common, B(EquipmentStatType.HpFlat, 3));
            for (int i = 0; i < 100; i++)
            {
                var up = BattleEquipmentGenerator.CreateUpgraded(hpItem);
                AssertEqual("鉄壁の盾", up.itemName, "盾の上位の名前");
                AssertTrue(up.bonuses[0].statType == EquipmentStatType.HpFlat && up.bonuses[0].value >= 3f, "最大HP+は下がらない");
            }
        }

        private static void E_UpgradedSweep()
        {
            int checkedCount = 0;
            for (int i = 0; i < 1500; i++)
            {
                var item = BattleEquipmentGenerator.GenerateRandomEquipment(2);
                var up = BattleEquipmentGenerator.CreateUpgraded(item);

                if (item.rarity == EquipmentRarity.Legendary)
                {
                    AssertTrue(up == null, "Legendaryはnull");
                    continue;
                }

                AssertTrue(up != null, $"[{item.rarity}]は上位版を作れる");
                AssertEqual(item.rarity + 1, up.rarity, "レアリティ+1");

                int index = Array.IndexOf(NamesFor(item.rarity), item.itemName);
                AssertTrue(index >= 0, "元の名前が名前プールにある");
                AssertEqual(NamesFor(up.rarity)[index], up.itemName, "名前が同じ種類の上位の名前");

                AssertEqual(ExpectedBonusCount(up.rarity), up.bonuses.Count, $"[{up.rarity}]のボーナスの数");

                var upTypes = new HashSet<EquipmentStatType>();
                foreach (var b in up.bonuses)
                {
                    AssertTrue(upTypes.Add(b.statType), "種類の重複なし");
                    AssertTrue(AllowedTypes(up.rarity).Contains(b.statType), $"[{up.rarity}]に出ない種類: {b.statType}");
                    CheckBonusValue(b, up.rarity);
                }

                foreach (var original in item.bonuses)
                {
                    EquipmentStatBonus match = null;
                    foreach (var b in up.bonuses) if (b.statType == original.statType) match = b;
                    AssertTrue(match != null, $"元の効果 {original.statType} が維持される");
                    AssertTrue(match.value >= original.value - 0.0001f, $"{original.statType} の値が元より下がらない（{original.value}→{match.value}）");
                }

                checkedCount++;
            }
            AssertTrue(checkedCount > 0, "検証した装備が1件以上ある");
        }

        private static void E_FormatBonus()
        {
            foreach (EquipmentStatType type in Enum.GetValues(typeof(EquipmentStatType)))
            {
                string text = BattleEquipmentGenerator.FormatBonus(B(type, 1f));
                AssertTrue(!string.IsNullOrEmpty(text), $"{type} のFormatBonusが空");
            }

            AssertEqual("ATK+2", BattleEquipmentGenerator.FormatBonus(B(EquipmentStatType.AttackFlat, 2f)), "攻撃力+の表記");
            AssertEqual("HP+5", BattleEquipmentGenerator.FormatBonus(B(EquipmentStatType.HpFlat, 5f)), "最大HP+の表記");
            AssertEqual("行動回数+1", BattleEquipmentGenerator.FormatBonus(B(EquipmentStatType.BonusActions, 1f)), "行動回数+の表記");
            AssertEqual("感知+1", BattleEquipmentGenerator.FormatBonus(B(EquipmentStatType.SenseLevel, 1f)), "感知+の表記");
            AssertTrue(BattleEquipmentGenerator.FormatBonus(B(EquipmentStatType.DashRangeBonus, 1f)).Contains("突進距離+1"), "突進距離+の表記");
            AssertTrue(BattleEquipmentGenerator.FormatBonus(B(EquipmentStatType.DistanceBonusRate, 0.5f)).Contains("距離ボーナス+50%"), "距離ボーナス+の表記");
        }

        // ---------------------------------------------------------------
        // F. RunState
        // ---------------------------------------------------------------

        private static EquipmentInstance SimpleItem(string name = "古びた剣")
        {
            return Item(name, EquipmentRarity.Common, B(EquipmentStatType.AttackFlat, 1));
        }

        private static void F_EquipUnequip()
        {
            var a = SimpleItem("A");
            var b = SimpleItem("B");
            RunState.Inventory.Add(a);
            RunState.Inventory.Add(b);

            AssertTrue(RunState.TryEquip(PieceType.Knight, 0, a), "所持品を装備できる");
            AssertEqual(1, RunState.Inventory.Items.Count, "装備すると所持品が減る");
            AssertTrue(ReferenceEquals(RunState.GetEquipped(PieceType.Knight)[0], a), "スロット0に装備される");
            AssertTrue(!RunState.Inventory.Contains(a), "装備した物は所持品にない");

            // 入れ替え：元の装備は所持品に戻る
            AssertTrue(RunState.TryEquip(PieceType.Knight, 0, b), "埋まっているスロットへ入れ替えで装備できる");
            AssertEqual(1, RunState.Inventory.Items.Count, "入れ替えでも所持品の数は変わらない");
            AssertTrue(RunState.Inventory.Contains(a), "外れた装備は所持品に戻る");
            AssertTrue(ReferenceEquals(RunState.GetEquipped(PieceType.Knight)[0], b), "新しい装備に置き換わる");

            // 別のスロット
            AssertTrue(RunState.TryEquip(PieceType.Knight, 2, a), "別のスロットにも装備できる");
            AssertEqual(0, RunState.Inventory.Items.Count, "所持品が空になる");
            AssertEqual(RunState.SlotsPerPiece, RunState.GetEquipped(PieceType.Knight).Count, "スロット数は3");
            AssertTrue(RunState.GetEquipped(PieceType.Knight)[1] == null, "未使用のスロットはnull");

            // 失敗するケース
            var c = SimpleItem("C");
            RunState.Inventory.Add(c);
            AssertTrue(!RunState.TryEquip(PieceType.Knight, 3, c), "範囲外のスロットは失敗（上限）");
            AssertTrue(!RunState.TryEquip(PieceType.Knight, -1, c), "範囲外のスロットは失敗（負）");
            AssertTrue(!RunState.TryEquip(PieceType.King, 0, c), "キングには装備できない");
            AssertTrue(!RunState.TryEquip(PieceType.Queen, 0, c), "対象外の駒種には装備できない");
            AssertTrue(!RunState.TryEquip(PieceType.Knight, 1, null), "nullは装備できない");
            AssertTrue(!RunState.TryEquip(PieceType.Knight, 1, SimpleItem("所持していない")), "所持品にない装備は失敗");
            AssertEqual(1, RunState.Inventory.Items.Count, "失敗しても所持品は変わらない");
            AssertTrue(!RunState.TryEquip(PieceType.Rook, 0, b), "既に装備中の物は所持品にないので他の駒へ装備できない");

            // 外す
            AssertTrue(RunState.Unequip(PieceType.Knight, 2), "外せる");
            AssertEqual(2, RunState.Inventory.Items.Count, "外した装備は所持品に戻る");
            AssertTrue(RunState.GetEquipped(PieceType.Knight)[2] == null, "スロットが空になる");
            AssertTrue(!RunState.Unequip(PieceType.Knight, 2), "空きスロットはfalse");
            AssertTrue(!RunState.Unequip(PieceType.Knight, 1), "未使用のスロットはfalse");
            AssertTrue(!RunState.Unequip(PieceType.Knight, 5), "範囲外はfalse");
            AssertTrue(!RunState.Unequip(PieceType.Knight, -1), "負の範囲外はfalse");
            AssertTrue(!RunState.Unequip(PieceType.King, 0), "キングはfalse");

            AssertEqual(0, RunState.GetEquipped(PieceType.King).Count, "対象外の駒種の装備一覧は空");

            // 4種の駒が独立している
            var d = SimpleItem("D");
            RunState.Inventory.Add(d);
            AssertTrue(RunState.TryEquip(PieceType.Pawn, 1, d), "ポーンにも装備できる");
            AssertTrue(RunState.GetEquipped(PieceType.Rook)[1] == null, "他の駒のスロットは変わらない");
        }

        private static void F_MarkDead()
        {
            var a = SimpleItem("A");
            var b = SimpleItem("B");
            var c = SimpleItem("C");
            RunState.Inventory.Add(a);
            RunState.Inventory.Add(b);
            RunState.Inventory.Add(c);
            RunState.TryEquip(PieceType.Knight, 0, a);
            RunState.TryEquip(PieceType.Knight, 2, b);
            RunState.TryEquip(PieceType.Rook, 0, c);
            AssertEqual(0, RunState.Inventory.Items.Count, "前提：全て装備済み");

            RunState.MarkPieceDead(PieceType.Knight);
            AssertTrue(RunState.IsPieceDead(PieceType.Knight), "墓地に入る");
            AssertEqual(2, RunState.Inventory.Items.Count, "装備が所持品に戻る");
            AssertTrue(RunState.Inventory.Contains(a) && RunState.Inventory.Contains(b), "戻ったのは倒れた駒の装備");
            AssertTrue(RunState.GetEquipped(PieceType.Knight)[0] == null && RunState.GetEquipped(PieceType.Knight)[2] == null, "スロットは空になる");
            AssertTrue(ReferenceEquals(RunState.GetEquipped(PieceType.Rook)[0], c), "他の駒の装備は変わらない");
            AssertTrue(!RunState.IsPieceDead(PieceType.Rook), "他の駒は墓地に入らない");

            // 2回目は何も起きない
            RunState.MarkPieceDead(PieceType.Knight);
            AssertEqual(2, RunState.Inventory.Items.Count, "2回目で所持品が増えない");
            AssertEqual(1, RunState.DeadPieces.Count, "DeadPiecesは1件");
            AssertTrue(new List<PieceType>(RunState.DeadPieces).Contains(PieceType.Knight), "DeadPiecesにナイト");

            // 対象外の駒種
            RunState.MarkPieceDead(PieceType.King);
            AssertTrue(!RunState.IsPieceDead(PieceType.King), "キングは墓地に入れられない");
            AssertEqual(1, RunState.DeadPieces.Count, "DeadPiecesは変わらない");
        }

        private static void F_Revive()
        {
            var a = SimpleItem("A");
            var b = SimpleItem("B");
            RunState.Inventory.Add(a);
            RunState.Inventory.Add(b);
            RunState.TryEquip(PieceType.Bishop, 0, a);
            RunState.MarkPieceDead(PieceType.Bishop);   // aは所持品に戻る
            AssertEqual(2, RunState.Inventory.Items.Count, "前提：所持品2つ");

            AssertTrue(!RunState.TryRevive(PieceType.Bishop, null), "nullでは蘇生できない");
            AssertTrue(!RunState.TryRevive(PieceType.Bishop, SimpleItem("所持していない")), "所持品にない装備では蘇生できない");
            AssertTrue(!RunState.TryRevive(PieceType.Rook, a), "墓地にいない駒は蘇生できない");
            AssertTrue(RunState.IsPieceDead(PieceType.Bishop), "失敗しても墓地のまま");
            AssertEqual(2, RunState.Inventory.Items.Count, "失敗しても装備は消費されない");

            AssertTrue(RunState.TryRevive(PieceType.Bishop, a), "装備を消費して蘇生できる");
            AssertTrue(!RunState.IsPieceDead(PieceType.Bishop), "蘇生で墓地から出る");
            AssertEqual(1, RunState.Inventory.Items.Count, "装備が1つだけ消費される");
            AssertTrue(!RunState.Inventory.Contains(a) && RunState.Inventory.Contains(b), "消費されたのは選んだ装備");
            AssertTrue(RunState.GetEquipped(PieceType.Bishop)[0] == null, "蘇生した駒の装備スロットは空");

            // 蘇生後は装備できる
            AssertTrue(RunState.TryEquip(PieceType.Bishop, 0, b), "蘇生した駒には装備できる");
        }

        private static void F_DeadCannotEquip()
        {
            RunState.MarkPieceDead(PieceType.Rook);
            var item = SimpleItem();
            RunState.Inventory.Add(item);
            AssertTrue(!RunState.TryEquip(PieceType.Rook, 0, item), "墓地の駒には装備できない");
            AssertTrue(!RunState.Unequip(PieceType.Rook, 0), "墓地の駒の装備を外す操作は失敗");
            AssertEqual(1, RunState.Inventory.Items.Count, "所持品は変わらない");
        }

        private static void F_SkillAcquire()
        {
            AssertEqual(0, RunState.SkillPoints, "初期のスキルポイントは0");
            AssertTrue(!RunState.CanAcquireSkill("sense_1"), "ポイント不足では取得できない");
            AssertTrue(!RunState.TryAcquireSkill("sense_1"), "TryAcquireも失敗");
            AssertEqual(0, RunState.SkillPoints, "失敗してもポイントは変わらない");

            RunState.AddSkillPoints(1);
            AssertTrue(RunState.CanAcquireSkill("sense_1"), "コスト1・前提なしで取得できる");
            AssertTrue(RunState.TryAcquireSkill("sense_1"), "取得できる");
            AssertEqual(0, RunState.SkillPoints, "コストが引かれる");
            AssertTrue(RunState.HasSkill("sense_1"), "取得済みになる");
            AssertTrue(!RunState.CanAcquireSkill("sense_1"), "取得済みは再取得できない");
            AssertTrue(!RunState.TryAcquireSkill("sense_1"), "再取得は失敗");

            // 前提・コスト
            RunState.AddSkillPoints(1);
            AssertTrue(!RunState.CanAcquireSkill("sense_2"), "コスト2に対してポイント1では不足");
            RunState.AddSkillPoints(1);
            AssertTrue(RunState.CanAcquireSkill("sense_2"), "前提を取得済みでポイントも足りれば取得できる");

            // 前提が無いと、ポイントがあっても取得できない
            RunState.AddSkillPoints(100);
            AssertTrue(!RunState.CanAcquireSkill("sense_3"), "前提(sense_2)未取得では取得できない");
            AssertTrue(!RunState.CanAcquireSkill("atk_2"), "前提(atk_1)未取得では取得できない");
            AssertTrue(!RunState.CanAcquireSkill("appraise_2"), "前提(appraise_1)未取得では取得できない");
            int before = RunState.SkillPoints;
            AssertTrue(!RunState.TryAcquireSkill("sense_3"), "TryAcquireも失敗");
            AssertEqual(before, RunState.SkillPoints, "失敗でポイントは変わらない");

            AssertTrue(RunState.TryAcquireSkill("sense_2"), "sense_2を取得");
            AssertTrue(RunState.TryAcquireSkill("sense_3"), "前提を取得したらsense_3を取得できる");
            AssertEqual(before - 2 - 3, RunState.SkillPoints, "コスト2+3が引かれる");

            // 存在しないid・null
            AssertTrue(!RunState.CanAcquireSkill("存在しないスキル"), "存在しないidは取得できない");
            AssertTrue(!RunState.CanAcquireSkill(null), "nullは取得できない");
            AssertTrue(!RunState.HasSkill(null), "HasSkill(null)はfalse");
            AssertTrue(!RunState.HasSkill("存在しないスキル"), "未取得のidはfalse");

            // ポイントは0未満にならない
            RunState.AddSkillPoints(-100000);
            AssertEqual(0, RunState.SkillPoints, "ポイントは0未満にならない");

            // コストはカタログの値と一致する
            RunState.Reset();
            KingSkillCatalog.TryGet("quality_1", out var quality);
            RunState.AddSkillPoints(quality.cost - 1);
            AssertTrue(!RunState.CanAcquireSkill("quality_1"), "コストより1少なければ取得できない");
            RunState.AddSkillPoints(1);
            AssertTrue(RunState.CanAcquireSkill("quality_1"), "コストちょうどで取得できる");
            AssertTrue(RunState.TryAcquireSkill("quality_1"), "取得できる");
            AssertEqual(0, RunState.SkillPoints, "ちょうど使い切る");
            AssertEqual(1, RunState.OwnedSkills.Count, "OwnedSkillsに追加される");
        }

        private static void F_SkillDerived()
        {
            // 初期値
            AssertEqual(0, RunState.GetSenseSkillLevel(), "感知の初期値");
            AssertEqual(0, RunState.GetAppraisalSkillLevel(), "鑑定の初期値");
            AssertEqual(0, RunState.GetSkillAttackBonus(), "攻撃力の初期値");
            AssertEqual(0, RunState.GetSkillPieceHpBonus(), "駒HPの初期値");
            AssertEqual(0, RunState.GetSkillKingHpBonus(), "キングHPの初期値");
            AssertTrue(!RunState.HasPickupDoubleBoostSkill(), "拾得ボーナス強化の初期値");
            AssertEqual(0, RunState.GetSkillDropQualityBonus(), "ドロップ品質の初期値");

            RunState.AddSkillPoints(1000);

            Acquire("sense_1"); AssertEqual(1, RunState.GetSenseSkillLevel(), "感知Lv1");
            Acquire("sense_2"); AssertEqual(2, RunState.GetSenseSkillLevel(), "感知Lv2");
            Acquire("sense_3"); AssertEqual(3, RunState.GetSenseSkillLevel(), "感知Lv3");

            Acquire("appraise_1"); AssertEqual(1, RunState.GetAppraisalSkillLevel(), "鑑定Lv1");
            Acquire("appraise_2"); AssertEqual(2, RunState.GetAppraisalSkillLevel(), "鑑定Lv2");

            Acquire("atk_1"); AssertEqual(1, RunState.GetSkillAttackBonus(), "攻撃強化I：+1");
            Acquire("atk_2"); AssertEqual(2, RunState.GetSkillAttackBonus(), "攻撃強化II：+2");

            Acquire("hp_1"); AssertEqual(3, RunState.GetSkillPieceHpBonus(), "体力強化I：+3");
            Acquire("hp_2"); AssertEqual(6, RunState.GetSkillPieceHpBonus(), "体力強化II：+6");

            Acquire("kinghp_1"); AssertEqual(5, RunState.GetSkillKingHpBonus(), "王の体力I：+5");
            Acquire("kinghp_2"); AssertEqual(10, RunState.GetSkillKingHpBonus(), "王の体力II：+10");

            Acquire("bonus_1"); AssertTrue(RunState.HasPickupDoubleBoostSkill(), "拾得ボーナス強化");
            Acquire("quality_1"); AssertEqual(1, RunState.GetSkillDropQualityBonus(), "良品の予感：+1");

            // Resetで全て戻る
            RunState.Reset();
            AssertEqual(0, RunState.GetSenseSkillLevel(), "Reset後の感知");
            AssertEqual(0, RunState.GetSkillAttackBonus(), "Reset後の攻撃力");
            AssertTrue(!RunState.HasPickupDoubleBoostSkill(), "Reset後の拾得ボーナス強化");
        }

        private static void Acquire(string id)
        {
            AssertTrue(RunState.TryAcquireSkill(id), $"スキル {id} を取得できる");
        }

        private static void F_PawnGrowthAndStats()
        {
            RunState.SetPawnGrowth(-5, 0);
            AssertEqual(0, RunState.PawnXp, "経験値は0未満にならない");
            AssertEqual(1, RunState.PawnRank, "rankは1以上");

            RunState.SetPawnGrowth(10, 5);
            AssertEqual(10, RunState.PawnXp, "経験値はそのまま");
            AssertEqual(2, RunState.PawnRank, "rankは2以下");

            RunState.SetPawnGrowth(7, 1);
            AssertEqual(7, RunState.PawnXp, "経験値7");
            AssertEqual(1, RunState.PawnRank, "rank1");

            RunState.SetPawnGrowth(3, -3);
            AssertEqual(1, RunState.PawnRank, "負のrankは1に丸める");

            RunState.RecordVictory(3, 4, 1);
            RunState.RecordVictory(2, 1, 0);
            AssertEqual(2, RunState.FloorsCleared, "クリアした階");
            AssertEqual(5, RunState.EnemiesDefeated, "倒した敵");
            AssertEqual(5, RunState.ItemsCollected, "獲得した装備");
            AssertEqual(1, RunState.PiecesLost, "失った駒");
        }

        private static void F_Placement()
        {
            AssertTrue(!RunState.TryGetLastPlacement(PieceType.Knight, out _), "初期は記憶なし");

            RunState.SavePlacement(new List<KeyValuePair<PieceType, Vector2Int>>
            {
                new KeyValuePair<PieceType, Vector2Int>(PieceType.Knight, V(2, 1)),
                new KeyValuePair<PieceType, Vector2Int>(PieceType.Rook, V(5, 1))
            });
            AssertTrue(RunState.TryGetLastPlacement(PieceType.Knight, out var knight), "ナイトの配置を記憶している");
            AssertEqual(V(2, 1), knight, "ナイトの配置");
            AssertTrue(RunState.TryGetLastPlacement(PieceType.Rook, out var rook), "ルークの配置を記憶している");
            AssertEqual(V(5, 1), rook, "ルークの配置");
            AssertTrue(!RunState.TryGetLastPlacement(PieceType.Bishop, out _), "保存していない駒は記憶なし");

            // 置き換え：以前の記憶は消える
            RunState.SavePlacement(new List<KeyValuePair<PieceType, Vector2Int>>
            {
                new KeyValuePair<PieceType, Vector2Int>(PieceType.Bishop, V(3, 2))
            });
            AssertTrue(!RunState.TryGetLastPlacement(PieceType.Knight, out _), "置き換えで以前の記憶は消える");
            AssertTrue(RunState.TryGetLastPlacement(PieceType.Bishop, out var bishop), "新しい記憶");
            AssertEqual(V(3, 2), bishop, "ビショップの配置");

            // nullで全消去
            RunState.SavePlacement(null);
            AssertTrue(!RunState.TryGetLastPlacement(PieceType.Bishop, out _), "nullで全て消える");

            // Resetで消える
            RunState.SavePlacement(new List<KeyValuePair<PieceType, Vector2Int>>
            {
                new KeyValuePair<PieceType, Vector2Int>(PieceType.Pawn, V(4, 2))
            });
            RunState.Reset();
            AssertTrue(!RunState.TryGetLastPlacement(PieceType.Pawn, out _), "Resetで配置の記憶が消える");
        }

        private static void F_Reset()
        {
            // 状態を変える
            var item = SimpleItem();
            RunState.Inventory.Add(item);
            var oldInventory = RunState.Inventory;
            RunState.TryEquip(PieceType.Knight, 0, item);
            RunState.MarkPieceDead(PieceType.Rook);
            RunState.CurrentFloor = 7;
            RunState.NextEncounter = new EncounterPlan(7, null);
            RunState.RecordVictory(3, 3, 1);
            RunState.SetPawnGrowth(9, 2);
            RunState.AddSkillPoints(5);
            RunState.TryAcquireSkill("sense_1");
            int oldSeed = RunState.RunSeed;

            RunState.Reset();

            AssertTrue(!ReferenceEquals(oldInventory, RunState.Inventory), "所持品は新しいインスタンス");
            AssertEqual(0, RunState.Inventory.Items.Count, "所持品が空");
            AssertTrue(RunState.GetEquipped(PieceType.Knight)[0] == null, "装備が空");
            AssertEqual(RunState.SlotsPerPiece, RunState.GetEquipped(PieceType.Knight).Count, "スロット数は3のまま");
            AssertEqual(0, RunState.DeadPieces.Count, "墓地が空");
            AssertTrue(!RunState.IsPieceDead(PieceType.Rook), "墓地の駒がいない");
            AssertEqual(1, RunState.CurrentFloor, "階数は1");
            AssertTrue(RunState.NextEncounter == null, "次の階の計画はnull");
            AssertEqual(0, RunState.FloorsCleared, "クリアした階");
            AssertEqual(0, RunState.EnemiesDefeated, "倒した敵");
            AssertEqual(0, RunState.ItemsCollected, "獲得した装備");
            AssertEqual(0, RunState.PiecesLost, "失った駒");
            AssertEqual(0, RunState.PawnXp, "ポーンの経験値");
            AssertEqual(1, RunState.PawnRank, "ポーンのrank");
            AssertEqual(0, RunState.SkillPoints, "スキルポイント");
            AssertEqual(0, RunState.OwnedSkills.Count, "取得済みスキル");

            // 乱数シードが新しく決まる（極めて低い確率で一致しうるため、3回試す）
            bool changed = false;
            for (int i = 0; i < 3 && !changed; i++)
            {
                RunState.Reset();
                changed = RunState.RunSeed != oldSeed;
            }
            AssertTrue(changed, "Resetでランの乱数シードが新しく決まる");
        }

        // ---------------------------------------------------------------
        // G. KingSkillCatalog
        // ---------------------------------------------------------------

        private static void G_Catalog()
        {
            var ids = new HashSet<string>();
            foreach (var skill in KingSkillCatalog.All)
            {
                AssertTrue(!string.IsNullOrEmpty(skill.id), "idが空でない");
                AssertTrue(ids.Add(skill.id), $"idが重複: {skill.id}");
                AssertTrue(skill.cost > 0, $"{skill.id}：コストが0より大きい");
                AssertTrue(!string.IsNullOrEmpty(skill.displayName), $"{skill.id}：表示名が空");
                AssertTrue(!string.IsNullOrEmpty(skill.description), $"{skill.id}：説明が空");
                AssertTrue(skill.tier >= 1 && skill.tier <= 3, $"{skill.id}：tierは1〜3（UIが3段まで）");
                AssertTrue(skill.prerequisites != null, $"{skill.id}：前提の配列がnullでない");
                AssertTrue(KingSkillCatalog.TryGet(skill.id, out var found) && ReferenceEquals(found, skill), $"{skill.id}：TryGetで引ける");
            }
            AssertTrue(!KingSkillCatalog.TryGet(null, out _), "TryGet(null)はfalse");
            AssertTrue(!KingSkillCatalog.TryGet("存在しない", out _), "存在しないidはfalse");

            // 前提のidが存在する・自分自身を前提にしない
            foreach (var skill in KingSkillCatalog.All)
            {
                foreach (var prerequisite in skill.prerequisites)
                {
                    AssertTrue(ids.Contains(prerequisite), $"{skill.id}：前提 {prerequisite} が存在しない");
                    AssertTrue(prerequisite != skill.id, $"{skill.id}：自分自身を前提にしている");
                }
            }

            // 循環がない（深さ優先探索）
            var state = new Dictionary<string, int>(); // 0=未訪問 1=探索中 2=完了
            foreach (var skill in KingSkillCatalog.All) VisitSkill(skill.id, state);

            // 全てのスキルが、十分なポイントがあれば取得できる（前提の連鎖が詰まらない）
            RunState.Reset();
            RunState.AddSkillPoints(10000);
            int guard = 0;
            bool progress = true;
            while (progress && guard++ < 100)
            {
                progress = false;
                foreach (var skill in KingSkillCatalog.All)
                {
                    if (RunState.CanAcquireSkill(skill.id) && RunState.TryAcquireSkill(skill.id)) progress = true;
                }
            }
            foreach (var skill in KingSkillCatalog.All)
                AssertTrue(RunState.HasSkill(skill.id), $"{skill.id}：十分なポイントがあっても取得できない");
            AssertEqual(KingSkillCatalog.All.Count, RunState.OwnedSkills.Count, "全てのスキルを取得できる");
        }

        private static void VisitSkill(string id, Dictionary<string, int> state)
        {
            state.TryGetValue(id, out int current);
            if (current == 2) return;
            if (current == 1) Fail($"スキルの前提に循環がある（{id}）");

            state[id] = 1;
            KingSkillCatalog.TryGet(id, out var skill);
            foreach (var prerequisite in skill.prerequisites) VisitSkill(prerequisite, state);
            state[id] = 2;
        }

        // ---------------------------------------------------------------
        // H. KeyBindings
        // ---------------------------------------------------------------

        private static void ResetKeyBindingsStatics()
        {
            var method = typeof(KeyBindings).GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static);
            if (method == null) Fail("KeyBindings.ResetStatics が見つからない（リフレクション）。KeyBindingsの実装が変わった可能性");
            method.Invoke(null, null);
        }

        /// <summary>
        /// KeyBindingsのテストを、保存データ（PlayerPrefs）を壊さずに実行する。
        /// 前：該当キーを退避して削除し、静的状態をリセット。後：静的状態をリセットして、退避した値を復元する。
        /// </summary>
        private static void WithCleanKeyBindings(Action body)
        {
            bool had = PlayerPrefs.HasKey(KeyBindingsPrefsKey);
            string saved = PlayerPrefs.GetString(KeyBindingsPrefsKey, "");
            try
            {
                ResetKeyBindingsStatics();
                PlayerPrefs.DeleteKey(KeyBindingsPrefsKey);
                body();
            }
            finally
            {
                ResetKeyBindingsStatics();
                if (had) PlayerPrefs.SetString(KeyBindingsPrefsKey, saved);
                else PlayerPrefs.DeleteKey(KeyBindingsPrefsKey);
                PlayerPrefs.Save();
            }
        }

        private static void AssertEntry(KeyBindingEntry entry, Key key, bool shift, string message)
        {
            AssertEqual(key, entry.key, message + "（キー）");
            AssertEqual(shift, entry.shift, message + "（Shift）");
        }

        private static void H_Defaults()
        {
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveUp, 0), Key.UpArrow, false, "上：スロット0");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveUp, 1), Key.W, false, "上：スロット1");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveDown, 0), Key.DownArrow, false, "下：スロット0");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveDown, 1), Key.S, false, "下：スロット1");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveLeft, 0), Key.LeftArrow, false, "左：スロット0");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveLeft, 1), Key.A, false, "左：スロット1");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveRight, 0), Key.RightArrow, false, "右：スロット0");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveRight, 1), Key.D, false, "右：スロット1");
            AssertEntry(KeyBindings.GetBinding(GameAction.Confirm, 0), Key.Enter, false, "決定：スロット0");
            AssertEntry(KeyBindings.GetBinding(GameAction.Confirm, 1), Key.NumpadEnter, false, "決定：スロット1");
            AssertEntry(KeyBindings.GetBinding(GameAction.Cancel, 0), Key.Escape, false, "キャンセル：スロット0");
            AssertEntry(KeyBindings.GetBinding(GameAction.Cancel, 1), Key.Backspace, false, "キャンセル：スロット1");
            AssertEntry(KeyBindings.GetBinding(GameAction.NextPiece, 0), Key.Tab, false, "次の駒：スロット0");
            AssertEntry(KeyBindings.GetBinding(GameAction.NextPiece, 1), Key.None, false, "次の駒：スロット1は未設定");
            AssertEntry(KeyBindings.GetBinding(GameAction.PrevPiece, 0), Key.Tab, true, "前の駒：Shift+Tab");
            AssertEntry(KeyBindings.GetBinding(GameAction.PrevPiece, 1), Key.None, false, "前の駒：スロット1は未設定");
            AssertEntry(KeyBindings.GetBinding(GameAction.EndTurn, 0), Key.Space, false, "ターン終了：スロット0");
            AssertEntry(KeyBindings.GetBinding(GameAction.EndTurn, 1), Key.None, false, "ターン終了：スロット1は未設定");
            AssertEntry(KeyBindings.GetBinding(GameAction.OpenSettings, 0), Key.F1, false, "設定を開く：スロット0");
            AssertEntry(KeyBindings.GetBinding(GameAction.OpenSettings, 1), Key.None, false, "設定を開く：スロット1は未設定");

            // 範囲外のスロットは未設定として返る
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveUp, 2), Key.None, false, "範囲外のスロット");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveUp, -1), Key.None, false, "負のスロット");

            // 全ての操作が定義されている
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                AssertTrue(KeyBindings.GetLabel(action) != "未設定", $"{action}：初期設定で1つ以上割り当てがある");
                AssertTrue(!string.IsNullOrEmpty(GameActionInfo.GetDisplayName(action)), $"{action}：表示名がある");
            }
            AssertEqual(2, KeyBindings.SlotCount, "1つの操作につき2スロット");
        }

        private static void H_SetConflict()
        {
            // 既に他の操作が使っているキー（S：下の移動のスロット1）を、別の操作に割り当てる
            var cleared = KeyBindings.Set(GameAction.MoveUp, 1, new KeyBindingEntry(Key.S));
            AssertTrue(cleared.HasValue && cleared.Value == GameAction.MoveDown, "同じ内容を使っていた「下」が解除される");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveUp, 1), Key.S, false, "上のスロット1にSが入る");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveDown, 1), Key.None, false, "下のスロット1は未設定になる");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveDown, 0), Key.DownArrow, false, "下のスロット0は変わらない");

            // Shiftの有無が違えば、別の内容として扱う（Tabは次の駒、Shift+Tabは前の駒）
            var noConflict = KeyBindings.Set(GameAction.EndTurn, 1, new KeyBindingEntry(Key.Tab, false));
            AssertTrue(noConflict.HasValue && noConflict.Value == GameAction.NextPiece, "Tab（Shiftなし）は「次の駒」と衝突して解除する");
            AssertEntry(KeyBindings.GetBinding(GameAction.PrevPiece, 0), Key.Tab, true, "Shift+Tabの「前の駒」は影響を受けない");
            AssertEntry(KeyBindings.GetBinding(GameAction.NextPiece, 0), Key.None, false, "「次の駒」のスロット0は未設定");

            // 同じ操作・同じスロットへの同じ割り当ては衝突しない
            var self = KeyBindings.Set(GameAction.MoveLeft, 0, new KeyBindingEntry(Key.LeftArrow));
            AssertTrue(!self.HasValue, "自分自身は解除されない");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveLeft, 0), Key.LeftArrow, false, "割り当ては維持される");

            // 同じ操作の別スロットが同じ内容なら、そちらが解除される
            var sameAction = KeyBindings.Set(GameAction.MoveLeft, 1, new KeyBindingEntry(Key.LeftArrow));
            AssertTrue(sameAction.HasValue && sameAction.Value == GameAction.MoveLeft, "同じ操作の別スロットも解除対象");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveLeft, 0), Key.None, false, "スロット0が解除される");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveLeft, 1), Key.LeftArrow, false, "スロット1に入る");

            // 無効なキー（Key.None）はそのスロットを未設定にする
            var none = KeyBindings.Set(GameAction.MoveRight, 0, new KeyBindingEntry(Key.None));
            AssertTrue(!none.HasValue, "Key.Noneは何も解除しない");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveRight, 0), Key.None, false, "スロットが未設定になる");

            // 範囲外のスロットは何も起きない
            var outOfRange = KeyBindings.Set(GameAction.Confirm, 5, new KeyBindingEntry(Key.Q));
            AssertTrue(!outOfRange.HasValue, "範囲外のスロットはnull");
            AssertEntry(KeyBindings.GetBinding(GameAction.Confirm, 0), Key.Enter, false, "他の割り当ては変わらない");

            // Shift付きの割り当て
            KeyBindings.Set(GameAction.OpenSettings, 1, new KeyBindingEntry(Key.F2, true));
            AssertEntry(KeyBindings.GetBinding(GameAction.OpenSettings, 1), Key.F2, true, "Shift+F2を割り当てられる");
        }

        private static void H_ClearReset()
        {
            int changed = 0;
            Action handler = () => changed++;
            KeyBindings.Changed += handler;
            try
            {
                KeyBindings.Clear(GameAction.MoveUp, 0);
                AssertEntry(KeyBindings.GetBinding(GameAction.MoveUp, 0), Key.None, false, "Clearでスロットが未設定になる");
                AssertEntry(KeyBindings.GetBinding(GameAction.MoveUp, 1), Key.W, false, "他のスロットは変わらない");
                AssertEqual(1, changed, "Clearで変更通知が出る");

                KeyBindings.Clear(GameAction.MoveUp, 9);
                AssertEqual(1, changed, "範囲外のClearでは通知しない");

                KeyBindings.Set(GameAction.Confirm, 0, new KeyBindingEntry(Key.Z));
                AssertEqual(2, changed, "Setで変更通知が出る");

                KeyBindings.ResetToDefault();
                AssertEqual(3, changed, "ResetToDefaultで変更通知が出る");
                AssertEntry(KeyBindings.GetBinding(GameAction.MoveUp, 0), Key.UpArrow, false, "初期設定に戻る（上）");
                AssertEntry(KeyBindings.GetBinding(GameAction.Confirm, 0), Key.Enter, false, "初期設定に戻る（決定）");
                AssertEntry(KeyBindings.GetBinding(GameAction.PrevPiece, 0), Key.Tab, true, "初期設定に戻る（前の駒）");
            }
            finally
            {
                KeyBindings.Changed -= handler;
            }
        }

        private static void H_Labels()
        {
            AssertEqual("↑ / W", KeyBindings.GetLabel(GameAction.MoveUp), "上のラベル");
            AssertEqual("Tab", KeyBindings.GetLabel(GameAction.NextPiece), "次の駒のラベル");
            AssertEqual("Shift+Tab", KeyBindings.GetLabel(GameAction.PrevPiece), "前の駒のラベル");
            AssertEqual("Enter / NumpadEnter", KeyBindings.GetLabel(GameAction.Confirm), "決定のラベル");
            AssertEqual("Esc / Backspace", KeyBindings.GetLabel(GameAction.Cancel), "キャンセルのラベル");
            AssertEqual("Space", KeyBindings.GetLabel(GameAction.EndTurn), "ターン終了のラベル");
            AssertEqual("F1", KeyBindings.GetLabel(GameAction.OpenSettings), "設定を開くのラベル");

            AssertEqual("↑↓←→/WSAD", KeyBindings.GetMoveLabel(), "移動のまとめラベル");

            // 未設定
            KeyBindings.Clear(GameAction.EndTurn, 0);
            AssertEqual("未設定", KeyBindings.GetLabel(GameAction.EndTurn), "全て未設定なら「未設定」");
            AssertEqual("未設定", KeyBindings.GetEntryLabel(default), "GetEntryLabel(未設定)");
            AssertEqual("Q", KeyBindings.GetEntryLabel(new KeyBindingEntry(Key.Q)), "GetEntryLabel(Q)");
            AssertEqual("Shift+Q", KeyBindings.GetEntryLabel(new KeyBindingEntry(Key.Q, true)), "GetEntryLabel(Shift+Q)");

            // 数字キーは「Digit」を省く
            KeyBindings.Set(GameAction.OpenSettings, 1, new KeyBindingEntry(Key.Digit1));
            AssertEqual("F1 / 1", KeyBindings.GetLabel(GameAction.OpenSettings), "数字キーは数字だけ表示");

            // 移動が全て未設定なら「未設定」
            foreach (var direction in new[] { GameAction.MoveUp, GameAction.MoveDown, GameAction.MoveLeft, GameAction.MoveRight })
            {
                KeyBindings.Clear(direction, 0);
                KeyBindings.Clear(direction, 1);
            }
            AssertEqual("未設定", KeyBindings.GetMoveLabel(), "移動が全て未設定なら「未設定」");
        }

        private static void H_Persistence()
        {
            KeyBindings.Set(GameAction.MoveUp, 1, new KeyBindingEntry(Key.I));
            KeyBindings.Set(GameAction.PrevPiece, 1, new KeyBindingEntry(Key.Q, true));
            KeyBindings.Clear(GameAction.Cancel, 1);

            // 静的状態を捨てて、PlayerPrefsから読み直す
            ResetKeyBindingsStatics();
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveUp, 1), Key.I, false, "保存した割り当てが復元される");
            AssertEntry(KeyBindings.GetBinding(GameAction.PrevPiece, 1), Key.Q, true, "Shift付きも復元される");
            AssertEntry(KeyBindings.GetBinding(GameAction.Cancel, 1), Key.None, false, "解除した割り当ても復元される");
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveUp, 0), Key.UpArrow, false, "変えていない割り当ても残る");

            // 壊れた保存データは初期設定に戻る（読み込み失敗の警告がConsoleに1件出る）
            PlayerPrefs.SetString(KeyBindingsPrefsKey, "これはJSONではない{");
            ResetKeyBindingsStatics();
            AssertEntry(KeyBindings.GetBinding(GameAction.MoveUp, 1), Key.W, false, "壊れたデータは初期設定にフォールバック");
            AssertEntry(KeyBindings.GetBinding(GameAction.Confirm, 0), Key.Enter, false, "初期設定（決定）");
        }

        // ---------------------------------------------------------------
        // I. DropRules
        // ---------------------------------------------------------------

        private static void I_DropRules()
        {
            AssertEqual(3, DropRules.GetQualityTier(EnemyArchetype.Boss), "ボスの品質は3");
            AssertEqual(1, DropRules.GetQualityTier(EnemyArchetype.Scout), "スカウトの品質は1");
            AssertEqual(3, DropRules.GetQualityTier(EnemyArchetype.Retreater), "逃げる敵の品質は3");

            foreach (EnemyArchetype archetype in Enum.GetValues(typeof(EnemyArchetype)))
            {
                int baseTier = DropRules.GetQualityTier(archetype);
                for (int bonus = 0; bonus <= 10; bonus++)
                {
                    int tier = DropRules.GetBoostedQualityTier(archetype, bonus);
                    AssertTrue(tier <= 3, $"{archetype}+{bonus}：品質は3を超えない（{tier}）");
                    AssertTrue(tier >= baseTier, $"{archetype}+{bonus}：基礎より下がらない");
                    AssertEqual(Mathf.Min(3, baseTier + bonus), tier, $"{archetype}+{bonus}：品質 = min(3, 基礎+加算)");
                }
            }

            AssertEqual(1, DropRules.GetBoostedQualityTier(EnemyArchetype.Scout, 0), "スカウト+0");
            AssertEqual(2, DropRules.GetBoostedQualityTier(EnemyArchetype.Scout, 1), "スカウト+1");
            AssertEqual(3, DropRules.GetBoostedQualityTier(EnemyArchetype.Scout, 2), "スカウト+2");
            AssertEqual(3, DropRules.GetBoostedQualityTier(EnemyArchetype.Boss, 5), "ボス+5も3で頭打ち");
        }

        // ---------------------------------------------------------------
        // J. PieceSnapshotFactory
        // ---------------------------------------------------------------

        private static void J_MaxActions()
        {
            AssertEqual(1, PieceSnapshotFactory.ComputeMaxActions(null), "nullなら1");
            AssertEqual(1, PieceSnapshotFactory.ComputeMaxActions(new Dictionary<string, float>()), "空なら1");
            AssertEqual(2, PieceSnapshotFactory.ComputeMaxActions(Mods(("BonusActions", 1f))), "BonusActions=1なら2");
            AssertEqual(3, PieceSnapshotFactory.ComputeMaxActions(Mods(("BonusActions", 2f))), "BonusActions=2なら3");
            AssertEqual(2, PieceSnapshotFactory.ComputeMaxActions(Mods(("BonusActions", 1.9f))), "小数は切り捨て");
            AssertEqual(1, PieceSnapshotFactory.ComputeMaxActions(Mods(("DistanceBonusRate", 0.5f), ("DashRangeBonus", 1f))), "無関係なキーは影響しない");

            // 生成時のactionsLeft
            var player = Player(PieceType.Knight, 0, 0, 10, 3, Mods(("BonusActions", 1f)));
            AssertEqual(2, player.actionsLeft, "プレイヤー駒のactionsLeft（BonusActions込み）");
            AssertEqual(1, Player(PieceType.Rook, 0, 0, 10, 3).actionsLeft, "プレイヤー駒の通常のactionsLeft");
            AssertEqual(0, Enemy(EnemyArchetype.Scout, 0, 0, 5, 2).actionsLeft, "敵駒のactionsLeftは0");

            // その他の初期値
            var snapshot = Player(PieceType.Pawn, 2, 3, 10, 3);
            AssertEqual(1, snapshot.rank, "rankの初期値は1");
            AssertEqual(V(2, 3), snapshot.position, "position");
            AssertTrue(snapshot.isPlayerSide, "isPlayerSide");
            AssertTrue(snapshot.statModifiers != null, "statModifiersはnullでない");
            AssertEqual(0, snapshot.statusEffects.Count, "状態異常は空");

            // instanceIdは重複しない
            var a = Player(PieceType.Pawn, 0, 0, 1, 1);
            var b = Player(PieceType.Pawn, 0, 0, 1, 1);
            AssertTrue(a.instanceId != b.instanceId, "instanceIdは一意");

            // 状態異常はコピーされる（元のリストの変更が影響しない）
            var source = new List<ActiveStatusEffect>
            {
                new ActiveStatusEffect { type = StatusEffectType.Stun, remainingTurns = 2, magnitude = 0f, appliedTurn = 1 }
            };
            var enemy = PieceSnapshotFactory.Create(PieceType.King, false, V(0, 0), 5, 5, 2, null, source, EnemyArchetype.Boss);
            source[0].remainingTurns = 99;
            source.Add(new ActiveStatusEffect { type = StatusEffectType.Slow, remainingTurns = 1 });
            AssertEqual(1, enemy.statusEffects.Count, "元のリストへの追加は影響しない");
            AssertEqual(2, enemy.statusEffects[0].remainingTurns, "元の要素の変更は影響しない（ディープコピー）");
            AssertEqual(1, enemy.statusEffects[0].appliedTurn, "appliedTurnもコピーされる");
            AssertEqual(EnemyArchetype.Boss, enemy.enemyArchetype, "enemyArchetypeが保持される");
            AssertTrue(enemy.statusEffects is List<ActiveStatusEffect>, "statusEffectsはList<ActiveStatusEffect>の実体");
        }
    }
}
