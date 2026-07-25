namespace FxLink.Registries;

internal interface IMessageConfiguratorBehavior
{
    void AddConfigurator(Type targetType, IMessageConfigurator configurator);
    IMessageConfigurator[] GetConfigurators(Type targetType);

    TMessageConfigurator GetConfigurator<TMessageConfigurator>(Type targetType)
        where TMessageConfigurator : IMessageConfigurator;
}