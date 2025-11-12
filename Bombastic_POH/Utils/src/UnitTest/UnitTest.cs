using System;
using System.Collections.Generic;
using System.Reflection;
namespace UT
{
    public class UTAttribute : System.Attribute
    {
        public string TestName { get; private set; }

        public UTAttribute(string name)
        {
            TestName = name;
        }
    }

    public interface IUTest
    {
        void Assert(bool flag, string text);
        void Assert(bool flag);
        void Equal(int a, int b);
        void Equal(bool a, bool b);
        void Equal(float a, float b);
        void Equal(double a, double b);
        //void Equal(Shared.Time a, Shared.Time b);
        //void Equal(Geom2d.Vector a, Geom2d.Vector b);
        //void Equal(Geom2d.Rotation a, Geom2d.Rotation b);

        IUTest Check(string text);
        IUTest Check(Func<string> text);

        bool RandomBool();
        int RandomInt(int exclusiveMax);
        float RandomFloat();
    }

    public class UnitTest : IUTest
    {
        private const double Eps = 1e-5;

        private class UTFailException : Exception
        {
            private System.Diagnostics.StackFrame mStackFrame;
            
            public UTFailException()
            {
                mStackFrame = new System.Diagnostics.StackFrame(2, true);
            }

            public override string ToString()
            {
                return mStackFrame.ToString();
            }
        }

        public static void RunUT(IEnumerable<System.Reflection.Assembly> assemblies)
        {
            UnitTest ut = new UnitTest();
            ut.FindAndRunUTests(assemblies);
        }
        
        private void FindAndRunUTests(IEnumerable<Assembly> assemblies)
        {
            Type attrType = typeof(UTAttribute);

            foreach (var assembly in assemblies)
            {
                foreach (var _type in assembly.GetTypes())
                {
                    Type type = _type;
                    if (type.IsGenericTypeDefinition)
                    {
                        List<Type> list = new List<Type>();
                        for (int i = 0; i < type.GetGenericArguments().Length; ++i)
                        {
                            list.Add(typeof (object));
                        }

                        type = type.MakeGenericType(list.ToArray());
                    }

                    var methods = type.GetMethods(
                        BindingFlags.DeclaredOnly | BindingFlags.Static |
                        BindingFlags.NonPublic | BindingFlags.Public
                        );
                    foreach (var method in methods)
                    {
                        UTAttribute attr = System.Attribute.GetCustomAttribute(method, attrType) as UTAttribute;
                        if (attr != null)
                        {
                            ParameterInfo[] parameters = method.GetParameters();
                            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(IUTest))
                            {
                                RunUT(attr, method);
                            }
                        }
                    }
                }
            }            
        }

        private void RunUT(UTAttribute attr, MethodInfo method)
        {
            try
            {
                PrepareForTest();
                method.Invoke(null, new object[] { this });
                Log.i("Testing '{0}'... OK", attr.TestName);
            }
            catch (TargetInvocationException ex)
            {
                UTFailException utFail = ex.InnerException as UTFailException;
                if (utFail != null)
                {
                    Log.e("{0} UT FAILED: {1}", attr.TestName, mCheckText != null ? mCheckText() : "");
                    Log.e(utFail.ToString());
                }
                else
                {
                    Log.e("{0} UT FAILED!", attr.TestName);
                    Log.wtf(ex.InnerException);
                }
            }
        }

        private System.Random mRandom;
        private Func<string> mCheckText = null;

        private void PrepareForTest()
        {
            mRandom = new Random(777);
        }

        void IUTest.Assert(bool flag, string text)
        {
            mCheckText = () => text;
            if (!flag)
            {
                throw new UTFailException();
            }
            mCheckText = null;
        }

        void IUTest.Assert(bool flag)
        {
            if (!flag)
            {
                throw new UTFailException();
            }
            mCheckText = null;
        }

        void IUTest.Equal(int a, int b)
        {
            if (a != b)
            {
                throw new UTFailException();
            }
            mCheckText = null;
        }

        void IUTest.Equal(bool a, bool b)
        {
            if (a != b)
            {
                throw new UTFailException();
            }
            mCheckText = null;
        }

        void IUTest.Equal(float a, float b)
        {
            if (Math.Abs(a - b) > Eps)
            {
                throw new UTFailException();
            }
            mCheckText = null;
        }

        void IUTest.Equal(double a, double b)
        {
            if (Math.Abs(a - b) > Eps * Eps)
            {
                throw new UTFailException();
            }
            mCheckText = null;
        }

        //void IUTest.Equal(Shared.Time a, Shared.Time b)
        //{
        //    if (a.MilliSeconds != b.MilliSeconds)
        //    {
        //        throw new UTFailException();
        //    }
        //    mCheckText = null;
        //}

        //void IUTest.Equal(Geom2d.Vector a, Geom2d.Vector b)
        //{
        //    if (Geom2d.Vector.Distance(a, b) > Eps)
        //    {
        //        throw new UTFailException();
        //    }
        //    mCheckText = null;
        //}

        //void IUTest.Equal(Geom2d.Rotation a, Geom2d.Rotation b)
        //{
        //    if (Math.Abs((a - b).Angle) > Eps)
        //    {
        //        throw new UTFailException();
        //    }
        //    mCheckText = null;
        //}

        IUTest IUTest.Check(string text)
        {
            mCheckText = () => text;
            return this;
        }

        IUTest IUTest.Check(Func<string> text)
        {
            mCheckText = text;
            return this;
        }

        bool IUTest.RandomBool()
        {
            return mRandom.Next(2) == 1;
        }

        int IUTest.RandomInt(int exclusiveMax)
        {
            return mRandom.Next(exclusiveMax);
        }

        float IUTest.RandomFloat()
        {
            return (float)mRandom.NextDouble();
        }
    }
}