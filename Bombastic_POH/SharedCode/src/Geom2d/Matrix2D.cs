#define USE_IDENTITY_FLAG

using System;
using System.Globalization;

namespace Geom2d
{
    public struct Matrix2D
    {
        private float _m00, _m01, _m02, _m10, _m11, _m12;
#if USE_IDENTITY_FLAG
        private bool mIsIdentity;
#endif
        public static readonly Matrix2D identity;

        private const float EPS = 1e-5f;

        static Matrix2D()
        {
            identity._m00 = 1;
            identity._m10 = 0;
            identity._m01 = 0;
            identity._m11 = 1;
            identity._m02 = 0;
            identity._m12 = 0;
#if USE_IDENTITY_FLAG
            identity.mIsIdentity = true;
#endif
        }

        public Matrix2D(float[] vList)
        {
            _m00 = vList[0];
            _m10 = vList[1];
            _m01 = vList[2];
            _m11 = vList[3];
            _m02 = vList[4];
            _m12 = vList[5];
#if USE_IDENTITY_FLAG
            mIsIdentity = false;
            mIsIdentity = IsIdentity();
#endif
        }

        public Matrix2D(float m00, float m01, float m02, float m10, float m11, float m12)
        {
            _m00 = m00;
            _m10 = m10;
            _m01 = m01;
            _m11 = m11;
            _m02 = m02;
            _m12 = m12;
#if USE_IDENTITY_FLAG
            mIsIdentity = false;
            mIsIdentity = IsIdentity();
#endif
        }

//        public Matrix2D(UnityEngine.Matrix4x4 mat)
//        {
//            _m00 = mat.m00;
//            _m10 = mat.m10;
//            _m01 = mat.m01;
//            _m11 = mat.m11;
//            _m02 = mat.m03;
//            _m12 = mat.m13;
//#if USE_IDENTITY_FLAG
//            mIsIdentity = false;
//            mIsIdentity = IsIdentity();
//#endif
//            // Check for absence of Z-scale, nonZ-rotation and Z-shift
//            DBG.Diagnostics.Assert(Math.Abs(mat.m02) + Math.Abs(mat.m12) + Math.Abs(mat.m21) + Math.Abs(mat.m20) + Math.Abs(mat.m23) < EPS && Math.Abs(mat.m22 - 1.0f) < EPS);
//        }

        //public UnityEngine.Matrix4x4 ToMatrix4D()
        //{
        //    UnityEngine.Matrix4x4 transform = UnityEngine.Matrix4x4.identity;
        //    transform.m00 = _m00;
        //    transform.m10 = _m10;
        //    transform.m01 = _m01;
        //    transform.m11 = _m11;
        //    transform.m03 = _m02;
        //    transform.m13 = _m12;
        //    return transform;
        //}

//        public static Matrix2D From3D(UnityEngine.Matrix4x4 mat)
//        {
//            Matrix2D m;
//            m._m00 = mat.m00;
//            m._m10 = mat.m10;
//            m._m01 = mat.m01;
//            m._m11 = mat.m11;
//            m._m02 = mat.m03;
//            m._m12 = mat.m13;
//#if USE_IDENTITY_FLAG
//            m.mIsIdentity = false;
//            m.mIsIdentity = m.IsIdentity();
//#endif
//            return m;
//        }

        /// <summary>
        /// Scale matrix
        /// </summary>
        public static Matrix2D Scale(float scale)
        {
            return Matrix2D.Scale(scale, scale);
        }

        /// <summary>
        /// Scale matrix
        /// </summary>
        public static Matrix2D Scale(float scaleX, float scaleY)
        {
            Matrix2D transform = Matrix2D.identity;
            transform._m00 = scaleX;
            transform._m11 = scaleY;
#if USE_IDENTITY_FLAG
            transform.mIsIdentity = false;
#endif
            return transform;
        }

        /// <summary>
        /// Translate matrix
        /// </summary>
        public static Matrix2D Translate(Geom2d.Vector position)
        {
            return Translate(position.x, position.y);
        }

        /// <summary>
        /// Translate matrix
        /// </summary>
        public static Matrix2D Translate(float dX, float dY)
        {
            Matrix2D transform = Matrix2D.identity;
            transform._m02 = dX;
            transform._m12 = dY;
#if USE_IDENTITY_FLAG
            transform.mIsIdentity = false;
#endif
            return transform;
        }

        /// <summary>
        /// Rotation matrix
        /// </summary>
        /// <param name="alfa"> In radians </param>
        public static Matrix2D Rotate(float alfa)
        {
            Matrix2D transform = Matrix2D.identity;

            float cos = (float)Math.Cos(alfa);
            float sin = (float)Math.Sin(alfa);

            transform._m00 = cos;
            transform._m11 = cos;
            transform._m10 = sin;
            transform._m01 = -sin;
#if USE_IDENTITY_FLAG
            transform.mIsIdentity = false;
#endif
            return transform;
        }

        /// <summary>
        /// Translate(dx, dy) * Scale(scaleX, scaleY)
        /// </summary>
        /// <returns></returns>
        public static Matrix2D TranslateScale(float dX, float dY, float scaleX, float scaleY)
        {
            Matrix2D transform = Matrix2D.identity;
            transform._m00 = scaleX;
            transform._m11 = scaleY;
            transform._m02 = dX;
            transform._m12 = dY;
#if USE_IDENTITY_FLAG
            transform.mIsIdentity = false;
#endif
            return transform;
        }

        public static Matrix2D TRS(float dX, float dY, float angle, float scaleX, float scaleY)
        {
            Matrix2D res = Matrix2D.Rotate(angle);

            res._m00 *= scaleX;
            res._m01 *= scaleY;
            res._m10 *= scaleX;
            res._m11 *= scaleY;
            
            res._m02 = dX;
            res._m12 = dY;
#if USE_IDENTITY_FLAG
            res.mIsIdentity = false;
#endif
            return res;
        }

        /// <summary>
        /// Translate(x0, y0) * Scale(scaleX, scaleY) * Translate(-x0, -y0)
        /// </summary>
        /// <returns></returns>
        public static Matrix2D PivotScale(float x0, float y0, float scaleX, float scaleY)
        {
            return new Matrix2D(scaleX, 0, -x0 * (scaleX - 1.0f), 0, scaleY, -y0 * (scaleY - 1.0f));
        }

        /// <summary>
        /// Translate(x0, y0) * Rotate(scaleX, scaleY) * Translate(-x0, -y0)
        /// </summary>
        /// <returns></returns>
        public static Matrix2D PivotRotate(float x0, float y0, float angle)
        {
            return Matrix2D.Translate(x0, y0).Mult(Matrix2D.Rotate(angle)).Mult(Matrix2D.Translate(-x0, -y0));
        }

        /// <summary>
        /// Translate 'form' rect to 'to' rect.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static Matrix2D MapRectToRect(Geom2d.Rect from, Geom2d.Rect to)
        {
            float sx = to.Width / from.Width;
            float sy = to.Height / from.Height;
            return Matrix2D.TranslateScale(to.pt00.x - sx * from.pt00.x, to.pt00.y - sy * from.pt00.y, sx, sy);

        }

        public float m00 { get { return _m00; } }
        public float m10 { get { return _m10; } }
        public float m01 { get { return _m01; } }
        public float m11 { get { return _m11; } }
        public float m02 { get { return _m02; } }
        public float m12 { get { return _m12; } }

        public Geom2d.Vector ExtractTranslate
        {
            get
            {
                return new Geom2d.Vector(_m02, _m12);
            }
        }

        public Geom2d.Vector ExtractScale
        {
            get
            {
                DBG.Diagnostics.Assert(IsScaleShift());
                return new Geom2d.Vector(_m00, _m11);
            }
        }

        public void SetTranslate(float dX, float dY)
        {
            _m02 = dX;
            _m12 = dY;
#if USE_IDENTITY_FLAG
            mIsIdentity = false;
#endif
        }

        public void SetScale(float scaleX, float scaleY)
        {
            _m00 = scaleX;
            _m11 = scaleY;
#if USE_IDENTITY_FLAG
            mIsIdentity = false;
#endif
        }

        /// <summary>
        /// Домножает на матрицу сдвига !слева!
        /// </summary>
        /// <param name="dX"></param>
        /// <param name="dY"></param>
		public void AddTranslateLeft(float dX, float dY)
        {
            _m02 += dX;
            _m12 += dY;
#if USE_IDENTITY_FLAG
            mIsIdentity = false;
#endif
        }

                /// <summary>
        /// Домножает на матрицу сдвига !справа!
        /// </summary>
        /// <param name="dX"></param>
        /// <param name="dY"></param>
        public void AddTranslateRight(float dX, float dY)
        {
            _m02 += _m00 * dX + _m01 * dY;
            _m12 += _m10 * dX + _m11 * dY;
#if USE_IDENTITY_FLAG
            mIsIdentity = false;
#endif
        }

        /// <summary>
        /// Домножает на матрицу растяжения !слева!
        /// </summary>
        /// <param name="scaleX"></param>
        /// <param name="scaleY"></param>
        public void AddScaleLeft(float scaleX, float scaleY)
        {
            _m00 *= scaleX;
            _m01 *= scaleX;
            _m02 *= scaleX;
            _m10 *= scaleY;
            _m11 *= scaleY;
            _m12 *= scaleY;
#if USE_IDENTITY_FLAG
            mIsIdentity = false;
#endif
        }

        /// <summary>
        /// Домножает на матрицу растяжения !справа!
        /// </summary>
        /// <param name="scaleX"></param>
        /// <param name="scaleY"></param>
        public void AddScaleRight(float scaleX, float scaleY)
        {
            _m00 *= scaleX;
            _m01 *= scaleX;
            _m10 *= scaleY;
            _m11 *= scaleY;
#if USE_IDENTITY_FLAG
            mIsIdentity = false;
#endif
        }

        public Matrix2D Invert
        {
            get
            {
#if USE_IDENTITY_FLAG
                if (mIsIdentity)
                {
                    return this;
                }
#endif
                float det = _m00 * _m11 - _m10 * _m01;
                DBG.Diagnostics.Assert(Math.Abs(det) > EPS);

                det = 1.0f / det;

                Matrix2D mat;

                mat._m00 = +det * _m11;
                mat._m01 = -det * _m01;
                mat._m10 = -det * _m10;
                mat._m11 = +det * _m00;
                mat._m02 = -(mat._m00 * _m02 + mat._m01 * _m12);
                mat._m12 = -(mat._m10 * _m02 + mat._m11 * _m12);
#if USE_IDENTITY_FLAG
                mat.mIsIdentity = false;
#endif
                return mat;
            }
        }

        public static Matrix2D ToMatrix(string sMatrix, bool bIsBase64)
        {
            float[] vNumbers = new float[6];

            if (bIsBase64)
            {
                byte[] vBytes = System.Convert.FromBase64String(sMatrix);
                System.Buffer.BlockCopy(vBytes, 0, vNumbers, 0, vBytes.Length);
            }
            else
            {
                string[] vSNumbers = sMatrix.Split(';');
                DBG.Diagnostics.Assert(vSNumbers.Length == 6);
                if (vSNumbers.Length != 6)
                {
                    DBG.Diagnostics.Assert(false);
                    return Matrix2D.identity;
                }

                for (int i = 0; i < 6; ++i)
                {
                    if (!float.TryParse(vSNumbers[i], NumberStyles.Float, CultureInfo.InvariantCulture, out vNumbers[i]))
                    {
                        DBG.Diagnostics.Assert(false);
                        return Matrix2D.identity;
                    }
                }
            }
            return new Matrix2D(vNumbers);
        }

        public string ToNiceString()
        {
            return String.Format("{0} {1} | {2}\n{3} {4} | {5}", _m00, _m01, _m02, _m10, _m11, _m12);
        }

        public override string ToString()
        {
            return ToString(false);
        }

        public string ToString(bool bIsBase64)
        {
            float[] vLIst = new float[] { _m00, _m10, _m01, _m11, _m02, _m12 };

            if (bIsBase64)
            {
                int nLen = System.Buffer.ByteLength(vLIst);
                byte[] vBytes = new byte[nLen];
                System.Buffer.BlockCopy(vLIst, 0, vBytes, 0, nLen);
                string sBytes = System.Convert.ToBase64String(vBytes);
                return sBytes;
            }
            else
            {
                string sText = "";
                for (int i = 0; i < 6; ++i)
                {
                    sText += (i != 0 ? ";" : "") + vLIst[i].ToString("G");
                }
                return sText;
            }
        }

        public bool IsIdentity()
        {
            return Math.Abs(_m00 - 1.0f) < EPS && Math.Abs(_m10) < EPS && Math.Abs(_m01) < EPS &&
                   Math.Abs(_m11 - 1.0f) < EPS && Math.Abs(_m02) < EPS && Math.Abs(_m12) < EPS;
        }

        public bool IsScaleShift()
        {
            return Math.Abs(_m01) < EPS && Math.Abs(_m10) < EPS;
        }

        public bool IsPositiveScaleAndShift()
        {
            return Math.Abs(_m01) < EPS && Math.Abs(_m10) < EPS && _m00 > EPS && _m11 > EPS;
        }

        public bool IsEqual(Matrix2D mat)
        {
            return Math.Abs(_m00 - mat._m00) < EPS && Math.Abs(_m10 - mat._m10) < EPS && Math.Abs(_m01 - mat._m01) < EPS &&
                   Math.Abs(_m11 - mat._m11) < EPS && Math.Abs(_m02 - mat._m02) < EPS && Math.Abs(_m12 - mat._m12) < EPS;
        }

        public Geom2d.Vector ApplyTo(float x, float y)
        {
            Geom2d.Vector res = new Geom2d.Vector(_m00 * x + _m01 * y + _m02, _m10 * x + _m11 * y + _m12);
            return res;
        }

        public Geom2d.Vector ApplyTo(Geom2d.Vector vec)
        {
            Geom2d.Vector res = new Geom2d.Vector(_m00 * vec.x + _m01 * vec.y + _m02, _m10 * vec.x + _m11 * vec.y + _m12);
            return res;
        }

        public Geom2d.Rect ApplyTo(Geom2d.Rect rect)
        {
            DBG.Diagnostics.Assert(IsScaleShift());
            return new Geom2d.Rect(ApplyTo(rect.pt00), ApplyTo(rect.pt11));
        }

        public Geom2d.Quad ApplyTo(Geom2d.Quad quad)
        {
            return new Geom2d.Quad(ApplyTo(quad.P0), ApplyTo(quad.P1), ApplyTo(quad.P2), ApplyTo(quad.P3));
        }

        /// <summary>
        /// Домножает справа (inplace)
        /// </summary>
        /// <param name="mat"> const ref</param>
        /// <returns></returns>
        public void MultBy(ref Matrix2D mat)
        {
#if USE_IDENTITY_FLAG
            if (mIsIdentity)
            {
                if (!mat.mIsIdentity)
                {
                    _m00 = mat._m00;
                    _m01 = mat._m01;
                    _m02 = mat._m02;
                    _m10 = mat._m10;
                    _m11 = mat._m11;
                    _m12 = mat._m12;
                    mIsIdentity = false;
                }
            }
            else
            {
                if (!mat.mIsIdentity)
                {
#endif
                    _m02 = _m00 * mat._m02 + _m01 * mat._m12 + _m02;
                    _m12 = _m10 * mat._m02 + _m11 * mat._m12 + _m12;

                    float tmp;

                    //tmp == _m00
                    tmp  = _m00 * mat._m00 + _m01 * mat._m10;
                    _m01 = _m00 * mat._m01 + _m01 * mat._m11;
                    _m00 = tmp;
                    
                    // tmp == _m10
                    tmp = _m10 * mat._m00 + _m11 * mat._m10;
                    _m11 = _m10 * mat._m01 + _m11 * mat._m11;
                    _m10 = tmp;
                }
#if USE_IDENTITY_FLAG
            }
#endif
        }

        /// <summary>
        /// Домножает слева (inplace)
        /// </summary>
        /// <param name="mat"> const ref</param>
        /// <returns></returns>
        public void MultByFromLeft(ref Matrix2D mat)
        {
#if USE_IDENTITY_FLAG
            if (mIsIdentity)
            {
                if (!mat.mIsIdentity)
                {
                    _m00 = mat._m00;
                    _m01 = mat._m01;
                    _m02 = mat._m02;
                    _m10 = mat._m10;
                    _m11 = mat._m11;
                    _m12 = mat._m12;
                    mIsIdentity = false;
                }
            }
            else
            {
                if (!mat.mIsIdentity)
                {
#endif
                    float tmp;
                    //tmp == _m02
                    tmp = mat._m00 * _m02 + mat._m01 * _m12 + mat._m02;
                    _m12 = mat._m10 * _m02 + mat._m11 * _m12 + mat._m12;
                    _m02 = tmp;
                    
                    //tmp == _m00
                    tmp = mat._m00 * _m00 + mat._m01 * _m10;
                    _m10 = mat._m10 * _m00 + mat._m11 * _m10;
                    _m00 = tmp;

                    // tmp == _m01
                    tmp = mat._m00 * _m01 + mat._m01 * _m11;
                    _m11 = mat._m10 * _m01 + mat._m11 * _m11;
                    _m01 = tmp;
                }
#if USE_IDENTITY_FLAG
            }
#endif
        }

        /// <summary>
        /// Домножает справа
        /// </summary>
        /// <param name="mat"> const ref </param>
        /// <returns></returns>
        public Matrix2D Mult(ref Matrix2D mat)
        {
#if USE_IDENTITY_FLAG
            if (mIsIdentity)
                return mat;
            if (mat.mIsIdentity)
                return this;
#endif
            Matrix2D res;
            res._m00 = _m00 * mat._m00 + _m01 * mat._m10;
            res._m01 = _m00 * mat._m01 + _m01 * mat._m11;
            res._m02 = _m00 * mat._m02 + _m01 * mat._m12 + _m02;
            res._m10 = _m10 * mat._m00 + _m11 * mat._m10;
            res._m11 = _m10 * mat._m01 + _m11 * mat._m11;
            res._m12 = _m10 * mat._m02 + _m11 * mat._m12 + _m12;
#if USE_IDENTITY_FLAG
            res.mIsIdentity = false;
#endif
            return res;
        }

        public Matrix2D Mult(Matrix2D mat)
        {
            return Mult(ref mat);
        }

        public static Matrix2D operator *(Matrix2D m1, Matrix2D m2)
        {
            return m1.Mult(ref m2);
        }

        public Geom2d.Rect ApplyToBBox(Geom2d.Rect boundingBox)
        {
#if USE_IDENTITY_FLAG
            if (mIsIdentity)
                return boundingBox;
#endif
            Geom2d.Vector v00 = ApplyTo(boundingBox.Pt00);
            Geom2d.Vector v10 = ApplyTo(boundingBox.Pt10);
            Geom2d.Vector v01 = ApplyTo(boundingBox.Pt01);
            Geom2d.Vector v11 = ApplyTo(boundingBox.Pt11);

            float x0 = Math.Min(Math.Min(v00.x, v10.x), Math.Min(v01.x, v11.x));
            float y0 = Math.Min(Math.Min(v00.y, v10.y), Math.Min(v01.y, v11.y));
            float x1 = Math.Max(Math.Max(v00.x, v10.x), Math.Max(v01.x, v11.x));
            float y1 = Math.Max(Math.Max(v00.y, v10.y), Math.Max(v01.y, v11.y));

            Geom2d.Rect res = new Geom2d.Rect(x0, y0, x1 - x0, y1 - y0);
            return res;
        }
    }

}
