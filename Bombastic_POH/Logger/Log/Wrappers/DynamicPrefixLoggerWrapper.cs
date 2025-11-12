using System;
using JsonFactory;

internal class DynamicPrefixLoggerWrapper : LoggerWrapper
{
    private readonly Func<string> mTagValue;
    private readonly string mTagName;    

    public DynamicPrefixLoggerWrapper(ILogger log, string tagName, Func<string> tagValue)
        : base(log)
    {
        mTagName = tagName;
        mTagValue = tagValue;
    }

    protected override void AppendTags(IJsonArray tags)
    {
        var tag = tags.AddObject();
        tag.AddElement(mTagName, mTagValue());
    }
}

public static class DynamicPrefixLoggerWrapperExt
{
    public static ILogger Wrap(this ILogger logger, string tagName, Func<string> tagValue)
    {
        return new DynamicPrefixLoggerWrapper(logger, tagName, tagValue);
    }
}