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

    public void Ssl(Action<SslOption> sslOption)
    {
        var option = new SslOption();
        sslOption.Invoke(option);
        SslOptionValue = option;
    }
}