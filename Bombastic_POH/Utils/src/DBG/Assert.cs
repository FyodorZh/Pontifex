using System;
//using System.Diagnostics;

namespace DBG
{
    public class Diagnostics
    {
        //static System.Text.StringBuilder sb = new System.Text.StringBuilder();

#if USE_ASSERTS
        public static bool AssertsOn = true;
#else
        public static bool AssertsOn = false;
#endif

        /// <summary>
        /// Prints an error message if assertion is false
        /// This method is removed if conditional compilation symbol "USE_ASSERTS" is not defined
        /// </summary>
        //[ConditionalAttribute("USE_ASSERTS")]
        public static void Assert(bool assertion)
        {
            if (!assertion)
            {
                var method = new System.Diagnostics.StackTrace().GetFrame(1).GetMethod();
                Log.e("Assertion failed in {class}.{method}()", method.DeclaringType.Name, method.Name);
            }
        }

        //[ConditionalAttribute("USE_ASSERTS")]
        public static void Assert(bool assertion, string message)
        {
            if (!assertion)
            {
                Log.e("Assertion failed: {message}", message);
            }
        }

        /// <summary>
        /// Prints an error message if assertion is false
        /// This method is removed if conditional compilation symbol "USE_ASSERTS" is not defined
        /// </summary>
        //[ConditionalAttribute("USE_ASSERTS")]
        public static void Assert(bool assertion, string message, params object[] args)
        {
            if (!assertion)
            {
                if (args.Length > 0)
                {
                    message = String.Format(message, args);
                }
                Log.e("Assertion failed: {message}", message);
            }
        }

//        public static string LogStackTrace()
//        {
//            string text;
//            lock (sb)
//            {
//                System.Diagnostics.StackTrace stack = new System.Diagnostics.StackTrace();
//                for (int i = 1; i < stack.FrameCount; ++i)
//                {
//                    var method = stack.GetFrame(i).GetMethod();
//                    sb.Append((method.DeclaringType != null) ? method.DeclaringType.Name : "BAD_METHOD_NAME");
//                    sb.Append(".");
//                    sb.AppendLine(method.ToString());
//                }
//                sb.AppendLine();
//                text = sb.ToString();
//                sb.Length = 0;
//            }
//            return text;
//        }
    }
 }