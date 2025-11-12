namespace Shared.Battle
{
    public class DummyLogicInfoVisualizer : ILogicInfoPresenterProxy, ILogicInfoPresenter
    {
        ILogicInfoPresenter ILogicInfoPresenterProxy.Presenter
        {
            get { return this; }
        }

        public bool IsEnabled { get { return false; } }
        
        void ILogicInfoPresenter.AddLogicVisualizer(ILogicInfo logic)
        {
        }

        void ILogicInfoPresenter.UpdateLogicVisualizer(ILogicInfoUpdate update)
        {
        }

        void ILogicInfoPresenter.RemoveLogicVisualizer(Shared.ID<ILogicInfo> id)
        {
        }
    }
}
