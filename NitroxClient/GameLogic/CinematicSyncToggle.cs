namespace NitroxClient.GameLogic
{
    /// <summary>
    /// Feature toggle for cinematic player controller synchronization.
    /// </summary>
    internal static class CinematicSyncToggle
    {
        public const bool Enabled = false;

        /// <summary>
        /// Gate for concurrency checks that deny cinematics when another player is already interacting.
        /// </summary>
        public const bool ConcurrencyChecksEnabled = false;
    }
}
