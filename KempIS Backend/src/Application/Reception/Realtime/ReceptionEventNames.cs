namespace Application.Reception.Realtime;

public static class ReceptionEventNames
{
  public const string PairJoin = "pair:join";
  public const string PairReady = "pair:ready";
  public const string PairPeerLeft = "pair:peer_left";
  public const string PairDisplaced = "pair:displaced";
  public const string Error = "error";

  public const string SessionPush = "session:push";
  public const string SessionClear = "session:clear";
  public const string SignatureCaptured = "signature:captured";
  public const string SignatureCleared = "signature:cleared";
  public const string GuestSigned = "guest:signed";
  public const string GuestSignatureCleared = "guest:signature_cleared";
  public const string EDokladyStart = "edoklady:start";
  public const string EDokladyTransaction = "edoklady:transaction";
  public const string EDokladyState = "edoklady:state";
  public const string EDokladyResult = "edoklady:result";
  public const string EDokladyCancel = "edoklady:cancel";
}
