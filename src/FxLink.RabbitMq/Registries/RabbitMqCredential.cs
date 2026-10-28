using FxLink.RabbitMq.Constants;
using RabbitMQ.Client;

namespace FxLink.RabbitMq.Registries;

public sealed class RabbitMqCredential
{
    internal string UserNameValue { get; private set; } = RabbitMqConstants.DefaultUserName;
    internal string PasswordValue { get; private set; } = RabbitMqConstants.DefaultPassword;
    internal SslOption SslOptionValue { get; private set; }

    public void UserName(string userName) => UserNameValue = userName;
    public void Password(string password) => PasswordValue = password;

    /// <summary>
    /// Connects over TLS. Calling this enables TLS (<see cref="SslOption.Enabled"/> starts as <c>true</c>);
    /// <see cref="SslOption.ServerName"/> defaults to the host given to <c>Host(...)</c> when left empty,
    /// and the port defaults to 5671 instead of 5672 when <c>Host(...)</c> was not given one explicitly.
    /// </summary>
    public void Ssl(Action<SslOption> sslOption = null)
    {
        var option = new SslOption { Enabled = true };
        sslOption?.Invoke(option);
        SslOptionValue = option;
    }
}