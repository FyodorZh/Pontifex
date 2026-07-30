namespace Pontifex.Utils.CheckPointGate
{
    /// <summary>
    ///   Indicates how a wait operation on a <see cref="ICheckPointCtl"/>
    ///   completed.
    /// </summary>
    public enum CheckPointWaitResult
    {
        /// <summary>
        ///   The gate reached the required number of hits: the blocking hit
        ///   has arrived and <see cref="ICheckPoint.Hit"/> /
        ///   <see cref="ICheckPoint.HitAsync"/> is now blocking the caller.
        /// </summary>
        Reached,

        /// <summary>
        ///   The gate was released (via <see cref="ICheckPointCtl.Reset"/>,
        ///   <see cref="ICheckPointCtl.Arm"/> replacing this operation, or
        ///   <see cref="System.IDisposable.Dispose"/>) before the required
        ///   number of hits was reached.
        /// </summary>
        Released,
    }
}