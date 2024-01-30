using System.Text.Json.Serialization;

namespace Lanpartyseating.Desktop.Abstractions;

[JsonDerivedType(typeof(ReservationStateRequest), typeDiscriminator: "sessionstaterequest")]
[JsonDerivedType(typeof(ReservationStateResponse), typeDiscriminator: "sessionstateresponse")]
[JsonDerivedType(typeof(TextMessage), typeDiscriminator: "textmessage")]
public abstract class BaseMessage
{
}