using System.Reflection;
using Domain.Finance.Bills;
using Domain.Finance.FinancialClosings;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Shouldly;

namespace ArchitectureTests.Layers;

public sealed class DocumentEntityProjectionTests : BaseTest
{
  private static readonly HashSet<string> ForbiddenMethodNames = new(StringComparer.Ordinal)
  {
    "ToListAsync",
    "ToArrayAsync",
    "ToList",
    "ToArray",
  };

  private static readonly HashSet<string> ForbiddenGenericArgTypeNames = new(StringComparer.Ordinal)
  {
    typeof(Bill).FullName!,
    typeof(FinancialClosing).FullName!,
  };

  [Fact]
  public void Application_Should_NotMaterializeBillOrClosingCollections()
  {
    List<string> violations = ScanAssembly(ApplicationAssembly);

    violations.ShouldBeEmpty(
      "The following method bodies materialize Bill or FinancialClosing into a List/Array " +
      "without projection. Use .Select(b => new DtoType(...)) that excludes DocumentContent:\n" +
      string.Join("\n", violations));
  }

  [Fact]
  public void Infrastructure_Should_NotMaterializeBillOrClosingCollections()
  {
    List<string> violations = ScanAssembly(InfrastructureAssembly);

    violations.ShouldBeEmpty(
      "The following method bodies materialize Bill or FinancialClosing into a List/Array " +
      "without projection. Use .Select(b => new DtoType(...)) that excludes DocumentContent:\n" +
      string.Join("\n", violations));
  }

  private static List<string> ScanAssembly(Assembly assembly)
  {
    var violations = new List<string>();

    using var assemblyDef = AssemblyDefinition.ReadAssembly(assembly.Location);

    foreach (ModuleDefinition module in assemblyDef.Modules)
    {
      foreach (TypeDefinition type in GetAllTypes(module))
      {
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

            if (!ForbiddenMethodNames.Contains(methodRef.Name))
            {
              continue;
            }

            if (methodRef is not GenericInstanceMethod genericMethod)
            {
              continue;
            }

            foreach (TypeReference genericArg in genericMethod.GenericArguments)
            {
              if (ForbiddenGenericArgTypeNames.Contains(genericArg.FullName))
              {
                violations.Add(
                  $"{type.FullName}.{method.Name} -> {methodRef.Name}<{genericArg.FullName}>");
              }
            }
          }
        }
      }
    }

    return violations;
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
