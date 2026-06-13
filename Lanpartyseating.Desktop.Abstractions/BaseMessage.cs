using System.Text.Json.Serialization;

namespace Lanpartyseating.Desktop.Abstractions;

[JsonDerivedType(typeof(ReservationStateRequest), typeDiscriminator: "sessionstaterequest")]
[JsonDerivedType(typeof(ReservationStateResponse), typeDiscriminator: "sessionstateresponse")]
[JsonDerivedType(typeof(ClearAutoLogonRequest), typeDiscriminator: "clearautologonrequest")]
[JsonDerivedType(typeof(CredentialRequest), typeDiscriminator: "credentialrequest")]
[JsonDerivedType(typeof(CredentialResponse), typeDiscriminator: "credentialresponse")]
[JsonDerivedType(typeof(CredentialProviderConnected), typeDiscriminator: "credentialproviderconnected")]
[JsonDerivedType(typeof(ConnectionAcknowledged), typeDiscriminator: "connectionacknowledged")]
[JsonDerivedType(typeof(TriggerLoginRequest), typeDiscriminator: "triggerloginrequest")]
[JsonDerivedType(typeof(TextMessage), typeDiscriminator: "textmessage")]
public abstract class BaseMessage
{
}