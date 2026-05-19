using System.Text.Json;
using Web.Api.Realtime;

namespace Web.Api.IntegrationTests.Reception;

public sealed class RealtimeEnvelopeTests
{
  [Fact]
  public void Serialize_RoundTrips_EventNameAndData()
  {
    var envelope = new RealtimeEnvelope("pair:join", JsonDocument.Parse("""{"pairCode":"abc","role":"tablet"}""").RootElement);

    string json = RealtimeEnvelope.Serialize(envelope);

    var parsed = RealtimeEnvelope.TryParse(json);
    parsed.ShouldNotBeNull();
    parsed!.Event.ShouldBe("pair:join");
    parsed.Data.GetProperty("pairCode").GetString().ShouldBe("abc");
    parsed.Data.GetProperty("role").GetString().ShouldBe("tablet");
  }

  [Fact]
  public void TryParse_MissingEventField_ReturnsNull()
  {
    var parsed = RealtimeEnvelope.TryParse("""{"data":{}}""");
    parsed.ShouldBeNull();
  }

  [Fact]
  public void TryParse_NonObjectRoot_ReturnsNull()
  {
    RealtimeEnvelope.TryParse("\"hello\"").ShouldBeNull();
    RealtimeEnvelope.TryParse("[1,2]").ShouldBeNull();
    RealtimeEnvelope.TryParse("garbage").ShouldBeNull();
  }

  [Fact]
  public void TryParse_NullData_IsTreatedAsEmptyObject()
  {
    var parsed = RealtimeEnvelope.TryParse("""{"event":"x","data":null}""");
    parsed.ShouldNotBeNull();
    parsed!.Data.ValueKind.ShouldBe(JsonValueKind.Object);
    parsed.Data.EnumerateObject().ShouldBeEmpty();
  }

  [Fact]
  public void SerializeFromObject_NullPayload_WritesEmptyDataObject()
  {
    string json = RealtimeEnvelope.SerializeFromObject("session:clear", payload: null);

    var parsed = RealtimeEnvelope.TryParse(json);
    parsed.ShouldNotBeNull();
    parsed!.Event.ShouldBe("session:clear");
    parsed.Data.ValueKind.ShouldBe(JsonValueKind.Object);
    parsed.Data.EnumerateObject().ShouldBeEmpty();
  }

  [Fact]
  public void SerializeFromObject_ObjectPayload_SerializesProperties()
  {
    string json = RealtimeEnvelope.SerializeFromObject("pair:ready", new { peerRole = "tablet" });

    var parsed = RealtimeEnvelope.TryParse(json);
    parsed.ShouldNotBeNull();
    parsed!.Event.ShouldBe("pair:ready");
    parsed.Data.GetProperty("peerRole").GetString().ShouldBe("tablet");
  }
}
