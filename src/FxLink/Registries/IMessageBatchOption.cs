using FxLink.Contexts;

namespace FxLink.Registries;

public interface IMessageBatchOption<out TMessage> : IOption where TMessage : class
{
    public IMessageBatchOption<TMessage> SetMessageLimit(int limit);
    public IMessageBatchOption<TMessage> SetConcurrencyLimit(int limit);
    public IMessageBatchOption<TMessage> SetTimeLimit(TimeSpan limit);
    public IMessageBatchOption<TMessage> SetTimeLimitStart(BatchTimeLimitStart timeLimitStart);

    public IMessageBatchOption<TMessage> GroupBy<TProperty>(Func<IConsumeContext<TMessage>, TProperty?> selector)
        where TProperty : struct;

    public IMessageBatchOption<TMessage> GroupBy<TProperty>(Func<IConsumeContext<TMessage>, TProperty> selector)
        where TProperty : class;
}