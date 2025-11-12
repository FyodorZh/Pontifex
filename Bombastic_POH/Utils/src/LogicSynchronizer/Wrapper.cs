namespace Shared.LogicSynchronizer
{
    public abstract class Wrapper<TKey>
    {
        protected readonly ISyncContextViewForWrapper<TKey> Context;
        protected readonly System.Func<bool> mIsReadOnly;

        protected bool IsReadOnly
        {
            get { return mIsReadOnly.Invoke(); }
        }

        public TKey Key
        {
            get { return Context.Key; }
        }

        protected Wrapper(ISyncContextViewForWrapper<TKey> context, System.Func<bool> isReadOnly = null)
        {
            Context = context;
            if (isReadOnly != null)
            {
                mIsReadOnly = isReadOnly;
            }
            else
            {
                mIsReadOnly = () => false;
            }
        }
    }
}
