using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Shared;

namespace LogConsumers
{
    public class CharBuffer : TextWriter
    {
        private readonly IConcurrentQueue<char[]> _charArrayPool;
        private readonly int _charArrayMaxLength;
        private readonly IConcurrentQueue<ArraySegment<char>> _readyBuffers = new SynchronizedConcurrentQueue<ArraySegment<char>>();

        private char[] _currentBuffer;
        private int _nextBufferIndex;

        public CharBuffer(IConcurrentQueue<char[]> charArrayPool, int charArrayMaxLength)
        {
            _charArrayPool = charArrayPool;
            _charArrayMaxLength = charArrayMaxLength;
        }

        public override Encoding Encoding
        {
            get { return Encoding.Unicode; }
        }

        public override void Write(char value)
        {
            CheckCurrentBuffer();
            _currentBuffer[_nextBufferIndex++] = value;
        }

        public override void Write(string value)
        {
            for (int i = 0; i < value.Length; ++i)
            {
                CheckCurrentBuffer();
                _currentBuffer[_nextBufferIndex++] = value[i];
            }
        }

        public void Clear()
        {
            _nextBufferIndex = 0;
            _currentBuffer = null;

            ArraySegment<char> notFreedBuffer;
            while (_readyBuffers.TryPop(out notFreedBuffer))
            {
                // в нормальном режиме работы сюда никогда не попадем
                // но на всякий случай освобождаем те буферы, которые не освободились итератором
                _charArrayPool.Put(notFreedBuffer.Array);
            }
        }

        public Enumerator GetEnumerator()
        {
            _readyBuffers.Put(new ArraySegment<char>(_currentBuffer, 0, _nextBufferIndex));
            return new Enumerator(_readyBuffers, _charArrayPool);
        }

#if  !UNITY        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void CheckCurrentBuffer()
        {
            if (_currentBuffer == null)
            {
                GetCharAray(out _currentBuffer);
            }

            if (_nextBufferIndex > _currentBuffer.Length - 1)
            {
                _readyBuffers.Put(new ArraySegment<char>(_currentBuffer, 0, _currentBuffer.Length));
                GetCharAray(out _currentBuffer);
                _nextBufferIndex = 0;
            }
        }

        private void GetCharAray(out char[] charArrayPool)
        {
            if (!_charArrayPool.TryPop(out charArrayPool))
            {
                charArrayPool = new char[_charArrayMaxLength];
            }
        }

        public struct Enumerator
        {
            private readonly IConcurrentQueue<ArraySegment<char>> _buffers;
            private readonly IConcurrentQueue<char[]> _charArrayPool;
            private ArraySegment<char> _currentBuffer;

            public Enumerator(IConcurrentQueue<ArraySegment<char>> buffers, IConcurrentQueue<char[]> charArrayPool)
            {
                _buffers = buffers;
                _charArrayPool = charArrayPool;
                _currentBuffer = default(ArraySegment<char>);
            }

            public ArraySegment<char> Current
            {
                get { return _currentBuffer; }
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (_currentBuffer != default(ArraySegment<char>))
                {
                    _charArrayPool.Put(_currentBuffer.Array);
                    _currentBuffer = default(ArraySegment<char>);
                }

                return _buffers.TryPop(out _currentBuffer);
            }
        }
    }
}
