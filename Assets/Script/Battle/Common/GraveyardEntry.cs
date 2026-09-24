namespace Chebyss.Battle
{
    /// <summary>
    /// 墓地の1件。PieceSnapshotをそのまま使わず、
    /// 死んだ駒には意味のない情報（HP・statModifiers等）を持たない軽量な型にしている。
    /// </summary>
    public readonly struct GraveyardEntry
    {
        public readonly PieceType pieceType;
        public readonly int turnDefeated;

        public GraveyardEntry(PieceType pieceType, int turnDefeated)
        {
            this.pieceType = pieceType;
            this.turnDefeated = turnDefeated;
        }
    }
}
