namespace Chebyss.Battle
{
    /// <summary>
    /// 墓地の1件。PieceSnapshotをそのまま使わず、
    /// 死んだ駒には意味のない情報（HP・statModifiers等）を持たない軽量な型にしている。
    /// </summary>
    public readonly struct GraveyardEntry
    {
        public readonly int instanceId;
        public readonly PieceType pieceType;
        public readonly int turnDefeated;

        public GraveyardEntry(int instanceId, PieceType pieceType, int turnDefeated)
        {
            this.instanceId = instanceId;
            this.pieceType = pieceType;
            this.turnDefeated = turnDefeated;
        }
    }
}
