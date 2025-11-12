using System;
using System.Diagnostics;

namespace DBG
{
    public static class Profiler
    {
        private static Action<string> mDoBeginSample = (s) => { };
        private static Action mDoEndSample = () => {};

        [Conditional("DEBUG")]
        public static void Setup(Action<string> doBeginSample, Action doEndSample)
        {
            mDoBeginSample = doBeginSample ?? (s => { });
            mDoEndSample = doEndSample ?? (() => { });
        }

        [Conditional("DEBUG")]
        public static void BeginSample(string name)
        {
            mDoBeginSample(name);
        }

        [Conditional("DEBUG")]
        public static void EndSample()
        {
            mDoEndSample();
        }
    }

    public static class ThreadChecker
    {
        private static int mId = System.Threading.Thread.CurrentThread.ManagedThreadId;

        public static void Check()
        {
#if UNITY_2017_1_OR_NEWER
            int curId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (mId != curId)
            {
                Debugger.Break();
                Log.e("FUCK");
            }
#endif
        }

    }
}
