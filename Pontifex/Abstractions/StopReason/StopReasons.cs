using Scriba.JsonFactory;

namespace Pontifex.StopReasons
{
    /// <summary>
    /// Unknown stop reason
    /// </summary>
    public sealed class Unknown : StopReason
    {
        public Unknown(string source) : base(source, "Unknown") { }
    }

    /// <summary>
    /// Transport stopped by business logic initiative
    /// </summary>
    public sealed class UserIntention : StopReason
    {
        private readonly string _text;

        public UserIntention(string source, string text = "") : base(source, "UserIntention")
        {
            _text = text;
        }

        public string Text => _text;

        public override void PrintTo(IJsonObject dst)
        {
            base.PrintTo(dst);
            dst.AddElement("Text", _text);
        }
    }

    /// <summary>
    /// Transport stopped by unknown remote initiative
    /// </summary>
    public sealed class UnknownRemoteIntention : StopReason
    {
        public UnknownRemoteIntention(string source) : base(source, "UnknownRemoteIntention") { }
    }

    /// <summary>
    /// Transport stopped by remote initiative
    /// </summary>
    public sealed class GracefulRemoteIntention : StopReason
    {
        public GracefulRemoteIntention(string source) : base(source, "GracefulRemoteIntention") { }
    }

    /// <summary>
    /// Remote agent does not respond
    /// </summary>
    public sealed class TimeOut : StopReason
    {
        public TimeOut(string source) : base(source, "TimeOut") { }
    }

    /// <summary>
    /// Ack was not passed
    /// </summary>
    public sealed class AckRejected : StopReason
    {
        public AckRejected(string source) : base(source, "AckRejected") { }
    }

    /// <summary>
    /// A local error of any type occurred
    /// </summary>
    public abstract class AnyFail : StopReason
    {
        protected AnyFail(string source, string failType) : base(source, failType) { }
    }

    /// <summary>
    /// An error occurred. An internal invariant was violated. There is a textual description.
    /// </summary>
    public class TextFail : AnyFail
    {
        private readonly string _text;

        protected TextFail(string source, string failType, string error, params object[] list)
            : base(source, failType)
        {
            _text = string.Format(error, list);
        }

        public TextFail(string source, string error, params object[] list)
            : this(source, "TextFail", error, list)
        {
        }

        public string Text => _text;

        public override void PrintTo(IJsonObject dst)
        {
            base.PrintTo(dst);
            dst.AddElement("Text", _text);
        }
    }

    /// <summary>
    /// An error occurred in the local business logic
    /// </summary>
    public class UserFail : TextFail
    {
        public UserFail(string error, params object[] list)
            : base("user", "UserFail", error, list)
        {
        }
    }

    /// <summary>
    /// An error occurred. An exception was thrown.
    /// </summary>
    public class ExceptionFail : AnyFail
    {
        private readonly System.Exception _exception;
        private readonly string _text;

        public ExceptionFail(string source, System.Exception exception, string text = "")
            : base(source, "ExceptionFail")
        {
            _exception = exception;
            _text = text;
        }

        public System.Exception Exception => _exception;

        public string Text => _text;

        public override void PrintTo(IJsonObject dst)
        {
            base.PrintTo(dst);
            dst.AddElement("Text", _text);
            dst.AddElement("Exception", _exception.ToString());
        }
    }

    /// <summary>
    /// An error occurred. The transport was stopped due to a chain of reasons. The reason is described in the nested StopReason.
    /// </summary>
    public sealed class ChainFail : AnyFail
    {
        private readonly StopReason _reason;
        private readonly string _text;

        public ChainFail(string source, StopReason reason, string text)
            : base(source, "ChainFail")
        {
            _reason = reason;
            _text = text;
        }

        public StopReason Reason => _reason;

        public string Text => _text;


        public override void PrintTo(IJsonObject dst)
        {
            base.PrintTo(dst);
            dst.AddElement("Text", _text);
            var nested = dst.AddObject("Nested");
            if (nested != null)
            {
                _reason.PrintTo(nested);
            }
        }
    }

    /// <summary>
    /// An error occurred. The transport was stopped due to an induced reason. The reason is described in the nested StopReason.
    /// </summary>
    public sealed class Induced : StopReason
    {
        private readonly StopReason _cause;

        public Induced(string source, StopReason cause)
            : base(source, "Induced")
        {
            _cause = cause;
        }

        public StopReason Cause => _cause;

        public override void PrintTo(IJsonObject dst)
        {
            base.PrintTo(dst);
            var cause = dst.AddObject("Cause");
            if (cause != null)
            {
                _cause.PrintTo(cause);
            }
        }
    }
}
