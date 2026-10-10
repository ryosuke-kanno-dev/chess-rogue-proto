using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>階ごとの敵生成の調整値。BattleBootstrapperのInspectorから編集する（暫定値）。</summary>
    [Serializable]
    public class EncounterGeneratorSettings
    {
        [Tooltip("この階数ごとにボス階になる。0以下ならボス階なし")]
        public int bossFloorInterval = 3;

        public int baseScoutCount = 2;

        [Tooltip("この階数ごとにスカウトが1体増える")]
        public int scoutCountIncreaseEveryFloors = 2;

        public int maxScoutCount = 5;

        [Tooltip("通常階だけ。逃げる敵を1体追加する確率（0〜1）")]
        [Range(0f, 1f)]
        public float retreaterChance = 0.3f;

        public int retreaterHp = 4;
        public int retreaterAttack = 1;

        [Tooltip("1階進むごとのスカウトのHP増加量")]
        public int scoutHpPerFloor = 1;

        [Tooltip("1階進むごとのボスのHP増加量")]
        public int bossHpPerFloor = 2;
    }

    /// <summary>
    /// 階ごとの敵の出現計画（EncounterPlan）を生成する。MonoBehaviourに依存しない。
    /// 乱数はSystem.Randomにseedを渡して使うため、同じ引数なら必ず同じ結果になる
    /// （装備画面での次の階の表示と、実際の戦闘で同じ計画を使うため）。
    /// </summary>
    public static class EncounterGenerator
    {
        public static EncounterPlan Generate(
            int floor,
            int boardWidth,
            int boardHeight,
            EncounterGeneratorSettings settings,
            int baseBossHp,
            int baseBossAttack,
            int baseScoutHp,
            int baseScoutAttack,
            bool allowRetreater,
            int seed)
        {
            settings ??= new EncounterGeneratorSettings();
            floor = Math.Max(1, floor);

            var rng = new System.Random(seed);

            bool isBossFloor = settings.bossFloorInterval > 0 && floor % settings.bossFloorInterval == 0;

            int normalScoutCount = settings.baseScoutCount;
            if (settings.scoutCountIncreaseEveryFloors > 0)
                normalScoutCount += (floor - 1) / settings.scoutCountIncreaseEveryFloors;
            normalScoutCount = Math.Min(settings.maxScoutCount, normalScoutCount);

            int scoutCount = isBossFloor ? Math.Max(1, normalScoutCount - 1) : normalScoutCount;
            int scoutHp = baseScoutHp + (floor - 1) * settings.scoutHpPerFloor;

            // 配置：敵陣の最上段から順に使う。ボスは最上段中央に固定。
            var occupied = new HashSet<Vector2Int>();
            var pool = new List<Vector2Int>();
            int nextRow = boardHeight - 1;

            Vector2Int? TakeCell()
            {
                while (pool.Count == 0)
                {
                    if (nextRow < 0) return null; // 盤面が埋まった

                    for (int x = 0; x < boardWidth; x++)
                    {
                        var cell = new Vector2Int(x, nextRow);
                        if (!occupied.Contains(cell)) pool.Add(cell);
                    }
                    nextRow--;

                    // Fisher-Yatesシャッフル（seed付きのrngのみ使用）
                    for (int i = pool.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        (pool[i], pool[j]) = (pool[j], pool[i]);
                    }
                }

                var taken = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                occupied.Add(taken);
                return taken;
            }

            var spawns = new List<EnemySpawn>();

            if (isBossFloor)
            {
                var bossPos = new Vector2Int(boardWidth / 2, boardHeight - 1);
                occupied.Add(bossPos);
                int bossHp = baseBossHp + (floor - 1) * settings.bossHpPerFloor;
                spawns.Add(new EnemySpawn(EnemyArchetype.Boss, bossPos, bossHp, baseBossAttack));
            }

            for (int i = 0; i < scoutCount; i++)
            {
                var cell = TakeCell();
                if (!cell.HasValue) break;
                spawns.Add(new EnemySpawn(EnemyArchetype.Scout, cell.Value, scoutHp, baseScoutAttack));
            }

            // 逃げる敵は通常階のみ。確率判定は常にrngを1回消費する（結果が引数だけで決まるように）
            if (!isBossFloor)
            {
                bool addRetreater = rng.NextDouble() < settings.retreaterChance;
                if (allowRetreater && addRetreater)
                {
                    var cell = TakeCell();
                    if (cell.HasValue)
                        spawns.Add(new EnemySpawn(EnemyArchetype.Retreater, cell.Value, settings.retreaterHp, settings.retreaterAttack));
                }
            }

            return new EncounterPlan(floor, spawns);
        }
    }
}
