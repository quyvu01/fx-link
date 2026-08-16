namespace FxLink.Registries;

internal interface IMessageConfiguratorBehavior
{
    void AddConfigurator(Type targetType, IConsumeConfigurator configurator);
    IConsumeConfigurator[] GetConfigurators(Type targetType);

    TMessageConfigurator GetConfigurator<TMessageConfigurator>(Type targetType)
        where TMessageConfigurator : IConsumeConfigurator;
}