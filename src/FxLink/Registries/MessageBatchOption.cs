using FxLink.Contexts;

namespace FxLink.Registries;

internal abstract class MessageBatchOption
{
    internal abstract MessageBatchConfigurator GetMessageBatchConfigurator();
}

internal sealed class MessageBatchOption<TMessage> : MessageBatchOption, IMessageBatchOption<TMessage>
    where TMessage : class
{
    private IGroupKeyProvider _groupByProvider;
    private int _messageLimit = 10;
    private int _concurrentLimit = 1;
    private TimeSpan _timeLimit = TimeSpan.FromSeconds(1);
    private BatchTimeLimitStart _timeLimitStart = BatchTimeLimitStart.FromFirst;

    public IMessageBatchOption<TMessage> SetMessageLimit(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        _messageLimit = limit;
        return this;
    }

    public IMessageBatchOption<TMessage> SetConcurrencyLimit(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        _concurrentLimit = limit;
        return this;
    }

    public IMessageBatchOption<TMessage> SetTimeLimit(TimeSpan limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(limit, TimeSpan.Zero);
        _timeLimit = limit;
        return this;
    }

    public IMessageBatchOption<TMessage> SetTimeLimitStart(BatchTimeLimitStart timeLimitStart)
    {
        _timeLimitStart = timeLimitStart;
        return this;
    }

    public IMessageBatchOption<TMessage> GroupBy<TProperty>(Func<IConsumeContext<TMessage>, TProperty?> selector)
        where TProperty : struct
    {
        _groupByProvider = new ValueTypeGroupByProvider<TMessage, TProperty>(selector);
        return this;
    }

    public IMessageBatchOption<TMessage> GroupBy<TProperty>(Func<IConsumeContext<TMessage>, TProperty> selector)
        where TProperty : class
    {
        _groupByProvider = new GroupKeyProvider<TMessage, TProperty>(selector);
        return this;
    }

    internal override MessageBatchConfigurator GetMessageBatchConfigurator() =>
        new(_groupByProvider, _messageLimit, _concurrentLimit, _timeLimit, _timeLimitStart);
}