namespace Shared.Constants
{
    /// <summary>
    /// имена анхоров
    /// </summary>
    public enum AnchorType
    {
        /// <summary>
        /// точка к которой крепится ui(имя хпбар) юнита
        /// </summary>
        UI = 0,
        /// <summary>
        /// точка из которой вылетает FlyOff текст
        /// </summary>
        FlyOffText = 1,
        /// <summary>
        /// точка, к которой цепляются декали(подсветка юнита) 
        /// </summary>
        Decal = 2,
        /// <summary>
        /// голова юнита
        /// </summary>
        Head = 3,
        /// <summary>
        /// тело юнита
        /// </summary>
        Body = 4,
        /// <summary>
        /// нижняя точка юнита
        /// </summary>
        Ground = 5,
        /// <summary>
        /// точка каста
        /// </summary>
        Cast = 6,
        /// <summary>
        /// ещё одна точка каста
        /// </summary>
        Cast1 = 7
    }
}