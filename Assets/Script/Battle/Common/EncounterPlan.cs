using System.Collections.Generic;
using UnityEngine;

namespace Chebyss.Battle
{
    /// <summary>敵1体分の出現データ。</summary>
    public readonly struct EnemySpawn
    {
        public readonly EnemyArchetype archetype;
        public readonly Vector2Int position;
        public readonly int hp;
        public readonly int attack;

        public EnemySpawn(EnemyArchetype archetype, Vector2Int position, int hp, int attack)
        {
            this.archetype = archetype;
            this.position = position;
            this.hp = hp;
            this.attack = attack;
        }
    }

    /// <summary>
    /// 1フロア分の敵の出現計画。戦闘開始時にBattleBootstrapperがこれに従って敵を配置し、
    /// 装備画面は「次の階」の情報開示（感知レベル）の元データとして参照する。
    /// </summary>
    public class EncounterPlan
    {
        public readonly int floorNumber;
        public readonly IReadOnlyList<EnemySpawn> enemies;

        public EncounterPlan(int floorNumber, IReadOnlyList<EnemySpawn> enemies)
        {
            this.floorNumber = floorNumber;
            this.enemies = enemies ?? new List<EnemySpawn>();
        }
    }
}
