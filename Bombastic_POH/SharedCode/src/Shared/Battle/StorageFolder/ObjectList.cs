using System;
using System.Collections;
using System.Collections.Generic;

public class ObjectList<T> : IEnumerable where T : class
{
    protected List<T> mItems = null;
    public List<T> Items { get { return mItems; } }

    public ObjectList() { mItems = new List<T>(); }
    public ObjectList(Int32 capacity) { mItems = new List<T>(capacity); }

    public IEnumerator GetEnumerator() { return mItems.GetEnumerator(); }
    public Int32 Count { get { return mItems.Count; } }
    public void Clear() { mItems.Clear(); }
    public T GetItem(Int32 index)
    {
        int length = Count;
        if (index < 0 || index >= length)
        {
            //Log.e("Incorrect array index: {0}", index.ToString());
            return default(T);
        }
        return mItems[index];
    }
    public Int32 FindItem(T obj)
    {
        Int32 i = 0;
        foreach (T _obj in mItems)
        {
            if (_obj == obj)
            {
                return i;
            }
            i++;
        }
        return -1;
    }
    public Boolean RemoveItem(T obj)
    {
        Int32 i = 0;
        foreach (T _obj in mItems)
        {
            if (_obj == obj)
            {
                mItems.RemoveAt(i);
                return true;
            }
            i++;
        }
        return false;
    }
    public Boolean RemoveItem(Int32 index)
    {
        int length = Count;
        if (index < 0 || index >= length)
        {
            Log.e("Incorrect array index: {0}", index.ToString());
            return false;
        }
        mItems.RemoveAt(index);
        return true;
    }
    public virtual void Add(T obj) { mItems.Add(obj); }
}
