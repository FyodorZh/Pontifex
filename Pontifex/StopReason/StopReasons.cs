using System.Text;
using Scriba.JsonFactory;

namespace Transport.StopReasons
{
    /// <summary>
    /// Причина остановки неизвестна
    /// </summary>
    public sealed class Unknown : StopReason
    {
        public Unknown(string source) : base(source, "Unknown") { }
    }

    /// <summary>
    /// Транспорт остановлен по инициативе бизнесс логики
    /// </summary>
    public sealed class UserIntention : StopReason
    {
        private readonly string mText;

        public UserIntention(string source, string text = "") : base(source, "UserIntention")
        {
            mText = text;
        }

        public string Text
        {
            get { return mText; }
        }

        public override void PrintTo(IJsonObject dst)
        {
            base.PrintTo(dst);
            dst.AddElement("Text", mText);
        }
    }

    /// <summary>
    /// Транспорт остановлен по непонятной инициативе удалённой стороны
    /// </summary>
    public sealed class UnknownRemoteIntention : StopReason
    {
        public UnknownRemoteIntention(string source) : base(source, "UnknownRemoteIntention") { }
    }

    /// <summary>
    /// Транспорт остановлен по инициативе удалённой стороны
    /// </summary>
    public sealed class GracefulRemoteIntention : StopReason
    {
        public GracefulRemoteIntention(string source) : base(source, "GracefulRemoteIntention") { }
    }

    /// <summary>
    /// Удалённый агент не отвечает
    /// </summary>
    public sealed class TimeOut : StopReason
    {
        public TimeOut(string source) : base(source, "TimeOut") { }
    }

    /// <summary>
    /// Ack не был пройден
    /// </summary>
    public sealed class AckRejected : StopReason
    {
        public AckRejected(string source) : base(source, "AckRejected") { }
    }

    /// <summary>
    /// Произошла локальная ошибка любого типа
    /// </summary>
    public abstract class AnyFail : StopReason
    {
        protected AnyFail(string source, string failType) : base(source, failType) { }
    }

    /// <summary>
    /// Произошла ошибка. Нарушен внутренний инвариант. Есть текстовое описание
    /// </summary>
    public class TextFail : AnyFail
    {
        private readonly string mText;

        protected TextFail(string source, string failType, string error, params object[] list)
            : base(source, failType)
        {
            mText = string.Format(error, list);
        }

        public TextFail(string source, string error, params object[] list)
            : this(source, "TextFail", error, list)
        {
        }

        public string Text
        {
            get { return mText; }
        }

        public override void PrintTo(IJsonObject dst)
        {
            base.PrintTo(dst);
            dst.AddElement("Text", mText);
        }
    }

    /// <summary>
    /// Ошибка в локальной бизнеслогике
    /// </summary>
    public class UserFail : TextFail
    {
        public UserFail(string error, params object[] list)
            : base("user", "UserFail", error, list)
        {
        }
    }

    /// <summary>
    /// Произошла ошибка. Брошено исключение.
    /// </summary>
    public class ExceptionFail : AnyFail
    {
        private readonly System.Exception mException;
        private readonly string mText;

        public ExceptionFail(string source, System.Exception exception, string text = "")
            : base(source, "ExceptionFail")
        {
            mException = exception;
            mText = text;
        }

        public System.Exception Exception
        {
            get { return mException; }
        }

        public string Text
        {
            get { return mText; }
        }

        public override void PrintTo(IJsonObject dst)
        {
            base.PrintTo(dst);
            dst.AddElement("Text", mText);
            dst.AddElement("Exception", mException.ToString());
        }
    }

    public sealed class ChainFail : AnyFail
    {
        private readonly StopReason mReason;
        private readonly string mText;

        public ChainFail(string source, StopReason reason, string text)
            : base(source, "ChainFail")
        {
            mReason = reason;
            mText = text;
        }

        public StopReason Reason
        {
            get { return mReason; }
        }

        public string Text
        {
            get { return mText; }
        }

        public override void PrintTo(IJsonObject dst)
        {
            base.PrintTo(dst);
            dst.AddElement("Text", mText);
            var nested = dst.AddObject("Nested");
            mReason.PrintTo(nested);
        }
    }

    public sealed class Induced : StopReason
    {
        private readonly StopReason mCause;

        public Induced(string source, StopReason cause)
            : base(source, "Induced")
        {
            mCause = cause;
        }

        public StopReason Cause
        {
            get { return mCause; }
        }

        public override void PrintTo(IJsonObject dst)
        {
            base.PrintTo(dst);
            var cause = dst.AddObject("Cause");
            mCause.PrintTo(cause);
        }
    }
}
