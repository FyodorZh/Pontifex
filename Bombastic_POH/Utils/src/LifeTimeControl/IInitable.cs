namespace Shared
{
    public interface IDeInitable
    {
        void DeInit();
    }

    public interface IInitable : IDeInitable
    {
        bool Init();
    }

    public interface IInitable<TParam0> : IDeInitable
    {
        bool Init(TParam0 p0);
    }

    public interface IInitable<TParam0, TParam1> : IDeInitable
    {
        bool Init(TParam0 p0, TParam1 p1);
    }

    public interface IInitable<TParam0, TParam1, TParam2> : IDeInitable
    {
        bool Init(TParam0 p0, TParam1 p1, TParam2 p2);
    }

    public interface IInitable<TParam0, TParam1, TParam2, TParam3> : IDeInitable
    {
        bool Init(TParam0 p0, TParam1 p1, TParam2 p2, TParam3 p3);
    }
}