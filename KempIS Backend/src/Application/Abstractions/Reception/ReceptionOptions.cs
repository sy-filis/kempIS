using System.ComponentModel.DataAnnotations;

namespace Application.Abstractions.Reception;

public sealed class ReceptionOptions
{
  public const string SectionName = "Reception";

  [Range(10, 600)] public int PairCodeTtlSeconds { get; set; } = 120;

  [Range(1, 60)] public int TabletJoinGraceSeconds { get; set; } = 10;

  [Range(1024, 1_048_576)] public int SessionPushMaxBytes { get; set; } = 65_536;

  [Range(1024, 4_194_304)] public int SignaturePngMaxBytes { get; set; } = 262_144;

  [Range(512, 65_536)] public int DefaultEventMaxBytes { get; set; } = 16_384;

  [Range(10, 600)] public int AllowlistSweepIntervalSeconds { get; set; } = 60;
}
