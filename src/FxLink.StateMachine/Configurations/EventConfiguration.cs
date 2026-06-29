using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.Registries;

namespace FxLink.StateMachine.Configurations;

public sealed record EventConfiguration(IActivity Event, IEventConfigurator Configurator);