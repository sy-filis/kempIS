using Mono.Cecil;
using Mono.Cecil.Cil;
using Shouldly;

namespace ArchitectureTests.Layers;

public class AmbientDateTimeTests : BaseTest
{
  private static readonly HashSet<string> BannedMembers = new(StringComparer.Ordinal)
    {
        "System.DateTime::get_UtcNow",
        "System.DateTime::get_Now",
        "System.DateTime::get_Today",
        "System.DateTimeOffset::get_UtcNow",
        "System.DateTimeOffset::get_Now",
    };

  [Fact]
  public void Domain_Should_NotUse_AmbientDateTime()
  {
    string[] violations = ScanForBannedCalls(DomainAssembly.Location).ToArray();

    violations.ShouldBeEmpty(
        "Domain must obtain time through IDateTimeProvider:\n" + string.Join("\n", violations));
  }

  [Fact]
  public void Application_Should_NotUse_AmbientDateTime()
  {
    string[] violations = ScanForBannedCalls(ApplicationAssembly.Location).ToArray();

    violations.ShouldBeEmpty(
        "Application must obtain time through IDateTimeProvider:\n" + string.Join("\n", violations));
  }

  private static IEnumerable<string> ScanForBannedCalls(string assemblyPath)
  {
    using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

    foreach (ModuleDefinition module in assembly.Modules)
    {
      foreach (TypeDefinition type in GetAllTypes(module))
      {
        // Coverlet injects Coverlet.Core.Instrumentation.Tracker.* types into
        // every assembly it instruments; their internal logging uses DateTime.UtcNow.
        if (type.FullName.StartsWith("Coverlet.Core.Instrumentation.", StringComparison.Ordinal))
        {
          continue;
        }

        foreach (MethodDefinition method in type.Methods)
        {
          if (!method.HasBody)
          {
            continue;
          }

          foreach (Instruction instruction in method.Body.Instructions)
          {
            if (instruction.Operand is not MethodReference methodRef)
            {
              continue;
            }

            string key = $"{methodRef.DeclaringType.FullName}::{methodRef.Name}";
            if (BannedMembers.Contains(key))
            {
              yield return $"{type.FullName}.{method.Name} -> {key}";
            }
          }
        }
      }
    }
  }

  private static IEnumerable<TypeDefinition> GetAllTypes(ModuleDefinition module)
  {
    foreach (TypeDefinition type in module.Types)
    {
      yield return type;
      foreach (TypeDefinition nested in GetNested(type))
      {
        yield return nested;
      }
    }
  }

  private static IEnumerable<TypeDefinition> GetNested(TypeDefinition type)
  {
    foreach (TypeDefinition nested in type.NestedTypes)
    {
      yield return nested;
      foreach (TypeDefinition deeper in GetNested(nested))
      {
        yield return deeper;
      }
    }
  }
}
