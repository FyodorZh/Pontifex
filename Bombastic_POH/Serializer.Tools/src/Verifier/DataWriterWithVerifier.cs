using Serializer.Extensions.Pool;
using Serializer.Factory;

namespace Serializer.BinarySerializer
{
    public class DataWriterWithVerifier : DataWriter<ManagedWriterWithVerifier<ManagedWriter<DefaultAllocator>>>
    {
        public DataWriterWithVerifier(IDataStructFactory factory, ManagedWriterVerifier verifier)
            : base(new ManagedWriterWithVerifier<ManagedWriter<DefaultAllocator>>(ManagedWriter<DefaultAllocator>.Construct(new DefaultAllocator()), verifier), factory)
        {
        }
    }

    public class DataWriterWithPoolAndVerifier : DataWriter<ManagedWriterWithVerifier<ManagedWriter<BytesPoolAllocator>>>
    {
        public DataWriterWithPoolAndVerifier(IManagedWriterVerifier verifier)
            : this(verifier, new TrivialFactory())
        {
        }

        public DataWriterWithPoolAndVerifier(IManagedWriterVerifier verifier, IDataStructFactory factory)
            : base(new ManagedWriterWithVerifier<ManagedWriter<BytesPoolAllocator>>(ManagedWriter<BytesPoolAllocator>.Construct(new BytesPoolAllocator()), verifier), factory)
        {
        }
    }
}