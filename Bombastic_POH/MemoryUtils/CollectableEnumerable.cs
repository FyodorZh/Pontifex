// using System;
// using System.Collections.Generic;
//
// namespace Shared.Pool
// {
//     public class CollectableEnumerable<T> : Collectable
//     {
//         private readonly List<T> mData = new List<T>();
//
//         protected virtual void OnMoveTo(T element) { }
//
//         protected virtual void OnCollect(List<T> data) { }
//
//         protected sealed override void Collect()
//         {
//             OnCollect(mData);
//             mData.Clear();
//         }
//
//         protected sealed override void Restore()
//         {
//         }
//
//         public void Add(T element)
//         {
//             mData.Add(element);
//         }
//
//         public void Add(T[] elements)
//         {
//             for (int i = 0; i < elements.Length; ++i)
//             {
//                 mData.Add(elements[i]);
//             }
//         }
//
//         public void Add(List<T> elements)
//         {
//             int count = elements.Count;
//             for (int i = 0; i < count; ++i)
//             {
//                 mData.Add(elements[i]);
//             }
//         }
//
//         public int Count
//         {
//             get { return mData.Count; }
//         }
//
//         public T this[int id]
//         {
//             get { return mData[id]; }
//         }
//
//         public Enumerator GetEnumerator()
//         {
//             return new Enumerator(this, mData);
//         }
//
//         [Serializable]
//         public struct Enumerator : IEnumerator<T>, System.Collections.IEnumerator
//         {
//             private readonly CollectableEnumerable<T> mOwner;
//             private readonly List<T> mList;
//             private int mIndex;
//             private T mCurrent;
//
//             public Enumerator(CollectableEnumerable<T> owner, List<T> list)
//             {
//                 mOwner = owner;
//                 mList = list;
//                 mIndex = 0;
//                 mCurrent = default(T);
//             }
//
//             public void Dispose()
//             {
//                 mOwner.Release();
//             }
//
//             public bool MoveNext()
//             {
//                 List<T> localList = mList;
//
//                 if (mIndex < localList.Count)
//                 {
//                     mCurrent = localList[mIndex];
//                     mOwner.OnMoveTo(mCurrent);
//                     mIndex++;
//                     return true;
//                 }
//                 return MoveNextRare();
//             }
//
//             private bool MoveNextRare()
//             {
//                 mIndex = mList.Count + 1;
//                 mCurrent = default(T);
//                 return false;
//             }
//
//             public T Current
//             {
//                 get
//                 {
//                     return mCurrent;
//                 }
//             }
//
//             Object System.Collections.IEnumerator.Current
//             {
//                 get
//                 {
//                     if (mIndex == 0 || mIndex == mList.Count + 1)
//                     {
//                         throw new InvalidOperationException();
//                     }
//                     return Current;
//                 }
//             }
//
//             void System.Collections.IEnumerator.Reset()
//             {
//                 mIndex = 0;
//                 mCurrent = default(T);
//             }
//         }
//     }
// }
