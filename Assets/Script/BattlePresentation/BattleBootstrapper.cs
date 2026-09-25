using System.Collections.Generic;
using UnityEngine;
using Chebyss.Battle;

public class BattleBootstrapper : MonoBehaviour
{
    [Header("盤面設定")]
    [SerializeField] private int boardWidth = 8;
    [SerializeField] private int boardHeight = 8;
    [SerializeField] private Vector2Int kingStartPosition = new Vector2Int(4, 0);
    [SerializeField] private int deploymentZoneHeight = 3;
    [SerializeField] private int kingHp = 20;

    [Header("プレイヤー駒パターン（SOアセットを割り当てる）")]
    [SerializeField] private KnightMovementPatternSO knightPattern;
    [SerializeField] private RookMovementPatternSO rookPattern;
    [SerializeField] private BishopMovementPatternSO bishopPattern;

    [Header("プレイヤー駒 基礎ステータス（プロトタイプ用の仮値）")]
    [SerializeField] private int knightHp = 10;
    [SerializeField] private int knightAttack = 5;
    [SerializeField] private int rookHp = 12;
    [SerializeField] private int rookAttack = 4;
    [SerializeField] private int bishopHp = 8;
    [SerializeField] private int bishopAttack = 3;

    [Header("敵パターン（SOアセットを割り当てる）")]
    [SerializeField] private StraightAdvanceMovementPatternSO bossPattern;
    [SerializeField] private ChaseNearestMovementPatternSO scoutPattern;
    [SerializeField] private RetreatMovementPatternSO retreaterPattern; // 今回の固定配置では未使用だが登録しておく

    [Header("敵の初期配置（プロトタイプ用の固定データ。フロア/呪縛レベル未実装のための仮対応）")]
    [SerializeField] private Vector2Int bossStartPosition = new Vector2Int(4, 7);
    [SerializeField] private Vector2Int[] scoutStartPositions =
        { new Vector2Int(2, 7), new Vector2Int(6, 7) };
    [SerializeField] private int bossHp = 30;
    [SerializeField] private int bossAttack = 6;
    [SerializeField] private int scoutHp = 8;
    [SerializeField] private int scoutAttack = 3;

    public BoardState Board { get; private set; }
    public PlacementController Placement { get; private set; }
    public BattleTurnController TurnController { get; private set; }

    void Awake()
    {
        Board = new BoardState(boardWidth, boardHeight, kingStartPosition);

        var king = PieceSnapshotFactory.Create(
            PieceType.King, isPlayerSide: true, position: kingStartPosition,
            currentHp: kingHp, attack: 0, statModifiers: null, statusEffects: null);
        Board.PlacePiece(king);

        PlaceFixedTestEnemies();

        var roster = new List<PieceSnapshot>
        {
            PieceSnapshotFactory.Create(PieceType.Knight, true, Vector2Int.zero, knightHp, knightAttack, null, null),
            PieceSnapshotFactory.Create(PieceType.Rook,   true, Vector2Int.zero, rookHp,   rookAttack,   null, null),
            PieceSnapshotFactory.Create(PieceType.Bishop, true, Vector2Int.zero, bishopHp, bishopAttack, null, null),
        };
        Placement = new PlacementController(Board, roster, deploymentZoneHeight);
    }

    private void PlaceFixedTestEnemies()
    {
        // PieceTypeは見た目上の仮の分類(敵の正式なデザインはdesign doc「15.未決定事項」参照)。
        // 行動を決めるのはenemyArchetypeの方。
        var boss = PieceSnapshotFactory.Create(
            PieceType.Pawn, isPlayerSide: false, position: bossStartPosition,
            currentHp: bossHp, attack: bossAttack, statModifiers: null, statusEffects: null,
            enemyArchetype: EnemyArchetype.Boss);
        Board.PlacePiece(boss);

        foreach (var pos in scoutStartPositions)
        {
            var scout = PieceSnapshotFactory.Create(
                PieceType.Pawn, isPlayerSide: false, position: pos,
                currentHp: scoutHp, attack: scoutAttack, statModifiers: null, statusEffects: null,
                enemyArchetype: EnemyArchetype.Scout);
            Board.PlacePiece(scout);
        }
    }

    // 配置フェーズ完了後、呼び出し元（UI）がこれを呼んでバトル本編を起動する
    public void StartBattlePhase()
    {
        if (!Placement.IsComplete) return; // 未配置の駒が残っている

        var playerRules = new Dictionary<PieceType, IMovementAttackRule>
        {
            { PieceType.Knight, new KnightDashRule() },
            { PieceType.Rook,   new RookLineRule() },
            { PieceType.Bishop, new BishopLineRule() },
        };
        var playerPatterns = new Dictionary<PieceType, PieceMovementPatternSO>
        {
            { PieceType.Knight, knightPattern },
            { PieceType.Rook,   rookPattern },
            { PieceType.Bishop, bishopPattern },
        };

        var enemyRules = new Dictionary<EnemyArchetype, IEnemyMoveRule>
        {
            { EnemyArchetype.Boss,      new StraightAdvanceMoveRule() },
            { EnemyArchetype.Scout,     new ChaseNearestMoveRule() },
            { EnemyArchetype.Retreater, new RetreatMoveRule() },
        };
        var enemyPatterns = new Dictionary<EnemyArchetype, EnemyMovementPatternSO>
        {
            { EnemyArchetype.Boss,      bossPattern },
            { EnemyArchetype.Scout,     scoutPattern },
            { EnemyArchetype.Retreater, retreaterPattern },
        };

        var enemyTurnProcessor = new EnemyTurnProcessor(enemyRules, enemyPatterns);
        TurnController = new BattleTurnController(Board, enemyTurnProcessor, playerRules, playerPatterns);
    }
}
