using Shared;

namespace Shared.Battle
{
    public interface ILogicInfoPresenter
    {
        void AddLogicVisualizer(ILogicInfo logic);
        void UpdateLogicVisualizer(ILogicInfoUpdate update);
        void RemoveLogicVisualizer(ID<ILogicInfo> id);
    }

    public interface ILogicInfoPresenterProxy
    {
        ILogicInfoPresenter Presenter { get; }
        bool IsEnabled { get; }
    }

    public enum LogicInfoType : byte
    {
        Unknown = 0,

        RectGeometry,
        HollowRectGeometry,
        CircleGeometry,
        TorusGeometry,
        CircleSectorGeometry,
    }

    // Базовый интерфейс для визуализации данных логических объектов трастовой части логики
    public interface ILogicInfo
    {
        ID<ILogicInfo> Id { get; }
        ID<ILogicInfo> OwnerId { get; }
        LogicInfoType InfoType { get; }
        string Name { get; }
    }

    public interface ILogicInfoUpdate
    {
        ID<ILogicInfo> Id { get; }
        LogicInfoType InfoType { get; }
    }

    public abstract class LogicInfo : ILogicInfo, ILogicInfoUpdate
    {
        public ID<ILogicInfo> Id { get; protected set; }
        public ID<ILogicInfo> OwnerId { get; protected set; }
        public LogicInfoType InfoType { get; protected set; }
        public string Name { get; protected set; }

        protected LogicInfo(LogicInfoType infoType)
        {
            InfoType = infoType;
        }

        protected LogicInfo(LogicInfoType infoType, IDSource<ILogicInfo> idSource, ID<ILogicInfo> ownerId):
            this(infoType)
        {
            Id = new ID<ILogicInfo>(idSource);
            OwnerId = ownerId;
        }
    }

    public interface ILogicGeometryUpdate : ILogicInfoUpdate
    {
        Geom2d.Vector Center { get; }
        Geom2d.Vector Direction { get; }
    }

    public interface ILogicGeometry : ILogicInfo, ILogicGeometryUpdate { }
    public abstract class LogicGeometry : LogicInfo, ILogicGeometry
    {
        public Geom2d.Vector Center { get; protected set; }
        public Geom2d.Vector Direction { get; protected set; }

        protected LogicGeometry(LogicInfoType infoType, IDSource<ILogicInfo> idSource, ID<ILogicInfo> ownerId)
            : base(infoType, idSource, ownerId)
        {
        }
    }

    public interface IRectLogicGeometryUpdate : ILogicGeometryUpdate
    {
        Geom2d.Quad Quad { get; }
    }

    public interface IRectLogicGeometry : ILogicInfo, IRectLogicGeometryUpdate { }
    public class RectLogicGeometry : LogicGeometry, IRectLogicGeometry
    {
        public Geom2d.Quad Quad { get; protected set; }

        public RectLogicGeometry(IDSource<ILogicInfo> idSource, ID<ILogicInfo> ownerId)
            : base(LogicInfoType.RectGeometry, idSource, ownerId)
        {
        }
    }

    public interface IHollowRectLogicGeometryUpdate : ILogicGeometryUpdate
    {
        Geom2d.Quad OuterQuad { get; }
        Geom2d.Quad InnerQuad { get; }
    }

    public interface IHollowRectLogicGeometry : ILogicInfo, IHollowRectLogicGeometryUpdate { }
    public class HollowRectLogicGeometry : LogicGeometry, IHollowRectLogicGeometry
    {
        public Geom2d.Quad OuterQuad { get; protected set; }
        public Geom2d.Quad InnerQuad { get; protected set; }

        public HollowRectLogicGeometry(IDSource<ILogicInfo> idSource, ID<ILogicInfo> ownerId)
            : base(LogicInfoType.HollowRectGeometry, idSource, ownerId)
        {
        }
    }

    public interface ICircleLogicGeometryUpdate : ILogicGeometryUpdate
    {
        Geom2d.Circle Circle { get; }
    }

    public interface ICircleLogicGeometry : ILogicInfo, ICircleLogicGeometryUpdate { }
    public class CircleLogicGeometry : LogicGeometry, ICircleLogicGeometry
    {
        public Geom2d.Circle Circle { get; protected set; }

        public CircleLogicGeometry(IDSource<ILogicInfo> idSource, ID<ILogicInfo> ownerId)
            : base(LogicInfoType.CircleGeometry, idSource, ownerId)
        {
        }
    }

    public interface ITorusLogicGeometryUpdate : ILogicGeometryUpdate
    {
        Geom2d.Circle OuterCircle { get; }
        float InnerRadius { get; }
    }

    public interface ITorusLogicGeometry : ILogicInfo, ITorusLogicGeometryUpdate { }
    public class TorusLogicGeometry : LogicGeometry, ITorusLogicGeometry
    {
        public Geom2d.Circle OuterCircle { get; protected set; }
        public float InnerRadius  { get; protected set; }

        public TorusLogicGeometry(IDSource<ILogicInfo> idSource, ID<ILogicInfo> ownerId)
            : base(LogicInfoType.CircleGeometry, idSource, ownerId)
        {
        }
    }

    public interface ICircleSectorLogicGeometryUpdate : ILogicGeometryUpdate
    {
        Geom2d.CircleSector Sector { get; }
    }

    public interface ICircleSectorLogicGeometry : ILogicInfo, ICircleSectorLogicGeometryUpdate { }
    public class CircleSectorLogicGeometry : LogicGeometry, ICircleSectorLogicGeometry
    {
        public Geom2d.CircleSector Sector { get; protected set; }

        public CircleSectorLogicGeometry(IDSource<ILogicInfo> idSource, ID<ILogicInfo> ownerId)
            : base(LogicInfoType.CircleSectorGeometry, idSource, ownerId)
        {
        }
    }
}
