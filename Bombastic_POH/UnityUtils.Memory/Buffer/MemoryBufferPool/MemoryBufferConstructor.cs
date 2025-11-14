//#define USE_BUFFER_HOLDER

namespace Shared.Buffer
{
    public static class MemoryBufferConstructor
    {
        #if USE_BUFFER_HOLDER
        public static readonly IConstructor<IMemoryBuffer> Instance = new MemoryBufferNoHolderConstructor();
        #else
        public static readonly IConstructor<IMemoryBuffer> Instance = new MemoryBufferWithHolderConstructor();
        #endif
    }

    internal class MemoryBufferNoHolderConstructor : IConstructor<IMemoryBuffer>
    {
        public IMemoryBuffer Construct()
        {
            return new MemoryBuffer();
        }
    }

    internal class MemoryBufferWithHolderConstructor : IConstructor<IMemoryBuffer>
    {
        public IMemoryBuffer Construct()
        {
            return new MemoryBufferAsHolder();
        }
    }
}