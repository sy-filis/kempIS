namespace Application.Reception.Realtime;

public sealed record RoomJoinOutcome(string PairCode, bool RoomReady, PeerRole? OtherPeer);
